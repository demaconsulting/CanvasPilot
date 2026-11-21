// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.PayloadSubsystem;

/// <summary>
///     Draws the marks of a message on the picture they were drawn on, numbered, and cuts out the area around each mark,
///     with CanvasNet, without a window and without writing a file.
/// </summary>
/// <remarks>
///     <para>
///         The marks are drawn by building an SVG document in memory that embeds the picture the user saw, as the PNG
///         the render service makes of its pixels, and draws every mark over it: each mark as a line in a color that
///         belongs to its number, with a white line under it so that it can be seen on any background, and a round
///         badge with the number at the top-left of the mark. CanvasNet draws that document to a surface of the size of
///         the picture, which is encoded as the annotated picture and cut for the areas. The picture the user saw is
///         the starting point, so the pixels away from the marks are the pixels that were shown.
///     </para>
///     <para>
///         The area of a mark is its bounding box and its badge with a margin of <see cref="CropPadding" /> pixels, at
///         least <see cref="MinimumCropSize" /> pixels in each direction, moved inside the picture, and the whole
///         picture when it is smaller. The areas are cut from the annotated picture, so the other marks that fall in an
///         area are drawn in it. The composer holds no state and is safe for concurrent use.
///     </para>
/// </remarks>
public sealed class MarkImageComposer : IMarkImageComposer
{
    /// <summary>
    ///     The margin, in pixels, around a mark and its badge in the area that is cut out for the mark.
    /// </summary>
    public const int CropPadding = 16;

    /// <summary>
    ///     The smallest size, in pixels, of the area that is cut out for a mark, in each direction.
    /// </summary>
    public const int MinimumCropSize = 64;

    /// <summary>
    ///     The colors of the marks, which repeat after six marks: red, blue, green, orange, purple, and teal. Each is dark
    ///     enough for white text and distinct from the others.
    /// </summary>
    private static readonly string[] Palette = ["#e53935", "#1e88e5", "#43a047", "#fb8c00", "#8e24aa", "#00acc1"];

    /// <summary>
    ///     The angle, in radians, between the shaft of an arrow and each of the two lines of its head.
    /// </summary>
    private static readonly double ArrowHeadAngle = 28 * Math.PI / 180;

    /// <summary>
    ///     Makes PNG files of pictures; the render service, so that pictures are encoded in one place only.
    /// </summary>
    private readonly IImageRenderService _renderer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MarkImageComposer" /> class.
    /// </summary>
    /// <param name="renderer">The service that encodes the picture as PNG for the drawing.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderer" /> is <see langword="null" />.</exception>
    public MarkImageComposer(IImageRenderService renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        _renderer = renderer;
    }

    /// <inheritdoc />
    public MarkedPictureImages Compose(RenderedImage picture, IReadOnlyList<Mark> marks)
    {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentNullException.ThrowIfNull(marks);

        // Every mark must belong to this exact picture, or its geometry would be drawn on the wrong pixels
        var foreign = marks.FirstOrDefault(mark => !string.Equals(mark.Snapshot.SnapshotKey, picture.SnapshotKey, StringComparison.Ordinal));
        if (foreign is not null)
        {
            throw new ArgumentException($"Mark {foreign.Number} was drawn on another picture.", nameof(marks));
        }

        // Without marks the annotated picture is the picture itself
        var ordered = marks.OrderBy(mark => mark.Number).ToList();
        var basePng = _renderer.EncodePng(picture);
        if (ordered.Count == 0)
        {
            return new MarkedPictureImages(basePng, []);
        }

        // Draw the marks over the picture with CanvasNet and read the result back as a surface of the same size
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(BuildSvg(picture, basePng, ordered)));
        using var drawn = Draw(stream);
        if (drawn.Surface.Width != picture.Width || drawn.Surface.Height != picture.Height)
        {
            throw new InvalidOperationException(
                $"The marks were drawn at {drawn.Surface.Width} x {drawn.Surface.Height} pixels instead of {picture.Width} x {picture.Height}.");
        }

        // Encode the whole picture, then cut the area of each mark from it
        var annotated = Encode(drawn.Surface);
        var radius = BadgeRadius(picture.Width, picture.Height);
        var crops = ImmutableArray.CreateBuilder<MarkCrop>(ordered.Count);
        foreach (var mark in ordered)
        {
            var area = CropAreaFor(mark.Geometry.BoundingBox, picture.Width, picture.Height, radius);
            using var cut = drawn.Surface.Crop((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
            crops.Add(new MarkCrop(mark.Number, Encode(cut), area));
        }

        return new MarkedPictureImages(annotated, crops.MoveToImmutable());
    }

    /// <summary>
    ///     Gets the color of the mark with a number.
    /// </summary>
    /// <param name="number">The number of the mark, one or more.</param>
    /// <returns>The color as <c>#rrggbb</c>.</returns>
    internal static string ColorOf(int number)
    {
        return Palette[(number - 1) % Palette.Length];
    }

    /// <summary>
    ///     Gets the width of the lines of marks on a picture. It grows with the picture so that marks stay visible on a
    ///     large picture and do not hide a small one.
    /// </summary>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The width in pixels, from 3 to 8.</returns>
    internal static double StrokeWidth(int width, int height)
    {
        return Math.Clamp(Math.Round(Math.Min(width, height) / 80.0), 3, 8);
    }

    /// <summary>
    ///     Gets the radius of the badge that shows the number of a mark.
    /// </summary>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The radius in pixels, from 9 to 20.</returns>
    internal static double BadgeRadius(int width, int height)
    {
        return Math.Clamp(Math.Round(Math.Min(width, height) * 0.035), 9, 20);
    }

    /// <summary>
    ///     Gets the center of the badge of a mark: the top-left corner of its bounding box, moved so that the whole badge
    ///     is inside the picture.
    /// </summary>
    /// <param name="box">The bounding box of the mark.</param>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The center of the badge.</returns>
    internal static RenderPoint BadgeCenter(RenderBox box, int width, int height)
    {
        var radius = BadgeRadius(width, height);
        return new RenderPoint(
            Math.Clamp(box.X, Math.Min(radius, width / 2.0), Math.Max(width - radius, width / 2.0)),
            Math.Clamp(box.Y, Math.Min(radius, height / 2.0), Math.Max(height - radius, height / 2.0)));
    }

    /// <summary>
    ///     Gets the area, in whole picture pixels, that is cut out for a mark.
    /// </summary>
    /// <param name="box">The bounding box of the mark.</param>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <param name="radius">The radius of the badge.</param>
    /// <returns>The area: the box and the badge with a margin, at least the minimum size, inside the picture.</returns>
    internal static RenderBox CropAreaFor(RenderBox box, int width, int height, double radius)
    {
        // The area holds the box, the badge, and the margin
        var badge = BadgeCenter(box, width, height);
        var left = (int)Math.Floor(Math.Min(box.X, badge.X - radius) - CropPadding);
        var top = (int)Math.Floor(Math.Min(box.Y, badge.Y - radius) - CropPadding);
        var right = (int)Math.Ceiling(Math.Max(box.X + box.Width, badge.X + radius) + CropPadding);
        var bottom = (int)Math.Ceiling(Math.Max(box.Y + box.Height, badge.Y + radius) + CropPadding);

        // Grow a small area around its center to the minimum size, then fit it into the picture
        var areaWidth = Math.Min(Math.Max(right - left, MinimumCropSize), width);
        var areaHeight = Math.Min(Math.Max(bottom - top, MinimumCropSize), height);
        var x = Math.Clamp(left - ((areaWidth - (right - left)) / 2), 0, width - areaWidth);
        var y = Math.Clamp(top - ((areaHeight - (bottom - top)) / 2), 0, height - areaHeight);
        return new RenderBox(x, y, areaWidth, areaHeight);
    }

    /// <summary>
    ///     Draws an SVG document with CanvasNet.
    /// </summary>
    /// <param name="document">The SVG document.</param>
    /// <returns>The drawn picture, which the caller disposes.</returns>
    /// <exception cref="InvalidOperationException">Thrown when CanvasNet cannot draw the document or reports a problem with it.</exception>
    private static ViewerImage Draw(MemoryStream document)
    {
        ViewerImage drawn;
        try
        {
            drawn = CanvasNetViewer.Render(document, "marks.svg", new ViewerRenderOptions());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("The marks could not be drawn on the picture.", ex);
        }

        // A problem such as a picture that was not drawn would leave an annotated picture that is not what the user saw
        if (drawn.Diagnostics.Count > 0)
        {
            var first = drawn.Diagnostics[0];
            drawn.Dispose();
            throw new InvalidOperationException($"The marks could not be drawn on the picture: {first.Code}: {first.Message}");
        }

        return drawn;
    }

    /// <summary>
    ///     Encodes a surface as a PNG file.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The bytes of the file.</returns>
    private static byte[] Encode(Surface surface)
    {
        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream);
        return stream.ToArray();
    }

    /// <summary>
    ///     Builds the SVG document that draws the picture and the marks over it.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="picturePng">The PNG file of the picture.</param>
    /// <param name="marks">The marks, in the order of their numbers.</param>
    /// <returns>The document, in the pixels of the picture.</returns>
    private static string BuildSvg(RenderedImage picture, byte[] picturePng, IReadOnlyList<Mark> marks)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='{picture.Width}' height='{picture.Height}'>");
        builder.Append(CultureInfo.InvariantCulture, $"<image href='data:image/png;base64,{Convert.ToBase64String(picturePng)}' x='0' y='0' width='{picture.Width}' height='{picture.Height}'/>");

        // Draw each mark twice, a white line first so that the color shows on any background
        var stroke = StrokeWidth(picture.Width, picture.Height);
        foreach (var mark in marks)
        {
            AppendShape(builder, mark.Geometry, "#ffffff", stroke + 3, stroke);
            AppendShape(builder, mark.Geometry, ColorOf(mark.Number), stroke, stroke);
        }

        // Draw the badges last, so that no line covers a number
        foreach (var mark in marks)
        {
            AppendBadge(builder, mark, picture.Width, picture.Height);
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    /// <summary>
    ///     Appends the lines of one shape.
    /// </summary>
    /// <param name="builder">The document under construction.</param>
    /// <param name="geometry">The shape.</param>
    /// <param name="color">The color of the lines.</param>
    /// <param name="width">The width of the lines.</param>
    /// <param name="stroke">
    ///     The width of the lines of the mark itself, from which the size of an arrow head is taken, so that the white lines
    ///     under a mark have exactly the shape of the colored lines over them.
    /// </param>
    private static void AppendShape(StringBuilder builder, MarkGeometry geometry, string color, double width, double stroke)
    {
        var paint = $"fill='none' stroke='{color}' stroke-width='{Number(width)}' stroke-linecap='round' stroke-linejoin='round'";
        var box = geometry.BoundingBox;
        switch (geometry.Kind)
        {
            case MarkKind.Rectangle:
                builder.Append($"<rect x='{Number(box.X)}' y='{Number(box.Y)}' width='{Number(box.Width)}' height='{Number(box.Height)}' {paint}/>");
                break;

            case MarkKind.Ellipse:
                builder.Append($"<ellipse cx='{Number(box.X + (box.Width / 2))}' cy='{Number(box.Y + (box.Height / 2))}' rx='{Number(box.Width / 2)}' ry='{Number(box.Height / 2)}' {paint}/>");
                break;

            case MarkKind.Arrow:
                AppendArrow(builder, geometry.Points[0], geometry.Points[^1], stroke, paint);
                break;

            default:
                builder.Append($"<polyline points='{Points(geometry.Points)}' {paint}/>");
                break;
        }
    }

    /// <summary>
    ///     Appends the shaft and the head of an arrow.
    /// </summary>
    /// <param name="builder">The document under construction.</param>
    /// <param name="tail">The tail of the arrow.</param>
    /// <param name="head">The head of the arrow, the point it points at.</param>
    /// <param name="stroke">The width of the lines of the mark, from which the size of the head is taken.</param>
    /// <param name="paint">The attributes that paint the lines.</param>
    private static void AppendArrow(StringBuilder builder, RenderPoint tail, RenderPoint head, double stroke, string paint)
    {
        // The shaft
        builder.Append($"<polyline points='{Points([tail, head])}' {paint}/>");

        // The head: two lines back from the tip at the head angle, as long as five line widths but not longer than the shaft
        var length = MarkGeometry.Distance(tail, head);
        var backX = (tail.X - head.X) / length;
        var backY = (tail.Y - head.Y) / length;
        var wing = Math.Min(Math.Max(12, stroke * 5), length);
        var cos = Math.Cos(ArrowHeadAngle);
        var sin = Math.Sin(ArrowHeadAngle);
        var first = new RenderPoint(head.X + (wing * ((backX * cos) - (backY * sin))), head.Y + (wing * ((backX * sin) + (backY * cos))));
        var second = new RenderPoint(head.X + (wing * ((backX * cos) + (backY * sin))), head.Y + (wing * ((-backX * sin) + (backY * cos))));
        builder.Append($"<polyline points='{Points([first, head])}' {paint}/>");
        builder.Append($"<polyline points='{Points([second, head])}' {paint}/>");
    }

    /// <summary>
    ///     Appends the badge that shows the number of a mark: a filled circle in the color of the mark with a white edge
    ///     and the number in white.
    /// </summary>
    /// <param name="builder">The document under construction.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    private static void AppendBadge(StringBuilder builder, Mark mark, int width, int height)
    {
        var radius = BadgeRadius(width, height);
        var center = BadgeCenter(mark.Geometry.BoundingBox, width, height);
        builder.Append($"<circle cx='{Number(center.X)}' cy='{Number(center.Y)}' r='{Number(radius)}' fill='{ColorOf(mark.Number)}' stroke='#ffffff' stroke-width='2'/>");

        // Scale the text to the number of digits so that it stays inside the circle, and center it by the average width
        // of a digit, which does not depend on a font that supports text anchors
        var label = mark.Number.ToString(CultureInfo.InvariantCulture);
        var fontSize = radius * (label.Length switch { 1 => 1.3, 2 => 1.05, _ => 0.8 });
        var x = center.X - (label.Length * fontSize * 0.55 / 2);
        var y = center.Y + (fontSize * 0.36);
        builder.Append($"<text x='{Number(x)}' y='{Number(y)}' font-size='{Number(fontSize)}' font-family='sans-serif' font-weight='bold' fill='#ffffff'>{label}</text>");
    }

    /// <summary>
    ///     Writes points as the value of an SVG <c>points</c> attribute.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>The pairs of coordinates separated by spaces.</returns>
    private static string Points(IEnumerable<RenderPoint> points)
    {
        return string.Join(' ', points.Select(point => $"{Number(point.X)},{Number(point.Y)}"));
    }

    /// <summary>
    ///     Writes a number for an SVG document: invariant culture, at most three decimals.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Number(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

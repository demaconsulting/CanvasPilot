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

using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     How a mark looks on the picture on the screen: the colors, the width of the lines, the size and place of the
///     number badge, and the size of the head of an arrow. They are the same as the ones with which the annotated picture
///     of a sent message is drawn, so that the user sees what the assistant gets; the payload subsystem has its own copy,
///     because it must not depend on the user interface, and a test keeps the two equal.
/// </summary>
internal static class AnnotationOverlayStyle
{
    /// <summary>
    ///     The angle between the shaft of an arrow and each line of its head, in radians.
    /// </summary>
    public static readonly double ArrowHeadAngle = 28 * Math.PI / 180;

    /// <summary>
    ///     The colors of the marks, which repeat after six marks: red, blue, green, orange, purple, and teal.
    /// </summary>
    private static readonly string[] Palette = ["#e53935", "#1e88e5", "#43a047", "#fb8c00", "#8e24aa", "#00acc1"];

    /// <summary>
    ///     Gets the color of the mark with a number.
    /// </summary>
    /// <param name="number">The number of the mark, one or more.</param>
    /// <returns>The color as <c>#rrggbb</c>.</returns>
    public static string ColorOf(int number)
    {
        return Palette[(number - 1) % Palette.Length];
    }

    /// <summary>
    ///     Gets the width of the lines of marks on a picture, which grows with the picture.
    /// </summary>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The width in pixels, from 3 to 8.</returns>
    public static double StrokeWidth(int width, int height)
    {
        return Math.Clamp(Math.Round(Math.Min(width, height) / 80.0), 3, 8);
    }

    /// <summary>
    ///     Gets the radius of the badge that shows the number of a mark.
    /// </summary>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The radius in pixels, from 9 to 20.</returns>
    public static double BadgeRadius(int width, int height)
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
    public static RenderPoint BadgeCenter(RenderBox box, int width, int height)
    {
        var radius = BadgeRadius(width, height);
        return new RenderPoint(
            Math.Clamp(box.X, Math.Min(radius, width / 2.0), Math.Max(width - radius, width / 2.0)),
            Math.Clamp(box.Y, Math.Min(radius, height / 2.0), Math.Max(height - radius, height / 2.0)));
    }

    /// <summary>
    ///     Gets the length of each line of the head of an arrow.
    /// </summary>
    /// <param name="stroke">The width of the lines of the mark.</param>
    /// <param name="shaftLength">The length of the shaft, which the head is not longer than.</param>
    /// <returns>The length in pixels.</returns>
    public static double ArrowWing(double stroke, double shaftLength)
    {
        return Math.Min(Math.Max(12, stroke * 5), shaftLength);
    }

    /// <summary>
    ///     Gets the size of the text of the note that is shown beside the badge of a mark, which grows with the badge.
    ///     The note label is drawn only on the screen, never on the pictures sent to the assistant, so the payload
    ///     subsystem has no copy of this.
    /// </summary>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The size in pixels, from 8 to 18.</returns>
    public static double NoteFontSize(int width, int height)
    {
        return Math.Round(BadgeRadius(width, height) * 0.9);
    }

    /// <summary>
    ///     Gets the widest the note label beside a badge may be, so that a long note stays a short line on the picture.
    /// </summary>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The width in pixels: 40 % of the picture, at least six and at most twenty-four badge radii.</returns>
    public static double NoteMaxWidth(int width, int height)
    {
        var radius = BadgeRadius(width, height);
        return Math.Max(radius * 6, Math.Min(width * 0.4, radius * 24));
    }

    /// <summary>
    ///     Gets where the note label of a mark goes: beside its badge on the side that has room, at the height of the
    ///     badge, and no wider than the room it has inside the picture.
    /// </summary>
    /// <param name="box">The bounding box of the mark.</param>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The place and the widest width of the label.</returns>
    public static NoteLabelLayout NoteLabel(RenderBox box, int width, int height)
    {
        const double inset = 2;
        var center = BadgeCenter(box, width, height);
        var radius = BadgeRadius(width, height);
        var gap = Math.Max(2, radius * 0.3);
        var maximum = NoteMaxWidth(width, height);

        // The room to the right and to the left of the badge, inside the picture
        var right = width - inset - (center.X + radius + gap);
        var left = center.X - radius - gap - inset;

        // The right side is preferred; the left is used when the right is too narrow and the left has more room
        var useRight = right >= Math.Min(maximum, radius * 6) || right >= left;
        var room = useRight ? right : left;
        var edge = useRight ? center.X + radius + gap : center.X - radius - gap;
        return new NoteLabelLayout(edge, center.Y - radius, radius * 2, Math.Max(0, Math.Min(maximum, room)), !useRight);
    }
}

/// <summary>
///     Where the note label of a mark goes on the picture, in picture pixels.
/// </summary>
/// <param name="Edge">
///     The left edge of the label when it is to the right of the badge, or its right edge when it is to the left.
/// </param>
/// <param name="Top">The top of the label, which is the top of the badge.</param>
/// <param name="Height">The height of the label, which is the height of the badge, so the two are centered together.</param>
/// <param name="MaxWidth">The widest the label may be, so that it stays inside the picture; zero or more.</param>
/// <param name="LeftOfBadge">Whether the label is to the left of the badge.</param>
internal readonly record struct NoteLabelLayout(double Edge, double Top, double Height, double MaxWidth, bool LeftOfBadge);

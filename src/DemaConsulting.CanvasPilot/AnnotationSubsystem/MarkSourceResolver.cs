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
///     Finds which parts of a picture's source file lie under a mark, from the element positions and anchors that the
///     renderer reported with the picture, so that the assistant can be told which nodes, bars, or anchors the user
///     pointed at and not only which pixels.
/// </summary>
/// <remarks>
///     <para>
///         Only some pictures know where their parts are: Mermaid diagrams and Vega-Lite charts report the box of every
///         node, edge, mark, and axis, and OpenSCAD models report the pixel of every anchor they declare. For every other
///         kind of picture the resolver says so, with the reason, instead of returning an empty list that would claim
///         that nothing lies under the mark.
///     </para>
///     <para>
///         The rules are: a rectangle or an ellipse covers the elements whose box it overlaps by at least a quarter of
///         the box, or whose center it contains; an arrow covers the element that holds its head, and the element that
///         holds its tail, less; a freehand mark covers the elements that its stroke crosses. Elements are ordered by how
///         much of them the mark covers, then smaller boxes first, then in the order the renderer reported them, and at
///         most <see cref="MaximumElements" /> are returned. A box is the bounding box of the element, so the box of a
///         diagonal edge also covers the empty corners around it; this is a limit of the information the renderer gives.
///         The resolver is a static class without state, so it is safe for concurrent use.
///     </para>
/// </remarks>
public static class MarkSourceResolver
{
    /// <summary>
    ///     The most elements the resolver returns for one mark, so that a mark over a crowded diagram stays a short list.
    /// </summary>
    public const int MaximumElements = 25;

    /// <summary>
    ///     The part of an element's box that a rectangle or an ellipse must cover for the element to count as under it.
    /// </summary>
    public const double OverlapThreshold = 0.25;

    /// <summary>
    ///     The largest distance, in pixels, between the head of an arrow and an anchor for the arrow to point at the anchor.
    /// </summary>
    public const double ArrowAnchorDistance = 24;

    /// <summary>
    ///     The largest distance, in pixels, between a freehand stroke and an anchor for the stroke to mark the anchor.
    /// </summary>
    public const double FreehandAnchorDistance = 8;

    /// <summary>
    ///     How many sample points per side are used to measure how much of an element an ellipse covers.
    /// </summary>
    private const int EllipseSamples = 16;

    /// <summary>
    ///     The score of an element that is under a mark by the rules but has almost no overlap, so that it still ranks
    ///     above elements that are not under it.
    /// </summary>
    private const double MinimumScore = 0.0001;

    /// <summary>
    ///     The score of the element that holds the tail of an arrow, as a part of the score of one that holds its head.
    /// </summary>
    private const double TailScore = 0.5;

    /// <summary>
    ///     The kinds of picture whose elements the renderer reports positions for, which decides the wording of the reason
    ///     when none were reported.
    /// </summary>
    private static readonly string[] PositionFormats = ["mermaid", "vega-lite"];

    /// <summary>
    ///     Finds the parts of the source file that lie under a mark.
    /// </summary>
    /// <param name="mark">The mark, which carries the picture it was drawn on and its geometry in that picture's pixels.</param>
    /// <returns>
    ///     The parts under the mark, most relevant first, or the statement that the picture cannot say, with the reason.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mark" /> is <see langword="null" />.</exception>
    public static SourceElementSearchResult Resolve(Mark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);

        // Use the element positions when the renderer reported any, otherwise the anchors that have a position
        var picture = mark.Snapshot;
        if (picture.Positions.Count > 0)
        {
            return ResolvePositions(mark.Geometry, picture);
        }

        if (picture.Anchors.Any(anchor => anchor.PixelX is not null && anchor.PixelY is not null))
        {
            return ResolveAnchors(mark.Geometry, picture);
        }

        // Nothing is known about where the parts of this picture are: say so, and why
        return SourceElementSearchResult.NotAvailable(UnavailableReasonFor(picture.Format));
    }

    /// <summary>
    ///     Gets the reason a picture of a format has no element positions.
    /// </summary>
    /// <param name="format">The name of the format that was drawn.</param>
    /// <returns>The reason.</returns>
    private static string UnavailableReasonFor(string format)
    {
        if (PositionFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
        {
            return $"the renderer reported no element positions for this {format} picture";
        }

        if (string.Equals(format, "scad", StringComparison.OrdinalIgnoreCase))
        {
            return "the model declares no anchors that appear in the picture";
        }

        return format.Length == 0 ? "this kind of file provides no element positions" : $"{format} provides no element positions";
    }

    /// <summary>
    ///     Finds the elements of a diagram or chart that lie under a mark.
    /// </summary>
    /// <param name="geometry">Where the mark is.</param>
    /// <param name="picture">The picture with its element positions.</param>
    /// <returns>The result.</returns>
    private static SourceElementSearchResult ResolvePositions(MarkGeometry geometry, RenderedImage picture)
    {
        // Score every element that has a box; an element without a box cannot be placed under anything
        var candidates = new List<(RenderPosition Element, double Score, double Area, int Index)>();
        for (var i = 0; i < picture.Positions.Count; i++)
        {
            var element = picture.Positions[i];
            if (element.Box is null)
            {
                continue;
            }

            var box = Inflate(element.Box.Value);
            var score = Score(geometry, box);
            if (score > 0)
            {
                candidates.Add((element, score, box.Width * box.Height, i));
            }
        }

        // Most covered first, then the most specific (smallest) element, then the renderer's own order
        var ordered = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Area)
            .ThenBy(candidate => candidate.Index)
            .ToList();
        var truncated = ordered.Count > MaximumElements || picture.PositionsTruncated;
        var references = ordered
            .Take(MaximumElements)
            .Select(candidate => new SourceElementReference(
                candidate.Element.Kind,
                candidate.Element.Id,
                candidate.Element.Text,
                candidate.Element.Line,
                candidate.Element.Box,
                candidate.Element.Pixel));
        return SourceElementSearchResult.Found(references, truncated);
    }

    /// <summary>
    ///     Finds the anchors of a model that lie under a mark.
    /// </summary>
    /// <param name="geometry">Where the mark is.</param>
    /// <param name="picture">The picture with its anchors.</param>
    /// <returns>The result.</returns>
    private static SourceElementSearchResult ResolveAnchors(MarkGeometry geometry, RenderedImage picture)
    {
        // Take the anchors that have a place in the picture and are under the mark, with their distance from the mark
        var candidates = new List<(RenderAnchor Anchor, RenderPoint Point, double Distance, int Index)>();
        var reference = ReferencePoint(geometry);
        for (var i = 0; i < picture.Anchors.Count; i++)
        {
            var anchor = picture.Anchors[i];
            if (anchor.PixelX is not { } x || anchor.PixelY is not { } y)
            {
                continue;
            }

            var point = new RenderPoint(x, y);
            if (IsAnchorUnder(geometry, point))
            {
                candidates.Add((anchor, point, MarkGeometry.Distance(point, reference), i));
            }
        }

        // The nearest anchor first; an arrow points at one thing, so it names only the nearest
        var ordered = candidates.OrderBy(candidate => candidate.Distance).ThenBy(candidate => candidate.Index).ToList();
        if (geometry.Kind == MarkKind.Arrow && ordered.Count > 1)
        {
            ordered = [ordered[0]];
        }

        var truncated = ordered.Count > MaximumElements;
        var references = ordered
            .Take(MaximumElements)
            .Select(candidate => new SourceElementReference("anchor", candidate.Anchor.Name, null, null, null, candidate.Point));
        return SourceElementSearchResult.Found(references, truncated);
    }

    /// <summary>
    ///     Gets how much of an element's box a mark covers by the rules of its shape.
    /// </summary>
    /// <param name="geometry">Where the mark is.</param>
    /// <param name="box">The box of the element.</param>
    /// <returns>A score above zero when the element is under the mark, and zero when it is not; larger is more covered.</returns>
    private static double Score(MarkGeometry geometry, RenderBox box)
    {
        var region = geometry.BoundingBox;
        var center = new RenderPoint(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        switch (geometry.Kind)
        {
            case MarkKind.Rectangle:
                var overlap = IntersectionArea(region, box) / (box.Width * box.Height);
                return overlap >= OverlapThreshold || ContainsPoint(region, center) ? Math.Max(overlap, MinimumScore) : 0;

            case MarkKind.Ellipse:
                var coverage = EllipseCoverage(region, box);
                return coverage >= OverlapThreshold || EllipseContains(region, center) ? Math.Max(coverage, MinimumScore) : 0;

            case MarkKind.Arrow:
                if (ContainsPoint(box, geometry.Points[^1]))
                {
                    return 1;
                }

                return ContainsPoint(box, geometry.Points[0]) ? TailScore : 0;

            default:
                var crossed = StrokeLengthInside(geometry.Points, box);
                if (crossed < 0)
                {
                    return 0;
                }

                var total = StrokeLength(geometry.Points);
                return Math.Max(total == 0 ? 0 : crossed / total, MinimumScore);
        }
    }

    /// <summary>
    ///     Determines whether an anchor lies under a mark by the rules of its shape.
    /// </summary>
    /// <param name="geometry">Where the mark is.</param>
    /// <param name="point">The pixel of the anchor.</param>
    /// <returns><see langword="true" /> when the anchor is under the mark.</returns>
    private static bool IsAnchorUnder(MarkGeometry geometry, RenderPoint point)
    {
        return geometry.Kind switch
        {
            MarkKind.Rectangle => ContainsPoint(geometry.BoundingBox, point),
            MarkKind.Ellipse => EllipseContains(geometry.BoundingBox, point),
            MarkKind.Arrow => MarkGeometry.Distance(point, geometry.Points[^1]) <= ArrowAnchorDistance,
            _ => DistanceToStroke(point, geometry.Points) <= FreehandAnchorDistance
        };
    }

    /// <summary>
    ///     Gets the point that anchors are ordered by: the center of a rectangle or an ellipse, the head of an arrow, and
    ///     the middle point of the stroke of a freehand mark.
    /// </summary>
    /// <param name="geometry">Where the mark is.</param>
    /// <returns>The point.</returns>
    private static RenderPoint ReferencePoint(MarkGeometry geometry)
    {
        var box = geometry.BoundingBox;
        return geometry.Kind switch
        {
            MarkKind.Arrow => geometry.Points[^1],
            MarkKind.Freehand => geometry.Points[geometry.Points.Length / 2],
            _ => new RenderPoint(box.X + (box.Width / 2), box.Y + (box.Height / 2))
        };
    }

    /// <summary>
    ///     Makes a box at least one pixel wide and high around its own center, so that the straight lines that edges are
    ///     reported as have an area to be covered.
    /// </summary>
    /// <param name="box">The box.</param>
    /// <returns>The box, or a one-pixel wide or high version of it.</returns>
    private static RenderBox Inflate(RenderBox box)
    {
        var width = Math.Max(box.Width, 1);
        var height = Math.Max(box.Height, 1);
        return new RenderBox(box.X - ((width - box.Width) / 2), box.Y - ((height - box.Height) / 2), width, height);
    }

    /// <summary>
    ///     Determines whether a box holds a point, edges included.
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true" /> when the point is in the box.</returns>
    private static bool ContainsPoint(RenderBox box, RenderPoint point)
    {
        return point.X >= box.X && point.X <= box.X + box.Width && point.Y >= box.Y && point.Y <= box.Y + box.Height;
    }

    /// <summary>
    ///     Gets the area two boxes have in common.
    /// </summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <returns>The area, zero when they do not overlap.</returns>
    private static double IntersectionArea(RenderBox a, RenderBox b)
    {
        var width = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var height = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        return width > 0 && height > 0 ? width * height : 0;
    }

    /// <summary>
    ///     Determines whether the ellipse inscribed in a box holds a point.
    /// </summary>
    /// <param name="bounds">The bounding box of the ellipse.</param>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true" /> when the point is inside the ellipse or on it.</returns>
    private static bool EllipseContains(RenderBox bounds, RenderPoint point)
    {
        var rx = bounds.Width / 2;
        var ry = bounds.Height / 2;
        var dx = (point.X - (bounds.X + rx)) / rx;
        var dy = (point.Y - (bounds.Y + ry)) / ry;
        return (dx * dx) + (dy * dy) <= 1;
    }

    /// <summary>
    ///     Estimates the part of a box that an ellipse covers by testing a regular grid of points in the box.
    /// </summary>
    /// <param name="ellipse">The bounding box of the ellipse.</param>
    /// <param name="box">The box.</param>
    /// <returns>The part of the box that is inside the ellipse, from zero to one.</returns>
    private static double EllipseCoverage(RenderBox ellipse, RenderBox box)
    {
        var inside = 0;
        for (var row = 0; row < EllipseSamples; row++)
        {
            for (var column = 0; column < EllipseSamples; column++)
            {
                var sample = new RenderPoint(
                    box.X + (box.Width * (column + 0.5) / EllipseSamples),
                    box.Y + (box.Height * (row + 0.5) / EllipseSamples));
                if (EllipseContains(ellipse, sample))
                {
                    inside++;
                }
            }
        }

        return inside / (double)(EllipseSamples * EllipseSamples);
    }

    /// <summary>
    ///     Gets the length of a polyline.
    /// </summary>
    /// <param name="points">The points of the polyline.</param>
    /// <returns>The length in pixels.</returns>
    private static double StrokeLength(IReadOnlyList<RenderPoint> points)
    {
        var length = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            length += MarkGeometry.Distance(points[i - 1], points[i]);
        }

        return length;
    }

    /// <summary>
    ///     Gets the distance from a point to the nearest part of a polyline.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="stroke">The points of the polyline.</param>
    /// <returns>The distance in pixels.</returns>
    private static double DistanceToStroke(RenderPoint point, IReadOnlyList<RenderPoint> stroke)
    {
        var nearest = double.MaxValue;
        for (var i = 1; i < stroke.Count; i++)
        {
            nearest = Math.Min(nearest, MarkGeometry.DistanceToSegment(point, stroke[i - 1], stroke[i]));
        }

        return nearest;
    }

    /// <summary>
    ///     Gets the length of the part of a polyline that lies in a box.
    /// </summary>
    /// <param name="stroke">The points of the polyline.</param>
    /// <param name="box">The box.</param>
    /// <returns>The length in pixels, or a negative number when the polyline does not touch the box.</returns>
    private static double StrokeLengthInside(IReadOnlyList<RenderPoint> stroke, RenderBox box)
    {
        var touches = false;
        var inside = 0.0;
        for (var i = 1; i < stroke.Count; i++)
        {
            var length = ClippedLength(stroke[i - 1], stroke[i], box);
            if (length >= 0)
            {
                touches = true;
                inside += length;
            }
        }

        return touches ? inside : -1;
    }

    /// <summary>
    ///     Gets the length of the part of a line segment that lies in a box, using the Liang-Barsky clipping method.
    /// </summary>
    /// <param name="a">The start of the segment.</param>
    /// <param name="b">The end of the segment.</param>
    /// <param name="box">The box.</param>
    /// <returns>The length in pixels, or a negative number when the segment does not touch the box.</returns>
    private static double ClippedLength(RenderPoint a, RenderPoint b, RenderBox box)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        double t0 = 0;
        double t1 = 1;
        if (!Clip(-dx, a.X - box.X, ref t0, ref t1)
            || !Clip(dx, box.X + box.Width - a.X, ref t0, ref t1)
            || !Clip(-dy, a.Y - box.Y, ref t0, ref t1)
            || !Clip(dy, box.Y + box.Height - a.Y, ref t0, ref t1))
        {
            return -1;
        }

        return (t1 - t0) * Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    ///     Narrows the part of a segment that is inside one side of a box.
    /// </summary>
    /// <param name="p">The direction of the segment against the side; negative when it enters, positive when it leaves.</param>
    /// <param name="q">The distance of the start of the segment from the side, positive inside.</param>
    /// <param name="t0">The start of the part of the segment that is inside so far, from zero to one.</param>
    /// <param name="t1">The end of the part of the segment that is inside so far, from zero to one.</param>
    /// <returns><see langword="false" /> when no part of the segment can be inside.</returns>
    private static bool Clip(double p, double q, ref double t0, ref double t1)
    {
        if (p == 0)
        {
            return q >= 0;
        }

        var r = q / p;
        if (p < 0)
        {
            if (r > t1)
            {
                return false;
            }

            t0 = Math.Max(t0, r);
        }
        else
        {
            if (r < t0)
            {
                return false;
            }

            t1 = Math.Min(t1, r);
        }

        return true;
    }
}

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
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     The shape of a mark that the user draws on a picture.
/// </summary>
public enum MarkKind
{
    /// <summary>
    ///     A rectangle with sides parallel to the edges of the picture.
    /// </summary>
    Rectangle,

    /// <summary>
    ///     An ellipse with axes parallel to the edges of the picture, inscribed in its bounding box.
    /// </summary>
    Ellipse,

    /// <summary>
    ///     An arrow from a tail to a head; the head is the point the arrow points at.
    /// </summary>
    Arrow,

    /// <summary>
    ///     A line that follows the pointer, kept as a polyline.
    /// </summary>
    Freehand
}

/// <summary>
///     Where a mark is on its picture, in the pixel coordinates of that picture (origin at the top-left corner, X to the
///     right, Y downward), in one normal form that does not depend on how the user drew the mark: a rectangle or an
///     ellipse is its top-left and bottom-right corners, an arrow is its tail and head, and a freehand mark is a
///     simplified polyline. Every point lies inside the picture.
/// </summary>
/// <remarks>
///     The form is made by <see cref="Create" /> so that everything that reads a mark, the code that draws it, the code
///     that finds the parts of the source it covers, and the message that tells the assistant about it, can rely on one
///     meaning of the numbers whatever the zoom of the window was when the mark was drawn. Instances are immutable and
///     safe to share between threads.
/// </remarks>
public sealed class MarkGeometry
{
    /// <summary>
    ///     The most points a freehand mark keeps. A long stroke is simplified until it has at most this many points, so
    ///     that a mark stays small enough to describe in a message.
    /// </summary>
    public const int MaximumFreehandPoints = 500;

    /// <summary>
    ///     The smallest size, in pixels, that a mark must have: a rectangle or an ellipse in both directions, an arrow
    ///     or a freehand stroke along its length. A smaller shape is a click, not a mark.
    /// </summary>
    public const double MinimumExtent = 1.0;

    /// <summary>
    ///     How far, in pixels, a simplified freehand stroke may leave the stroke that was drawn, at the start. The
    ///     distance grows only when the stroke still has too many points.
    /// </summary>
    private const double SimplificationTolerance = 1.0;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MarkGeometry" /> class from points that are already normal.
    /// </summary>
    /// <param name="kind">The shape.</param>
    /// <param name="points">The normal points of the shape.</param>
    private MarkGeometry(MarkKind kind, ImmutableArray<RenderPoint> points)
    {
        Kind = kind;
        Points = points;
        BoundingBox = BoundingBoxOf(points);
    }

    /// <summary>
    ///     Gets the shape.
    /// </summary>
    public MarkKind Kind { get; }

    /// <summary>
    ///     Gets the points of the shape, in picture pixels: two corners (top-left, then bottom-right) for a rectangle or
    ///     an ellipse, the tail and then the head for an arrow, and the polyline from its start to its end for a freehand
    ///     mark.
    /// </summary>
    public ImmutableArray<RenderPoint> Points { get; }

    /// <summary>
    ///     Gets the smallest rectangle, in picture pixels, that holds every point of the shape.
    /// </summary>
    public RenderBox BoundingBox { get; }

    /// <summary>
    ///     Makes the normal geometry of a mark from the points the user's pointer went through.
    /// </summary>
    /// <remarks>
    ///     Every point is first moved into the picture, because a stroke that leaves the picture still means the part of
    ///     it that is inside. A rectangle or ellipse uses the first and last point as opposite corners in any direction,
    ///     so dragging up and to the left gives the same shape as dragging down and to the right. An arrow uses the first
    ///     point as its tail and the last as its head. A freehand stroke drops repeated points and is simplified with the
    ///     Ramer-Douglas-Peucker method, which keeps its first and last point, with a distance that starts at one pixel and
    ///     doubles until at most <see cref="MaximumFreehandPoints" /> points remain. A shape that is smaller than
    ///     <see cref="MinimumExtent" /> is refused, so a click never becomes a mark.
    /// </remarks>
    /// <param name="kind">The shape the points describe.</param>
    /// <param name="points">The points in picture pixels, in the order the pointer visited them; at least two.</param>
    /// <param name="imageWidth">The width of the picture in pixels; at least 1.</param>
    /// <param name="imageHeight">The height of the picture in pixels; at least 1.</param>
    /// <returns>The geometry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the picture is smaller than one pixel or <paramref name="kind" /> is not a defined shape.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when fewer than two points are given, a coordinate is not a finite number, or the shape is smaller than
    ///     <see cref="MinimumExtent" />.
    /// </exception>
    public static MarkGeometry Create(MarkKind kind, IReadOnlyList<RenderPoint> points, int imageWidth, int imageHeight)
    {
        // Check the arguments before any calculation: each failure is a caller mistake that cannot be repaired here
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfLessThan(imageWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(imageHeight, 1);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The shape of the mark is not known.");
        }

        if (points.Count < 2)
        {
            throw new ArgumentException("A mark needs at least two points.", nameof(points));
        }

        // Move every point into the picture; a coordinate that is not a number cannot be moved anywhere
        var inside = new RenderPoint[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            {
                throw new ArgumentException("The coordinates of a mark must be finite numbers.", nameof(points));
            }

            inside[i] = new RenderPoint(Math.Clamp(point.X, 0, imageWidth), Math.Clamp(point.Y, 0, imageHeight));
        }

        // Build the normal form of the shape
        var first = inside[0];
        var last = inside[^1];
        switch (kind)
        {
            case MarkKind.Rectangle:
            case MarkKind.Ellipse:
                var left = Math.Min(first.X, last.X);
                var top = Math.Min(first.Y, last.Y);
                var right = Math.Max(first.X, last.X);
                var bottom = Math.Max(first.Y, last.Y);
                if (right - left < MinimumExtent || bottom - top < MinimumExtent)
                {
                    throw new ArgumentException("A rectangle or an ellipse must be at least one pixel wide and high.", nameof(points));
                }

                return new MarkGeometry(kind, [new RenderPoint(left, top), new RenderPoint(right, bottom)]);

            case MarkKind.Arrow:
                if (Distance(first, last) < MinimumExtent)
                {
                    throw new ArgumentException("An arrow must be at least one pixel long.", nameof(points));
                }

                return new MarkGeometry(kind, [first, last]);

            default:
                return new MarkGeometry(kind, SimplifyStroke(inside));
        }
    }

    /// <summary>
    ///     Gets the distance between two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance in pixels.</returns>
    internal static double Distance(RenderPoint a, RenderPoint b)
    {
        return Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
    }

    /// <summary>
    ///     Gets the distance from a point to the nearest point of a line segment.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="start">One end of the segment.</param>
    /// <param name="end">The other end of the segment.</param>
    /// <returns>The distance in pixels.</returns>
    internal static double DistanceToSegment(RenderPoint point, RenderPoint start, RenderPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared == 0)
        {
            return Distance(point, start);
        }

        // Project the point onto the line and keep the projection on the segment
        var t = Math.Clamp((((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared, 0, 1);
        return Distance(point, new RenderPoint(start.X + (t * dx), start.Y + (t * dy)));
    }

    /// <summary>
    ///     Makes the polyline of a freehand mark: drops repeated points and simplifies the stroke to at most
    ///     <see cref="MaximumFreehandPoints" /> points that include its first and last point.
    /// </summary>
    /// <param name="stroke">The points the pointer visited, already inside the picture.</param>
    /// <returns>The polyline.</returns>
    /// <exception cref="ArgumentException">Thrown when the stroke is shorter than <see cref="MinimumExtent" />.</exception>
    private static ImmutableArray<RenderPoint> SimplifyStroke(RenderPoint[] stroke)
    {
        // Remove points that repeat their predecessor, which a slow pointer produces and which carry no information
        var distinct = new List<RenderPoint>(stroke.Length) { stroke[0] };
        for (var i = 1; i < stroke.Length; i++)
        {
            if (stroke[i] != distinct[^1])
            {
                distinct.Add(stroke[i]);
            }
        }

        // A stroke that never moved is a click
        var length = 0.0;
        for (var i = 1; i < distinct.Count; i++)
        {
            length += Distance(distinct[i - 1], distinct[i]);
        }

        if (length < MinimumExtent)
        {
            throw new ArgumentException("A freehand mark must be at least one pixel long.", nameof(stroke));
        }

        // Simplify with a growing tolerance until the stroke is small enough; with a large enough tolerance only the two
        // end points remain, so the loop ends
        var tolerance = SimplificationTolerance;
        var simplified = Simplify(distinct, tolerance);
        while (simplified.Count > MaximumFreehandPoints)
        {
            tolerance *= 2;
            simplified = Simplify(distinct, tolerance);
        }

        return [.. simplified];
    }

    /// <summary>
    ///     Simplifies a polyline with the Ramer-Douglas-Peucker method: a point is kept only when it is farther than the
    ///     tolerance from the line between the points kept around it. The first and last point are always kept.
    /// </summary>
    /// <remarks>
    ///     The method is written with an explicit stack instead of recursion so that a stroke of any length cannot
    ///     exhaust the call stack.
    /// </remarks>
    /// <param name="points">The polyline; at least two points.</param>
    /// <param name="tolerance">The largest distance, in pixels, a dropped point may be from the simplified line.</param>
    /// <returns>The kept points in their original order.</returns>
    private static List<RenderPoint> Simplify(List<RenderPoint> points, double tolerance)
    {
        var keep = new bool[points.Count];
        keep[0] = true;
        keep[^1] = true;
        var ranges = new Stack<(int Start, int End)>();
        ranges.Push((0, points.Count - 1));
        while (ranges.Count > 0)
        {
            // Find the point of the range that is farthest from the line between the ends of the range
            var (start, end) = ranges.Pop();
            var farthest = -1.0;
            var index = -1;
            for (var i = start + 1; i < end; i++)
            {
                var distance = DistanceToSegment(points[i], points[start], points[end]);
                if (distance > farthest)
                {
                    farthest = distance;
                    index = i;
                }
            }

            // Keep the point and look at both sides of it, or drop everything between the ends
            if (index >= 0 && farthest > tolerance)
            {
                keep[index] = true;
                ranges.Push((start, index));
                ranges.Push((index, end));
            }
        }

        var result = new List<RenderPoint>();
        for (var i = 0; i < points.Count; i++)
        {
            if (keep[i])
            {
                result.Add(points[i]);
            }
        }

        return result;
    }

    /// <summary>
    ///     Gets the smallest rectangle that holds every point.
    /// </summary>
    /// <param name="points">The points; at least one.</param>
    /// <returns>The rectangle.</returns>
    private static RenderBox BoundingBoxOf(ImmutableArray<RenderPoint> points)
    {
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new RenderBox(left, top, right - left, bottom - top);
    }
}

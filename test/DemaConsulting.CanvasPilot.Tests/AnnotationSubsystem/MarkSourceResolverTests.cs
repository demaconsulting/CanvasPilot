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

using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="MarkSourceResolver" />.
/// </summary>
public sealed class MarkSourceResolverTests
{
    /// <summary>
    ///     A diagram of 400 x 200 pixels: node A at the left, node B at the right, an edge from A to B, and node C below B.
    /// </summary>
    private static readonly RenderPosition[] Diagram =
    [
        AnnotationTestPictures.Node("A", "Start", 20, 20, 100, 40, line: 2),
        AnnotationTestPictures.Node("B", "Decide", 200, 20, 100, 40, line: 3),
        AnnotationTestPictures.Node("C", "End", 200, 120, 100, 40, line: 4),
        new("edge", "A-B", null, 5, "A", "B", new RenderBox(120, 38, 80, 4))
    ];

    /// <summary>
    ///     Makes a mark on a picture without going through the registry.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="kind">The shape.</param>
    /// <param name="points">The points of the shape.</param>
    /// <returns>The mark.</returns>
    private static Mark MarkOn(RenderedImage picture, MarkKind kind, params RenderPoint[] points)
    {
        return new MarkRegistry().Add(picture, kind, points);
    }

    /// <summary>
    ///     Makes a diagram picture of 400 x 200 pixels with the given positions.
    /// </summary>
    /// <param name="positions">The element positions.</param>
    /// <param name="truncated">Whether the renderer left positions out.</param>
    /// <returns>The picture.</returns>
    private static RenderedImage DiagramPicture(IReadOnlyList<RenderPosition>? positions = null, bool truncated = false)
    {
        return AnnotationTestPictures.Picture(width: 400, height: 200, positions: positions ?? Diagram, positionsTruncated: truncated);
    }

    /// <summary>
    ///     Test that a rectangle around one node finds that node with everything the renderer knows about it.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_RectangleAroundNode_ReportsNodeWithKindIdTextLineAndBox()
    {
        // Arrange: a mark that surrounds node A and nothing else
        var mark = MarkOn(DiagramPicture(), MarkKind.Rectangle, new RenderPoint(10, 10), new RenderPoint(130, 70));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the node, with its identifier, text, source line, and box; the edge is covered too little
        Assert.True(result.IsAvailable);
        Assert.Null(result.UnavailableReason);
        Assert.False(result.IsTruncated);
        var node = Assert.Single(result.Elements);
        Assert.Equal(new SourceElementReference("node", "A", "Start", 2, new RenderBox(20, 20, 100, 40), null), node);
    }

    /// <summary>
    ///     Test that a rectangle over two nodes and the edge between them finds all three, the most specific first.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_RectangleOverTwoNodes_OrdersByCoverageThenSmallerBoxThenRenderOrder()
    {
        // Arrange: a mark over A, the edge, and B
        var mark = MarkOn(DiagramPicture(), MarkKind.Rectangle, new RenderPoint(10, 10), new RenderPoint(310, 70));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: all fully covered, so the smaller edge comes first, then the nodes in the renderer's order
        Assert.Equal(["A-B", "A", "B"], result.Elements.Select(element => element.Id));
        Assert.Equal("edge", result.Elements[0].Kind);
    }

    /// <summary>
    ///     Test the quarter rule: a rectangle that covers a quarter of a node finds it and one that covers less does not.
    /// </summary>
    [Theory]
    [InlineData(25, true)]
    [InlineData(24, false)]
    public void MarkSourceResolver_Resolve_RectangleCoveringPartOfNode_NeedsAQuarterOfTheNodeBox(double covered, bool found)
    {
        // Arrange: a rectangle from the left edge of the picture over the left part of node A, which is 100 pixels wide
        // and starts at 20: the rectangle covers 'covered' pixels of the node, so it ends at 20 plus that
        var mark = MarkOn(DiagramPicture(), MarkKind.Rectangle, new RenderPoint(0, 0), new RenderPoint(20 + covered, 100));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: node A is found only when at least a quarter of its box is covered (25 of 100 pixels, but not 24)
        Assert.Equal(found, result.Elements.Any(element => element.Id == "A"));
    }

    /// <summary>
    ///     Test that a small rectangle in the middle of a big element finds the element, because it contains its center.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_SmallRectangleAtCenterOfLargeElement_FindsTheElement()
    {
        // Arrange: one big node and a small rectangle at its center that covers 2 percent of it
        var big = AnnotationTestPictures.Node("Big", "Group", 100, 100, 200, 100);
        var mark = MarkOn(DiagramPicture([big]), MarkKind.Rectangle, new RenderPoint(190, 140), new RenderPoint(210, 160));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the element is found though only 2 percent of it is covered
        Assert.Equal(["Big"], result.Elements.Select(element => element.Id));
    }

    /// <summary>
    ///     Test that a rectangle that touches only nothing finds nothing, and the result still says that elements were
    ///     available.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_RectangleOverEmptySpace_FindsNothingButIsAvailable()
    {
        // Arrange: a mark in the empty lower left of the diagram
        var mark = MarkOn(DiagramPicture(), MarkKind.Rectangle, new RenderPoint(10, 120), new RenderPoint(100, 190));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the picture could say, and nothing is under the mark
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Elements);
        Assert.False(result.IsTruncated);
    }

    /// <summary>
    ///     Test that an ellipse is measured as an ellipse: it finds a node that it covers mostly, and not one that only
    ///     the corner of its bounding box touches.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_Ellipse_UsesTheEllipseNotItsBoundingBox()
    {
        // Arrange: an ellipse around node A, and a small ellipse in whose bounding box the node's corner lies but of
        // which the ellipse itself covers only a sliver
        var around = MarkOn(DiagramPicture(), MarkKind.Ellipse, new RenderPoint(10, 10), new RenderPoint(130, 70));
        var corner = MarkOn(DiagramPicture(), MarkKind.Ellipse, new RenderPoint(0, 0), new RenderPoint(40, 40));

        // Act: find the parts under them
        var aroundResult = MarkSourceResolver.Resolve(around);
        var cornerResult = MarkSourceResolver.Resolve(corner);

        // Assert: the large ellipse finds A, the small one finds nothing although its bounding box overlaps A
        Assert.Equal(["A"], aroundResult.Elements.Select(element => element.Id));
        Assert.Empty(cornerResult.Elements);
    }

    /// <summary>
    ///     Test that an arrow finds the element that holds its head first and the one that holds its tail after it.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_Arrow_FindsHeadElementThenTailElement()
    {
        // Arrange: an arrow from node A (tail) to node C (head)
        var mark = MarkOn(DiagramPicture(), MarkKind.Arrow, new RenderPoint(60, 40), new RenderPoint(250, 140));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the head's node first, the tail's node second, and nothing in between
        Assert.Equal(["C", "A"], result.Elements.Select(element => element.Id));
    }

    /// <summary>
    ///     Test that an arrow whose head is in several nested elements names the most specific one first.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_ArrowHeadInNestedElements_NamesSmallerElementFirst()
    {
        // Arrange: a container with a node inside it, and an arrow that points at the node from outside both
        var container = AnnotationTestPictures.Node("Group", "Group", 0, 0, 400, 200);
        var node = AnnotationTestPictures.Node("Inner", "Inner", 100, 50, 100, 40);
        var picture = DiagramPicture([container, node]);
        var mark = MarkOn(picture, MarkKind.Arrow, new RenderPoint(300, 150), new RenderPoint(150, 70));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the inner node, which holds the head, comes before the container, which holds head and tail
        Assert.Equal(["Inner", "Group"], result.Elements.Select(element => element.Id));
    }

    /// <summary>
    ///     Test that a freehand stroke finds the elements it crosses and orders them by how much of the stroke is in each.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_FreehandAcrossNodes_FindsCrossedElementsOrderedByStrokeLength()
    {
        // Arrange: a stroke from inside A, along the edge, into B, which ends before the middle of B
        var mark = MarkOn(DiagramPicture(), MarkKind.Freehand, new RenderPoint(60, 40), new RenderPoint(250, 40));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the edge holds 80 of the 190 pixels of the stroke, A 60, and B 50; C is not crossed
        Assert.Equal(["A-B", "A", "B"], result.Elements.Select(element => element.Id));
    }

    /// <summary>
    ///     Test that a freehand stroke that crosses nothing finds nothing.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_FreehandOverEmptySpace_FindsNothing()
    {
        // Arrange: a stroke below node A and left of node C
        var mark = MarkOn(DiagramPicture(), MarkKind.Freehand, new RenderPoint(10, 100), new RenderPoint(150, 190));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: available and empty
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Elements);
    }

    /// <summary>
    ///     Test that elements without a box are skipped and that edges drawn as straight lines, which have a box of no
    ///     height, can still be found.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_ElementsWithoutBoxOrWithoutHeight_AreSkippedOrWidened()
    {
        // Arrange: an element with no box, a straight edge of no height, and a rectangle around the edge
        var noBox = new RenderPosition("node", "Unplaced", "No box");
        var flat = new RenderPosition("edge", "Flat", null, 7, Box: new RenderBox(100, 50, 100, 0));
        var mark = MarkOn(DiagramPicture([noBox, flat]), MarkKind.Rectangle, new RenderPoint(90, 40), new RenderPoint(210, 60));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: only the flat edge is found, and it keeps its own box
        var found = Assert.Single(result.Elements);
        Assert.Equal("Flat", found.Id);
        Assert.Equal(new RenderBox(100, 50, 100, 0), found.Box);
    }

    /// <summary>
    ///     Test that at most 25 elements are returned and that the result says that it left some out.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_MoreThanTheLimit_ReturnsTwentyFiveAndFlagsTruncation()
    {
        // Arrange: 40 small nodes in a grid and a rectangle over all of them
        var nodes = Enumerable.Range(0, 40)
            .Select(i => AnnotationTestPictures.Node($"N{i}", $"Node {i}", 10 + ((i % 8) * 45), 10 + ((i / 8) * 35), 30, 20))
            .ToArray();
        var mark = MarkOn(DiagramPicture(nodes), MarkKind.Rectangle, new RenderPoint(0, 0), new RenderPoint(400, 200));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the limit, the first 25 in the renderer's order (all equally covered and equally large), and the flag
        Assert.Equal(MarkSourceResolver.MaximumElements, result.Elements.Length);
        Assert.Equal(Enumerable.Range(0, 25).Select(i => $"N{i}"), result.Elements.Select(element => element.Id));
        Assert.True(result.IsTruncated);
    }

    /// <summary>
    ///     Test that the renderer's own note that it left elements out is passed on.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_RendererTruncatedPositions_PassesTheFlagOn()
    {
        // Arrange: a picture whose renderer reported that it left positions out
        var mark = MarkOn(DiagramPicture(truncated: true), MarkKind.Rectangle, new RenderPoint(10, 10), new RenderPoint(130, 70));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: a part was found and the result says that the list may be incomplete
        Assert.Single(result.Elements);
        Assert.True(result.IsTruncated);
    }

    /// <summary>
    ///     Test that a mark on an OpenSCAD model finds the anchors inside a rectangle and ignores anchors without a pixel.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_RectangleOverAnchors_ReportsAnchorsInsideNearestFirst()
    {
        // Arrange: a model with three anchors that have a pixel, one that has none, and a rectangle over two of them
        var anchors = new[]
        {
            new RenderAnchor("top", 100, 50),
            new RenderAnchor("side", 300, 50),
            new RenderAnchor("near", 105, 52),
            new RenderAnchor("hidden")
        };
        var picture = AnnotationTestPictures.Picture(width: 400, height: 200, format: "scad", anchors: anchors);
        var mark = MarkOn(picture, MarkKind.Rectangle, new RenderPoint(50, 20), new RenderPoint(150, 80));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: both anchors inside, the one nearest the center of the rectangle (100, 50) first, as anchor references
        Assert.True(result.IsAvailable);
        Assert.Equal(["top", "near"], result.Elements.Select(element => element.Id));
        Assert.Equal(new SourceElementReference("anchor", "top", null, null, null, new RenderPoint(100, 50)), result.Elements[0]);
    }

    /// <summary>
    ///     Test that an ellipse finds only the anchors inside the ellipse and not those in the corners of its box.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_EllipseOverAnchors_ExcludesAnchorInCornerOfBoundingBox()
    {
        // Arrange: an anchor at the center and one in the corner of the bounding box of the ellipse
        var anchors = new[] { new RenderAnchor("center", 100, 50), new RenderAnchor("corner", 52, 22) };
        var picture = AnnotationTestPictures.Picture(width: 400, height: 200, format: "scad", anchors: anchors);
        var mark = MarkOn(picture, MarkKind.Ellipse, new RenderPoint(50, 20), new RenderPoint(150, 80));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: only the center anchor
        Assert.Equal(["center"], result.Elements.Select(element => element.Id));
    }

    /// <summary>
    ///     Test that an arrow names the one anchor nearest its head within 24 pixels and no other.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_ArrowNearAnchors_NamesOnlyTheNearestWithinTwentyFourPixels()
    {
        // Arrange: two anchors within reach of the head and one beyond it
        var anchors = new[] { new RenderAnchor("far", 160, 50), new RenderAnchor("second", 110, 50), new RenderAnchor("nearest", 105, 50) };
        var picture = AnnotationTestPictures.Picture(width: 400, height: 200, format: "scad", anchors: anchors);
        var pointing = MarkOn(picture, MarkKind.Arrow, new RenderPoint(100, 120), new RenderPoint(100, 50));
        var missing = MarkOn(picture, MarkKind.Arrow, new RenderPoint(300, 190), new RenderPoint(300, 150));

        // Act: find the parts under them
        var pointingResult = MarkSourceResolver.Resolve(pointing);
        var missingResult = MarkSourceResolver.Resolve(missing);

        // Assert: the nearest anchor alone; an arrow that points at nothing finds nothing
        Assert.Equal(["nearest"], pointingResult.Elements.Select(element => element.Id));
        Assert.True(missingResult.IsAvailable);
        Assert.Empty(missingResult.Elements);
    }

    /// <summary>
    ///     Test that a freehand stroke names the anchors within 8 pixels of the stroke.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_FreehandNearAnchors_NamesAnchorsWithinEightPixelsOfStroke()
    {
        // Arrange: a horizontal stroke, an anchor 5 pixels above it, and an anchor 12 pixels above it
        var anchors = new[] { new RenderAnchor("close", 100, 95), new RenderAnchor("away", 100, 88) };
        var picture = AnnotationTestPictures.Picture(width: 400, height: 200, format: "scad", anchors: anchors);
        var mark = MarkOn(picture, MarkKind.Freehand, new RenderPoint(50, 100), new RenderPoint(150, 100));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: only the close anchor
        Assert.Equal(["close"], result.Elements.Select(element => element.Id));
    }

    /// <summary>
    ///     Test that a picture that knows nothing about its elements says so with the format in the reason, instead of
    ///     returning an empty list.
    /// </summary>
    [Theory]
    [InlineData("svg", "svg provides no element positions")]
    [InlineData("png", "png provides no element positions")]
    [InlineData("pdf", "pdf provides no element positions")]
    [InlineData("pptx", "pptx provides no element positions")]
    [InlineData("", "this kind of file provides no element positions")]
    [InlineData("mermaid", "the renderer reported no element positions for this mermaid picture")]
    [InlineData("vega-lite", "the renderer reported no element positions for this vega-lite picture")]
    [InlineData("scad", "the model declares no anchors that appear in the picture")]
    public void MarkSourceResolver_Resolve_PictureWithoutPositions_ReportsNotAvailableWithReason(string format, string reason)
    {
        // Arrange: a picture of that format without positions or anchors, and a mark on it
        var picture = AnnotationTestPictures.Picture(format: format, anchors: [new RenderAnchor("no-pixel")]);
        var mark = MarkOn(picture, MarkKind.Rectangle, new RenderPoint(10, 10), new RenderPoint(60, 60));

        // Act: find the parts under it
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: not available, with the reason, and no elements
        Assert.False(result.IsAvailable);
        Assert.Equal(reason, result.UnavailableReason);
        Assert.Empty(result.Elements);
        Assert.False(result.IsTruncated);
    }

    /// <summary>
    ///     Test that a missing mark is refused.
    /// </summary>
    [Fact]
    public void MarkSourceResolver_Resolve_NullMark_ThrowsArgumentNullException()
    {
        // Act / Assert: no mark, no answer
        Assert.Throws<ArgumentNullException>(() => MarkSourceResolver.Resolve(null!));
    }
}

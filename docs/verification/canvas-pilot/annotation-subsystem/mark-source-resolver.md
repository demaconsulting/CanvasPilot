### MarkSourceResolver

#### Verification Approach

`MarkSourceResolver` is verified with unit tests in `MarkSourceResolverTests.cs` on pictures that are made without
drawing a file (`AnnotationTestPictures`): a diagram of 400 by 200 pixels with three nodes and an edge between two of
them, a model with anchors, and pictures of formats that report nothing. Marks are made with the real `MarkRegistry`,
because the resolver reads the geometry and the picture it keeps. Nothing is substituted.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A rectangle or an ellipse finds the elements that it covers by a quarter of their box or whose center it contains, and
  not those that it only touches; an arrow finds the head's element before the tail's; a freehand stroke finds what it
  crosses; the results are ordered by coverage, then smaller box first, then the renderer's order.
- Anchors are found inside a rectangle or ellipse, the nearest within 24 pixels of an arrow head, and within 8 pixels of
  a stroke.
- A picture without positions or anchors gives not available with a reason that names its format and no elements.
- At most 25 elements are returned and the cut is reported, as is the renderer's own truncation.

#### Test Scenarios

**MarkSourceResolver_Resolve_RectangleAroundNode_ReportsNodeWithKindIdTextLineAndBox**: A rectangle around the first
node finds it, with kind, identifier, text, line, and box, and not the edge, which the rectangle covers by an eighth
only. This scenario is tested by `MarkSourceResolver_Resolve_RectangleAroundNode_ReportsNodeWithKindIdTextLineAndBox`.

**MarkSourceResolver_Resolve_RectangleOverTwoNodes_OrdersByCoverageThenSmallerBoxThenRenderOrder**: A rectangle over
both nodes and the edge between them finds the edge first (equally covered but smaller), then the nodes in the
renderer's order. This scenario is tested by
`MarkSourceResolver_Resolve_RectangleOverTwoNodes_OrdersByCoverageThenSmallerBoxThenRenderOrder`.

**MarkSourceResolver_Resolve_RectangleCoveringPartOfNode_NeedsAQuarterOfTheNodeBox**: Covering 25 of the 100 pixels of a
node finds it and covering 24 does not. This scenario is tested by
`MarkSourceResolver_Resolve_RectangleCoveringPartOfNode_NeedsAQuarterOfTheNodeBox`.

**MarkSourceResolver_Resolve_SmallRectangleAtCenterOfLargeElement_FindsTheElement** and
**MarkSourceResolver_Resolve_RectangleOverEmptySpace_FindsNothingButIsAvailable**: A small rectangle at the center of a
large element finds it; a rectangle over empty space finds nothing and the result is still available. These scenarios
are tested by the tests of the same names.

**MarkSourceResolver_Resolve_Ellipse_UsesTheEllipseNotItsBoundingBox**: An ellipse around a node finds it; a small
ellipse in whose bounding box only the corner of the node lies finds nothing. This scenario is tested by
`MarkSourceResolver_Resolve_Ellipse_UsesTheEllipseNotItsBoundingBox`.

**MarkSourceResolver_Resolve_Arrow_FindsHeadElementThenTailElement** and
**MarkSourceResolver_Resolve_ArrowHeadInNestedElements_NamesSmallerElementFirst**: An arrow from one node to another
reports the head's node and then the tail's; an arrow whose head is inside a node inside a container names the node
before the container. These scenarios are tested by the tests of the same names.

**MarkSourceResolver_Resolve_FreehandAcrossNodes_FindsCrossedElementsOrderedByStrokeLength** and
**MarkSourceResolver_Resolve_FreehandOverEmptySpace_FindsNothing**: A stroke across a node, the edge, and a second node
reports them ordered by the length of the stroke inside each; a stroke over empty space reports nothing. These scenarios
are tested by the tests of the same names.

**MarkSourceResolver_Resolve_ElementsWithoutBoxOrWithoutHeight_AreSkippedOrWidened**: An element without a box is
skipped and a straight edge of no height is found, with its own box. This scenario is tested by
`MarkSourceResolver_Resolve_ElementsWithoutBoxOrWithoutHeight_AreSkippedOrWidened`.

**MarkSourceResolver_Resolve_RectangleOverAnchors_ReportsAnchorsInsideNearestFirst**,
**MarkSourceResolver_Resolve_EllipseOverAnchors_ExcludesAnchorInCornerOfBoundingBox**,
**MarkSourceResolver_Resolve_ArrowNearAnchors_NamesOnlyTheNearestWithinTwentyFourPixels**, and
**MarkSourceResolver_Resolve_FreehandNearAnchors_NamesAnchorsWithinEightPixelsOfStroke**: The anchor rules of each
shape, including that an anchor without a pixel is skipped and that an arrow that points at nothing finds nothing but is
still available. These scenarios are tested by the tests of the same names.

**MarkSourceResolver_Resolve_PictureWithoutPositions_ReportsNotAvailableWithReason**: Pictures of the formats svg, png,
pdf, pptx, an unnamed format, mermaid, vega-lite, and scad without usable positions give not available with the expected
reason and no elements. This scenario is tested by
`MarkSourceResolver_Resolve_PictureWithoutPositions_ReportsNotAvailableWithReason`.

**MarkSourceResolver_Resolve_MoreThanTheLimit_ReturnsTwentyFiveAndFlagsTruncation** and
**MarkSourceResolver_Resolve_RendererTruncatedPositions_PassesTheFlagOn**: Forty equally covered nodes give the first 25
and the cut flag; the renderer's own truncation flag is passed on. These scenarios are tested by the tests of the same
names.

**MarkSourceResolver_Resolve_NullMark_ThrowsArgumentNullException**: A missing mark is refused. This scenario is tested
by `MarkSourceResolver_Resolve_NullMark_ThrowsArgumentNullException`.

### MarkImageComposer

#### Verification Approach

`MarkImageComposer` is verified with unit tests in `MarkImageComposerTests.cs` that draw with the real CanvasNet viewer
and PNG codec, because what is verified is the picture that comes out. The tests use flat-color pictures
(`AnnotationTestPictures`) so that every pixel that is not drawn on has a known value, make marks with the real
`MarkRegistry`, decode the PNG files that the composer returns, and look at the pixels on the lines, in the badge,
inside and outside the marks, and in the cut-outs. The pixel positions are those of the lines of the documented widths:
a line of width 3 centered on a whole coordinate colors the two pixels next to the coordinate fully, and the white line
of width 6 under it colors three pixels on each side of the center. Nothing is substituted.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The annotated picture has the size of the picture, for ordinary and odd sizes; the lines of a mark have the color of
  its number with a white line under them; pixels inside and outside the marks, and the corners, are as shown; the
  picture that was marked is not changed; transparency away from the marks is kept.
- The badge with the number is at the top-left of the mark, in the color of the mark with a white edge and a white
  number.
- Ellipses, arrows with a head, and freehand marks are drawn where their geometry says.
- The cut-out of a mark is the box and badge with a margin of 16 pixels, at least 64 pixels, inside the picture or the
  whole picture where it is smaller, and equals that region of the annotated picture, other marks included.
- One, two, and ten marks give one cut-out each in the order of the numbers.
- A missing argument and a mark of another picture are refused.

#### Test Scenarios

**MarkImageComposer_Compose_Marks_AnnotatedPictureHasSizeOfPicture**: Pictures of 200 by 100, 97 by 53, and 1000 by 40
pixels with a rectangle give a PNG file of the same size. This scenario is tested by
`MarkImageComposer_Compose_Marks_AnnotatedPictureHasSizeOfPicture`.

**MarkImageComposer_Compose_Rectangle_DrawsColoredLinesOverWhiteAndLeavesOtherPixelsAsShown**: A rectangle from (50, 40)
to (150, 80) has the color of mark one on its lines, white next to them, and the background inside, outside, and in the
corners. This scenario is tested by
`MarkImageComposer_Compose_Rectangle_DrawsColoredLinesOverWhiteAndLeavesOtherPixelsAsShown`.

**MarkImageComposer_Compose_Mark_DrawsNumberedBadgeAtTopLeftOfMark**: The badge of a mark at (50, 40) is filled with its
color, has a white edge, a white number, and nothing outside it. This scenario is tested by
`MarkImageComposer_Compose_Mark_DrawsNumberedBadgeAtTopLeftOfMark`.

**MarkImageComposer_Compose_EllipseArrowAndFreehand_AreDrawnWhereTheirGeometrySays**: The left end of an ellipse, the
middle of the shaft of an arrow, and the middle of a stroke have the colors of their numbers, and the center of the
ellipse is as shown. This scenario is tested by
`MarkImageComposer_Compose_EllipseArrowAndFreehand_AreDrawnWhereTheirGeometrySays`.

**MarkImageComposer_Compose_Arrow_HasAHeadAtItsPointedEnd**: Near the head of an arrow there are many more pixels of its
color than near its tail. This scenario is tested by `MarkImageComposer_Compose_Arrow_HasAHeadAtItsPointedEnd`.

**MarkImageComposer_ColorOf_Numbers_GiveDistinctColorsThatRepeatAfterSix** and
**MarkImageComposer_Sizes_GrowWithPictureWithinLimits**: The first six numbers have six colors and the seventh the
first; the line width and the badge radius follow the size of the picture within their limits. These scenarios are
tested by the tests of the same names.

**MarkImageComposer_Compose_Marks_DoNotChangeTheRenderedPicture**: The pixels and the fingerprint of a picture with a
gradient are the same after drawing a mark on it. This scenario is tested by
`MarkImageComposer_Compose_Marks_DoNotChangeTheRenderedPicture`.

**MarkImageComposer_Compose_PictureWithTransparency_KeepsTransparentPixelsAwayFromMarks**: On a fully transparent
picture the pixel far from the mark is still transparent and the pixel on the line is opaque. This scenario is tested by
`MarkImageComposer_Compose_PictureWithTransparency_KeepsTransparentPixelsAwayFromMarks`.

**MarkImageComposer_Compose_NoMarks_ReturnsPictureWithoutAreas**: Without marks the annotated picture is the picture and
no cut-out is made. This scenario is tested by `MarkImageComposer_Compose_NoMarks_ReturnsPictureWithoutAreas`.

**MarkImageComposer_Compose_Mark_CutsAreaOfBoxAndBadgeWithMarginFromAnnotatedPicture**: A rectangle from (80, 50) to
(120, 70) on a 300 by 200 picture, with a second mark near it, has the area (55, 24) of 81 by 64 pixels, and the cut-out
is that region of the annotated picture. This scenario is tested by
`MarkImageComposer_Compose_Mark_CutsAreaOfBoxAndBadgeWithMarginFromAnnotatedPicture`.

**MarkImageComposer_Compose_MarkNearCornerOrOnSmallPicture_KeepsAreaInsidePicture**: A mark in the corner has the area
from (0, 0) of 64 by 64 pixels and a mark on a picture of 50 by 40 pixels has the whole picture. This scenario is tested
by `MarkImageComposer_Compose_MarkNearCornerOrOnSmallPicture_KeepsAreaInsidePicture`.

**MarkImageComposer_Compose_SeveralMarks_GivesOneAreaForEachInNumberOrder**: One, two, and ten marks, given in reverse
order, give one cut-out each in the order of the numbers, each at least 64 pixels in each direction. This scenario is
tested by `MarkImageComposer_Compose_SeveralMarks_GivesOneAreaForEachInNumberOrder`.

**MarkImageComposer_Compose_InvalidArguments_Throw**: A missing render service, picture, or marks, and a mark of another
picture, are refused. This scenario is tested by `MarkImageComposer_Compose_InvalidArguments_Throw`.

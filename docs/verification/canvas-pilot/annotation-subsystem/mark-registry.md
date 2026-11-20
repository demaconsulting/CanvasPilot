### MarkRegistry

#### Verification Approach

`MarkRegistry` and the `MarkGeometry` and `Mark` records it makes are verified with unit tests in
`MarkRegistryTests.cs`. The tests make pictures without drawing a file (`AnnotationTestPictures`, a flat-color picture
with the element positions a test needs), use a fixed clock for the time of creation, and use the real registry; nothing
is substituted, because the unit has no I/O and no dependency except the picture type.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each mark holds the very picture it was drawn on and is numbered one more than the largest number; numbers are not
  reused until the registry is cleared.
- Only marks whose chips are present are active, in number order; a picture shows only the active marks that were drawn
  on exactly that picture, and a picture drawn again with the same pixels shows them.
- Geometry is in normal form inside the picture; shapes smaller than a pixel and invalid points are refused without a
  change; a stroke has at most 500 points and keeps its end points; a note is one clean line of at most 200 characters.
- Clearing removes every mark and restarts the numbering.

#### Test Scenarios

**MarkRegistry_Add_Rectangle_PinsMarkToPictureAndNumbersItOne**: A rectangle is drawn; the mark holds the same picture
object, its key, number one, its kind, and the time of the clock, and can be found by its identity. This scenario is
tested by `MarkRegistry_Add_Rectangle_PinsMarkToPictureAndNumbersItOne`.

**MarkRegistry_Add_SeveralMarks_NumbersThemInOrderOfCreation**: Three marks on two pictures are numbered one, two, and
three and listed in that order. This scenario is tested by `MarkRegistry_Add_SeveralMarks_NumbersThemInOrderOfCreation`.

**MarkRegistry_Add_AfterMarkWasHidden_DoesNotReuseItsNumber**: With the chip of the first of two marks gone, a third
mark is number three. This scenario is tested by `MarkRegistry_Add_AfterMarkWasHidden_DoesNotReuseItsNumber`.

**MarkRegistry_ActiveMarks_ChipsPresent_ReturnsOnlyThoseMarksInNumberOrder**: Of three marks, the two whose chips are
listed out of order, with a repeat and an unknown identity, are returned in number order. This scenario is tested by
`MarkRegistry_ActiveMarks_ChipsPresent_ReturnsOnlyThoseMarksInNumberOrder`.

**MarkRegistry_VisibleMarks_NewPictureOfSameFile_HidesMarksOfTheEarlierPicture**: Two pictures of the same file with
other pixels each show only their own mark while both marks are in the message. This scenario is tested by
`MarkRegistry_VisibleMarks_NewPictureOfSameFile_HidesMarksOfTheEarlierPicture`.

**MarkRegistry_VisibleMarks_SamePixelsDrawnAgain_ShowsTheMarks**: A picture object with the same file and pixels shows
the marks of the earlier object. This scenario is tested by
`MarkRegistry_VisibleMarks_SamePixelsDrawnAgain_ShowsTheMarks`.

**MarkRegistry_Clear_RemovesEveryMarkAndRestartsNumbering** and
**MarkRegistry_Marks_ReturnsCopyThatLaterAddsDoNotChange**: Clearing leaves no mark and the next mark is number one; a
list taken earlier is not changed by later marks. These scenarios are tested by the tests of the same names.

**MarkRegistry_Add_ShapeDraggedUpAndLeft_NormalizesCorners**: A rectangle and an ellipse dragged from the bottom-right
to the top-left have the top-left corner first and the same box as the opposite drag. This scenario is tested by
`MarkRegistry_Add_ShapeDraggedUpAndLeft_NormalizesCorners`.

**MarkRegistry_Add_PointsOutsidePicture_AreClampedToPictureBounds**: Corners outside the picture are moved to its edges.
This scenario is tested by `MarkRegistry_Add_PointsOutsidePicture_AreClampedToPictureBounds`.

**MarkRegistry_Add_Arrow_KeepsTailAndHeadOnly**: An arrow keeps its first point as tail and its last as head. This
scenario is tested by `MarkRegistry_Add_Arrow_KeepsTailAndHeadOnly`.

**MarkRegistry_Add_LongFreehandStroke_IsSimplifiedKeepingEndPoints** and
**MarkRegistry_Add_StraightFreehandStroke_CollapsesToItsEnds**: A wavy stroke of 5000 points is simplified to at most
500 points that keep its first and last point and its extent; a straight stroke with repeated points becomes its two
ends. These scenarios are tested by the tests of the same names.

**MarkRegistry_Add_ShapeSmallerThanAPixel_ThrowsAndAddsNothing** and **MarkRegistry_Add_InvalidArguments_Throw**:
Clicks, shapes thinner than a pixel, a missing picture or points, one point, coordinates that are not numbers, and an
unknown shape are refused and add nothing. These scenarios are tested by the tests of the same names.

**MarkRegistry_Add_Note_IsOneCleanLineOfAtMostTwoHundredCharacters** and
**MarkRegistry_CleanNote_CutInsideSurrogatePair_DropsTheWholeCharacter**: Notes are trimmed, made one line, cut at 200
characters without splitting a surrogate pair, and a blank note is no note. These scenarios are tested by the tests of
the same names.

**Mark_New_InvalidArguments_Throw**: A mark needs a number of at least one, a shape, and a picture. This scenario is
tested by `Mark_New_InvalidArguments_Throw`.

### PayloadBuilder

#### Verification Approach

`PayloadBuilder` is verified with unit tests in `PayloadBuilderTests.cs`. The tests use a hand-written composer of mark
pictures (`FakeMarkImageComposer`) that draws nothing, returns recognizable bytes, and records what it was asked to
draw, so that what the builder decides is visible without drawing; the real `MarkRegistry`, `ComposerDocument`, and
`MarkSourceResolver`, which have no I/O; and staged attachments that are made directly. The drawing itself is verified
with `MarkImageComposer`, and the units together by the subsystem tests.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The text and the parts are in the order of the message with the references, the origins, and the one-based indexes of
  the attachments; an empty message gives an empty payload and a message without marks gives no image and no drawing.
- The marks on two pictures give two images in the order of the first mark, marks in number order; the same picture as
  two objects is one picture; marks without chips are left out with their pictures; a repeated chip is one mark.
- A mark carries its number, shape, points, bounding box, note, and the picture and area that the composer made.
- Source elements and the statement that a picture cannot tell are carried.
- The attachments carry their kind, name, both paths, size, and number of files.
- Attachments that are not the chips', unknown marks, and a wrong number are refused; a composer that gives the wrong
  number of areas fails the build; the background build gives the same payload and honors cancellation.
- The payload is not changed by later changes to the draft and the registry.
- Built through the composer's `IMessageBuilder`, the builder returns the sent message with the snapshot, only the marks
  whose chips are in the message, the attachments, the payload, and its time.

#### Test Scenarios

**PayloadBuilder_Build_MessageWithTextMarksAndAttachments_KeepsOrderAndMakesReferences**: A message of typed text, a
mark, a file, dictated text, another mark, and a folder gives the text with `[Mark 2]`, `[Attachment 1: brief.txt]`, and
the others in place, and the matching parts with their origins. This scenario is tested by
`PayloadBuilder_Build_MessageWithTextMarksAndAttachments_KeepsOrderAndMakesReferences`.

**PayloadBuilder_Build_Attachments_CarryOriginalAndProjectRelativePaths**: A file, a folder, and a picture are listed
with their kinds, names, both paths, sizes, and number of files. This scenario is tested by
`PayloadBuilder_Build_Attachments_CarryOriginalAndProjectRelativePaths`.

**PayloadBuilder_Build_Mark_CarriesGeometryInPicturePixelsNoteAndImageFacts**: A mark dragged from the bottom-right to
the top-left is described with corners in normal form, its bounding box, its note, the fingerprints and format of its
picture, and the picture and area of the composer. This scenario is tested by
`PayloadBuilder_Build_Mark_CarriesGeometryInPicturePixelsNoteAndImageFacts`.

**PayloadBuilder_Build_Marks_CarrySourceElementsWhereKnownAndNotAvailableOtherwise**: A mark around a node carries the
node with its text, line, and box; a mark on an SVG picture carries no elements, not available, and the reason. This
scenario is tested by `PayloadBuilder_Build_Marks_CarrySourceElementsWhereKnownAndNotAvailableOtherwise`.

**PayloadBuilder_Build_MarksOnTwoPictures_GivesOneImageForEachInOrderOfFirstMark**,
**PayloadBuilder_Build_SamePictureAsTwoObjects_GivesOneImage**, and
**PayloadBuilder_Build_ComposerReturnsWrongNumberOfAreas_ThrowsInvalidOperationException**: The grouping of marks by
picture, and the failure of the build when the composer does not give an area for every mark. These scenarios are tested
by the tests of the same names.

**PayloadBuilder_Build_MarkWithoutChip_IsOmittedAndRepeatedChipIsOneMark**: Of three marks, only the one with chips is
described, once, and the picture of the others is not drawn. This scenario is tested by
`PayloadBuilder_Build_MarkWithoutChip_IsOmittedAndRepeatedChipIsOneMark`.

**PayloadBuilder_Build_TextOnlyOrChipsOnly_HasNoImagesAndDrawsNothing** and
**PayloadBuilder_Build_EmptyMessage_GivesEmptyPayload**: A text-only message and a message of one attachment have no
image and the composer is not asked to draw; an empty message gives an empty payload. These scenarios are tested by the
tests of the same names.

**PayloadBuilder_Build_AttachmentsThatAreNotTheChipsInOrder_ThrowsArgumentException** and
**PayloadBuilder_Build_MarkChipWithoutMarkOrWithOtherNumber_ThrowsArgumentException**: Too few, too many, and
attachments in the wrong order, a chip of an unknown mark, and a chip with another number are refused. These scenarios
are tested by the tests of the same names.

**PayloadBuilder_BuildAsync_GivesTheSamePayloadAsBuildAndHonorsCancellation**: The payload built in the background has
the same text, parts, and image as the direct one, and a canceled token cancels the build. This scenario is tested by
`PayloadBuilder_BuildAsync_GivesTheSamePayloadAsBuildAndHonorsCancellation`.

**PayloadBuilder_Build_Payload_IsNotChangedByLaterChangesToDraftAndRegistry**: After the draft is cleared and written
again and the registry is cleared, the payload has its text, area, and note. This scenario is tested by
`PayloadBuilder_Build_Payload_IsNotChangedByLaterChangesToDraftAndRegistry`.

**PayloadBuilder_BuildAsyncAsMessageBuilder_ReturnsSentMessageWithPayloadAndMarksOfChips**: Of two marks, one has a chip
in the message, which also holds a file. Built through `IMessageBuilder`, the result holds the same snapshot, only the
mark with the chip, the attachments, a payload with the text `Look: [Mark 2][Attachment 1: brief.txt]`, one image, and
the time of the payload. This scenario is tested by
`PayloadBuilder_BuildAsyncAsMessageBuilder_ReturnsSentMessageWithPayloadAndMarksOfChips`.

**PayloadBuilder_Build_MissingArguments_ThrowArgumentNullException**: A missing composer, snapshot, marks, or
attachments is refused. This scenario is tested by `PayloadBuilder_Build_MissingArguments_ThrowArgumentNullException`.

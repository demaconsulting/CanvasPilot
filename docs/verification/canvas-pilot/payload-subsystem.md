## Payload

### Verification Approach

The `Payload` subsystem is verified by integration tests defined in `PayloadSubsystemTests.cs`. The tests use the real
`ImageRenderService` of `Preview` to draw a Mermaid diagram and an SVG file in a temporary project folder, the real
`MarkRegistry` and `MarkSourceResolver` of `Annotation` to make marks on the pictures, the real `ComposerDocument` and
`AttachmentStore` of `Composer` to compose a message and put an attached file into the project, and the real
`MarkImageComposer`, `PayloadBuilder`, and `PayloadDescriber`, the way the chat will use them. They check the observable
result: the text and the parts of the payload, the annotated picture and the cut-outs decoded from their PNG files, the
geometry and the source elements of the marks, the attachment paths, the immutability of the payload, and the
description. The subsystem has no dependency on the user interface, so no substitute is needed.

### Test Environment

A temporary folder per test holds the project and a folder for the file that is attached from outside the project. No
network, display, or special permission is needed.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- The text of the payload has `[Mark n]` and `[Attachment n: name]` at the places the user put them, and the parts keep
  the order and the origin of the text.
- The annotated picture has the size of the picture that was shown, shows the marks in the colors and with the numbers
  they have, and keeps the pixels away from the marks as they were shown; each cut-out has the size of its area and is
  that region of the annotated picture.
- The marks have their number, shape, points, bounding box inside the picture, and note; a mark on a diagram names the
  nodes under it, the head of an arrow first; a mark on an SVG picture says that the picture cannot tell, with the
  reason.
- The attachment has its original path and its path in the project, and the file is in the project.
- The payload and its description do not change when the draft is cleared and written again and the marks are cleared.
- The description lists the text, the attachment, the picture facts, and the marks with their notes and source elements.

### Test Scenarios

**PayloadSubsystem_Build_MessageText_HasReferencesInOrderWithOrigins**: A message is typed, with two marks, an attached
file, and a dictated ending, and built. The text is
`Make [Mark 1] bigger than [Mark 2], see [Attachment 1: brief.txt] - said thank you` and the parts are the typed text,
the marks, the attachment, and the dictated text in that order. This scenario is tested by
`PayloadSubsystem_Build_MessageText_HasReferencesInOrderWithOrigins`.

**PayloadSubsystem_Build_MarkedPicture_HasAnnotatedImageWithMarksDrawnAndNumbered**: The one image of the payload names
the file and holds the fingerprints and format of the picture, is a picture of the same size, has the color of mark one
on the left line of the rectangle and in its badge, and has the alpha of the last pixel of the shown picture. This
scenario is tested by `PayloadSubsystem_Build_MarkedPicture_HasAnnotatedImageWithMarksDrawnAndNumbered`.

**PayloadSubsystem_Build_Marks_HaveCropsThatAreRegionsOfTheAnnotatedPicture**: Each of the two marks has a cut-out whose
size is that of its area, whose area is inside the picture and starts at or before the mark, and whose every pixel is
the pixel of the annotated picture at that place. This scenario is tested by
`PayloadSubsystem_Build_Marks_HaveCropsThatAreRegionsOfTheAnnotatedPicture`.

**PayloadSubsystem_Build_Marks_HaveGeometryInPicturePixelsAndNote**: The marks are numbered one and two, are a rectangle
and an arrow, and have the points and bounding box of the marks, inside the picture; the first has its note and the
second none. This scenario is tested by `PayloadSubsystem_Build_Marks_HaveGeometryInPicturePixelsAndNote`.

**PayloadSubsystem_Build_Marks_HaveSourceElementsWhereKnownAndNotAvailableOtherwise**: The rectangle around the node
`Start` names that node with the text and source line of the renderer; the arrow from `Start` to `End` names `End` first
and `Start` second; a mark on an SVG picture is not available with the reason `svg provides no element positions`. This
scenario is tested by `PayloadSubsystem_Build_Marks_HaveSourceElementsWhereKnownAndNotAvailableOtherwise`.

**PayloadSubsystem_Build_Attachments_HaveOriginalAndProjectRelativePaths**: The attached file is listed with index one,
kind file, name, the path where it was, the project-relative path `attachments/brief.txt`, and its size, and the copy
exists in the project. This scenario is tested by
`PayloadSubsystem_Build_Attachments_HaveOriginalAndProjectRelativePaths`.

**PayloadSubsystem_Build_Payload_IsNotChangedByTheNextDraft**: After the payload is built the draft is cleared and
another text is typed, and the marks are cleared. The text, the annotated picture, the marks, and the description of the
payload are what they were. This scenario is tested by `PayloadSubsystem_Build_Payload_IsNotChangedByTheNextDraft`.

**PayloadSubsystem_Describe_Payload_ListsEverythingTheAssistantReceives**: The description is read as JSON and holds the
text, the project-relative path of the attachment, the fingerprints of the picture, two marks with the note of the
first, the text of the node under the first mark, and the dictated origin of the last part. This scenario is tested by
`PayloadSubsystem_Describe_Payload_ListsEverythingTheAssistantReceives`.

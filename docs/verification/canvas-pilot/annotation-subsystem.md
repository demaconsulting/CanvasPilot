## Annotation

### Verification Approach

The `Annotation` subsystem is verified by integration tests defined in `AnnotationSubsystemTests.cs`. The tests use the
real `ImageRenderService` of the `Preview` subsystem to draw a Mermaid diagram, an OpenSCAD model, and an SVG file in a
temporary project folder, and the real `MarkRegistry` and `MarkSourceResolver` on the pictures it returns, the way the
overlay and the payload will use them. They check the observable result: the number, geometry, note, and picture of each
mark, what each picture shows after the file has changed, and the parts of the source that are reported under a mark.
The subsystem has no dependency on the user interface or on another subsystem except `Preview`, so no substitute is
needed.

### Test Environment

A temporary folder per test is the project and holds the files that are drawn. No network, display, or special
permission is needed.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Marks drawn on a rendered diagram are numbered one, two, three in order, hold the picture they were drawn on, keep
  their geometry in the pixels of that picture however they were drawn, and keep a cleaned note.
- After the file changes and the picture is drawn again, the marks of the earlier picture are not shown on the new
  picture, are still part of the message while their chips are, and still hold the earlier pixels; a new mark continues
  the numbering.
- A mark around a node of a rendered Mermaid diagram reports that node with its text and source line; a mark over an
  anchor of a rendered OpenSCAD model reports the anchor with its pixel; a mark on an SVG picture reports that the
  picture provides no element positions.

### Test Scenarios

**AnnotationSubsystem_MarkPicture_MarksOnRenderedDiagram_AreNumberedAndKeepPixelGeometryAndNote**: A diagram is drawn. A
rectangle with a note that has line breaks, an ellipse dragged from the bottom-right to the top-left, and an arrow are
drawn on it. The marks are numbered one to three, the rectangle's box and the corners of the ellipse are in picture
pixels in normal form, the note is one clean line, and every mark holds the picture. This scenario is tested by
`AnnotationSubsystem_MarkPicture_MarksOnRenderedDiagram_AreNumberedAndKeepPixelGeometryAndNote`.

**AnnotationSubsystem_MarkPicture_FileChanged_MarksStayOnTheirOwnPicture**: A mark is drawn on a diagram, the file is
edited and drawn again, and a mark is drawn on the new picture. Each picture shows only its own mark, both marks are in
the message, the first mark still holds the first picture, and the numbers are one and two. This scenario is tested by
`AnnotationSubsystem_MarkPicture_FileChanged_MarksStayOnTheirOwnPicture`.

**AnnotationSubsystem_FindSourceElements_MermaidNode_IsReportedWithTextAndLine**: A rectangle is drawn around the node
`Start` of a diagram, with a margin. The node is reported with its identifier, text, source line, and box as the
renderer gave them, and the other node is not reported. This scenario is tested by
`AnnotationSubsystem_FindSourceElements_MermaidNode_IsReportedWithTextAndLine`.

**AnnotationSubsystem_FindSourceElements_ScadAnchor_IsReportedWithPixel**: An ellipse is drawn around the pixel of the
only anchor of a model. The anchor is reported by kind `anchor`, name, and pixel. This scenario is tested by
`AnnotationSubsystem_FindSourceElements_ScadAnchor_IsReportedWithPixel`.

**AnnotationSubsystem_FindSourceElements_SvgPicture_SaysNotAvailable**: A mark is drawn on an SVG picture. The result is
not available, with the reason `svg provides no element positions`, and holds no elements. This scenario is tested by
`AnnotationSubsystem_FindSourceElements_SvgPicture_SaysNotAvailable`.

## Annotation

### Verification Approach

The `Annotation` subsystem is verified by integration tests defined in `AnnotationSubsystemTests.cs`. The tests use the
real `ImageRenderService` of the `Preview` subsystem to draw a Mermaid diagram, an OpenSCAD model, and an SVG file in a
temporary project folder, and the real `MarkRegistry`, `MarkSourceResolver`, and `AnnotationOverlayViewModel` on the
pictures it returns, the way the application uses them. They check the observable result: the number, geometry, note,
and picture of each mark, what each picture shows after the file has changed, the marks that the overlay makes with each
tool, and the parts of the source that are reported under a mark. The subsystem has no dependency on another subsystem
except `Preview` and `Logging`, so no substitute is needed except the source of the shown picture, which the tests set
by hand; the views of the overlay are verified by the headless UI tests described in the unit verification document.

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
- The overlay makes a rectangle, an ellipse, a freehand line, and an arrow on a rendered diagram as marks one to four,
  with a note when one was typed, each pinned to the rendered picture; the mark around a node reports that node; and the
  switch for dragging the picture out was off while the tools were in use and is on again.
- After the file changes, the overlay shows only the marks of the picture that is shown, says how many are hidden, keeps
  the other marks, and continues the numbering.

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

**AnnotationSubsystem_DrawMarks_OverlayOnRenderedDiagram_MakesNumberedMarksPinnedToThePicture**: A diagram is drawn and
shown to an overlay. A rectangle is drawn around the node `Start` with a note, then an ellipse, a freehand line, and an
arrow, each with the overlay's own methods for the press, the move, and the release of the pointer, and then no tool is
chosen. There are four marks numbered one to four of the four kinds, the first has its note, every mark holds the
rendered picture, the node `Start` is found under the first mark, and the switch for dragging out was told `false` for
each tool and `true` at the end. This scenario is tested by
`AnnotationSubsystem_DrawMarks_OverlayOnRenderedDiagram_MakesNumberedMarksPinnedToThePicture`.

**AnnotationSubsystem_DrawMarks_FileChanged_OverlayShowsOnlyMarksOfTheShownPicture**: A mark is drawn on a diagram and
its chip is announced to the overlay. The file is edited and drawn again: the overlay shows no mark and counts one
hidden mark. A mark is drawn on the new picture and is number two and the only one shown; showing the first picture
again shows only the first mark. Both marks stay in the registry with their own pictures. This scenario is tested by
`AnnotationSubsystem_DrawMarks_FileChanged_OverlayShowsOnlyMarksOfTheShownPicture`.

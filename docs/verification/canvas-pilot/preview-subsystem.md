## Preview

### Verification Approach

The `Preview` subsystem is verified by integration tests defined in `PreviewSubsystemTests.cs`. The tests compose the
real `ImageRenderService` (and so the real CanvasNet viewer and PNG codec), a real `PreviewPaneViewModel` over a real
`ProjectExplorerViewModel` with the real `CanvasNetImageSourceFilter`, and a real `ImageExporter`, on temporary
project folders with real SVG files. No mocking is required for the behavior under test: the refresh schedulers are
hand-written fakes that hold callbacks until a test runs them, so no test waits for a delay, and the export dialogs
are a hand-written fake that plays the user. The views are verified by the headless UI tests described in the unit
verification documents.

### Test Environment

N/A - standard test environment. Tests use temporary local folders and real file I/O, including the real folder
watcher; no network, external services, or display are required.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Selecting an SVG in a project shows a picture with the SVG's size and format whose first pixel has the SVG's
  straight-alpha color.
- A file that cannot be drawn shows the error code `cannot-draw`, the picture is gone, and export is unavailable;
  selecting a good file then shows its picture and makes export available.
- Replacing the selected file on disk makes the pane show the new content after the debounce delay.
- Export writes a PNG file whose signature and header size match the picture, and copy hands the picture to the
  clipboard of the export dialogs.

### Test Scenarios

**PreviewSubsystem_RenderSelectedImage_SvgInProject_ShowsRenderedPixels**: A project holds a 40 by 20 SVG with a
half-transparent red rectangle. The explorer opens the project and selects the file and the pane renders it. The
pane has an image of 40 by 20 pixels, no error, is not busy, shows the status `wide.svg: 40 x 20 px, svg`, and the
first pixel is red with alpha 128. This scenario is tested by
`PreviewSubsystem_RenderSelectedImage_SvgInProject_ShowsRenderedPixels`.

**PreviewSubsystem_ReportRenderProblems_BrokenFile_ShowsErrorThenRecovers**: A broken SVG is selected: the pane has
an error with the code `cannot-draw` and the exporter cannot export. A good SVG is then selected: the error is gone,
a picture is shown, and the exporter can export. This scenario is tested by
`PreviewSubsystem_ReportRenderProblems_BrokenFile_ShowsErrorThenRecovers`.

**PreviewSubsystem_KeepPreviewCurrent_SelectedFileEdited_RendersNewContent**: A 40 by 20 picture is shown; the file
is replaced by a 10 by 10 SVG, the real watcher requests a render, the debounce delay passes, and the pane then
shows the 10 by 10 picture. This scenario is tested by
`PreviewSubsystem_KeepPreviewCurrent_SelectedFileEdited_RendersNewContent`.

**PreviewSubsystem_ExportImage_ShownPicture_WritesPngAndCopiesToClipboard**: With a 40 by 20 picture shown, export
writes the chosen file, which starts with the PNG signature and whose header holds width 40 and height 20, and copy
gives the shown picture to the export dialogs. This scenario is tested by
`PreviewSubsystem_ExportImage_ShownPicture_WritesPngAndCopiesToClipboard`.

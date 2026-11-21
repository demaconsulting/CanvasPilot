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
- The shown picture of a diagram names its file, holds fingerprints of the file and the pixels, and locates its
  nodes inside the picture; a picture of an SVG has no element positions.
- Content assigned to the pane's overlay slot stays there while different pictures are shown, a changed file is
  rendered again, and a file cannot be drawn.
- The slot in the pane's top bar is hidden while empty and shows the content assigned to it at its own size outside the
  scaled picture; this is verified by the pane's unit and headless tests.
- The shown picture can be prepared for dragging out as a file that is a PNG of exactly that picture, which is reused
  for the same picture and is another file for another picture.
- Exported at a size that is chosen in the export dialog, the shown file is drawn again and written as a PNG of exactly
  that size with its resolution, and the choice is offered again the next time.
- Notes that another part gives for the shown picture are listed after the notes of the renderer, are replaced by the
  next notes of the same group, open the list, and are gone when the next picture is drawn.

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

**PreviewSubsystem_ReportPictureFacts_DiagramAndSvg_ShowsIdentityAndPositions**: A project holds a two-node Mermaid
diagram and an SVG. After the diagram is selected the shown picture names the diagram's file, has a 64-character file
fingerprint, has the snapshot key made of the path and the pixel fingerprint, and has boxes for the nodes `Start` and
`End` that lie inside the picture. After the SVG is selected its picture has a different pixel fingerprint and no
positions and no anchors. This scenario is tested by
`PreviewSubsystem_ReportPictureFacts_DiagramAndSvg_ShowsIdentityAndPositions`.

**PreviewSubsystem_HostOverlay_ContentAssigned_StaysWhilePicturesChange**: A pane over the real render service has
content assigned to its overlay slot before the project opens. One SVG is shown, then another, then a broken file,
and then the first SVG is edited and selected again. After each of these the slot holds the same object, and the pane
shows the picture, the error, or the new picture next to it. This scenario is tested by
`PreviewSubsystem_HostOverlay_ContentAssigned_StaysWhilePicturesChange`.

**PreviewSubsystem_DragImageOut_ShownPicture_ProvidesPngFileAndBytes**: With the real render service and a pane that
has a drag source over a temporary folder, the 40 by 20 picture of `wide.svg` is prepared twice, which gives the same
file twice, and its bytes equal the encoding of the shown picture, start with the PNG signature, and hold width 40 and
height 20. Showing `good.svg` gives another file named `good-`. The handling of the pointer and the data offered to the
drag are verified with the `PreviewPane` unit, and which programs accept the dropped picture is checked by hand (see the
verification of `ImageDragSource`). This scenario is tested by
`PreviewSubsystem_DragImageOut_ShownPicture_ProvidesPngFileAndBytes`.

**PreviewSubsystem_RenderAtRequestedSize_ShownFile_IsDrawnAtTheBoxAndEncodedWithResolution**: With the real render
service, the file of the shown 40 by 20 picture and the folder of the project that the pane gives as the source of the
picture are drawn again into a 400 by 400 box with a white margin. The picture is exactly 400 by 400, its corner is
white, the red picture is in the middle, and the PNG that is encoded from it with 150 dots per inch decodes at 400 by
400 and holds 5906 pixels per meter in its physical size chunk. This scenario is tested by
`PreviewSubsystem_RenderAtRequestedSize_ShownFile_IsDrawnAtTheBoxAndEncodedWithResolution`.

**PreviewSubsystem_ShowNotesOfOtherParts_ListedWithRendererNotesAndGoWithTheNextPicture**: With a picture shown, a part
gives a note of the group `style`, and then another note of the same group; only the second is listed, the header says
`1 note`, the list is open, and after another file is drawn the list is empty. This scenario is tested by
`PreviewSubsystem_ShowNotesOfOtherParts_ListedWithRendererNotesAndGoWithTheNextPicture`.

**PreviewSubsystem_ExportAtPreset_ShownPicture_WritesPngOfThePresetSizeAndRemembersTheChoice**: With the 40 by 20
picture shown, a source of one size (300 by 300 at 200 dots per inch), and a user who works the real export dialog view
model, the first export offers the picture as it is shown and the size, and writes a PNG whose header holds 300 by 300
and whose physical size chunk holds 7874 pixels per meter; the second export offers the size already selected. This
scenario is tested by `PreviewSubsystem_ExportAtPreset_ShownPicture_WritesPngOfThePresetSizeAndRemembersTheChoice`.

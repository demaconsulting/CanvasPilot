### ImageExporter

#### Verification Approach

`ImageExporter` is verified by unit tests in `ImageExporterTests.cs`. They use a hand-written
`FakeRenderedImageSource` for the shown picture, the real `ImageRenderService` for PNG encoding, the hand-written
`FakeExportDialogs` that plays the user (a chosen save path, or cancel; a clipboard that works or refuses; it
implements only `IExportDialogs` of the `Preview` subsystem), an NSubstitute `IExportDialogs` in one test, and a
temporary folder for the written files. The real clipboard and save picker belong to the main window and are
verified by `MainWindowViewTests.cs` (the clipboard under the headless platform) and manually before a release (the
platform save picker, which cannot be shown headless).

#### Test Environment

Standard test environment with temporary local folders and real file I/O; no network, external services, or display.
The headless clipboard test runs under the Avalonia headless platform.

#### Acceptance Criteria

- All tests pass with zero failures.
- Export is possible only while a picture is shown, and each change is announced once.
- Export writes a PNG file with the signature at the chosen path, adds `.png` when the chosen name lacks it, writes
  nothing when the user cancels or no picture is shown, and tells the user when the file cannot be written.
- The exporter works with a substitute that implements only IExportDialogs, which proves it does not depend on the
  application's dialog service.
- The suggested file name is the source file's stem with `.png`, including for `.vl.json` files.
- Copy gives the shown picture to the export dialogs and says nothing when it works; it tells the user when the
  clipboard cannot be used and does nothing without a picture.
- The headless clipboard takes a picture and returns it as a bitmap of the same size.

#### Test Scenarios

**ImageExporter_CanExport_FollowsShownPicture_AndRaisesEvent**: A picture appearing and then going away changes
`CanExport` from false to true and back and raises the event twice. This scenario is tested by
`ImageExporter_CanExport_FollowsShownPicture_AndRaisesEvent`.

**ImageExporter_ExportPngAsync_PictureShown_WritesValidPngToChosenFile**: With a picture shown and a chosen file,
export writes a file that starts with the PNG signature and has content beyond it, with no message. This scenario is
tested by `ImageExporter_ExportPngAsync_PictureShown_WritesValidPngToChosenFile`.

**ImageExporter_ExportPngAsync_NameWithoutExtension_AppendsPng**: A chosen name without an extension is saved with
`.png`. This scenario is tested by `ImageExporter_ExportPngAsync_NameWithoutExtension_AppendsPng`.

**ImageExporter_ExportPngAsync_CancelledOrNoPicture_WritesNothing**: Export without a picture, and with a cancelled
dialog, writes no file and reports nothing. This scenario is tested by
`ImageExporter_ExportPngAsync_CancelledOrNoPicture_WritesNothing`.

**ImageExporter_ExportPngAsync_UnwritableTarget_ShowsMessage**: A target in a folder that does not exist shows the
message `The image could not be saved: ...` titled `Export as PNG`. This scenario is tested by
`ImageExporter_ExportPngAsync_UnwritableTarget_ShowsMessage`.

**ImageExporter_SuggestFileName_SourcePath_ReplacesImageExtensionWithPng**: `cover.svg`, `diagram.mmd`,
`chart.vl.json`, `CHART.VL.JSON`, `my.chart.svg`, and `.svg` suggest `cover.png`, `diagram.png`, `chart.png`,
`CHART.png`, `my.chart.png`, and `image.png`. This scenario is tested by
`ImageExporter_SuggestFileName_SourcePath_ReplacesImageExtensionWithPng`.

**ImageExporter_CopyImageAsync_PictureShown_CopiesPictureToClipboard**: Copy gives the shown picture to the dialog
service and reports nothing. This scenario is tested by
`ImageExporter_CopyImageAsync_PictureShown_CopiesPictureToClipboard`.

**ImageExporter_CopyImageAsync_ClipboardUnavailableOrNoPicture_ShowsMessageOrDoesNothing**: Copy with no picture does
nothing; with a picture and a clipboard that refuses it shows a message titled `Copy Image` that mentions the
clipboard. This scenario is tested by
`ImageExporter_CopyImageAsync_ClipboardUnavailableOrNoPicture_ShowsMessageOrDoesNothing`.

**ImageExporter_ExportAndCopy_UsingExportDialogsOnly_DoNotRequireDialogService**: Export and copy run against a
substitute that implements only IExportDialogs: the PNG is written to the path the substitute returns, the picture is
copied through it exactly once, and no message is shown, so the exporter needs nothing of the application's dialog
service. This scenario is tested by ImageExporter_ExportAndCopy_UsingExportDialogsOnly_DoNotRequireDialogService.

**ImageExporter_InvalidArguments_Throw**: Missing collaborators, a blank path, and missing export dialogs are
refused. This scenario is tested by `ImageExporter_InvalidArguments_Throw`.

**MainWindowView_CopyImageToClipboard_Picture_ReportsResultAndIsReadable**: Under the headless platform the main
window puts a 2 by 2 picture on its clipboard, answers `true`, and the clipboard returns a bitmap of 2 by 2 pixels.
This scenario is tested by `MainWindowView_CopyImageToClipboard_Picture_ReportsResultAndIsReadable`.

**MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu**: The Image menu items are unusable with no picture,
become usable when the preview shows one, and are unusable again when the project is closed. This scenario is tested
by `MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu`.

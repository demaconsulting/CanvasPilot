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
- With sizes to offer the user is asked how to export before a file is asked for, the picture as it is shown is first,
  and the choice is offered again the next time; without sizes no question is asked and the export is as before.
- A size draws the source file again with exactly the settings of the size and writes the encoding of that picture with
  its resolution; a size that cannot be drawn is explained and no file is written; canceling at either dialog writes
  nothing.
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

**ImageExporter_ExportPngAsync_NoPresetSourceOrNoPresets_DoesNotAskAndExportsAsShown**: With no source of sizes and with
a source that gives none, the user is never asked how to export and both exports write the same file as before. This
scenario is tested by `ImageExporter_ExportPngAsync_NoPresetSourceOrNoPresets_DoesNotAskAndExportsAsShown`.

**ImageExporter_ExportPngAsync_AsRenderedChosen_OffersListAndEncodesShownPicture**: With two sizes offered the list is
the picture as it is shown, then the two sizes in order, with no earlier choice; choosing the picture as it is shown
draws nothing again and writes the bytes of the encoder for the shown picture. This scenario is tested by
`ImageExporter_ExportPngAsync_AsRenderedChosen_OffersListAndEncodesShownPicture`.

**ImageExporter_ExportPngAsync_PresetChosen_RendersWithExactSettingsAndWritesEncoderBytes**: Choosing a size draws the
source file of the shown picture once with exactly the size, background, and resolution of the size in the folder of the
project, encodes the new picture (and not the shown one) with the resolution, writes the bytes of the encoder, and shows
no message. This scenario is tested by
`ImageExporter_ExportPngAsync_PresetChosen_RendersWithExactSettingsAndWritesEncoderBytes`.

**ImageExporter_ExportPngAsync_TransparentPresetWithoutDpi_AsksForTransparencyAndNoResolution**: A transparent size with
only a width is drawn with transparency and no background, the folder of the file is used when the picture source gives
no project folder, and no resolution is passed to the encoder. This scenario is tested by
`ImageExporter_ExportPngAsync_TransparentPresetWithoutDpi_AsksForTransparencyAndNoResolution`.

**ImageExporter_ExportPngAsync_CancelAtEitherDialog_DrawsAndWritesNothing**: Canceling at the size dialog, and choosing
a size and then canceling the save dialog, draw nothing, write nothing, and report nothing. This scenario is tested by
`ImageExporter_ExportPngAsync_CancelAtEitherDialog_DrawsAndWritesNothing`.

**ImageExporter_ExportPngAsync_RenderFails_ShowsMessageWithReasonAndWritesNoFile**: A size that the renderer refuses
shows one message titled `Export as PNG` with the reason of the renderer and writes no file. This scenario is tested by
`ImageExporter_ExportPngAsync_RenderFails_ShowsMessageWithReasonAndWritesNoFile`.

**ImageExporter_ExportPngAsync_RenderCancelledOrEncoderRefuses_WritesNoFile**: A render that was cancelled writes no
file and says nothing; an encoder that refuses the resolution shows `The image could not be saved:` and writes no file.
This scenario is tested by `ImageExporter_ExportPngAsync_RenderCancelledOrEncoderRefuses_WritesNoFile`.

**ImageExporter_ExportPngAsync_SecondExport_OffersTheLastChoice**: The first offer has no earlier choice and the second
offers the choice made in the first. This scenario is tested by
`ImageExporter_ExportPngAsync_SecondExport_OffersTheLastChoice`.

**ImageExporter_ExportPngAsync_SvgAtPresetSize_WritesPngOfThatSizeWithResolution**: With the real renderer, an SVG that
is 1600 by 640 exported at 1920 by 1080 with a background and a resolution is a PNG that decodes at 1920 by 1080, has
the background in its corner, and holds the physical size chunk. This scenario is tested by
`ImageExporter_ExportPngAsync_SvgAtPresetSize_WritesPngOfThatSizeWithResolution`.

**MainWindowView_CopyImageToClipboard_Picture_ReportsResultAndIsReadable**: Under the headless platform the main
window puts a 2 by 2 picture on its clipboard, answers `true`, and the clipboard returns a bitmap of 2 by 2 pixels.
This scenario is tested by `MainWindowView_CopyImageToClipboard_Picture_ReportsResultAndIsReadable`.

**MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu**: The Image menu items are unusable with no picture,
become usable when the preview shows one, and are unusable again when the project is closed. This scenario is tested
by `MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu`.

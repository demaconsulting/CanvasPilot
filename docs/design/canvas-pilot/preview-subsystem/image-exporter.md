### ImageExporter

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`ImageExporter` is the `IImageExporter` (of the `Preview` subsystem) behind the Image menu. It exports the picture that
the preview pane shows: as a PNG file at a place the user chooses, or as an image on the clipboard. It can export only
while a picture is shown, and tells its listeners when that changes so the menu items follow.

#### Data Model

**IRenderedImageSource**: `CurrentImage` and `CurrentImageChanged`, implemented by the preview pane.

**CanExport**: true while `CurrentImage` is not `null`. `CanExportChanged` is raised whenever the picture changes.

**ExportPreset**: the way to export, a plain value of the `Preview` subsystem: an identifier, a name, a width and a
height (either may be missing), a resolution, transparency, a background color, and the origin (`AsRendered`,
`StyleKit`, or `Application`). `ExportPreset.AsRendered` is the picture as it is shown. `ToRenderSettings` gives the
`RenderSettings` for a drawing; a transparent preset gives no background. `IExportPresetSource.GetPresets` supplies the
list.

**SuggestFileName(sourcePath)**: the file name without its image source extension, followed by `.png`; a Vega-Lite
`.vl.json` name loses both parts, and a name that is only an extension becomes `image.png`.

#### Key Methods

**ExportPngAsync**: Does nothing without a picture. When the exporter has a source of sizes (`IExportPresetSource`) that
gives at least one preset, it first asks `IExportDialogs.PickExportPresetAsync` with the picture as it is shown
(`ExportPreset.AsRendered`) first and the presets after it in the order given, and the choice of the last time; `null`
means the user cancelled and nothing happens. The choice is kept for the rest of the session. Without a source, or with
no presets, no question is asked. Then it asks `IExportDialogs.PickSaveFileAsync` for a file with the suggested name and
the filter `PNG image` (`*.png`); `null` means the user cancelled and nothing is written. A chosen name without the
`.png` extension gets it. The picture as it is shown is encoded with `IImageRenderService.EncodePng`. For a size, the
source file of the shown picture is drawn again with `IImageRenderService.RenderAsync` and the settings of the preset
(`ExportPreset.ToRenderSettings`: the width and height, transparency or the background, and the resolution), in the
folder of the project that the picture source reports, or the folder of the file when it reports none, and the new
picture is encoded with `EncodePng(image, dpi)`. A render that fails is logged and shown as
`The picture could not be drawn at that size: {message}` and writes no file; a render that was cancelled writes no file
and says nothing. The bytes are written with `File.WriteAllBytesAsync`. An `IOException`, `UnauthorizedAccessException`,
`NotSupportedException`, or `ArgumentException` (such as a resolution that the encoder refuses) is logged and shown with
`ShowMessageAsync` as `The image could not be saved: ...`. A successful export is logged.

**CopyImageAsync**: Does nothing without a picture. Otherwise calls `IExportDialogs.CopyImageToClipboardAsync`. The
main window creates a `WriteableBitmap` from the picture (through `RenderedImageBitmap.Create`) and puts it on the
window's clipboard with `SetBitmapAsync`, then flushes the clipboard so the image stays available after the
application exits. A `false` answer is logged and shown as `The image could not be copied to the clipboard.`

#### Error Handling

The constructor and methods throw `ArgumentNullException` for missing arguments, and `SuggestFileName` throws
`ArgumentException` for a blank path. Problems saving or copying are messages to the user, never thrown. Failures to
write a log entry are ignored.

#### Dependencies

- **ImageRenderService** (through `IImageRenderService`) — encodes the picture as PNG and draws the source file again
  at a size.
- **PreviewPane** (through `IRenderedImageSource`) — supplies the shown picture and the folder of the project.
- **IExportPresetSource** — the interface, owned by the `Preview` subsystem, through which the exporter learns the
  sizes to offer; the application composes a supplier (the style of the project). Optional: without it the exporter
  never asks about sizes.
- **IExportDialogs** — the interface, owned by the `Preview` subsystem, through which the exporter asks the user
  how to export, where to save, copies to the clipboard, and shows messages; the main window's dialog service
  implements it. The exporter does not depend on `AppShell`.
- **ExportPresetDialog** — the window that the main window shows for `PickExportPresetAsync`.
- **RollingFileLogger** — optional; records exports and failures.

#### Callers

- `MainWindowShell` — runs `ExportPngAsync` and `CopyImageAsync` from the Image menu and follows
  `CanExportChanged`.
- `ApplicationComposition` — creates the exporter over the preview pane, the render service, and the sizes of the
  style of the project.

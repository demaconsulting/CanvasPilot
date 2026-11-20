### ImageExporter

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`ImageExporter` is the `IImageExporter` (of the `Preview` subsystem) behind the Image menu. It exports the picture that
the preview pane shows: as a PNG file at a place the user chooses, or as an image on the clipboard. It can export only
while a picture is shown, and tells its listeners when that changes so the menu items follow.

#### Data Model

**IRenderedImageSource**: `CurrentImage` and `CurrentImageChanged`, implemented by the preview pane.

**CanExport**: true while `CurrentImage` is not `null`. `CanExportChanged` is raised whenever the picture changes.

**SuggestFileName(sourcePath)**: the file name without its image source extension, followed by `.png`; a Vega-Lite
`.vl.json` name loses both parts, and a name that is only an extension becomes `image.png`.

#### Key Methods

**ExportPngAsync**: Does nothing without a picture. Otherwise asks `IExportDialogs.PickSaveFileAsync` for a file
with the suggested name and the filter `PNG image` (`*.png`); `null` means the user cancelled and nothing is
written. A chosen name without the `.png` extension gets it. The picture is encoded with
`IImageRenderService.EncodePng` and written with `File.WriteAllBytesAsync`. An `IOException`,
`UnauthorizedAccessException`, `NotSupportedException`, or `ArgumentException` is logged and shown with
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

- **ImageRenderService** (through `IImageRenderService`) — encodes the picture as PNG.
- **PreviewPane** (through `IRenderedImageSource`) — supplies the shown picture.
- **IExportDialogs** — the interface, owned by the `Preview` subsystem, through which the exporter asks the user
  where to save, copies to the clipboard, and shows messages; the main window's dialog service implements it. The
  exporter does not depend on `AppShell`.
- **RollingFileLogger** — optional; records exports and failures.

#### Callers

- `MainWindowShell` — runs `ExportPngAsync` and `CopyImageAsync` from the Image menu and follows
  `CanExportChanged`.
- `ApplicationComposition` — creates the exporter over the preview pane and the render service.

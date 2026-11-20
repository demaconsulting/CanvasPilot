## Preview

![Preview Structure](PreviewSubsystemView.svg)

### Overview

The `Preview` subsystem turns the image source file that the user selects into a picture on the screen, and the
picture into a PNG file or a clipboard image. Its boundary starts with the selection and the project folder of
the project explorer and ends with the picture shown in the main window, files written where the user chooses,
and the system clipboard. It contains three units: `ImageRenderService`, which draws a file with the CanvasNet
viewer library in the background and encodes pictures as PNG; `PreviewPane`, the view model and view that follow
the selection, show the picture, the busy state, errors, and notes, and render again when files change; and
`ImageExporter`, which the Image menu uses to save or copy the shown picture. CanvasNet is used in exactly one
unit, `ImageRenderService`, so the other units and their tests work with a substitute render service.

### Interfaces

**Render API**: The in-process interface through which pictures are drawn and encoded.

- *Type*: In-process .NET API (`IImageRenderService`).
- *Role*: Provider.
- *Contract*: `RenderAsync(filePath, projectFolder, cancellationToken)` returns a `RenderResult` that holds a
  `RenderedImage` (width, height, format, straight-alpha RGBA pixels, and notes), a `RenderError` (code, message,
  and optional hint), or a cancelled outcome; `EncodePng(image)` returns the bytes of a PNG file.
- *Constraints*: Ordinary failures to draw a file are results, never exceptions. The drawn file may read other
  files only from inside the project folder. A cancelled token gives a cancelled result.

**Picture Source**: How the exporter finds the shown picture.

- *Type*: In-process .NET API (`IRenderedImageSource`).
- *Role*: Provider (the preview pane) and consumer (the exporter).
- *Contract*: `CurrentImage` is the shown picture or `null`; `CurrentImageChanged` is raised after it changes.

**Image Exporter**: The unit behind the Image menu.

- *Type*: In-process .NET API (`IImageExporter`, with `NullImageExporter`, and `IExportDialogs`).
- *Role*: Provider of both contracts; the main window's shell consumes `IImageExporter`, and the main window's
  dialog service implements `IExportDialogs`.
- *Contract*: `CanExport` is true while a picture is shown; `ExportPngAsync(dialogs)` asks for a file with
  `IExportDialogs.PickSaveFileAsync` and writes a PNG; `CopyImageAsync(dialogs)` calls
  `IExportDialogs.CopyImageToClipboardAsync`; `ShowMessageAsync` reports problems.

**Project Explorer**: The selection and project folder that drive the preview.

- *Type*: In-process .NET API (`ProjectExplorerViewModel`, `IRefreshScheduler`).
- *Role*: Consumer.
- *Contract*: The pane observes `SelectedItem` and `FolderPath`, reads `SelectedFile`, and uses the application's
  refresh scheduler to delay renders after file changes.

**Project Folder**: The files the picture depends on.

- *Type*: File system (`FileSystemWatcher` over the project folder and its sub-folders).
- *Role*: Consumer.
- *Contract*: Created, changed, deleted, and renamed files cause a delayed new render, except changes inside the
  hidden `.canvaspilot` data folder and to the project file.

**Preview Pane View**: The user interface.

- *Type*: Avalonia control (`PreviewPaneView`) hosted by the main window's workspace.
- *Role*: Provider.
- *Contract*: A scrollable picture that is scaled to fit or shown at its own size, a busy indicator, an error
  panel with the error code, message, and hint, a status line, and an expandable list of notes.

### Design

1. The application composes one `ImageRenderService`, one `PreviewPaneViewModel` over the project explorer and the
   refresh scheduler, and one `ImageExporter` over the pane. The workspace shows the pane; the main window's
   shell uses the exporter for the Image menu.
2. When the explorer's selected file changes, the pane cancels the render that is running, starts a new one
   through `IImageRenderService.RenderAsync`, and shows the busy state. A counter identifies each render; a result
   that arrives for an older render is ignored.
3. When the render finishes, the pane shows the picture and its notes, or the error with its code and hint.
   The view converts the picture's pixels to an Avalonia bitmap and shows it.
4. When files in the project folder change, the pane asks the scheduler for a render after a short delay. The
   current picture stays visible until the new result arrives.
5. The exporter is enabled while the pane shows a picture. Export asks the dialog service for a file name and
   writes the PNG that `EncodePng` produces from the shown pixels; Copy hands the picture to the dialog service,
   whose window implementation puts a bitmap on the clipboard.

The subsystem does not depend on `AppShell`: the exporter asks the user only through `IExportDialogs`, which this
subsystem defines and the main window implements; the application composes the units in `ApplicationComposition`.

### Third-party items

The subsystem consumes `DemaConsulting.CanvasNet.Viewer` (drawing, with its core library's PNG codec) in
`ImageRenderService`, and Avalonia and CommunityToolkit.Mvvm in the pane and its view.

## Preview

![Preview Structure](PreviewSubsystemView.svg)

### Overview

The `Preview` subsystem turns the image source file that the user selects into a picture on the screen, and the picture
into a PNG file, a clipboard image, or a picture dragged into another program. Its boundary starts with the selection
and the project folder of the project explorer and ends with the picture shown in the main window, files written where
the user chooses, the system clipboard, and the operating system's drag and drop. It contains six units:
`ImageRenderService`, which draws a file with the CanvasNet viewer library in the background, at a size if asked, and
encodes pictures as PNG; `PreviewPane`, the view model and view that follow the selection, show the picture on a stage
at a fitted size or a zoom, with the busy line, errors, and notes, render again when files change, tell the facts of the
picture, host a layer over the picture, and start the drag of the picture; `ImageExporter`, which the Image menu uses to
save or copy the shown picture, as it is shown or at a size; `ExportPresetDialog`, the window in which the user chooses
how the picture is exported; `ImageDragSource`, which writes the picture as a PNG file for dragging it out; and
`ThumbnailProvider`, which makes the small pictures that the project explorer and the welcome screen show for files.
CanvasNet is used in exactly one unit, `ImageRenderService`, so the other units and their tests work with a substitute
render service.

### Interfaces

**Render API**: The in-process interface through which pictures are drawn and encoded.

- *Type*: In-process .NET API (`IImageRenderService`).
- *Role*: Provider.
- *Contract*: `RenderAsync(filePath, projectFolder, cancellationToken)` returns a `RenderResult` that holds a
  `RenderedImage` (width, height, format, straight-alpha RGBA pixels, notes, a fingerprint of the source file and of
  the pixels, a snapshot key, and the positions of the elements of a diagram, chart, or model), a `RenderError`
  (code, message, and optional hint), or a cancelled outcome; `RenderAsync` with a `RenderSettings` draws the file at a
  requested size, over a background or transparent, and at a page resolution; `EncodePng(image)` returns the bytes of a
  PNG file and `EncodePng(image, dpi)` writes the resolution into it.
- *Constraints*: Ordinary failures to draw a file are results, never exceptions. The drawn file may read other
  files only from inside the project folder. A cancelled token gives a cancelled result.

**Picture Source**: How the exporter finds the shown picture.

- *Type*: In-process .NET API (`IRenderedImageSource`).
- *Role*: Provider (the preview pane) and consumer (the exporter).
- *Contract*: `CurrentImage` is the shown picture or `null`; `CurrentImageChanged` is raised after it changes;
  `ProjectFolder` is the folder of the open project, which a part that draws the shown file again at another size needs.

**Image Exporter**: The unit behind the Image menu.

- *Type*: In-process .NET API (`IImageExporter`, with `NullImageExporter`, and `IExportDialogs`).
- *Role*: Provider of both contracts; the main window's shell consumes `IImageExporter`, and the main window's
  dialog service implements `IExportDialogs`.
- *Contract*: `CanExport` is true while a picture is shown; `ExportPngAsync(dialogs)` asks how to export with
  `IExportDialogs.PickExportPresetAsync` when sizes are offered, asks for a file with
  `IExportDialogs.PickSaveFileAsync`, and writes a PNG, drawing the file again at the size that was chosen;
  `CopyImageAsync(dialogs)` calls
  `IExportDialogs.CopyImageToClipboardAsync`; `ShowMessageAsync` reports problems.

**Export Presets**: The sizes that a picture can be exported at.

- *Type*: In-process .NET API (`ExportPreset`, `IExportPresetSource`).
- *Role*: Consumer (the exporter and the export dialog); the supplier is composed by the application.
- *Contract*: An `ExportPreset` is a plain value (name, width and height, resolution, transparency, background, origin).
  `IExportPresetSource.GetPresets` gives the sizes to offer after the picture as it is shown, in order, and may give
  none. The `Preview` subsystem knows nothing about where the sizes come from.

**Render Notes**: Notes about the shown picture from another part.

- *Type*: In-process .NET API (`IRenderNoteSink`).
- *Role*: Provider (declared here; the preview pane takes the notes).
- *Contract*: `ShowNotes(group, notes)` replaces the notes of a group in the list of notes of the picture.

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
- *Contract*: A picture on a stage that is scaled down to fit or shown at a zoom from 25 to 400 percent with scroll
  bars, a zoom pill (Fit, actual size, smaller, label, larger) over the stage, a thin busy line along its top, an error
  card with the error code, message, and hint, a placeholder, and a drawer with the expandable list of notes. The pane
  view holds no status line: the status bar of the main window reports the facts of the picture that the pane view model
  tells (`FileName`, `ImageSizeText`, `FormatText`, `RenderTimeText`). The pane view model's `Overlay` property is a
  slot for content that the view shows exactly over the picture, laid out in the picture's own pixel coordinates and
  scaled together with the picture, so that the host's content needs no conversion between the screen and the picture;
  the slot is empty by default. A second slot, `Toolbar`, floats over the top center of the stage and is not scaled, for
  the host's controls of the overlay. The stage carries the menu of the picture, opened by a right-click or by Shift+F10
  or the Menu key with the stage focused, whose Copy image and Export as PNG items run the commands that the host puts
  in the view model's `CopyImageCommand` and `ExportPngCommand` slots and are usable only while those commands can run;
  the menu stays closed while the host turns `IsPictureMenuEnabled` off and over the controls that float over the stage.

**Thumbnails**: The small pictures of files.

- *Type*: In-process .NET API (`IThumbnailSource`, declared by the `Project` subsystem, and `ThumbnailProvider`).
- *Role*: Provider (`ThumbnailProvider`); the explorer and the welcome screen of the main window consume it.
- *Contract*: `CreateAsync(filePath, projectFolder, cancellationToken)` gives a new bitmap of 56 by 36 pixels, or `null`
  when the file cannot be drawn; the overload with a width and a height gives a picture of that size. The caller owns
  and disposes the bitmap, and the work is done off the user interface thread.

**Image Drag-out**: How the shown picture leaves the application by drag and drop.

- *Type*: In-process .NET API (`ImageDragSource`, and `PreviewPaneViewModel.PrepareDragFile`) and the operating
  system's drag and drop, started by `PreviewPaneView`.
- *Role*: Provider.
- *Contract*: `ImageDragSource.Prepare(image)` writes the picture as a PNG file named after its source file and the
  first eight characters of its pixel fingerprint in its folder and returns the path, or `null` when that fails;
  `Purge()` deletes the files the unit wrote. The view offers the file and a bitmap of the picture in one drag item.

### Design

1. The application composes one `ImageRenderService`, one `PreviewPaneViewModel` over the project explorer and the
   refresh scheduler, and one `ImageExporter` over the pane. The workspace shows the pane; the main window's
   shell uses the exporter for the Image menu.
2. When the explorer's selected file changes, the pane cancels the render that is running, starts a new one
   through `IImageRenderService.RenderAsync`, and shows the busy state. A counter identifies each render; a result
   that arrives for an older render is ignored.
3. When the render finishes, the pane shows the picture and its notes, or the error with its code and hint. The view
   converts the picture's pixels to an Avalonia bitmap and shows it. The picture also carries its identity (file, file
   fingerprint, pixel fingerprint, snapshot key) and the positions of its elements, which later units use to tie marks
   and messages to exactly this picture; the pane itself does not use them. The picture and the overlay slot share one
   surface of the picture's pixel size inside a `Viewbox`, so content the host assigns to `Overlay` is laid out in
   picture pixels at any zoom; the zoom is fixed by the size of the `Viewbox`. The slot keeps its content while pictures
   change, are rendered again, or fail to draw. The slot that floats over the stage, `Toolbar`, shows the host's
   controls for the overlay at their own size. The view model tells the file name, size, format, and render time of the
   picture, which the status bar of the main window shows.
4. When files in the project folder change, the pane asks the scheduler for a render after a short delay. The
   current picture stays visible until the new result arrives.
5. The exporter is enabled while the pane shows a picture. Export asks the dialog service how to export when sizes are
   offered (the Export PNG dialog), then for a file name, and writes the PNG that `EncodePng` produces from the shown
   pixels, or from the file drawn again at the chosen size; Copy hands the picture to the dialog service,
   whose window implementation puts a bitmap on the clipboard.
6. When the user presses the left button on the shown picture and moves the pointer, the view asks the pane for the
   picture as a file; `ImageDragSource` writes it as a PNG in its own folder, and the view starts the system's drag
   with that file and a bitmap of the picture. The application deletes the files in that folder when it starts and when
   it ends.
7. When the user right-clicks the stage, or presses Shift+F10 or the Menu key while the stage has the focus, the view
   opens the menu of the picture, whose items run the host's copy and export commands, so they do exactly what the
   host's own menu does; the subsystem holds no second copy of that logic. The menu stays closed while the host turns it
   off, which the host does while the pointer draws on the picture.
8. The explorer asks `ThumbnailProvider` for the thumbnail of each file that can be drawn and whose row is visible. The
   provider
   draws the file through the render service at 56 by 36 pixels on white, at most two at a time, keeps the pixels of the
   latest 256 thumbnails keyed by the file's path, time, size, and the picture size, and gives each caller a new bitmap.

The subsystem does not depend on `AppShell`: the exporter asks the user only through `IExportDialogs`, which this
subsystem defines and the main window implements; the application composes the units in `ApplicationComposition`.

### Third-party items

The subsystem consumes `DemaConsulting.CanvasNet.Viewer` (drawing, with its core library's PNG codec) in
`ImageRenderService`, and Avalonia and CommunityToolkit.Mvvm in the pane and its view.

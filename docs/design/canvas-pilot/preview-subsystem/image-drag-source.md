### ImageDragSource

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`ImageDragSource` prepares the shown picture for dragging it out of the application into another program, such as a
presentation, a mail message, or a folder. Programs that accept a dropped picture ask for a file or for image data, so
the unit writes the picture as a PNG file in a folder that belongs to the application. The preview view then offers that
file, together with the picture's bitmap, to the program the user drops them on. The unit has no dependency on the
user interface, so it is tested without a window.

#### Data Model

**Folder**: The folder that holds the files, passed to the constructor. The running application uses
`ImageDragSource.DefaultFolder`, a folder named `drag` in the `CanvasPilot` folder of the per-user local application
data folder; tests and the composition without an explicit folder use another one. The folder is created when the first
file is written, not before.

**File name**: The source file's name without its image source extension (the name that `ImageExporter.SuggestFileName`
suggests, without `.png`, so a Vega-Lite `.vl.json` loses both parts), with every character that a file name cannot hold
replaced by an underscore and at most 60 characters, a hyphen, the first eight characters of the picture's pixel
fingerprint (`RenderedImage.ContentHash`), and `.png`; for example `flow-1a2b3c4d.png`. The same picture of the same
file therefore always has the same name, and a changed picture of the same file has a different one.

**Own files**: The files this unit writes are those whose names match `<stem>-<8 lower-case hexadecimal digits>.png`,
and, while one is being written, the same name followed by `.tmp`. Only such files are deleted, and only directly in
the folder.

#### Key Methods

**Prepare(image)**: Builds the file name, combines it with the folder through `PathHelpers.SafePathCombine`, and returns
the path at once when a non-empty file of that name exists, so a picture is written once. Otherwise it creates the
folder, encodes the picture with `IImageRenderService.EncodePng`, writes the bytes to the name followed by `.tmp`, and
moves that file onto the final name, so a file that was cut short by a crash is never mistaken for the picture. An
`IOException`, `UnauthorizedAccessException`, `ArgumentException`, or `NotSupportedException` is logged as a warning
with the source file's path, the half-written file is deleted, and the result is `null`.

**Purge()**: Deletes the own files (see above) in the folder and returns how many it deleted. A folder that does not
exist, or that cannot be listed, is not an error: it returns zero, and a listing problem is logged. Other files, files
whose names almost match (for example seven digits or capital letters), folders, and the content of folders are never
touched.

**Lifetime**: `ApplicationComposition` creates the unit, calls `Purge` at start to remove files of earlier runs that
ended without cleaning up, and calls `Purge` again when it is disposed, so the files the application wrote do not
outlive it. A program that was given a dragged file has copied or read it before the drag ends, so the file is not
needed afterwards.

**Offering the file (in the view)**: `PreviewPaneViewModel.PrepareDragFile()` returns the path of the PNG of the shown
picture, or `null` when no picture is shown, dragging out is turned off (`IsDragOutEnabled`, which the host sets to
`false` while the pointer is used on the picture for something else), or the pane has no drag source. `PreviewPaneView`
remembers a press of the left button on the picture and, once the pointer has moved four device-independent pixels
along either axis while the button stays down, starts the drag: it asks for the file, gets the storage file for it from
the window's storage provider, loads the PNG as a bitmap, puts both into one `DataTransferItem` (`SetFile` and
`SetBitmap`), and calls its drag operation with `DragDropEffects.Copy`. A release or a lost pointer capture cancels the
pending drag, so a click starts nothing. The drag operation is a replaceable property of the view whose default is
`DragDrop.DoDragDropAsync`, because that call never completes under the headless platform; tests replace it to see
what is offered.

**Formats**: One item offers a file (on Windows a file drop list) and a bitmap (device-independent bitmap and PNG image
data). A target such as Windows Explorer takes the file, and a target such as a presentation or a mail message may take
either. Putting an item with both formats on the clipboard through the same Windows data object showed a file drop list
and PNG, device-independent-bitmap, and bitmap data; that the drag source offers the same formats is an inference from
the shared implementation, not something a test can show. If a particular program is found to mishandle one of the
formats, that format is to be left out of the item and the limitation noted in the user guide; the manual check in the
verification document records which programs were tried.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing encoder and `ArgumentException` for a blank folder;
`Prepare` throws `ArgumentNullException` for a missing picture. A folder that cannot be created or written, an encoder
that fails, and a name that cannot be combined are logged and give `null`, never an exception. A failure while the view
starts the drag (the storage provider, the bitmap, or the system's drag and drop) is caught in the view, written to the
log through `PreviewPaneViewModel.ReportDragProblem`, and leaves the pane working, because no code waits for the drag
to end. Failures to write a log entry are ignored.

#### Dependencies

- **ImageRenderService** (through `IImageRenderService`) — encodes the picture as PNG.
- **ImageExporter** — `SuggestFileName` gives the stem of the file name.
- **PathHelpers** — confines the file name to the folder.
- **RollingFileLogger** — optional; records problems preparing the file and cleaning the folder.

#### Callers

- `PreviewPane` — asks for the file when the user starts a drag, and holds the unit as its drag source.
- `ApplicationComposition` — creates the unit over the render service, and purges its folder at start and at the end.

### PreviewPane

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`PreviewPane` is the view model (`PreviewPaneViewModel`) and view (`PreviewPaneView`) that show the picture of the
file selected in the project explorer. It starts a render whenever the selection or project changes, cancels the
render that is still running, ignores results of renders that are no longer wanted, renders again after a delay
when files in the project change, and shows the picture, the busy state, the error with its code and hint, and the
notes of the render. The view model has no dependency on Avalonia's imaging; the view converts the pixels. The pane
also hosts one overlay slot over the picture, so that other units can place content on the picture in its own pixel
coordinates without the pane knowing them.

#### Data Model

**Image** / **HasImage** / **CurrentImage**: the shown `RenderedImage` or `null`. `CurrentImageChanged` is raised
after it changes (`IRenderedImageSource`).

**IsBusy** / **StatusText**: whether a render runs, and a line such as `Rendering a.svg...`,
`a.svg: 40 x 20 px, svg, 1 note`, or `a.svg: render failed`.

**Error**, **HasError**, **ErrorTitle** (`Render failed (code)`), **ErrorMessage**, **ErrorHint** (`Hint: ...` or
empty) and **HasErrorHint**: the failed render.

**Diagnostics**, **HasDiagnostics**, **DiagnosticsHeader** (`1 note`, `3 notes`): the notes of the shown picture.

**ShowPlaceholder** / **PlaceholderText**: true while there is no picture, error, or running render; the text says
that no project is open or that a file is to be selected.

**FitToWindow**: `bool`, true by default; scale the picture down to the pane, otherwise show it at its own size
with scroll bars.

**Overlay** / **HasOverlay**: `object?`, a slot for the content that is shown exactly over the picture; `null` by
default. The pane does not interpret it: the view shows it in a `ContentControl`, so the application's view locator
picks the view of whatever view model the host assigns, and the pane never refers to the code that fills the slot.
The slot belongs to the pane, not to a picture, so it keeps its content while pictures change, are rendered again, or
fail to draw; the host decides what the content shows then.

**IsDragOutEnabled** / **DragSource** / **PrepareDragFile()** / **ReportDragProblem(exception)**: `IsDragOutEnabled` is
a `bool`, true by default, that the host turns off while the pointer is used on the picture for something else, such as
drawing a mark. `DragSource` is the optional `ImageDragSource` that was passed to the constructor. `PrepareDragFile()`
returns the path of a PNG file of the shown picture, written by the drag source, or `null` when no picture is shown,
dragging out is off, the pane has no drag source, or the file could not be written. `ReportDragProblem` writes a
failure of the view's drag to the log.

**RenderCompletion**: a task that completes when every render started so far has applied or discarded its result,
so tests and callers can wait.

**RenderedImageBitmap**: `ConvertToPremultipliedBgra(rgba, width, height, destination, destinationRowBytes)` and
`Create(image)`. CanvasNet's pixels are red, green, blue, alpha with straight alpha; Avalonia's `WriteableBitmap`
composites blue, green, red, alpha with premultiplied alpha. Each color channel becomes
`(channel * alpha + 127) / 255` and the first and third channels swap places; rows are written at the framebuffer's
row size. `Create` makes a 96 dpi `WriteableBitmap` (`Bgra8888`, `AlphaFormat.Premul`) and fills it through
`Lock`.

**View**: `PreviewPaneView` has a check box (`PreviewFitToWindow`), a busy `ProgressBar` (`PreviewBusy`), a
`ScrollViewer` (`PreviewScroller`) that holds a `Viewbox` (`PreviewViewbox`), an error panel (`PreviewErrorPanel`,
`PreviewErrorTitle`, `PreviewErrorMessage`, `PreviewErrorHint`), a placeholder (`PreviewPlaceholder`), a status
line (`PreviewStatus`), and an `Expander` (`PreviewDiagnosticsExpander`) with the list of notes
(`PreviewDiagnostics`). The view creates the bitmap when the view model's picture changes, disposes the previous
one, and releases it when detached.

**Layout in picture pixels**: The `Viewbox` holds one `Grid` (`PreviewSurface`) that has the exact pixel size of the
shown picture, and the grid holds the `Image` (`PreviewImage`, never stretched, because the bitmap is 96 dpi and so
one device-independent pixel per picture pixel) and, over it, a `ContentControl` (`PreviewOverlay`) bound to
`Overlay`, which is visible only while the slot has content and stretches to the size of the grid. The overlay's own
coordinate system is therefore the picture's pixel coordinates, whatever the zoom is: the `Viewbox` scales the grid,
and with it the picture and the overlay, as one. With **FitToWindow** the `Viewbox` uses `Stretch.Uniform` and
`StretchDirection.DownOnly` and the scroll bars are disabled; otherwise it uses `Stretch.None` and the scroll bars are
automatic. Because the grid has a transparent background, pointer input anywhere on the picture reaches the surface
even where the overlay has no content of its own.

**Dragging the picture out**: The surface remembers a press of the left button when a picture is shown, dragging out is
on, and the pane has a drag source. When the pointer then moves four device-independent pixels along either axis with
the button still down, the view starts one drag (`StartDragAsync`): it asks `PrepareDragFile()` for the PNG, gets the
storage file for the path from the window's storage provider, loads the PNG as a bitmap, puts both into one
`DataTransferItem` (`SetFile` when the storage file exists, and `SetBitmap`) of a `DataTransfer`, and calls the
view's `DragStarter` with the press, the data, and `DragDropEffects.Copy`; the bitmap is disposed when the drag ends.
A release or a lost pointer capture cancels the pending drag, so a click starts nothing. `DragStarter` is an internal,
replaceable operation whose default is `DragDrop.DoDragDropAsync`, which never completes under the headless platform,
so that tests replace it and check what is offered. Nothing awaits the drag, so any exception from it is caught and
passed to `ReportDragProblem`, and the pane keeps working. See *ImageDragSource* for the file and the formats.

#### Key Methods

**Following the explorer**: A change of `SelectedItem` starts a render of `SelectedFile` in `FolderPath`. A change
of `FolderPath` (project opened, replaced, or closed) restarts the folder watcher, cancels any render, and clears
the pane; the selection change that follows decides what to render.

**StartRender**: Under a lock, disposes any pending delayed render, cancels the running render's token, increments
the generation counter, and creates a new token. With no selected file it clears the pane instead. It clears the
picture and error unless the render is a repeat for the same file, sets the busy state, and awaits the render
service.

**Stale results**: When a render finishes, the pane compares the render's generation with the current one under
the lock and discards the result when they differ, when the pane was disposed, or when a cancelled result arrives
for the current render. A render service that throws is shown as the error `operation-failed`.

**RequestRerender**: Callable from any thread. Disposes the pending delayed render and schedules a new one with the
scheduler after the debounce delay (400 milliseconds by default), so a burst of changes causes one render. When it
runs it re-renders with the current picture kept visible until the result arrives. A callback that was scheduled
for an earlier generation does nothing.

**Folder watcher**: A `FileSystemWatcher` over the project folder with sub-folders, watching file names,
directory names, write times, and sizes. `IsRelevantChange(projectFolder, changedPath)` ignores a path inside the
hidden `.canvaspilot` folder at the project root and a `.canvaspilot` project file at the root, and treats every
other path, including assets in sub-folders, as relevant. Watcher errors are logged and request a render. A folder
that cannot be watched is logged; the pane then renders only when the selection changes.

**Dispose**: Stops the watcher, discards the pending render, cancels the running render, and unsubscribes from the
explorer. It is safe to call twice. Results that arrive afterwards are ignored.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing collaborator. `ConvertToPremultipliedBgra` throws
`ArgumentOutOfRangeException` for a dimension below 1 or a row size below `width * 4`, and `ArgumentException` for
a source or destination that is too small. Render problems are shown, never thrown. Failures to write a log
entry are ignored.

#### Dependencies

- **ImageRenderService** (through `IImageRenderService`) — draws the files.
- **ProjectExplorer** — the selected file and project folder, and the `IRefreshScheduler` seam shared with it.
- **ProjectService** — the project file extension and data folder name constants.
- **ImageDragSource** — optional; writes the shown picture as a PNG file for dragging it out.
- **RollingFileLogger** — optional; records unexpected failures and watcher problems.
- **CommunityToolkit.Mvvm**, **Avalonia** — notification, source generators, controls, and bitmaps.

#### Callers

- The workspace — shows the view through the view locator and disposes the view model.
- `ImageExporter` — reads the shown picture through `IRenderedImageSource`.
- `ApplicationComposition` — creates the pane over the explorer, the refresh scheduler, and the drag source.

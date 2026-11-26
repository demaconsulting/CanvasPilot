### PreviewPane

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`PreviewPane` is the view model (`PreviewPaneViewModel`) and view (`PreviewPaneView`) that show the picture of the
file selected in the project explorer. It starts a render whenever the selection or project changes, cancels the
render that is still running, ignores results of renders that are no longer wanted, renders again after a delay
when files in the project change, and shows the picture on a stage, at a fitted size or at a zoom from 25 to 400
percent, with the busy state, the error with its code and hint, and the notes of the render. It also tells the facts of
the shown picture (file name, size, format, render time) to the part of the window that reports them; the pane view
itself holds no status line. The view model has no dependency on Avalonia's imaging; the view converts the pixels. The
pane also hosts one overlay slot over the picture, so that other units can place content on the picture in its own
pixel coordinates without the pane knowing them, and one slot that floats over the stage for the controls of that
content. It offers a menu of the picture on the stage that runs the copy and export commands its host gives.

#### Data Model

**Image** / **HasImage** / **CurrentImage**: the shown `RenderedImage` or `null`. `CurrentImageChanged` is raised after
it changes (`IRenderedImageSource`). **ProjectFolder** (`IRenderedImageSource`) is the folder of the open project that
the explorer holds, or `null` when no project is open, for the parts that draw the shown file again at another size.

**IRenderNoteSink**: the interface (`ShowNotes(group, notes)`) through which a part that checks the picture can put its
own notes in the list of notes of the picture; the code of every note of a group starts with the group name and a
hyphen. The pane implements it (see **ShowNotes** below). **AreDiagnosticsExpanded**: whether the list of notes is
open, bound both ways to the expander of the view; the pane opens it when notes are given.

**IsBusy** / **StatusText**: whether a render runs, and a one-sentence summary of the pane's state such as
`Rendering a.svg...`, `a.svg: 40 x 20 px, svg, 1 note`, or `a.svg: render failed`.

**FileName** / **ImageSizeText** / **FormatText** / **RenderTimeText** / **RenderMilliseconds**: the facts of the shown
picture, for the status bar of the window, which reads them from the pane. `FileName` is the name of the file that is
rendered or was last rendered (empty when none); `ImageSizeText` is `900 × 500 px`; `FormatText` is the format in
capital letters, such as `SVG`; `RenderTimeText` is `Rendered in 84 ms`. All but `FileName` are empty while no picture
is shown, also after a render failed. `RenderMilliseconds` (`long?`) is measured with a stopwatch around the call of the
render service and is set when the result is applied.

**Error**, **HasError**, **ErrorTitle** (`Render failed (code)`), **ErrorMessage**, **ErrorHint** (`Hint: ...` or
empty) and **HasErrorHint**: the failed render.

**Diagnostics**, **HasDiagnostics**, **DiagnosticsHeader** (`1 note`, `3 notes`): the notes of the shown picture.

**ShowPlaceholder** / **PlaceholderText**: true while there is no picture, error, or running render; the text says that
no project is open (`No project is open.`) or that a file is to be selected (`Select an image file to preview it.`).

**FitToWindow**: `bool`, true by default; scale the picture down to the pane (never up), otherwise show it at
**ZoomPercent**.

**Zoom model**: `ZoomPercent` (`int`, 100 by default) is the zoom that applies while Fit is off and is also the zoom
last chosen, so that turning Fit off returns to it. `FitPercent` (`int`, 100 by default) is the zoom at which the fitted
picture is shown; only the view knows the size of the pane, so the view reports it (from 1 to 100, because a fitted
picture is never enlarged). `EffectiveZoomPercent` is `FitPercent` while fitted and `ZoomPercent` otherwise; `ZoomLabel`
is that value followed by `%`; `IsActualSize` is true while Fit is off and `ZoomPercent` is 100; `IsZoomInPossible` and
`IsZoomOutPossible` are false at 400 and at 25. `ZoomSteps` is the list 25, 50, 75, 100, 150, 200, 300, 400 and
`ActualSizePercent` is 100. The commands are `ZoomIn`, `ZoomOut`, `ZoomActualSize`, and `ZoomToFit` (each with a
generated `...Command`).

**Overlay** / **HasOverlay**: `object?`, a slot for the content that is shown exactly over the picture; `null` by
default. The pane does not interpret it: the view shows it in a `ContentControl`, so the application's view locator
picks the view of whatever view model the host assigns, and the pane never refers to the code that fills the slot.
The slot belongs to the pane, not to a picture, so it keeps its content while pictures change, are rendered again, or
fail to draw; the host decides what the content shows then.

**Toolbar** / **HasToolbar**: `object?`, a slot that floats over the top center of the stage, for the host's controls
that belong to the overlay, such as the tools for marking the picture; `null` by default. It is a `ContentControl`
(`PreviewToolbar`) with a margin of 12 that is visible only while the slot has content. It is outside the scaled
picture, so its content keeps its own size at every zoom. Like `Overlay`, the pane does not interpret it.

**IsDragOutEnabled** / **DragSource** / **PrepareDragFile()** / **ReportDragProblem(exception)**: `IsDragOutEnabled` is
a `bool`, true by default, that the host turns off while the pointer is used on the picture for something else, such as
drawing a mark. `DragSource` is the optional `ImageDragSource` that was passed to the constructor. `PrepareDragFile()`
returns the path of a PNG file of the shown picture, written by the drag source, or `null` when no picture is shown,
dragging out is off, the pane has no drag source, or the file could not be written. `ReportDragProblem` writes a
failure of the view's drag to the log.

**CopyImageCommand** / **ExportPngCommand** / **IsPictureMenuEnabled**: `CopyImageCommand` and `ExportPngCommand` are
`ICommand?` slots, `null` by default, for the host's commands that copy the shown picture and export it as a PNG file;
the menu of the picture runs them (see *Menu of the picture*). Like `Overlay`, the pane does not interpret them, so the
menu runs exactly the host's commands, with their rule of availability, and the pane does not depend on the host.
`IsPictureMenuEnabled` is a `bool`, true by default, that the host turns off while the pointer is used on the picture
for something else, such as drawing a mark; the menu does not open while it is off. Each raises its change.

**RenderCompletion**: a task that completes when every render started so far has applied or discarded its result,
so tests and callers can wait.

**RenderedImageBitmap**: `ConvertToPremultipliedBgra(rgba, width, height, destination, destinationRowBytes)` and
`Create(image)`. CanvasNet's pixels are red, green, blue, alpha with straight alpha; Avalonia's `WriteableBitmap`
composites blue, green, red, alpha with premultiplied alpha. Each color channel becomes
`(channel * alpha + 127) / 255` and the first and third channels swap places; rows are written at the framebuffer's
row size. `Create` makes a 96 dpi `WriteableBitmap` (`Bgra8888`, `AlphaFormat.Premul`) and fills it through
`Lock`.

**View**: `PreviewPaneView` is one `Grid` (`PreviewStage`, clipped, background `CpSurfaceStage`, with the menu of the
picture `PictureMenu` as its `ContextMenu`) whose layers, from back to front, are:

1. A dot grid: a non-hit-testable `Border` at opacity 0.2 filled with a tiled drawing of one dot of 2 pixels every 20
   pixels in `CpTextTertiary`.
2. The picture: a `ScrollViewer` (`PreviewScroller`, focusable, visible while a picture is shown, scroll bars off or
   automatic as below) that holds a `Viewbox` (`PreviewViewbox`) with a margin of 32 and 64, centered, around the
   picture surface (see *Layout in picture pixels*). A `Border` with the `CpShadowPicture` shadow lies under the `Image`
   (`PreviewImage`) on that surface.
3. The error card (`ErrorCard`): a raised, centered card (at most 560 wide, a `CpDanger` outline, an error icon in
   `CpDanger`) that holds the error panel (`PreviewErrorPanel`) with `PreviewErrorTitle`, `PreviewErrorMessage`, and
   `PreviewErrorHint`, visible while `HasError` holds.
4. The placeholder (`PreviewPlaceholder`), centered, in `CpFontSubtitle` and `CpTextSecondary`.
5. The busy line (`PreviewBusy`): an indeterminate `ProgressBar` 2 pixels high along the top of the stage, with
   a transparent track and the `CpAccentGradient` brush, named "Rendering" for screen readers, visible while `IsBusy`.
6. The slot of the host's controls (`PreviewToolbar`), top center, margin 12.
7. The zoom pill (`PreviewZoomPill`): a rounded pill 36 high at the bottom right (margin 16), visible while a picture is
   shown, holding in order a Fit `ToggleButton` (`PreviewFitToWindow`, two-way bound to `FitToWindow`; while checked it
   has the accent tint as its fill, a one pixel outline and the icon in the accent text color, which keep showing while
   the pointer is over it after the click, because the tint alone is faint on the raised pill), the actual size button
   `1:1` (`PreviewZoomActual`), zoom out (`PreviewZoomOut`), the zoom label (`PreviewZoomLabel`, a `Button` that runs
   `ZoomToFit`), and zoom in (`PreviewZoomIn`). Zoom out and zoom in are enabled by `IsZoomOutPossible` and
   `IsZoomInPossible`. Every button has an `AutomationProperties.Name` and a tooltip with its key
   (`Fit to window  Ctrl+0`, `Actual size  Ctrl+1`, `Zoom out  Ctrl+Minus`, `Zoom in  Ctrl+Plus`, and
   `Click to fit to window` for the label).
8. The notes drawer (`PreviewDiagnosticsExpander`): an `Expander` that opens upward, at the bottom left (margin 16 at
   the left, the right, and the top, and 60 at the bottom, which is the 16 pixel margin and the 36 pixel height of the
   zoom pill and 8 pixels between them; at most 480 wide), whose header is a warning icon and the `DiagnosticsHeader`,
   and whose body is a scrolling list (`PreviewDiagnostics`, at most 160 high). It is two-way bound to
   `AreDiagnosticsExpanded` and visible while `HasDiagnostics` holds. Because the drawer sits above the zoom pill, the
   two never overlap, also on a narrow stage.

The view creates the bitmap when the view model's picture changes, disposes the previous one, and releases it when
detached. The `UserControl` has key bindings for `Ctrl+0` (`ZoomToFit`), `Ctrl+1` (`ZoomActualSize`), `Ctrl+Plus` and
`Ctrl+Add` (`ZoomIn`), and `Ctrl+Minus` and `Ctrl+Subtract` (`ZoomOut`), which work while the pane has the keyboard
focus.

**Layout in picture pixels**: The `Viewbox` holds one `Grid` (`PreviewSurface`) that has the exact pixel size of the
shown picture, and the grid holds the picture's shadow, the `Image` (`PreviewImage`, never stretched, because the bitmap
is 96 dpi and so one device-independent pixel per picture pixel) and, over it, a `ContentControl` (`PreviewOverlay`)
bound to `Overlay`, which is visible only while the slot has content and stretches to the size of the grid. The
overlay's own coordinate system is therefore the picture's pixel coordinates, whatever the zoom is: the `Viewbox` scales
the grid, and with it the picture and the overlay, as one. Because the grid has a transparent background, pointer input
anywhere on the picture reaches the surface even where the overlay has no content of its own.

**Zoom in the view** (`ApplyFit`, run when the picture, `FitToWindow`, or `ZoomPercent` changes): while fitted, the
`Viewbox` has no fixed size, uses `Stretch.Uniform` and `StretchDirection.DownOnly`, and the scroll bars are disabled.
With Fit off and a zoom of 100 percent, it uses `Stretch.None`. At any other zoom the view gives the `Viewbox` the size
of the zoomed picture (picture size times `ZoomPercent` over 100, in each direction) and `Stretch.Uniform` with
`StretchDirection.Both`, so the zoom is fixed by the size of the `Viewbox` and not by the size of the pane. Whenever Fit
is off the scroll bars are automatic. `ReportFitPercent` runs while fitted and whenever the size of the `Viewbox`
changes: it divides the width of the `Viewbox` by the picture's width, rounds, clamps to 1 to 100, and sets
`FitPercent`.

**Pointer and keys on the stage**: A handler in the tunnel phase on the scroller (a) with `Control` held, turns the
mouse wheel into `ZoomIn` (up) or `ZoomOut` (down) and handles the event, so the wheel scrolls otherwise; (b) on a press
outside the picture surface, focuses the scroller, so that the zoom keys work after a click on the empty stage, while a
press on the picture is left to the picture's own tools and does not scroll; (c) on a press of the middle button,
remembers the pointer position and scroll offset, captures the pointer, and moves the scroll offset (never below zero)
opposite to the pointer's movement until the button is released. Space-drag panning, a checkerboard behind transparent
pixels, and remembering the zoom per picture do not exist.

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

**Menu of the picture**: The stage's `ContextMenu` (`PictureMenu`, named "Picture" for screen readers) holds two
items, in this order: `PictureMenuCopyImage` (header `_Copy image`, named "Copy image") bound to `CopyImageCommand`, and
`PictureMenuExportPng` (header `_Export as PNG...`, named "Export as PNG") bound to `ExportPngCommand`. A `MenuItem`
whose command is `null` is usable in Avalonia, so each item's `IsEnabled` is also bound to whether its command slot is
filled; with a command, the framework shows the item unusable while the command cannot run. Because the menu belongs to
the whole stage, it opens on a right-click of the picture, the overlay on it, the empty stage around it, the error card,
or the placeholder, so after a failed render the user sees its items unusable, as in the host's menu. The ways to open
it:

- A right-click: the framework raises `ContextRequested` from the control under the pointer when the right button is
  released, and the request bubbles to the stage; the menu opens at the pointer.
- The Menu key: the framework raises the request from the focused control on the key's release; with the scroller
  (`PreviewScroller`, the stage's focus target after Tab or a click on the empty stage) or a control inside it, such as
  the layer for marks, focused, it bubbles to the stage.
- Shift+F10: the framework counts it as a menu key only on Windows, where it raises the request from the focused control
  on the release of F10 while Shift is still held, as for the Menu key; on other platforms, and on the headless platform
  of the tests, it does not. So a `KeyDown` handler on the scroller remembers a press of F10 with exactly Shift held,
  together with the control inside the scroller that has the focus (the scroller itself, or a control in it such as the
  layer for marks, which keeps the focus after its tool is put away), and marks it handled. The `KeyUp` handler raises a
  `ContextRequestedEventArgs` from that control when its F10 is released, so the request bubbles as the Menu key's does:
  a control with a menu of its own opens that menu, and otherwise the stage opens the menu of the picture, through the
  same check in `Opening`. The handler does nothing when the platform's gestures for a context menu match the release,
  because the framework has raised the request then, so one press never asks twice; on Windows the handler is what
  opens the menu when Shift is released before F10. Any other key pressed in the scroller, a release on another
  control, or the focus leaving a control in the scroller forgets the remembered press.

The view handles the menu's `Opening` event and cancels it while `IsPictureMenuEnabled` is off or no view model is
shown, which covers the pointer and the keys alike. Canceling the opening, instead of marking every request on the
stage handled, keeps the context menus of controls on the stage, such as the text box of a note, working while a tool is
chosen. The slot of the host's controls (`PreviewToolbar`), the zoom pill, and the notes drawer each mark a
`ContextRequested` that bubbles through them handled, so a right-click or the Menu key on them never opens the menu of
the picture; the handler runs as the request bubbles, after their children, so a child with a menu of its own still
opens it.

**Automation identifiers that changed**: `PreviewFitToWindow` is now a `ToggleButton` (it was a `CheckBox`) named "Fit
to window"; new are `PreviewStage`, `PreviewZoomPill`, `PreviewZoomActual`, `PreviewZoomOut`, `PreviewZoomLabel`, and
`PreviewZoomIn`; `PreviewStatus` is gone from the pane view (the status bar of the window holds it); `PreviewBusy` is
the 2 pixel line, `PreviewErrorPanel` is the panel inside the error card, and `PreviewDiagnosticsExpander` is the
drawer.

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

**ShowNotes**: Implements `IRenderNoteSink`. Given a group name and notes, and only while a picture is shown, it removes
from the list of notes, in place, every note whose code starts with the group name and a hyphen, adds the new notes at
the end, and opens the list when there are new notes. The notes of the renderer and of other groups keep their place. A
blank group or a missing list is refused with an exception. The list of notes is replaced as a whole by the result of
the next render, so these notes are gone as soon as the picture is drawn again; the caller applies them on the thread
that owns the user interface.

**Zoom commands**: `ZoomIn` takes the next step of `ZoomSteps` that is above `EffectiveZoomPercent`, and `ZoomOut` the
next step below it, so from a fitted picture shown at 90 percent they give 100 and 75; at the end of the list they do
nothing. Both, and `ZoomActualSize`, set `ZoomPercent` (kept within 25 to 400) and then turn `FitToWindow` off, so the
chosen zoom stays in `ZoomPercent` when Fit is turned on again and off again. `ZoomToFit` turns `FitToWindow` on and
leaves `ZoomPercent` alone. Setting any of `FitToWindow`, `ZoomPercent`, and `FitPercent` raises the change of the
dependent label and limit properties.

**Dispose**: Stops the watcher, discards the pending render, cancels the running render, and unsubscribes from the
explorer. It is safe to call twice. Results that arrive afterwards are ignored.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing collaborator. `ConvertToPremultipliedBgra` throws
`ArgumentOutOfRangeException` for a dimension below 1 or a row size below `width * 4`, and `ArgumentException` for
a source or destination that is too small. Render problems are shown, never thrown. Failures to write a log
entry are ignored.

**Known limitations**: The busy line and the spinner do not stop when the system asks for reduced motion. The sentence
of the marking tools (`StatusText` of the annotation overlay) is a tooltip and a hidden live text of the overlay; it is
not part of the live region of the status bar. Transparent pictures are drawn on the plain stage color, without a
checkerboard, and the picture cannot be panned by dragging with the Space key held. A menu of the picture that is opened
from the keyboard is placed by the framework at the bottom of the stage's scroller, not at the picture. The menu cannot
be opened from the keyboard while no picture is shown, because the scroller, the stage's focus target, is hidden then; a
right-click still opens it.

#### Dependencies

- **ImageRenderService** (through `IImageRenderService`) — draws the files.
- **ProjectExplorer** — the selected file and project folder, and the `IRefreshScheduler` seam shared with it.
- **ProjectService** — the project file extension and data folder name constants.
- **ImageDragSource** — optional; writes the shown picture as a PNG file for dragging it out.
- **RollingFileLogger** — optional; records unexpected failures and watcher problems.
- **CommunityToolkit.Mvvm**, **Avalonia** — notification, source generators, controls, and bitmaps.

#### Callers

- The workspace — shows the view through the view locator and disposes the view model.
- The status bar of the main window (`AppShell`) — reads `HasImage`, `IsBusy`, `HasError`, `StatusText`, `FileName`,
  `ImageSizeText`, `FormatText`, `RenderTimeText`, and the notes' header, and follows the pane's changes.
- `ImageExporter` — reads the shown picture through `IRenderedImageSource`.
- The check of the picture against the style (`StyleCheckCommand`, of the `Style` subsystem) — reads the shown picture
  through `IRenderedImageSource` and gives its notes through `IRenderNoteSink`.
- `ApplicationComposition` — creates the pane over the explorer, the refresh scheduler, and the drag source, puts the
  marking layer in `Overlay` and its tool bar in `Toolbar`, puts the shell's Copy Image and Export as PNG commands in
  `CopyImageCommand` and `ExportPngCommand`, and turns `IsDragOutEnabled` and `IsPictureMenuEnabled` off while a marking
  tool is chosen.
- `AnnotationToolbarView` — finds the pane view among its ancestors to take the tool keys from it.

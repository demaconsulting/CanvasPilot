### ProjectExplorer

![Project Structure](ProjectSubsystemView.svg)

#### Purpose

`ProjectExplorer` is the view model, tree node, and view that show the open project as a tree: the folders of the
project, and in them the image source files and the assets. It keeps the tree current by watching the project folder and
its sub-folders, remembers which folders are expanded and which node is selected, creates files and folders, copies
files that are added or dropped into the project, renames and deletes the selected file, and shows a small picture of
each drawing. The hidden `.canvaspilot` data folder, the project file, and hidden files are never listed.

#### Data Model

**Tree**: `Roots` is a `ReadOnlyObservableCollection<ProjectNodeViewModel>` of the nodes at the top: the folders of the
project folder first, then its files, each group sorted by name ignoring case. `ProjectNodeViewModel` is one folder or
one file, with `Name`, `FullPath`, `IsFolder`, `Kind`, `Format`, a flag that says whether CanvasNet draws the file,
`Children` (folders first, then files, sorted in the same way), `IsExpanded` (settable, observable), and `BadgeKey`.
`Kind` (`ProjectNodeKind`) is `Folder`, `Svg`, `Mermaid`, `VegaLite`, `Source` (another source that CanvasNet draws),
`Pdf`, `Bitmap`, or `Style`, told from the file name by `KindOf`, so `style.json` is `Style`, `.vl.json`, `.vl`, and
`.vega-lite.json` are `VegaLite`, and `.mmd` and `.mermaid` are `Mermaid`. `Format` is the extension in capital letters
(`VL.JSON` for the compound extension) and empty for a folder. `BadgeKey` is the key the view uses to draw the badge:
`folder`, `svg`, `mermaid`, `vega`, `pdf`, `image`, `style`, or `source`. A file is listed when the image source filter
accepts its name or when it is an asset (`Bitmap`, `Pdf`, or `Style`, told by `IsListedAsset`); the flag for a drawn
file is true only for a file that the filter accepts, that is, a file that CanvasNet draws.

**Flat list and selection**: `Items` is a `ReadOnlyObservableCollection<ProjectFileItem>` of the drawn files
anywhere in the tree, in the order of the tree; `ProjectFileItem` is a `record` of `Name` and `FullPath`. `SelectedItem`
and `SelectedFile` (its full path or `null`) are the selected drawn file. `SelectedNode` is the selected node of
the tree, whatever it is. The two selections are kept in step: setting `SelectedNode` to a drawn file selects the
item, and setting it to a folder or to an asset that is not drawn makes `SelectedItem` `null`; setting `SelectedItem`
selects the node with the same path. `FindNode(path)` finds the node of a path (ignoring case) or returns `null`.

**State**: `FolderPath` and `HasProject` (the project folder shown, or `null`); `EmptyMessage` ("No project is open." or
"This project has no image files yet." when the tree is empty, otherwise empty); `ErrorMessage` (the last problem, shown
in the view); `IsWatching`; `IsTruncated` (whether the tree was cut short); `ShowThumbnails` (default `true`);
`ComfortableRows` (default `false`: rows 26 px high; `true`: 32 px); `TargetFolder` (where a new file or folder goes:
the selected folder, the folder of the selected file, or the project folder, or `null` with no project);
`NewFileCommand` and `NewFolderCommand` (set by the application shell, which asks the user for the name; they are `null`
until then); and `RefreshCommand` (an `IRelayCommand` that can be used while `HasProject`). The constants are
`MaximumNodes` (5000), `MaximumDepth` (12), `AttachmentsFolderName` (`attachments`), and `DefaultDebounce` (250
milliseconds).

**Expansion memory**: a set of the full paths of the folders that the user expanded in this session. A node that is made
for a folder in the set starts expanded, and expanding or collapsing a folder adds or removes its path. The set is
cleared when another project is opened or the explorer is closed, and kept when the same project is opened again. It is
not saved to the settings.

**Thumbnails**: each file node has `Thumbnail` (a `Bitmap` the node owns, or `null`) and `HasThumbnail`, an internal
"requested" flag, and the size and time of the file at the last refresh (`UpdateStamp`). Nodes are `IDisposable`:
disposing a node frees its bitmap and those of its children.

**IImageSourceFilter**: `IsImageSource(fileName)` decides which files are drawn. `CanvasNetImageSourceFilter` is the
application's implementation: a file is an image source when its name ends with one of `CanvasNetViewer.Extensions`
(lower case, with the dot, including compound extensions such as `.vl.json`), ignoring case, and has a name before the
extension.

**IRefreshScheduler**: `Schedule(delay, callback)` returns an object whose disposal cancels the callback.
`DispatcherRefreshScheduler` waits on the thread pool and then posts the callback to the Avalonia user interface thread.

**IThumbnailSource**: the seam through which the explorer asks for thumbnails; the Project subsystem owns the interface
and the Preview subsystem implements it (`ThumbnailProvider`, see its design).
`CreateAsync(filePath, projectFolder, cancellationToken)` returns a task with a new bitmap that the caller owns and
disposes, made off the user interface thread from files inside the project folder, or `null` when the file cannot be
drawn. A second overload with a width and a height makes a larger picture, as the welcome screen shows; by default it
gives the same bitmap as the first. The explorer is given the source in its constructor (optional) and never creates it.

**View**: `ProjectExplorerView` is an Avalonia control in four rows:

- The header, 40 px high: the label `EXPLORER` (caption size, bold, letter-spaced, secondary text color) and three icon
  buttons of the `IconButton` theme, New file, New folder, and Refresh, bound to `NewFileCommand`, `NewFolderCommand`,
  and `RefreshCommand`. A button whose command is `null` or cannot run is disabled by the binding. Each button has an
  automation name and a tooltip with the same text.
- The tree: a `TreeView` named "Project files" bound to `Roots` and `SelectedNode`, whose items use the
  `ExplorerTreeViewItem` control theme of `ThemeResources`: rows 26 px high, a chevron on a folder, 16 px of indent for
  each level, the hover tint, the selected tint with a 3 by 14 px accent bar and a semibold name, and the 2 px focus
  ring on keyboard focus. The content of a row is a badge (a 16 px tile of the color of the kind with a white glyph, or
  an amber outline folder, open or closed) in front of the name, with the full path as tooltip, and, at the right, the
  28 by 18 px thumbnail box that shows only while the node has a thumbnail. The arrow keys, Right, and Left are the
  keyboard handling of the `TreeView`: they move the selection and expand and collapse folders. The tree carries the
  class `comfortable` while `ComfortableRows` is on, and a style of the view sets the `MinHeight` of its rows to 32 px,
  over the 26 px of the control theme.
- The empty message over the tree and the error line below it: a raised card with the danger icon and the message in the
  danger color, shown while `ErrorMessage` is not empty.
- The drop zone: a dashed outline with an attach icon and the words "Drop pictures or PDFs here", shown while a project
  is open.

Controls carry automation identifiers: `ProjectExplorerList` (the tree), `ProjectExplorerEmptyMessage`,
`ProjectExplorerError`, `ExplorerNewFileButton`, `ExplorerNewFolderButton`, `ExplorerRefreshButton`, `ExplorerDropZone`,
and `ExplorerThumbnail`. The tree is a `TreeView` whose items are `TreeViewItem` of `ProjectNodeViewModel`, no longer a
flat `ListBox` of files, but it keeps the identifier `ProjectExplorerList`; the error text sits in a card that hides
with it, so a test that checks whether the error shows asks whether the text is effectively visible.

#### Key Methods

**Open**: Shows a project folder.

- *Parameters*: `string projectFolder`.
- *Postconditions*: Any earlier watcher is stopped, the expansion memory is cleared unless the folder is the one shown
  before, the tree holds the folder's content, and the folder and its sub-folders are watched.

**Close**: Stops watching, cancels a pending refresh, and clears the tree, the list, the selection, the expansion
memory, `IsTruncated`, and the error. Removed nodes are disposed.

**Refresh**: Reads the project folder again and brings the tree in line with it by `SyncFolder`, which for one folder
reads its entries, removes the nodes that are gone (disposing them), inserts the new ones and moves the ones that
changed place, and so keeps the nodes that remain, which keeps the selection, the expansion, and the scroll position,
then does the same for each folder below. Entries are folders (not hidden, not links, not starting with a period, so
never the data folder) and files (not hidden, not starting with a period, not with the project file extension, and
accepted by the filter or an asset), folders first, each group sorted by name ignoring case. Reading stops at
`MaximumNodes` entries in all, and no folder deeper than `MaximumDepth` is read; either sets `IsTruncated`. An
`IOException` or `UnauthorizedAccessException` for the project folder is logged and shown in `ErrorMessage` with an
empty tree; the same for a folder below it is logged and leaves only that folder empty. A file node learns the size and
time of its file; when they change, its thumbnail is dropped and may be asked for again. Afterwards `Items` is brought
in line with the drawn files in tree order, a selected file or node that is gone is cleared, and thumbnails are
requested. Must run on the thread that owns the tree.

**RequestRefresh**: Callable from any thread. Cancels a refresh that is waiting and schedules a new one after the
debounce delay, so a burst of changes causes one refresh. Ignored when no project is open or the view model is disposed.
A scheduled refresh carries the generation of the project it was requested for and is ignored when the project has since
changed or closed.

**SelectFile**: Selects the listed file with a path (ignoring case) and expands the folders above it; returns whether it
was found and clears the selection otherwise.

**DeleteSelected**: Deletes the selected drawn file with `File.Delete` wherever it lies in the project, logs it,
and refreshes. Returns an empty string or a user-readable problem, which is also shown in `ErrorMessage`. The caller
asks the user to confirm first. Only drawings can be deleted: with a folder or an asset selected there is no selected
file, so the problem "Select a file in the project first." is returned. Folders cannot be renamed or deleted.

**RenameSelected**: Renames the selected drawn file within the folder that holds it. The new name is trimmed and
must pass the project name rules (shared with `ProjectService`), must still be accepted by the image source filter, and
must not already exist as a file or folder in that folder (a change of letter case only is allowed); the move never
overwrites. On success the tree is refreshed and the renamed file is selected. Returns an empty string or a problem.

**CreateFile**: Creates a file in `TargetFolder`. The name is trimmed and must pass the project name rules, be accepted
by the filter, stay in the folder (`PathHelpers.SafePathCombine`), and not exist as a file or folder. The file is
written with a blank drawing of its kind (`EmptyDrawingFor`): an SVG of 800 by 450 with no shapes, a Mermaid flowchart
with two boxes, a Vega-Lite chart with no data, and an empty file for any other kind. Then the tree is refreshed and the
file is selected and shown. Returns an empty string or a problem; with no project open the problem is "Open a project
first."

**CreateFolder**: Creates a folder in `TargetFolder`. The name must pass the project name rules (which refuse names that
start with a period, so a hidden folder cannot be made) and not exist. Then the tree is refreshed and the folder is
selected with the folders above it expanded.

**AddFiles**: Copies files into the project.

- *Parameters*: `IEnumerable<string> sourcePaths`; `string? targetFolder` (`null` for the `attachments` folder).
- *Algorithm*: the destination must be the project folder or a folder inside it, and not the data folder or inside it,
  otherwise "Files can only be added to a folder of the project." is returned and nothing is copied. For each source: a
  folder is refused ("is a folder; only files can be added"), a missing file is reported, a name that the filter and the
  asset rule both reject is refused ("was not added: only pictures, PDF files, and drawings can be added"), and any
  other file is copied with `File.Copy` without overwriting into the destination, created when needed, under the name
  `PathHelpers.UniqueFileName` gives. The source is never changed. The tree is refreshed afterwards and the last copy is
  selected with the folders above it expanded.
- *Returns*: an empty string when every file was added, otherwise the problems joined, which are also shown in
  `ErrorMessage`.

**Drop handling** (`ProjectExplorerView`): the view accepts drops of files. While a drag over the view carries files and
the project is open, `FindTarget` walks up from the element under the pointer to find the drop target: the drop zone
(copy into `attachments`, the zone is lit by the class `drag-active`), the row of a folder (copy into that folder, the
row is lit by the class that the control theme draws as a dashed ring), or the row of a file (copy into
the folder of the file, no highlight). Any other place, a drag that is not of files, or a view with no project shows no
highlight and gives the effect `None`. On the drop the highlight is cleared and `AddFiles` is called with the local
paths of the files.

**Thumbnails**: `RequestThumbnails` runs after a refresh, when a folder is expanded, and when `ShowThumbnails` is turned
on. While `ShowThumbnails` is on and a source is set, it walks the nodes that can be seen (those at the top and those
below folders that are all expanded) and, for each drawn file that has no thumbnail and has not been asked for,
marks it requested and starts `LoadThumbnailAsync`, which awaits `IThumbnailSource.CreateAsync` off the user interface
thread and gives the bitmap to the node, unless the node was disposed, the setting was turned off, another project
was opened meanwhile, or the explorer was disposed, when the bitmap is disposed. The calls of the source carry a token
that is cancelled when the explorer is disposed. A failure of the source (an I/O problem, a cancellation, or an
invalid operation) is logged and leaves the node without a picture, unless the thumbnail was no longer wanted by then,
because the log may be gone. Turning `ShowThumbnails` off removes and disposes
every thumbnail and clears the requested flags. Folders and assets are never asked for.

**Folder watcher**: A `FileSystemWatcher` that includes sub-folders, watching file names, directory names, write times,
and sizes. A change inside the `.canvaspilot` data folder, or of that folder itself, is ignored. A folder that cannot be
watched is logged; the tree still works but does not refresh by itself. The watcher is stopped and disposed on `Close`,
on `Open` of another folder, and on `Dispose`.

#### Error Handling

The constructor throws `ArgumentNullException` for a null filter or scheduler and `ArgumentOutOfRangeException` for a
maximum below 1, and `Open` throws `ArgumentException` for a blank folder and `ObjectDisposedException` after disposal.
File problems while reading, deleting, renaming, creating, and copying are returned as messages, shown in
`ErrorMessage`, and logged, never thrown. A failing thumbnail source is logged and leaves the row without a picture.
Failures to write a log entry are ignored.

**Known limitations**: The expansion memory lives only as long as the explorer. The expanded folders are not saved per
project, so a project opens with its folders closed after a restart.

#### Dependencies

- **ProjectService** — the project file extension constant, the data folder name, and the file name rules.
- **PathHelpers** — resolves the renamed and created file paths safely and gives free names for copies.
- **RollingFileLogger** — optional; records read, delete, rename, create, copy, thumbnail, and watcher problems.
- **DemaConsulting.CanvasNet.Viewer** (OTS) — only `CanvasNetViewer.Extensions`, through `CanvasNetImageSourceFilter`;
  see the CanvasNet OTS design. Drawing files is done by `ImageRenderService` of the `Preview` subsystem, not by this
  unit.
- **IThumbnailSource** — implemented by `ThumbnailProvider` of the `Preview` subsystem (see its design); the explorer
  only calls the interface.
- **ThemeResources** — the `ExplorerTreeViewItem`, `IconButton` control themes, the `CpBadge` colors, and the design
  tokens that the view uses; see its design.
- **CommunityToolkit.Mvvm**, **Avalonia** — notification, source generators, controls, bitmaps, drag and drop, and the
  user interface thread dispatcher.

#### Callers

- The application shell (`ProjectWorkspace`) — hosts the view, calls `Open` and `Close` as the open project changes,
  sets `NewFileCommand` and `NewFolderCommand` (which ask the user for a name and call `CreateFile` and `CreateFolder`),
  reads `SelectedFile`, and calls `DeleteSelected` and `RenameSelected` after confirming with the user.
- `MainWindowShell` and `ApplicationComposition` — give the explorer its thumbnail source and set `ShowThumbnails` and
  `ComfortableRows` from the Appearance setting at the start and (the shell) on every change of the settings.

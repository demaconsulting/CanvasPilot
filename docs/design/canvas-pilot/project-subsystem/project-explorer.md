### ProjectExplorer

![Project Structure](ProjectSubsystemView.svg)

#### Purpose

`ProjectExplorer` is the view model and list view that show the image source files in the root of the open
project's folder. It keeps the list current by watching the folder, remembers the selected file, and deletes
or renames the selected file. It lists no sub-folders, nothing inside the hidden `.canvaspilot` data folder,
no project file, and no hidden files.

#### Data Model

**Items**: `ReadOnlyObservableCollection<ProjectFileItem>` — the listed files sorted by name ignoring case;
`ProjectFileItem` is a `record` of `Name` and `FullPath`.

**SelectedItem** / **SelectedFile**: the selected item, and its full path or `null`.

**FolderPath** / **HasProject**: the project folder shown, or `null`.

**EmptyMessage**: `string` — "No project is open." or "This project has no image files yet." when the list
is empty, otherwise empty. **ErrorMessage**: `string` — the last problem, shown in the view.

**IsWatching**: `bool` — whether a folder watcher is running.

**IImageSourceFilter**: `IsImageSource(fileName)` decides which files are listed. `CanvasNetImageSourceFilter`
is the application's implementation: a file is an image source when its name ends with one of
`CanvasNetViewer.Extensions` (lower case, with the dot, including compound extensions such as `.vl.json`),
ignoring case, and has a name before the extension.

**IRefreshScheduler**: `Schedule(delay, callback)` returns an object whose disposal cancels the callback.
`DispatcherRefreshScheduler` waits on the thread pool and then posts the callback to the Avalonia user
interface thread. The default debounce delay is 250 milliseconds.

**View**: `ProjectExplorerView` is an Avalonia control with a `ListBox` of file names bound to `Items` and
`SelectedItem`, an empty-state message over it, and an error line. Controls carry automation identifiers
(`ProjectExplorerList`, `ProjectExplorerEmptyMessage`, `ProjectExplorerError`).

#### Key Methods

**Open**: Shows a project folder.

- *Parameters*: `string projectFolder`.
- *Postconditions*: Any earlier watcher is stopped, the list holds the folder's image source files, and the
  folder is watched.

**Close**: Stops watching, cancels a pending refresh, and clears the list, the selection, and the error.

**Refresh**: Reads the folder's root files — skipping names that start with a period, files with the hidden
attribute, files with the project file extension, and files the filter rejects — and updates the list with the
fewest changes (removing files that are gone, inserting new files at their sorted position) so the selection and
scroll position of remaining files are kept. The selection is cleared if its file is no longer listed. An
`IOException` or `UnauthorizedAccessException` is logged and shown in `ErrorMessage` with an empty list;
a successful read clears the error. Must run on the thread that owns the list.

**RequestRefresh**: Callable from any thread. Cancels a refresh that is waiting and schedules a new one after
the debounce delay, so a burst of changes causes one refresh. Ignored when no project is open or the view
model is disposed. The folder watcher calls it for created, deleted, changed, and renamed files and for watcher
errors. A scheduled refresh carries the generation of the project it was requested for and is ignored when the
project has since changed or closed.

**SelectFile**: Selects the listed file with a path (ignoring case); returns whether it was found and clears
the selection otherwise.

**DeleteSelected**: Deletes the selected file with `File.Delete`, logs it, and refreshes. Returns an empty string
or a user-readable problem, which is also shown in `ErrorMessage`. The caller asks the user to confirm first.

**RenameSelected**: Renames the selected file within the project folder. The new name is trimmed and must pass
the project name rules (shared with `ProjectService`), must still be accepted by the image source filter, and
must not already exist as a file or folder (a change of letter case only is allowed); the move never overwrites.
On success the list is refreshed and the renamed file is selected. Returns an empty string or a problem.

**Folder watcher**: A `FileSystemWatcher` without sub-folders, watching file names, directory names, write
times, and sizes. A folder that cannot be watched is logged; the list still works but does not refresh by
itself. The watcher is stopped and disposed on `Close`, on `Open` of another folder, and on `Dispose`.

#### Error Handling

The constructor throws `ArgumentNullException` for a null filter or scheduler, and `Open` throws
`ArgumentException` for a blank folder and `ObjectDisposedException` after disposal. File problems while
listing, deleting, and renaming are returned as messages and logged, never thrown. Failures to write a log
entry are ignored.

#### Dependencies

- **ProjectService** — the project file extension constant and the file name rules.
- **PathHelpers** — resolves the renamed file path.
- **RollingFileLogger** — optional; records read, delete, rename, and watcher problems.
- **DemaConsulting.CanvasNet.Viewer** — only `CanvasNetViewer.Extensions`, through
  `CanvasNetImageSourceFilter`. Rendering is not part of this unit.
- **CommunityToolkit.Mvvm**, **Avalonia** — notification, source generators, controls, and the user interface
  thread dispatcher.

#### Callers

- The application shell — hosts the view, calls `Open` and `Close` as the open project changes, reads
  `SelectedFile`, and calls `DeleteSelected` and `RenameSelected` after confirming with the user.

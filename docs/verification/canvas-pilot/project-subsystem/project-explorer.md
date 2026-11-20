### ProjectExplorer

#### Verification Approach

`ProjectExplorer` is verified at two levels. Unit tests in `ProjectExplorerTests.cs` drive
`ProjectExplorerViewModel` on real temporary project folders. Refreshes are driven through a hand-written
`FakeRefreshScheduler` that holds scheduled callbacks until a test runs them, so debouncing and stale refreshes
are checked without real delays, and a hand-written `FakeImageSourceFilter` keeps most tests independent of the
CanvasNet library; two tests use the real `CanvasNetImageSourceFilter` against `CanvasNetViewer.Extensions`. One
test lets the real `FileSystemWatcher` report a new file and waits (with a timeout) for the explorer to request
the refresh. Headless UI tests in `ProjectExplorerViewTests.cs` (the `UiTests` project, using Avalonia's
headless platform) show the real `ProjectExplorerView`, including one test with the real
`DispatcherRefreshScheduler` that adds and deletes files on disk and waits for the list to follow.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display or windowing system is required. Tests use
temporary local folders and real file I/O; no network or external services are required. Two tests depend on
Windows behavior (the hidden attribute and a file held open blocking deletion) and are skipped on other
platforms; the same requirements are also covered by tests that run everywhere.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures (skipped platform-specific tests excepted).
- Only image source files in the project root are listed, sorted by name; sub-folders, the data folder, the
  project file, and hidden files are not. An empty list and an unreadable folder are explained.
- Every extension in `CanvasNetViewer.Extensions`, including compound ones, is accepted in any case, and other
  files are not.
- The selection is kept for a file that stays and cleared for one that goes.
- A burst of refresh requests leaves one pending refresh after the debounce delay; requests with no project or
  after disposal are ignored; a stale refresh does nothing; the real watcher requests a refresh for a new file.
- Closing, replacing, and disposing stop the watcher and clear or replace the list.
- Delete removes the selected file and reports a message for no selection or an undeletable file.
- Rename moves the file, selects it, allows a case-only change, does nothing for the same name, and refuses empty,
  path-containing, reserved, hidden, unsupported-extension, and existing names without replacing anything.

#### Test Scenarios

**ProjectExplorerViewModel_Open_ProjectFolder_ListsOnlyRootImageSourceFilesSorted**: A folder with root images,
a text file, the project file, a hidden-name file, a sub-folder file, and a data folder file lists only the two
root images sorted ignoring case, with their full paths. This scenario is tested by
`ProjectExplorerViewModel_Open_ProjectFolder_ListsOnlyRootImageSourceFilesSorted`.

**ProjectExplorerViewModel_Open_HiddenAttributeFile_IsNotListed**: A file with the hidden attribute is not
listed (Windows only). This scenario is tested by
`ProjectExplorerViewModel_Open_HiddenAttributeFile_IsNotListed`.

**ProjectExplorerViewModel_Open_NoImageFiles_ShowsEmptyMessage**: A folder with only a text file lists nothing
and shows the empty message. This scenario is tested by
`ProjectExplorerViewModel_Open_NoImageFiles_ShowsEmptyMessage`.

**ProjectExplorerViewModel_Open_MissingFolder_ShowsErrorAndEmptyList**: A missing folder shows and logs an error,
lists nothing, and starts no watcher. This scenario is tested by
`ProjectExplorerViewModel_Open_MissingFolder_ShowsErrorAndEmptyList`.

**CanvasNetImageSourceFilter_IsImageSource_CanvasNetExtensions_AreAccepted**: Every extension the viewer library
lists is accepted in lower and upper case, including `.vl.json`; `.txt`, `.json`, the project extension, a
bare extension, no extension, and an empty name are not. This scenario is tested by
`CanvasNetImageSourceFilter_IsImageSource_CanvasNetExtensions_AreAccepted`.

**CanvasNetImageSourceFilter_IsImageSource_PlantUmlExtensions_AreAccepted**: The PlantUML extensions `.puml`,
`.plantuml`, and `.pu` are in the viewer library's list and are accepted in lower and upper case, so PlantUML
files are listed with no special handling. This scenario is tested by
`CanvasNetImageSourceFilter_IsImageSource_PlantUmlExtensions_AreAccepted`.

**ProjectExplorerViewModel_Open_WithCanvasNetFilter_ListsSvgButNotText**: With the real filter, an SVG is listed
and a text file is not. This scenario is tested by
`ProjectExplorerViewModel_Open_WithCanvasNetFilter_ListsSvgButNotText`.

**ProjectExplorerViewModel_OpenAndClose_StartsAndStopsWatchingAndClearsList**: Opening starts watching; closing
stops it, clears the list, selection, and message, and cancels the pending refresh. This scenario is tested by
`ProjectExplorerViewModel_OpenAndClose_StartsAndStopsWatchingAndClearsList`.

**ProjectExplorerViewModel_Open_AnotherFolder_ReplacesListAndIgnoresStaleRefresh**: Opening a second folder
replaces the list; a refresh scheduled for the first folder, run late, does not change it. This scenario is
tested by `ProjectExplorerViewModel_Open_AnotherFolder_ReplacesListAndIgnoresStaleRefresh`.

**ProjectExplorerViewModel_Refresh_FilesAddedAndRemoved_UpdatesListAndKeepsSelection**: A file added between two
and the first deleted updates the list while the same selected item stays selected. This scenario is tested by
`ProjectExplorerViewModel_Refresh_FilesAddedAndRemoved_UpdatesListAndKeepsSelection`.

**ProjectExplorerViewModel_Refresh_SelectedFileRemoved_ClearsSelection**: Deleting the selected file and
refreshing clears the selection and the list. This scenario is tested by
`ProjectExplorerViewModel_Refresh_SelectedFileRemoved_ClearsSelection`.

**ProjectExplorerViewModel_Refresh_FolderReappears_ClearsError**: A folder that was missing at open appears with
a file; refresh clears the error and lists it. This scenario is tested by
`ProjectExplorerViewModel_Refresh_FolderReappears_ClearsError`.

**ProjectExplorerViewModel_SelectFile_ListedAndUnlisted_ReportsResult**: Selecting a listed file by a path in a
different case succeeds and announces the selected file; an unlisted path reports `false` and clears the
selection. This scenario is tested by `ProjectExplorerViewModel_SelectFile_ListedAndUnlisted_ReportsResult`.

**ProjectExplorerViewModel_RequestRefresh_Burst_CoalescesIntoOneRefresh**: Three requests leave one pending
callback with the custom delay; the list changes only when it runs. This scenario is tested by
`ProjectExplorerViewModel_RequestRefresh_Burst_CoalescesIntoOneRefresh`.

**ProjectExplorerViewModel_RequestRefresh_DefaultDebounce_IsQuarterSecond**: Without a delay argument the
default of 250 milliseconds is used. This scenario is tested by
`ProjectExplorerViewModel_RequestRefresh_DefaultDebounce_IsQuarterSecond`.

**ProjectExplorerViewModel_RequestRefresh_NoProjectOrDisposed_SchedulesNothing**: Requests before a project is
open and after disposal schedule nothing. This scenario is tested by
`ProjectExplorerViewModel_RequestRefresh_NoProjectOrDisposed_SchedulesNothing`.

**ProjectExplorerViewModel_Open_FileCreatedInFolder_WatcherRequestsRefresh**: A file created in the watched
folder makes the real watcher request a refresh within a timeout, and running it lists the file. This scenario
is tested by `ProjectExplorerViewModel_Open_FileCreatedInFolder_WatcherRequestsRefresh`.

**ProjectExplorerViewModel_DeleteSelected_SelectedFile_DeletesFileAndRefreshes**: Deleting the selected file
removes it from disk and list and clears the selection. This scenario is tested by
`ProjectExplorerViewModel_DeleteSelected_SelectedFile_DeletesFileAndRefreshes`.

**ProjectExplorerViewModel_DeleteAndRename_NothingSelected_ReportMessage**: Delete and rename with nothing
selected return a message, show it, and change nothing. This scenario is tested by
`ProjectExplorerViewModel_DeleteAndRename_NothingSelected_ReportMessage`.

**ProjectExplorerViewModel_DeleteSelected_FileInUse_ReturnsMessageAndLogs**: A file held open without sharing
cannot be deleted; a message is returned, shown, and logged and the file stays listed (Windows only). This
scenario is tested by `ProjectExplorerViewModel_DeleteSelected_FileInUse_ReturnsMessageAndLogs`.

**ProjectExplorerViewModel_RenameSelected_ValidName_RenamesAndSelectsNewFile**: A padded new name with another
supported extension moves the file, lists it, and selects it. This scenario is tested by
`ProjectExplorerViewModel_RenameSelected_ValidName_RenamesAndSelectsNewFile`.

**ProjectExplorerViewModel_RenameSelected_CaseOnlyChange_IsAllowed**: Changing only letter case is allowed and
the list shows the new spelling. This scenario is tested by
`ProjectExplorerViewModel_RenameSelected_CaseOnlyChange_IsAllowed`.

**ProjectExplorerViewModel_RenameSelected_SameName_DoesNothing**: Renaming to the same name returns no problem
and leaves the file. This scenario is tested by `ProjectExplorerViewModel_RenameSelected_SameName_DoesNothing`.

**ProjectExplorerViewModel_RenameSelected_InvalidName_IsRefused**: Empty, blank, path-containing, traversal,
unsupported-extension, no-extension, leading-period, reserved, and project-file names are refused with a shown
message and the file untouched. This scenario is tested by
`ProjectExplorerViewModel_RenameSelected_InvalidName_IsRefused`.

**ProjectExplorerViewModel_RenameSelected_TargetExists_IsRefusedAndNothingReplaced**: Renaming onto an existing
file or folder is refused and the existing content is intact. This scenario is tested by
`ProjectExplorerViewModel_RenameSelected_TargetExists_IsRefusedAndNothingReplaced`.

**ProjectExplorerViewModel_Dispose_OpenFolder_StopsWatchingAndRefusesOpen**: Disposing twice stops the watcher,
and opening afterwards throws. This scenario is tested by
`ProjectExplorerViewModel_Dispose_OpenFolder_StopsWatchingAndRefusesOpen`.

**ProjectExplorerViewModel_InvalidArguments_Throw**: Null collaborators and a blank folder are refused. This
scenario is tested by `ProjectExplorerViewModel_InvalidArguments_Throw`.

**ProjectExplorerView_OpenProject_ListsImageFiles**: The shown view lists the two image files in order and
hides the empty message. This scenario is tested by `ProjectExplorerView_OpenProject_ListsImageFiles`.

**ProjectExplorerView_NoProjectOrNoImages_ShowsEmptyMessage**: The view explains an explorer with no project and
a project with no images. This scenario is tested by `ProjectExplorerView_NoProjectOrNoImages_ShowsEmptyMessage`.

**ProjectExplorerView_SelectItem_UpdatesSelectedFile**: Selecting the second row sets the selected file. This
scenario is tested by `ProjectExplorerView_SelectItem_UpdatesSelectedFile`.

**ProjectExplorerView_SelectFileInViewModel_SelectsItemInList**: Selecting by path selects the row. This
scenario is tested by `ProjectExplorerView_SelectFileInViewModel_SelectsItemInList`.

**ProjectExplorerView_RenameRefused_ShowsErrorMessage**: A refused rename shows the error line. This scenario is
tested by `ProjectExplorerView_RenameRefused_ShowsErrorMessage`.

**ProjectExplorerView_FilesChangeOnDisk_ListRefreshesAfterDebounce**: With the real scheduler, a file added and a
file deleted on disk appear in the list by themselves. This scenario is tested by
`ProjectExplorerView_FilesChangeOnDisk_ListRefreshesAfterDebounce`.

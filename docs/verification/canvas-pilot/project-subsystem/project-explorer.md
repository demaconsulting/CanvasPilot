### ProjectExplorer

#### Verification Approach

`ProjectExplorer` is verified at two levels. Unit tests in `ProjectExplorerTests.cs` drive `ProjectExplorerViewModel`
and `ProjectNodeViewModel` on real temporary project folders. Refreshes are driven through a hand-written
`FakeRefreshScheduler` that holds scheduled callbacks until a test runs them, so debouncing and stale refreshes are
checked without real delays; a hand-written `FakeImageSourceFilter` keeps most tests independent of the CanvasNet
library, while two tests use the real `CanvasNetImageSourceFilter` against `CanvasNetViewer.Extensions`; and a
hand-written `RecordingThumbnailSource` (in `ProjectSubsystemFakes.cs`) records which files thumbnails were asked for
and gives no bitmap, or fails, so that the thumbnail behavior is checked without drawing. Two tests let the real
`FileSystemWatcher` report a change and wait (with a timeout) for the explorer to request the refresh. Headless UI tests
in `ProjectExplorerViewTests.cs` (the `UiTests` project, using Avalonia's headless platform) show the real
`ProjectExplorerView`, including one test with the real `DispatcherRefreshScheduler` that adds and deletes files on disk
and waits for the tree to follow, and tests that drag files from a temporary folder onto the drop zone and the rows
through the headless window. The thumbnail view test in `ThumbnailViewTests.cs` shows the explorer over a real
`ThumbnailProvider`, and the accessibility sweep in `AccessibilityViewTests.cs` walks the workspace that holds the
explorer.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display or windowing system is required. Tests use
temporary local folders and real file I/O; no network or external services are required. Two tests depend on
Windows behavior (the hidden attribute and a file held open blocking deletion) and are skipped on other
platforms; the same requirements are also covered by tests that run everywhere.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures (skipped platform-specific tests excepted).
- The project folder is shown as a tree, folders first and then files, each group sorted ignoring case, with the folders
  at any depth, the files that CanvasNet draws, and the assets (bitmaps, PDF files, and `style.json` files); the data
  folder, the project file, hidden files, and other files are not shown. The flat list holds the drawn files in tree
  order. An empty tree and an unreadable folder are explained.
- Every extension in `CanvasNetViewer.Extensions`, including compound ones, is accepted in any case, and other
  files are not. The kind and format of a node follow its name.
- A tree with more entries or folders than the limits is cut short and marked.
- The selection of a node and of a file stay in step: a folder or an asset selects no file; a file selected by path
  shows its folders; a file that stays keeps its selection and one that goes loses it. Expanded folders stay expanded
  across refreshes and a reopening of the same project, and are forgotten for another project.
- A burst of refresh requests leaves one pending refresh after the debounce delay; requests with no project or
  after disposal are ignored; a stale refresh does nothing; the real watcher requests a refresh for a new file and for a
  change in a sub-folder but not for a change in the data folder. The Refresh command reads the folder at once and only
  works with a project.
- Closing, replacing, and disposing stop the watcher and clear or replace the tree.
- Delete removes the selected file wherever it lies and reports a message for no selection or an undeletable file.
- Rename moves the file within its folder, selects it, allows a case-only change, does nothing for the same name, and
  refuses empty, path-containing, reserved, hidden, unsupported-extension, and existing names without replacing
  anything.
- A new file lands in the selected folder, the folder of the selected file, or the project folder, holds a blank drawing
  of its kind, and is selected; a new folder is selected with its parents expanded; bad, hidden, and taken names and a
  missing project are refused with a message.
- Added files are copied, with free names and the source untouched, into `attachments` or the chosen folder; folders,
  other kinds of files, and targets outside the project or in the data folder are refused with a message.
- The view shows rows 26 high with chevrons, a 16 px indent per level, badges, a bar and a semibold name on the selected
  row, the problem line with the danger icon, the header label and three named buttons with tooltips that run their
  commands, and the drop zone only with a project; drops are copied where they fall and lit while the drag is over the
  place.
- The rows are 26 high, or 32 high while the comfortable rows setting is on, and follow the setting when it changes.
- Thumbnails are asked for only for drawn files that can be seen, once, again when a file changes, not while off, and
  freed with removed nodes; a missing or failing source leaves the rows as they were; the view shows a 28 by 18 box only
  on the rows that have one.
- The tree is named and every button has a name, a tooltip, keyboard focus, and a target of at least 28 by 28 pixels.

#### Test Scenarios

**ProjectExplorerViewModel_Open_ProjectFolder_BuildsTreeWithFoldersFirstAndSkipsHiddenAndOtherFiles**: A folder with
root images, a text file, the project file, a hidden-name file, sub-folders with a picture, a bitmap, a PDF, and style
files, and a data folder file shows the folders first and then the files, each sorted ignoring case, with the kind and
format of each node, lists the drawn pictures in the flat list in tree order, and shows neither the hidden files, the
project file, the data folder, nor the text files. This scenario is tested by
`ProjectExplorerViewModel_Open_ProjectFolder_BuildsTreeWithFoldersFirstAndSkipsHiddenAndOtherFiles`.

**ProjectNodeViewModel_KindAndFormat_FromTheFileName**: The kind and the format of a node are told from its file name,
including the compound extension of Vega-Lite and the `style.json` file. This scenario is tested by
`ProjectNodeViewModel_KindAndFormat_FromTheFileName`.

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

**ProjectExplorerViewModel_Open_TooManyOrTooDeep_IsTruncatedAndSaysSo**: A folder with more files than a small limit
holds only the limit and is marked cut short, and a folder with folders deeper than the depth limit is marked too; a
limit of zero is refused. This scenario is tested by
`ProjectExplorerViewModel_Open_TooManyOrTooDeep_IsTruncatedAndSaysSo`.

**ProjectExplorerViewModel_OpenAndClose_StartsAndStopsWatchingAndClearsList**: Opening starts watching; closing
stops it, clears the tree, the list, selection, and message, and cancels the pending refresh. This scenario is tested by
`ProjectExplorerViewModel_OpenAndClose_StartsAndStopsWatchingAndClearsList`.

**ProjectExplorerViewModel_Open_AnotherFolder_ReplacesListAndIgnoresStaleRefresh**: Opening a second folder
replaces the tree; a refresh scheduled for the first folder, run late, does not change it. This scenario is
tested by `ProjectExplorerViewModel_Open_AnotherFolder_ReplacesListAndIgnoresStaleRefresh`.

**ProjectExplorerViewModel_Refresh_FilesAddedAndRemoved_UpdatesListAndKeepsSelection**: A file added between two and the
first deleted updates the tree and the list while the same selected item stays selected. This scenario is tested by
`ProjectExplorerViewModel_Refresh_FilesAddedAndRemoved_UpdatesListAndKeepsSelection`.

**ProjectExplorerViewModel_Refresh_NestedChanges_KeepsNodesSelectionAndExpansion**: A refresh keeps the nodes that
remain (so the selection and the expansion stay), adds new files in sub-folders, removes the ones that went, and clears
the selection of a node that went. This scenario is tested by
`ProjectExplorerViewModel_Refresh_NestedChanges_KeepsNodesSelectionAndExpansion`.

**ProjectExplorerViewModel_Refresh_SelectedFileRemoved_ClearsSelection**: Deleting the selected file and
refreshing clears the selection and the list. This scenario is tested by
`ProjectExplorerViewModel_Refresh_SelectedFileRemoved_ClearsSelection`.

**ProjectExplorerViewModel_Refresh_FolderReappears_ClearsError**: A folder that was missing at open appears with
a file; refresh clears the error and lists it. This scenario is tested by
`ProjectExplorerViewModel_Refresh_FolderReappears_ClearsError`.

**ProjectExplorerViewModel_Expansion_SurvivesRefreshAndReopenOfTheSameProjectOnly**: Folders that the user expanded stay
expanded after a refresh that makes new nodes for them and when the same project is opened again, and are forgotten when
another project is opened. This scenario is tested by
`ProjectExplorerViewModel_Expansion_SurvivesRefreshAndReopenOfTheSameProjectOnly`.

**ProjectExplorerViewModel_SelectFile_ListedAndUnlisted_ReportsResult**: Selecting a listed file by a path in a
different case succeeds and announces the selected file; an unlisted path reports `false` and clears the
selection. This scenario is tested by `ProjectExplorerViewModel_SelectFile_ListedAndUnlisted_ReportsResult`.

**ProjectExplorerViewModel_SelectedNode_FolderFileAndAsset_FollowsTheSelectionOfTheFile**: Selecting a node selects the
file when it is drawn and no file for a folder or an asset that is not drawn; selecting a file by path expands the
folders above it and selects its node; the two selections stay in step. This scenario is tested by
`ProjectExplorerViewModel_SelectedNode_FolderFileAndAsset_FollowsTheSelectionOfTheFile`.

**ProjectExplorerViewModel_RequestRefresh_Burst_CoalescesIntoOneRefresh**: Three requests leave one pending
callback with the custom delay; the tree changes only when it runs. This scenario is tested by
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

**ProjectExplorerViewModel_Open_ChangeInSubFolder_WatcherRequestsRefresh**: A change in a sub-folder makes the real
watcher request a refresh, and running it lists the new file. This scenario is tested by
`ProjectExplorerViewModel_Open_ChangeInSubFolder_WatcherRequestsRefresh`.

**ProjectExplorerViewModel_IsInDataFolder_Path_IsTrueOnlyForTheDataFolderAndItsContent**: The check that the watcher
uses to ignore changes is true for the hidden data folder and the paths inside it, in any letter case, and false for a
sibling whose name starts like it, a data folder of a sub-folder, a project file, and other files. This scenario is
tested by `ProjectExplorerViewModel_IsInDataFolder_Path_IsTrueOnlyForTheDataFolderAndItsContent`.

**ProjectExplorerViewModel_AddFilesToFolderAndRefreshCommand_ShowTheNewFiles**: A file added to a folder of the project
shows in that folder, a file created behind the explorer's back shows after the Refresh command runs, and the command
can be used only while a project is open. This scenario is tested by
`ProjectExplorerViewModel_AddFilesToFolderAndRefreshCommand_ShowTheNewFiles`.

**ProjectExplorerViewModel_DeleteSelected_SelectedFile_DeletesFileAndRefreshes**: Deleting the selected file
removes it from disk and tree and clears the selection. This scenario is tested by
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
the tree shows the new spelling. This scenario is tested by
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

**ProjectExplorerViewModel_RenameAndDelete_FileInSubFolder_StaysInItsFolder**: A picture in a sub-folder is renamed
within its own folder and stays selected, and is then deleted from disk and tree. This scenario is tested by
`ProjectExplorerViewModel_RenameAndDelete_FileInSubFolder_StaysInItsFolder`.

**ProjectExplorerViewModel_CreateFile_TargetFolderAndNames_CreatesOrRefuses**: A new file goes into the selected folder,
the folder of the selected file, or the project folder, gets a blank drawing of its kind, is listed and selected, and a
bad, unsupported, or existing name is refused. This scenario is tested by
`ProjectExplorerViewModel_CreateFile_TargetFolderAndNames_CreatesOrRefuses`.

**ProjectExplorerViewModel_CreateFile_KindsAndNoProject_GetTemplatesOrAreRefused**: The blank drawings fit the SVG,
Mermaid, and Vega-Lite kinds, other kinds start empty, and nothing is created while no project is open. This scenario is
tested by `ProjectExplorerViewModel_CreateFile_KindsAndNoProject_GetTemplatesOrAreRefused`.

**ProjectExplorerViewModel_CreateFolder_NamesAndTarget_CreatesOrRefuses**: A new folder is created in the target folder,
is selected with the folders above it expanded, and names that are bad, start with a period, or exist are refused. This
scenario is tested by `ProjectExplorerViewModel_CreateFolder_NamesAndTarget_CreatesOrRefuses`.

**ProjectExplorerViewModel_AddFiles_PicturesAndOthers_CopiesAndExplains**: Added files are copied into the attachments
folder, or a chosen folder of the project, under free names; the sources stay; folders and other kinds of files are left
out and named in the message; a target outside the project, and one in the data folder, is refused. This scenario is
tested by `ProjectExplorerViewModel_AddFiles_PicturesAndOthers_CopiesAndExplains`.

**ProjectExplorerViewModel_Thumbnails_AskedOnlyForDrawnVisibleFilesAndAgainWhenTheFileChangesOrTheSettingIsOn**:
Thumbnails are asked for only for drawn files whose rows can be seen (every folder above them expanded), once for each,
again for a file that changed, not at all while thumbnails are off, and again for the files that can be seen when they
are turned on. This scenario is tested by
`ProjectExplorerViewModel_Thumbnails_AskedOnlyForDrawnVisibleFilesAndAgainWhenTheFileChangesOrTheSettingIsOn`.

**ProjectExplorerViewModel_Dispose_ThumbnailBeingMade_IsCancelled**: A thumbnail that is being made when the explorer is
disposed is told to stop through the token of the request, and the failure that the source reports afterwards leaves the
node without a thumbnail. This scenario is tested by `ProjectExplorerViewModel_Dispose_ThumbnailBeingMade_IsCancelled`.

**ProjectExplorerViewModel_Thumbnails_NoSourceOrFailingSource_LeavesNodesWithoutThumbnail**: An explorer with no source
of thumbnails asks for none and works as before, and a source that fails leaves the node without a thumbnail and does
not break the explorer. This scenario is tested by
`ProjectExplorerViewModel_Thumbnails_NoSourceOrFailingSource_LeavesNodesWithoutThumbnail`.

**ProjectExplorerViewModel_Nodes_RemovedOrCleared_AreDisposed**: Nodes that are removed from the tree or cleared with it
are disposed, so that a thumbnail that arrives for them later is thrown away. This scenario is tested by
`ProjectExplorerViewModel_Nodes_RemovedOrCleared_AreDisposed`.

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
file deleted on disk appear in the tree by themselves. This scenario is tested by
`ProjectExplorerView_FilesChangeOnDisk_ListRefreshesAfterDebounce`.

**ProjectExplorerView_FoldersAndFiles_ShowAsRowsWithBadgesAndIndent**: A folder row has a chevron and, once expanded,
its children are indented 16 pixels below it; every row has a badge of its kind and is 26 pixels high; selecting a
folder selects no file and selecting a picture selects its file. This scenario is tested by
`ProjectExplorerView_FoldersAndFiles_ShowAsRowsWithBadgesAndIndent`.

**ProjectExplorerView_SelectedRow_ShowsBarAndSemiboldName**: Only the selected row shows the accent bar and the semibold
name. This scenario is tested by `ProjectExplorerView_SelectedRow_ShowsBarAndSemiboldName`.

**ProjectExplorerView_UnreadableFolder_ShowsTheProblemWithTheDangerIcon**: A missing project folder shows its words in
the danger color with an icon of the same color, and lists nothing. This scenario is tested by
`ProjectExplorerView_UnreadableFolder_ShowsTheProblemWithTheDangerIcon`.

**ProjectExplorerView_Header_ShowsLabelAndButtonsThatRunTheCommands**: The header shows the label EXPLORER and three
buttons with names, tooltips, and targets of at least 28 by 28 pixels; they run the New file and New folder commands,
and Refresh reads the folder again. This scenario is tested by
`ProjectExplorerView_Header_ShowsLabelAndButtonsThatRunTheCommands`.

**ProjectExplorerView_NoCommandsNoProject_ButtonsOffAndDropZoneHidden**: With no project and no commands the buttons are
off and the drop zone is hidden; opening a project turns the Refresh button and the zone on. This scenario is tested by
`ProjectExplorerView_NoCommandsNoProject_ButtonsOffAndDropZoneHidden`.

**ProjectExplorerView_DropFiles_OnZoneAndOnFolderRow_CopiesAndHighlights**: A file dropped on the drop zone is copied
into the attachments folder and one dropped on a folder row into that folder, the place is lit during the drag and not
after the drop, and the sources stay. This scenario is tested by
`ProjectExplorerView_DropFiles_OnZoneAndOnFolderRow_CopiesAndHighlights`.

**ProjectExplorerView_DropTextOrOtherFile_ChangesNothingOrExplains**: A drop of text lights nothing and adds nothing; a
dropped text file is refused with a message and not copied. This scenario is tested by
`ProjectExplorerView_DropTextOrOtherFile_ChangesNothingOrExplains`.

**ProjectExplorerView_Thumbnails_ShownOnRowsOfDrawnFilesAndFollowTheSetting**: A row of a picture shows a thumbnail box
28 by 18 once the picture is made, a PDF row and a folder row show none, turning thumbnails off takes them away, and
turning them on makes them again. This scenario is tested by
`ProjectExplorerView_Thumbnails_ShownOnRowsOfDrawnFilesAndFollowTheSetting`.

**ProjectExplorerViewModel_ComfortableRows_DefaultsToCompactAndRaisesPropertyChanged**: A new explorer has compact rows,
and each change of the setting is reported once. This scenario is tested by
`ProjectExplorerViewModel_ComfortableRows_DefaultsToCompactAndRaisesPropertyChanged`.

**ProjectExplorerView_ComfortableRows_RowsAre32HighInsteadOf26**: A row of the tree is 26 high, becomes 32 high when the
comfortable rows setting is turned on, and is 26 high again when it is turned off. This scenario is tested by
`ProjectExplorerView_ComfortableRows_RowsAre32HighInsteadOf26`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: In the workspace of an open project, which holds the
explorer, in both theme variants, the tree has a name and every button without text has a name, a tooltip, keyboard
focus, and a size of at least 28 by 28. This scenario is tested by
`Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.

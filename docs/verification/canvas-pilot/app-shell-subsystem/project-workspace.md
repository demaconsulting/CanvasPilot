### ProjectWorkspace

#### Verification Approach

`ProjectWorkspace` is verified by unit tests in `ProjectWorkspaceTests.cs`, which drive the workspace view model
over a real `ProjectExplorerViewModel` on temporary folders with a hand-written `FakeDialogService` playing the
user, and by headless UI tests in `MainWindowViewTests.cs` that show the real workspace view inside the main window
and interact with it through the real message dialog. The tests assert on the explorer list, the placeholder text,
the files on disk, and the dialogs shown.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform, serialized with
test parallelization disabled; no display is required. Tests use temporary local folders.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The placeholder says `No project open`, then `No file selected`, then `No preview available`, and supplied
  preview content replaces it and is shown in the preview host.
- The chat content is shown (in the column on the right of the preview) only while a project is open and content was
  supplied; it is not disposed with the workspace. With a project open the explorer is 260 wide, the chat column is 380
  wide, the preview is between them with a splitter on each side, and the chat column takes no room without a project.
- Showing a project lists its image files; showing it again keeps the selection; showing none empties the list.
- Delete asks first and deletes only when confirmed; Rename starts with the current name, renames when a name is
  entered, and shows a problem for an unacceptable name; both do nothing when cancelled or without a dialog service.

#### Test Scenarios

**ProjectWorkspace_New_NoProject_ShowsNoProjectPlaceholder**: A new workspace shows the no-project placeholder and
hint, no preview, and disabled file commands. This scenario is tested by
`ProjectWorkspace_New_NoProject_ShowsNoProjectPlaceholder`.

**ProjectWorkspace_Constructor_NullExplorer_Throws**: A missing explorer is refused. This scenario is tested by
`ProjectWorkspace_Constructor_NullExplorer_Throws`.

**ProjectWorkspace_ShowProject_ListsFilesAndTracksSelection**: Showing a project lists its image files in name
order; selecting a file changes the placeholder and enables the file commands, and the changes are announced. This
scenario is tested by `ProjectWorkspace_ShowProject_ListsFilesAndTracksSelection`.

**ProjectWorkspace_ShowProject_SameProjectKeepsSelectionAndNullCloses**: Showing the same project keeps the
selection; showing none empties the explorer. This scenario is tested by
`ProjectWorkspace_ShowProject_SameProjectKeepsSelectionAndNullCloses`.

**ProjectWorkspace_Preview_SuppliedContent_ReplacesPlaceholder**: Supplied content is exposed and announced, and
removing it restores the placeholder state. This scenario is tested by
`ProjectWorkspace_Preview_SuppliedContent_ReplacesPlaceholder`.

**ProjectWorkspace_DeleteSelected_ConfirmedDeletesAndDeclinedKeeps**: A declined confirmation keeps the file; a
confirmed one deletes it; the question names the file. This scenario is tested by
`ProjectWorkspace_DeleteSelected_ConfirmedDeletesAndDeclinedKeeps`.

**ProjectWorkspace_RenameSelected_NewName_RenamesFile**: The prompt starts with the current name and the entered
name renames the file, which stays selected. This scenario is tested by
`ProjectWorkspace_RenameSelected_NewName_RenamesFile`.

**ProjectWorkspace_RenameSelected_CancelledOrInvalidName_ChangesNothingAndReports**: A cancelled prompt is silent;
a name that is not an image source file name is shown as a problem and nothing is renamed. This scenario is tested
by `ProjectWorkspace_RenameSelected_CancelledOrInvalidName_ChangesNothingAndReports`.

**ProjectWorkspace_FileCommands_NoDialogService_DoNothing**: Without a dialog service both commands leave the file
alone. This scenario is tested by `ProjectWorkspace_FileCommands_NoDialogService_DoNothing`.

**ProjectWorkspace_Chat_ShownOnlyWhileProjectIsOpen**: With chat content set, the chat is not visible before a
project is shown, is visible with a project, and is not visible after the project is closed; the changes are
announced. This scenario is tested by `ProjectWorkspace_Chat_ShownOnlyWhileProjectIsOpen`.

**ProjectWorkspace_Chat_NoContent_NotShownWithProject**: Without chat content the chat is not visible even with a
project open. This scenario is tested by `ProjectWorkspace_Chat_NoContent_NotShownWithProject`.

**ProjectWorkspace_Dispose_DoesNotDisposeChatContent**: Disposing the workspace leaves the chat content alone,
because the composition root owns it. This scenario is tested by
`ProjectWorkspace_Dispose_DoesNotDisposeChatContent`.

**MainWindowView_Workspace_ChatColumnVisibleOnlyWithProject_ThreeColumns**: In the window with no project the chat
host and its splitter are not visible; once a project is open the chat panel is shown, 400 wide, to the right of the
preview, which is to the right of the explorer, 264 wide; after the project is closed the chat host is hidden again.
This scenario is tested by `MainWindowView_Workspace_ChatColumnVisibleOnlyWithProject_ThreeColumns`.

**ProjectWorkspace_Dispose_DisposesExplorer**: Disposing the workspace disposes the explorer. This scenario is
tested by `ProjectWorkspace_Dispose_DisposesExplorer`.

**ProjectWorkspace_Dispose_DisposesPreviewContent**: Disposing the workspace disposes a preview that needs
disposal, such as the preview pane. This scenario is tested by `ProjectWorkspace_Dispose_DisposesPreviewContent`.

**MainWindowView_Workspace_SelectionAndPreview_ChangePlaceholderAndContent**: In the window, with the composed
preview pane removed, the workspace's placeholder follows the list selection and supplied preview content hides it
and shows the preview host. This scenario is tested by
`MainWindowView_Workspace_SelectionAndPreview_ChangePlaceholderAndContent`.

**MainWindowView_DeleteSelectedFile_ConfirmDialog_RemovesFile**: Running Delete in the window shows a dialog naming
the file; confirming it removes the file from the disk and the list. This scenario is tested by
`MainWindowView_DeleteSelectedFile_ConfirmDialog_RemovesFile`.

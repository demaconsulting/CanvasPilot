### ProjectWorkspace

#### Verification Approach

`ProjectWorkspace` is verified at three levels. Unit tests in `ProjectWorkspaceTests.cs` drive the workspace view model
over a real `ProjectExplorerViewModel` on temporary folders with a hand-written `FakeDialogService` playing the
user. Unit tests in `WindowLayoutTests.cs` call the pure rules of `WindowLayout.cs` (`WindowPlacement.Resolve` and
`WorkspaceLayout.Fit`) with made-up screens and widths, so no window is needed. Headless UI tests in
`MainWindowViewTests.cs` and `WorkspaceLayoutViewTests.cs` show the real workspace view inside the main window, with the
composed application on temporary folders, at several window widths, and interact with it through the keyboard and
the real message dialog. The tests assert on the explorer list, the placeholder text, the widths and places of the
columns, the saved settings, the files on disk, and the dialogs shown. Dragging a splitter with the mouse and moving the
window between monitors need a real window and are checked by hand before a release; the tests use the keyboard, which
ends in the same code path that saves the widths.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform, serialized with
test parallelization disabled; no display is required. Tests use temporary local folders.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The placeholder says `No project open`, then `No file selected`, then `No preview available`, and supplied
  preview content replaces it and is shown in the preview host.
- The chat content is shown (in the column on the right of the preview) only while a project is open, content was
  supplied, and the user has not hidden it; it is not disposed with the workspace. With a project open the explorer is
  264 wide, the chat column is 400 wide, the preview is between them with a splitter on each side, and the chat column
  takes no room without a project.
- Hiding the chat keeps its content, removes the column, and offers the restore chip; using the chip brings the column
  back.
- Chosen widths stay within 180 to 420 (explorer) and 320 to 640 (chat); a restore announces nothing and a choice
  announces once; the arrow keys move a focused splitter by 16 pixels and the choice is saved.
- At the window widths 1440, 1100, 1000, and 960 nothing is cropped, the stage is at least 360 wide, the picture lies in
  the stage, and the explorer gives up room first (264, 264, 238, 198); below 942 the explorer is at its least and the
  chat column gives up room too, down to 320; with no chat column it has no width.
- A saved window that lies on a screen is kept with its state; one on a removed screen is left to the system; one that
  hangs over an edge is moved inside; one larger than the screen is shrunk to it but never below 960 by 600; the display
  scale is used.
- Showing a project lists its image files; showing it again keeps the selection; showing none empties the list.
- Delete asks first and deletes only when confirmed; Rename starts with the current name, renames when a name is
  entered, and shows a problem for an unacceptable name; both do nothing when cancelled or without a dialog service.
- New File and New Folder are usable only while a project is open, ask for a name with a suggestion, create in the
  selected folder, do nothing on cancel, and show a refused name.

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

**ProjectWorkspace_NewFileAndNewFolder_AskForNameCreateOrReport**: Both commands are unusable with no project and usable
with one; each asks for a name with its suggestion (`untitled.svg`, `New folder`), creates what was named in the
selected folder, does nothing when the prompt is cancelled, and shows a message for a refused name; the explorer holds
the same commands for its buttons. This scenario is tested by
`ProjectWorkspace_NewFileAndNewFolder_AskForNameCreateOrReport`.

**ProjectWorkspace_ChatCollapsed_HidesColumnAndOffersRestore**: With a project and chat content the column is shown and
no chip is offered; collapsing hides the column, offers the chip, and announces both changes; the restore command shows
the column again and removes the chip. This scenario is tested by
`ProjectWorkspace_ChatCollapsed_HidesColumnAndOffersRestore`.

**ProjectWorkspace_ColumnWidths_RestoreAndChoose_AreClampedAndAnnouncedOnlyForChoices**: A new workspace has the widths
264 and 400; a restore of 300 and 500 changes them without announcing a choice; a choice of 10 and 9999 is clamped to
180 and 640 and announced once; the same choice again announces nothing. This scenario is tested by
`ProjectWorkspace_ColumnWidths_RestoreAndChoose_AreClampedAndAnnouncedOnlyForChoices`.

**WorkspaceLayout_WindowWidth_NothingIsCroppedAndTheExplorerGivesUpRoomFirst**: In windows 1440, 1100, 1000, and 960
wide, over a project with a drawn picture, the explorer is 264, 264, 238, and 198 wide and the chat column 400, the
stage is at least 360 wide, the chat column ends inside the window, and the picture lies inside the stage. This scenario
is tested by `WorkspaceLayout_WindowWidth_NothingIsCroppedAndTheExplorerGivesUpRoomFirst`.

**WorkspaceLayout_Splitters_SavedWidthsUsedAndKeyboardChoiceSaved**: With saved widths of 300 and 500 the window opens
with those widths; the right arrow on the focused explorer splitter and the left arrow on the chat splitter each move it
by 16, the view model holds 316 and 516, the columns are that wide, and the settings hold the same. This scenario is
tested by `WorkspaceLayout_Splitters_SavedWidthsUsedAndKeyboardChoiceSaved`.

**WorkspaceLayout_Fit_WideWindow_UsesTheChosenWidthsWithinTheirLimits**: In a window 1600 wide the chosen 264 and 400
are used, and in one 3000 wide the chosen 50 and 2000 are clamped to 180 and 640. This scenario is tested by
`WorkspaceLayout_Fit_WideWindow_UsesTheChosenWidthsWithinTheirLimits`.

**WorkspaceLayout_Fit_NarrowWindow_TakesRoomFromTheExplorerFirst**: From 1100 down to 700 wide the explorer gives up
room first (264, 238, 198, 180) and then the chat column (358 at 900, 320 at 862 and below), and the stage keeps 360
whenever the window is at least 862 wide. This scenario is tested by
`WorkspaceLayout_Fit_NarrowWindow_TakesRoomFromTheExplorerFirst`.

**WorkspaceLayout_Fit_ChatNotShown_HasNoChatWidth**: With no chat shown the chat width is 0 and the explorer gives up
room only when the stage needs it (239 in a window 600 wide, 264 in a wide one). This scenario is tested by
`WorkspaceLayout_Fit_ChatNotShown_HasNoChatWidth`.

**WindowPlacement_Resolve_LayoutOnAScreen_IsKept**: A saved layout on a screen, normal and maximized, is returned as it
is. This scenario is tested by `WindowPlacement_Resolve_LayoutOnAScreen_IsKept`.

**WindowPlacement_Resolve_ScreenRemovedOrUnknown_LeavesThePlaceToTheSystem**: A window saved on a screen that was
removed, a layout with no place, and an empty list of screens keep the size and get no place. This scenario is tested by
`WindowPlacement_Resolve_ScreenRemovedOrUnknown_LeavesThePlaceToTheSystem`.

**WindowPlacement_Resolve_HangingOrTooLarge_IsMovedAndShrunkToTheScreen**: A window that hangs over the edge is moved
inside the screen, one on the second screen stays there, and one larger than the screen is made as large as the screen
holds. This scenario is tested by `WindowPlacement_Resolve_HangingOrTooLarge_IsMovedAndShrunkToTheScreen`.

**WindowPlacement_Resolve_TooSmallOrScaled_KeepsTheSmallestSizeAndUsesTheScale**: A saved size below 960 by 600 is
raised to it; a window of 1500 by 900 keeps its size on a screen of 3000 by 1800 pixels at a display scale of 2 and is
shrunk to 1000 by 600 on a screen of 2000 by 1200 pixels. This scenario is tested by
`WindowPlacement_Resolve_TooSmallOrScaled_KeepsTheSmallestSizeAndUsesTheScale`.

**WindowPlacement_Resolve_NullArguments_Throw**: A missing layout or list of screens is refused. This scenario is tested
by `WindowPlacement_Resolve_NullArguments_Throw`.

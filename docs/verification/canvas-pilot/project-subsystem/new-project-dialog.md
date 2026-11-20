### NewProjectDialog

#### Verification Approach

`NewProjectDialog` is verified at two levels. Unit tests in `NewProjectDialogTests.cs` drive
`NewProjectDialogViewModel` directly with a real `ProjectService` on temporary folders and an NSubstitute
`IFolderPicker`, so browsing is checked without a platform dialog. Headless UI tests in
`NewProjectDialogViewTests.cs` (the `UiTests` project, using Avalonia's headless platform) show the real
`NewProjectDialogView` and interact with it through typing, mouse clicks, and a hand-written folder picker,
finding controls by automation identifier. The headless tests assert the observable result, such as message
visibility, enabled state, shown text, and whether the window closed.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display or windowing system is required. Tests use
temporary local folders; no network or external services are required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The dialog starts with the supplied folder and an empty name, reports what is missing, and has OK disabled.
- A valid name and folder clear the messages, show the target path, and enable OK; an invalid name, a missing
  folder, and an existing non-empty project folder each show a message and keep OK disabled.
- Browse starts in the current folder, shows the chosen folder, and keeps the entry when cancelled.
- OK with valid entries accepts and closes; Cancel closes without accepting.

#### Test Scenarios

**NewProjectDialogViewModel_New_InitialFolder_ShowsFolderAndDisablesOk**: A new dialog shows its title and the
folder, reports the missing name, and has OK disabled. This scenario is tested by
`NewProjectDialogViewModel_New_InitialFolder_ShowsFolderAndDisablesOk`.

**NewProjectDialogViewModel_New_NoInitialFolder_AsksForFolder**: Without a starting folder the folder entry is
empty and reported. This scenario is tested by `NewProjectDialogViewModel_New_NoInitialFolder_AsksForFolder`.

**NewProjectDialogViewModel_ValidEntries_EnableOkAndShowTargetPath**: Typing a padded name enables OK, clears
the messages, shows the trimmed target path, and announces the dependent properties. This scenario is tested by
`NewProjectDialogViewModel_ValidEntries_EnableOkAndShowTargetPath`.

**NewProjectDialogViewModel_InvalidName_ShowsMessageAndDisablesOk**: A name with a slash shows a message,
disables OK, and clears the target path. This scenario is tested by
`NewProjectDialogViewModel_InvalidName_ShowsMessageAndDisablesOk`.

**NewProjectDialogViewModel_MissingFolder_ShowsMessageAndDisablesOk**: A folder that does not exist shows a
message and disables OK. This scenario is tested by
`NewProjectDialogViewModel_MissingFolder_ShowsMessageAndDisablesOk`.

**NewProjectDialogViewModel_ExistingNonEmptyFolder_ShowsTargetErrorAndDisablesOk**: A name that matches a
folder with content shows the target message and disables OK. This scenario is tested by
`NewProjectDialogViewModel_ExistingNonEmptyFolder_ShowsTargetErrorAndDisablesOk`.

**NewProjectDialogViewModel_Browse_FolderChosen_SetsParentFolder**: Browse asks the picker, starting in the
current folder, and the chosen folder becomes the parent folder. This scenario is tested by
`NewProjectDialogViewModel_Browse_FolderChosen_SetsParentFolder`.

**NewProjectDialogViewModel_Browse_Cancelled_KeepsParentFolder**: A cancelled picker leaves the folder
unchanged. This scenario is tested by `NewProjectDialogViewModel_Browse_Cancelled_KeepsParentFolder`.

**NewProjectDialogViewModel_Browse_NoFolderEntered_PassesNoStartFolder**: With no folder entered the picker is
given no start folder. This scenario is tested by
`NewProjectDialogViewModel_Browse_NoFolderEntered_PassesNoStartFolder`.

**NewProjectDialogViewModel_Ok_ValidEntries_AcceptsAndRequestsClose**: OK marks the dialog accepted and
requests one close with acceptance. This scenario is tested by
`NewProjectDialogViewModel_Ok_ValidEntries_AcceptsAndRequestsClose`.

**NewProjectDialogViewModel_Ok_InvalidEntries_DoesNothing**: With an invalid name OK cannot run, the dialog is
not accepted, and no close is requested. This scenario is tested by
`NewProjectDialogViewModel_Ok_InvalidEntries_DoesNothing`.

**NewProjectDialogViewModel_Cancel_RequestsCloseWithoutAcceptance**: Cancel requests one close without
acceptance. This scenario is tested by `NewProjectDialogViewModel_Cancel_RequestsCloseWithoutAcceptance`.

**NewProjectDialogViewModel_ProjectName_BecomesValid_SignalsOkCanExecuteChanged**: Typing a valid name signals a
change of the OK command state. This scenario is tested by
`NewProjectDialogViewModel_ProjectName_BecomesValid_SignalsOkCanExecuteChanged`.

**NewProjectDialogViewModel_Constructor_NullArguments_Throw**: A null service or picker is refused. This
scenario is tested by `NewProjectDialogViewModel_Constructor_NullArguments_Throw`.

**NewProjectDialogView_Show_EmptyName_ShowsMessageAndDisablesOk**: The shown window has its title and folder,
the name message visible, no folder message, OK disabled, and Cancel enabled. This scenario is tested by
`NewProjectDialogView_Show_EmptyName_ShowsMessageAndDisablesOk`.

**NewProjectDialogView_TypeValidName_EnablesOkAndShowsTargetPath**: Typing a name hides the name message, shows
the target path, and enables OK. This scenario is tested by
`NewProjectDialogView_TypeValidName_EnablesOkAndShowsTargetPath`.

**NewProjectDialogView_InvalidEntries_ShowMessagesAndDisableOk**: An invalid name and then a missing folder show
their messages and disable OK. This scenario is tested by
`NewProjectDialogView_InvalidEntries_ShowMessagesAndDisableOk`.

**NewProjectDialogView_BrowseButton_ShowsChosenFolder**: Clicking Browse asks the picker, which started in the
old folder, and the folder box shows the chosen folder. This scenario is tested by
`NewProjectDialogView_BrowseButton_ShowsChosenFolder`.

**NewProjectDialogView_OkButton_ClosesWindowAndAccepts**: Clicking OK closes the window and accepts. This
scenario is tested by `NewProjectDialogView_OkButton_ClosesWindowAndAccepts`.

**NewProjectDialogView_CancelButton_ClosesWindowWithoutAccepting**: Clicking Cancel closes the window without
accepting. This scenario is tested by `NewProjectDialogView_CancelButton_ClosesWindowWithoutAccepting`.

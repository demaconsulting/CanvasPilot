### MainWindowShell

#### Verification Approach

`MainWindowShell` is verified at two levels. Unit tests in `MainWindowShellTests.cs` drive the shell view model over
a real project service, recent projects list, and settings service on temporary folders, with a hand-written
`FakeDialogService` that plays the user and a hand-written `FakeImageExporter`; two tests use an NSubstitute
`IProjectService` to force a failed creation and a failed save. Unit tests in `MessageDialogTests.cs` drive the
message dialog view model. Headless UI tests in `MainWindowViewTests.cs` (the `UiTests` project, using Avalonia's
headless platform) show the real `MainWindowView`, find controls by name and automation identifier, run the
commands behind the menu items, and exercise the real message, About, Settings, and New Project dialogs by clicking
their buttons. The platform pickers cannot be shown headless; the UI test confirms they answer as cancelled, and
the real pickers are checked manually before a release. Likewise the real keyboard (the repeat of a held F8, the
window losing the active state when the user switches to another program, and a screen reader's use of the Edit
menu) is checked manually on a desktop before a release; the headless tests send key events and call the platform's
deactivation callback.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display or windowing system is required. Tests use
temporary local folders; no network or external services are required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The window has the File, Edit, Image, Tools, and Help menus with the expected items in order; every item has a name
  and an automation identifier that are the same; with no project only the commands that need nothing are usable.
- Undo and Redo act on the focused part that can undo and redo, are usable only while it has a step to undo or redo,
  follow its changes, and are unusable and do nothing while the focus is in a part that cannot (for example the
  explorer) or while there is no target; a target that was replaced is no longer followed.
- The F8 key starts the push-to-talk target once while it is held (repeats are ignored), stops the target that was
  started when the key goes up, the window closes, or the window is deactivated, and does nothing without a target.
- The title is the application name, or the project name followed by the application name, and follows a project
  opened, closed, or created through the service, and stops following after disposal.
- Accepting the New Project dialog creates, shows, and remembers the project; canceling creates nothing; a failed
  creation, a damaged file, and a failed save are shown to the user.
- Recent projects are listed newest first, open when chosen, show underscores as typed, and a missing project is
  reported and removed.
- Exit closes the window; Save and Close follow the open project.
- Settings opens over its pages and tells them when it closed; About opens the About dialog.
- The Image commands are disabled until the exporter reports an image, follow it, and run it with the dialog
  service.
- Message, confirmation, and prompt dialogs return the user's answer, trim a prompt's text, and cannot accept a
  blank prompt.

#### Test Scenarios

**MainWindowShell_New_NoProject_TitleIsApplicationNameAndProjectCommandsDisabled**: A new shell shows the
application name, has no project or recent projects, and only the commands that need nothing can run. This
scenario is tested by `MainWindowShell_New_NoProject_TitleIsApplicationNameAndProjectCommandsDisabled`.

**MainWindowShell_Constructor_NullArguments_Throw**: Each missing collaborator, and a missing dialog service, is
refused. This scenario is tested by `MainWindowShell_Constructor_NullArguments_Throw`.

**MainWindowShell_NewProject_DialogAccepted_CreatesAndShowsProject**: The user types a name and chooses OK; the
project exists, titles the window, is shown in the explorer and remembered, the dialog started in the default
projects folder, and no message was shown. This scenario is tested by
`MainWindowShell_NewProject_DialogAccepted_CreatesAndShowsProject`.

**MainWindowShell_NewProject_DialogCancelled_CreatesNothing**: Canceling creates no project or folder. This
scenario is tested by `MainWindowShell_NewProject_DialogCancelled_CreatesNothing`.

**MainWindowShell_NewProject_CreateFails_ShowsMessage**: A service that fails to create the accepted project
leads to a message with the problem and no open project. This scenario is tested by
`MainWindowShell_NewProject_CreateFails_ShowsMessage`.

**MainWindowShell_OpenProject_FileChosen_OpensAndRemembersProject**: A chosen project file opens, titles the
window, fills the explorer, and is remembered. This scenario is tested by
`MainWindowShell_OpenProject_FileChosen_OpensAndRemembersProject`.

**MainWindowShell_OpenProject_PickerCancelled_OpensNothing**: A cancelled picker opens nothing and shows no
message. This scenario is tested by `MainWindowShell_OpenProject_PickerCancelled_OpensNothing`.

**MainWindowShell_OpenProject_DamagedFile_ShowsMessageAndOpensNothing**: A damaged file is reported with the
dialog title, no project opens, and nothing is remembered. This scenario is tested by
`MainWindowShell_OpenProject_DamagedFile_ShowsMessageAndOpensNothing`.

**MainWindowShell_OpenProject_WithRecentProject_PickerStartsInProjectsFolder**: With a remembered project, the
picker is asked to start in the folder that holds the project's folder. This scenario is tested by
`MainWindowShell_OpenProject_WithRecentProject_PickerStartsInProjectsFolder`.

**MainWindowShell_RecentProjects_ListedMostRecentFirstAndOpenOnChoice**: Two remembered projects are listed
newest first; choosing the older one opens it and moves it to the top. This scenario is tested by
`MainWindowShell_RecentProjects_ListedMostRecentFirstAndOpenOnChoice`.

**MainWindowShell_RecentProjectItem_NameWithUnderscore_HeaderDoublesUnderscore**: A name with an underscore keeps
it and the menu header doubles it. This scenario is tested by
`MainWindowShell_RecentProjectItem_NameWithUnderscore_HeaderDoublesUnderscore`.

**MainWindowShell_RecentProjects_MissingProject_ShowsMessageAndRemovesEntry**: Choosing a project whose folder was
deleted shows a message, opens nothing, and empties the menu. This scenario is tested by
`MainWindowShell_RecentProjects_MissingProject_ShowsMessageAndRemovesEntry`.

**MainWindowShell_SaveProject_OpenProject_WritesModifiedTime**: Save writes a modified time to the project file
and shows no message. This scenario is tested by `MainWindowShell_SaveProject_OpenProject_WritesModifiedTime`.

**MainWindowShell_SaveProject_SaveFails_ShowsMessage**: A failed save is shown with the problem. This scenario is
tested by `MainWindowShell_SaveProject_SaveFails_ShowsMessage`.

**MainWindowShell_CloseProject_OpenProject_ResetsTitleAndExplorer**: Close restores the title, empties the
explorer, and disables Save and Close. This scenario is tested by
`MainWindowShell_CloseProject_OpenProject_ResetsTitleAndExplorer`.

**MainWindowShell_ProjectServiceChanges_TitleFollows**: A project created and then closed directly through the
service changes the title and explorer. This scenario is tested by `MainWindowShell_ProjectServiceChanges_TitleFollows`.

**MainWindowShell_Dispose_StopsFollowingServices**: After disposal, a project opened through the service does
not change the title, and disposing twice is harmless. This scenario is tested by
`MainWindowShell_Dispose_StopsFollowingServices`.

**MainWindowShell_Exit_RaisesExitRequested**: Exit raises one request. This scenario is tested by
`MainWindowShell_Exit_RaisesExitRequested`.

**MainWindowShell_ImageCommands_FollowExporterAndInvokeIt**: The Image commands are disabled, become enabled when
the exporter reports an image, announce the change, and call the exporter with the dialog service. This scenario
is tested by `MainWindowShell_ImageCommands_FollowExporterAndInvokeIt`.

**NullImageExporter_Default_IsDisabledAndDoesNothing**: The default exporter cannot export and both operations
complete without effect. This scenario is tested by `NullImageExporter_Default_IsDisabledAndDoesNothing`.

**MainWindowShell_OpenSettings_ShowsDialogWithPagesAndNotifiesClose**: Settings shows a dialog over the supplied
page and the page is told the dialog closed. This scenario is tested by
`MainWindowShell_OpenSettings_ShowsDialogWithPagesAndNotifiesClose`.

**MainWindowShell_ShowAbout_ShowsAboutDialog**: About shows the About dialog. This scenario is tested by
`MainWindowShell_ShowAbout_ShowsAboutDialog`.

**MainWindowShell_CommandsWithoutDialogService_DoNothing**: Before a dialog service is attached, every command
that needs the user does nothing. This scenario is tested by
`MainWindowShell_CommandsWithoutDialogService_DoNothing`.

**MessageDialogViewModel_Message_OkAcceptsAndRequestsClose**: A message offers only OK, which accepts and requests
a close. This scenario is tested by `MessageDialogViewModel_Message_OkAcceptsAndRequestsClose`.

**MessageDialogViewModel_Confirmation_CancelRequestsCloseWithoutAcceptance**: Cancel closes a confirmation without
accepting. This scenario is tested by `MessageDialogViewModel_Confirmation_CancelRequestsCloseWithoutAcceptance`.

**MessageDialogViewModel_Prompt_BlankEntry_CannotBeAccepted**: A blank prompt entry disables OK and the command
announces the change. This scenario is tested by `MessageDialogViewModel_Prompt_BlankEntry_CannotBeAccepted`.

**MessageDialogViewModel_Constructor_InvalidArguments_Throw**: Missing texts and a blank label are refused. This
scenario is tested by `MessageDialogViewModel_Constructor_InvalidArguments_Throw`.

**MainWindowShell_UndoRedo_FocusedTarget_DelegatesAndFollowsTargetState**: With a target that can undo but not redo,
the Undo command is usable and Redo is not; running Undo reaches the target. When the target changes to one that can
redo and not undo and announces it, the commands follow and announce their change, and Redo reaches the target. This
scenario is tested by `MainWindowShell_UndoRedo_FocusedTarget_DelegatesAndFollowsTargetState`.

**MainWindowShell_UndoRedo_NoTarget_DisabledAndReplacedTargetIgnored**: Without a target, or after the target was
removed, neither command is usable and running them reaches no one; a notification from the removed target does
not re-evaluate the commands. This scenario is tested by
`MainWindowShell_UndoRedo_NoTarget_DisabledAndReplacedTargetIgnored`.

**MainWindowShell_PushToTalk_BeginEnd_DelegatesToTarget**: The talk key is F8. A press starts the target, repeats
of the key start nothing more, a release stops it once, and a second release is ignored. This scenario is tested by
`MainWindowShell_PushToTalk_BeginEnd_DelegatesToTarget`.

**MainWindowShell_PushToTalk_NoTargetOrTargetReplaced_IsIgnoredOrStopsStartedTarget**: A press without a target starts
nothing; when the target is replaced while the key is down, the release stops the target that was started and does
not touch the new one. This scenario is tested by
`MainWindowShell_PushToTalk_NoTargetOrTargetReplaced_IsIgnoredOrStopsStartedTarget`.

**MainWindowView_Show_OpensWithShellTitle**: The window opens headless and shows the shell title. This scenario is
tested by `MainWindowView_Show_OpensWithShellTitle`.

**MainWindowView_Menus_HaveExpectedStructureAndAutomationIds**: The five menus and their items are in the
expected order, each has a matching name and automation identifier, and the Undo and Redo items show Ctrl+Z and
Ctrl+Y as text. This scenario is tested by
`MainWindowView_Menus_HaveExpectedStructureAndAutomationIds`.

**MainWindowView_Menus_NoProject_EnabledStateFollowsCommands**: With no project the New, Open, Exit, Settings, and About
items are usable and the Recent, Save, Close, Undo, Redo, Export, and Copy items are not. This scenario is tested by
`MainWindowView_Menus_NoProject_EnabledStateFollowsCommands`.

**MainWindowView_EditMenu_TypingInComposerEnablesUndoAndExplorerFocusDisables**: With a project open, Undo is not
usable; after typing in the message editor the shell's edit target is the composer and Undo is usable and Redo is
not; the Undo item undoes the typing and then Redo is usable; the Redo item restores it; when the explorer list
has the focus the edit target is empty and neither item is usable. This scenario is tested by
`MainWindowView_EditMenu_TypingInComposerEnablesUndoAndExplorerFocusDisables`.

**MainWindowView_PushToTalkKey_DownAndUp_DrivesTarget**: Pressing F8 twice (a repeat) starts the target once; another
key does nothing; releasing F8 stops it once and a second release is ignored. This scenario is tested by
`MainWindowView_PushToTalkKey_DownAndUp_DrivesTarget`.

**MainWindowView_PushToTalkKey_NoTarget_IsIgnored**: Without a target F8 starts nothing. This scenario is tested by
`MainWindowView_PushToTalkKey_NoTarget_IsIgnored`.

**MainWindowView_PushToTalkKey_WindowClosedWhileHeld_StopsTarget**: Closing the window while F8 is held stops the
target. This scenario is tested by `MainWindowView_PushToTalkKey_WindowClosedWhileHeld_StopsTarget`.

**MainWindowView_PushToTalkKey_WindowDeactivatedWhileHeld_StopsTarget**: When the platform reports that the window
was deactivated while F8 is held, the target is stopped at once and the release that follows changes nothing. The
headless platform never deactivates a window, so the test calls the platform's deactivation callback by reflection. This
scenario is tested by `MainWindowView_PushToTalkKey_WindowDeactivatedWhileHeld_StopsTarget`.

**MainWindowView_Show_ChatPanelLaunchesInspectorThroughWindow**: The window of a composed application is the launcher
of the payload inspector of the chat panel. This scenario is tested by
`MainWindowView_Show_ChatPanelLaunchesInspectorThroughWindow`.

**MainWindowView_ExitMenuItem_ClosesWindow**: The command behind Exit closes the window. This scenario is tested
by `MainWindowView_ExitMenuItem_ClosesWindow`.

**MainWindowView_AboutMenuItem_OpensAboutDialogAndCloseButtonCloses**: The About command opens the dialog with
the identity texts and five libraries, and clicking Close ends it. This scenario is tested by
`MainWindowView_AboutMenuItem_OpensAboutDialogAndCloseButtonCloses`.

**MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses**: The Settings command opens the dialog with
one page and clicking Cancel closes it. This scenario is tested by
`MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses`.

**MainWindowView_NewProjectMenuItem_CancelThenOk_CreatesProjectAndUpdatesTitle**: Cancel in the dialog creates
nothing; typing a name and choosing OK creates the project, which titles the window, shows the placeholder, and
enables Save and the Recent Projects menu. This scenario is tested by
`MainWindowView_NewProjectMenuItem_CancelThenOk_CreatesProjectAndUpdatesTitle`.

**MainWindowView_RecentProject_Open_ListsFilesAndCloseProjectResetsWindow**: A remembered project opens from the
menu entry and the window lists only its image files; Close Project restores the empty state. This scenario is
tested by `MainWindowView_RecentProject_Open_ListsFilesAndCloseProjectResetsWindow`.

**MainWindowView_RecentProjectsSubmenu_ItemShowsNameAndOpensProject**: In the open submenu the item shows the
project name with the underscore doubled and its command opens the project. This scenario is tested by
`MainWindowView_RecentProjectsSubmenu_ItemShowsNameAndOpensProject`.

**MainWindowView_ImageMenu_ExporterEnables_ItemsEnabledAndRunExporter**: The Image items are unusable until the
exporter reports an image, then usable, and each reaches the exporter. This scenario is tested by
`MainWindowView_ImageMenu_ExporterEnables_ItemsEnabledAndRunExporter`.

**MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu**: With the composed application, selecting an SVG in the
explorer shows its bitmap in the preview pane inside the workspace and makes the Image items usable; closing the
project makes them unusable again. This scenario is tested by
`MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu`.

**MainWindowView_CopyImageToClipboard_Picture_ReportsResultAndIsReadable**: The window puts a picture on its
clipboard as a bitmap, answers whether that worked, and the clipboard returns a bitmap of the same size. This
scenario is tested by `MainWindowView_CopyImageToClipboard_Picture_ReportsResultAndIsReadable`.

**MainWindowView_ShowMessage_OkButtonClosesDialog**: A message dialog shows its text without a text entry or
Cancel, and OK closes it. This scenario is tested by `MainWindowView_ShowMessage_OkButtonClosesDialog`.

**MainWindowView_Confirm_ConfirmAndCancelButtons_ReturnAnswer**: Cancel answers false and the confirm button,
labeled as asked, answers true. This scenario is tested by
`MainWindowView_Confirm_ConfirmAndCancelButtons_ReturnAnswer`.

**MainWindowView_Prompt_TypedTextAndCancel_ReturnTrimmedTextOrNull**: A prompt starts with the given text, returns
the trimmed text typed, and returns null when cancelled. This scenario is tested by
`MainWindowView_Prompt_TypedTextAndCancel_ReturnTrimmedTextOrNull`.

**MainWindowView_Pickers_HeadlessPlatform_ReturnNull**: The folder, project file, and save pickers answer null
on the headless platform. This scenario is tested by `MainWindowView_Pickers_HeadlessPlatform_ReturnNull`.

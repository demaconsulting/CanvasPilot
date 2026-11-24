### MainWindowShell

#### Verification Approach

`MainWindowShell` is verified at two levels. Unit tests in `MainWindowShellTests.cs` drive the shell view model over a
real project service, recent projects list, and settings service on temporary folders, with a hand-written
`FakeDialogService` that plays the user and a hand-written `FakeImageExporter`; two tests use an NSubstitute
`IProjectService` to force a failed creation and a failed save. Unit tests in `MessageDialogTests.cs` drive the message
dialog view model. Headless UI tests (the `UiTests` project, using Avalonia's headless platform) show the real
`MainWindowView`, find controls by name and automation identifier, run the commands behind the menu items, and exercise
the real message, About, Settings, and New Project dialogs by clicking their buttons: `MainWindowViewTests.cs` for the
window, its title bar, menus, dialogs, and body; `WorkspaceLayoutViewTests.cs` for the saved size of the window and the
widths of the columns; `AccessibilityViewTests.cs` for the access keys of the menu and the names, tooltips, focus, and
sizes of the controls of the window in both theme variants; and `StylePickerViewTests.cs` for the style list in the
title bar. `MainWindowTitleBarLayoutTests.cs` verifies the window buttons and the room kept for them: because the
headless platform draws no window frame, the tests put the real overlay of the drawn decorations (built from the
template of the theme, with the three buttons that the window asks for) over the title bar of the real window, where a
window on Windows has it, and measure the result; the same file holds the tests that the window has no full-screen mode.
The folder that Help ▸ Open Log Folder shows is tested with a hand-written `FakeFolderLauncher` (in
`AppShellSubsystemFakes.cs`) that records the folders it is asked for and can be made to fail, and, in the UI tests,
with a recording launcher given to the composed application; `SystemFolderLauncher` itself starts Explorer, `open`, or
`xdg-open`, which no automated test does, so it is checked manually. The platform pickers cannot be shown headless; the
UI test confirms they answer as canceled, and the real pickers are checked manually before a release. Likewise the real
keyboard (the repeat of a held F9, the window losing the active state when the user switches to another program, and a
screen reader's use of the Edit menu), the buttons of the system over the title bar on a real window (their room is
tested with the overlay of the theme and as a pure function, but the headless platform never extends the window into its
frame; see the manual check below), dragging the empty strip of the title bar, and the move of the window between
monitors are checked manually on a desktop before a release; the headless tests send key events and call the platform's
deactivation callback.

**Manual check of the title bar.** Before a release, on a Windows desktop the application is started with the window at
a normal size and maximized, at 100 %, 125 %, and 150 % display scaling, with a project open so that the provider pill
shows: the title bar shows the three buttons minimize, maximize or restore, and close and no full-screen button, the
right edge of the provider pill is clear of the left edge of the minimize button with a normal space, and no part of the
pill or its gear lies under a button. Pressing F11 does not enter full screen. Opening Help
▸ Open Log Folder shows the folder of the log in Explorer (checked once on each platform that is released), and the
folder is shown also when it did not exist before.

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
- The talk key is F9 until the user chooses another key or chord on the Speech page, which takes effect at once. The key
  starts the push-to-talk target once while it is held (repeats are ignored); the old key, and the key of a chord
  pressed without its modifiers, do nothing; the target is stopped when the key, or any key of the chord, goes up, the
  window closes, or the window is deactivated, and nothing happens without a target.
- Help ▸ Open Log Folder is usable with no project open and asks the folder launcher to show the folder that the logger
  writes to (the default log folder when there is no logger); when the launcher cannot show it, the user is told the
  reason and the path in a message; the item is disabled in a shell that has no launcher, and running it then does
  nothing.
- The title is the application name, or the project name followed by the application name, and follows a project
  opened, closed, or created through the service, and stops following after disposal.
- Accepting the New Project dialog creates, shows, and remembers the project; canceling creates nothing; a failed
  creation, a damaged file, and a failed save are shown to the user.
- Recent projects are listed newest first, open when chosen, show underscores as typed, and a missing project is
  reported and removed.
- Exit closes the window; Save and Close follow the open project.
- Settings opens over its pages and tells them when it closed; About opens the About dialog.
- Drawing Knowledge, in the Help menu above About, asks the dialog service to show a browser over the catalog that the
  shell was given, reading the catalog only when it is first used; it is unusable when the shell has no catalog and does
  nothing without a dialog service; a catalog that cannot be read is logged and shown to the user and no browser is
  shown; the browser is a window owned by the main window that does not block it, a second request brings the open one
  to the front, a request after it was closed opens a new one, and closing the main window closes it.
- The Image commands are disabled until the exporter reports an image, follow it, and run it with the dialog
  service.
- Message, confirmation, and prompt dialogs return the user's answer, trim a prompt's text, and cannot accept a
  blank prompt.
- A shell that has a style picker shows it, as a pill in the title bar, while a project is open and not otherwise; a
  project that was created, and no project that was only opened, is given the default style kit of the user.
- The Tools menu has Style Kits above Settings, usable when the application has a window that manages style kits;
  running it makes the view model of the window from the dialog service each time and hands it to the dialog service,
  and does nothing without a window or a dialog service. The real main window shows that window as a window that it owns
  and that does not block it, one at a time (a second request brings the open one to the front), closes it when the main
  window closes, and shows the editor of a kit modally over that window or over itself.
- Check Against Style, the last item of the Image menu, is usable only while the application can check and a picture is
  shown, runs the check, tells the user in a message why when nothing was checked, and is unusable and does nothing in a
  shell that has no check.
- The window opens 1440 by 900 with a least size of 960 by 600, has a title bar 44 high over its whole width that holds
  the menu, shows the project name in bold followed by the application name (the application name alone with no project)
  while the system title stays `{project} - CanvasPilot`, keeps 140 pixels free at the right end on Windows (the width
  of three window buttons) until the buttons are laid out and then the width that they measure, and 78 at the left end
  on macOS, only when the window extends into its frame, and shows the provider pill inside the right edge of the title
  bar, clear of the first window button at every width of the window and in the maximized state.
- The window asks for the minimize, maximize, and close buttons and not for the full-screen button, binds no key (F11)
  and no menu item to full screen, and turns a window that is put into full screen back to the normal state, whatever
  state it was in before.
- The status bar is at the bottom at all times, 26 high, with and without a project.
- With no project the welcome screen is shown and the workspace has not been built; once a project is open the workspace
  is shown, and after it is closed the welcome screen is shown again.
- Closing the window saves its size and state, a window made later over the same settings opens with them, the saved
  widths of the explorer and the chat column are used, and a width chosen with a splitter is saved.
- Every menu and every item of a menu has an access key that is unique among its neighbors.
- In both theme variants every button of the main window that shows no text has a name and a tooltip and is at least 28
  by 28, every enabled button can take the keyboard focus, and every field, list, and drop-down has a name.

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

**MainWindowShell_ShowDrawingKnowledge_ShowsBrowserOnTheCatalog**: A shell that was given a catalog does not ask for it
when it is made; running Drawing Knowledge asks once and gives the dialog service a browser whose tree is the roots of
the catalog, and no message is shown. This scenario is tested by
`MainWindowShell_ShowDrawingKnowledge_ShowsBrowserOnTheCatalog`.

**MainWindowShell_ShowDrawingKnowledge_WithoutKnowledgeOrDialogService_DoesNothing**: A shell with no catalog cannot run
the command, and a shell with a catalog but no dialog service runs it without asking for the catalog or showing
anything. This scenario is tested by `MainWindowShell_ShowDrawingKnowledge_WithoutKnowledgeOrDialogService_DoesNothing`.

**MainWindowShell_ShowDrawingKnowledge_CatalogCannotBeLoaded_LogsAndShowsMessage**: A catalog that throws is written to
the log, and the user is told in a message titled Drawing Knowledge that names the problem; no browser is shown. This
scenario is tested by `MainWindowShell_ShowDrawingKnowledge_CatalogCannotBeLoaded_LogsAndShowsMessage`.

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

**MainWindowShell_PushToTalk_BeginEnd_DelegatesToTarget**: The talk key is F9 by default (its text, key, and modifiers
are read from the shell's `TalkKey`). A press starts the target, repeats of the key start nothing more, a release stops
it once, and a second release is ignored. This scenario is tested by
`MainWindowShell_PushToTalk_BeginEnd_DelegatesToTarget`.

**MainWindowShell_PushToTalk_NoTargetOrTargetReplaced_IsIgnoredOrStopsStartedTarget**: A press without a target starts
nothing; when the target is replaced while the key is down, the release stops the target that was started and does
not touch the new one. This scenario is tested by
`MainWindowShell_PushToTalk_NoTargetOrTargetReplaced_IsIgnoredOrStopsStartedTarget`.

**MainWindowShell_OpenLogFolder_ShowsTheFolderOfTheLogger**: With a logger on a temporary folder and a fake launcher,
the command is usable with no project open, and running it asks the launcher once to show the folder that the logger
writes to, and shows no message. This scenario is tested by `MainWindowShell_OpenLogFolder_ShowsTheFolderOfTheLogger`.

**MainWindowShell_OpenLogFolder_NoLoggerOrLauncherFails_UsesTheDefaultFolderAndTellsTheUser**: A shell without a logger
asks the launcher to show the default log folder; a launcher that fails with "No file manager." leads to one message
titled `Open Log Folder` that contains the reason and the path of the folder. This scenario is tested by
`MainWindowShell_OpenLogFolder_NoLoggerOrLauncherFails_UsesTheDefaultFolderAndTellsTheUser`.

**MainWindowShell_OpenLogFolder_WithoutLauncher_IsDisabledAndDoesNothing**: A shell that has no launcher cannot run the
command, and running it anyway shows no message. This scenario is tested by
`MainWindowShell_OpenLogFolder_WithoutLauncher_IsDisabledAndDoesNothing`.

**MainWindowView_Show_OpensWithShellTitle**: The window opens headless and shows the shell title. This scenario is
tested by `MainWindowView_Show_OpensWithShellTitle`.

**MainWindowView_Menus_HaveExpectedStructureAndAutomationIds**: The five menus and their items are in the expected order
(the Help menu has Drawing Knowledge, Open Log Folder, a separator, and About, and the Open Log Folder item has the
header `Open _Log Folder`), each has a matching name and automation identifier, and the Undo and Redo items show Ctrl+Z
and Ctrl+Y as text (Cmd+Z and Cmd+Shift+Z on macOS). This scenario is tested by
`MainWindowView_Menus_HaveExpectedStructureAndAutomationIds`.

**MainWindowView_Menus_NoProject_EnabledStateFollowsCommands**: With no project the New, Open, Exit, Settings, Drawing
Knowledge, Open Log Folder, and About
items are usable and the Recent, Save, Close, Undo, Redo, Export, and Copy items are not. This scenario is tested by
`MainWindowView_Menus_NoProject_EnabledStateFollowsCommands`.

**MainWindowView_OpenLogFolderMenuItem_ShowsTheFolderOfTheLog**: In the real window composed over a recording launcher,
with no project open, the Open Log Folder item is usable and its command asks the launcher once to show the `logs`
folder of the application data of the test. This scenario is tested by
`MainWindowView_OpenLogFolderMenuItem_ShowsTheFolderOfTheLog`.

**MainWindowView_EditMenu_TypingInComposerEnablesUndoAndExplorerFocusDisables**: With a project open, Undo is not
usable; after typing in the message editor the shell's edit target is the composer and Undo is usable and Redo is
not; the Undo item undoes the typing and then Redo is usable; the Redo item restores it; when the explorer list
has the focus the edit target is empty and neither item is usable. This scenario is tested by
`MainWindowView_EditMenu_TypingInComposerEnablesUndoAndExplorerFocusDisables`.

**MainWindowView_PushToTalkKey_DownAndUp_DrivesTarget**: Pressing F9 twice (a repeat) starts the target once; another
key does nothing; releasing F9 stops it once and a second release is ignored. This scenario is tested by
`MainWindowView_PushToTalkKey_DownAndUp_DrivesTarget`.

**MainWindowView_PushToTalkKey_ChosenChord_StartsOnTheChordAndStopsWhenAnyKeyOfItIsReleased**: After the key
`Ctrl+Shift+Space` is saved in the settings, the old key F9 does nothing, Space alone does nothing, the chord starts the
target once however it repeats, letting go of Ctrl stops it once, and the release of Space that follows changes nothing.
This shows both that the chord comes from the settings at once and that the release of any key of the chord ends the
talking. This scenario is tested by
`MainWindowView_PushToTalkKey_ChosenChord_StartsOnTheChordAndStopsWhenAnyKeyOfItIsReleased`.

**MainWindowView_PushToTalkKey_NoTarget_IsIgnored**: Without a target F9 starts nothing. This scenario is tested by
`MainWindowView_PushToTalkKey_NoTarget_IsIgnored`.

**MainWindowView_PushToTalkKey_WindowClosedWhileHeld_StopsTarget**: Closing the window while F9 is held stops the
target. This scenario is tested by `MainWindowView_PushToTalkKey_WindowClosedWhileHeld_StopsTarget`.

**MainWindowView_PushToTalkKey_WindowDeactivatedWhileHeld_StopsTarget**: When the platform reports that the window
was deactivated while F9 is held, the target is stopped at once and the release that follows changes nothing. The
headless platform never deactivates a window, so the test calls the platform's deactivation callback by reflection. This
scenario is tested by `MainWindowView_PushToTalkKey_WindowDeactivatedWhileHeld_StopsTarget`.

**MainWindowView_Show_ChatPanelLaunchesInspectorThroughWindow**: The window of a composed application is the launcher
of the payload inspector of the chat panel. This scenario is tested by
`MainWindowView_Show_ChatPanelLaunchesInspectorThroughWindow`.

**MainWindowView_ExitMenuItem_ClosesWindow**: The command behind Exit closes the window. This scenario is tested
by `MainWindowView_ExitMenuItem_ClosesWindow`.

**MainWindowView_AboutMenuItem_OpensAboutDialogAndCloseButtonCloses**: The About command opens the dialog with
the identity texts and six libraries, and clicking Close ends it. This scenario is tested by
`MainWindowView_AboutMenuItem_OpensAboutDialogAndCloseButtonCloses`.

**MainWindowView_DrawingKnowledgeMenuItem_ShowsBrowserWindowThatDoesNotBlock**: The Drawing Knowledge command of the
real window finishes while a browser window that the main window owns is open, with the roots of the catalog of the
application in its tree, and the main window stays enabled. This scenario is tested by
`MainWindowView_DrawingKnowledgeMenuItem_ShowsBrowserWindowThatDoesNotBlock`.

**MainWindowView_DrawingKnowledgeMenuItem_SecondRequest_ReusesTheOpenBrowser**: Asking again while the browser is open
brings the same window, with the card that was chosen, to the front; after it is closed, asking opens a new one with
nothing chosen. This scenario is tested by `MainWindowView_DrawingKnowledgeMenuItem_SecondRequest_ReusesTheOpenBrowser`.

**MainWindowView_Close_ClosesTheBrowserItOwns**: Closing the main window closes the browser. This scenario is tested by
`MainWindowView_Close_ClosesTheBrowserItOwns`.

**MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses**: The Settings command opens the dialog with
one page and clicking Cancel closes it. This scenario is tested by
`MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses`.

**MainWindowView_NewProjectMenuItem_CancelThenOk_CreatesProjectAndUpdatesTitle**: Cancel in the dialog creates
nothing; typing a name and choosing OK creates the project, which titles the window, shows the placeholder, and
enables Save and the Recent Projects menu. This scenario is tested by
`MainWindowView_NewProjectMenuItem_CancelThenOk_CreatesProjectAndUpdatesTitle`.

**MainWindowView_RecentProject_Open_ListsFilesAndCloseProjectResetsWindow**: A remembered project opens from the
menu entry and the window lists only its image files as the nodes of the tree; Close Project restores the empty state:
the title, an empty list, the placeholder text, and the welcome screen shown again. This scenario is
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

**MainWindowShell_NewProject_WithStylePicker_AppliesTheDefaultOnlyForANewProject**: With a style picker that counts the
requests to give the default kit, creating a project asks once, closing it and opening it again from the recent projects
does not ask again, and the picker is off with no project and on with one. This scenario
is tested by `MainWindowShell_NewProject_WithStylePicker_AppliesTheDefaultOnlyForANewProject`.

**MainWindowShell_NewProject_WithoutStylePicker_HasNoPicker**: A shell with no style picker creates a project as it
always did and has no picker. This scenario is tested by
`MainWindowShell_NewProject_WithoutStylePicker_HasNoPicker`.

**MainWindowShell_CheckAgainstStyle_FollowsThePictureRunsTheCheckAndReportsWhyNot**: With a check that first has no
picture, the command is unusable; when the check says that a picture appeared the command is usable and announces it;
running it asks the check, which shows its own notes, and shows no message; a check that answers with a sentence has the
sentence shown in a message titled `Check Against Style`. This scenario is tested by
`MainWindowShell_CheckAgainstStyle_FollowsThePictureRunsTheCheckAndReportsWhyNot`.

**MainWindowShell_CheckAgainstStyle_NothingToCheckWith_IsUnavailableAndDoesNothing**: A shell with no check leaves the
command unusable, and running it does nothing and shows no message. This scenario is tested by
`MainWindowShell_CheckAgainstStyle_NothingToCheckWith_IsUnavailableAndDoesNothing`.

**MainWindowView_CheckAgainstStyleMenuItem_PictureShown_EnablesAndShowsNotesInThePreview**: In the real window composed
with the real units, the item is unusable with no picture and usable when a picture is shown; running it against the
kit DEMA, with a blue picture that the kit does not have, lists a `style-summary` note that says FAILED and a
`style-off-palette` note in the open list of notes of the preview, and closing the project makes the item unusable
again. This scenario is tested by
`MainWindowView_CheckAgainstStyleMenuItem_PictureShown_EnablesAndShowsNotesInThePreview`.

**StylePickerView_TitleBar_ShownOnlyWithAProjectAndListsTheKits**: In the real window composed with the real units, the
list in the title bar is hidden with no project and shown with one, as a pill named `Style kit` that holds a list with
no style and the three kits that come with the program. This scenario is tested by
`StylePickerView_TitleBar_ShownOnlyWithAProjectAndListsTheKits`.

**MainWindowShell_ShowStyleKits_MakesTheViewModelFromTheDialogsAndShowsIt**: With a function that makes the window of a
real catalog, running Style Kits twice calls the function twice with the dialog service, and the dialog service shows
two different view models. This scenario is tested by
`MainWindowShell_ShowStyleKits_MakesTheViewModelFromTheDialogsAndShowsIt`.

**MainWindowShell_ShowStyleKits_WithoutWindowOrDialogService_DoesNothing**: A shell with no window cannot run the
command, and a shell with a window and no dialog service does not call the function. This scenario is tested by
`MainWindowShell_ShowStyleKits_WithoutWindowOrDialogService_DoesNothing`.

**MainWindowView_StyleKitsMenuItem_ShowsWindowThatDoesNotBlock**: In the real window composed with the real units, the
Style Kits item opens a window titled Style Kits that the main window owns, with a card for each of the three kits that
come with the program, and the main window stays usable. This scenario is tested by
`MainWindowView_StyleKitsMenuItem_ShowsWindowThatDoesNotBlock`.

**MainWindowView_StyleKitsMenuItem_SecondRequest_ReusesTheOpenWindow**: A second request brings the same window to the
front with the kit that was chosen still chosen; after it was closed a request opens a new window with nothing chosen.
This scenario is tested by `MainWindowView_StyleKitsMenuItem_SecondRequest_ReusesTheOpenWindow`.

**MainWindowView_ShowStyleKitEditor_OverTheManagerOrTheMainWindow_ShowsEditorModally**: Asking the main window for the
editor of a kit shows it over the main window when the window that manages kits is not open and over that window when it
is, and the Cancel button of the editor completes the request. This scenario is tested by
`MainWindowView_ShowStyleKitEditor_OverTheManagerOrTheMainWindow_ShowsEditorModally`.

**MainWindowView_Close_ClosesTheStyleKitsWindowItOwns**: Closing the main window closes the window that manages kits.
This scenario is tested by `MainWindowView_Close_ClosesTheStyleKitsWindowItOwns`.

**MainWindowView_Show_HasTitleBarWithMenuAndTheDesignSizes**: The shown window is 1440 by 900 with a least size of 960
by 600; the title bar is 44 high and as wide as the window and holds the menu; both rooms kept free for the buttons of
the system are 0 wide, because the headless window is not extended into its frame. This scenario is tested by
`MainWindowView_Show_HasTitleBarWithMenuAndTheDesignSizes`.

**MainWindowView_CaptionReserve_FollowsPlatformAndExtension**: The room kept free is 0 and 0 when the window is not
extended on Windows or on macOS, 140 on the right on Windows (three buttons of 45 with the space between them and the
frame line), 78 on the left on macOS, and none on another platform.
This scenario is tested by `MainWindowView_CaptionReserve_FollowsPlatformAndExtension`, a theory of five cases.

**MainWindowView_TitleBarDecorations_AskForNoFullScreenButton**: The composed window asks, through
`WindowDrawnDecorations.TitleBarDecorations`, for the minimize, maximize, and close buttons, and not for the full-screen
button. This scenario is tested by `MainWindowView_TitleBarDecorations_AskForNoFullScreenButton`.

**MainWindowView_WindowState_FullScreenIsTurnedBackToNormal**: A shown window that is put into full screen is in the
normal state at once, one that is maximized stays maximized, and one that is put into full screen from the maximized
state ends in the normal state. This scenario is tested by `MainWindowView_WindowState_FullScreenIsTurnedBackToNormal`.

**MainWindowView_KeyF11_DoesNotEnterFullScreen**: Pressing F11 in a shown window does not put it into full screen, and
no menu item of the window has F11 as its input gesture. This scenario is tested by
`MainWindowView_KeyF11_DoesNotEnterFullScreen`.

**MainWindowView_TitleBar_PillEndsClearOfTheWindowButtons**: With the real overlay of the drawn decorations over the
title bar of the real window, three buttons are visible and the room that the title bar keeps (after
`UpdateCaptionReserve` with the facts of Windows) equals the distance from the left edge of the buttons to the right
edge of the window, between 139 and the constant 140, and the provider pill ends at least 12 pixels before the first
button and inside the window, and is shown. This scenario is tested by
`MainWindowView_TitleBar_PillEndsClearOfTheWindowButtons`, a theory of four cases: widths 1920, 1280, and 960 in the
normal state, and 1920 maximized; each case also writes a picture of the window for a person to look at.

**MainWindowView_CaptionReserve_FallsBackToTheConstantUntilTheButtonsAreMeasured**: Without the overlay the measurement
answers nothing and the room that is kept is the constant, 140; once the overlay is laid out the measurement finds it
(although it is not below the window in the visual tree) and the room is the measured width, which differs from the
constant. This scenario is tested by `MainWindowView_CaptionReserve_FallsBackToTheConstantUntilTheButtonsAreMeasured`.

**MainWindowView_TitleBar_ShowsProjectNameThenApplicationName**: The title text is `CanvasPilot` with no project,
`Release Overview · CanvasPilot` while the project is open (the name in bold), `CanvasPilot` again after Close, and the
title of the system is `Release Overview - CanvasPilot` while the project is open. This scenario is tested by
`MainWindowView_TitleBar_ShowsProjectNameThenApplicationName`.

**MainWindowView_StatusBar_AlwaysShownAndReportsThePicture**: The status bar is visible and 26 high with no project and
says that no project is open; with a project and no picture it says that no picture is selected; for a drawn picture it
shows the file, size, format, and render time, and for a broken one it shows the failure. This scenario is tested by
`MainWindowView_StatusBar_AlwaysShownAndReportsThePicture`.

**MainWindowView_ProviderPill_InTheTitleBar_OpensTheSettingsOnTheProviderPage**: The provider pill is inside the title
bar and its right edge, 26 high, named with the provider and how it is; using it opens the settings on the AI Provider
page. This scenario is tested by `MainWindowView_ProviderPill_InTheTitleBar_OpensTheSettingsOnTheProviderPage`.

**MainWindowView_Workspace_ChatColumnVisibleOnlyWithProject_ThreeColumns**: With no project the workspace has not been
built (no chat host or splitter exists); once a project is open the chat panel is shown 400 wide to the right of the
preview, which is to the right of the explorer, 264 wide; after the project is closed the chat host is hidden. This
scenario is tested by `MainWindowView_Workspace_ChatColumnVisibleOnlyWithProject_ThreeColumns`.

**MainWindowShell_WindowLayout_RestoresWidthsAndSavesChoicesAndWindow**: A shell over settings that hold the widths 300
and 500 gives them to the workspace; a choice of 220 and 350 is saved at once; telling the shell a window of 1200 by 800
at 40, 50 that is maximized saves them with the widths. This scenario is tested by
`MainWindowShell_WindowLayout_RestoresWidthsAndSavesChoicesAndWindow`.

**MainWindowShell_AppearanceSettings_ComfortableRowsReachTheExplorer**: A shell over settings that ask for comfortable
rows gives them to the explorer; turning the setting off and on again in the settings changes the explorer to match.
This scenario is tested by `MainWindowShell_AppearanceSettings_ComfortableRowsReachTheExplorer`.

**WorkspaceLayout_CloseWindow_SavesTheSizeAndANewWindowOpensWithIt**: Closing a window that the user made 1200 by 700
saves that size in the normal state, and a window made later over the same shell opens with that size and state. This
scenario is tested by `WorkspaceLayout_CloseWindow_SavesTheSizeAndANewWindowOpensWithIt`.

**WorkspaceLayout_Splitters_SavedWidthsUsedAndKeyboardChoiceSaved**: With saved widths of 300 and 500 the columns open
with them, and one key on each splitter moves it by 16 and is saved in the settings. This scenario is tested by
`WorkspaceLayout_Splitters_SavedWidthsUsedAndKeyboardChoiceSaved`.

**Accessibility_MainMenu_EveryEntryHasAUniqueAccessKey**: The five menus have five different access keys, and every item
of each menu has one that no other item of the menu has. This scenario is tested by
`Accessibility_MainMenu_EveryEntryHasAUniqueAccessKey`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: The welcome screen and the workspace of an open
project, the About dialog, the knowledge browser, the New Project dialog, the Style Kits window, and every page of the
settings are walked in the dark and the light theme; a button that shows no text has a name and a tooltip, is at least
28 by 28, and every enabled button takes the focus; every field, list, drop-down, slider, and tree has a name; and more
than 150 controls were looked at. This scenario is tested by
`Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.

## AppShell

### Verification Approach

The `AppShell` subsystem is verified by integration tests defined in `AppShellSubsystemTests.cs`. The tests build
the application with the real `ApplicationComposition` over a real `ProjectService`, `RecentProjects`,
`SettingsService`, `RollingFileLogger`, and `ProjectExplorerViewModel` on temporary folders, and drive the real
`MainWindowShell` and `ProjectWorkspaceViewModel` commands. The only substitutes are a hand-written
`FakeDialogService`, which plays the user at each dialog because a real window and platform pickers are not part
of these tests, and `FakeRefreshScheduler`, so no test waits for a delay. The views themselves are verified by the
headless UI tests described in the unit verification documents.

### Test Environment

N/A - standard test environment. Tests use temporary local folders and real file I/O; no network, external
services, or display are required.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Composing the application twice over the same folders shows the first run's stored settings and remembered
  project in the second run, and the log records each start and stop.
- A project created through the shell is titled in the window, listed in the explorer, saved, closed, and reopened
  from the Recent Projects menu with its files.
- Renaming and deleting the selected file through the workspace changes the project folder and the explorer list.
- Choices made in the settings dialog opened from the shell are stored and in effect in the next run, and the
  dialog hosts the AI Provider page followed by the Speech page.
- The About dialog opened from the composed shell shows the application name, a version, and the five libraries.
- The Drawing Knowledge command of the composed shell gives the dialog service a browser over the catalog of the
  application, whose tree is the roots of the catalog.
- The composer of the message created by the composition works in the folder of the project that is open, and has no
  project and no draft once the project is closed.
- The layer for marks is the overlay of the preview pane with its tool bar in the pane's top bar, and it can make marks
  only while a project is open.
- The chat panel of the composition works with the composition's composer, can be used only while a project is open,
  keeps the message that was sent in its transcript, and has an empty transcript once the project is closed.
- The chat panel is the chat content of the workspace (shown only while a project is open), the push-to-talk target
  of the shell, and has the window's dialog service as its launcher of the payload inspector.
- The composition has the catalog of style kits with the kits that come with the program, the picker of the style, the
  two style tool descriptors, and an exporter that asks for sizes; a new project of the composed shell has the default
  kit of the user as its style, and the status bar shows the picker while a project is open.
- Check Against Style on the composed shell checks the picture that is shown against the kit of the project, shows the
  result among the notes of the preview with the list open, tells the user when the project has no style, and the notes
  are gone when another picture is drawn.

### Test Scenarios

**AppShellSubsystem_ComposeApplication_ChatIsTheChatColumnAndTheTalkKeyTarget**: A composition starts with no
project, and the workspace shows no chat. After a project is created the chat is shown; the talk key of the shell
reaches the chat panel (with no speech model chosen the composer says why nothing started), and is no longer active
after it is released. After the project is closed the chat is not shown. The chat panel is the workspace's chat
content and the shell's push-to-talk target, and the attached dialog service is its inspector launcher. This
scenario is tested by `AppShellSubsystem_ComposeApplication_ChatIsTheChatColumnAndTheTalkKeyTarget`.

**AppShellSubsystem_ComposeApplication_ChatPanelFollowsTheOpenProject**: A composition starts with no project, and its
chat panel cannot be used. After a project is created the panel works with the composition's composer; a message typed
in the composer and sent through the panel's command is the one entry of its transcript. After the project is closed the
transcript is empty. This scenario is tested by `AppShellSubsystem_ComposeApplication_ChatPanelFollowsTheOpenProject`.

**AppShellSubsystem_ComposeApplication_AnnotationLayerIsOnThePictureAndFollowsTheOpenProject**: A composition starts
with no project, and the layer for marks does not allow marks. The layer is the overlay of the preview pane and its tool
bar is in the pane's top bar. After a project is created marks are allowed; after the project is closed they are not.
This scenario is tested by `AppShellSubsystem_ComposeApplication_AnnotationLayerIsOnThePictureAndFollowsTheOpenProject`.

**AppShellSubsystem_ComposeApplication_ComposerFollowsTheOpenProject**: A composition starts with no project, and its
composer has none. After a project is created the composer works in the project's folder and accepts typed text; after
the project is closed it has no project, no draft, no marks, and refuses typing. This scenario is tested by
`AppShellSubsystem_ComposeApplication_ComposerFollowsTheOpenProject`.

**AppShellSubsystem_ComposeApplication_LoadsSettingsListsRecentProjectsAndLogs**: A first run creates a project and
stores a model name, then ends. A second composition over the same folders has the stored model name in effect,
lists the project in the recent projects menu with no project open, and the log holds both the start and stop
entries. This scenario is tested by `AppShellSubsystem_ComposeApplication_LoadsSettingsListsRecentProjectsAndLogs`.

**AppShellSubsystem_ProjectLifecycle_NewSaveCloseReopenFromRecent**: The user creates a project through the New
Project dialog, an image is added and the explorer refreshed, the project is saved and closed, and the first
recent project is opened. The title and explorer follow each step and the reopened project has a modified time.
This scenario is tested by `AppShellSubsystem_ProjectLifecycle_NewSaveCloseReopenFromRecent`.

**AppShellSubsystem_ManageFiles_RenameAndDelete_UpdateFolderAndExplorer**: A project with two images has the first
selected; the user renames it and then deletes the renamed file after confirming. The folder and the list show
each change and no message was shown. This scenario is tested by
`AppShellSubsystem_ManageFiles_RenameAndDelete_UpdateFolderAndExplorer`.

**AppShellSubsystem_Settings_DialogOk_StoresChoicesForNextRun**: The user edits the AI provider page in the
settings dialog opened from the shell and chooses OK; a new composition has the model name in effect. This scenario
is tested by `AppShellSubsystem_Settings_DialogOk_StoresChoicesForNextRun`.

**AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages**: The Settings command of a composed
shell shows a dialog whose pages are titled AI Provider and Speech, in that order, and the second page is the
Speech page. This scenario is tested by `AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages`.

**AppShellSubsystem_About_ComposedShell_ShowsIdentityVersionAndLibraries**: The About command of a composed shell
shows a dialog with the application name, a non-blank version, and five libraries. This scenario is tested by
`AppShellSubsystem_About_ComposedShell_ShowsIdentityVersionAndLibraries`.

**AppShellSubsystem_DrawingKnowledge_ComposedShell_ShowsBrowserOnTheCatalogOfTheApplication**: The Drawing Knowledge
command of a composed shell is usable, gives the dialog service one browser whose tree is the roots of the catalog of
the application, and the browser can show a card of CanvasPilot. This scenario is tested by
`AppShellSubsystem_DrawingKnowledge_ComposedShell_ShowsBrowserOnTheCatalogOfTheApplication`.

**AppShellSubsystem_ComposeApplication_StyleKitsPickerDefaultToolsAndExporterAreWired**: A composition whose user chose
`clean` as the default kit finds the three kits that come with the program, shows no picker with no project, gives a
project created through the shell the kit `clean` as its style (the picker selects it and the status bar is shown),
describes the tools `style_get` and `style_check`, and, when the user exports the picture that is shown, asks which
size to export at. This scenario is tested by
`AppShellSubsystem_ComposeApplication_StyleKitsPickerDefaultToolsAndExporterAreWired`.

**AppShellSubsystem_CheckAgainstStyle_ComposedShell_ShowsNotesInThePreview**: A composed application with a project that
shows a picture of a lime green that the kit Clean does not have: the command is usable; with no style it shows a
message that says so and the notes stay empty; after the kit Clean is chosen it lists a `style-summary` note that says
FAILED and a `style-off-palette` note that names the lime green and opens the list; after another file is drawn the
notes of the style are gone. This scenario is tested by
`AppShellSubsystem_CheckAgainstStyle_ComposedShell_ShowsNotesInThePreview`.

**AppShellSubsystem_Create_NullArguments_Throw**: Composing with a missing logger, settings service, or scheduler
is refused. This scenario is tested by `AppShellSubsystem_Create_NullArguments_Throw`.

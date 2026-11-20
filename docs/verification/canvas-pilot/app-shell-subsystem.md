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
- The About dialog opened from the composed shell shows the application name, a version, and the four libraries.

### Test Scenarios

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
shows a dialog with the application name, a non-blank version, and four libraries. This scenario is tested by
`AppShellSubsystem_About_ComposedShell_ShowsIdentityVersionAndLibraries`.

**AppShellSubsystem_Create_NullArguments_Throw**: Composing with a missing logger, settings service, or scheduler
is refused. This scenario is tested by `AppShellSubsystem_Create_NullArguments_Throw`.

# CanvasPilot

## Verification Approach

System-level verification uses tests in `CanvasPilotTests.cs` that compose real units on temporary
directories without opening a window. Each test exercises the units together as the application does
and asserts on observable outcomes such as the files written to disk and the exceptions raised. The
tests use real file I/O and no mocks for the units themselves; where a user would act in a dialog, a hand-written
dialog service plays the user, and a hand-written refresh scheduler replaces timed refreshes so no test waits.

## Test Environment

System tests run under the standard .NET test runner on Windows, Linux, and macOS with the .NET 10
runtime. Temporary directories are created and removed within each test. No network access, external
services, or display is required.

## Acceptance Criteria

- All system tests pass with zero failures on every supported platform.
- Application events and failures logged by the application appear in a local log file within the log
  folder.
- A file location built from a name that attempts to leave its intended folder is rejected, while a
  name inside the folder is accepted.
- Settings saved by one run are restored by the next run, and a damaged settings file yields default
  settings, a kept-aside copy of the file, and a log entry rather than a failure.
- AI provider choices made in the settings dialog are stored and restored, and unrestricted agent tools
  remain off unless the user opts in.
- A project created in a folder is reopened from the recent projects list, and a damaged project file is
  refused with a message and a log entry without changing the open project.
- The project explorer lists the image source files of the open project and not other content.
- The application starts on every supported platform: composing its units without a window gives a titled shell
  with the right commands available, and the log records the start and stop.
- The main window shell creates a project, lists its image files, finds it in the recent projects after a
  restart, and keeps a changed setting across the restart.

## Test Scenarios

**CanvasPilot_OperationalLogging_ApplicationEvent_WritesLocalLog**: A log folder is resolved beneath a
per-user data root, an informational event and a failure with exception details are logged, and the log
folder is inspected; exactly one log file exists and it contains both entries including the exception
detail. This scenario is tested by `CanvasPilot_OperationalLogging_ApplicationEvent_WritesLocalLog`.

**CanvasPilot_SafeFileHandling_TraversalName_IsRejected**: A supplied name containing parent-directory
traversal is combined with a base folder and an `ArgumentException` is thrown; a name inside the folder
is combined successfully and the result stays under the base folder. This scenario is tested by
`CanvasPilot_SafeFileHandling_TraversalName_IsRejected`.

**CanvasPilot_PersistUserSettings_SaveAndReload_RestoresSettingsAndToleratesDamage**: Settings with a recent
project are saved by one service, loaded by a second, and restored; the settings file is then replaced by
invalid text and loaded by a third service, which yields default settings, leaves a `settings.json.bad` copy,
and logs the corruption. This scenario is tested by
`CanvasPilot_PersistUserSettings_SaveAndReload_RestoresSettingsAndToleratesDamage`.

**CanvasPilot_ConfigureAiProvider_DialogOk_StoresChoicesAndKeepsToolsOptIn**: The settings dialog is composed
over the AI provider page; Ollama, a model name, a context window, and an endpoint are chosen and OK is
selected; a new service loads those choices and unrestricted agent tools are still off. This scenario is
tested by `CanvasPilot_ConfigureAiProvider_DialogOk_StoresChoicesAndKeepsToolsOptIn`.

**CanvasPilot_ManageProjects_NewProject_CreatesReopensAndRemembersProject**: A project is created in a
documents folder, added to the recent projects list, and closed; a new settings service loads the list and the
first entry is opened. The project reopens with its name. A damaged project file is then opened and refused with
a failed result, the open project is unchanged, and the refusal is logged. This scenario is tested by
`CanvasPilot_ManageProjects_NewProject_CreatesReopensAndRemembersProject`.

**CanvasPilot_BrowseProjectImages_OpenProject_ListsImageSourceFiles**: A created project holds two SVG files and
a text file; the project explorer is opened on the project folder and lists the two SVG files in name order,
and selecting one reports its path. This scenario is tested by
`CanvasPilot_BrowseProjectImages_OpenProject_ListsImageSourceFiles`.

**CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell**: The application's units are composed exactly as
at startup, without a window, over temporary per-user folders; the shell is titled with the application name,
the commands that need nothing are available, those that need a project are not, the default settings are in
effect, and the log holds the start and stop entries. The platform requirements link this test to each platform
and runtime with source filters. This scenario is tested by
`CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell`.

**CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip**: The composed application's user
creates a project through the New Project dialog; two files are added, one an image; the explorer lists only the
image and the title names the project. The user changes the AI model name in the settings dialog and the
application is restarted: the project is in the recent projects menu and the model name is in effect. This
scenario is tested by `CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip`.

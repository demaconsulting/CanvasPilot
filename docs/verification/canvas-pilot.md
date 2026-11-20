# CanvasPilot

## Verification Approach

System-level verification uses tests in `CanvasPilotTests.cs` that compose real units on temporary
directories without opening a window. Each test exercises the units together as the application does
and asserts on observable outcomes such as the files written to disk and the exceptions raised. The
tests use real file I/O and no mocks for the units themselves; where a user would act in a dialog, a hand-written
dialog service plays the user, and a hand-written refresh scheduler replaces timed refreshes so no test waits.

The `AppShell` subsystem depends on the `Preview` subsystem and not the reverse (see *Dependency Direction* in the
design). Verification follows that direction: the `Preview` units are verified with a substitute for the
`IExportDialogs` interface that `Preview` owns, the shell is verified with a substitute image exporter and with the
disabled default exporter, and only the system tests and the user interface tests compose the real shell with the real
preview, which is also what proves that the two work together. The stored AI provider choices are verified through the
settings service and the settings dialog; the agent that will use them does not exist yet and has no verification.

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
- Speech devices, models, and a model setting chosen in the Speech page of the settings dialog are stored by device
  name and model identifier and restored by the next run, using substitute audio and model services.
- A project created in a folder is reopened from the recent projects list, and a damaged project file is
  refused with a message and a log entry without changing the open project.
- The project explorer lists the image source files of the open project and not other content.
- A selected image source file is rendered and shown with its size, a file that cannot be drawn shows an error with
  its code, and the shown picture can be exported as a valid PNG file.
- The shown picture of a diagram is identified by its file and by fingerprints of the file and the pixels, which
  change when the file is edited, and it locates the diagram's elements in picture pixels.
- The preview pane has a layer over the shown picture that holds the content placed in it, beside every picture.
- The application starts on every supported platform: composing its units without a window gives a titled shell
  with the right commands available, and the log records the start and stop.
- The Windows build produces exactly one MSI package of non-trivial size for the application, named for the build
  version; installing it is checked by hand before each release.
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

**CanvasPilot_ConfigureSpeech_DialogOk_StoresChoicesAndRestoresThemNextRun**: The application is composed with
substitute audio and model services that list two microphones, two speakers, a speech-to-text model, and a
text-to-speech model with a voice choice. The user opens Settings, chooses the second microphone and speaker, both
models, and the first voice on the Speech page, and chooses OK. After a restart the stored settings hold the device
names, the model identifiers, and the voice, and a Speech page loaded from them shows the microphone and the voice.
No hardware is used and nothing is downloaded. This scenario is tested by
`CanvasPilot_ConfigureSpeech_DialogOk_StoresChoicesAndRestoresThemNextRun`.

**CanvasPilot_ManageProjects_NewProject_CreatesReopensAndRemembersProject**: A project is created in a
documents folder, added to the recent projects list, and closed; a new settings service loads the list and the
first entry is opened. The project reopens with its name. A damaged project file is then opened and refused with
a failed result, the open project is unchanged, and the refusal is logged. This scenario is tested by
`CanvasPilot_ManageProjects_NewProject_CreatesReopensAndRemembersProject`.

**CanvasPilot_BrowseProjectImages_OpenProject_ListsImageSourceFiles**: A created project holds two SVG files and
a text file; the project explorer is opened on the project folder and lists the two SVG files in name order,
and selecting one reports its path. This scenario is tested by
`CanvasPilot_BrowseProjectImages_OpenProject_ListsImageSourceFiles`.

**CanvasPilot_ManageProjectFiles_RenameAndDelete_UpdateFilesAndReportProblems**: The composed application's user
creates a project that holds two SVG files and selects the first. Renaming it to a name that is not an image source
file is refused with a message; renaming it to `cover.svg` renames the file. Deleting `cover.svg` is first declined
at the confirmation, which keeps the file, and then confirmed, which removes it. The explorer then lists only the
other file. This scenario is tested by `CanvasPilot_ManageProjectFiles_RenameAndDelete_UpdateFilesAndReportProblems`.

**CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell**: The application's units are composed exactly as
at startup, without a window, over temporary per-user folders; the shell is titled with the application name,
the commands that need nothing are available, those that need a project are not, the default settings are in
effect, and the log holds the start and stop entries. The platform requirements link this test to each platform
and runtime with source filters. This scenario is tested by
`CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell`.

**CanvasPilot_PreviewImages_SelectedFile_ShowsRenderedPictureOrError**: The composed application's user creates a
project that holds a good and a broken SVG and selects the good file: the preview shows a 32 by 16 picture and the
image export is available. Selecting the broken file shows an error with the code `cannot-draw`, no picture, and no
export. This scenario is tested by `CanvasPilot_PreviewImages_SelectedFile_ShowsRenderedPictureOrError`.

**CanvasPilot_ExportImages_RenderedSvg_WritesValidPngFile**: With the good SVG shown, the Export as PNG command is
enabled and, with a chosen file, writes a file that starts with the PNG signature and whose header holds width 32 and
height 16. This scenario is tested by `CanvasPilot_ExportImages_RenderedSvg_WritesValidPngFile`.

**CanvasPilot_BuildMessagePayload_SelectedDiagram_PinsPictureToFileAndReportsElements**: The composed application's
user selects a two-node Mermaid diagram in a project. The shown picture names the diagram's file, has a file
fingerprint and a pixel fingerprint of 64 hexadecimal characters each, has a box for each of its two nodes, and the
nodes' text is `Start` and `End`. After another file is shown, the diagram is edited so that the second node is
`Finish`, and it is selected again: the file fingerprint, the pixel fingerprint, and the snapshot key all differ and
the node text is `Start` and `Finish`. This scenario is tested by
`CanvasPilot_BuildMessagePayload_SelectedDiagram_PinsPictureToFileAndReportsElements`.

**CanvasPilot_MarkImage_ShownPicture_HasOverlayLayerThatSurvivesNewPictures**: The composed application's user creates a
project that holds a 32 by 16 and an 8 by 8 SVG. Content is placed in the overlay layer of the pane that the workspace
shows, which is the application's own pane. After the first SVG is selected the layer holds the content beside the 32
pixel wide picture, and after the second SVG is selected it still holds the same content beside the 8 pixel wide
picture. This scenario is tested by `CanvasPilot_MarkImage_ShownPicture_HasOverlayLayerThatSurvivesNewPictures`.

**CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip**: The composed application's user
creates a project through the New Project dialog; two files are added, one an image; the explorer lists only the
image and the title names the project. The user changes the AI model name in the settings dialog and the
application is restarted: the project is in the recent projects menu and the model name is in effect. This
scenario is tested by `CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip`.

**WiX_MsiPackage**: After the Windows build job publishes the application self-contained for win-x64, builds the
installer project with the numeric build version, and copies the package, FileAssert asserts that exactly one
`package/CanvasPilot-*-win-x64.msi` exists with a non-trivial size. This scenario is tested by `WiX_MsiPackage`, which
is run in CI only and is described in the WiX Toolset OTS verification document.

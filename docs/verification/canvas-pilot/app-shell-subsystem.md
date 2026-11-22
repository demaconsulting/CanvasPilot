## AppShell

### Verification Approach

The `AppShell` subsystem is verified by integration tests defined in `AppShellSubsystemTests.cs`. The tests build the
application with the real `ApplicationComposition` over a real `ProjectService`, `RecentProjects`, `SettingsService`,
`RollingFileLogger`, and `ProjectExplorerViewModel` on temporary folders, and drive the real `MainWindowShell` and
`ProjectWorkspaceViewModel` commands. The only substitutes are a hand-written `FakeDialogService`, which plays the user
at each dialog because a real window and platform pickers are not part of these tests, and `FakeRefreshScheduler`, so no
test waits for a delay. The tests of the AI copilot also use a scripted chat model, a stand-in Ollama probe, and the
processes, files, and environment of a fake machine, so that nothing of the computer of the person who runs them is
started or read. The views themselves are verified by the headless UI tests described in the unit verification
documents.

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
- The Style Kits item of the composed shell shows a window over the catalog of the application, which lists the kits
  that come with the program and with which a kit of the user can be made in the folder of the user of the composition.
- Check Against Style on the composed shell checks the picture that is shown against the kit of the project, shows the
  result among the notes of the preview with the list open, tells the user when the project has no style, and the notes
  are gone when another picture is drawn.
- A composition given the folder for the local data builds the AI copilot and gives the AI Provider page a Check
  connection that asks the copilot's readiness check (over a stand-in Ollama server and a fake machine); a composition
  without the folder has no copilot and no check.
- The chat panel's request for the settings opens the settings dialog of the shell; a change of the AI provider settings
  makes the panel check the provider again (the banner follows), and a change of other settings does not.
- A file that the copilot writes while it answers is listed and selected in the explorer, and the preview draws it.
- Disposing the composition disposes the copilot and its factory once, and the settings are no longer looked at.
- A stock window and the text on it take the pane surface and the primary text color of the theme variant that is
  asked for, and the system accent is the brand accent, in the dark and in the light variant.
- Walking through a window with the Tab key shows the focus ring on the control that has the focus and on no other.
- An icon in a stock button is 16 by 16 and has the primary text color of the theme variant that is asked for.
- The published folder holds the notice of the third-party icons (checked in the build of the installer).

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
shell shows a dialog whose pages are titled AI Provider, Speech, and Style Kits, in that order, and the second page is
the Speech page and the third is the Style Kits page. This scenario is tested by
`AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages`.

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

**AppShellSubsystem_StyleKits_ComposedShell_ShowsTheWindowOnTheCatalogOfTheApplication**: With the kits read, the Style
Kits command of a composed shell is usable, gives the dialog service one window that lists the three kits that come with
the program, and a kit that is made in that window is in the folder of the user of the composition and was offered to
the editor. This scenario is tested by
AppShellSubsystem_StyleKits_ComposedShell_ShowsTheWindowOnTheCatalogOfTheApplication.

**AppShellSubsystem_ComposeApplication_CopilotIsBuiltFromItsFoldersAndFeedsTheSettingsPage**: A composition without the
folder for the local data has no copilot and an AI Provider page without a connection check. A composition with the
folder, over a stand-in Ollama server that has the model, has an `AgentService`, and on its page, after Ollama and the
model are chosen, Check connection finds the provider usable by asking the server once at the address of the page;
no agent was made. This scenario is tested by
`AppShellSubsystem_ComposeApplication_CopilotIsBuiltFromItsFoldersAndFeedsTheSettingsPage`.

**AppShellSubsystem_ComposeApplication_CopilotAsksForSettingsAndIsCheckedAgainWhenTheyChange**: The settings choose
GitHub Copilot on a machine without a sign-in and the panel shows the sign-in sentence; the panel asks for the
settings and the settings dialog is opened once; a change of the recent projects asks nothing of the server; a change
to Ollama with a model of the server makes the panel check again and the banner goes away. This scenario is tested by
`AppShellSubsystem_ComposeApplication_CopilotAsksForSettingsAndIsCheckedAgainWhenTheyChange`.

**AppShellSubsystem_ComposeApplication_CopilotWritesFile_ExplorerSelectsItAndPreviewDrawsIt**: A scripted model says a
word, writes `box.svg` with the project tool, and says that it is drawn. After the message is sent the file has the
drawing, the explorer selects it, the preview shows a picture of 40 by 20, and the transcript holds the message, the
words, the tool row, and the last words. This scenario is tested by
`AppShellSubsystem_ComposeApplication_CopilotWritesFile_ExplorerSelectsItAndPreviewDrawsIt`.

**AppShellSubsystem_ComposeApplication_DisposeStopsTheCopilotAndTheWiring**: After the panel checked the provider the
composition is disposed twice and the AI provider settings are changed; the factory of the agents was disposed once and
the server was not asked again. This scenario is tested by
`AppShellSubsystem_ComposeApplication_DisposeStopsTheCopilotAndTheWiring`.

**AppShellSubsystem_Themes_PaletteThemesStockControlsInBothVariants**: A stock window with a text block is shown in the
dark and in the light variant (a headless UI test in `AppShellThemeTests.cs`); its background is the pane surface of the
variant, the text is the primary text color, the system accent is the brand accent, and the two variants differ. This
scenario is tested by `AppShellSubsystem_Themes_PaletteThemesStockControlsInBothVariants`.

**AppShellSubsystem_Themes_TabKeyShowsTheFocusRingOnTheFocusedControlOnly**: An icon button, a tool toggle, a pill
button, and a splitter are in a window (a headless UI test in `AppShellThemeTests.cs`); the Tab key is pressed four
times and after each press the ring is in the focus color on the control that has the focus and transparent on the
others. This scenario is tested by `AppShellSubsystem_Themes_TabKeyShowsTheFocusRingOnTheFocusedControlOnly`.

**AppShellSubsystem_Icons_IconInButtonTakesThePrimaryTextColorOfTheVariant**: A stock button holds an icon of class
`icon` in a window in the dark and in the light variant (a headless UI test in `AppShellThemeTests.cs`); the icon is 16
by 16 and its foreground is the primary text color of the variant. This scenario is tested by
`AppShellSubsystem_Icons_IconInButtonTakesThePrimaryTextColorOfTheVariant`.

**ThirdPartyNotices_Published**: The notice of the third-party icons is in the published folder, which the installer
takes whole; it is checked by FileAssert in the build of the installer and is described in the system verification
document. This scenario is tested by `ThirdPartyNotices_Published`, which is run in CI only.

**AppShellSubsystem_Create_NullArguments_Throw**: Composing with a missing logger, settings service, or scheduler
is refused. This scenario is tested by `AppShellSubsystem_Create_NullArguments_Throw`.

### Screenshot Generation (Opt-In Tool)

The pictures of the README and the user guide are drawn from the real application by headless UI tests of the `UiTests`
project, so that they cannot drift from what the program shows. `ScreenshotSupport` shows a window at a fixed size and
theme in the headless Avalonia platform with Skia and the embedded Inter font, lets it settle, and saves the drawn frame
as a PNG. The screenshot tests carry the category `Screenshots` and skip themselves unless the environment variable
`CANVASPILOT_SCREENSHOT_DIR` names an output folder, so the normal run of `build.ps1` writes nothing. The script
`docs/images/render-screenshots.ps1` sets the variable and runs only those tests; with `-Check` it compares fresh
pictures with the committed ones using a tolerance, and it is not part of the continuous integration because text
hinting is not bit-for-bit the same on every operating system. The tool is not linked to a requirement; two tests keep
it honest in every run: `ScreenshotSupport_Capture_WritesPngOfRequestedSize` proves that a drawn window is saved as a
PNG of exactly the requested size that is not a single flat color, in both theme variants, and
`ScreenshotSmoke_MainWindow_WritesBothThemes` is the opt-in smoke test of the whole route.

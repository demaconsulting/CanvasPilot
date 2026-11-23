package CanvasPilot_MainWindowShell {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AboutDialog::*;
    private import CanvasPilot_ComposerViewModel::*;
    private import CanvasPilot_ExportPresetDialog::*;
    private import CanvasPilot_ImageExporter::*;
    private import CanvasPilot_KnowledgeBrowser::*;
    private import CanvasPilot_NewProjectDialog::*;
    private import CanvasPilot_PayloadInspector::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_ProjectWorkspace::*;
    private import CanvasPilot_RecentProjects::*;
    private import CanvasPilot_SettingsDialog::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_StatusBar::*;
    private import CanvasPilot_StyleChecker::*;
    private import CanvasPilot_StyleKitManager::*;
    private import CanvasPilot_StylePicker::*;
    private import CanvasPilot_ThemeResources::*;
    private import CanvasPilot_Welcome::*;
    private import CanvasPilot_ProviderPill::*;
    private import CanvasPilot_PushToTalkBinding::*;

    part def MainWindowShell {
        doc /* The main window's view model and window: File, Edit, Image, Tools, and Help commands (Help shows the About dialog, the browser of the drawing knowledge, and, through a folder launcher that tests replace, the folder of the log files, with a message that gives the reason and the path when it cannot be shown), the window title, the title bar that holds the compact menu, the name of the open project, the style list, and the AI provider pill and keeps clear of the buttons that the system draws at the edge of the window (it asks for the minimize, maximize, and close buttons and no full-screen button, keeps free the width that they cover, measured from the top of the visual tree with 140 pixels as the fallback on Windows, and turns a window that is put into full screen by any other means back to the normal state, with no key for full screen), the body that shows the welcome screen or the workspace and the status bar below it, the recent projects menu, Undo and Redo of the focused part, the talk key (the key or chord that the speech settings choose, F9 by default: dictation starts when it goes down, ignores its repeats, and ends when any key of the chord is released, the window closes, or the window is no longer active), the saved size, place, and state of the window with the widths of its columns, the thumbnail setting of the explorer, and the dialog service the window implements. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/MainWindowShell.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/IDialogService.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/IFolderLauncher.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/MessageDialogViewModel.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/MessageDialogView.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/MessageDialogView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/MainWindowView.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/MainWindowView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/MainWindowShellTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/MessageDialogTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/AppShellSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/MainWindowViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/MainWindowTitleBarLayoutTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AppShellUiTestSupport.cs"
            );
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/main-window-shell.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/main-window-shell.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/main-window-shell.yaml");
        }

        // Creates, opens, saves, and closes projects and follows the open project.
        dependency MainWindowShell to ProjectService;

        // Lists the recent projects in the File menu and records opened projects.
        dependency MainWindowShell to RecentProjects;

        // Opens the New Project dialog for the user to choose a name and location.
        dependency MainWindowShell to NewProjectDialog;

        // Opens the settings dialog over the supplied settings pages.
        dependency MainWindowShell to SettingsDialog;

        // Hosts the workspace as the window content and passes it the dialog service.
        dependency MainWindowShell to ProjectWorkspace;

        // Opens the About dialog.
        dependency MainWindowShell to AboutDialog;

        // Runs the Image menu commands through the exporter seam (IImageExporter) that the Preview subsystem owns.
        dependency MainWindowShell to ImageExporter;

        // Shows the Export PNG dialog, as a dialog it owns, when the exporter asks how to export the picture, through
        // the dialog interface (IExportDialogs) that the Preview subsystem owns and the dialog service extends.
        dependency MainWindowShell to ExportPresetDialog;

        // Shows the payload inspector as a dialog it owns, on behalf of the chat panel, through the launcher seam
        // (IPayloadInspectorLauncher) that the Chat subsystem owns and the dialog service extends.
        dependency MainWindowShell to PayloadInspector;

        // Shows the browser of the drawing knowledge as a window it owns that does not block it, through the launcher
        // seam (IKnowledgeBrowserLauncher) that the Knowledge subsystem owns and the dialog service extends, over the
        // catalog that the composition gives it.
        dependency MainWindowShell to KnowledgeBrowser;

        // Undoes and redoes through the focused part's IUndoRedoTarget and starts and stops dictation through an
        // IPushToTalkTarget, both seams that the Composer subsystem owns (ComposerViewModel implements the first).
        dependency MainWindowShell to ComposerViewModel;

        // Hosts the list that chooses the style kit in the title bar, and asks it to give a new project the default kit
        // and to open the window that manages kits.
        dependency MainWindowShell to StylePicker;

        // Shows the window that manages style kits from the Tools menu, through the dialogs that the main window implements.
        dependency MainWindowShell to StyleKitManager;

        // Runs the Check Against Style item of the Image menu through the interface (ICheckAgainstStyle) that the Style
        // subsystem owns and the check of the shown picture implements.
        dependency MainWindowShell to StyleChecker;

        // Makes the status bar over the workspace and places it at the bottom of the window.
        dependency MainWindowShell to StatusBar;

        // Holds the pill that tells which AI provider is chosen, which the composition gives it, and shows it in the title bar.
        dependency MainWindowShell to ProviderPill;

        // Makes the welcome screen over itself and shows it in the body of the window while no project is open.
        dependency MainWindowShell to Welcome;

        // Reads the saved layout of the window, the time of each opening of a project, and the thumbnail setting, and
        // writes the size, place, and state of the window and the widths of the columns.
        dependency MainWindowShell to SettingsService;

        // Takes every color, size, icon, and button look of the window and its dialogs from the tokens and control themes.
        dependency MainWindowShell to ThemeResources;

        // Holds the key that is held to talk, as the setting that follows the choice of the user, and listens for it.
        dependency MainWindowShell to PushToTalkBinding;
    }
}

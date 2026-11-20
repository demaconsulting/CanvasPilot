package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def MainWindowShell {
        doc /* The main window's view model and window: File, Image, Tools, and Help commands, the window title, the recent projects menu, and the dialog service the window implements. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/MainWindowShell.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/IDialogService.cs",
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

        // Shows the payload inspector as a dialog it owns, on behalf of the chat panel, through the launcher seam
        // (IPayloadInspectorLauncher) that the Chat subsystem owns and the dialog service extends.
        dependency MainWindowShell to PayloadInspector;
    }
}

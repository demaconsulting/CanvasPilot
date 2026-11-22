package CanvasPilot_AppShellSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AboutDialog::*;
    private import CanvasPilot_MainWindowShell::*;
    private import CanvasPilot_ProjectWorkspace::*;
    private import CanvasPilot_ThemeResources::*;

    part def AppShellSubsystem {
        doc /* The main window with its menus, the project workspace, the About dialog, and the composition root that builds the application's units at startup, including the AI copilot with its history, and joins them to the chat panel, the settings page, and the project explorer. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/App.axaml",
                "src/DemaConsulting.CanvasPilot/App.axaml.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ApplicationComposition.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ViewLocator.cs",
                "src/DemaConsulting.CanvasPilot.Desktop/Program.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/AppShellSubsystemTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/TestAppBuilder.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ScreenshotSupport.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ScreenshotSmokeTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem.yaml");
        }

        part mainWindowShell : MainWindowShell;
        part projectWorkspace : ProjectWorkspace;
        part aboutDialog : AboutDialog;
        part themeResources : ThemeResources;
    }
}

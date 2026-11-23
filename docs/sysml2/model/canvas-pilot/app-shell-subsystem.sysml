package CanvasPilot_AppShellSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AboutDialog::*;
    private import CanvasPilot_MainWindowShell::*;
    private import CanvasPilot_ProjectWorkspace::*;
    private import CanvasPilot_ProviderPill::*;
    private import CanvasPilot_StatusBar::*;
    private import CanvasPilot_ThemeResources::*;
    private import CanvasPilot_Welcome::*;

    part def AppShellSubsystem {
        doc /* The main window with its title bar, menus, status bar, AI provider pill, and welcome screen, the project workspace with its saved layout, the About dialog, the design tokens and control themes of the user interface, and the composition root that builds the application's units at startup, including the AI copilot with its history, and joins them to the chat panel, the settings page, and the project explorer. */

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
        part statusBar : StatusBar;
        part providerPill : ProviderPill;
        part welcome : Welcome;
    }
}

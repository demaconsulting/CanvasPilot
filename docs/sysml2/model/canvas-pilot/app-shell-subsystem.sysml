package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def AppShellSubsystem {
        doc /* The main window with its menus, the project workspace, the About dialog, and the composition root that builds the application's units at startup. */

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
                "test/DemaConsulting.CanvasPilot.UiTests/TestAppBuilder.cs"
            );
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem.yaml");
        }

        part mainWindowShell : MainWindowShell;
        part projectWorkspace : ProjectWorkspace;
        part aboutDialog : AboutDialog;
    }
}

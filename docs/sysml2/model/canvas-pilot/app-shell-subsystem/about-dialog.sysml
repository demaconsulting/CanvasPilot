package CanvasPilot_AboutDialog {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ThemeResources::*;

    part def AboutDialog {
        doc /* Modal About window that shows the brand mark, application name, tagline, version, copyright, and the open source libraries and the icon set (Fluent System Icons) that the application is built on. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/AboutDialogViewModel.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/AboutDialogView.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/AboutDialogView.axaml.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/AboutDialogTests.cs");
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/about-dialog.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/about-dialog.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/about-dialog.yaml");
        }

        // Takes the pane color, the card, heading, and hint styles, the main button look, and the brand icon from the tokens
        // and control themes.
        dependency AboutDialog to ThemeResources;
    }
}

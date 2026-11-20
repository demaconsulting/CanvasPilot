package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def AboutDialog {
        doc /* Modal About window that shows the application name, tagline, version, copyright, and open source libraries. */

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
    }
}

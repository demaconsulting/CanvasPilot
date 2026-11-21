package CanvasPilot_SettingsDialog {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_SettingsService::*;

    part def SettingsDialog {
        doc /* Modal settings window that hosts injected settings pages and saves them on OK or Apply. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/ISettingsPage.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsChoice.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsDialogViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsDialogView.axaml",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsDialogView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/SettingsDialogTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/FakeSettingsPage.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/SettingsDialogViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/SettingsUiTestSupport.cs"
            );
            designFiles = ("docs/design/canvas-pilot/settings-subsystem/settings-dialog.md");
            verificationFiles = ("docs/verification/canvas-pilot/settings-subsystem/settings-dialog.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/settings-subsystem/settings-dialog.yaml");
        }

        // Reads the settings in effect and saves the merged page entries.
        dependency SettingsDialog to SettingsService;
    }
}

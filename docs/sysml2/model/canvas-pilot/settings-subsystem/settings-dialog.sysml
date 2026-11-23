package CanvasPilot_SettingsDialog {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_SettingsService::*;

    part def SettingsDialog {
        doc /* Modal settings window that hosts injected settings pages in a rail with an icon and a title, shows the chosen page under its title and a sentence, shows a problem with saving in a banner, and saves the pages on OK or Apply. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/ISettingsPage.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/IconKeyConverter.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsChoice.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsDialogViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsDialogView.axaml",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsDialogView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/SettingsDialogTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/FakeSettingsPage.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/SettingsDialogViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/SettingsUiTestSupport.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/settings-subsystem/settings-dialog.md");
            verificationFiles = ("docs/verification/canvas-pilot/settings-subsystem/settings-dialog.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/settings-subsystem/settings-dialog.yaml");
        }

        // Reads the settings in effect and saves the merged page entries.
        dependency SettingsDialog to SettingsService;
    }
}

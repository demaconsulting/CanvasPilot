package CanvasPilot_AppearanceSettingsPage {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_SettingsDialog::*;

    part def AppearanceSettingsPage {
        doc /* The Appearance page of the settings dialog: the theme (follow the system, dark, or light) and the options of the project explorer, merging only the appearance into the settings when applied. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AppearanceSettingsPageViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AppearanceSettingsPageView.axaml",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AppearanceSettingsPageView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/AppearanceSettingsPageTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AppearanceSettingsPageViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/settings-subsystem/appearance-settings-page.md");
            verificationFiles = ("docs/verification/canvas-pilot/settings-subsystem/appearance-settings-page.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/settings-subsystem/appearance-settings-page.yaml");
        }

        // Is hosted by the settings dialog through its page contract and merges into the settings records.
        dependency AppearanceSettingsPage to SettingsDialog;
    }
}
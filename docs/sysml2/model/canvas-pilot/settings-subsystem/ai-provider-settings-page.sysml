package CanvasPilot_AiProviderSettingsPage {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_SettingsDialog::*;

    part def AiProviderSettingsPage {
        doc /* The AI Provider page of the settings dialog; edits and validates persisted provider choices. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AiProviderSettingsPageViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AiProviderSettingsPageView.axaml",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AiProviderSettingsPageView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/AiProviderSettingsPageTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AiProviderSettingsPageViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/settings-subsystem/ai-provider-settings-page.md");
            verificationFiles = ("docs/verification/canvas-pilot/settings-subsystem/ai-provider-settings-page.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/settings-subsystem/ai-provider-settings-page.yaml");
        }

        // Is hosted by the settings dialog through its page contract and merges into the settings records.
        dependency AiProviderSettingsPage to SettingsDialog;
    }
}

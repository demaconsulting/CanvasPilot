package CanvasPilot_AiProviderSettingsPage {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_SettingsDialog::*;

    part def AiProviderSettingsPage {
        doc /* The AI Provider page of the settings dialog; edits and validates persisted provider choices, lists the models of the selected provider with what each can do (tool calling, pictures, reasoning levels, context window) and a warning for what it lacks through the IProviderModelSource interface that the Settings subsystem owns, enables the reasoning level only among the levels that the chosen model offers, warns where the level is chosen that a high level (high, xhigh, or max, the level that the provider chooses included) on a large GitHub Copilot model (a billing multiplier of three or more, or a powerful or high-priced model) is slow and costly with the premium requests that a message counts, and shows no warning otherwise, keeps the typed model name and the stored choices when the models cannot be listed, and checks the connection to the provider through the IProviderCheck interface that the Settings subsystem owns. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AiProviderSettingsPageViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AiProviderSettingsPageView.axaml",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AiProviderSettingsPageView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/IProviderCheck.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/IProviderModelSource.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/ModelChoice.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/AiProviderSettingsPageTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AiProviderSettingsPageViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/settings-subsystem/ai-provider-settings-page.md");
            verificationFiles = ("docs/verification/canvas-pilot/settings-subsystem/ai-provider-settings-page.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/settings-subsystem/ai-provider-settings-page.yaml");
        }

        // Is hosted by the settings dialog through its page contract and merges into the settings records.
        dependency AiProviderSettingsPage to SettingsDialog;
    }
}

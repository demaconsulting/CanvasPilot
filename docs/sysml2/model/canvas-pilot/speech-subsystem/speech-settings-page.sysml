package CanvasPilot_SpeechSettingsPage {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_DeviceSelection::*;
    private import CanvasPilot_ModelCatalogPanel::*;
    private import CanvasPilot_ModelParameterEditor::*;
    private import CanvasPilot_PushToTalkBinding::*;
    private import CanvasPilot_SettingsDialog::*;

    part def SpeechSettingsPage {
        doc /* The Speech page of the settings dialog; composes the device pickers, model catalog, and parameter editors and maps them to and from the stored speech settings, and lets the user choose the key that is held to talk by pressing it in a box (a modifier alone is waited out and Esc cancels), shows it in words, refuses a key that is not safe with the sentence that says why and leaves the earlier key, stores it with the speech settings (nothing for the default), and puts the default back with a reset button. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/SpeechSettingsPageViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/SpeechSettingsPageView.axaml",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/SpeechSettingsPageView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/SpeechSettingsPageTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/SpeechSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/SpeechSettingsPageViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/SpeechUiTestSupport.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/speech-subsystem/speech-settings-page.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem/speech-settings-page.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem/speech-settings-page.yaml");
        }

        // Is hosted by the settings dialog through its page contract and merges into the settings records.
        dependency SpeechSettingsPage to SettingsDialog;

        // Composes the device pickers.
        dependency SpeechSettingsPage to DeviceSelection;

        // Composes the model lists and their downloads.
        dependency SpeechSettingsPage to ModelCatalogPanel;

        // Composes the parameter editors of the chosen models.
        dependency SpeechSettingsPage to ModelParameterEditor;

        // Checks, writes, and reads the key that is held to talk with the rules of the key.
        dependency SpeechSettingsPage to PushToTalkBinding;
    }
}

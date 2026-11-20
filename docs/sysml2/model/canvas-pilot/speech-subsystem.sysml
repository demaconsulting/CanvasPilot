package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def SpeechSubsystem {
        doc /* Lists audio devices and speech models, downloads models, provides the Speech page of the settings dialog with device pickers, a model catalog, and model parameter editors, and turns speech held to talk into text. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/SpeechSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/speech-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem.yaml");
        }

        part audioDeviceService : AudioDeviceService;
        part speechModelService : SpeechModelService;
        part dictationService : DictationService;
        part deviceSelection : DeviceSelection;
        part modelCatalogPanel : ModelCatalogPanel;
        part modelParameterEditor : ModelParameterEditor;
        part speechSettingsPage : SpeechSettingsPage;
    }
}

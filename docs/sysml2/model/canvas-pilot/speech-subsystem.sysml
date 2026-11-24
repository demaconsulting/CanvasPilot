package CanvasPilot_SpeechSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AudioDeviceService::*;
    private import CanvasPilot_DeviceSelection::*;
    private import CanvasPilot_DictationService::*;
    private import CanvasPilot_ModelCatalogPanel::*;
    private import CanvasPilot_ModelParameterEditor::*;
    private import CanvasPilot_PushToTalkBinding::*;
    private import CanvasPilot_SpeechModelService::*;
    private import CanvasPilot_SpeechSettingsPage::*;

    part def SpeechSubsystem {
        doc /* Lists audio devices and speech models, downloads models, provides the Speech page of the settings dialog with device pickers, a model catalog, model parameter editors, and the choice of the key that is held to talk (F9 by default), and turns speech held to talk into text. */

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
        part pushToTalkBinding : PushToTalkBinding;
    }
}

package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def DeviceSelection {
        doc /* The capture and playback device pickers of the Speech page; chooses devices by name and explains missing devices. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/DeviceSelectionViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/DeviceSelectionView.axaml",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/DeviceSelectionView.axaml.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/DeviceSelectionTests.cs");
            designFiles = ("docs/design/canvas-pilot/speech-subsystem/device-selection.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem/device-selection.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem/device-selection.yaml");
        }

        // Lists devices and scans for changed hardware through the audio device service.
        dependency DeviceSelection to AudioDeviceService;
    }
}

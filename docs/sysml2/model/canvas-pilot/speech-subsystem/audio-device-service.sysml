package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def AudioDeviceService {
        doc /* Lists capture and playback devices through the speech library on demand and returns a missing or unusable audio system as a result with an explanation. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/IAudioDeviceService.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/AudioDeviceService.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/AudioDeviceServiceTests.cs");
            designFiles = ("docs/design/canvas-pilot/speech-subsystem/audio-device-service.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem/audio-device-service.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem/audio-device-service.yaml");
        }

        // Lists devices and scans for changed hardware with the speech library's audio device factory.
        dependency AudioDeviceService to Ots::Speech;

        // Reports audio system problems to the application log.
        dependency AudioDeviceService to RollingFileLogger;
    }
}

package CanvasPilot_DictationService {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AudioDeviceService::*;
    private import CanvasPilot_RollingFileLogger::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_SpeechModelService::*;
    private import OtsDependencies::*;

    part def DictationService {
        doc /* Turns what the user says while holding a talk control into text through the speech library's recognition engine, with the model, settings, and microphone chosen in the settings; loads the engine once, checks the native speech files before it loads one, and returns every state in which dictation cannot work as a result with an explanation. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/IDictationService.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/DictationService.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/DictationServiceTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/DictationTestSupport.cs"
            );
            designFiles = ("docs/design/canvas-pilot/speech-subsystem/dictation-service.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem/dictation-service.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem/dictation-service.yaml");
        }

        // Reads the chosen model, its stored settings, and the chosen microphone from the settings in effect.
        dependency DictationService to SettingsService;

        // Lists the models, and loads the speech engine of the chosen speech-to-text model.
        dependency DictationService to SpeechModelService;

        // Lists the microphones and creates the capture device of the chosen one.
        dependency DictationService to AudioDeviceService;

        // Streams the recognition results of a capture device through the speech library's recognition session.
        dependency DictationService to Speech;

        // Reports dictation events and problems, never the recognized text, to the application log.
        dependency DictationService to RollingFileLogger;
    }
}

package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def SpeechModelService {
        doc /* Lists the speech library's models with their install state, downloads them into the shared model store, and loads the speech engine of an installed speech-to-text model, returning every failure as a result. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ISpeechModelService.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/SpeechModelService.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/SpeechModelServiceTests.cs");
            designFiles = ("docs/design/canvas-pilot/speech-subsystem/speech-model-service.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem/speech-model-service.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem/speech-model-service.yaml");
        }

        // Uses the speech library's model catalog filled with the Sherpa models.
        dependency SpeechModelService to Ots::Speech;

        // Reports catalog and download problems to the application log.
        dependency SpeechModelService to RollingFileLogger;
    }
}

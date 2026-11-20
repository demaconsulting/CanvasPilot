package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ModelCatalogPanel {
        doc /* The speech-to-text and text-to-speech model lists of the Speech page; chooses models and runs downloads with progress and cancellation. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ModelCatalogPanelViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ModelListItemViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ModelCatalogPanelView.axaml",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ModelCatalogPanelView.axaml.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/ModelCatalogPanelTests.cs");
            designFiles = ("docs/design/canvas-pilot/speech-subsystem/model-catalog-panel.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem/model-catalog-panel.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem/model-catalog-panel.yaml");
        }

        // Lists and downloads models through the speech model service.
        dependency ModelCatalogPanel to SpeechModelService;

        // Shows the library's model identity and install state types.
        dependency ModelCatalogPanel to Ots::Speech;
    }
}

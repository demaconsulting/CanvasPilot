package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ModelParameterEditor {
        doc /* Builds numeric, choice, and boolean editors from the parameters a speech model declares and reads and writes their values as invariant-culture strings. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ModelParameterEditorViewModel.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ParameterViewModels.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ModelParameterEditorView.axaml",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/ModelParameterEditorView.axaml.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/ModelParameterEditorTests.cs");
            designFiles = ("docs/design/canvas-pilot/speech-subsystem/model-parameter-editor.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem/model-parameter-editor.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem/model-parameter-editor.yaml");
        }

        // Reads the typed parameter descriptors that the library's models declare.
        dependency ModelParameterEditor to Ots::Speech;
    }
}

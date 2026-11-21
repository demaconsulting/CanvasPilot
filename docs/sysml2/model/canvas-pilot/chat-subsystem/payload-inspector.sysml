package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def PayloadInspector {
        doc /* The window that shows the user exactly what the assistant would receive for a sent message: the description that the payload describer writes, as read-only text, and the pictures of the payload, each marked picture with its marks drawn on it and the area around each mark, each with a caption. It changes nothing, releases its decoded pictures when it closes, and is shown through a launcher seam that the main window implements as a dialog it owns. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/IPayloadInspectorLauncher.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/PayloadInspectorView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/PayloadInspectorView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/PayloadInspectorViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/PayloadPictureViewModel.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/PayloadInspectorTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/PayloadInspectorViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/chat-subsystem/payload-inspector.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem/payload-inspector.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem/payload-inspector.yaml");
        }

        // Shows the values of the payload and its pictures.
        dependency PayloadInspector to MessagePayload;

        // Writes the text that the window shows.
        dependency PayloadInspector to PayloadDescriber;
    }
}

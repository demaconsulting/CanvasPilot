package CanvasPilot {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;

    part def MessagePayload {
        doc /* The contract between the chat and the assistant: the immutable values that hold the message that was sent, with its text, its parts in order (text of an origin, attachment references, mark references), its attachments, and its pictures with their annotated images, marks, areas, geometry, and source elements. The values copy the lists and bytes they are given. The value records are documented as supporting value types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/PayloadSubsystem/MessagePayload.cs",
                "src/DemaConsulting.CanvasPilot/PayloadSubsystem/PayloadAttachment.cs",
                "src/DemaConsulting.CanvasPilot/PayloadSubsystem/PayloadImage.cs",
                "src/DemaConsulting.CanvasPilot/PayloadSubsystem/PayloadMark.cs",
                "src/DemaConsulting.CanvasPilot/PayloadSubsystem/PayloadSegment.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/PayloadSubsystem/MessagePayloadTests.cs");
            designFiles = ("docs/design/canvas-pilot/payload-subsystem/message-payload.md");
            verificationFiles = ("docs/verification/canvas-pilot/payload-subsystem/message-payload.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/payload-subsystem/message-payload.yaml");
        }

        // The version of the contract that the values describe.
        attribute contractVersion : Integer;

        // Holds the origin of text, the kind of attachments, the shape of marks, the elements under marks, and the
        // coordinates of the picture.
        dependency MessagePayload to ComposerDocument;
        dependency MessagePayload to MarkRegistry;
        dependency MessagePayload to MarkSourceResolver;
        dependency MessagePayload to ImageRenderService;
    }
}

package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def PayloadSubsystem {
        doc /* Builds, from the message that the user sends, the immutable payload for the assistant: the text with its references in order, the attachments with their paths, and for each picture that marks were drawn on the annotated picture, the area around each mark, the geometry and note of each mark, and the parts of the source file under it. It also describes a payload as text. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/PayloadSubsystem/PayloadSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/payload-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/payload-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/payload-subsystem.yaml");
        }

        part messagePayload : MessagePayload;
        part markImageComposer : MarkImageComposer;
        part payloadBuilder : PayloadBuilder;
        part payloadDescriber : PayloadDescriber;
    }
}

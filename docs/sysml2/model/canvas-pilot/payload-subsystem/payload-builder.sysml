package CanvasPilot_PayloadBuilder {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AttachmentStore::*;
    private import CanvasPilot_ComposerDocument::*;
    private import CanvasPilot_ComposerViewModel::*;
    private import CanvasPilot_ImageRenderService::*;
    private import CanvasPilot_MarkImageComposer::*;
    private import CanvasPilot_MarkRegistry::*;
    private import CanvasPilot_MarkSourceResolver::*;
    private import CanvasPilot_MessagePayload::*;

    part def PayloadBuilder {
        doc /* Builds the payload of a message that is sent from a snapshot of the composer, the marks that were made, and the attachments that were put into the project: the text with references and the parts in order, the attachments with their paths, and one image for each distinct picture that marks of the message were drawn on, with the annotated picture, and for each mark its geometry, note, area, and the parts of the source under it. It reads only values that cannot change, so it can run on a background thread. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/PayloadSubsystem/PayloadBuilder.cs");
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/PayloadSubsystem/PayloadBuilderTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/PayloadSubsystem/PayloadSubsystemFakes.cs"
            );
            designFiles = ("docs/design/canvas-pilot/payload-subsystem/payload-builder.md");
            verificationFiles = ("docs/verification/canvas-pilot/payload-subsystem/payload-builder.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/payload-subsystem/payload-builder.yaml");
        }

        // Reads the snapshot of the message: the text, the origins, and the chips.
        dependency PayloadBuilder to ComposerDocument;

        // Reads the staged attachments with their paths in the project.
        dependency PayloadBuilder to AttachmentStore;

        // Reads the marks of the message with their pictures and geometry.
        dependency PayloadBuilder to MarkRegistry;

        // Asks for the parts of the source file under each mark.
        dependency PayloadBuilder to MarkSourceResolver;

        // Has the marks drawn on their pictures and the areas cut out.
        dependency PayloadBuilder to MarkImageComposer;

        // Makes the values of the payload.
        dependency PayloadBuilder to MessagePayload;

        // Reads the facts of the pictures: file, fingerprints, format, and size.
        dependency PayloadBuilder to ImageRenderService;

        // Implements the message builder interface that the composer owns, so that the composer has its messages built.
        dependency PayloadBuilder to ComposerViewModel;
    }
}

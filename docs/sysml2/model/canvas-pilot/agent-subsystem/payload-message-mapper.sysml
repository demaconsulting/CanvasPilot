package CanvasPilot_PayloadMessageMapper {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import CanvasPilot_MessagePayload::*;
    private import OtsDependencies::*;

    part def PayloadMessageMapper {
        doc /* Turns the message that the user sent (the message payload) into the one message of the role user that a model is given. The text of the user comes first, with line breaks made line feeds; when the message has attachments or pictures, a block of context follows that names each attachment (index, kind, name, path relative to the project folder), each picture (path relative to the project folder, or the file name for a picture outside the project, format, size in pixels), and each mark (number, shape, note, bounding box and close-up box in whole picture pixels, and up to five parts of the source under it, with a statement when more are left out, when none were found, or why the picture cannot say), and lists the images in order. The annotated pictures follow as PNG images in the order of the pictures, then the close-ups of the marks in the order of the numbers of the marks, at most ten, with a sentence that says which marks have none. The mapping is pure and deterministic, the same in every culture; it never writes the place where the user had an attachment, and every value that a user or a file supplies is written on one line, with double quotes made single, cut at 200 characters. It has no supporting type. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/AgentSubsystem/PayloadMessageMapper.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/PayloadMessageMapperTests.cs");
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/payload-message-mapper.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/payload-message-mapper.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/payload-message-mapper.yaml");
        }

        // Reads the text, attachments, pictures, and marks of the message that the user sent.
        dependency PayloadMessageMapper to MessagePayload;

        // Makes the user message with its text content and its PNG image contents.
        dependency PayloadMessageMapper to ExtensionsAi;

        // The most close-ups of marks that are sent as images.
        attribute maxCrops : Integer;

        // The most parts of the source that are written for one mark.
        attribute maxSourceElementsPerMark : Integer;
    }
}

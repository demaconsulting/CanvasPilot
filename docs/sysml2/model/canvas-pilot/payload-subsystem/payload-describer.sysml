package CanvasPilot_PayloadDescriber {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_MessagePayload::*;

    part def PayloadDescriber {
        doc /* Describes a payload as JSON text for a person to read, listing the message, the attachments, and each picture with its marks, geometry, notes, and source elements, with each image replaced by its size in bytes and its SHA-256 fingerprint, in a fixed order and number format so that the same payload always gives the same text. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/PayloadSubsystem/PayloadDescriber.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/PayloadSubsystem/PayloadDescriberTests.cs");
            designFiles = ("docs/design/canvas-pilot/payload-subsystem/payload-describer.md");
            verificationFiles = ("docs/verification/canvas-pilot/payload-subsystem/payload-describer.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/payload-subsystem/payload-describer.yaml");
        }

        // Reads the values of the payload.
        dependency PayloadDescriber to MessagePayload;
    }
}

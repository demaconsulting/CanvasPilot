package CanvasPilot_AttachmentIntake {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AttachmentStore::*;
    private import CanvasPilot_ComposerDocument::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def AttachmentIntake {
        doc /* Turns dropped and pasted content into chips and text for the message: decides what each dropped path is without opening it, leaves out unsafe, missing, repeated, and excessive items with a reason, saves pasted pictures through the attachment store, and cleans and limits text. Everything dropped is treated as data. The view-neutral content and result records are documented as supporting value types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/AttachmentIntake.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/DroppedContent.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ComposerSubsystem/AttachmentIntakeTests.cs");
            designFiles = ("docs/design/canvas-pilot/composer-subsystem/attachment-intake.md");
            verificationFiles = ("docs/verification/canvas-pilot/composer-subsystem/attachment-intake.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/composer-subsystem/attachment-intake.yaml");
        }

        // Makes the chips and text segments, and cleans text with the document's text rule.
        dependency AttachmentIntake to ComposerDocument;

        // Saves pasted pictures through the store, and applies the store's rules for safe paths and the size limit of a message.
        dependency AttachmentIntake to AttachmentStore;

        // Reports items that were left out to the application log.
        dependency AttachmentIntake to RollingFileLogger;
    }
}

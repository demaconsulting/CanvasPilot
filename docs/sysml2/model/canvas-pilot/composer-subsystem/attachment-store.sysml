package CanvasPilot_AttachmentStore {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def AttachmentStore {
        doc /* Keeps the files of attachments in one project folder: saves pictures pasted from the clipboard in the project's private data folder, removes the ones no chip refers to, and, when a message is sent, copies the attachments from outside the project into the project's attachments folder, all or none, within limits of files, folders, and bytes, with safe unique names, without following links. The attachment content is data and is never opened or run. The staged attachment and result records are documented as supporting value types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/AttachmentNames.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/AttachmentStore.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/IAttachmentStore.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/StagedAttachment.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ComposerSubsystem/AttachmentStoreTests.cs");
            designFiles = ("docs/design/canvas-pilot/composer-subsystem/attachment-store.md");
            verificationFiles = ("docs/verification/canvas-pilot/composer-subsystem/attachment-store.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/composer-subsystem/attachment-store.yaml");
        }

        // Builds every path under a folder with the safe path-combination utility, so no name can lead out of the folder.
        dependency AttachmentStore to PathHelpers;

        // Reports failed copies and files that could not be deleted to the application log.
        dependency AttachmentStore to RollingFileLogger;

        // Takes the name of the project's private data folder from the project document.
        dependency AttachmentStore to ProjectService;
    }
}

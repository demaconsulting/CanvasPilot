package CanvasPilot_ComposerViewModel {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AttachmentIntake::*;
    private import CanvasPilot_AttachmentStore::*;
    private import CanvasPilot_ComposerDocument::*;
    private import CanvasPilot_ComposerHistory::*;
    private import CanvasPilot_DictationService::*;
    private import CanvasPilot_MarkRegistry::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def ComposerViewModel {
        doc /* The orchestration of the message being composed, without a window: owns the document, its undo history, the intake of dropped and pasted content, and push-to-talk dictation, connects the marks and the attachments of the open project to the message, and sends it. It makes each action of the user exactly one undo step, offers undo and redo to the Edit menu and the keys, shows the live words while the talk button is held and adds the final words as one dictated block, names what a drop left out, allows editing only in an open project, and sends the message by putting the attachments into the project and having the message built, keeping the draft when anything fails. The message builder, the undo and redo target, and the push-to-talk target are interfaces that it owns or implements and are documented as supporting types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ComposerViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/IMessageBuilder.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/IPushToTalkTarget.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/IUndoRedoTarget.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/SentMessage.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ComposerSubsystem/ComposerViewModelTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ComposerSubsystem/ComposerViewModelFakes.cs"
            );
            designFiles = ("docs/design/canvas-pilot/composer-subsystem/composer-view-model.md");
            verificationFiles = ("docs/verification/canvas-pilot/composer-subsystem/composer-view-model.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/composer-subsystem/composer-view-model.yaml");
        }

        // Holds the message as segments and chips, and takes the snapshot that is sent.
        dependency ComposerViewModel to ComposerDocument;

        // Makes every change of the message one undo step and clears the steps when the message is sent.
        dependency ComposerViewModel to ComposerHistory;

        // Turns dropped and pasted content into chips and text.
        dependency ComposerViewModel to AttachmentIntake;

        // Puts the attachments into the project when the message is sent and removes saved pictures that nothing refers to.
        dependency ComposerViewModel to AttachmentStore;

        // Reads the marks that belong to the message, checks the marks it adds, and empties the marks when the message is sent.
        dependency ComposerViewModel to MarkRegistry;

        // Listens while the talk button is held and gives the words, or the reason why it cannot.
        dependency ComposerViewModel to DictationService;

        // Writes what it could not do to the application log.
        dependency ComposerViewModel to RollingFileLogger;
    }
}

package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ComposerSubsystem {
        doc /* Holds the message being composed for the chat as ordered segments of typed text, dictated text, and chips, undoes and redoes the user's changes one action at a time, turns dropped and pasted content into chips treating it as data, copies attachments into the project when the message is sent, has the sent message built, and forgets the draft when the message is sent. Its view model orchestrates all of it without a window, including push-to-talk dictation, and its editor view shows the message in one editing surface with chips inline. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ComposerSubsystem/ComposerSubsystemTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ComposerSubsystemUiTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/composer-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/composer-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/composer-subsystem.yaml");
        }

        part composerDocument : ComposerDocument;
        part composerHistory : ComposerHistory;
        part attachmentIntake : AttachmentIntake;
        part attachmentStore : AttachmentStore;
        part composerViewModel : ComposerViewModel;
        part composerEditor : ComposerEditor;
    }
}

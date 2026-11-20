package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ComposerSubsystem {
        doc /* Holds the message being composed for the chat as ordered segments of typed text, dictated text, and chips, undoes and redoes the user's changes one action at a time, and forgets the draft when the message is sent. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ComposerSubsystem/ComposerSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/composer-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/composer-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/composer-subsystem.yaml");
        }

        part composerDocument : ComposerDocument;
        part composerHistory : ComposerHistory;
    }
}

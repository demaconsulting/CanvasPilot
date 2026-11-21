package CanvasPilot_ComposerHistory {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ComposerDocument::*;
    private import ScalarValues::Integer;

    part def ComposerHistory {
        doc /* Applies the user's changes to the composer document as steps and undoes and redoes whole steps, each user action being exactly one step. The history lives in memory only, is bounded, and is cleared when the message is sent. The edit records that make each action one step are documented as supporting value types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ComposerEdit.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ComposerHistory.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ComposerSubsystem/ComposerHistoryTests.cs");
            designFiles = ("docs/design/canvas-pilot/composer-subsystem/composer-history.md");
            verificationFiles = ("docs/verification/canvas-pilot/composer-subsystem/composer-history.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/composer-subsystem/composer-history.yaml");
        }

        // The largest number of steps that can be undone.
        attribute depthLimit : Integer;

        // Changes the document and reads what an edit removes from it.
        dependency ComposerHistory to ComposerDocument;
    }
}

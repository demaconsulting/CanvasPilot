package CanvasPilot {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;

    // The origin of a piece of text in the message, as the TextOrigin enumeration of the segment records (Typed and
    // Dictated; the word 'typed' is reserved in SysML2).
    enum def TextOrigin {
        enum typedText;
        enum dictatedText;
    }

    part def ComposerDocument {
        doc /* The message being composed as an ordered sequence of segments: typed text, dictated text, and chips for files, folders, images, and marks. It is the single source of truth about the message, counts one position for each character and each chip, keeps its segments in one canonical form, cleans all text that enters it, and gives immutable snapshots. The segment, chip, and snapshot records are documented as supporting value types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ComposerDocument.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ComposerSegment.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ComposerSnapshot.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ComposerSubsystem/ComposerDocumentTests.cs");
            designFiles = ("docs/design/canvas-pilot/composer-subsystem/composer-document.md");
            verificationFiles = ("docs/verification/canvas-pilot/composer-subsystem/composer-document.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/composer-subsystem/composer-document.yaml");
        }

        // The number of positions: one for each character of text and one for each chip.
        attribute length : Integer;
    }
}

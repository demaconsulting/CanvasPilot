package CanvasPilot_MarkRegistry {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ImageRenderService::*;
    private import ScalarValues::Integer;

    // The shape of a mark, as the MarkKind enumeration of the marks (the names are those of the shapes in lower case).
    enum def MarkKind {
        enum rectangleMark;
        enum ellipseMark;
        enum arrowMark;
        enum freehandMark;
    }

    part def MarkRegistry {
        doc /* Holds the marks the user has drawn for the message being composed. Each mark is pinned to the exact picture it was drawn on, numbered one more than the largest number so far, described by its geometry in the pixels of its picture in one normal form (corners, tail and head, or a simplified stroke, every point inside the picture) and an optional note of one short line, and shown only on its own picture while its chip is in the message. The mark and geometry records are documented as supporting value types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/Mark.cs",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/MarkGeometry.cs",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/MarkRegistry.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AnnotationSubsystem/AnnotationSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AnnotationSubsystem/MarkRegistryTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/annotation-subsystem/mark-registry.md");
            verificationFiles = ("docs/verification/canvas-pilot/annotation-subsystem/mark-registry.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/annotation-subsystem/mark-registry.yaml");
        }

        // The longest note a mark keeps, in characters.
        attribute maximumNoteLength : Integer;

        // The most points a freehand mark keeps.
        attribute maximumFreehandPoints : Integer;

        // Pins each mark to the rendered picture it was drawn on and uses the picture's size, fingerprints, and pixel
        // coordinates.
        dependency MarkRegistry to ImageRenderService;
    }
}

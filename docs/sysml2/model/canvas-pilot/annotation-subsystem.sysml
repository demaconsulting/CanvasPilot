package CanvasPilot_AnnotationSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AnnotationOverlay::*;
    private import CanvasPilot_MarkRegistry::*;
    private import CanvasPilot_MarkSourceResolver::*;

    part def AnnotationSubsystem {
        doc /* Holds the marks that the user draws on the shown picture for the message being composed, each pinned to the exact picture it was drawn on, numbered, and described by its geometry in the picture's pixels and an optional note that can be changed until the message is sent, finds which parts of the picture's source file lie under a mark, and offers the layer on which the user draws the marks, shows their notes, and changes a note, and its tool bar. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AnnotationSubsystem/AnnotationSubsystemTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AnnotationSubsystemUiTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/annotation-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/annotation-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/annotation-subsystem.yaml");
        }

        part markRegistry : MarkRegistry;
        part markSourceResolver : MarkSourceResolver;
        part annotationOverlay : AnnotationOverlay;
    }
}

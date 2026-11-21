package CanvasPilot_AnnotationOverlay {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_MarkRegistry::*;
    private import CanvasPilot_PreviewPane::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def AnnotationOverlay {
        doc /* The layer on which the user marks the shown picture. It offers a rectangle, an ellipse, a freehand, and an arrow tool in a tool bar; draws the chosen shape with the pointer or the keyboard in the pixels of the picture, shows it while it is drawn, and asks for an optional note of at most 200 characters; makes each finished mark in the mark registry, pinned to the picture that was shown when the shape was started, and reports it once; shows over the picture only the marks whose chips are in the message and that were drawn on exactly that picture and says how many are hidden; throws a shape away on Escape; offers its tools only while a picture is shown and the host allows marks; and tells the host to keep the picture from being dragged out while a tool is chosen. It does not know the composer. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/AnnotationOverlayStyle.cs",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/AnnotationOverlayView.axaml",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/AnnotationOverlayView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/AnnotationOverlayViewModel.cs",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/AnnotationToolbarView.axaml",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/AnnotationToolbarView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/AnnotationToolbarViewModel.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AnnotationSubsystem/AnnotationOverlayTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AnnotationOverlayViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/annotation-subsystem/annotation-overlay.md");
            verificationFiles = ("docs/verification/canvas-pilot/annotation-subsystem/annotation-overlay.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/annotation-subsystem/annotation-overlay.yaml");
        }

        // The longest note of a mark, in characters, that the note box allows.
        attribute maximumNoteLength : ScalarValues::Integer;

        // Makes every mark in the registry and shows the marks whose chips are in the message.
        dependency AnnotationOverlay to MarkRegistry;

        // Follows the shown picture and its changes, and is placed in the overlay and top bar slots of the pane.
        dependency AnnotationOverlay to PreviewPane;

        // Reports a mark that could not be made.
        dependency AnnotationOverlay to RollingFileLogger;
    }
}

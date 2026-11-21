package CanvasPilot {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;

    part def MarkImageComposer {
        doc /* Draws the marks of a message on the picture they were drawn on, each as a line of its shape in a color that belongs to its number over a white line, with a badge with its number at the top-left of the mark, and cuts out the area around each mark, with CanvasNet, without a window and without writing a file. The interface and the result records are documented as supporting types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/PayloadSubsystem/IMarkImageComposer.cs",
                "src/DemaConsulting.CanvasPilot/PayloadSubsystem/MarkImageComposer.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/PayloadSubsystem/MarkImageComposerTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/PayloadSubsystem/PayloadSubsystemFakes.cs"
            );
            designFiles = ("docs/design/canvas-pilot/payload-subsystem/mark-image-composer.md");
            verificationFiles = ("docs/verification/canvas-pilot/payload-subsystem/mark-image-composer.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/payload-subsystem/mark-image-composer.yaml");
        }

        // The margin around a mark in the area that is cut out for it, in pixels.
        attribute cropPadding : Integer;

        // The smallest size of the area that is cut out for a mark, in pixels.
        attribute minimumCropSize : Integer;

        // Draws an SVG document that embeds the picture and the marks with the CanvasNet viewer and encodes the result
        // with its PNG codec.
        dependency MarkImageComposer to Ots::CanvasNet;

        // Takes the PNG of the picture from the render service, so that pictures are encoded in one place.
        dependency MarkImageComposer to ImageRenderService;

        // Draws the geometry of the marks.
        dependency MarkImageComposer to MarkRegistry;
    }
}

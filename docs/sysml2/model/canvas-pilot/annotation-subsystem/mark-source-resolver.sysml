package CanvasPilot {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;

    part def MarkSourceResolver {
        doc /* Finds which parts of a picture's source file lie under a mark, from the element positions and anchors that the renderer reported with the picture: nodes and edges of a diagram, marks and axes of a chart, anchors of a model. It applies the rules of the mark's shape, orders the parts by how much of them the mark covers, keeps at most 25, and says that the picture cannot tell, and why, when it reported no positions. The reference and result records are documented as supporting value types in this unit's design. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/MarkSourceResolver.cs",
                "src/DemaConsulting.CanvasPilot/AnnotationSubsystem/SourceElementReference.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AnnotationSubsystem/MarkSourceResolverTests.cs");
            designFiles = ("docs/design/canvas-pilot/annotation-subsystem/mark-source-resolver.md");
            verificationFiles = ("docs/verification/canvas-pilot/annotation-subsystem/mark-source-resolver.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/annotation-subsystem/mark-source-resolver.yaml");
        }

        // The most elements reported for one mark.
        attribute maximumElements : Integer;

        // Reads the geometry and the picture of a mark.
        dependency MarkSourceResolver to MarkRegistry;

        // Reads the element positions and anchors that the picture carries.
        dependency MarkSourceResolver to ImageRenderService;
    }
}

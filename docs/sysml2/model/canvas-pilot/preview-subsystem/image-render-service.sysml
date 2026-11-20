package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ImageRenderService {
        doc /* Draws an image source file with the CanvasNet viewer in the background, confined to the project folder, returns the pixels, notes, or a coded error, and encodes pictures as PNG. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/IImageRenderService.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/ImageRenderService.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/RenderResult.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/ImageRenderServiceTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/RenderResultTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/preview-subsystem/image-render-service.md");
            verificationFiles = ("docs/verification/canvas-pilot/preview-subsystem/image-render-service.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/preview-subsystem/image-render-service.yaml");
        }

        // Draws files with the CanvasNet viewer and encodes PNG with its codec; no other unit draws files with it (the
        // MarkImageComposer draws only an SVG document of its own).
        dependency ImageRenderService to Ots::CanvasNet;

        // Reports files that could not be drawn to the application log.
        dependency ImageRenderService to RollingFileLogger;
    }
}

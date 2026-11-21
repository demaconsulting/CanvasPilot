package CanvasPilot_ImageDragSource {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ImageExporter::*;
    private import CanvasPilot_ImageRenderService::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def ImageDragSource {
        doc /* Prepares the shown picture for dragging it into another program by writing it as a PNG file, named after its source file and the start of its pixel fingerprint, in a folder of its own, and removes the files it wrote. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/PreviewSubsystem/ImageDragSource.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/ImageDragSourceTests.cs");
            designFiles = ("docs/design/canvas-pilot/preview-subsystem/image-drag-source.md");
            verificationFiles = ("docs/verification/canvas-pilot/preview-subsystem/image-drag-source.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/preview-subsystem/image-drag-source.yaml");
        }

        // Encodes the picture as PNG through the render service.
        dependency ImageDragSource to ImageRenderService;

        // Takes the stem of the file name from the name that the exporter suggests for the picture.
        dependency ImageDragSource to ImageExporter;

        // Keeps every file name inside the drag folder.
        dependency ImageDragSource to PathHelpers;

        // Reports files that could not be written or deleted to the application log.
        dependency ImageDragSource to RollingFileLogger;
    }
}

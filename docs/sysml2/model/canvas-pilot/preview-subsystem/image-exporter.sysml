package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ImageExporter {
        doc /* Saves the shown picture as a PNG file the user chooses or copies it to the clipboard, and is available only while a picture is shown. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/ImageExporter.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/IImageExporter.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/IExportDialogs.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/ExportPreset.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/IExportPresetSource.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/ImageExporterTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/PreviewSubsystemFakes.cs"
            );
            designFiles = ("docs/design/canvas-pilot/preview-subsystem/image-exporter.md");
            verificationFiles = ("docs/verification/canvas-pilot/preview-subsystem/image-exporter.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/preview-subsystem/image-exporter.yaml");
        }

        // Encodes the shown picture as PNG through the render service.
        dependency ImageExporter to ImageRenderService;

        // Reports exports and failures to the application log.
        dependency ImageExporter to RollingFileLogger;
    }
}

package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ImageExporter {
        doc /* Saves the shown picture as a PNG file the user chooses or copies it to the clipboard, and is available only while a picture is shown. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/PreviewSubsystem/ImageExporter.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/ImageExporterTests.cs");
            designFiles = ("docs/design/canvas-pilot/preview-subsystem/image-exporter.md");
            verificationFiles = ("docs/verification/canvas-pilot/preview-subsystem/image-exporter.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/preview-subsystem/image-exporter.yaml");
        }

        // Encodes the shown picture as PNG through the render service.
        dependency ImageExporter to ImageRenderService;

        // Plugs into the image menu through the shell's exporter seam and asks the user through the dialog service.
        dependency ImageExporter to MainWindowShell;

        // Reports exports and failures to the application log.
        dependency ImageExporter to RollingFileLogger;
    }
}

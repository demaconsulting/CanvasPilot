package CanvasPilot_PreviewSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ExportPresetDialog::*;
    private import CanvasPilot_ImageDragSource::*;
    private import CanvasPilot_ImageExporter::*;
    private import CanvasPilot_ImageRenderService::*;
    private import CanvasPilot_PreviewPane::*;

    part def PreviewSubsystem {
        doc /* Draws the selected image source file with CanvasNet in the background, shows the picture with its errors and notes, keeps it current as files change, hosts a layer over it, and exports it as a PNG file, to the clipboard, or by dragging it out. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/PreviewSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/preview-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/preview-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/preview-subsystem.yaml");
        }

        part imageRenderService : ImageRenderService;
        part previewPane : PreviewPane;
        part imageExporter : ImageExporter;
        part exportPresetDialog : ExportPresetDialog;
        part imageDragSource : ImageDragSource;
    }
}

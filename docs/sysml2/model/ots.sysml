package CanvasPilot {
    private import SoftwareItemMetadata::*;

    package Ots {
        part def CanvasNet {
            doc /* DemaConsulting.CanvasNet.Viewer NuGet dependency (with the DemaConsulting.CanvasNet core library it brings) that draws SVG, diagram, chart, model, document, and raster files to pixels and encodes PNG. */

            metadata info : SoftwareInfo {
                designFiles = ("docs/design/ots/canvasnet.md");
                verificationFiles = ("docs/verification/ots/canvasnet.md");
                requirementFiles = ("docs/reqstream/ots/canvasnet.yaml");
            }
        }
    }
}

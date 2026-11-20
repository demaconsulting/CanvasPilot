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

        part def Speech {
            doc /* DemaConsulting.Speech NuGet dependency, with its DemaConsulting.Speech.Sherpa model package, that lists audio devices, lists and downloads speech models, and describes their tunable parameters. */

            metadata info : SoftwareInfo {
                testFiles = ("test/OtsSoftwareTests/SpeechTests.cs");
                designFiles = ("docs/design/ots/speech.md");
                verificationFiles = ("docs/verification/ots/speech.md");
                requirementFiles = ("docs/reqstream/ots/speech.yaml");
            }
        }
        part def WiX {
            doc /* WiX Toolset v5 (WixToolset.Sdk and the Util extension) that builds the published win-x64 application into the Windows Installer (MSI) package; the installer project is built by CI and is not part of the solution. */

            metadata info : SoftwareInfo {
                sourceFiles = (
                    "src/DemaConsulting.CanvasPilot.Installer/DemaConsulting.CanvasPilot.Installer.wixproj",
                    "src/DemaConsulting.CanvasPilot.Installer/Package.wxs"
                );
                designFiles = ("docs/design/ots/wix.md");
                verificationFiles = ("docs/verification/ots/wix.md");
                requirementFiles = ("docs/reqstream/ots/wix.yaml");
            }
        }
    }
}

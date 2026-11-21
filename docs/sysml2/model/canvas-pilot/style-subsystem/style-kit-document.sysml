package CanvasPilot_StyleKitDocument {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;

    part def StyleKitDocument {
        doc /* Holds the values of a style kit and the format of its file: reads a kit from a style file and from its folder with a message for each wrong property, checks a kit that is built in memory with the same rules, warns about fonts that not every format draws, bounds the size of every part of a kit, and writes a kit as canonical text. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleColors.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleExample.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleFonts.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleGuidanceText.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleIssue.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKit.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitLocation.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitParseResult.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitReader.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitSerializer.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleMermaid.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleSizePreset.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleKitDocumentTests.cs");
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-kit-document.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-kit-document.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-kit-document.yaml");
        }

        // Classifies the first family of a font list as a family that CanvasNet draws the same on every machine or not.
        dependency StyleKitDocument to CanvasNet;
    }
}

package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def StyleChecker {
        doc /* Measures the colors of a rendered picture against the palette of a style kit with a histogram of 32 levels per channel and the CIE76 color difference in CIELAB, sorting the pixels into palette colors, blends of two palette colors at edges, and colors that are off the palette, and reports the shares, the largest off-palette colors with their nearest palette color, and whether the picture passes, as notes and as text, the same for the same pixels every time. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/ColorMath.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleCheckOptions.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleCheckReport.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleChecker.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleCheckerTests.cs");
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-checker.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-checker.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-checker.yaml");
        }

        // Reads the colors of the palette of the kit.
        dependency StyleChecker to StyleKitDocument;

        // Takes the pixels of a rendered picture and writes the report as notes of a picture.
        dependency StyleChecker to ImageRenderService;
    }
}

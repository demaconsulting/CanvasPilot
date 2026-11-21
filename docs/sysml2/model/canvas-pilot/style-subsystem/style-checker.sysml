package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def StyleChecker {
        doc /* Measures the colors of a rendered picture against the palette of a style kit with a histogram of 32 levels per channel and the CIE76 color difference in CIELAB, sorting the pixels into palette colors, blends of two palette colors at edges, and colors that are off the palette, and reports the shares, the largest off-palette colors with their nearest palette color, and whether the picture passes, as notes and as text, the same for the same pixels every time; the command that the Image menu runs checks the shown picture against the kit of the project on a worker thread and shows the notes among the notes of the picture. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/ColorMath.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/ICheckAgainstStyle.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleCheckCommand.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleCheckOptions.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleCheckReport.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleChecker.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleCheckerTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleCheckCommandTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-checker.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-checker.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-checker.yaml");
        }

        // Reads the colors of the palette of the kit.
        dependency StyleChecker to StyleKitDocument;

        // Takes the pixels of a rendered picture and writes the report as notes of a picture.
        dependency StyleChecker to ImageRenderService;

        // Takes the shown picture and gives the notes of the check to the list of notes of the picture, through the
        // interfaces (IRenderedImageSource and IRenderNoteSink) that the Preview subsystem owns and the pane implements.
        dependency StyleChecker to PreviewPane;

        // Reads the kit that the open project uses for the check that the user asks for.
        dependency StyleChecker to StyleSelection;
    }
}

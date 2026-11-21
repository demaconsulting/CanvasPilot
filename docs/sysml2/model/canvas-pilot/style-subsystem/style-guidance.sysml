package CanvasPilot_StyleGuidance {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_StyleKitCatalog::*;
    private import CanvasPilot_StyleKitDocument::*;

    part def StyleGuidance {
        doc /* Writes the style of a kit as a block of Markdown text of at most 4,000 characters for an assistant, cut in a fixed order of steps when the kit is large and ending with a line that says so, and as single whole sections, with the colors, the fonts, the snippets for Mermaid and Vega-Lite, the lists, the export sizes, and the full paths of the example images, and with free text of the kit made safe. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleGuidance.cs");
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleGuidanceTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/Golden/clean-guidance.txt"
            );
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-guidance.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-guidance.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-guidance.yaml");
        }

        // Reads the values of the kit that it writes as text.
        dependency StyleGuidance to StyleKitDocument;

        // Takes the kit with the place and the folder it was found in.
        dependency StyleGuidance to StyleKitCatalog;
    }
}

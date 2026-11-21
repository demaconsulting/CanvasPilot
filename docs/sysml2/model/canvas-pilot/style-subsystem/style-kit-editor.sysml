package CanvasPilot_StyleKitEditor {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_StyleKitCatalog::*;
    private import CanvasPilot_StyleKitDocument::*;
    private import CanvasPilot_StylePicker::*;

    part def StyleKitEditor {
        doc /* The editor of one style kit of the user or of the project: a draft of every part of the kit (the name, the palette with colors that are set as text or with red, green, and blue values, the fonts, the numbers, the Mermaid theme and its variables, the Vega-Lite configuration, the advice, the export sizes, and the example images with their captions), the problems of the draft with the same messages as the reader of the style file, and a save that refuses a draft with an error, copies the chosen example files in, writes the kit, and deletes the removed examples, leaving the disk as it was when the user cancels. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/ColorEditorView.axaml",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/ColorEditorView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleColorRowViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleExampleRowViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitEditorView.axaml",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitEditorView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitEditorViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleNameValueRowViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StylePresetRowViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleTextRowViewModel.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleKitEditorTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/StyleKitEditorViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-kit-editor.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-kit-editor.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-kit-editor.yaml");
        }

        // Checks the draft with the rules of the style file and writes the kit by the same format.
        dependency StyleKitEditor to StyleKitDocument;

        // Writes the kit of the user or of the project through the catalog.
        dependency StyleKitEditor to StyleKitCatalog;

        // Uses the words for the places of kits.
        dependency StyleKitEditor to StylePicker;
    }
}

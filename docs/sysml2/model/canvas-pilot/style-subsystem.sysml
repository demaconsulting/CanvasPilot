package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def StyleSubsystem {
        doc /* Holds the look that pictures should have as style kits: folders with a style file and example images that are found in the places that ship with the program, that the user keeps, that a project holds, and that a team shares, the kit that a project uses, the export sizes, the guidance and tools for an assistant, and a check of a picture against the palette of a kit. It is kept apart from every provider of a language model. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/style-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem.yaml");
        }

        part styleKitDocument : StyleKitDocument;
        part styleKitCatalog : StyleKitCatalog;
        part styleKitArchive : StyleKitArchive;
        part styleSelection : StyleSelection;
        part styleGuidance : StyleGuidance;
        part styleChecker : StyleChecker;
        part styleTools : StyleTools;
        part stylePicker : StylePicker;
        part styleSettingsPage : StyleSettingsPage;
        part styleKitEditor : StyleKitEditor;
    }
}

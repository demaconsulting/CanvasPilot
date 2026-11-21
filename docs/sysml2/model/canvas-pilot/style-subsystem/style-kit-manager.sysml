package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def StyleKitManager {
        doc /* The window in which the user sees the style kits that were found, in groups by place with notes about places that cannot be read and kits that cannot be used, with a preview of the kit that is chosen (the colors with their roles, the fonts, the numbers, the advice, the export sizes, the warnings, the text that an assistant would be given, and small pictures of the example images that are drawn from the folder of the kit and kept in a cache), and the commands that apply to it: use it in the project, make a new kit or a copy, edit, delete, export and import as zip files, copy into the project, and read the places again, with the commands that would change a kit of the program or of a team unavailable. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/IStyleKitDialogs.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitGroupViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitItemViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitManagerView.axaml",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitManagerView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitManagerViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleSwatch.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleThumbnail.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleThumbnailCache.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleThumbnailViewModel.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleKitManagerTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/StyleKitManagerViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-kit-manager.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-kit-manager.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-kit-manager.yaml");
        }

        // Lists the kits and the places and makes, copies, saves, and deletes kits of the user and of the project.
        dependency StyleKitManager to StyleKitCatalog;

        // Exports a kit as a zip file and imports one.
        dependency StyleKitManager to StyleKitArchive;

        // Makes the kit that is chosen the style of the open project and marks the kit that is in use.
        dependency StyleKitManager to StyleSelection;

        // Opens the editor on a kit.
        dependency StyleKitManager to StyleKitEditor;

        // Shows the text that an assistant would be given about the kit.
        dependency StyleKitManager to StyleGuidance;

        // Uses the words for the places of kits.
        dependency StyleKitManager to StylePicker;

        // Says whether a project is open.
        dependency StyleKitManager to ProjectService;

        // Draws the example images as small pictures.
        dependency StyleKitManager to ImageRenderService;
    }
}

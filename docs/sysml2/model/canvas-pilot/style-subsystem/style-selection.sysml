package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def StyleSelection {
        doc /* Finds the style kit that the open project uses from the id in the project file through the catalog of kits, keeps a new choice in the project file, starts a new project with the default kit of the user, and supplies the export sizes of the kit and of the application to the export dialog of the Preview subsystem. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/IActiveStyle.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleSelection.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleSelectionTests.cs");
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-selection.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-selection.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-selection.yaml");
        }

        // Reads the open project and keeps the choice of the kit in its file.
        dependency StyleSelection to ProjectService;

        // Finds the kit for the id in the project file.
        dependency StyleSelection to StyleKitCatalog;

        // Reads the default kit of the user.
        dependency StyleSelection to SettingsService;

        // Supplies the export sizes through the interface that the exporter owns.
        dependency StyleSelection to ImageExporter;
    }
}

package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def StyleKitCatalog {
        doc /* Finds the style kits in the kits that come with the program, the folder of the user, the styles folder of the open project, and the shared folders of the settings, chooses the kit of the most local place for an id, reads shared folders on workers with a time limit, keeps the kits of the program and the shared kits read only, and creates, saves, duplicates, copies, and deletes the kits of the user and of the project. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/IStyleKitCatalog.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleBrokenKit.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitCatalog.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitEntry.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitScanner.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleLocationStatus.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleKitCatalogTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleSubsystemFakes.cs"
            );
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-kit-catalog.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-kit-catalog.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-kit-catalog.yaml");
        }

        // Reads and writes the style file of each kit and its examples folder.
        dependency StyleKitCatalog to StyleKitDocument;

        // Learns which project is open, and when it changes, to read its styles folder.
        dependency StyleKitCatalog to ProjectService;

        // Reads the shared folders and learns when they change.
        dependency StyleKitCatalog to SettingsService;

        // Resolves the folders of kits and the files that are copied.
        dependency StyleKitCatalog to PathHelpers;

        // Reports kits that cannot be used and folders that cannot be read to the application log.
        dependency StyleKitCatalog to RollingFileLogger;
    }
}

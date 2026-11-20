package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ProjectExplorer {
        doc /* Lists, watches, selects, renames, and deletes the image source files in the open project's folder. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IImageSourceFilter.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IRefreshScheduler.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectFileItem.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectExplorerViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectExplorerView.axaml",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectExplorerView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ProjectSubsystem/ProjectExplorerTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ProjectSubsystem/ProjectSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ProjectExplorerViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/project-subsystem/project-explorer.md");
            verificationFiles = ("docs/verification/canvas-pilot/project-subsystem/project-explorer.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/project-subsystem/project-explorer.yaml");
        }

        // Uses the project file extension, the data folder name, and the file name rules of the project service.
        dependency ProjectExplorer to ProjectService;

        // Resolves renamed file paths with the safe path-combination utility.
        dependency ProjectExplorer to PathHelpers;

        // Uses the extension list of CanvasNet's viewer to decide which files are image sources.
        dependency ProjectExplorer to Ots::CanvasNet;

        // Reports failed file operations to the application log.
        dependency ProjectExplorer to RollingFileLogger;
    }
}

package CanvasPilot_ProjectExplorer {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_RollingFileLogger::*;
    private import OtsDependencies::*;

    part def ProjectExplorer {
        doc /* Shows the open project's folder as a tree of folders, image source files, and assets, watches it, remembers the selection and the expanded folders, creates, adds, renames, and deletes files, and asks for the thumbnails of the drawings. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IImageSourceFilter.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IRefreshScheduler.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IThumbnailSource.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectFileItem.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectNodeViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectExplorerViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectExplorerView.axaml",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectExplorerView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ProjectSubsystem/ProjectExplorerTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ProjectSubsystem/ProjectSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ProjectExplorerViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ThumbnailViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/project-subsystem/project-explorer.md");
            verificationFiles = ("docs/verification/canvas-pilot/project-subsystem/project-explorer.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/project-subsystem/project-explorer.yaml");
        }

        // Uses the project file extension, the data folder name, and the file name rules of the project service.
        dependency ProjectExplorer to ProjectService;

        // Resolves renamed and created file paths with the safe path-combination utility and names copies with its free name finder.
        dependency ProjectExplorer to PathHelpers;

        // Uses the extension list of CanvasNet's viewer to decide which files are image sources.
        dependency ProjectExplorer to CanvasNet;

        // Reports failed file operations to the application log.
        dependency ProjectExplorer to RollingFileLogger;
    }
}

package CanvasPilot_ProjectService {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def ProjectService {
        doc /* Creates, opens, saves, and closes projects stored as a folder with a versioned project file. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IProjectService.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectDocument.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectNameRules.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/ProjectService.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ProjectSubsystem/ProjectServiceTests.cs");
            designFiles = ("docs/design/canvas-pilot/project-subsystem/project-service.md");
            verificationFiles = ("docs/verification/canvas-pilot/project-subsystem/project-service.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/project-subsystem/project-service.yaml");
        }

        // Resolves project and temporary file paths with the safe path-combination utility.
        dependency ProjectService to PathHelpers;

        // Reports refused and failed operations to the application log.
        dependency ProjectService to RollingFileLogger;
    }
}

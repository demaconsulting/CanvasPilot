package CanvasPilot_ProjectSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_NewProjectDialog::*;
    private import CanvasPilot_ProjectExplorer::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_RecentProjects::*;

    part def ProjectSubsystem {
        doc /* Projects as folders with a project file, the recent projects list, the New Project dialog, and the project explorer. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ProjectSubsystem/ProjectSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/project-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/project-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/project-subsystem.yaml");
        }

        part projectService : ProjectService;
        part recentProjects : RecentProjects;
        part newProjectDialog : NewProjectDialog;
        part projectExplorer : ProjectExplorer;
    }
}

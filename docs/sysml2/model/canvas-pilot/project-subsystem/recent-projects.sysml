package CanvasPilot_RecentProjects {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_SettingsService::*;

    part def RecentProjects {
        doc /* Keeps the most-recently-used project file list, with the time of each opening and the picture last shown in each project (relative to its folder, never in the project), in the application settings. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IRecentProjects.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/RecentProjects.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ProjectSubsystem/RecentProjectsTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/WelcomeTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/project-subsystem/recent-projects.md");
            verificationFiles = ("docs/verification/canvas-pilot/project-subsystem/recent-projects.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/project-subsystem/recent-projects.yaml");
        }

        // Reads and updates the list through the settings service.
        dependency RecentProjects to SettingsService;
    }
}

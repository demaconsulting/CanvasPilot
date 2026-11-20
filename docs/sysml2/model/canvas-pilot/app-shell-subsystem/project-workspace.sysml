package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ProjectWorkspace {
        doc /* The main window's content area: the project explorer, the preview area, and the confirmed delete and rename of project files. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProjectWorkspaceViewModel.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProjectWorkspaceView.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProjectWorkspaceView.axaml.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/ProjectWorkspaceTests.cs");
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/project-workspace.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/project-workspace.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/project-workspace.yaml");
        }

        // Hosts the explorer and drives its selection, delete, and rename.
        dependency ProjectWorkspace to ProjectExplorer;
    }
}

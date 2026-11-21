package CanvasPilot_ProjectWorkspace {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ChatPanel::*;
    private import CanvasPilot_ProjectExplorer::*;

    part def ProjectWorkspace {
        doc /* The main window's content area: the project explorer, the preview area, the chat column, and the confirmed delete and rename of project files. */

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

        // Hosts the chat panel in the chat column and makes the dialog service its launcher of the payload inspector.
        dependency ProjectWorkspace to ChatPanel;
    }
}

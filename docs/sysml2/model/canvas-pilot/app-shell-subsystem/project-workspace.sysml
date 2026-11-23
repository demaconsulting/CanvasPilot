package CanvasPilot_ProjectWorkspace {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ChatPanel::*;
    private import CanvasPilot_ProjectExplorer::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_ThemeResources::*;

    part def ProjectWorkspace {
        doc /* The main window's content area: the project explorer, the preview area, the chat column that can be hidden and brought back, the widths of the columns that the user chose and that are fitted to the window, the rules that fit a saved window to the screens, and the confirmed delete, the rename, and the named creation of project files and folders. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProjectWorkspaceViewModel.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProjectWorkspaceView.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProjectWorkspaceView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/WindowLayout.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/ProjectWorkspaceTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/WindowLayoutTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/WorkspaceLayoutViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/project-workspace.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/project-workspace.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/project-workspace.yaml");
        }

        // Hosts the explorer and drives its selection, delete, rename, and creation of files and folders.
        dependency ProjectWorkspace to ProjectExplorer;

        // Hosts the chat panel in the chat column, makes the dialog service its launcher of the payload inspector and its
        // picker of attachments, and hears when the user hides the chat.
        dependency ProjectWorkspace to ChatPanel;

        // Takes the limits and default widths of the columns and the saved layout of the window from the window settings.
        dependency ProjectWorkspace to SettingsService;

        // Takes the pane colors, the splitter look, and the look of the restore button from the tokens and control themes.
        dependency ProjectWorkspace to ThemeResources;
    }
}

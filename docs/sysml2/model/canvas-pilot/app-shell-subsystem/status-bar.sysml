package CanvasPilot_StatusBar {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PreviewPane::*;
    private import CanvasPilot_ProjectWorkspace::*;
    private import CanvasPilot_PushToTalkBinding::*;

    part def StatusBar {
        doc /* The bar at the bottom of the main window: always shown at one height, it follows the workspace and its preview pane and says with words and an icon that no project is open or no picture is selected, or what the shown picture is (file, size, format, render time, notes), that it is being rendered, or that the render failed, and, while a project is open, when the project file was saved (only once it has a saved time, from the modified time of the file, never a claim that the work is saved) and which key is held to talk (the key in effect, which follows the speech settings); one sentence sums it up for a screen reader. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/StatusBarViewModel.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/StatusBarView.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/StatusBarView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/PushToTalkHintsTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/StatusBarSavedTimeTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/StatusBarTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/status-bar.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/status-bar.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/status-bar.yaml");
        }

        // Follows whether a project is open and which preview pane the workspace holds.
        dependency StatusBar to ProjectWorkspace;

        // Reads the busy, error, and image state, the file name, the size, the format, the render time, and the notes of the picture.
        dependency StatusBar to PreviewPane;

        // Names the key that is held to talk, which follows the speech settings.
        dependency StatusBar to PushToTalkBinding;
    }
}

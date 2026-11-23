package CanvasPilot_Welcome {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ProjectExplorer::*;
    private import CanvasPilot_ProviderPill::*;
    private import CanvasPilot_PushToTalkBinding::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def Welcome {
        doc /* The screen that fills the main window while no project is open: the name, tagline, and three steps of the application (the first names the key that is held to talk, which follows the settings), the New project and Open project buttons, up to six cards of the most recent projects (picture, name, folder, when it was opened in words, how many pictures it holds) that open the project and are moved between with the arrow keys and removed from the list only with Delete or a menu, a card that invites the first project, and a notice with an Open Settings button while the AI provider needs something or cannot be used. The counting of the pictures of a project and the drawing of the card pictures in the background are supporting types of this unit. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/WelcomeViewModel.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/WelcomeView.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/WelcomeView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/RecentProjectItem.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProjectPictureCounter.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/RecentThumbnailLoader.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/WelcomeTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/WelcomeViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/welcome.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/welcome.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/welcome.yaml");
        }

        // Follows the provider pill of the shell to show the notice about the AI provider and takes its sentence.
        dependency Welcome to ProviderPill;

        // Counts the pictures of a project with the filter that tells which files are pictures, which the explorer owns.
        dependency Welcome to ProjectExplorer;

        // Logs a card picture that could not be drawn.
        dependency Welcome to RollingFileLogger;

        // Names the key that is held to talk in the first step of how the application works, which follows the speech settings.
        dependency Welcome to PushToTalkBinding;
    }
}

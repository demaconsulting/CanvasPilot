package CanvasPilot_KnowledgeBrowser {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_KnowledgeCatalog::*;
    private import CanvasPilot_KnowledgeMarkdown::*;
    private import CanvasPilot_KnowledgeSearch::*;
    private import CanvasPilot_RollingFileLogger::*;
    private import ScalarValues::Integer;

    part def KnowledgeBrowser {
        doc /* The window in which the user reads the drawing knowledge: a search box, the cards as a tree or the hits of the search, and the chosen card with its text drawn from Markdown, the cards below it and its related cards as links, and its examples with a button that copies each. It reads the catalog and changes nothing in it, reports a clipboard that cannot be reached in a status line, and is shown through a launcher seam that the main window implements as a window of its own that does not block it. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/IKnowledgeBrowserLauncher.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/IKnowledgeClipboard.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeBrowserView.axaml",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeBrowserView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeBrowserViewModel.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeNodeViewModel.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeLinkViewModel.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeExampleViewModel.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/MarkdownBlocksPanel.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/WindowKnowledgeClipboard.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeBrowserTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/RecordingKnowledgeClipboard.cs",
                "test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/FakeKnowledgeCatalog.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/KnowledgeBrowserViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem/knowledge-browser.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem/knowledge-browser.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem/knowledge-browser.yaml");
        }

        // The fewest characters of the search box that start a search.
        attribute minimumSearchLength : Integer;

        // Shows the cards, their children, their related cards, and their examples.
        dependency KnowledgeBrowser to KnowledgeCatalog;

        // Finds the cards that fit the words of the search box.
        dependency KnowledgeBrowser to KnowledgeSearch;

        // Reads the text of a card into the blocks that the window draws.
        dependency KnowledgeBrowser to KnowledgeMarkdown;

        // Writes a failure to reach the clipboard.
        dependency KnowledgeBrowser to RollingFileLogger;
    }
}

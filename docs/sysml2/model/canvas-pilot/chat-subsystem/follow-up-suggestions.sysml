package CanvasPilot_FollowUpSuggestions {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ProjectExplorer::*;

    part def FollowUpSuggestions {
        doc /* Decides which example prompts the chat panel offers from three facts: whether the project has a drawing, whether a picture is shown, and whether the AI copilot works. While the project has no drawing it plans three ways to start one (a flowchart, a title card, and a chart); once it has one it plans three ways to refine it (a larger title, a legend, and the style kit of the user) and adds the export of the picture only while a picture is shown; and while the copilot works it plans none. It also finds out whether a project folder holds a drawing, with the filter that the project explorer owns: a file that the explorer lists as an image source at the top of the folder or a few levels inside it, leaving out the folders of the attachments and of the style kits at the top, the hidden folders, folders deeper than the search goes, and a folder that cannot be read, and stopping at the first drawing. It writes nothing, calls nothing, and keeps no state; the chat panel shows what it plans. The search of the folder is a supporting type of this unit, which has no part definition of its own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/FollowUpSuggestions.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ProjectDrawingProbe.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/FollowUpSuggestionsTests.cs");
            designFiles = ("docs/design/canvas-pilot/chat-subsystem/follow-up-suggestions.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem/follow-up-suggestions.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem/follow-up-suggestions.yaml");
        }

        // Tells which files are drawings with the filter of image sources (IImageSourceFilter) that the project explorer owns.
        dependency FollowUpSuggestions to ProjectExplorer;
    }
}

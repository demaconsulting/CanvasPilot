package CanvasPilot_ReplyMarkdown {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;

    part def ReplyMarkdown {
        doc /* Reads the Markdown text of a completed reply of the copilot into headings, paragraphs, list items, fenced blocks of code, and runs of bold, italic, and code text, without a Markdown library, keeping every other construct (such as a link, a table, or a quote) as the text that was written, and draws those blocks as selectable text, with the code blocks on a raised surface of the theme, in a control that the chat panel uses for the replies of the copilot. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ReplyMarkdown.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/MarkdownBlock.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/HeadingBlock.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ParagraphBlock.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ListItemBlock.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/CodeBlock.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/MarkdownSpan.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/MarkdownSpanStyle.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/MarkdownBlocksPanel.cs");
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ReplyMarkdownTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ReplyMarkdownViewTests.cs");
            designFiles = ("docs/design/canvas-pilot/chat-subsystem/reply-markdown.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem/reply-markdown.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem/reply-markdown.yaml");
        }

        // The deepest level of a heading that the unit reads.
        attribute maximumHeadingLevel : Integer;
    }
}

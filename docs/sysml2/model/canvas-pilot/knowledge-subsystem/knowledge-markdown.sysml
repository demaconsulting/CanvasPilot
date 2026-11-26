package CanvasPilot_KnowledgeMarkdown {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;

    part def KnowledgeMarkdown {
        doc /* Reads the small dialect of Markdown that the knowledge cards are written in, which the copilot writes its replies in, into headings, paragraphs, list items, fenced blocks of code, and runs of bold, italic, and code text, without a Markdown library and without losing text, and draws those blocks as selectable text, with the code blocks on a raised surface of the theme, in a control that the chat panel uses for the replies of the copilot. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeMarkdown.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/MarkdownBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/HeadingBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/ParagraphBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/ListItemBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/CodeBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/MarkdownSpan.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/MarkdownSpanStyle.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/MarkdownBlocksPanel.cs");
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeMarkdownTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/KnowledgeMarkdownViewTests.cs");
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem/knowledge-markdown.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem/knowledge-markdown.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem/knowledge-markdown.yaml");
        }

        // The deepest level of a heading that the unit reads.
        attribute maximumHeadingLevel : Integer;
    }
}

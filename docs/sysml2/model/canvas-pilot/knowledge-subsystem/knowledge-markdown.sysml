package CanvasPilot {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;

    part def KnowledgeMarkdown {
        doc /* Reads the small dialect of Markdown that the knowledge cards are written in into headings, paragraphs, list items, fenced blocks of code, and runs of bold, italic, and code text that a window can draw, without a Markdown library and without losing text. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeMarkdown.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/MarkdownBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/HeadingBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/ParagraphBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/ListItemBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/CodeBlock.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/MarkdownSpan.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/MarkdownSpanStyle.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeMarkdownTests.cs");
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem/knowledge-markdown.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem/knowledge-markdown.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem/knowledge-markdown.yaml");
        }

        // The deepest level of a heading that the unit reads.
        attribute maximumHeadingLevel : Integer;
    }
}

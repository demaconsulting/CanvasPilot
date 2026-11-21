package CanvasPilot {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;

    part def KnowledgePromptBuilder {
        doc /* Writes the knowledge cards as plain text for the prompt of an assistant: a compact index with one line for each card that stays within a fixed number of characters, a card with the cards below it, the cards related to it, and its examples, an example in a fenced block, and the result of a search, the same for the same cards every time. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgePromptBuilder.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgePromptBuilderTests.cs");
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem/knowledge-prompt-builder.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem/knowledge-prompt-builder.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem/knowledge-prompt-builder.yaml");
        }

        // The most characters that the index of the catalog of the application may have.
        attribute indexBudgetCharacters : Integer;

        // Reads the cards, the cards below them, and the cards related to them from the catalog.
        dependency KnowledgePromptBuilder to KnowledgeCatalog;

        // Writes the results that the search finds.
        dependency KnowledgePromptBuilder to KnowledgeSearch;
    }
}

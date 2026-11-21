package CanvasPilot_KnowledgeTools {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_KnowledgeCatalog::*;
    private import CanvasPilot_KnowledgePromptBuilder::*;
    private import CanvasPilot_KnowledgeSearch::*;
    private import ScalarValues::String;

    part def KnowledgeTools {
        doc /* Describes the tool that gives an assistant the drawing knowledge, canvasnet_help, as plain values and a function that answers the index of the cards, a card, an example, or a search from arguments in JSON text, and never throws on bad arguments. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeTool.cs", "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeTools.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeToolsTests.cs");
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem/knowledge-tools.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem/knowledge-tools.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem/knowledge-tools.yaml");
        }

        // The name of the tool that gives the drawing knowledge.
        attribute helpToolName : String;

        // Reads the cards, their paths, and their examples from the catalog.
        dependency KnowledgeTools to KnowledgeCatalog;

        // Finds the cards that fit the words of a query and the suggestions for an unknown topic.
        dependency KnowledgeTools to KnowledgeSearch;

        // Writes the index, a card, an example, and the results of a search as text.
        dependency KnowledgeTools to KnowledgePromptBuilder;
    }
}

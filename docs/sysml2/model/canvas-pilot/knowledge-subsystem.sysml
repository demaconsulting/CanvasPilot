package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def KnowledgeSubsystem {
        doc /* Holds the knowledge of how to draw with the formats CanvasNet draws and how to work in a CanvasPilot project, as a tree of cards taken from the help of CanvasNet, without the cards about what the host program controls, and extended with cards of CanvasPilot. It is kept apart from every provider of a language model and has no user interface. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem.yaml");
        }

        part knowledgeCatalog : KnowledgeCatalog;
        part knowledgeSearch : KnowledgeSearch;
        part knowledgePromptBuilder : KnowledgePromptBuilder;
    }
}

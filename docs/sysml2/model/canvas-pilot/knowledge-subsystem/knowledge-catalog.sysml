package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def KnowledgeCatalog {
        doc /* Builds the tree of knowledge cards of the application from the help cards of CanvasNet, extended with the cards of CanvasPilot that are embedded in the application and filtered to leave out the cards about what the host program controls, copies them into values of its own with the links of each body written as card identifiers, and finds cards by path or other name and examples by name. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/IKnowledgeCatalog.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeCard.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeCatalog.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeExample.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeExampleReference.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeCatalogTests.cs");
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem/knowledge-catalog.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem/knowledge-catalog.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem/knowledge-catalog.yaml");
        }

        // Reads the help cards of the library, extends them with the cards of the application, and filters them by tag.
        dependency KnowledgeCatalog to Ots::CanvasNet;
    }
}

package CanvasPilot_KnowledgeSkills {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_KnowledgeCatalog::*;
    private import CanvasPilot_KnowledgePromptBuilder::*;
    private import ScalarValues::String;

    part def KnowledgeSkills {
        doc /* Describes the cards of a catalog as skills for an assistant: one plain value for each card with a name made from the identifier of the card, the title and summary as its description, and the card with its examples as its content, made as the skills are read. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeSkill.cs", "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeSkills.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeSkillsTests.cs");
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem/knowledge-skills.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem/knowledge-skills.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem/knowledge-skills.yaml");
        }

        // The start of the name of every skill.
        attribute namePrefix : String;

        // Reads the cards from the catalog.
        dependency KnowledgeSkills to KnowledgeCatalog;

        // Writes the content of each skill as the text of a card with its examples.
        dependency KnowledgeSkills to KnowledgePromptBuilder;
    }
}

package CanvasPilot_AgentInstructions {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import CanvasPilot_KnowledgePromptBuilder::*;
    private import CanvasPilot_StyleGuidance::*;
    private import CanvasPilot_SettingsService::*;

    part def AgentInstructions {
        doc /* Writes the instructions that the AI copilot is given for one project, in this order: the role prompt (an embedded Markdown resource of at most 3,000 characters that tells the copilot to draw with CanvasNet in the open project with only its tools, to call style_get before drawing and style_check after, to look at the picture with render_image after every change, to use project-relative paths and never invent a file, to read a file before replacing it because project_write overwrites with no backup, and to explain briefly), the index of the drawing knowledge, the facts of the project (name, folder, the head of the list of files limited in number and in size, and the name and id of the style kit, or the statement that there is none or that it is missing), and the style of the kit as the style guidance writes it. The full tier is written for every GitHub Copilot model and for an Ollama context window of at least 16,384 tokens; a smaller window gets the compact tier with the cards at the top of the index only, a shorter list of files, and a style without example images. The sizes are limits that its tests measure with the real cards and kits: 20,000 characters (full), 11,000 (compact), and 7,000 for the text of all tools together. Every value of the project is written on one line, and the text holds no setting, token, message, or file content. It holds the facts of the project and the tier as supporting types, which have no part definition of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentInstructions.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentProjectFacts.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/Instructions/RolePrompt.md"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentInstructionsTests.cs");
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/agent-instructions.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/agent-instructions.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/agent-instructions.yaml");
        }

        // Writes the index of the drawing knowledge that follows the role prompt, in full or for the top cards only.
        dependency AgentInstructions to KnowledgePromptBuilder;

        // Writes the style block of the active kit, with or without the example images.
        dependency AgentInstructions to StyleGuidance;

        // Takes the recommended minimum context window and the provider from the AI provider settings to choose the tier.
        dependency AgentInstructions to SettingsService;

        // The most characters of the instructions of the full tier.
        attribute fullBudgetCharacters : Integer;

        // The most characters of the instructions of the compact tier.
        attribute compactBudgetCharacters : Integer;

        // The most characters that the names, words, and schemas of all tools have together.
        attribute toolSchemaBudgetCharacters : Integer;

        // The smallest context window, in tokens, that gets the full tier.
        attribute fullTierMinimumContextTokens : Integer;
    }
}

package CanvasPilot_AgentSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_CopilotCliLocator::*;
    private import CanvasPilot_AgentReadiness::*;
    private import CanvasPilot_AgentToolRegistry::*;
    private import CanvasPilot_ProjectTools::*;
    private import CanvasPilot_AgentInstructions::*;

    part def AgentSubsystem {
        doc /* The AI copilot behind the chat: the provider-independent contract (IAgentService, the turn request with its restored history, the stream of events, and the readiness report) through which the chat runs a turn, stops it, and learns whether the chosen AI provider can be used, the locator of the GitHub Copilot CLI that the user installed, the readiness checks of both AI providers (Copilot in this build is gated and never ready), and the registry that builds the tools the copilot may use for one project, confined to the project folder, and the project tools with which the copilot lists, reads, writes, and draws the files of the project (links refused, files replaced directly, pictures kept in memory), and the instructions that the copilot is given (the role prompt, the knowledge index, the project facts, and the style, in a full and a compact tier with measured sizes). It has no user interface and none of its contract types names an AI library. The units that implement the contract are added to the model as they are built. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/agent-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem.yaml");
        }

        part agentContract : AgentContract;
        part copilotCliLocator : CopilotCliLocator;
        part agentReadiness : AgentReadiness;
        part agentToolRegistry : AgentToolRegistry;
        part projectTools : ProjectTools;
        part agentInstructions : AgentInstructions;
    }
}

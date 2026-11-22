package CanvasPilot_AgentSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_CopilotCliLocator::*;

    part def AgentSubsystem {
        doc /* The AI copilot behind the chat: the provider-independent contract (IAgentService, the turn request with its restored history, the stream of events, and the readiness report) through which the chat runs a turn, stops it, and learns whether the chosen AI provider can be used, and the locator of the GitHub Copilot CLI that the user installed. It has no user interface and none of its contract types names an AI library. The units that implement the contract are added to the model as they are built. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/agent-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem.yaml");
        }

        part agentContract : AgentContract;
        part copilotCliLocator : CopilotCliLocator;
    }
}

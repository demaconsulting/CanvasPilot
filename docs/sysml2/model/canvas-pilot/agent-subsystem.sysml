package CanvasPilot_AgentSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_CopilotCliLocator::*;
    private import CanvasPilot_CopilotRuntimeStarter::*;
    private import CanvasPilot_ProviderModelCatalog::*;
    private import CanvasPilot_AgentReadiness::*;
    private import CanvasPilot_AgentToolRegistry::*;
    private import CanvasPilot_ProjectTools::*;
    private import CanvasPilot_AgentInstructions::*;
    private import CanvasPilot_PayloadMessageMapper::*;
    private import CanvasPilot_AgentEventMapper::*;
    private import CanvasPilot_AgentProviderFactory::*;
    private import CanvasPilot_AgentService::*;

    part def AgentSubsystem {
        doc /* The AI copilot behind the chat: the provider-independent contract (IAgentService, the turn request with its restored history, the stream of events, and the readiness report) through which the chat runs a turn, stops it, and learns whether the chosen AI provider can be used, the locator of the GitHub Copilot CLI that the user installed, the starter of the Copilot runtime that runs the installed CLI as one shared runtime, the list of the models that the chosen provider offers with what each can do (and the policy of what the application needs of a model), the readiness checks of both AI providers (Copilot is ready when the runtime starts and is signed in, and its sentence names the account that the runtime signed in as; the model and the reasoning effort of the settings are validated against the catalog of the provider), and the registry that builds the tools the copilot may use for one project, confined to the project folder, and the project tools with which the copilot lists, reads, writes, and draws the files of the project (links refused, files replaced directly, pictures kept in memory), and the instructions that the copilot is given (the role prompt, the knowledge index, the project facts, and the style, in a full and a compact tier with measured sizes), and the mapper that turns the message that the user sent into the message of the model (the words, the described attachments, pictures, and marks, and the annotated pictures and close-ups as images), and the mapper that turns the updates of an agent into the events of a turn and the failures of a turn into sentences that say what to do, and the factory that makes the agent of a conversation for Ollama or for GitHub Copilot (on the shared Copilot runtime, with the model and the reasoning effort validated against the catalog of the provider, and with a typed status and no agent when the provider cannot be served), and the service that implements the contract (the conversation of the open project, made when the first message is sent and made again when the project or a relevant setting changes, one turn at a time, the data of the Copilot runtime kept below the local data of the user and never inside a project, and a log of metadata only). It has no user interface and none of its contract types names an AI library. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/agent-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem.yaml");
        }

        part agentContract : AgentContract;
        part copilotCliLocator : CopilotCliLocator;
        part copilotRuntimeStarter : CopilotRuntimeStarter;
        part providerModelCatalog : ProviderModelCatalog;
        part agentReadiness : AgentReadiness;
        part agentToolRegistry : AgentToolRegistry;
        part projectTools : ProjectTools;
        part agentInstructions : AgentInstructions;
        part payloadMessageMapper : PayloadMessageMapper;
        part agentEventMapper : AgentEventMapper;
        part agentProviderFactory : AgentProviderFactory;
        part agentService : AgentService;
    }
}

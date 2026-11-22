package CanvasPilot_AgentProviderFactory {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::String;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_AgentReadiness::*;
    private import CanvasPilot_CopilotCliLocator::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_RollingFileLogger::*;
    private import OtsDependencies::*;

    part def AgentProviderFactory {
        doc /* Makes the agent of a conversation for the AI provider that the settings choose, from the settings, the tools of the project, and the instructions. For Ollama it checks the settings first (an absolute http or https address, a model name, and a context window in range, otherwise the typed status NotConfigured with a sentence that says what to enter and no request), then builds an HTTP client on the address without a time limit, an Ollama client for the model, and a context sizing client around it so that the context window of the settings is sent as num_ctx on every request, and makes the agent with the chat client agent factory of the AI library; the result owns the clients and disposes them once. For GitHub Copilot it finds the CLI with the locator (a missing or too old CLI gets the sentence of the readiness check and the starter is not asked), asks the starter of the Copilot runtime to start the runtime once under a gate, keeps the runtime it gets, and makes agents on it with exactly the supplied tools, the instructions, the name CanvasPilot, the model of the settings (none when blank), and the project folder as the working directory, in one place that supplies no permission handler so that the default-deny handler of the AI library applies. The starter that this build ships is gated, so Copilot gets the typed status CopilotRuntimeUnavailable and no agent; a starter that fails, or that starts a runtime without a way to make agents, gets the same status and is asked again by the next request. The factory disposes the runtime it started once; a result never does. Its seam is IAgentFactory, with which tests put an agent on a scripted chat client, and the stand-in for the sealed runtime of the AI library is ICopilotAgentSource. The log gets metadata only. It holds the request and result types, the stand-in interface, and the class on the real runtime as supporting types, which have no part definition of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentProviderFactory.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/CopilotAgentSource.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/IAgentFactory.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentProviderFactoryTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentProviderTestDoubles.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/agent-provider-factory.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/agent-provider-factory.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/agent-provider-factory.yaml");
        }

        // Answers a request that cannot be served with the readiness report of the contract (a status and one sentence).
        dependency AgentProviderFactory to AgentContract;

        // Uses the starter of the Copilot runtime and its gated implementation, and the statuses and sentences of the readiness check.
        dependency AgentProviderFactory to AgentReadiness;

        // Finds the installed Copilot CLI and its version, which the starter needs to start the runtime.
        dependency AgentProviderFactory to CopilotCliLocator;

        // Reads the AI provider choices (provider, model, address, context window, CLI path) that decide the agent.
        dependency AgentProviderFactory to SettingsService;

        // Writes the metadata of each agent that is made to the log (hub supplier, omitted from the runtime view).
        dependency AgentProviderFactory to RollingFileLogger;

        // Makes the agents (for a chat client and for the Copilot runtime) and sizes the context window of the Ollama client.
        dependency AgentProviderFactory to AgentKit;

        // Talks to the Ollama server through its chat client.
        dependency AgentProviderFactory to OllamaSharp;

        // Returns the agent type that the conversation runs on (hub supplier, omitted from the runtime view).
        dependency AgentProviderFactory to AgentFramework;

        // Carries the tools as functions and the chat client of the Ollama agent (hub supplier, omitted from the runtime view).
        dependency AgentProviderFactory to ExtensionsAi;

        // The name that the agent of the application carries.
        attribute agentName : String;
    }
}

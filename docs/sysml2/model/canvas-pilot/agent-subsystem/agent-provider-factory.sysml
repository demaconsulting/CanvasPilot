package CanvasPilot_AgentProviderFactory {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::String;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_AgentReadiness::*;
    private import CanvasPilot_CopilotCliLocator::*;
    private import CanvasPilot_CopilotRuntimeStarter::*;
    private import CanvasPilot_ProviderModelCatalog::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_RollingFileLogger::*;
    private import OtsDependencies::*;

    part def AgentProviderFactory {
        doc /* Makes the agent of a conversation for the AI provider that the settings choose, from the settings, the tools of the project, and the instructions. For Ollama it checks the settings first (an absolute http or https address, a model name, and a context window in range, otherwise the typed status NotConfigured with a sentence that says what to enter and no request), then validates the model, the window, and the reasoning effort against the catalog of the server (a model that is not there, cannot call tools, cannot be used, offers no such effort, or was trained for fewer tokens than the window gives the typed status ModelSettingsInvalid with the sentence of the AI library and no agent; a server that does not describe its models leaves the typed model to be tried as it is), then builds an HTTP client on the address without a time limit, an Ollama client for the model, and a context sizing client around it so that the context window of the settings is sent as num_ctx on every request, and makes the agent with the chat client agent factory of the AI library and the validated selection, which adds the model and the thinking level to every request; the result owns the clients and disposes them once. For GitHub Copilot it finds the CLI with the locator (a missing or too old CLI gets the sentence of the readiness check and the starter is not asked), asks the starter of the Copilot runtime for the runtime on that CLI (shared with the readiness check and the model list and started once), validates the model and the reasoning effort against the catalog of that runtime (a model that is not offered, cannot be used, cannot call tools, or does not offer the effort gives ModelSettingsInvalid and no agent; no model named means the default model of the runtime), and makes agents on it with exactly the supplied tools, the instructions, the name CanvasPilot, the validated model selection, and the project folder as the working directory, with the default-deny permission handler of the AI library. A starter that says that the CLI is too old, that the runtime is not signed in, or that it could not be started, or that starts a runtime without a way to make agents, gives the matching typed status and no agent, and is asked again by the next request. The runtime belongs to the starter; the factory stops the starter, and so the runtime, once when it is disposed only if it owns the starter, and a result never does. Its seam is IAgentFactory, with which tests put an agent on a scripted chat client. The log gets metadata only. It holds the request and result types as supporting types, which have no part definition of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentProviderFactory.cs",
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

        // Uses the statuses and sentences of the readiness check for a Copilot that cannot be served.
        dependency AgentProviderFactory to AgentReadiness;

        // Asks the shared starter of the Copilot runtime for the runtime on the CLI that was found, and stops the starter that it owns.
        dependency AgentProviderFactory to CopilotRuntimeStarter;

        // Uses the policy of what the application needs of a model and the catalogs of the Ollama servers, to validate the model settings.
        dependency AgentProviderFactory to ProviderModelCatalog;

        // Finds the installed Copilot CLI and its version, which the starter needs to start the runtime.
        dependency AgentProviderFactory to CopilotCliLocator;

        // Reads the AI provider choices (provider, model, address, context window, CLI path) that decide the agent.
        dependency AgentProviderFactory to SettingsService;

        // Writes the metadata of each agent that is made to the log (hub supplier, omitted from the runtime view).
        dependency AgentProviderFactory to RollingFileLogger;

        // Makes the agents (for a chat client and for the Copilot runtime), sizes the context window of the Ollama client, and validates the model settings against a model catalog.
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

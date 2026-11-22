package CanvasPilot_ProviderModelCatalog {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import CanvasPilot_CopilotCliLocator::*;
    private import CanvasPilot_CopilotRuntimeStarter::*;
    private import CanvasPilot_AiProviderSettingsPage::*;
    private import OtsDependencies::*;

    part def ProviderModelCatalog {
        doc /* Lists the models that the AI provider of the settings offers, in the types of the application, for the AI Provider page of the Settings: it implements the IProviderModelSource interface that the Settings subsystem owns, so the page depends neither on this subsystem nor on the AI library. For GitHub Copilot it finds the CLI with the locator, asks the shared starter for the one running runtime (no second runtime is ever started for the list), and lists the catalog of that runtime, after a refresh when one is asked for. For Ollama it lists the catalog of the server at the address of the settings through the catalogs of Ollama (one catalog for the address that was asked for last, shared with the readiness check and the factory) within 15 seconds. Every model is listed, those that cannot be used as well, usable ones first and each group by name, with its id and name, whether it can call tools, take pictures, and reason, its largest context window, the reasoning efforts that it offers with the provider's own ids, its default effort, whether it can be used and why not, and the notes of the provider that ask for attention. A provider that cannot be asked (no or old CLI, a runtime that is not signed in, stopped, or without a catalog, an address that is not valid, a server that does not answer or answers something else, or a server without models) is a list with no models and one sentence, never an exception; only a cancellation is thrown. The unit also holds the policy of what the application needs of a model (ModelRequirements: tool calling is required, pictures are wanted and only warned about, and the choices of the settings become the model settings that the AI library validates) and the catalogs of the Ollama servers (IOllamaModelCatalogs with the real OllamaModelCatalogs), which the readiness check and the factory of the agents use as well, as supporting types without part definitions of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ModelRequirements.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/OllamaModelCatalogs.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ProviderModelCatalog.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentProviderTestDoubles.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/ProviderModelCatalogTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/provider-model-catalog.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/provider-model-catalog.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/provider-model-catalog.yaml");
        }

        // Finds the installed Copilot CLI and its version, which the starter needs to start (or reuse) the runtime whose catalog is listed.
        dependency ProviderModelCatalog to CopilotCliLocator;

        // Asks the shared starter of the Copilot runtime for the one running runtime and lists its model catalog.
        dependency ProviderModelCatalog to CopilotRuntimeStarter;

        // Implements the IProviderModelSource list of models that is documented with the AI Provider page of the settings dialog.
        dependency ProviderModelCatalog to AiProviderSettingsPage;

        // Lists and describes models with the model catalogs of the Copilot and Ollama packages and the model types of the core package.
        dependency ProviderModelCatalog to AgentKit;

        // Lists the models of an Ollama server through its client, in the Ollama model catalog.
        dependency ProviderModelCatalog to OllamaSharp;

        // The longest time, in seconds, that the Ollama server may take to list and describe its models.
        attribute ollamaListTimeoutSeconds : Integer;
    }
}

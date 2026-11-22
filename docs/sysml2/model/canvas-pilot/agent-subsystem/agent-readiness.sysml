package CanvasPilot_AgentReadiness {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Boolean;
    private import ScalarValues::Integer;
    private import ScalarValues::String;
    private import CanvasPilot_CopilotCliLocator::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_AiProviderSettingsPage::*;
    private import OtsDependencies::*;

    // What the starter of the Copilot runtime answered, as the CopilotStartStatus enumeration of the application (the names
    // are those of the statuses with a lower case first letter).
    enum def CopilotStartStatus {
        enum started;
        enum needsAgentKitUpdate;
        enum failed;
    }

    // What the application asks of the starter: the CLI that the locator found, its version, and whether the caller only
    // wants to know that the runtime could be started.
    attribute def CopilotStartRequest {
        attribute cliPath : String;
        attribute cliVersion : String;
        attribute checkOnly : Boolean;
    }

    // The answer of the starter: a status and one sentence for the user.
    attribute def CopilotStartResult {
        attribute status : CopilotStartStatus;
        attribute sentence : String;
    }

    part def AgentReadiness {
        doc /* Checks whether the AI provider that the settings choose can be used and says in one sentence what it found and what to do. For GitHub Copilot the checks run in order and the first failure gives the answer: the installed Copilot CLI is found and is version 1.0.113 or later (by the locator), credentials exist (a non-empty COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN, else gh auth token exits with 0 and prints something within 5 seconds; only emptiness is looked at and a token is never stored, shown, or logged), and the starter of the Copilot runtime says that the runtime can be started. The starter that this build ships is gated and always answers that an update of the AI library is needed, so Copilot is never reported ready. For Ollama the checks are the address and model name, the server within 5 seconds, the model (a name without a tag means latest), and a context window of at least 16,384 tokens (the smaller of the configured window and the one the model supports, when the server says), which is a warning that still allows a turn. The unit implements the IProviderCheck interface that the Settings subsystem owns, so the settings dialog offers Check connection without depending on this subsystem. It holds the starter interface with the gated starter and the Ollama probe interface with its real probe as supporting types, which have no part definition of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentReadiness.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/GatedCopilotRuntimeStarter.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ICopilotRuntimeStarter.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/IOllamaProbe.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/OllamaProbe.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentReadinessTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/OllamaProbeTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/ReadinessTestDoubles.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/agent-readiness.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/agent-readiness.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/agent-readiness.yaml");
        }

        // Finds the installed Copilot CLI and checks its version, which are the first two checks of GitHub Copilot, and shares its process runner, file system reader, and environment reader for the credential check.
        dependency AgentReadiness to CopilotCliLocator;

        // Reads the AI provider choices (provider, model, address, context window, CLI path) that it checks.
        dependency AgentReadiness to SettingsService;

        // Implements the IProviderCheck connection check that is documented with the AI Provider page of the settings dialog.
        dependency AgentReadiness to AiProviderSettingsPage;

        // Lists the models of an Ollama server and shows the context length of a model, in its real probe.
        dependency AgentReadiness to OllamaSharp;

        // The longest time, in seconds, that the check of the GitHub CLI or of the Ollama server may take.
        attribute probeTimeoutSeconds : Integer;

        // The smallest context window, in tokens, that is not warned about.
        attribute recommendedMinimumContextTokens : Integer;
    }
}

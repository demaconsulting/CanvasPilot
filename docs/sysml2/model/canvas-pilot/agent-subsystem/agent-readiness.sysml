package CanvasPilot_AgentReadiness {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import CanvasPilot_CopilotCliLocator::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_AiProviderSettingsPage::*;
    private import CanvasPilot_CopilotRuntimeStarter::*;
    private import OtsDependencies::*;

    part def AgentReadiness {
        doc /* Checks whether the AI provider that the settings choose can be used and says in one sentence what it found and what to do. For GitHub Copilot the checks run in order and the first failure gives the answer: the installed Copilot CLI is found and is version 1.0.113 or later (by the locator, which reads the version with --no-auto-update --version), credentials exist (a non-empty COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN, else gh auth token exits with 0 and prints something within 5 seconds; only emptiness is looked at and a token is never stored, shown, or logged), and the starter of the Copilot runtime starts the runtime on that CLI, or reuses the one that runs; the starter answers that the CLI is too old, that the runtime is not signed in, or that it could not be started, and the check reports the matching status with the sentence of the starter. For Ollama the checks are the address and model name, the server within 5 seconds, the model (a name without a tag means latest), and a context window of at least 16,384 tokens (the smaller of the configured window and the one the model supports, when the server says), which is a warning that still allows a turn. The unit implements the IProviderCheck interface that the Settings subsystem owns, so the settings dialog offers Check connection without depending on this subsystem. It holds the Ollama probe interface with its real probe as supporting types, which have no part definition of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentReadiness.cs",
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

        // Starts, or reuses, the shared Copilot runtime on the CLI that was found, which is the last check of GitHub Copilot, and takes its status and sentence.
        dependency AgentReadiness to CopilotRuntimeStarter;

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

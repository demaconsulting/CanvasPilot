package CanvasPilot_AgentService {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import ScalarValues::String;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_AgentToolRegistry::*;
    private import CanvasPilot_AgentInstructions::*;
    private import CanvasPilot_PayloadMessageMapper::*;
    private import CanvasPilot_AgentEventMapper::*;
    private import CanvasPilot_AgentReadiness::*;
    private import CanvasPilot_AgentProviderFactory::*;
    private import CanvasPilot_ProjectTools::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_StyleSelection::*;
    private import CanvasPilot_RollingFileLogger::*;
    private import OtsDependencies::*;

    part def AgentService {
        doc /* The AI copilot as the chat uses it: the implementation of IAgentService. It makes nothing until the first turn, then builds for the open project the tools (through the registry), the instructions (with the facts of the project and its style), the agent (through the factory, which starts the Copilot runtime once for the application when the provider needs it), and one provider session, and keeps them for the next turns. It makes them again when the open project changes or closes or when the provider, the model, the reasoning effort, the Ollama address, the context window, or the path of the Copilot CLI changes, and on a reset; it then deletes the session that the provider kept through the factory and releases the agent in the background after its turn is over. It runs one turn at a time (a request made while one runs gets a failure of the kind busy), links the token of the turn to the caller, a reset, a change of the project, and the end of the service, streams the events of the mapper with the files that the project tools wrote placed before the answer of the call that wrote them, and raises its own FileWritten event for the shell. It reads the stream of the provider on a thread of the pool and hands the events to the caller through a queue (the coalescer, a supporting type) in which the pieces of text that wait are joined and the progress that waits is reduced to the latest of its kind, never merging, dropping, or reordering the events that carry facts, and disposes the stream of the provider before the next turn can start. It records who canceled a turn (the caller, a reset, a change of the project, the end of the service, or a caller that stopped reading) and a turn whose provider does not end its stream within five seconds of the cancellation is ended without a failure and its conversation dropped; a turn that was stopped before the provider said anything drops the conversation so that the next turn gets the digest once; a turn that was stopped later keeps the conversation marked as interrupted, and the next turn on it that fails with a lost connection or an unexpected error before it says anything replaces the conversation and runs once more, with a line in the log; a turn that ended with its completion is never interrupted, also when the caller stopped reading at the completion. The text of a restored conversation is given once to the first message of a new provider session as one block of at most 6,000 characters kept in memory only. The Copilot runtime gets an explicit data folder below an injected local data root (the local application data of the user by default) and never one inside a project, and a turn is refused with the failure kind data inside project when that folder or the folder of the chat history lies inside the project. The log gets metadata only: the number of each turn on every line, the provider, the model, the reasoning effort, the count of earlier turns handed over and whether the digest was added, the identifier of the provider session and whether it was created, resumed, or released, the outcome, the cancel source, the times (the whole turn, the first event, the longest gap), the length of the answer text, whether the provider reasoned, the usage, the file where the provider recorded the events of a session whose turn failed or was canceled, and the reason of a release; the Copilot runtime logs through an adapter that drops the message. Dispose ends the turn, releases the conversation, and disposes the factory once. The digest of the restored history, the coalescer, and the adapter of the logger are supporting types, which have no part definitions of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentEventCoalescer.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentService.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/RestoredHistoryDigest.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/RuntimeLogAdapter.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentEventCoalescerTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentLoggingTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentServicePipelineTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentServiceStopTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentServiceTestRig.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentServiceTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/LiveCopilotLongTurnTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/RestoredHistoryDigestTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/agent-service.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/agent-service.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/agent-service.yaml");
        }

        // Implements the contract: the events of a turn, the failure kinds, and the readiness report.
        dependency AgentService to AgentContract;

        // Builds the tools of the open project, confined to its folder.
        dependency AgentService to AgentToolRegistry;

        // Writes the instructions for the project and chooses the tier from the context window.
        dependency AgentService to AgentInstructions;

        // Turns the message that the user sent into the message of the model.
        dependency AgentService to PayloadMessageMapper;

        // Turns the updates of the agent into the events of the turn, one mapper for each turn.
        dependency AgentService to AgentEventMapper;

        // Asks whether the chosen provider can be used.
        dependency AgentService to AgentReadiness;

        // Asks the factory for the agent of a conversation and for the deletion of a session that ends.
        dependency AgentService to AgentProviderFactory;

        // Passes on the event that a file of the project was created or replaced.
        dependency AgentService to ProjectTools;

        // Reads the AI provider choices and rebuilds the conversation when they change.
        dependency AgentService to SettingsService;

        // Reads the open project and rebuilds the conversation when it changes or closes (hub supplier, omitted from the service view).
        dependency AgentService to ProjectService;

        // Reads the style kit of the open project for the instructions.
        dependency AgentService to StyleSelection;

        // Writes the metadata of each turn and conversation to the log (hub supplier, omitted from the service view).
        dependency AgentService to RollingFileLogger;

        // Reads and writes the agent and the provider session of the Microsoft Agent Framework, and builds the message with its content types.
        dependency AgentService to AgentFramework;

        // The longest block, in characters, that stands for the restored history of a conversation.
        attribute maxDigestCharacters : Integer;

        // The name of the folder below the local data root that holds the Copilot runtime data.
        attribute copilotFolderName : String;
    }
}

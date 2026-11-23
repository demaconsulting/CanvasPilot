package CanvasPilot_ChatTurnRunner {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import CanvasPilot_ChatPanel::*;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_ComposerViewModel::*;
    private import CanvasPilot_MessagePayload::*;
    private import CanvasPilot_RollingFileLogger::*;

    // How a turn of the AI copilot ended, as the ChatTurnOutcome enumeration of the application.
    enum def ChatTurnOutcome {
        enum completed;
        enum stopped;
        enum failed;
        enum refused;
    }

    part def ChatTurnRunner {
        doc /* The logic of one turn of the conversation with the AI copilot, without a window: it gives the message that was sent to the copilot through IAgentService, reads the events of the turn, and keeps the rows of the transcript that show them, in the order in which things happened: the text of the copilot in a reply that grows (a reply ends when a tool is called, and the text after the tool is a new reply), one line for each tool call that says what was done, such as that a file was created or replaced, and notices and errors. It adds the text of a reply at most once every 50 milliseconds, through a clock and a timer that it is given, and on the synchronization context that the turn started on. It runs one turn at a time and tells when it is busy, so that the panel can block the composer. While a turn runs it keeps what the copilot is doing in words (working, thinking, the size of the response, the file that is being written with the bytes so far on the one row that the start and the end of the call go on to update, the tool that runs, the wait for the model, the answer), ticks the elapsed time once a second, and gives an honest hint after 30 seconds of silence while no tool runs; it shows only what the copilot reported. It stops the turn on request, says at once that it is stopping, ignores a second request, and keeps what was written, marked as stopped; a failure that the copilot reports, an exception of the stream, or a stream that ends early keeps the text, marks the reply as failed, and adds an error notice with the message, what to do, and the offer of the settings when they can help; nothing is thrown to the caller. It sums the tokens that the copilot reported for the model calls of the turn, keeps the largest model multiplier, and puts one quiet caption of only the reported figures and the time under the last reply when the turn ends, saying that a stopped turn may still count. The log gets the kind of a failure and the type of an exception, never words. The sentences of the tool rows and the sum of the usage are supporting types, which have no part definitions of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatTurnOutcome.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatTurnRunner.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ToolActivityText.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TurnUsage.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatTurnRunnerStatusTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatTurnRunnerTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatTurnRunnerUsageTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatTurnTestSupport.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ToolActivityTextTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/chat-subsystem/chat-turn-runner.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem/chat-turn-runner.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem/chat-turn-runner.yaml");
        }

        // Makes and changes the rows of the transcript: the reply, the tool line, and the notice.
        dependency ChatTurnRunner to ChatPanel;

        // Gives the turn to the copilot through IAgentService and reads its events and failure kinds, and nothing else of the Agent subsystem.
        dependency ChatTurnRunner to AgentContract;

        // Takes the SentMessage that the composer raised.
        dependency ChatTurnRunner to ComposerViewModel;

        // Gives the payload of the sent message to the copilot.
        dependency ChatTurnRunner to MessagePayload;

        // Logs the kind of a failure and the type of an exception (hub supplier, omitted from the turn view).
        dependency ChatTurnRunner to RollingFileLogger;

        // The shortest time, in milliseconds, between two updates of the text of a reply.
        attribute updateIntervalMilliseconds : Integer;
    }
}

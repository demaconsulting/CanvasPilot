package CanvasPilot_AgentEventMapper {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_AgentReadiness::*;
    private import CanvasPilot_SettingsService::*;
    private import CanvasPilot_RollingFileLogger::*;
    private import OtsDependencies::*;

    part def AgentEventMapper {
        doc /* Turns the stream of updates that an AI agent produces for one turn into the events of the provider-independent contract, and the exception of a failed turn into a failure with a sentence that says what to do. GitHub Copilot and Ollama make the same kinds of content, so one mapping serves both: the pieces of text of an update are joined and an empty update is dropped; a call of a tool becomes a start with a short summary of its arguments (a long text is shown by its length, never in full) and its result becomes a finish that is tied to the call by id, says whether the tool did what was asked (a refusal or an exception did not), summarizes the answer in at most 200 characters (so the answer of project_write that a file was created or replaced reaches the chat), and tells whether it carried a picture; usage is passed on with the input, output, and reasoning tokens and, for GitHub Copilot, the multiplier and the model that the runtime reports on the raw event (read in one adapter that confines the experimental member of the Copilot SDK; a provider that sends none has none); progress that the provider reports while the model works (the arguments of a call that it writes, the size of its response, and reasoning) becomes typed events that carry counts of bytes in UTF-8 and the path of the file found in the first 256 characters of the arguments, and never the content, made at most once for every 250 milliseconds of the clock that the mapper is given or at once when the phase changes; an error in the content ends the turn; and the end of the updates is completion. An exception is a failure of a kind that the cause tells, found by type and then by the words of the message through the inner exceptions: a refused sign-in (for GitHub Copilot the short sentence that says to run copilot and type /login and names gh auth login and the token variables as alternatives), a protocol version that the Copilot CLI and the AI library do not share, a model that the provider does not have (for Ollama, which reports only a 404 status, the sentence names ollama pull and the model), a lost connection, or anything else with the words on one line. A turn that the caller cancels ends without a failure and without completion. The log gets metadata only: the provider, the kind of a failure, the type of an exception, counts, and for each tool call the number of the turn, the id, the name, the path made safe for one line, the bytes of its arguments and of its result, its outcome, and its time. One mapper serves one turn. It has no supporting type. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentEventMapper.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/CopilotUsageReader.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentEventMapperTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentEventMapperProgressTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentEventMapperUsageTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/ScriptedChatClient.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/agent-event-mapper.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/agent-event-mapper.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/agent-event-mapper.yaml");
        }

        // Makes the events and the failure kinds of the contract that the chat consumes.
        dependency AgentEventMapper to AgentContract;

        // Uses the sentence of a Copilot that is not signed in, so that the chat and the settings dialog say the same.
        dependency AgentEventMapper to AgentReadiness;

        // Reads which AI provider the turn runs on, which decides the wording of a failure.
        dependency AgentEventMapper to SettingsService;

        // Writes the metadata of each turn to the log (hub supplier, omitted from the runtime view).
        dependency AgentEventMapper to RollingFileLogger;

        // Reads the stream of updates of the agent.
        dependency AgentEventMapper to AgentFramework;

        // Reads the text, call, result, usage, error, and picture content of the updates.
        dependency AgentEventMapper to ExtensionsAi;

        // Reads the raw events of the Copilot runtime that carry the progress of a tool call, the size of a response, reasoning, and usage with the multiplier of the model (the experimental member is read in one adapter only).
        dependency AgentEventMapper to CopilotSdk;

        // The most characters of an argument that are shown before it is shown by its length.
        attribute maxArgumentTextLength : Integer;
    }
}

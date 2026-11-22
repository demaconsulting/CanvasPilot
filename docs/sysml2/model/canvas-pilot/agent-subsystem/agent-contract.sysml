package CanvasPilot_AgentContract {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import ScalarValues::String;

    // What a readiness check found, as the AgentReadinessStatus enumeration of the application (the names are those of the
    // statuses with a lower case first letter).
    enum def AgentReadinessStatus {
        enum ready;
        enum notConfigured;
        enum copilotCliMissing;
        enum copilotCliTooOld;
        enum copilotNotSignedIn;
        enum copilotRuntimeUnavailable;
        enum ollamaUnreachable;
        enum ollamaModelMissing;
        enum ollamaContextTooSmall;
    }

    // Why a turn failed, as the AgentFailureKind enumeration of the application.
    enum def AgentFailureKind {
        enum notReady;
        enum authentication;
        enum protocolMismatch;
        enum modelNotFound;
        enum connectionLost;
        enum busy;
        enum dataInsideProject;
        enum unexpected;
    }

    // The report of a readiness check: a status and one sentence for the user. The turn request and the event records are
    // documented in the design of the unit; they carry types of the Payload subsystem and no further structure.
    attribute def AgentReadinessReport {
        attribute status : AgentReadinessStatus;
        attribute sentence : String;
    }

    part def AgentContract {
        doc /* The contract through which the chat runs the AI copilot, in plain records and one interface that name no AI library: IAgentService (readiness check, one turn at a time as a stream of events, reset of the conversation, and the event that tells the shell which file the copilot wrote), the turn request with the text of earlier turns for a restored conversation, the family of events (answer text, tool calls, files written, usage, completion, and failure with a named kind) whose summaries are cut to a fixed length, and the readiness report whose status says whether a turn can be run. The Settings subsystem owns the separate IProviderCheck interface, so the contract does not depend on it. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentEvent.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentReadinessReport.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentTurnRequest.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/IAgentService.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentContractTests.cs");
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/agent-contract.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/agent-contract.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/agent-contract.yaml");
        }

        // The longest summary, in characters, that a tool event carries.
        attribute maxSummaryLength : Integer;
    }
}

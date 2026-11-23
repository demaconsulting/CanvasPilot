package CanvasPilot_CopilotRuntimeStarter {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::String;
    private import OtsDependencies::*;

    // What the starter answered, as the CopilotStartStatus enumeration of the application (the names are those of the
    // statuses with a lower case first letter).
    enum def CopilotStartStatus {
        enum started;
        enum cliTooOld;
        enum notSignedIn;
        enum failed;
    }

    // What the application asks of the starter: the CLI that the locator found, its version, and, optionally, the folder
    // where the runtime keeps its data.
    attribute def CopilotStartRequest {
        attribute cliPath : String;
        attribute cliVersion : String;
        attribute dataDirectory : String;
    }

    // The answer of the starter: a status and one sentence for the user.
    attribute def CopilotStartResult {
        attribute status : CopilotStartStatus;
        attribute sentence : String;
    }

    part def CopilotRuntimeStarter {
        doc /* Starts the GitHub Copilot runtime of the AI library on the Copilot CLI that the user installed, at the full path that the locator found (the path of the settings first), with the installed CLI as the source of the runtime, the folder of the request (or the folder that it was made with, below the local data of the user and never in a project) for the data, and no token, host, or switch for the sign-in of the Copilot CLI (which stays on), so that no Copilot runtime is ever bundled or downloaded and the runtime signs in from the first source that applies: a token variable of the environment of the application, the sign-in of the Copilot CLI (copilot, then /login), or the GitHub CLI. The runtime reports how it signed in (the kind, the login, and the host, never a token), and the source of agents carries that as a sign-in record that words one sentence. One runtime is shared by the readiness check, the list of models, and the agents of the conversations: a start for the same CLI and folder returns the running runtime, concurrent starts start it once, another CLI or folder stops the old runtime and starts a new one, a start that fails or is cancelled is not remembered, and disposing stops the runtime once. Every way that the start fails becomes a status and one sentence: a CLI that is too old or speaks another protocol is reported with the explanation of the AI library, a runtime that is not signed in gets the sentence that says to run copilot and type /login (and names gh auth login and the token variables as alternatives) followed by a one-line digest of what the AI library found at each place it looked for a sign-in, a path that the library refuses is reported as a path in the settings that cannot be used, and any other failure keeps the explanation of the library. The start is a function that the constructor accepts, so that tests stand in for the sealed runtime. It holds the interface of the starter with its request and answer, the sign-in record, and the source of agents (the stand-in for the runtime, which also gives the model catalog of the runtime, and the one class that makes agents on the real runtime, the single call site of the agent factory of the AI library, with the default-deny permission handler) as supporting types, which have no part definition of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/CopilotAgentSource.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/CopilotRuntimeStarter.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ICopilotRuntimeStarter.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/CopilotRuntimeStarterTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/LiveCopilotTurnTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/copilot-runtime-starter.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/copilot-runtime-starter.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/copilot-runtime-starter.yaml");
        }

        // Starts the Copilot runtime on the installed CLI and makes agents on it with the factory of the Copilot package.
        dependency CopilotRuntimeStarter to AgentKit;
    }
}

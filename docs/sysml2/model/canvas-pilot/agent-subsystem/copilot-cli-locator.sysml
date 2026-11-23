package CanvasPilot_CopilotCliLocator {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import ScalarValues::String;

    // What the search for the Copilot CLI found, as the CopilotCliStatus enumeration of the application (the names are those
    // of the statuses with a lower case first letter).
    enum def CopilotCliStatus {
        enum found;
        enum notFound;
        enum notAnExecutable;
        enum tooOld;
        enum unreadable;
    }

    // Why a candidate was not used, as the CopilotCliRejection enumeration of the application.
    enum def CopilotCliRejection {
        enum missing;
        enum notExe;
        enum shim;
        enum tooSmall;
        enum unreadable;
        enum tooOld;
    }

    // A candidate that was not used: its path after links were followed, the reason, and one short sentence of facts.
    attribute def RejectedCliCandidate {
        attribute path : String;
        attribute reason : CopilotCliRejection;
        attribute detail : String;
    }

    // The result of the search: the status, the path and version of the CLI (or of the candidate that failed), and one
    // sentence for the user. The list of rejected candidates is documented in the design of the unit.
    attribute def CopilotCliInfo {
        attribute status : CopilotCliStatus;
        attribute path : String;
        attribute version : String;
        attribute sentence : String;
    }

    part def CopilotCliLocator {
        doc /* Finds the GitHub Copilot CLI that the user installed. A path in the settings is the only candidate and is not filtered; otherwise the candidates are the WinGet package folders, the WinGet links folder, and every copilot.exe on the PATH, followed through links and listed once. Files that are not .exe, lie in the folder of an editor shim (copilot-shim, .vscode, .vscode-insiders, github.copilot-chat, Microsoft VS Code...), or are smaller than 20 MiB are rejected and never started; the first remaining candidate that answers --version with GitHub Copilot CLI x.y.z at version 1.0.113 or later is used, and a candidate that fails the check is passed over. The result carries a status, the path, the version, one sentence for the user, and the rejected candidates with their reasons. The unit holds its supporting types, the process runner, the file system reader, and the environment reader (interfaces with real implementations), which make it testable; they have no part definition of their own. It uses the .NET base libraries only. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/CopilotCliInfo.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/CopilotCliLocator.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ICliFileSystem.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/IEnvironmentReader.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/IProcessRunner.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ProcessRunner.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/CopilotCliLocatorTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/CopilotCliTestDoubles.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/ProcessRunnerTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/copilot-cli-locator.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/copilot-cli-locator.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/copilot-cli-locator.yaml");
        }

        // The oldest Copilot CLI version that works, as its three numbers joined by dots (1.0.113).
        attribute minimumVersion : String;

        // The smallest size, in bytes, of a real Copilot CLI (20 MiB).
        attribute minimumBytes : Integer;

        // The longest time, in seconds, that the check of the version may take.
        attribute probeTimeoutSeconds : Integer;
    }
}

package CanvasPilot_ChatHistoryStore {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Integer;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_RollingFileLogger::*;

    // What a saved row of the chat history is, as the ChatHistoryRowKind enumeration of the application.
    enum def ChatHistoryRowKind {
        enum user;
        enum assistant;
        enum tool;
    }

    // How a saved reply of the AI copilot ended, as the ChatHistoryReplyState enumeration of the application.
    enum def ChatHistoryReplyState {
        enum completed;
        enum stopped;
        enum failed;
    }

    part def ChatHistoryStore {
        doc /* Keeps the words of the conversation of each project on the user's computer, below a root folder that it is given and never in the project, so that the conversation is still there when the project is opened again. The folder of a project is named by the first 16 bytes of the SHA-256 hash of the full, normalized path of its project file (32 lowercase hexadecimal characters), so a project that is moved or renamed has another key and a fresh history. It saves and reads a version 1 JSON file of rows of words only (user text with the labels of its marks and attachments, mark numbers and notes, attachment paths, assistant text and how the reply ended, and tool rows with name, sentence, and outcome; no picture, payload, or tool result), with a sidecar file that names the project, keeps the newest 200 rows and at most 2 MB by dropping the oldest first, writes a temporary file and moves it over the old one so that a file is whole or unchanged, removes the temporary files that a crash left, renames a file that it cannot use (not JSON, newer version, no rows, an unknown kind of row, far too big) to a .bad file in the same folder and logs the path and the reason and never a word, deletes the folder of a project on request, reports a failure to write or delete as a result and not as an exception, and refuses to read, write, or delete when its root is the project folder or inside it. The key rule, the row and mark records, and the result record are supporting types, which have no part definitions of their own; the row records are described by the attributes and enumerations here. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatHistoryRow.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatHistoryStore.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/IChatHistoryStore.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ProjectHistoryKey.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatHistoryStoreTests.cs");
            designFiles = ("docs/design/canvas-pilot/chat-subsystem/chat-history-store.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem/chat-history-store.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem/chat-history-store.yaml");
        }

        // Takes the project information types (the name, the folder, and the file of the open project), whose file names the history.
        dependency ChatHistoryStore to ProjectService;

        // Logs the path of a file that cannot be used and a write that fails, never a word of a row (hub supplier, omitted from the history view).
        dependency ChatHistoryStore to RollingFileLogger;

        // Combines the folder of a project history with the names of its files (hub supplier, omitted from the history view).
        dependency ChatHistoryStore to PathHelpers;

        // The version of the file that this build reads and writes.
        attribute currentVersion : Integer;

        // The most rows that the history keeps.
        attribute maxRows : Integer;

        // The most bytes of the history file.
        attribute maxBytes : Integer;

        // The most characters of the text of one row.
        attribute maxTextCharacters : Integer;

        // What the rows of the file are, one of the kinds of ChatHistoryRowKind.
        attribute rowKind : ChatHistoryRowKind;

        // How a saved reply ended, one of the states of ChatHistoryReplyState.
        attribute replyState : ChatHistoryReplyState;
    }
}

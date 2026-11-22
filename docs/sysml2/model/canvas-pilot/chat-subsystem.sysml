package CanvasPilot_ChatSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ChatPanel::*;
    private import CanvasPilot_ChatTurnRunner::*;
    private import CanvasPilot_ChatHistoryStore::*;
    private import CanvasPilot_PayloadInspector::*;

    part def ChatSubsystem {
        doc /* The panel in which the user converses with the AI copilot: the transcript of the messages that were sent, with the rows of the copilot's answer (its text, the tools it called, and notices), the composer below it with the buttons that send and that listen while held, the joining of the marks on the picture to the message, the logic of one turn of the conversation, the store that keeps the words of the conversation of each project on the user's computer outside the project (restored when the project is opened and cleared on request), and the window that shows exactly what the assistant would receive for a message. It holds none of the rules of the composer, dictation, marking, payload building, or the AI provider, reaches the copilot only through the contract of the Agent subsystem, and does not depend on the application shell. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/chat-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem.yaml");
        }

        part chatPanel : ChatPanel;
        part chatTurnRunner : ChatTurnRunner;
        part chatHistoryStore : ChatHistoryStore;
        part payloadInspector : PayloadInspector;
    }
}

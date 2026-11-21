package CanvasPilot_ChatSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ChatPanel::*;
    private import CanvasPilot_PayloadInspector::*;

    part def ChatSubsystem {
        doc /* The panel in which the user converses with the assistant that is added later: the transcript of the messages that were sent, the composer below it with the buttons that send and that listen while held, the joining of the marks on the picture to the message, and the window that shows exactly what the assistant would receive for a message. It holds none of the rules of the composer, dictation, marking, or payload building, and does not depend on the application shell. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/chat-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem.yaml");
        }

        part chatPanel : ChatPanel;
        part payloadInspector : PayloadInspector;
    }
}

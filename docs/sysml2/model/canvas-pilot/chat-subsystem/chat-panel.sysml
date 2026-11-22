package CanvasPilot_ChatPanel {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_AnnotationOverlay::*;
    private import CanvasPilot_ComposerEditor::*;
    private import CanvasPilot_ComposerViewModel::*;
    private import CanvasPilot_DictationService::*;
    private import CanvasPilot_KnowledgeMarkdown::*;
    private import CanvasPilot_MarkImageComposer::*;
    private import CanvasPilot_MarkRegistry::*;
    private import CanvasPilot_MessagePayload::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_PayloadInspector::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def ChatPanel {
        doc /* The panel in which the user converses: the conversation of the open project, oldest first, as one family of rows (the sent message with its words in the order they were written, its chips with their pictures, and a button that shows its payload; the reply of the AI copilot, which grows while it streams as plain selectable text and is drawn as Markdown only when it is completed, and keeps its text and a mark when it is stopped or failed; the line of a tool call; and the notice with what to do and a button that opens the settings when they can help); the composer's editing control; a button that sends the message exactly when the composer can; and a button that listens exactly while it is held, however the hold ends, and shows why when dictation cannot work. When it has a copilot, every sent message starts a turn through the turn runner: the composer cannot send and an indicator and a Stop button show while the turn runs, a change of the project stops the turn and drops its rows, and a banner says, from the readiness report of the copilot, why the provider cannot be used, with the buttons that open the settings and check again, and is hidden when everything is ready; without a copilot it only adds the entry of the message. It joins the layer on which the user marks the picture to the message, so that every mark becomes a chip as one undo step and the layer shows the marks whose chips the message holds, prepares dictation when it is shown, gives chips their pictures, and keeps the transcript only in memory, dropping it when the project changes. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/AssistantReplyView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/AssistantReplyView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/AssistantReplyViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChipThumbnailProvider.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/NoticeRowView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/NoticeRowView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/NoticeRowViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ToolActivityRowView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ToolActivityRowView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ToolActivityRowViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptEntryView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptEntryView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptEntryViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptItem.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptRowViewModel.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatPanelCopilotTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatPanelTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/TranscriptRowTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ChatPanelCopilotViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ChatPanelViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ChatUiTestSupport.cs"
            );
            designFiles = ("docs/design/canvas-pilot/chat-subsystem/chat-panel.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem/chat-panel.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem/chat-panel.yaml");
        }

        // Follows the message, sends it, takes its dictation, and adds a chip for every mark that is drawn.
        dependency ChatPanel to ComposerViewModel;

        // Hosts the editing view of the message and shows the sent messages with its chip control.
        dependency ChatPanel to ComposerEditor;

        // Finds the marks of the draft to make the pictures of their chips.
        dependency ChatPanel to MarkRegistry;

        // Tells it which mark chips the message holds and takes the marks that it reports.
        dependency ChatPanel to AnnotationOverlay;

        // Cuts the area around a mark out of its picture for the chip of the mark.
        dependency ChatPanel to MarkImageComposer;

        // Holds the payload of each sent message, from which the transcript takes the pictures of the marks.
        dependency ChatPanel to MessagePayload;

        // Prepares dictation ahead of the first press of the talk button.
        dependency ChatPanel to DictationService;

        // Asks for the inspector of a message's payload.
        dependency ChatPanel to PayloadInspector;

        // Starts the turn of the AI copilot for each sent message, stops it, and follows when it is busy. It is named with its
        // package because the turn runner imports this package, and the two must not import each other.
        dependency ChatPanel to CanvasPilot_ChatTurnRunner::ChatTurnRunner;

        // Asks the copilot whether the provider can be used (IAgentService and the readiness report only) for the banner.
        dependency ChatPanel to AgentContract;

        // Reads the final text of a reply as Markdown, which the knowledge browser draws too.
        dependency ChatPanel to KnowledgeMarkdown;

        // Records a failure to prepare dictation, to check the copilot, or of a turn.
        dependency ChatPanel to RollingFileLogger;

        // Builds the path of a copied picture in the project.
        dependency ChatPanel to PathHelpers;
    }
}

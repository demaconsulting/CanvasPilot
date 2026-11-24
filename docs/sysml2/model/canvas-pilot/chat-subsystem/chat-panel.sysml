package CanvasPilot_ChatPanel {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_AnnotationOverlay::*;
    private import CanvasPilot_ChatHistoryStore::*;
    private import CanvasPilot_ComposerEditor::*;
    private import CanvasPilot_ComposerViewModel::*;
    private import CanvasPilot_DictationService::*;
    private import CanvasPilot_FollowUpSuggestions::*;
    private import CanvasPilot_KnowledgeMarkdown::*;
    private import CanvasPilot_MarkImageComposer::*;
    private import CanvasPilot_MarkRegistry::*;
    private import CanvasPilot_MessagePayload::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_PayloadInspector::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_PushToTalkBinding::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def ChatPanel {
        doc /* The panel in which the user converses: the conversation of the open project, oldest first, as one family of rows (the sent message with its words in the order they were written, its chips with their pictures, and a button that shows its payload; the reply of the AI copilot, which grows while it streams as plain selectable text and is drawn as Markdown only when it is completed, and keeps its text and a mark when it is stopped or failed; the line of a tool call, with a check, a spinner, or a cross and the time the call took when that is noticeable; and the notice with what to do and a button that opens the settings when they can help; each row knows whether it opens a group, so that the name of the copilot shows once for a run of its rows); the composer's editing control in a card; a header with the title, a menu of more actions, and a button that asks the host to hide the panel; the example prompts that help to begin and the prompts that help to go on, which follow what the project has (ways to start while it has no drawing, ways to refine once it has one, and none while the copilot works, planned by the FollowUpSuggestions unit); a button that attaches files through the picker that the window gives it; a button that listens exactly while it is held, however the hold ends, and shows why when dictation cannot work, with the key cap and the tip that name the talk key in effect; and a button that sends the message exactly when the composer can, as does the Enter key on its own while Shift with Enter writes a line break, with every button that shows no text named and given a tooltip. When it has a copilot, every sent message starts a turn through the turn runner: the composer cannot send and an indicator of three pulsing dots, what the copilot is doing, the elapsed time, the hint about a long wait, and a Stop button show while the turn runs (Stop says that the copilot is stopping and cannot be pressed twice), a quiet caption of what the turn used shows under its last reply, a change of the project stops the turn and drops its rows, and a banner says, from the readiness report of the copilot, why the provider cannot be used, with the buttons that open the settings and check again, and is hidden when everything is ready; without a copilot it only adds the entry of the message. It joins the layer on which the user marks the picture to the message, so that every mark becomes a chip as one undo step, the layer shows the marks whose chips the message holds, clearing the marks takes all their chips out of the message, and a request from a mark's chip opens the mark's note on the layer, prepares dictation when it is shown, gives chips their pictures, and drops the transcript when the project changes. When it is given a chat history store and the project service it saves the words of every message and of what followed it when the turn has ended (never a notice), shows the saved conversation of a project as rows of text without pictures or payload when the project is opened, gives the saved words to the copilot with the next message, keeps each project's conversation apart, says in a notice when the history cannot be saved, and offers a Clear chat history item in the menu of the header that, without a question, deletes the saved history, empties the transcript but for a notice, stops a turn that runs, and makes the copilot forget the conversation. The mapping between the transcript and the saved rows, and the row of a restored message, are supporting types of this unit. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/AssistantReplyView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/AssistantReplyView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/AssistantReplyViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatHistoryMapper.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChipThumbnailProvider.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/NoticeRowView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/NoticeRowView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/NoticeRowViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/RestoredEntryView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/RestoredEntryView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/RestoredEntryViewModel.cs",
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
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatPanelHistoryTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatPanelTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/TranscriptRowTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ChatPanelCopilotViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ChatPanelHistoryViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ChatPanelViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ChatUiTestSupport.cs"
            );
            designFiles = ("docs/design/canvas-pilot/chat-subsystem/chat-panel.md");
            verificationFiles = ("docs/verification/canvas-pilot/chat-subsystem/chat-panel.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/chat-subsystem/chat-panel.yaml");
        }

        // Follows the message, sends it, takes its dictation, adds a chip for every mark that is drawn and deletes the mark chips on request, adds the files that the attach button picks, and writes prompts into it.
        dependency ChatPanel to ComposerViewModel;

        // Hosts the editing view of the message and shows the sent messages with its chip control.
        dependency ChatPanel to ComposerEditor;

        // Finds the marks of the draft to make the pictures of their chips.
        dependency ChatPanel to MarkRegistry;

        // Tells it which mark chips the message holds, takes the marks that it reports, and asks it to open the note of a mark for changing.
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

        // Saves the words of every turn, shows them again when the project is opened, and deletes them on request, through the store (IChatHistoryStore and the row records only).
        dependency ChatPanel to ChatHistoryStore;

        // Takes the open project, whose project file names its history (IProjectService and ProjectInfo only).
        dependency ChatPanel to ProjectService;

        // Records a failure to prepare dictation, to check the copilot, or of a turn.
        dependency ChatPanel to RollingFileLogger;

        // Builds the path of a copied picture in the project.
        dependency ChatPanel to PathHelpers;

        // Plans the prompts that it offers from whether the project has a drawing, whether a picture is shown, and whether the copilot works.
        dependency ChatPanel to FollowUpSuggestions;

        // Names the key that is held to talk in the key cap and the tip, following the speech settings.
        dependency ChatPanel to PushToTalkBinding;
    }
}

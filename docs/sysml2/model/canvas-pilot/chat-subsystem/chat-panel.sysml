package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def ChatPanel {
        doc /* The panel in which the user converses: the transcript of the messages that were sent in the open project, oldest first, each with its words in the order they were written, its chips with their pictures, and a button that shows its payload; the composer's editing control; a button that sends the message exactly when the composer can; and a button that listens exactly while it is held, however the hold ends, and shows why when dictation cannot work. It joins the layer on which the user marks the picture to the message, so that every mark becomes a chip as one undo step and the layer shows the marks whose chips the message holds, prepares dictation when it is shown, gives chips their pictures, and keeps the transcript only in memory, dropping it when the project changes. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChatPanelViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/ChipThumbnailProvider.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptEntryView.axaml",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptEntryView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptEntryViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ChatSubsystem/TranscriptItem.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatPanelTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatSubsystemFakes.cs",
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

        // Records a failure to prepare dictation.
        dependency ChatPanel to RollingFileLogger;

        // Builds the path of a copied picture in the project.
        dependency ChatPanel to PathHelpers;
    }
}

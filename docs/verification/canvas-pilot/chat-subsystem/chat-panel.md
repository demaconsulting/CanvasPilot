### ChatPanel

#### Verification Approach

`ChatPanel` is verified with unit tests in `test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatPanelTests.cs` for
the view models and the picture provider, in `ChatPanelCopilotTests.cs` for the part that joins the AI copilot (the
turn, the busy state, the busy row with its phase, elapsed time, and hint, Stop and the stopping state, the notices, the
change of project, and the readiness banner), in `FollowUpSuggestionsTests.cs` for the prompts that follow what the
project has (these also verify the unit `FollowUpSuggestions`; see its verification), in `ChatPanelHistoryTests.cs` for
the part that keeps the conversation on this computer (saving, restoring, the digest for the copilot, and Clear, over
the real `ChatHistoryStore` in a temporary folder), in `TranscriptRowTests.cs` of the same folder for the rows that the
copilot's turn adds (reply, tool, and notice), and with headless Avalonia UI tests in
`test/DemaConsulting.CanvasPilot.UiTests/ChatPanelViewTests.cs`, `ChatPanelCopilotViewTests.cs`, and
`ChatPanelHistoryViewTests.cs` (with `ChatUiTestSupport.cs` for the harness and the scripted copilot) for the views, in
`AccessibilityViewTests.cs` for the names, tooltips, sizes, and keyboard focus of the buttons of the panel in the
workspace of an open project, and in `AnnotationOverlayViewTests.cs` for the clearing of the marks through the real
panel and annotation toolbar. Both use the real `ChatPanelViewModel`, `ComposerViewModel`, `ComposerDocument`,
`ComposerHistory`, `AttachmentIntake`, `AttachmentStore`, `MarkRegistry`, and `AnnotationOverlayViewModel` over a
temporary project folder, because the unit exists to connect them and the rule under test is how they agree. The UI
tests also use the real `ChatPanelView`, `ComposerEditorView` with the AvaloniaEdit surface, `TranscriptEntryView`, and
the views of the rows of the copilot (`AssistantReplyView` with the `MarkdownBlocksPanel`, `ToolActivityRowView`, and
`NoticeRowView`). Only what has no place in a panel test is replaced: the dictation service (a controllable fake in the
unit tests, a scripted one in the UI tests, both with a session that says words and ends with a chosen text), the
builder of sent messages (one that gives a text, or a ready-made payload), the source of the shown picture, the composer
of mark pictures (a substitute, or a stub that gives a one-pixel PNG), the launcher of the payload inspector (a
recording one), the picker of files to attach (a fake that answers with the files the test names and records how it was
asked), and the AI copilot (a controlled or scripted `IAgentService` that streams the events the test gives it when it
gives them and reports the readiness the test chooses, with a manual clock in the unit tests, which times the tool rows
and ticks the elapsed time of the turn, and, in the UI tests, a scripted copilot that can also ignore the cancellation
as a slow provider does). The talk key of the hints is a real `PushToTalkSetting` over a real `SettingsService` in a
temporary folder (in `PushToTalkHintsTests.cs` of the AppShell tests, which verifies the hints of every unit that shows
the key). Input reaches the views as the platform delivers it: pointer presses and releases on the talk button, move
events away from it, the loss of the pointer capture, space key presses and releases on the focused button, Enter and
Shift+Enter key presses in the editing control, text input and Ctrl+Z in the editing control, and clicks on the Send,
Stop, "Open Settings", "Check again", and "View payload" buttons and on the example prompts.

#### Test Environment

The standard test runner, and for the views the headless Avalonia platform with the Skia back end and the application's
own `App`, which loads the style of the editing control. A temporary folder per test holds the project and the files
that are dropped, and is removed afterwards (a preview that is still being decoded can hold a file for a moment, so
removal retries). No window is shown on a screen, no real microphone or speech model is used, and the real operating
system pointer and keyboard are not involved.

The checks by hand before a release are listed in the verification of the `Chat` subsystem (real dictation, the Windows
microphone privacy setting, the cases without model, microphone, or native files, the release outside the button, and
the looks, such as the bubbles, the divider of earlier messages, the pulsing dots and talk button, and the "View
payload" button that shows on hover); they are not evidence for a requirement.

#### Acceptance Criteria

- All unit tests and UI tests of the unit pass with zero failures.
- The transcript lists exactly the sent messages, oldest first, never the draft; each entry shows its words in order,
  with dictated words shown differently from typed ones, and its chips in their places without delete buttons, with
  their names; the summary says how many marks and attachments it carried and is hidden when there are none.
- The transcript is dropped when the project changes or closes, and a disposed panel no longer follows the composer.
- A request from a mark's chip in the message opens the note of that mark on the annotation layer, filled with its note,
  and the chip shows the note confirmed there; a disposed panel opens nothing; the chip of a sent message offers no
  change of its note.
- The Send button, an icon button named Send, can be used exactly when the composer can send; sending moves the message
  to the transcript and empties the editing control; a failure keeps the draft, adds nothing, and shows the composer's
  reason. Enter on its own sends and Shift+Enter writes a line break and sends nothing; the hint beside Send says that
  Enter sends.
- The Attach files button asks the picker once, starting in the project folder, and the files it returns become
  attachment chips of the message.
- The empty conversation shows the question, the example prompts that fit the project (the ways to start in a project
  with no drawing, the ways to refine one that has a drawing), and the tip, and choosing a prompt writes it into the
  message so that it can be sent; the prompts above the message show only once there is a conversation; the button of
  the header raises the request to hide the panel.
- The prompts follow what the project has: ways to start while it has no drawing, ways to refine (and the export, only
  while a picture is shown) once a picture is shown, the copilot wrote a drawing, or the project folder holds one (not
  in `attachments`), none while the copilot works, planned again when the project, the picture, or a turn changes, each
  change announced, and a drawing written in one project not counted in another (see the verification of
  `FollowUpSuggestions`).
- While a turn runs the busy row says what the copilot is doing (the working text at the start, then what it reports),
  how long the turn has lasted as `m:ss` from the clock of the panel, and, after 30 seconds without a sign from the
  copilot, the hint about a long wait, hides the hint when the copilot is heard from, announces each change, and shows
  nothing of it all when no turn runs.
- From the press of Stop until the turn has ended the busy row says "Stopping...", Stop cannot be pressed again, the
  turn goes on being busy, and the change is announced; when the provider has ended its stream the reply is stopped with
  its text, Send can be used again, and the row is hidden.
- The last reply of a completed turn shows the caption of what the turn cost, with only the figures that were
  reported, and an earlier reply or a turn without a report shows none.
- The key cap of the talk button, its tooltip, and the tip of the empty conversation name the key of the setting (`F9`
  by default) and change, announced, when the setting changes.
- Every button that shows only an icon (overflow menu, hide, attach, talk, send) has an accessible name and a tooltip,
  can take the focus, and is at least 28 by 28 pixels, in both theme variants; the empty message shows its placeholder
  until text is typed.
- A mark drawn on the picture becomes one chip in the row of chips above the words as one undo step; undo removes the
  chip and hides the mark and redo restores both; marks are allowed exactly while a message can be written; no mark
  remains after sending; clearing the marks on the picture takes the chips of all marks out of the message.
- Pressing the pointer on the talk button listens and releasing it anywhere, losing the capture, removing the panel, or
  (with the keyboard) releasing the space key ends it, once for each hold; the accessible name of the button follows
  the state; the words so far
  are shown while it listens and one dictated block is added at the caret on release; a second press while held starts
  nothing.
- When dictation cannot work, a press shows the reason in the panel, listens to nothing, and changes nothing; with no
  project, neither the Send nor the talk button can be used.
- Dictation is prepared once when the panel is shown, and a failure of the preparation, or a panel without a dictation
  service, causes no failure.
- The chip of a mark in the draft gets the picture of the area around the mark; a picture that cannot be made, or a chip
  that is not a mark or a picture, keeps its icon without a failure; an attached picture of a sent message is previewed
  from its copy in the project.
- "View payload" gives the launcher an inspector for exactly that message's payload; it does nothing without a launcher
  and cannot run for a message without a payload.
- The rows of the copilot form one family with the sent message: each row tells its kind; a reply grows while it
  streams, is final only when completed, keeps its text and is marked when stopped or failed, and never changes after it
  has ended; a tool row says what is under way and then what was done, whether it worked or failed, how long it took
  when that was at least a tenth of a second, and whether it carried a
  picture, and keeps the report of a written file when the tool succeeded and shows the refusal when it did not; a
  notice holds its message, remedy, and whether the settings can help; the sent message keeps its payload and summary as
  a row; the first row, and the first row of the copilot after a message or a notice, opens a group.
- With a copilot, a sent message starts a turn with its payload and the rows of the answer follow the entry in order;
  the text of a reply is plain while it streams and Markdown only when it is complete (never for a stopped or failed
  one); the composer cannot send but can be written to while the turn runs, with an indicator and a Stop button shown
  only then; a change of project or disposing the panel stops the turn and nothing of it is shown afterwards; without a
  copilot only the entry is added and nothing is shown or blocked.
- Stop cancels the turn, keeps the text marked as stopped, and frees the composer; a failed turn is an error row with
  the message and the remedy, whose "Open Settings" raises the request that the host handles.
- The banner shows the sentence of the readiness report when the provider cannot be used or can be used with a warning
  (the warning does not block sending), is hidden when it is ready, shows a check that throws as a sentence, offers
  "Open Settings" and "Check again", and shows the latest check only.
- With a history store and a project service, the words of a message and of what followed it are saved when its turn has
  ended, stopped, or failed (never a notice, and not when the project changed or the history was cleared meanwhile);
  a project that is opened shows its saved conversation as text rows (a restored message has words, labels, and lines
  for notes and paths, but no picture, no payload, and no "View payload"); the saved words go to the copilot with the
  next message; each project has its own conversation; a failure to save is a notice and does not stop the
  conversation; and without a store or a project service nothing is saved or restored.
- "Clear chat history", the item of the menu of the header, deletes the saved history of the open project, empties the
  transcript but for a notice that is
  not saved, resets the copilot, stops a turn that runs and drops its rows, says in an error notice when the history
  could not be deleted, and works without a store or a copilot.

#### Test Scenarios

**ChatPanelViewModel_Send_WithCopilot_StartsTurnStreamsReplyAndMakesMarkdownWhenComplete**: A message is sent in a panel
with a controlled copilot; the copilot gets its payload, streams "Here is **the box**", which is the plain text of a
streaming reply with no Markdown blocks, and completes; the reply is complete and has one paragraph block, and the rows
are the entry and the reply. This scenario is tested by
`ChatPanelViewModel_Send_WithCopilot_StartsTurnStreamsReplyAndMakesMarkdownWhenComplete`.

**ChatPanelViewModel_TurnRuns_ComposerCannotSendAndBusyIsAnnouncedUntilTheTurnEnds**: While a turn runs the panel is
busy and announces it, `CanSend` is false with the composer's reason, the Stop command can run, and text typed meanwhile
is kept; when the turn completes all of it is reversed. This scenario is tested by
`ChatPanelViewModel_TurnRuns_ComposerCannotSendAndBusyIsAnnouncedUntilTheTurnEnds`.

**ChatPanelViewModel_Stop_KeepsPartialTextMarksItStoppedAndFreesTheComposer**: The Stop command ends a turn that has
written "Let me think": the reply keeps the text, is stopped with the label `Stopped`, has no Markdown, the token of the
turn was canceled, and the composer can send. This scenario is tested by
`ChatPanelViewModel_Stop_KeepsPartialTextMarksItStoppedAndFreesTheComposer`.

**ChatPanelViewModel_ToolCalled_AddsToolRowInOrderWithItsResult**: The copilot talks, writes a file that it replaces,
and completes; the rows are the entry, the reply, and a tool row that says "Replaced" and the path in code format, ended
and successful. This scenario is tested by `ChatPanelViewModel_ToolCalled_AddsToolRowInOrderWithItsResult`.

**ChatPanelViewModel_TurnFails_ShowsNoticeWithRemedyAndOpenSettingsRaisesTheRequest**: The provider reports a missing
model; the last row is an error notice with the message, a remedy, and the offer of the settings; choosing "Open
Settings" on the notice and on the panel raises the request each time. This scenario is tested by
`ChatPanelViewModel_TurnFails_ShowsNoticeWithRemedyAndOpenSettingsRaisesTheRequest`.

**ChatPanelViewModel_ProjectChangesWhileTurnRuns_StopsItAndDropsWhatItStillSays**: A project is opened while a turn
that has written text runs; the transcript is empty, the turn was canceled, the composer is free, and what the old turn
adds while it ends is dropped. This scenario is tested by
`ChatPanelViewModel_ProjectChangesWhileTurnRuns_StopsItAndDropsWhatItStillSays`.

**ChatPanelViewModel_Dispose_WhileTurnRuns_StopsTheTurn**: Disposing the panel cancels a running turn and leaves the
transcript empty. This scenario is tested by `ChatPanelViewModel_Dispose_WhileTurnRuns_StopsTheTurn`.

**ChatPanelViewModel_NoCopilot_OnlyAppendsTheEntryAndBlocksNothing**: A panel without a copilot adds one user row for a
sent message, is never busy, shows no banner, blocks nothing, and ignores Stop and the readiness check. This scenario is
tested by `ChatPanelViewModel_NoCopilot_OnlyAppendsTheEntryAndBlocksNothing`.

**ChatPanelViewModel_RefreshReadiness_ShowsTheActionableSentenceAndHidesItWhenReady**: A ready provider shows no banner;
the Copilot that is not signed in shows the sign-in sentence of `CopilotRuntimeStarter` and `HasReadinessIssue` is
announced; a warning is shown and sending stays possible; "Check again" repeats the check and a ready provider hides the
banner. This scenario is tested by `ChatPanelViewModel_RefreshReadiness_ShowsTheActionableSentenceAndHidesItWhenReady`.

**ChatPanelViewModel_RefreshReadiness_CheckThrows_ShowsTheProblemInTheBanner**: A check that throws is shown as a
sentence that says that the copilot could not be checked, with the reason, and nothing is thrown. This scenario is
tested by `ChatPanelViewModel_RefreshReadiness_CheckThrows_ShowsTheProblemInTheBanner`.

**AssistantReplyViewModel_Blocks_AreMarkdownOnlyOnceCompleted**: A reply that streams a heading and a paragraph has no
blocks; completing it gives two blocks, the same list each time, and announces the change; a stopped and a failed reply
have none. This scenario is tested by `AssistantReplyViewModel_Blocks_AreMarkdownOnlyOnceCompleted`.

**NoticeRowViewModel_OpenSettingsCommand_CallsWhatItWasGivenOrNothing**: The command of a notice calls the action that
it was given once, and a notice without an action does nothing. This scenario is tested by
`NoticeRowViewModel_OpenSettingsCommand_CallsWhatItWasGivenOrNothing`.

**ChatPanelView_CopilotAnswer_StreamsPlainTextThenMarkdownWhenComplete**: A message is sent; as the copilot streams
"Here is **the box**" the reply shows it as plain selectable text and not as Markdown; when the turn completes the
Markdown panel is shown with "Here is the box" and the plain text is hidden. This scenario is tested by
`ChatPanelView_CopilotAnswer_StreamsPlainTextThenMarkdownWhenComplete`.

**ChatPanelView_CopilotWorking_ShowsIndicatorAndStopAndBlocksSend**: The indicator and the Stop button are hidden before
the message is sent, shown while the turn runs with Send disabled and the typed text kept, and hidden again when the
turn ends, when Send can be used. This scenario is tested by
`ChatPanelView_CopilotWorking_ShowsIndicatorAndStopAndBlocksSend`.

**ChatPanelView_Stop_CancelsTurnKeepsPartialTextAndEnablesSend**: Pressing Stop cancels the turn, keeps the partial text
with the `Stopped` label, hides the indicator, and enables Send. This scenario is tested by
`ChatPanelView_Stop_CancelsTurnKeepsPartialTextAndEnablesSend`.

**ChatPanelView_ProviderFails_ShowsErrorRowWithRemedyAndOpenSettings**: A failure of the provider shows an error row
with the message, the remedy, and the "Open Settings" button; pressing it raises the request and the composer is free.
This scenario is tested by `ChatPanelView_ProviderFails_ShowsErrorRowWithRemedyAndOpenSettings`.

**ChatPanelView_ToolCalls_ShowToolRowsInOrder**: Text, a written file, a rendered picture, and text again show two
replies and two tool lines in order, the first saying that the file was created, and every line shows the check and
none still shows the spinner. This scenario is tested by `ChatPanelView_ToolCalls_ShowToolRowsInOrder`.

**ChatPanelView_Readiness_ShowsTheSentenceOffersSettingsAndHidesWhenReady**: A panel with a Copilot that is not signed
in shows the banner with the sign-in sentence when it is shown; "Open Settings" raises the request; after the copilot is
ready, "Check again" repeats the check and the banner is hidden. This scenario is tested by
`ChatPanelView_Readiness_ShowsTheSentenceOffersSettingsAndHidesWhenReady`.

**ChatPanelView_NoCopilot_ShowsNoBannerIndicatorOrStop**: A panel without a copilot shows no banner, no indicator, and
no Stop button. This scenario is tested by `ChatPanelView_NoCopilot_ShowsNoBannerIndicatorOrStop`.

**ChatPanelViewModel_TurnRuns_BusyRowShowsPhaseElapsedTimeAndHint**: A message is sent in a panel whose clock is manual.
At the start the busy text is the working text, the elapsed time is `0:00`, and there is no hint; when the clock has
advanced by the hint delay the elapsed time is `0:30`, the hint is the long-wait hint, and the panel says it has a hint;
when the copilot reports that it thinks, the busy text is the thinking text and the hint is gone; the panel announced
`BusyText`, `BusyElapsedText`, `BusyHintText`, and `HasBusyHint`; when the turn has completed all four are empty. This
scenario is tested by `ChatPanelViewModel_TurnRuns_BusyRowShowsPhaseElapsedTimeAndHint`.

**ChatPanelViewModel_Stop_ShowsStoppingAndDisablesStopUntilTheTurnEnds**: A turn runs on a copilot that is slow to stop;
before the Stop the panel is not stopping and Stop can run. After the Stop command the panel is still busy, is stopping,
says "Stopping...", and Stop cannot run, and `IsStopping` was announced; when the copilot ends its stream and the turn
has ended, the panel is not busy, not stopping, and Stop cannot run. This scenario is tested by
`ChatPanelViewModel_Stop_ShowsStoppingAndDisablesStopUntilTheTurnEnds`.

**ChatPanelView_CopilotProgress_BusyRowShowsElapsedTimeAndWhatIsReported**: A message is sent; the busy row is shown
with the elapsed time `0:00` and no hint. When the copilot reports that it thinks, the busy text becomes the thinking
text; when it reports 2 KB of a file `flow.svg` being written, the busy text starts with "Writing `flow.svg`..." and
names the size in KB; the hint is still hidden; when the turn completes the row is hidden. This scenario is tested by
`ChatPanelView_CopilotProgress_BusyRowShowsElapsedTimeAndWhatIsReported`.

**ChatPanelView_Stop_ShowsStoppingUntilTheTurnEnds**: A message is sent to a copilot that ignores the cancellation, some
text arrives, and Stop is pressed: the busy row says "Stopping...", the panel is still busy, and the Stop button cannot
be used; when the copilot ends its stream the reply has the label `Stopped`, Send can be used, and the busy row is
hidden. This scenario is tested by `ChatPanelView_Stop_ShowsStoppingUntilTheTurnEnds`.

**ChatPanelView_CompletedTurn_ShowsTheUsageCaptionUnderTheLastReply**: A turn answers "Done." and reports 12,400 tokens
in, 7,200 out, and the multiplier 15; when it has completed the one reply shows its caption, visible, which starts with
"12.4k in, 7.2k out, model x15 premium". This scenario is tested by
`ChatPanelView_CompletedTurn_ShowsTheUsageCaptionUnderTheLastReply`.

**ChatPanelViewModel_EmptyProject_OffersStarterPrompts**,
**ChatPanelViewModel_TurnWritesDrawing_SuggestionsHideWhileBusyThenBecomeRefinements**,
**ChatPanelViewModel_TurnWritesNotes_StillOffersStarterPrompts**,
**ChatPanelViewModel_OpenProject_WithDrawingOffersRefinementsAndEachProjectIsJudgedAlone**, and
**ChatPanelViewModel_PictureShown_OffersRefinementsWithExport**: These scenarios, in `FollowUpSuggestionsTests.cs`, show
that the panel offers the starter prompts in an empty project, none while a turn runs, the refinements after the copilot
wrote a drawing (not after it wrote a text file), the refinements for a project that already holds a drawing and not
for one whose only picture is an attachment, each project judged by its own folder, and the refinements with the export
while a picture is shown. They are described in the verification of the `FollowUpSuggestions` unit, together with the
planner scenario `FollowUpSuggestions_Plan_FollowsDrawingPictureAndBusy`, and are tested by the tests of those names.

**PushToTalkHints_ChatPanel_KeyCapTooltipAndTipFollowTheSetting**: A panel over a talk key setting that follows the
settings names `F9` in its key cap, its tooltip ("Hold this button, or hold F9, speak, and let go to add what you said
to the message"), and the tip of the empty conversation ("Drop a picture, or hold F9 and speak"); after the settings
change the key to `F6`, all three name `F6` and the panel announced `TalkKeyText`, `EmptyTipText`, and `TalkToolTip`.
This scenario is tested by `PushToTalkHints_ChatPanel_KeyCapTooltipAndTipFollowTheSetting` (in
`PushToTalkHintsTests.cs`).

**AssistantReplyViewModel_StreamThenComplete_GrowsAnnouncesAndIsFinalOnlyWhenCompleted**: A new reply is streaming and
empty; two pieces of text grow it and are announced (an empty piece changes nothing); completing it makes the text
final, and further text, stops, and failures change nothing. This scenario is tested by
`AssistantReplyViewModel_StreamThenComplete_GrowsAnnouncesAndIsFinalOnlyWhenCompleted`.

**AssistantReplyViewModel_StopOrFail_KeepsTextAndNamesWhatBecameOfIt**: A reply that is stopped, or that fails, keeps
its text, is not final, says `Stopped` or `Failed`, and is not changed by a later completion. This scenario is tested by
`AssistantReplyViewModel_StopOrFail_KeepsTextAndNamesWhatBecameOfIt`.

**AssistantReplyViewModel_Restored_IsInTheGivenStateAndRefusesNoText**: A reply made from restored text is in the given
state, does not grow, and refuses a missing text. This scenario is tested by
`AssistantReplyViewModel_Restored_IsInTheGivenStateAndRefusesNoText`.

**ToolActivityRowViewModel_StartFileReportAndFinish_KeepTheReportOnlyWhenTheToolWorked**: A running tool row reports a
file, finishes with success, and keeps the report; a later result that says it failed replaces the report; an ended row
is not abandoned. This scenario is tested by
`ToolActivityRowViewModel_StartFileReportAndFinish_KeepTheReportOnlyWhenTheToolWorked`.

**ToolActivityRowViewModel_RestoredAndAbandoned_AreEndedAndArgumentsAreChecked**: A restored row has ended; a running
row that is abandoned has failed; the arguments are checked. This scenario is tested by
`ToolActivityRowViewModel_RestoredAndAbandoned_AreEndedAndArgumentsAreChecked`.

**ToolActivityRowViewModel_Finish_ShowsOutcomeAndElapsedTime**: A call that started on a clock that the test moves
shows neither the check nor the cross and no time while it runs; after 0.8 seconds it finishes with success and shows
the check and `0.8 s`; a failure that was quicker than a tenth of a second shows the cross and no time; a restored row
shows no time. This scenario is tested by `ToolActivityRowViewModel_Finish_ShowsOutcomeAndElapsedTime`.

**ToolActivityRowViewModel_DisplaySummary_ShowsNoBackticks**: A row whose sentence names a file in markdown backticks
shows the sentence without the backticks, tells the observers when it changes, and keeps the stored sentence as it was.
This scenario is tested by `ToolActivityRowViewModel_DisplaySummary_ShowsNoBackticks`.

**ChatPanelViewModel_Transcript_MarksTheRowsThatOpenAGroup**: A reply, a tool row, a notice, a tool row, and a reply are
added; the first row and the tool row after the notice open a group and the others do not. This scenario is tested by
`ChatPanelViewModel_Transcript_MarksTheRowsThatOpenAGroup`.

**NoticeRowViewModel_ErrorWithRemedyAndInformation_HoldWhatTheyWereGiven**: An error with a remedy and an information
notice hold what they were given, and an empty message is refused. This scenario is tested by
`NoticeRowViewModel_ErrorWithRemedyAndInformation_HoldWhatTheyWereGiven`.

**TranscriptRows_Kinds_TheEntryOfAMessageIsTheUserRow**: The entry of a sent message, a reply, a tool row, and a notice
tell the kinds user, assistant, tool, and notice, and the entry still has its payload and summary. This scenario is
tested by `TranscriptRows_Kinds_TheEntryOfAMessageIsTheUserRow`.

**ChatPanelViewModel_New_NullArguments_Throw**: A missing composer, registry, layer, or mark picture composer is
refused. This scenario is tested by `ChatPanelViewModel_New_NullArguments_Throw`.

**ChatPanelViewModel_New_HasEmptyTranscriptAndCannotSend**: A new panel in an open project has an empty transcript and
an empty message that cannot be sent. This scenario is tested by
`ChatPanelViewModel_New_HasEmptyTranscriptAndCannotSend`.

**ChatPanelViewModel_CanSend_FollowsProjectContentAndTalkButton**: The message can be sent after words are typed, cannot
be sent while the talk button is held, can be sent again after the release, and with the project closed neither sending
nor talking is possible; the change is announced. This scenario is tested by
`ChatPanelViewModel_CanSend_FollowsProjectContentAndTalkButton`.

**ChatPanelViewModel_Send_TypedText_AddsOneEntryAndEmptiesTheDraft**: A typed message is sent: the transcript has one
entry with that text as a typed item, the draft and its history are empty, the summary is empty, and the message has no
payload to inspect. This scenario is tested by `ChatPanelViewModel_Send_TypedText_AddsOneEntryAndEmptiesTheDraft`.

**ChatPanelViewModel_Transcript_TwoMessagesAndADraft_ListsOnlySentMessagesInOrder**: Two messages are sent and a third
is typed; the transcript holds the first two in order. This scenario is tested by
`ChatPanelViewModel_Transcript_TwoMessagesAndADraft_ListsOnlySentMessagesInOrder`.

**ChatPanelViewModel_Send_TypedDictatedAndMark_EntryKeepsOrderOriginsAndSummary**: A message of typed words, dictated
words, and a mark is sent; the entry has the typed item, the dictated item, and the mark chip in that order, and the
summary is `1 mark`. This scenario is tested by
`ChatPanelViewModel_Send_TypedDictatedAndMark_EntryKeepsOrderOriginsAndSummary`.

**ChatPanelViewModel_Send_BuildFails_KeepsDraftAndTranscriptUnchangedAndSaysWhy**: The builder fails; the draft is kept,
the transcript is empty, and the status text says that the message was not sent and why. This scenario is tested by
`ChatPanelViewModel_Send_BuildFails_KeepsDraftAndTranscriptUnchangedAndSaysWhy`.

**ChatPanelViewModel_MarkDrawn_BecomesOneChipStepAndUndoHidesTheMark**: With typed text in the message, a rectangle is
drawn with the layer's methods: the chip is after the text and the layer shows the mark; undo removes the chip and hides
the mark; redo brings both back. This scenario is tested by
`ChatPanelViewModel_MarkDrawn_BecomesOneChipStepAndUndoHidesTheMark`.

**ChatPanelViewModel_DrawingAllowed_FollowsWhetherAMessageCanBeWritten**: The layer allows marks with a project open,
not after it is closed, and again in a project. This scenario is tested by
`ChatPanelViewModel_DrawingAllowed_FollowsWhetherAMessageCanBeWritten`.

**ChatPanelViewModel_Send_MessageWithMark_LayerShowsNoMarkAfterwards**: A message with a mark is sent; the registry and
the layer hold no mark. This scenario is tested by `ChatPanelViewModel_Send_MessageWithMark_LayerShowsNoMarkAfterwards`.

**ChatPanelViewModel_MarkNoteEditRequested_OpensTheNoteOnTheLayer**: A rectangle with the note `first` is drawn on the
layer and its chip is in the message; a request from the chip opens the note of that mark on the layer with `first`;
the note `second` is confirmed there, the composer gives `second` for the chip, and the message holds the same chip.
This scenario is tested by `ChatPanelViewModel_MarkNoteEditRequested_OpensTheNoteOnTheLayer`.

**ChatPanelView_MarkChipDoubleClicked_OpensItsNoteAndChipShowsTheChangedNote**: In the window of the panel a mark with
the note `first` is drawn; its chip's tooltip names the note. Two quick clicks on the chip open the note of the mark on
the layer with `first`; after `second` is confirmed the chip's tooltip names `second`. After the message is sent, the
chip in the transcript offers no change of its note and has no menu. This scenario is tested by
`ChatPanelView_MarkChipDoubleClicked_OpensItsNoteAndChipShowsTheChangedNote`.

**ChatPanelViewModel_TalkText_FollowsDictationState**: The text that names the talk button is `Hold to talk`, becomes
`Starting... keep holding`
while the start waits, `Listening... let go to finish` once listening, and `Hold to talk` again after the release; the
changes are announced. This scenario is tested by `ChatPanelViewModel_TalkText_FollowsDictationState`.

**ChatPanelViewModel_PushToTalk_PressSpeakRelease_AddsOneDictatedBlockAsOneStep**: After typed words, the button is
pressed, the recognizer hears "draw a", and the final text is "draw a circle": the live words were shown, the message
holds one dictated block, and one undo removes it. This scenario is tested by
`ChatPanelViewModel_PushToTalk_PressSpeakRelease_AddsOneDictatedBlockAsOneStep`.

**ChatPanelViewModel_PushToTalk_NoModelChosen_ShowsReasonAndChangesNothing**: Dictation says that no model is chosen; a
press and release leave the reason in the status text, start no listening, and leave the typed words alone. This
scenario is tested by `ChatPanelViewModel_PushToTalk_NoModelChosen_ShowsReasonAndChangesNothing`.

**ChatPanelViewModel_PushToTalk_SecondPressWhileHeld_IsIgnored**: Two presses before the release start listening once.
This scenario is tested by `ChatPanelViewModel_PushToTalk_SecondPressWhileHeld_IsIgnored`.

**ChatPanelViewModel_CanTalk_NoProject_IsFalse**: With the project closed the talk button cannot be used. This scenario
is tested by `ChatPanelViewModel_CanTalk_NoProject_IsFalse`.

**ChatPanelViewModel_PrepareDictation_PreparesOnceAndSwallowsFailure**: The preparation asks the service each time it is
called, and a service that fails causes no exception. This scenario is tested by
`ChatPanelViewModel_PrepareDictation_PreparesOnceAndSwallowsFailure`.

**ChatPanelViewModel_PrepareDictation_NoDictationService_DoesNothing**: A panel made without dictation completes the
preparation without failing. This scenario is tested by
`ChatPanelViewModel_PrepareDictation_NoDictationService_DoesNothing`.

**ChatPanelViewModel_ProjectChanges_DropsTheTranscript**: After a message is sent, another project is opened: the
transcript is empty and `HasTranscript` is announced; a message sent there is the only entry; closing the project
empties it again. This scenario is tested by `ChatPanelViewModel_ProjectChanges_DropsTheTranscript`.

**ChatPanelViewModel_Dispose_StopsFollowingTheComposer**: After the panel is disposed, a request from a mark's chip to
change its note opens nothing on the layer, and a message sent in the composer is not added to its transcript. This
scenario is tested by `ChatPanelViewModel_Dispose_StopsFollowingTheComposer`.

**TranscriptEntryViewModel_ViewPayloadCommand_ShowsInspectorForThePayloadOfTheMessage**: A message with a ready-made
payload is sent; the command gives the launcher an inspector whose payload is that payload and whose description is the
describer's text. This scenario is tested by
`TranscriptEntryViewModel_ViewPayloadCommand_ShowsInspectorForThePayloadOfTheMessage`.

**TranscriptEntryViewModel_ViewPayloadCommand_NoLauncherOrNoPayload_DoesNothing**: Without a launcher the command
completes and does nothing; for a message whose payload is not a message payload it cannot run. This scenario is tested
by `TranscriptEntryViewModel_ViewPayloadCommand_NoLauncherOrNoPayload_DoesNothing`.

**TranscriptEntryViewModel_Summary_MarksAndAttachments_AreCountedWithTheirNouns**: A message with two marks and one file
says `2 marks, 1 attachment` and has three chip items. This scenario is tested by
`TranscriptEntryViewModel_Summary_MarksAndAttachments_AreCountedWithTheirNouns`.

**TranscriptEntryViewModel_Items_AttachedPicture_AsksForTheCopyInTheProject**: A picture that was copied into the
project's attachments is shown as the chip the user dropped, and its picture is asked for from the copy in the project.
This scenario is tested by `TranscriptEntryViewModel_Items_AttachedPicture_AsksForTheCopyInTheProject`.

**ChipThumbnailProvider_GetThumbnail_NoPictureOrFailure_GivesNullAndDoesNotThrow**: A missing function is refused; a
mark without a picture, a mark whose function throws, and a folder chip give no picture and no failure. This scenario is
tested by `ChipThumbnailProvider_GetThumbnail_NoPictureOrFailure_GivesNullAndDoesNotThrow`.

**ChatPanelViewModel_PromptsAndCollapse_WriteTheMessageAndAnnounce**: In a panel with a project open, a picture shown,
and nothing said, the prompts are not shown above the message; using the second of the offered prompts writes it as the
message, and the collapse command raises its request once; the prompts offered are the plan for a project with a drawing
and a picture shown (the refinements and the export), and a missing prompt is refused. This scenario is tested by
`ChatPanelViewModel_PromptsAndCollapse_WriteTheMessageAndAnnounce`.

**ChatPanelView_New_ShowsEmptyTranscriptHintComposerAndButtons**: A new panel shows the question of the empty
conversation, no entries, the editing control bound to the composer with the panel's picture provider, a Send button
that cannot be used, and a talk button that can, named with its first text. This scenario is tested by
`ChatPanelView_New_ShowsEmptyTranscriptHintComposerAndButtons`.

**ChatPanelView_EmptyState_ExamplePromptFillsTheMessageAndSuggestionsWaitForAConversation**: The empty conversation
shows the prompts that the panel plans for the project of the harness (a picture is shown, so the refinements and the
export) as its examples, and none of the prompts above the message; pressing the first example writes it as the message
and enables Send; sending it hides the question and shows the same prompts above the message. This scenario is tested
by `ChatPanelView_EmptyState_ExamplePromptFillsTheMessageAndSuggestionsWaitForAConversation`.

**ChatPanelView_CollapseButton_RaisesCollapseRequested**: Using the button of the header raises the request to hide the
panel once. This scenario is tested by `ChatPanelView_CollapseButton_RaisesCollapseRequested`.

**ChatPanelView_ComposerCard_PlaceholderNamesAndHint**: A new panel in an open project shows the placeholder text over
the empty message; the Send and attach buttons are named `Send` and `Attach files`, the talk button is named with the
talk text and has a tooltip, and the hint says `Enter to send`; typing hides the placeholder. This scenario is tested by
`ChatPanelView_ComposerCard_PlaceholderNamesAndHint`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: In the workspace of an open project, in the dark and
the light theme, every button of the panel that shows no text has a name and a tooltip, takes the keyboard focus, and
is at least 28 by 28 pixels. This scenario is tested by `Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.

**ChatPanelView_EnterKey_SendsTheMessageAndShiftEnterWritesALineBreak**: With a message in the focused editing control,
Shift+Enter writes a second line and sends nothing; Enter on its own then sends the message. This scenario is tested by
`ChatPanelView_EnterKey_SendsTheMessageAndShiftEnterWritesALineBreak`.

**ChatPanelView_AttachButton_AddsThePickedFilesAsChips**: A picker that chooses one file is set; using the attach button
asks it once, from the project folder, and the message gets one file chip. This scenario is tested by
`ChatPanelView_AttachButton_AddsThePickedFilesAsChips`.

**AnnotationToolbarView_ClearMarks_TakesTheMarkChipsOutOfTheMessage**: With two marks on a picture and two mark chips in
the message, using the clear button of the annotation toolbar takes both chips out, shows no mark on the picture, and
disables the button. This scenario is tested by `AnnotationToolbarView_ClearMarks_TakesTheMarkChipsOutOfTheMessage`.

**ChatPanelView_Shown_PreparesDictation**: Showing the panel prepares dictation once. This scenario is tested by
`ChatPanelView_Shown_PreparesDictation`.

**ChatPanelView_NoProject_SaysSoAndDisablesSendAndTalk**: With no project the editing control says that a project must
be open and both buttons cannot be used. This scenario is tested by
`ChatPanelView_NoProject_SaysSoAndDisablesSendAndTalk`.

**ChatPanelView_Send_TypedText_AppearsInTranscriptAndEmptiesTheEditor**: After text is typed the Send button can be
used; pressing it shows the message in the transcript, hides the hint, empties the editing control, and disables Send.
This scenario is tested by `ChatPanelView_Send_TypedText_AppearsInTranscriptAndEmptiesTheEditor`.

**ChatPanelView_Send_TwoMessages_AreListedInOrder**: Two messages are sent and shown in order. This scenario is tested
by `ChatPanelView_Send_TwoMessages_AreListedInOrder`.

**ChatPanelView_Send_MessageWithChip_BubbleSizesToTheChipRowAndTheWords**: A message with a mark chip and the words
`Chart downloads by month` is sent. The bubble holds a row for the chip that is as high as the chip and a text block
below it that is at least one line high, and the bubble is as high as the two with its padding, so that neither is cut
off. This scenario is tested by `ChatPanelView_Send_MessageWithChip_BubbleSizesToTheChipRowAndTheWords`.

**RestoredEntryViewModel_New_ChipLabelGluedToWords_GetsASpace**: A saved row whose chip label is followed at once by a
word (`[Mark 1]Chart`), as an earlier version saved it, is shown with a space after the label; a label followed by a
line feed, a space, or punctuation is not changed. This scenario is tested by
`RestoredEntryViewModel_New_ChipLabelGluedToWords_GetsASpace`.

**ChatPanelView_Send_MessageWithMarkAndFile_ShowsChipsInTranscriptWithoutDeleteButtons**: A message with a mark, text,
and a dropped file is sent; the entry shows `Mark 1` and `notes.txt` as chips without delete buttons, the summary says
`1 mark, 1 attachment`, and the file is in the project's attachments. This scenario is tested by
`ChatPanelView_Send_MessageWithMarkAndFile_ShowsChipsInTranscriptWithoutDeleteButtons`.

**ChatPanelView_Send_DictatedWords_AreShownWithTheDictatedBackground**: A message of typed and dictated words is sent;
the transcript text has a typed run without background and a dictated run with one. This scenario is tested by
`ChatPanelView_Send_DictatedWords_AreShownWithTheDictatedBackground`.

**ChatPanelView_MarkChipInDraft_ShowsThePictureAroundTheMark**: A mark is added to the message; its chip shows the
picture that the provider makes for it. This scenario is tested by
`ChatPanelView_MarkChipInDraft_ShowsThePictureAroundTheMark`.

**ChatPanelView_HoldToTalk_PressSpeakRelease_ListensShowsWordsAndAddsOneBlock**: The pointer is pressed on the talk
button: it listens, its name says so, the words so far are shown, and Send cannot be used. Releasing the pointer on the
button adds the final words to the message, restores the name, and enables Send. This scenario is tested by
`ChatPanelView_HoldToTalk_PressSpeakRelease_ListensShowsWordsAndAddsOneBlock`.

**ChatPanelView_HoldToTalk_ReleaseOutsideTheButton_StillEndsListening**: After the pointer moved away, releasing it over
the transcript ends listening and adds the words. This scenario is tested by
`ChatPanelView_HoldToTalk_ReleaseOutsideTheButton_StillEndsListening`.

**ChatPanelView_HoldToTalk_PointerCaptureLost_EndsListening**: The button loses the pointer capture while held;
listening ends. This scenario is tested by `ChatPanelView_HoldToTalk_PointerCaptureLost_EndsListening`.

**ChatPanelView_HoldToTalk_SpaceKeyHeldOnFocusedButton_ListensUntilReleased**: Holding the space key on the focused
button listens and releasing it adds the words. This scenario is tested by
`ChatPanelView_HoldToTalk_SpaceKeyHeldOnFocusedButton_ListensUntilReleased`.

**ChatPanelView_HoldToTalk_PanelRemovedWhileHeld_EndsListening**: The panel leaves the window while the button is held;
listening ends. This scenario is tested by `ChatPanelView_HoldToTalk_PanelRemovedWhileHeld_EndsListening`.

**ChatPanelView_HoldToTalk_NoModelChosen_ShowsReasonInPanelAndChangesNothing**: With no model chosen, pressing and
releasing the button shows the reason in the panel's status line, starts no listening, and leaves the typed words alone.
This scenario is tested by `ChatPanelView_HoldToTalk_NoModelChosen_ShowsReasonInPanelAndChangesNothing`.

**ChatPanelView_MarkDrawn_ChipAppearsInTheEditorAndUndoRemovesIt**: A rectangle drawn with the layer puts a chip after
the typed text in the editing control and shows the mark; Ctrl+Z removes both. This scenario is tested by
`ChatPanelView_MarkDrawn_ChipAppearsInTheEditorAndUndoRemovesIt`.

**ChatPanelView_ViewPayloadButton_ShowsInspectorOfThatMessage**: Pressing "View payload" on a sent message gives the
launcher an inspector for the payload of that message. This scenario is tested by
`ChatPanelView_ViewPayloadButton_ShowsInspectorOfThatMessage`.

**ChatPanelViewModel_TurnCompleted_SavesTextRowsOutsideTheProject**: A message is answered with a reply and a tool call
that creates `box.svg`; the history store, whose root is outside the project, then holds a user row, a completed
assistant row, and a tool row for `project_write` that names the file, and the project folder is empty. This scenario is
tested by `ChatPanelViewModel_TurnCompleted_SavesTextRowsOutsideTheProject`.

**ChatPanelViewModel_TurnStopped_SavesTheStoppedReply**: A turn that has written "Let me think about" is stopped; the
saved rows are the message and an assistant row with that text whose reply state is stopped. This scenario is tested by
`ChatPanelViewModel_TurnStopped_SavesTheStoppedReply`.

**ChatPanelViewModel_TurnFailed_SavesTheMessageButNotTheNotice**: A turn fails at once; the transcript has the error
notice, and the saved history has the message only. This scenario is tested by
`ChatPanelViewModel_TurnFailed_SavesTheMessageButNotTheNotice`.

**ChatPanelViewModel_OpenProjectWithHistory_ShowsSavedConversationAsTextRows**: A project that has a saved conversation
is opened in a panel that has no project; the transcript has four rows in order: a restored message with its words,
the summary `1 mark, 1 attachment`, and the lines of the mark note and the attachment path, a completed reply, a tool
row that failed, and a stopped reply; the panel lists no sent message. This scenario is tested by
`ChatPanelViewModel_OpenProjectWithHistory_ShowsSavedConversationAsTextRows`.

**ChatPanelViewModel_New_ProjectAlreadyOpen_RestoresTheConversation**: A panel that is made while the project is open
shows its saved conversation at once. This scenario is tested by
`ChatPanelViewModel_New_ProjectAlreadyOpen_RestoresTheConversation`.

**ChatPanelViewModel_SendAfterRestore_GivesTheCopilotTheSavedTextAsRestoredHistory**: After a restore, the first request
that reaches the copilot carries four turns: the user's words with the mark note and the attachment, the reply, the
tool call as a line of the assistant that says it failed, and the stopped reply with a remark; the next request carries
those and the new message and reply. This scenario is tested by
`ChatPanelViewModel_SendAfterRestore_GivesTheCopilotTheSavedTextAsRestoredHistory`.

**ChatPanelViewModel_ProjectChanges_EachProjectHasItsOwnConversation**: A conversation in a first project; a second
project is opened with an empty transcript and gets no restored history and its own saved words; closing shows nothing;
opening the first project again shows its message as a restored row. This scenario is tested by
`ChatPanelViewModel_ProjectChanges_EachProjectHasItsOwnConversation`.

**ChatPanelViewModel_ProjectChangesWhileTurnRuns_SavesNothingIntoTheNewProject**: A turn that runs in the first project
is overtaken by opening a second one; the transcript is empty and the second project has no history file. This scenario
is tested by `ChatPanelViewModel_ProjectChangesWhileTurnRuns_SavesNothingIntoTheNewProject`.

**ChatPanelViewModel_SaveFails_ShowsAnErrorNoticeAndTheConversationGoesOn**: With a store that cannot save, each of two
turns leaves an error notice with the sentence of the store, and the second turn is run. This scenario is tested by
`ChatPanelViewModel_SaveFails_ShowsAnErrorNoticeAndTheConversationGoesOn`.

**ChatPanelViewModel_HistoryRootInsideTheProject_SaysSoAndWritesNothingIntoTheProject**: With a store whose root is
inside the project, a turn leaves one notice that the history lies inside the project folder, and the project folder is
empty. This scenario is tested by `ChatPanelViewModel_HistoryRootInsideTheProject_SaysSoAndWritesNothingIntoTheProject`.

**ChatPanelViewModel_NoCopilot_SavesTheMessageWhenItIsSent**: A panel without a copilot saves the message when it is
sent. This scenario is tested by `ChatPanelViewModel_NoCopilot_SavesTheMessageWhenItIsSent`.

**ChatPanelViewModel_NoStoreOrNoProjectService_SavesAndRestoresNothing**: A panel without a store, and one without a
project service, restore nothing from a saved conversation; a message in the second gives the copilot no restored
history and leaves the saved conversation as it was. This scenario is tested by
`ChatPanelViewModel_NoStoreOrNoProjectService_SavesAndRestoresNothing`.

**ChatHistoryMapper_ToHistoryRows_SentMessageAndRows_KeepsWordsLabelsNotesAndPathsOnly**: A message of words, a mark
with a note, dictated words, and an attached picture becomes a user row with the labels `[Mark 1]` and
`[Attachment: logo.png]` in their places, the mark's number and note, and the project-relative path, dated with the time
the message was sent; a running tool, a reply without text, and a notice are left out; a finished tool and a failed
reply become rows; the words of the payload are not in any row. This scenario is tested by
`ChatHistoryMapper_ToHistoryRows_SentMessageAndRows_KeepsWordsLabelsNotesAndPathsOnly`.

**ChatHistoryMapper_ToTranscriptRowAndToAgentTurns_CoverEveryKind**: Every kind of saved row becomes the row of the
transcript that shows it (a restored message, a reply in its state, a tool row with its outcome), and becomes the turn
for a new provider session, with the remark for a failed reply and the line `[Used the tool ...]` for a tool call. This
scenario is tested by `ChatHistoryMapper_ToTranscriptRowAndToAgentTurns_CoverEveryKind`.

**RestoredEntryViewModel_New_OnlyUserRows_AndSummaryCountsMarksAndAttachments**: The row of a restored message counts
marks and attachments in the plural, lists the marks in the order of their numbers with their notes, then the
attachments, has the kind of the user row, refuses a row of another kind and a missing row. This scenario is tested by
`RestoredEntryViewModel_New_OnlyUserRows_AndSummaryCountsMarksAndAttachments`.

**ChatPanelViewModel_ClearHistory_DeletesTheStoredHistoryEmptiesTheTranscriptAndResetsTheCopilot**: After a restore and
one more turn, Clear deletes the folder of the history, leaves only the notice "Chat history cleared." in the
transcript, resets the copilot once, and the next message gets no restored history and is the only saved row. This
scenario is tested by `ChatPanelViewModel_ClearHistory_DeletesTheStoredHistoryEmptiesTheTranscriptAndResetsTheCopilot`.

**ChatPanelViewModel_ClearHistory_WhileTurnRuns_StopsItDropsItsRowsAndSavesNothing**: Clear while a turn writes text
cancels the turn, shows only the notice, frees the composer, and leaves no history on the disk. This scenario is tested
by `ChatPanelViewModel_ClearHistory_WhileTurnRuns_StopsItDropsItsRowsAndSavesNothing`.

**ChatPanelViewModel_ClearHistory_WithoutStoreAndCopilot_EmptiesTheTranscript**: Clear in a panel with neither a store
nor a copilot empties the transcript and shows the notice. This scenario is tested by
`ChatPanelViewModel_ClearHistory_WithoutStoreAndCopilot_EmptiesTheTranscript`.

**ChatPanelViewModel_ClearHistory_StoreCannotDelete_ShowsAnErrorNotice**: With a store that cannot delete, Clear shows
an error notice with its sentence, empties the transcript, and resets the copilot. This scenario is tested by
`ChatPanelViewModel_ClearHistory_StoreCannotDelete_ShowsAnErrorNotice`.

**ChatPanelView_ClearChatHistoryButton_ClearsTranscriptDeletesHistoryAndSendStaysEnabled**: In a window, after an
answered message that was saved, the overflow button is shown and its menu holds the "Clear chat history" item; using it
removes the message and the reply
from the view, shows one notice with the cleared text, deletes the folder of the history, resets the copilot, keeps the
text typed meanwhile, and leaves Send enabled. This scenario is tested by
`ChatPanelView_ClearChatHistoryButton_ClearsTranscriptDeletesHistoryAndSendStaysEnabled`.

**ChatPanelView_ClearChatHistoryButton_NothingSaid_ShowsOnlyTheNotice**: In a panel with no store and no copilot,
using the item shows only the notice. This scenario is tested by
`ChatPanelView_ClearChatHistoryButton_NothingSaid_ShowsOnlyTheNotice`.

**ChatPanelView_RestoredHistory_ShowsTextRowsWithoutPayloadButtonOrPictures**: A project with a saved conversation is
opened in a window: the restored message shows its words, the lines of the mark note and the attachment, and the note
that pictures are not kept, and has no "View payload" button and no chip; the reply and the tool line are shown as
usual; the hint of the empty transcript is hidden. This scenario is tested by
`ChatPanelView_RestoredHistory_ShowsTextRowsWithoutPayloadButtonOrPictures`.

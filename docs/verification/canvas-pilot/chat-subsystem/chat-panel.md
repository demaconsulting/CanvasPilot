### ChatPanel

#### Verification Approach

`ChatPanel` is verified with unit tests in `test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatPanelTests.cs` for
the view models and the picture provider, and with headless Avalonia UI tests in
`test/DemaConsulting.CanvasPilot.UiTests/ChatPanelViewTests.cs` (with `ChatUiTestSupport.cs` for the harness) for the
views. Both use the real `ChatPanelViewModel`, `ComposerViewModel`, `ComposerDocument`, `ComposerHistory`,
`AttachmentIntake`, `AttachmentStore`, `MarkRegistry`, and `AnnotationOverlayViewModel` over a temporary project folder,
because the unit exists to connect them and the rule under test is how they agree. The UI tests also use the real
`ChatPanelView`, `ComposerEditorView` with the AvaloniaEdit surface, and `TranscriptEntryView`. Only what has no place
in a panel test is replaced: the dictation service (a controllable fake in the unit tests, a scripted one in the UI
tests, both with a session that says words and ends with a chosen text), the builder of sent messages (one that gives a
text, or a ready-made payload), the source of the shown picture, the composer of mark pictures (a substitute, or a stub
that gives a one-pixel PNG), and the launcher of the payload inspector (a recording one). Input reaches the views as the
platform delivers it: pointer presses and releases on the talk button, move events away from it, the loss of the pointer
capture, space key presses and releases on the focused button, text input and Ctrl+Z in the editing control, and clicks
on the Send and "View payload" buttons.

#### Test Environment

The standard test runner, and for the views the headless Avalonia platform with the Skia back end and the application's
own `App`, which loads the style of the editing control. A temporary folder per test holds the project and the files
that are dropped, and is removed afterwards (a preview that is still being decoded can hold a file for a moment, so
removal retries). No window is shown on a screen, no real microphone or speech model is used, and the real operating
system pointer and keyboard are not involved.

The checks by hand before a release are listed in the verification of the `Chat` subsystem (real dictation, the Windows
microphone privacy setting, the cases without model, microphone, or native files, the release outside the button, and
the looks); they are not evidence for a requirement.

#### Acceptance Criteria

- All unit tests and UI tests of the unit pass with zero failures.
- The transcript lists exactly the sent messages, oldest first, never the draft; each entry shows its words in order,
  with dictated words shown differently from typed ones, and its chips in their places without delete buttons, with
  their names; the summary says how many marks and attachments it carried and is hidden when there are none.
- The transcript is dropped when the project changes or closes, and a disposed panel no longer follows the composer.
- The Send button can be used exactly when the composer can send; sending moves the message to the transcript and
  empties the editing control; a failure keeps the draft, adds nothing, and shows the composer's reason.
- A mark drawn on the picture becomes one chip at the caret as one undo step; undo removes the chip and hides the mark
  and redo restores both; marks are allowed exactly while a message can be written; no mark remains after sending.
- Pressing the pointer on the talk button listens and releasing it anywhere, losing the capture, removing the panel, or
  (with the keyboard) releasing the space key ends it, once for each hold; the label follows the state; the words so far
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

#### Test Scenarios

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

**ChatPanelViewModel_TalkText_FollowsDictationState**: The label is `Hold to talk`, becomes `Starting... keep holding`
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

**ChatPanelViewModel_Dispose_StopsFollowingTheComposer**: After the panel is disposed, a message sent in the composer is
not added to its transcript. This scenario is tested by `ChatPanelViewModel_Dispose_StopsFollowingTheComposer`.

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

**ChatPanelView_New_ShowsEmptyTranscriptHintComposerAndButtons**: A new panel shows the hint that messages appear there,
no entries, the editing control bound to the composer with the panel's picture provider, a Send button that cannot be
used, and a talk button that can, with its first label. This scenario is tested by
`ChatPanelView_New_ShowsEmptyTranscriptHintComposerAndButtons`.

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
button: it listens, the label says so, the words so far are shown, and Send cannot be used. Releasing the pointer on the
button adds the final words to the message, restores the label, and enables Send. This scenario is tested by
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

### ComposerViewModel

#### Verification Approach

`ComposerViewModel` is verified with unit tests in `ComposerViewModelTests.cs`. The real `ComposerDocument`,
`ComposerHistory`, `AttachmentIntake`, and `MarkRegistry` are used, because the view model exists to connect them and
they have no user interface. Everything with a side effect or a thread is replaced: the attachment store is an
NSubstitute substitute that accepts every attachment and records its calls; the dictation service and its session are
hand-written fakes (`FakeDictationService`, `FakeDictationSession`) that the test controls, which can hold back the
start until the test lets it go, ignore its cancellation, hear words, fail, and count how often a session was ended and
disposed; the message builder (`FakeMessageBuilder`) records what it was given, can be held back, and can fail; and a
synchronization context that only queues work (`QueueingSynchronizationContext`) stands for the interface thread, so
that the move of live words from the thread of the recognizer can be seen. Dropped content is real files in a temporary
folder, so that the checks of the intake are the real ones. Every rule of the granularity of undo is tested through the
view model's methods, as the editing control will call them. The units together, with the real store and the real
payload builder, are verified by the subsystem tests.

#### Test Environment

A temporary folder per test holds the project folder and the files that are dropped from outside it, and is removed
afterwards. The tests that hold the builder back wait with a spin of at most ten seconds for the builder to be called
from the background thread. No window, network, microphone, or special permission is needed.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A typed or deleted character, a deleted selection, typing over a selection, a dictated block, a drop or paste of any
  number of items or of text, an added mark, and a deleted chip are each exactly one undo step; actions that change
  nothing are not steps; the view model reports one change for each action; undo puts the caret back and redo gives the
  same steps.
- The commands and the undo and redo interface follow the history and are told about each change.
- A drop or paste adds what the intake accepted as one step, names what it left out and why, and adds nothing when
  nothing was accepted.
- Without a project nothing can be edited and the status says to open one; changing the project drops the draft,
  history, marks, and saved pictures; the same project changes nothing; an unusable folder is explained.
- Sending puts the attachments into the project, gives the builder the snapshot, the marks that have chips, and the
  staged attachments, raises one event with the sent message, and leaves an empty draft with no history, no marks, and
  no saved pictures; it refuses an empty message, a message while the talk button is held, and a second send; the draft
  cannot be changed while it is sent.
- While sending is blocked from outside (the copilot is working) the message cannot be sent, the send command cannot
  run, and the status text gives the reason, while editing stays possible; lifting the block restores sending.
- When staging fails, the build fails, the send is canceled, or the project is closed meanwhile, nothing is changed, no
  event is raised, the status says why, and the same draft can be sent again.
- Holding the talk button shows the live words, and releasing it adds one dictated block at the caret over the selection
  as one step; every state of dictation (`Starting`, `Listening`, `Finishing`, `Idle`) is passed in order; presses and
  releases in the wrong state are ignored; a release before listening started gives up, and a session that arrives
  anyway is used; live words from another thread are moved to the caller's thread and dropped after the release; closing
  the project or disposing the view model releases a session that is listening.
- Each reason why dictation cannot work, a start without a session, a failure to start, a failure to end, a problem of
  the session, and a dictation without words each produce their own explanation and leave the draft as it is.

#### Test Scenarios

**ComposerViewModel_InsertTyped_EachCharacterIsOneStep**: Three characters are typed one at a time, as the control
reports them; the caret is after them and each undo removes one character. This scenario is tested by
`ComposerViewModel_InsertTyped_EachCharacterIsOneStep`.

**ComposerViewModel_ApplyTextChange_ReportedChange_IsAppliedAtItsOffsetAsTypedText**: The control reports that "world"
of "Hello world" was replaced by "there"; the text is typed text, the caret is after it, and one undo restores "world".
This scenario is tested by `ComposerViewModel_ApplyTextChange_ReportedChange_IsAppliedAtItsOffsetAsTypedText`.

**ComposerViewModel_BackspaceAndDelete_EachCharacterIsOneStep**: With the caret after "b" of "abcd", Backspace removes
"b" and Delete removes "c", each as one step. This scenario is tested by
`ComposerViewModel_BackspaceAndDelete_EachCharacterIsOneStep`.

**ComposerViewModel_BackspaceAtStartAndDeleteAtEnd_AreNoStep**: Backspace at the start and Delete at the end change
nothing and are no step. This scenario is tested by `ComposerViewModel_BackspaceAtStartAndDeleteAtEnd_AreNoStep`.

**ComposerViewModel_Delete_SelectionOfTextAndChip_IsOneStep**: A selection that covers text, a chip, and text is deleted
with Backspace as one step and one undo brings it all back. This scenario is tested by
`ComposerViewModel_Delete_SelectionOfTextAndChip_IsOneStep`.

**ComposerViewModel_InsertTyped_OverSelection_IsOneStep**: Typing "XYZ" over a selection replaces it as one step and one
undo restores it. This scenario is tested by `ComposerViewModel_InsertTyped_OverSelection_IsOneStep`.

**ComposerViewModel_AddDictatedBlock_ManyWords_IsOneStepOfDictatedText**: A block of eight words is dictated text after
the typed text, moves the caret to its end, and is removed by one undo. This scenario is tested by
`ComposerViewModel_AddDictatedBlock_ManyWords_IsOneStepOfDictatedText`.

**ComposerViewModel_AddDictatedBlock_NothingToAdd_IsNoStep**: Empty text, and text of control characters only, add
nothing and are no step. This scenario is tested by `ComposerViewModel_AddDictatedBlock_NothingToAdd_IsNoStep`.

**ComposerViewModel_AddDictatedBlock_TextThatLooksLikeAChip_StaysCleanedText**: Dictated text with the chip placeholder
and "[Mark 1]" makes no chip; the placeholder is removed. This scenario is tested by
`ComposerViewModel_AddDictatedBlock_TextThatLooksLikeAChip_StaysCleanedText`.

**ComposerViewModel_HandleDrop_ThreeFilesAndTwoPictures_IsOneStep**: Three files and two pasted pictures are dropped at
the caret after a typed start; five chips are added as one step, and one undo removes all of them and puts the caret
back. This scenario is tested by `ComposerViewModel_HandleDrop_ThreeFilesAndTwoPictures_IsOneStep`.

**ComposerViewModel_HandleDrop_AtOffset_PutsItemsThere**: A file dropped at offset 2 of "abcd" becomes a chip between
"ab" and "cd". This scenario is tested by `ComposerViewModel_HandleDrop_AtOffset_PutsItemsThere`.

**ComposerViewModel_HandlePaste_TextAndItems_AreOneStepAndReplaceSelection**: A long text pasted over a selection
replaces it as one step, and a pasted file is a second step at the caret; undo takes them back one at a time. This
scenario is tested by `ComposerViewModel_HandlePaste_TextAndItems_AreOneStepAndReplaceSelection`.

**ComposerViewModel_AddMark_IsOneStepAndUndoRemovesTheChip**: The chip of a mark is added at the caret as one step with
the mark's number; undo removes it and redo brings it back; the mark stays in the registry. This scenario is tested by
`ComposerViewModel_AddMark_IsOneStepAndUndoRemovesTheChip`.

**ComposerViewModel_AddMark_MarkOfAnotherRegistry_ThrowsArgumentException**: A mark of another registry and a missing
mark are refused and the draft is unchanged. This scenario is tested by
`ComposerViewModel_AddMark_MarkOfAnotherRegistry_ThrowsArgumentException`.

**ComposerViewModel_DeleteChip_IsOneStepAndUndoRestoresItAtTheSameOffset**: A chip between two texts is deleted as one
step, undo puts it back between them, and a text position is refused. This scenario is tested by
`ComposerViewModel_DeleteChip_IsOneStepAndUndoRestoresItAtTheSameOffset`.

**ComposerViewModel_Redo_MixedActions_GivesTheSameStepsBack**: Typing, a dictated block, a drop, a mark, and a deleted
chip are undone state by state to the empty draft and redone state by state to the end. This scenario is tested by
`ComposerViewModel_Redo_MixedActions_GivesTheSameStepsBack`.

**ComposerViewModel_InsertTyped_AfterUndo_DiscardsRedo**: Typing after an undo leaves nothing to redo. This scenario is
tested by `ComposerViewModel_InsertTyped_AfterUndo_DiscardsRedo`.

**ComposerViewModel_UndoRedoCommands_CanExecuteFollowsHistoryAndRunsSteps**: The commands cannot execute on an empty
draft, can after typing, tell their listeners about each change, and run the steps. This scenario is tested by
`ComposerViewModel_UndoRedoCommands_CanExecuteFollowsHistoryAndRunsSteps`.

**ComposerViewModel_UndoRedoTarget_ReportsStateAndSteps**: Through `IUndoRedoTarget` the state and the steps are the
same, the change is announced after each action (three times for type, undo, redo), and the property changes of
`CanUndo`, `CanRedo`, and `CanSend` are raised. This scenario is tested by
`ComposerViewModel_UndoRedoTarget_ReportsStateAndSteps`.

**ComposerViewModel_UndoAndRedo_NothingToDo_DoNothing**: Undo and redo of an empty history raise no change. This
scenario is tested by `ComposerViewModel_UndoAndRedo_NothingToDo_DoNothing`.

**ComposerViewModel_Undo_RestoresCaretOfTheStep**: Undo puts the caret where it was before a dictated block in the
middle of a text, and redo puts it after the block. This scenario is tested by
`ComposerViewModel_Undo_RestoresCaretOfTheStep`.

**ComposerViewModel_SetSelection_OutsideMessage_IsBroughtBackInside**: A caret beyond the end and a selection that
sticks out are clamped into the message. This scenario is tested by
`ComposerViewModel_SetSelection_OutsideMessage_IsBroughtBackInside`.

**ComposerViewModel_Changed_EachActionRaisesItOnce**: Typing, a drop of three items, a dictated block, a mark, a deleted
chip, an undo, and a redo each raise `Changed` exactly once. This scenario is tested by
`ComposerViewModel_Changed_EachActionRaisesItOnce`.

**ComposerViewModel_HandleDrop_NothingAccepted_IsNoStepAndNamesWhatWasLeftOut**: A missing path adds nothing, is no
step, and the status starts with "Left out:" and names the path. This scenario is tested by
`ComposerViewModel_HandleDrop_NothingAccepted_IsNoStepAndNamesWhatWasLeftOut`.

**ComposerViewModel_HandleDrop_GoodAndBadItems_AddsTheGoodOnesAndNamesTheBadOnes**: A file that exists and one that does
not give one chip as one step and a status that names the missing one. This scenario is tested by
`ComposerViewModel_HandleDrop_GoodAndBadItems_AddsTheGoodOnesAndNamesTheBadOnes`.

**ComposerViewModel_HandleDrop_ManyLeftOut_NamesThreeAndCountsTheRest**: Five missing paths name the first three and say
that two more were left out. This scenario is tested by
`ComposerViewModel_HandleDrop_ManyLeftOut_NamesThreeAndCountsTheRest`.

**ComposerViewModel_HandlePaste_PictureRefusedByStore_IsLeftOutWithReason**: A pasted picture that the store refuses is
left out with the store's reason while the pasted text is added. This scenario is tested by
`ComposerViewModel_HandlePaste_PictureRefusedByStore_IsLeftOutWithReason`.

**ComposerViewModel_StatusText_NextAction_ClearsIt**: The status of a refused drop is gone after the next typed text.
This scenario is tested by `ComposerViewModel_StatusText_NextAction_ClearsIt`.

**ComposerViewModel_NoProject_EditingIsRefusedAndStatusExplains**: Without a project typing, dropping, and dictating do
nothing, editing and sending are disabled, and the status says to open a project. This scenario is tested by
`ComposerViewModel_NoProject_EditingIsRefusedAndStatusExplains`.

**ComposerViewModel_SetProject_OtherProject_DropsDraftHistoryMarksAndSavedPictures**: A draft with text, a pasted
picture, and a mark is dropped when another project is opened; both stores were asked to remove leftovers with nothing
to keep, and the new project can be written in. This scenario is tested by
`ComposerViewModel_SetProject_OtherProject_DropsDraftHistoryMarksAndSavedPictures`.

**ComposerViewModel_SetProject_SameProject_KeepsDraft**: Setting the open project again keeps the draft and its history.
This scenario is tested by `ComposerViewModel_SetProject_SameProject_KeepsDraft`.

**ComposerViewModel_SetProject_Null_ClosesTheProjectAndDropsTheDraft**: Closing the project empties the draft and
refuses editing with the message to open a project. This scenario is tested by
`ComposerViewModel_SetProject_Null_ClosesTheProjectAndDropsTheDraft`.

**ComposerViewModel_SetProject_UnusableFolder_ExplainsAndHasNoProject**: A store factory that refuses the folder leaves
no project and puts the reason into the status. This scenario is tested by
`ComposerViewModel_SetProject_UnusableFolder_ExplainsAndHasNoProject`.

**ComposerViewModel_ClearDraft_EmptiesEverything**: Clearing empties the draft, the history, and the marks and asks the
store to remove the saved pictures. This scenario is tested by `ComposerViewModel_ClearDraft_EmptiesEverything`.

**ComposerViewModel_SendAsync_TextAttachmentAndMark_StagesBuildsClearsAndRaisesEvent**: A draft with typed and dictated
text, a file, and a mark, and a second mark without a chip, is sent. The store stages exactly the attachment chips, the
builder gets the snapshot, only the mark with the chip, and the staged attachments, one event carries the sent message
with its payload, marks, attachments, and time, and the draft, caret, history, marks, and send state are empty and the
store removed leftovers with nothing to keep. This scenario is tested by
`ComposerViewModel_SendAsync_TextAttachmentAndMark_StagesBuildsClearsAndRaisesEvent`.

**ComposerViewModel_SendAsync_ClearsUndoStack**: After a draft with undone and redone steps is sent, undo and redo do
nothing to the sent message and undoing the next message stops at the empty draft. This scenario is tested by
`ComposerViewModel_SendAsync_ClearsUndoStack`.

**ComposerViewModel_SendAsync_EmptyBlankAndChipOnlyMessages_AreHandledAsExpected**: An empty draft and a draft of spaces
are not sent and the second says to write a message; a draft of one chip is sent. This scenario is tested by
`ComposerViewModel_SendAsync_EmptyBlankAndChipOnlyMessages_AreHandledAsExpected`.

**ComposerViewModel_SendAsync_StagingFails_KeepsDraftAndSaysWhich**: The store refuses an attachment; the draft, the
history, and the marks are unchanged, the builder is not called, no event is raised, and the status holds the store's
sentence that names the attachment. This scenario is tested by
`ComposerViewModel_SendAsync_StagingFails_KeepsDraftAndSaysWhich`.

**ComposerViewModel_SendAsync_BuildFails_KeepsDraftAndSaysWhy**: The builder fails; the draft and its step stay, the
status gives the reason, and a second send of the same draft works once the cause is gone. This scenario is tested by
`ComposerViewModel_SendAsync_BuildFails_KeepsDraftAndSaysWhy`.

**ComposerViewModel_SendAsync_WhileSending_DraftCannotBeChanged**: While the builder waits, the view model is sending,
editing, dropping, undoing, and sending again do nothing, and after the build finishes the message was sent once and
editing works again. This scenario is tested by
`ComposerViewModel_SendAsync_WhileSending_DraftCannotBeChanged`.

**ComposerViewModel_SendAsync_ProjectClosedMeanwhile_IsNotSent**: The project is closed while the builder waits; the
message is not sent and no event is raised. This scenario is tested by
`ComposerViewModel_SendAsync_ProjectClosedMeanwhile_IsNotSent`.

**ComposerViewModel_SendAsync_Canceled_KeepsDraft**: A send that is canceled while the builder waits keeps the draft and
says that it was canceled. This scenario is tested by `ComposerViewModel_SendAsync_Canceled_KeepsDraft`.

**ComposerViewModel_SendCommand_ExecutesOnlyWithContent**: The send command cannot execute on an empty draft, and when
it executes it sends the message. This scenario is tested by `ComposerViewModel_SendCommand_ExecutesOnlyWithContent`.

**ComposerViewModel_SendAsync_WhileTalkButtonHeld_IsRefused**: A send while the talk button is held is refused with the
request to let go first. This scenario is tested by `ComposerViewModel_SendAsync_WhileTalkButtonHeld_IsRefused`.

**ComposerViewModel_BlockSending_CannotSendButCanEdit_UnblockRestoresAndClearsTheReason**: After `BlockSending` with the
text that the chat gives while the copilot works, `CanSend` is false, the send command cannot run, `IsSendBlocked` and
`SendBlockedReason` say so, the status text shows the reason, and the message can still be edited; after
`UnblockSending` the message can be sent and the reason is gone from the property and the status. This scenario is
tested by `ComposerViewModel_BlockSending_CannotSendButCanEdit_UnblockRestoresAndClearsTheReason`.

**ComposerViewModel_SendAsync_WhileBlocked_IsRefusedWithTheReasonAndKeepsTheDraft**: A send while sending is blocked is
refused with the reason as the status text, builds nothing, and keeps the draft, including what was typed meanwhile; the
same draft is sent once the block is lifted. This scenario is tested by
`ComposerViewModel_SendAsync_WhileBlocked_IsRefusedWithTheReasonAndKeepsTheDraft`.

**ComposerViewModel_UnblockSending_OtherStatusAndNoBlockAndEmptyReason_AreHandled**: Lifting the block when there is
none changes nothing, an empty or missing reason is refused, and a status sentence about another action is kept when the
block is lifted. This scenario is tested by
`ComposerViewModel_UnblockSending_OtherStatusAndNoBlockAndEmptyReason_AreHandled`.

**ComposerViewModel_PushToTalk_Unavailable_ShowsReasonAndChangesNothing**: For each of the five reasons dictation cannot
work (no model, model not installed, runtime missing, no microphone, engine unavailable) the status holds the service's
message, nothing listens, and the draft is unchanged. This scenario is tested by
`ComposerViewModel_PushToTalk_Unavailable_ShowsReasonAndChangesNothing`.

**ComposerViewModel_PushToTalk_HoldAndRelease_ShowsLiveTextThenAddsOneDictatedBlock**: While the button is held the
state is `Listening`, sending is disabled, and the live words follow the recognizer; on release the states were
`Starting`, `Listening`, `Finishing`, `Idle` in order, the live text is gone, the final words are one dictated block at
the caret, the session was ended and disposed once, and one undo removes the block. This scenario is tested by
`ComposerViewModel_PushToTalk_HoldAndRelease_ShowsLiveTextThenAddsOneDictatedBlock`.

**ComposerViewModel_PushToTalk_Release_AddsAtCaretOverSelection**: The dictated word replaces a selection and one undo
restores it. This scenario is tested by `ComposerViewModel_PushToTalk_Release_AddsAtCaretOverSelection`.

**ComposerViewModel_PushToTalk_SecondPress_IsIgnored**: A press while starting and a press while listening do not ask
for listening again. This scenario is tested by `ComposerViewModel_PushToTalk_SecondPress_IsIgnored`.

**ComposerViewModel_PushToTalk_ReleaseWhileStarting_GivesUpAndAddsNothing**: A release while the start waits cancels it;
no session is used, the composer is idle, nothing is added, and no message is shown. This scenario is tested by
`ComposerViewModel_PushToTalk_ReleaseWhileStarting_GivesUpAndAddsNothing`.

**ComposerViewModel_PushToTalk_ReleaseWhileStartingButSessionArrives_EndsItAndAddsWords**: The start ignores the
cancellation and delivers a session after the release; the session is ended and disposed and its words are added. This
scenario is tested by `ComposerViewModel_PushToTalk_ReleaseWhileStartingButSessionArrives_EndsItAndAddsWords`.

**ComposerViewModel_PushToTalk_ReleaseWithoutPress_IsIgnored**: A release without a press does nothing. This scenario is
tested by `ComposerViewModel_PushToTalk_ReleaseWithoutPress_IsIgnored`.

**ComposerViewModel_PushToTalk_NothingHeard_IsNoStepAndSaysSo**: A session that heard nothing adds nothing, is no step,
and the status says that no speech was recognized. This scenario is tested by
`ComposerViewModel_PushToTalk_NothingHeard_IsNoStepAndSaysSo`.

**ComposerViewModel_PushToTalk_SessionProblem_ShownAndWordsKept**: A session that reports a problem after hearing words
adds the words and shows the problem. This scenario is tested by
`ComposerViewModel_PushToTalk_SessionProblem_ShownAndWordsKept`.

**ComposerViewModel_PushToTalk_SessionProblemWithoutWords_ShowsTheProblem**: A problem without words is shown in place
of the message that nothing was heard. This scenario is tested by
`ComposerViewModel_PushToTalk_SessionProblemWithoutWords_ShowsTheProblem`.

**ComposerViewModel_PushToTalk_StartWithoutSession_ShowsReasonOrIsSilent**: A start without a session shows the
service's reason, and a start that was ignored because dictation is already going on shows nothing. This scenario is
tested by `ComposerViewModel_PushToTalk_StartWithoutSession_ShowsReasonOrIsSilent`.

**ComposerViewModel_PushToTalk_StartThrows_ExplainsAndIsIdle**: A service that throws leaves the composer idle with a
sentence that names the failure. This scenario is tested by
`ComposerViewModel_PushToTalk_StartThrows_ExplainsAndIsIdle`.

**ComposerViewModel_PushToTalk_EndThrows_ExplainsReleasesSessionAndAddsNothing**: A session that fails when it ends is
still disposed, nothing is added, and the status explains. This scenario is tested by
`ComposerViewModel_PushToTalk_EndThrows_ExplainsReleasesSessionAndAddsNothing`.

**ComposerViewModel_PushToTalk_ProjectClosedWhileListening_GivesUpAndAddsNothing**: Closing and reopening the project
while listening ends and disposes the session, clears the live text, and a later release adds nothing. This scenario is
tested by `ComposerViewModel_PushToTalk_ProjectClosedWhileListening_GivesUpAndAddsNothing`.

**ComposerViewModel_Dispose_WhileListening_ReleasesTheSession**: Disposing the view model while listening ends and
disposes the session. This scenario is tested by `ComposerViewModel_Dispose_WhileListening_ReleasesTheSession`.

**ComposerViewModel_PushToTalk_LiveTextFromAnotherThread_IsMovedToTheInterfaceThread**: Words reported from a
thread-pool thread are posted to the context of the thread that pressed the button and change the live text only when
that work runs; words that arrive after the release are dropped. This scenario is tested by
`ComposerViewModel_PushToTalk_LiveTextFromAnotherThread_IsMovedToTheInterfaceThread`.

**ComposerViewModel_PushToTalk_NoProject_SaysSoAndDoesNotListen**: A press without a project shows the request to open
one and does not ask for listening. This scenario is tested by
`ComposerViewModel_PushToTalk_NoProject_SaysSoAndDoesNotListen`.

**ComposerViewModel_Constructor_MissingArguments_ThrowArgumentNullException**: A missing builder, registry, dictation
service, text, or content is refused. This scenario is tested by
`ComposerViewModel_Constructor_MissingArguments_ThrowArgumentNullException`.

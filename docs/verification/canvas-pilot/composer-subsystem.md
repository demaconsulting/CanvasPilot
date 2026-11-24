## Composer

### Verification Approach

The `Composer` subsystem is verified by integration tests defined in `ComposerSubsystemTests.cs`. The tests use the real
`ComposerDocument`, `ComposerEdit`, `ComposerHistory`, `AttachmentIntake`, and `AttachmentStore` together on one draft,
and, through the `ComposerViewModel`, with a real `MarkRegistry` and `PayloadBuilder` (over a composer of mark pictures
that draws nothing) and a controllable fake of the dictation service. The tests check the observable result after each
user action: the segments of the message, their order, their origins, what undo and redo give, and, for attachments, the
files in a temporary project folder. The logic of the subsystem has no dependency on the user interface; the open
project that `ComposerViewModel` follows is checked together with the composition root by the `AppShell` subsystem test
`AppShellSubsystem_ComposeApplication_ComposerFollowsTheOpenProject`. The editing view, `ComposerEditor`, is verified
together with the other units in headless user interface tests (`ComposerSubsystemUiTests.cs` in
`test/DemaConsulting.CanvasPilot.UiTests`), which show the real view over the real view model, document, history,
intake, store, and marks, with only dictation and the message builder replaced by simple stand-ins, and check after
every operation that the editing surface holds exactly the message.

### Test Environment

A temporary folder per test holds a project folder and a folder for the content that is dropped from outside the
project. No network, display, or special permission is needed.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A message built from typed text, a dictated block, a drop of several chips, and a mark holds all its parts in the
  order they were added or placed, each with its origin and kind.
- Undoing a mixed sequence of actions passes through the state before each action in reverse order, and redoing passes
  through the same states in order.
- After the message is sent the document is empty, nothing can be undone or redone, and the next draft cannot be undone
  into the sent one.
- Dropping content copies nothing into the project; undoing the drop leaves no chips and no copies, and a pasted picture
  that no chip refers to is removed.
- Sending copies the attachments from outside the project into the project's attachments folder byte for byte, refers
  to files that are in the project in place, moves pasted pictures, and leaves the draft folder empty.
- Programs, scripts, and unsafe paths that are dropped are only chips or are left out; nothing is opened or run.
- Through the view model every kind of action is one undo step over the real units, dictation adds one block as one undo
  step, and sending puts the attachments into the project, builds the payload, and empties the draft, or keeps the draft
  and names the attachment that prevents it.
- While the owner blocks sending (the copilot works), the message can be written and attached to but is not sent and
  nothing is copied into the project; after the block is lifted the whole draft is sent.
- Through the editing view, typed text, a dictated block, dropped files, and pasted text keep the editing surface equal
  to the message after every action, and undo and redo with the keyboard pass through the same states; chips are drawn
  inline in the order of the message, and deleting one with its button is one step that undo brings back in place.
- The clearing of the marks takes every mark chip out of the message and no visible mark remains (the user interface
  test of the annotation toolbar over the real panel and composer), and the empty message shows its placeholder in the
  chat panel until text is typed (the user interface test of the chat panel).
- Through the editing view, the tooltip of a mark's chip shows the mark's note and follows each change of it; a
  double-click on the chip, the Edit note item of its menu, and F2 beside it each ask once for the mark of the chip,
  without a change of the message or its undo history.

### Test Scenarios

**ComposerSubsystem_Compose_MixedParts_KeepOrderOriginAndKind**: Text is typed, a block is dictated, a file, a folder,
and a pasted image are dropped in one operation, and a mark is added in the middle of the typed text. The snapshot holds
the typed text split around the mark, the dictated text with its origin, and the three chips of their kinds, in that
order, and its length is the number of characters plus one for each chip. This scenario is tested by
`ComposerSubsystem_Compose_MixedParts_KeepOrderOriginAndKind`.

**ComposerSubsystem_UndoRedo_MixedSequence_ReturnsToEachStateInOrder**: Three characters are typed one at a time, a
block is dictated, two chips are dropped, a mark is added, and a chip is deleted, with a snapshot taken after each. Undo
returns through every earlier snapshot in reverse order to the empty draft, and redo goes forward through the same
snapshots. This scenario is tested by `ComposerSubsystem_UndoRedo_MixedSequence_ReturnsToEachStateInOrder`.

**ComposerSubsystem_Send_ClearsHistoryAndDocument**: A draft with typed text, a dictated block, and a chip has one
undone step. Sending returns the content of the draft, empties the document, and leaves nothing to undo or redo; the
next typed text is the only content, and undo reaches only it. This scenario is tested by
`ComposerSubsystem_Send_ClearsHistoryAndDocument`.

**ComposerSubsystem_Send_SnapshotIsUnaffectedByNextDraft**: A draft with one chip is sent and another message with other
chips is composed. The snapshot taken for sending still holds only its own chip. This scenario is tested by
`ComposerSubsystem_Send_SnapshotIsUnaffectedByNextDraft`.

**ComposerSubsystem_DropThenUndo_RemovesChipsAndCopiesNothing**: A file, a folder, a pasted picture, and some text are
dropped in one operation and become one step with three chips. After undo no chips remain, nothing exists in the
project's attachments folder, the pasted picture that no chip refers to is removed as a leftover, and the dropped file
is unchanged. This scenario is tested by `ComposerSubsystem_DropThenUndo_RemovesChipsAndCopiesNothing`.

**ComposerSubsystem_Send_CopiesExternalFilesIntoProjectAttachments**: A file and a folder from outside the project, a
file inside the project, and a pasted picture are dropped and sent. The file and the folder are copied into the
attachments folder, the project's own file is referred to as `flow.mmd` and not copied, the pasted picture is moved into
the attachments folder and removed from the draft folder, the draft is empty, and the dropped file is unchanged. This
scenario is tested by `ComposerSubsystem_Send_CopiesExternalFilesIntoProjectAttachments`.

**ComposerSubsystem_EditThroughViewModel_MixedActionsUndoAndRedoOneStepEach**: Through the view model over the real
store, intake, and payload builder, two typed characters, a dictated block, a drop of two files and a pasted picture, a
mark, a deleted chip, and a Backspace are undone one step at a time through every earlier snapshot to the empty draft
and redone through the same snapshots; nothing is copied into the project while drafting. This scenario is tested by
`ComposerSubsystem_EditThroughViewModel_MixedActionsUndoAndRedoOneStepEach`.

**ComposerSubsystem_DictateThroughViewModel_HoldAndRelease_AddsOneDictatedBlockAsOneStep**: The talk button is held, the
live words are shown, and on release the words are one dictated block after the typed text that one undo removes; after
redo and sending, the payload keeps the typed and the dictated parts apart. This scenario is tested by
`ComposerSubsystem_DictateThroughViewModel_HoldAndRelease_AddsOneDictatedBlockAsOneStep`.

**ComposerSubsystem_SendThroughViewModel_CopiesAttachmentsBuildsPayloadAndClearsDraft**: A message with typed text, a
mark, dictated text, a dropped file, and a pasted picture is sent with the real store and payload builder. The payload
text has `[Mark 1]` and the two attachment references in order, the attachments' paths are in the project and the files
are there byte for byte, the pasted picture was moved out of the draft folder, the payload has the picture with its one
mark, and the draft, the history, and the marks are empty afterwards. This scenario is tested by
`ComposerSubsystem_SendThroughViewModel_CopiesAttachmentsBuildsPayloadAndClearsDraft`.

**ComposerSubsystem_SendThroughViewModel_AttachmentTooLarge_KeepsDraftAndLeavesProjectUntouched**: A dropped file that
is one byte over the limit of a message stops the send with a status that names it; the draft, its history, and the
chips stay, the project has no attachments folder, and after the chip is deleted the same message is sent. This scenario
is tested by `ComposerSubsystem_SendThroughViewModel_AttachmentTooLarge_KeepsDraftAndLeavesProjectUntouched`.

**ComposerSubsystem_BlockSendingThroughViewModel_WritesMeanwhileAndSendsTheWholeDraftAfterwards**: With sending blocked
by the owner, text is typed and a file is dropped, and a send is refused; the project has no attachments folder. After
the block is lifted the same draft is sent with its text and attachment. This scenario is tested by
`ComposerSubsystem_BlockSendingThroughViewModel_WritesMeanwhileAndSendsTheWholeDraftAfterwards`.

**ComposerSubsystem_Drop_ProgramsScriptsAndUnsafePaths_AreOnlyDataAndNeverRun**: A program, a script, and three unsafe
paths (a path with `..`, a path into the device namespace, and a relative path) are dropped. The program and the script
become chips, the unsafe paths are left out with a reason, and after sending both files are in the attachments folder
with exactly the bytes they had. This scenario is tested by
`ComposerSubsystem_Drop_ProgramsScriptsAndUnsafePaths_AreOnlyDataAndNeverRun`.

**ComposerSubsystem_EditMessageInEditor_MixedActions_SurfaceAlwaysEqualsMessage**: Through the editing view, text is
typed, a block is dictated, two files are dropped at the end, clipboard text is pasted, and a character is typed, with
the state of the message noted after each action. Ctrl+Z takes the message back through every earlier state to the empty
message, and Ctrl+Y brings it forward again to the same message with two chips; after every key press the surface equals
the message. This scenario is tested by `ComposerSubsystem_EditMessageInEditor_MixedActions_SurfaceAlwaysEqualsMessage`.

**ComposerSubsystem_ShowChipsInline_DropThenDeleteAndUndo_ChipsFollowTheMessage**: A file, a folder, and a picture file
are dropped after typed text and drawn as three chips in order. A click on the delete button of the middle chip leaves
the other two; undo draws the middle chip again between them, and redo removes it again. This scenario is tested by
`ComposerSubsystem_ShowChipsInline_DropThenDeleteAndUndo_ChipsFollowTheMessage`.

**ComposerEditorView_AutomationPeer_IsANamedEditFieldWithTheTextOfTheMessage**: The user interface test of the editing
surface asks for its automation peer, which is an edit field named `Message` that tells the message with the chip named
`[Mark 1]` and the words, and that replaces the message when its value is set. It is the evidence of the exposure of the
message to assistive technology, described in the verification of the `ComposerEditor` unit. This scenario is tested by
`ComposerEditorView_AutomationPeer_IsANamedEditFieldWithTheTextOfTheMessage`.

**AnnotationToolbarView_ClearMarks_TakesTheMarkChipsOutOfTheMessage**: The user interface test of the annotation toolbar
runs the real composer through the real chat panel: two marks are drawn and two mark chips are in the message; using
the clear button of the toolbar takes both out and leaves no visible mark. It is the evidence of the removal of all
mark chips, described in the verification of the `ComposerViewModel` unit. This scenario is tested by
`AnnotationToolbarView_ClearMarks_TakesTheMarkChipsOutOfTheMessage`.

**ComposerSubsystem_ShowMarkNoteOnChip_NoteChanged_ChipTooltipFollows**: A mark with a note is added after some words
in the editing view; the note is changed in the registry and then taken away. The tooltip of the chip says
`Mark 1 on the picture: first`, then `...: second`, then `Mark 1 on the picture`, and the message stays the chip, a line
feed, and the words. This scenario is tested by `ComposerSubsystem_ShowMarkNoteOnChip_NoteChanged_ChipTooltipFollows`.

**ComposerSubsystem_RequestMarkNoteEdit_DoubleClickMenuAndF2_AskForTheMarkOfTheChip**: A mark chip stands above the
words `go`. Two quick clicks on the chip, the Edit note item of its context menu, and F2 with the caret after the chip
each raise one request for the mark of the chip; the message and what can be undone and redone are as before. This
scenario is tested by `ComposerSubsystem_RequestMarkNoteEdit_DoubleClickMenuAndF2_AskForTheMarkOfTheChip`.

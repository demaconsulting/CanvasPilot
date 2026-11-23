### ComposerHistory

#### Verification Approach

`ComposerHistory` and the factories of `ComposerEdit` are verified with unit tests in `ComposerHistoryTests.cs`, which
use a real `ComposerDocument` because the history and the document define each other's contract and the document has no
I/O. Each rule of the undo granularity has its own test: it executes the edits the way the editing control will for that
user action, and asserts the number of steps and the document after each undo and redo. Nothing is substituted.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A typed character, a deleted character, a deleted selection, typing over a selection, a dictated block, a drop or a
  paste of any size, an added mark, and a deleted chip are each exactly one step.
- Redo passes through the states the undo steps passed through, in the opposite order.
- A new edit after an undo discards the redo steps; sending, and clearing, leave nothing to undo or redo.
- The number of steps is bounded; empty edits are not recorded; an edit made for another state of the document is
  refused without a change.

#### Test Scenarios

**ComposerHistory_TypedCharacters_EachIsOneStep**: Three characters are typed one at a time; there are three steps and
each undo removes one character. This scenario is tested by `ComposerHistory_TypedCharacters_EachIsOneStep`.

**ComposerHistory_BackspaceAndDelete_EachCharacterIsOneStep**: One character is removed with Backspace and one with
Delete; two steps bring the characters back one at a time. This scenario is tested by
`ComposerHistory_BackspaceAndDelete_EachCharacterIsOneStep`.

**ComposerHistory_DeleteSelection_IsOneStep**: A selection of text and a chip is deleted; one undo brings it all back.
This scenario is tested by `ComposerHistory_DeleteSelection_IsOneStep`.

**ComposerHistory_TypeOverSelection_IsOneStep**: Text typed over a selection replaces it as one step. This scenario is
tested by `ComposerHistory_TypeOverSelection_IsOneStep`.

**ComposerHistory_DictatedBlock_IsOneStep**: A block of many words is dictated; one undo removes the block and nothing
else, and redo restores it with its dictated origin. This scenario is tested by
`ComposerHistory_DictatedBlock_IsOneStep`.

**ComposerHistory_DropOfSeveralItems_IsOneStep**: Three chips and a text are dropped together; one undo removes all of
them. This scenario is tested by `ComposerHistory_DropOfSeveralItems_IsOneStep`.

**ComposerHistory_TextPaste_IsOneStep**: A long text with line breaks is pasted as one step, with its line breaks
cleaned. This scenario is tested by `ComposerHistory_TextPaste_IsOneStep`.

**ComposerHistory_AddMark_IsOneStep**: A mark chip is added in text; undo removes it and redo brings it back. This
scenario is tested by `ComposerHistory_AddMark_IsOneStep`.

**ComposerHistory_AddLeadingChips_PutsChipsInTheRowAboveTheWordsInOneStep**: A message has a chip, a line feed, and
words. A second chip is added with the caret at the end of the words; it stands next to the first chip, no second line
feed is added, the caret moves with the words, it is one step, and undo and redo take it away and bring it back. This
scenario is tested by `ComposerHistory_AddLeadingChips_PutsChipsInTheRowAboveTheWordsInOneStep`.

**ComposerHistory_AddLeadingChips_EmptyMessage_AddsTheLineFeedAndPutsTheCaretBelowTheRow**: Two chips are added to an
empty message; the message is the two chips and a line feed, the caret is below the row, and adding no chip is an empty
edit. This scenario is tested by
`ComposerHistory_AddLeadingChips_EmptyMessage_AddsTheLineFeedAndPutsTheCaretBelowTheRow`.

**ComposerHistory_DeleteChip_IsOneStep**: A chip is deleted; one undo puts the same chip back at the same place. This
scenario is tested by `ComposerHistory_DeleteChip_IsOneStep`.

**ComposerEdit_DeleteChip_OffsetOfTextOrEnd_ThrowsArgumentException**: Deleting a chip where there is text or nothing is
refused. This scenario is tested by `ComposerEdit_DeleteChip_OffsetOfTextOrEnd_ThrowsArgumentException`.

**ComposerHistory_Redo_RestoresSameGranularity**: A draft is built from a typed character, a dictated block, a drop of
two items, and a mark; all four steps are undone and redone one at a time through the same states. This scenario is
tested by `ComposerHistory_Redo_RestoresSameGranularity`.

**ComposerHistory_UndoAndRedo_RestoreCaret**: Undo returns the caret from before the step and redo the caret after it.
This scenario is tested by `ComposerHistory_UndoAndRedo_RestoreCaret`.

**ComposerHistory_NewEditAfterUndo_ClearsRedo**: Typing after an undo makes the undone step impossible to redo. This
scenario is tested by `ComposerHistory_NewEditAfterUndo_ClearsRedo`.

**ComposerHistory_Clear_EmptiesBothStacks**: Clearing forgets both stacks and leaves the text. This scenario is tested
by `ComposerHistory_Clear_EmptiesBothStacks`.

**ComposerHistory_CompleteSend_ClearsStacksAndDocumentAndReturnsContent**: Sending returns the content, empties the
document, and leaves nothing to undo or redo. This scenario is tested by
`ComposerHistory_CompleteSend_ClearsStacksAndDocumentAndReturnsContent`.

**ComposerHistory_Limit_DropsOldestStep**: With a limit of three steps, five typed characters keep only the last three
steps that can be undone. This scenario is tested by `ComposerHistory_Limit_DropsOldestStep`.

**ComposerHistory_UndoOnEmpty_DoesNothing** and **ComposerHistory_NewHistoryOverDocumentWithContent_HasNoSteps**: A
history has nothing to undo or redo at the start whatever the document holds, so an earlier draft is never restored.
These scenarios are tested by the tests of the same names.

**ComposerHistory_EmptyEdits_AreNotRecorded**: Empty dictation, text of only control characters, an empty drop, and a
zero-length delete are not recorded. This scenario is tested by `ComposerHistory_EmptyEdits_AreNotRecorded`.

**ComposerHistory_Execute_EditOfAnotherDocumentState_ThrowsAndChangesNothing**: An edit made for an earlier state of the
document is refused and neither the document nor the history changes. This scenario is tested by
`ComposerHistory_Execute_EditOfAnotherDocumentState_ThrowsAndChangesNothing`.

**ComposerHistory_Constructor_InvalidArguments_Throws**: A missing document, a depth limit below one, and a missing edit
are refused. This scenario is tested by `ComposerHistory_Constructor_InvalidArguments_Throws`.

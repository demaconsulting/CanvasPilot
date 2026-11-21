## AvaloniaEdit Verification

This document provides the verification evidence for the AvaloniaEdit OTS software item. Requirements for this OTS item
are defined in the AvaloniaEdit OTS Software Requirements document.

### Required Functionality

AvaloniaEdit (the `Avalonia.AvaloniaEdit` package) draws a control in the line of text in place of a placeholder
character and treats it as one character for the caret and the deletion keys, reports each change of its text with an
exact offset and the removed and inserted text, keeps no undo history when it is told not to and lets the application
answer the undo and redo keys, lets the application replace its paste, copy, and cut commands and receive drops, and
lets the application refuse typed text before it enters. The message composer relies on each of these behaviors, so each
has a requirement and a test.

### Verification Approach

AvaloniaEdit has no self-validation mode. It is verified by integration tests in
`test/OtsSoftwareTests/AvaloniaEditTests.cs` that run the control on the framework's headless platform and call it with
the same kinds of controls, events, and keys that the composer uses: an editor with a generator that makes a button for
each placeholder, typing through text input events, physical key presses for Backspace, the arrow keys, the undo and
redo gestures, and the paste, copy, and cut gestures, a drop of a file with its path as text on the text view, and
handlers on the document's events. The tests are the evidence that the control behaves as the composer relies on, and
they are run again before a version upgrade is accepted. The user interface tests of the composer
(`test/DemaConsulting.CanvasPilot.UiTests`) repeat part of this evidence through the application's own views.

The headless platform has no real input method, does not use the operating system clipboard, and cannot complete a real
drag from another program. How an input method composes and commits text in the control, how a screenshot or a list of
files from the real clipboard reaches the composer, and how a real drag from the file manager or a browser is delivered
are checked by hand before a release (see the ComposerEditor verification). These checks are not automated and are not
evidence for a requirement.

### Test Scenarios

#### AvaloniaEdit_VisualLine_PlaceholderCharacter_BecomesInlineObjectElementHoldingControl

**Scenario**: An editor with a generator that makes a button for each placeholder character shows the text
`hello # world # end`, where each hash is a placeholder, and its first line is constructed.

**Expected**: The line is made of text, an inline object, text, an inline object, and text, two buttons were made, and
both are visible.

**Requirement coverage**: `CanvasPilot-OTS-AvaloniaEdit-EditTextWithInlineObjects`.

#### AvaloniaEdit_Caret_MovesOverInlineObjectAsOnePositionAndKeysDeleteItAsOneCharacter

**Scenario**: The caret is put just before an inline object, the Right arrow is pressed, and then Backspace.

**Expected**: The caret moved by one position, and Backspace removed exactly the object, leaving the text on both sides.

**Requirement coverage**: `CanvasPilot-OTS-AvaloniaEdit-EditTextWithInlineObjects`.

#### AvaloniaEdit_Document_TypingAndDeleting_ReportExactChanges

**Scenario**: Three characters are typed one at a time, Backspace is pressed, five characters are committed in one text
input, and one character is typed over a selection of two.

**Expected**: The document reports one change for each action, with the offset, the number and text removed, and the
number and text inserted exactly as they happened; typing over a selection is one change that removes and inserts.

**Requirement coverage**: `CanvasPilot-OTS-AvaloniaEdit-ReportDocumentChanges`.

#### AvaloniaEdit_Document_ProgrammaticEdit_RaisesChangedAndNestedEditsFollowTheRules

**Scenario**: Text is replaced from the program; then a handler of the change event inserts text during a typed change;
then a handler of the end of the update inserts text during another typed change.

**Expected**: The program's replacement raises the same change event as typing; the insertion inside the change event is
refused with an invalid operation exception; the insertion when the update finishes is made.

**Requirement coverage**: `CanvasPilot-OTS-AvaloniaEdit-ReportDocumentChanges`.

#### AvaloniaEdit_UndoStack_SizeLimitZeroAndRemovedCommands_LeaveApplicationInCharge

**Scenario**: The undo stack limit is set to zero, the undo and redo commands are removed from the text area, and key
bindings for Ctrl+Z, Ctrl+Y, and Ctrl+Shift+Z are added; three characters are typed and the three gestures are pressed.

**Expected**: The control has no history and the text is unchanged, and the application's three handlers are called, in
order.

**Requirement coverage**: `CanvasPilot-OTS-AvaloniaEdit-DisableBuiltInUndoRedo`.

#### AvaloniaEdit_CommandBindings_PasteCopyCut_CanBeReplaced

**Scenario**: The paste, copy, and cut bindings are replaced by handlers that count their calls; the three keys are
pressed with a selection and the control's own paste method is called.

**Expected**: Each key and the method reached the application's handler, and the text is unchanged.

**Requirement coverage**: `CanvasPilot-OTS-AvaloniaEdit-InterceptPasteAndDrop`.

#### AvaloniaEdit_TextEntering_Handled_RefusesTheTypedText

**Scenario**: A handler of the text entering event marks text with the placeholder character as handled; the placeholder
is typed, then a letter.

**Expected**: Only the letter entered.

**Requirement coverage**: `CanvasPilot-OTS-AvaloniaEdit-RefuseTypedText`.

#### AvaloniaEdit_Drop_OnTextViewWithAllowDrop_ReachesApplicationHandler

**Scenario**: Drops are allowed on the text view with handlers for drag over and drop, and a file that also carries its
path as text is dropped on it.

**Expected**: The application's handler saw the drop, and the text of the control is unchanged.

**Requirement coverage**: `CanvasPilot-OTS-AvaloniaEdit-InterceptPasteAndDrop`.

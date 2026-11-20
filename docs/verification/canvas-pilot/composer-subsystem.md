## Composer

### Verification Approach

The `Composer` subsystem is verified by integration tests defined in `ComposerSubsystemTests.cs`. The tests use the real
`ComposerDocument`, `ComposerEdit`, `ComposerHistory`, `AttachmentIntake`, and `AttachmentStore` together on one draft,
the way the chat panel will use them, and check the observable result after each user action: the segments of the
message, their order, their origins, what undo and redo give, and, for attachments, the files in a temporary project
folder. The subsystem has no dependency on the user interface or on another subsystem except `Utilities`, `Logging`, and
the name of the data folder of `Project`, so no substitute is needed.

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

**ComposerSubsystem_Drop_ProgramsScriptsAndUnsafePaths_AreOnlyDataAndNeverRun**: A program, a script, and three unsafe
paths (a path with `..`, a path into the device namespace, and a relative path) are dropped. The program and the script
become chips, the unsafe paths are left out with a reason, and after sending both files are in the attachments folder
with exactly the bytes they had. This scenario is tested by
`ComposerSubsystem_Drop_ProgramsScriptsAndUnsafePaths_AreOnlyDataAndNeverRun`.

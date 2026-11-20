## Composer

### Verification Approach

The `Composer` subsystem is verified by integration tests defined in `ComposerSubsystemTests.cs`. The tests use the real
`ComposerDocument`, `ComposerEdit`, and `ComposerHistory` together on one draft, the way the chat panel will use them,
and check the observable result after each user action: the segments of the message, their order, their origins, and
what undo and redo give. The subsystem has no dependency on the user interface, the file system, or any other subsystem,
so no substitute is needed.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A message built from typed text, a dictated block, a drop of several chips, and a mark holds all its parts in the
  order they were added or placed, each with its origin and kind.
- Undoing a mixed sequence of actions passes through the state before each action in reverse order, and redoing passes
  through the same states in order.
- After the message is sent the document is empty, nothing can be undone or redone, and the next draft cannot be undone
  into the sent one.

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

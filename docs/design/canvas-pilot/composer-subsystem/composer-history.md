### ComposerHistory

![Composer Structure](ComposerSubsystemView.svg)

#### Purpose

`ComposerHistory` applies the user's changes to the `ComposerDocument` as steps and undoes and redoes whole steps.
Together with `ComposerEdit`, whose factory methods give each user action its granularity, it is what makes "one
dictated block is one undo step" and its siblings true, in a unit that needs no window to test.

#### Data Model

- `ComposerEdit`: immutable; `Offset`, `Removed` and `Inserted` (arrays of segments, the latter in canonical form),
  `RemovedLength`, `InsertedLength`, `CaretBefore`, `CaretAfter`, and `IsEmpty`. It is made only by its factory methods,
  from the document it will change, because it reads what it removes there.
- `ComposerHistory`: the `Document`, the `DepthLimit` (default 1000), an undo list (oldest first) and a redo list.
- Invariants: every undo step has been applied to the document in order; the redo list is empty after a new edit; the
  undo list never holds more than the depth limit.

#### Key Methods

**ComposerEdit factories**: `TypeText`, `Dictate`, `Paste` (text, optionally over a selection), `Drop` (several
segments), `AddMark` (one mark chip at an offset), `AddLeadingChips` (chips at the end of the row of chips at the start
of the message, with a line feed after the row when there is none, so that the words are on the line below; the caret
goes to the start of the words when it was in or before the row and moves with the text otherwise), `Delete` (a range),
and `DeleteChip` (the chip at an offset). Each takes the offset, reads what is removed, and sets the caret before (by
default the end of the removed range) and after (the end of the inserted segments; the start of the range for
deletions). `Dictate` and `TypeText` differ only in the origin of the text, `Paste` is typed text that is one edit
however long it is, and `Drop` takes the chips and text of one operation as one edit.

**Apply / Revert**: remove the positions at the offset and insert the replacement, or the reverse. Before changing the
document they check that it holds exactly the segments the edit expects there and throw `InvalidOperationException`
otherwise, without changing it.

**Execute(edit)**: ignores an empty edit and returns `false`; otherwise applies the edit, clears the redo list, adds the
step, drops the oldest step above the limit, and returns `true`.

**Undo() / Redo()**: take the latest undo step (or redo step), revert (or apply) it, move it to the other list, and
return the caret to restore, or `null` when there is nothing to do. The step is moved only after the change succeeded.

**Clear()**: forgets every step. **CompleteSend()**: takes a snapshot, clears the document and the history, and returns
the snapshot.

#### Error Handling

An edit that does not fit the document throws `InvalidOperationException` and leaves both the document and the history
as they were. Invalid factory arguments throw `ArgumentOutOfRangeException` (range outside the document),
`ArgumentException` (`DeleteChip` where there is no chip, a null item), or `ArgumentNullException`. A depth limit below
one throws in the constructor. Nothing is logged; the unit performs no I/O and never writes a draft anywhere.

#### Dependencies

- **ComposerDocument** and the segment records - the content the history changes.
- **.NET BCL** - collections and immutable arrays.

#### Callers

- `ComposerViewModel` executes the edit of every user action and calls `Undo`, `Redo`, and `CompleteSend`; the chat
  panel uses it only through the view model.

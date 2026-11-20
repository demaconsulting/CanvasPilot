### ComposerDocument

![Composer Structure](ComposerSubsystemView.svg)

#### Purpose

`ComposerDocument` holds the message being composed as an ordered sequence of segments and is the single source of truth
about it: the editing control only shows it, and every change of the control becomes one edit of the document. It keeps
the sequence in a canonical form, counts positions the way the editing control does, cleans all text that enters it, and
gives immutable snapshots.

#### Data Model

- `ComposerSegment` (abstract record, closed set of types): `Length` is the number of positions.
  - `TextSegment(Text, Origin)`: cleaned text and its `TextOrigin` (`Typed` or `Dictated`); the constructor cleans the
    text, so no segment can hold the chip placeholder or a control character. `Length` is the number of characters.
  - `ChipSegment` (abstract, `Length` is one): `FileChip`, `FolderChip`, and `ImageChip` derive from `AttachmentChip`,
    which holds the full path of the thing and the name to show; `ImageChip` also says whether the application saved the
    picture from the clipboard (`IsPasted`). `MarkChip` holds the identity and the number of a mark and its label.
    Chips hold no content of the thing they stand for.
- `ComposerDocument`: a list of segments in canonical form (neighboring text of the same origin is one segment, text of
  different origins is separate, no text segment is empty), `Length` (positions), `FlatText` (the text with one
  placeholder character, U+FFFC, for each chip), `Segments` (a live read-only view), and `Chips`.
- `ComposerSnapshot`: an immutable array of the segments at one moment, with `Length`, `FlatText`, `Chips`, and
  `HasSameContentAs`.
- Invariants: the sequence is canonical after every operation; no text segment contains U+FFFC or a control character
  other than tab and line feed; a chip is one position.

#### Key Methods

**InsertText(offset, text, origin)**: cleans the text and inserts it. Returns the text that was inserted. Preconditions:
the offset is from zero to `Length`. Postcondition: the text is at the offset and the sequence is canonical.

**InsertChip(offset, chip)**: inserts a chip between the text before and after the offset.

**Insert(offset, segments)**: inserts several segments at once, as one drop or the undo of one deletion does.

**Delete(offset, length)**: removes the positions and returns the removed segments in order, with text that the range
cut through cut. Deleting zero positions changes nothing.

**GetSlice(offset, length)**: the segments that cover a range, without changing the document; this is how an edit learns
what it removes.

**Snapshot()**: copies the segments into a `ComposerSnapshot`. **Clear()**: removes everything.

**SanitizeText(text)** (static): removes the chip placeholder and every control character except tab and line feed, and
turns carriage return, carriage return with line feed, and the Unicode line and paragraph separators into one line feed;
returns the same string when nothing had to change.

The sequence is rebuilt from what lies before and after the changed range on every insert and delete and then put in
canonical form. A message is short, so clarity is preferred to an in-place algorithm, and the canonical form is what
makes undo exact.

#### Error Handling

Offsets and ranges outside the document throw `ArgumentOutOfRangeException` before anything changes, because they show a
mismatch between the editing control and the model that must not be hidden. Null arguments throw
`ArgumentNullException`, and a null segment in a list throws `ArgumentException`. Text that holds only unwanted
characters inserts nothing and is not an error. The class is not safe for concurrent use; the user interface thread owns
it. No I/O is performed.

#### Dependencies

- **.NET BCL** - collections, immutable arrays, `StringBuilder`, and `Path` for the name of a chip.
- No other local unit or OTS item.

#### Callers

- **ComposerEdit** and **ComposerHistory** - read slices to know what an edit removes, and apply and revert edits.
- The attachment, payload, and chat units build chips and read snapshots.

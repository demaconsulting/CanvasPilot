## Composer

![Composer Structure](ComposerSubsystemView.svg)

### Overview

The `Composer` subsystem holds the message that the user is composing for the chat. The message is not one string:
it is typed text, dictated text, and chips that stand for files, folders, and images that are attached and for marks
drawn on the shown picture, in the order the user placed them. The subsystem keeps that sequence, lets the user undo and
redo changes to it one action at a time, and forgets it when the message is sent. It has no dependency on the user
interface, so all of its rules are tested without a window.

The subsystem contains two units in this version: `ComposerDocument`, the sequence of segments, and `ComposerHistory`,
the undo and redo of the changes made to it. The units that bring in files and pictures that are dropped or pasted and
the units that show the message in an editing control are added with the later steps of the chat; their design is
described with them.

### Interfaces

**ComposerDocument**: The message as it is now.

- *Type*: In-process .NET class.
- *Role*: Provider.
- *Contract*: Holds segments, which are text of an origin (typed or dictated) or a chip (a file, a folder, an image,
  or a mark). Positions, called offsets, count one for each UTF-16 code unit of text and one for each chip. The
  document inserts text and chips at offsets, deletes ranges, cuts slices, and gives immutable snapshots. All text that
  enters it is cleaned first.
- *Constraints*: Not safe for concurrent use; the user interface thread owns it. Offsets and ranges outside the
  document throw `ArgumentOutOfRangeException`.

**ComposerEdit and ComposerHistory**: The user's changes as undo steps.

- *Type*: In-process .NET classes.
- *Role*: Provider.
- *Contract*: A `ComposerEdit` is one change, made from the document it will change by the factory method of the user
  action (`TypeText`, `Dictate`, `Paste`, `Drop`, `AddMark`, `Delete`, `DeleteChip`). `ComposerHistory.Execute` applies
  an edit and records it as one step; `Undo` and `Redo` take whole steps and return the caret offset to restore;
  `CompleteSend` hands over a snapshot, empties the document, and forgets every step.
- *Constraints*: Every change of the document that the user makes goes through the history. The history keeps at most
  1000 steps and lives in memory only.

### Design

**Segments and offsets.** A message is a list of `ComposerSegment` values: `TextSegment` (cleaned text and its
`TextOrigin`) and the chips `FileChip`, `FolderChip`, `ImageChip` (a picture file of the user, or a picture that the
application saved from the clipboard), and `MarkChip` (the identity and the number of a mark). Segments are immutable
records, so snapshots, edits, and the history share them without copying. A chip counts as one position, which is what
the editing control that is added later does too: it shows each chip as one placeholder character, and the i-th
placeholder is the i-th chip, so offsets of the control are offsets of the model.

**Canonical form.** The document keeps its segments in one form: neighboring text of the same origin is one segment,
text of different origins is separate segments, and no text segment is empty. Because the form is canonical, putting
back exactly what a change removed gives exactly the sequence from before the change, which is what undo relies on.

**Text is data.** Text that comes from a paste, a drop, or speech recognition is cleaned when a `TextSegment` is made:
the character that stands for a chip and every control character other than tab and line feed are removed, and every
kind of line break becomes one line feed. A placeholder inside text would be taken for a chip and shift every chip
after it. The cleaning only removes and replaces characters; it never interprets the text.

**Undo granularity.** The unit of undo is the `ComposerEdit`, and each user action is exactly one edit, whatever it
holds:

| User action | Factory | Steps |
| --- | --- | --- |
| Typing a character | `TypeText` | one per character |
| Backspace or Delete of a character | `Delete` | one per character |
| Deleting a selection | `Delete` | one |
| Typing over a selection | `TypeText` with a replace length | one |
| A dictated block | `Dictate` | one, however many words |
| A drop or paste of items | `Drop` | one, however many items |
| A paste of text | `Paste` | one, however long |
| Adding a mark | `AddMark` | one (the mark's chip) |
| Deleting a chip with its button | `DeleteChip` | one |

An edit stores what it removes and what it inserts at one offset, plus the caret before and after. It can be applied and
reverted any number of times, so redo applies the very same edit and has the same granularity. Before it changes the
document, an edit checks that the document holds what the edit expects at the offset and refuses otherwise, so a change
that went around the history is found at once instead of corrupting the message. An edit that changes nothing, such as
empty dictation, is not recorded.

**History.** `Execute` applies an edit first and records it second, so an edit that does not fit leaves everything as it
was. A new edit after an undo discards the steps that could be redone. At the limit of 1000 steps the oldest step is
dropped. `CompleteSend` is how a message is sent as far as the composer is concerned: it returns a snapshot of the
message and then empties the document and both stacks, so nothing of the sent message can be undone, redone, or found in
the next draft. The draft and the history are never written anywhere: the document and history units have no file or
network access, and a history that is started over a document with content starts with nothing to undo.

**Marks.** The composer knows a mark only as a `MarkChip` (identity and number). A mark is part of the message exactly
while its chip is in the sequence, so undoing the addition of a mark, or deleting its chip, removes the mark from the
message, and redoing brings it back, with one undo system for text, chips, and marks.

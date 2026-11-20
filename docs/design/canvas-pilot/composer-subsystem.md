## Composer

![Composer Structure](ComposerSubsystemView.svg)

### Overview

The `Composer` subsystem holds the message that the user is composing for the chat. The message is not one string:
it is typed text, dictated text, and chips that stand for files, folders, and images that are attached and for marks
drawn on the shown picture, in the order the user placed them. The subsystem keeps that sequence, lets the user undo and
redo changes to it one action at a time, and forgets it when the message is sent. It has no dependency on the user
interface, so all of its rules are tested without a window.

The subsystem contains four units: `ComposerDocument`, the sequence of segments, `ComposerHistory`, the undo and redo of
the changes made to it, `AttachmentIntake`, which turns dropped and pasted content into chips and text, and
`AttachmentStore`, which keeps the files that belong to attachments in the project. `AttachmentIntake` and
`AttachmentStore` work with the file system, but only to look at paths, to save pasted pictures, and to copy attachments
into the project when the message is sent; the document and the history have no file access. The units that show the
message in an editing control are added with the later steps of the chat; their design is described with them. The
subsystem depends on `Utilities` (safe path combination), `Logging` (diagnostics), and, for the name of the project's
data folder only, `Project`.

### Interfaces

**AttachmentIntake**: Dropped and pasted content as chips and text.

- *Type*: In-process .NET class.
- *Role*: Provider.
- *Contract*: `Accept` takes a `DroppedContent` (paths, PNG bytes of pasted pictures, text) and returns an
  `AttachmentIntakeResult`: the segments to insert, in order (dropped paths, pasted pictures, text), the items that were
  left out with a reason, and the items that were accepted with a warning. The caller inserts the segments as one drop
  edit.
- *Constraints*: Dropped content is data and is never opened, run, rendered, or followed. At most 200 paths and pictures
  are accepted from one operation and at most 100000 characters of text.

**IAttachmentStore and AttachmentStore**: The files of attachments in the project.

- *Type*: In-process .NET interface and class, working in one project folder.
- *Role*: Provider.
- *Contract*: `SavePastedImage` saves pasted PNG bytes in `.canvaspilot/attachments` of the project. `RemoveLeftovers`
  deletes saved pictures that no chip refers to. `StageForSend` takes the attachment chips of a message and returns a
  `StagingResult`: all attachments as `StagedAttachment` values with their project-relative paths, or a
  `StagingFailure` naming the chip and the reason, in which case nothing is left behind.
- *Constraints*: At most 500 files, 500 folders, and 200 MB are copied for one message and a pasted picture is at most
  50 MB. Files are created only in `attachments` and `.canvaspilot/attachments` of the project, never over a file that
  exists. Not safe for two simultaneous calls on one project.
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

**Attachments are data.** What is dropped or pasted comes from outside the application and is never trusted. The intake
works on the text of a path and on what the file system says about it (file or folder, size); it does not open a
file, so a program, a script, a shortcut, or a file that another program holds open becomes a chip like any other, and
nothing happens to it. Paths are refused, with a reason, when they are not complete paths, hold control characters,
point into the device namespace (`\\.\`, `\\?\`), have a part that is `.` or `..`, a part with characters a name cannot
hold (including the colon of an alternate data stream), a part that ends with a dot or a space, or a part that names a
device (`CON`, `NUL`, `COM1`, `aux.txt`, and so on); the same rules apply on every platform so that a name accepted
here is an ordinary name everywhere. A whole drive is refused, the same item is accepted once, and one operation is
limited to 200 items.

**Nothing is copied while drafting.** A dropped file or folder is only referred to by its original path. A pasted
picture has no file, so the store saves it in the project's private data folder (`.canvaspilot/attachments`) as
`paste-{yyyyMMdd-HHmmss}-{n}.png` after checking that it is a PNG of at most 50 MB; the bytes are never decoded. Undoing
a drop removes chips and copies nothing. Saved pictures that no chip refers to are removed with `RemoveLeftovers`, which
the chat is to call after a message is sent, when a project is closed, and when a project is opened; it deletes only
files with the paste name pattern in that one folder.

**Copy at send.** When the message is sent the chat gives the attachment chips of the snapshot to `StageForSend`. The
call works in three passes. First every chip is planned: its path is checked again, it must exist and be what the chip
says, a folder that contains the project is refused (it would be copied into itself), the private data folder is refused
except for pictures the store saved, a file or folder that is inside the project is referred to in place, and everything
else is measured against the limits for the whole message (500 files, 500 folders, 200 MB), counting only what is
copied; a dropped folder is listed with an early stop, so a huge folder is never enumerated to its end. Symbolic links
and junctions are skipped and counted, never followed. Second, if everything fits, the copies are made: each file is
created exclusively (never over an existing file), the bytes are counted again as they are copied so that a file that
grew cannot pass the limit, names are made safe (characters a name cannot hold replaced, device names prefixed, trailing
dots and spaces removed, length limited to 100 characters with the extension kept) and unique (`photo.png`,
`photo (2).png`), and every path is built with `PathHelpers.SafePathCombine` so that it cannot leave the folder it is
built for. A failure removes everything the call created, newest first, and the failure names the chip and the reason.
Third, pasted pictures that were copied are deleted from the draft folder. The same thing attached twice is copied once.
The result gives each chip its project-relative path with forward slashes, its size, its number of files, whether it was
copied, and how many links were skipped; the originals are never changed or deleted.

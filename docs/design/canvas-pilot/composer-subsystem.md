## Composer

![Composer Structure](ComposerSubsystemView.svg)

### Overview

The `Composer` subsystem holds the message that the user is composing for the chat. The message is not one string:
it is typed text, dictated text, and chips that stand for files, folders, and images that are attached and for marks
drawn on the shown picture, in the order the user placed them. The subsystem keeps that sequence, lets the user undo and
redo changes to it one action at a time, and forgets it when the message is sent. Its rules have no dependency on the
user interface, so they are all tested without a window; only the editing view and its small parts do.

The subsystem contains six units: `ComposerDocument`, the sequence of segments, `ComposerHistory`, the undo and redo of
the changes made to it, `AttachmentIntake`, which turns dropped and pasted content into chips and text,
`AttachmentStore`, which keeps the files that belong to attachments in the project, `ComposerViewModel`, which
orchestrates all of them with the marks, dictation, and sending, without a window, and `ComposerEditor`, the view that
shows the message in one editing surface with its chips inline and reports what the user does to the view model.
`AttachmentIntake` and `AttachmentStore` work with the file system, but only to look at paths, to save pasted pictures,
and to copy attachments into the project when the message is sent; the document and the history have no file access.
The subsystem depends on `Utilities` (safe path combination), `Logging` (diagnostics), `Annotation` (the registry of
marks), `Speech` (dictation), and, for the name of the project's data folder only, `Project`. It does not depend on
`Payload`: it owns the `IMessageBuilder` interface through which it has a sent message built, and `Payload` implements
it.

### Interfaces

**ComposerViewModel**: The message as the user works on it.

- *Type*: In-process .NET class, with the interfaces `IUndoRedoTarget` and `IPushToTalkTarget`, which it implements,
  `IMessageBuilder`, which it uses and the payload builder implements, and `IAttachmentPicker`, which the main window
  implements and the chat panel calls.
- *Role*: Provider to the editing control, the menu, and the talk key; consumer of the intake, the store, the marks, and
  dictation.
- *Contract*: Methods that report the user's actions (`ApplyTextChange`, `InsertTyped`, `Delete`, `DeleteBackward`,
  `DeleteForward`, `AddDictatedBlock`, `HandleDrop`, `HandlePaste`, `AddMark`, `DeleteChip`), each exactly one undo
  step, and `DeleteMarkChips`, which deletes the chips of all marks one step each; `Undo`, `Redo`, and their commands;
  `SetProject` and `ClearDraft`; `SendAsync`, which puts the attachments into
  the project, has the message built, empties the draft, and raises `MessageSent` with an immutable `SentMessage`, or
  keeps the draft and says why; `BlockSending(reason)` and `UnblockSending()`, with which the owner (the chat panel
  while the AI copilot works) makes the message impossible to send while editing stays possible, with `IsSendBlocked`
  and `SendBlockedReason`; `BeginPushToTalkAsync` and `EndPushToTalkAsync`; and observable state with a `Changed` event
  once for each action, including `ShowsPlaceholder`, which says that the editing control shows its hint
  (`PlaceholderText`). `IAttachmentPicker.PickAttachmentsAsync(initialFolder)` is the seam by which the chat panel asks
  the window for the full paths of files to attach; the panel gives them to `HandleDrop`. For the notes of marks,
  `GetMarkNote` gives the current note of a mark, `MarkNoteChanged` tells that a note changed, and
  `RequestMarkNoteEdit` and `RequestMarkNoteEditAtCaret` raise `MarkNoteEditRequested` with the mark of a chip in the
  message while it can be edited, without changing the message.
- *Constraints*: Owned by the interface thread, not safe for concurrent use. The message can be edited only while a
  project is open and no message is being sent.

**ComposerEditor**: The editing view of the message.

- *Type*: In-process Avalonia user control (`ComposerEditorView`) with its parts: `ChipView`, `ChipElementGenerator`,
  `DictatedTextBackgroundRenderer`, `DataTransferReader`, and `IChipThumbnailProvider` with
  `ImageChipThumbnailProvider`.
- *Role*: Consumer of `ComposerViewModel`, which it takes as its data context; provider to the chat panel and the Edit
  menu.
- *Contract*: Shows exactly the message of the view model in one editing surface, with each chip inline as one position;
  reports every change of the user as one view model action; writes every other change of the view model into the
  surface; handles Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, paste, copy, cut, and drops as data; gives the view model as the
  `IUndoRedoTarget` of the focused surface; takes the pictures of chips from an `IChipThumbnailProvider`; shows the note
  of a mark in the tooltip of its chip, gives the same text to a screen reader as the chip's help text, and reads a
  mark with a note as `[Mark N: note]` in the text of the message that assistive technology reads; and asks the view
  model to change the note of a mark on a double-click of its chip, the Edit note item of the chip's context menu
  (enabled only while the message can be edited), or F2 with the caret beside the chip.
- *Constraints*: Owned by the interface thread. The surface is read-only while the message cannot be edited. It uses
  the AvaloniaEdit control, and holds no state of the message.

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
the editing control does too: it shows each chip as one placeholder character, and the i-th
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

**The view model.** `ComposerViewModel` is the one place where the user's actions become edits. The editing control
reports what the user did, and the view model makes the matching `ComposerEdit`, executes it through the history, moves
the caret, and tells the control once that the message changed, so the granularity table above holds for the whole
subsystem and is tested without a window. It sends the message in two phases so that nothing is lost: first the
attachments are put into the project and the message is built, both without changing the draft, which cannot be edited
meanwhile; only when both worked is the draft emptied. Dictation is a small state machine (`Idle`, `Starting`,
`Listening`, `Finishing`) over the dictation service, which handles a release before listening has started and a
project change in the middle of a dictation without adding anything or leaving the microphone in use. The send is built
through `IMessageBuilder`, an interface that the composer owns and the payload builder implements, so the composer
needs no reference to the payload subsystem, which depends on the composer.

**The editing view.** `ComposerEditorView` shows the message in the AvaloniaEdit control and is a mirror, never the
owner: the text of the control is `ComposerDocument.FlatText`, with one placeholder character for each chip, and the
n-th placeholder is the n-th chip, so offsets are the same on both sides and a chip needs no identity in the text. The
control's own undo is switched off so that the message's history is the only one. Each update of the control is handed
to the view model as one action, each change of the view model that did not come from the control is written into the
control under a flag, and after each change the control is made equal to the message, so that text that the message
cleaned, or a change it refused, never shows. Paste, copy, cut, and drop are taken from the control and handled as data:
`DataTransferReader` reads a drop or the clipboard into `DroppedContent` (files before a picture before text), copy puts
readable text on the clipboard instead of the placeholder characters, and typed text with a placeholder or a control
character is refused before it enters. Chips are drawn inline by a generator that makes a small control for each
placeholder, with the picture that an `IChipThumbnailProvider` gives it, and dictated words get a background. While the
message is empty a hint (the placeholder) shows over the surface and does not take the pointer. A chip is a pill of the
shared look; the chip of a mark is tinted and outlined in the mark color, and the delete button of each chip is named
for the chip it removes.

**The seam for files.** A file dialog needs the main window as its owner, which the subsystem does not know. The
subsystem therefore owns the interface `IAttachmentPicker`, the main window implements it through its dialog service,
and the chat panel, which has the attach button, calls it; the files it returns enter the message through `HandleDrop`,
so a chosen file and a dropped file follow the same rules of the intake.

**Marks.** The composer knows a mark only as a `MarkChip` (identity and number), except that the view model holds the
registry of marks, so that it can check the mark it adds, pass the marks that are part of the message to the builder,
and clear them when the message is sent. A mark is part of the message exactly
while its chip is in the sequence, so undoing the addition of a mark, or deleting its chip, removes the mark from the
message, and redoing brings it back, with one undo system for text, chips, and marks. `DeleteMarkChips` deletes every
mark chip, one `DeleteChip` for each, so that a request to clear the marks of the picture can be taken back in steps,
like deleting the chips one by one. The note of a mark lives in the registry, not in the message: the editor shows it
in the tooltip of the chip through `GetMarkNote`, gives the same text to a screen reader as the chip's help text and in
the text of the message, and builds the chips again on `MarkNoteChanged`. The user asks to
change a note from the chip (double-click, the Edit note item of its context menu, or F2 with the caret beside it); the
view model passes the request on through `MarkNoteEditRequested` only while the message can be edited (the Edit note
item is disabled otherwise), and the chat panel has the annotation layer open the
note. The change of a note is therefore not a step of the message's undo history.

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
`ComposerViewModel` calls after a message is sent, when a draft is cleared, when a project is closed, and when a project
is opened; it deletes only
files with the paste name pattern in that one folder.

**Copy at send.** When the message is sent `ComposerViewModel` gives the attachment chips of the snapshot to
`StageForSend`. The call works in three passes. First every chip is planned: its path is checked again, it must exist
and be what the chip says, a folder that contains the project is refused (it would be copied into itself), the private
data folder is refused except for pictures the store saved, a file or folder that is inside the project is referred to
in place, and everything else is measured against the limits for the whole message (500 files, 500 folders, 200 MB),
counting only what is copied; a dropped folder is listed with an early stop, so a huge folder is never enumerated to its
end. Symbolic links and junctions are skipped and counted, never followed. Second, if everything fits, the copies are
made: each file is created exclusively (never over an existing file), the bytes are counted again as they are copied so
that a file that grew cannot pass the limit, names are made safe (characters a name cannot hold replaced, device names
prefixed, trailing dots and spaces removed, length limited to 100 characters with the extension kept) and unique
(`photo.png`, `photo (2).png`), and every path is built with `PathHelpers.SafePathCombine` so that it cannot leave the
folder it is built for. A failure removes everything the call created, newest first, and the failure names the chip and
the reason. Third, pasted pictures that were copied are deleted from the draft folder. The same thing attached twice is
copied once. The result gives each chip its project-relative path with forward slashes, its size, its number of files,
whether it was copied, and how many links were skipped; the originals are never changed or deleted.

### Dependencies

The dependencies of the units come from the SysML2 model (`sysml2tools query dependencies`). The hub suppliers
`RollingFileLogger`, `PathHelpers`, and `ProjectService`, which many units use, are left out of the view.

- `ComposerHistory` depends on `ComposerDocument`.
- `AttachmentIntake` depends on `ComposerDocument` and `AttachmentStore`.
- `ComposerViewModel` depends on `ComposerDocument`, `ComposerHistory`, `AttachmentIntake`, `AttachmentStore`,
  `MarkRegistry` (`Annotation`), and `DictationService` (`Speech`).
- `ComposerEditor` depends on `ComposerViewModel`, `AttachmentIntake`, and AvaloniaEdit (OTS).
- `ComposerDocument` and `AttachmentStore` depend on no other unit that the view shows.

![Composer dependencies](ComposerSubsystemDependencyView.svg)

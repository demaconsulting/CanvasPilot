### AttachmentIntake

![Composer Structure](ComposerSubsystemView.svg)

#### Purpose

`AttachmentIntake` turns what a drop or a paste brought, paths, pasted pictures, and text, into the chips and text that
go into the message. It decides what each dropped path is, saves pasted pictures through the attachment store, cleans
text, and tells what it left out and why. It treats everything as data: nothing that is dropped is opened, run,
rendered, or followed, and nothing is copied; the copy happens when the message is sent.

#### Data Model

- `DroppedContent`: `Paths` (full paths in the order the operating system gave them), `Images` (PNG bytes of pasted
  pictures), and `Text`. It is a plain, view-neutral record of what the view read from the drag or the clipboard.
- `AttachmentIntakeResult`: `Segments` (the chips and text for the message, in order: dropped paths, pasted pictures,
  text), `Skipped` (items that were left out, each an `IntakeNote` with the item and the reason), and `Flagged` (items
  that were accepted with a warning).
- Constants: at most 200 paths and pictures per operation, at most 100000 characters of text, and the picture extensions
  `.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, `.webp`, `.svg`, `.tif`, and `.tiff`.
- The unit holds no state besides its store and logger.

#### Key Methods

**Accept(content)**: Works through the content in a fixed order. For each path: refuse it when the text of the path is
not safe (see `AttachmentNames.FindProblemWithPath`), refuse a whole drive, accept an item that was seen already only
once, make a folder chip when the path is a folder, leave it out when it does not exist, flag a file that is larger than
200 MB (the most a message can carry), and make a picture chip when the extension is a picture extension, otherwise a
file chip. The only calls on the file system are `Directory.Exists`, `File.Exists`, and the length of a file, none of
which opens it. After 200 accepted items the rest is reported as left out. Each pasted picture is given to
`IAttachmentStore` and becomes a picture chip marked as pasted, or is left out with the reason the store gave. Text is
cleaned with `ComposerDocument.SanitizeText`, shortened to the limit with a warning, and made a typed text segment
unless nothing remains.

#### Error Handling

Nothing is thrown for the content itself: every problem with an item is an `IntakeNote` in `Skipped`. File system
exceptions (`IOException`, `UnauthorizedAccessException`, `NotSupportedException`, `ArgumentException`) while looking at
a path or saving a picture are caught per item and reported as the reason. A null `content` or `store` throws
`ArgumentNullException`. Skipped items are written to the log at information level; failing to log is ignored.

#### Dependencies

- **IAttachmentStore** - saves pasted pictures; the unit is verified with a substitute for it.
- **AttachmentStore** (the name rules `AttachmentNames` of its source files) - the safety rules for paths, and the limit
  of a message that a file is flagged against.
- **ComposerDocument** and the segment records - the chips and text segments it makes, and the text cleaning.
- **RollingFileLogger** - diagnostics.

#### Callers

- The chat panel and the editing control of later steps call `Accept` with what a drop or a paste brought and insert the
  segments as one `ComposerEdit.Drop`; they show the notes to the user.

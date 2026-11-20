### AttachmentStore

![Composer Structure](ComposerSubsystemView.svg)

#### Purpose

`AttachmentStore` keeps the files that belong to attachments in one project folder. While the message is a draft it
saves pictures that were pasted from the clipboard in the project's private data folder and removes the ones nobody
refers to; when the message is sent it puts the things the message's chips stand for into the project, so that the
message can refer to them there. It is the only unit of the subsystem that creates files, and it does so only inside the
project.

#### Data Model

- `AttachmentStore(projectFolder, logger, timeProvider)`: holds the full project folder, the project's private data
  folder (`.canvaspilot`, named by `ProjectDocument.DataFolderName`), the draft folder for pasted pictures
  (`.canvaspilot/attachments`), and the folder for the attachments of sent messages (`attachments`). No other state.
- Limits: `MaximumFiles` 500, `MaximumFolders` 500, `MaximumBytes` 200 MB per message; `MaximumPastedImageBytes` 50 MB
  per picture.
- `StagedAttachment`: the chip, the original path, the project-relative path (forward slashes), the kind, the name in
  the project, the size in bytes, the number of files, whether it was copied, and the number of links that were skipped.
- `StagingResult`: success with the attachments in chip order, or a `StagingFailure` with the chip and a sentence for
  the user; a failure has no attachments.
- `AttachmentNames` (internal, in the source files of this unit): the rules for paths and names that come from outside:
  `FindProblemWithPath`, `SanitizeName`, `UniqueName`, and `IsReservedDeviceName`.
- Internal plan records: for each distinct source a mode (in place, copy, or move of a pasted picture), its size and
  file count, and for a folder its listing with the safe destination name of every entry.

#### Key Methods

**SavePastedImage(png)**: refuses empty data, data over 50 MB, and data that does not start with the PNG signature, with
`ArgumentException`, before anything is written. It creates the draft folder, and then a file `paste-{time}-{n}.png`
exclusively (`FileMode.CreateNew`), trying the next number when the name is taken; a write that fails removes the file.

**RemoveLeftovers(keep)**: deletes the files in the draft folder whose names match the paste pattern and whose full
paths are not in `keep`; it never deletes anything else.

**StageForSend(chips)**: works in three passes. *Plan*: for each distinct path, the path text is checked again; the
thing must exist and be what the chip says; a folder that holds the project is refused; the private data folder is
refused except for pasted pictures that sit directly in the draft folder under a paste name; a claim that a picture was
pasted is refused for any other file; things inside the project are referred to in place, everything else is copied, and
what is copied is measured against the limits (a folder is listed iteratively, in name order, symbolic links and
junctions skipped and counted, names made safe and unique per folder, with an early stop when a limit is passed).
*Copy*: the attachments folder is created if needed, and each thing is copied into a new, exclusively created file or
folder with a unique safe name, counting bytes and files again while copying. *Finish*: the draft files of pasted
pictures that were copied are deleted and the result is built, one staged attachment for each chip. A failure in the
plan returns the failure with nothing created; a failure while copying first deletes every file and folder the call
created, newest first, and the attachments folder if the call created it.

#### Error Handling

The content problems of `StageForSend`, such as an unsafe path, a missing file, a limit passed, or a file that cannot be
read, are results, not exceptions: a `StagingResult` with the chip and the reason, and an entry in the log at warning
level. Only programming errors throw (`ArgumentNullException`, a null chip, a project folder that is not a complete
path). `SavePastedImage` throws `ArgumentException` for refused data and lets `IOException` and
`UnauthorizedAccessException` of the file system pass to the caller, which reports them. `RemoveLeftovers` and the
rollback ignore, and log, files that cannot be deleted. Failing to log is ignored.

#### Dependencies

- **PathHelpers** - every path under a folder is built with `SafePathCombine`.
- **RollingFileLogger** - diagnostics.
- **ProjectService** - only the name of the data folder, `ProjectDocument.DataFolderName`.
- **.NET BCL** - file system types and `TimeProvider`.

#### Callers

- **AttachmentIntake** - saves pasted pictures through `IAttachmentStore`.
- **ComposerViewModel** calls `StageForSend` with the attachment chips of the message when it is sent, and
  `RemoveLeftovers` after sending, when a draft is cleared, when a project is closed, and when a project is opened.

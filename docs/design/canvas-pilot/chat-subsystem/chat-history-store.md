### ChatHistoryStore

![Chat History Dependencies](ChatSubsystemHistoryView.svg)

#### Purpose

`ChatHistoryStore` keeps the words of the conversation of each project on the computer of the user, so that the
conversation is still there when the project is opened again, and keeps them outside the project: a project folder is
copied, shared, and put under source control, and the words of a conversation are the user's own. It names the folder of
a project by a hash of the path of its project file, saves and reads rows of words only in a small JSON file, keeps the
newest rows within limits, writes whole files or none, sets aside a file that it cannot use, deletes the history of a
project on request, and refuses a configuration that would put the history inside the project folder. It has no window
and no knowledge of the transcript: the chat panel gives it rows and takes rows.

#### Data Model

- `ChatHistoryStore`: `sealed class`, implements `IChatHistoryStore` (`Load`, `Save`, `Clear`). The constructor takes
  the history root, an optional `RollingFileLogger`, and an optional `TimeProvider`. `DefaultHistoryRoot` is
  `CanvasPilot\history` below `Environment.SpecialFolder.LocalApplicationData` (the user profile when there is none, as
  the log does). `FolderOf(project)` and `HistoryFilePath(project)` give the places. The constants `CurrentVersion` (1),
  `MaxRows` (200), `MaxBytes` (2 MB), and `MaxTextCharacters` (100,000) are the limits.
- `ChatHistoryRow`: the row as it is saved, a `sealed record` with `Kind` (`User`, `Assistant`, or `Tool`), `Utc`,
  `Text`, and the members that belong to one kind: `Attachments` (project-relative paths) and `Marks`
  (`ChatHistoryMark`: number and note) for a user row, `Reply` (`Completed`, `Stopped`, or `Failed`) for an assistant
  row, and `Tool` and `Succeeded` for a tool row. A member that does not belong to the row is `null` and is not written.
  There is nothing in it that could hold a picture, a payload, or a tool result.
- `ProjectHistoryKey`: the key rule (see below).
- `ChatHistoryResult`: `Succeeded` and a sentence `Message`; the answer of `Save` and `Clear`.
- The files. `history.json`: `{ "version": 1, "rows": [ ... ] }`, each row an object with camel-case names and enums as
  words, written compactly in UTF-8 without a byte order mark. `project.json`: `{ "projectName", "projectFile",
  "lastUsedUtc" }`, written with each save, for a person or a later tool that looks at the folders and sees only hashes.
  `history.json.bad`: a file that could not be used, set aside.

#### Key Methods

**The key.** `ProjectHistoryKey.Compute(projectFilePath)` takes `Path.GetFullPath` of the path, makes the separators
those of the platform, drops a trailing separator, lowers the path with the invariant culture when the file system
ignores case (Windows and macOS), encodes it as UTF-8, hashes it with SHA-256, and gives the first 16 bytes as 32
lowercase hexadecimal characters. Every spelling of one path gives one key; a project that is moved or renamed has
another key and therefore **a fresh history**: the old folder stays behind and its `project.json` shows to which project
it belonged. This is a known and accepted limit, which the user guide states under "Where CanvasPilot Keeps Its Files".
The history is per computer and is not shared through source control, which is intended.

**Saving.** `Save` first refuses when the root is the project folder or inside it (see Error Handling). Otherwise it
keeps the newest rows within the limits (`Cap`: at most 200 rows, a text cut at 100,000 characters with an ellipsis, and
the oldest rows dropped until the sum of the rows, counted as they are written, and a small allowance for the file
around them are at most 2 MB), creates the folder of the project, removes the temporary files that an earlier crash left
(`*.tmp`), and writes `history.json` and then `project.json`, each to a temporary file in the same folder, which is
moved over the old file in one step. A reader therefore sees the old file or the new one and never half of one. Nothing
is created at open: the folder appears with the first save.

**Reading.** `Load` returns nothing when the file is not there and creates nothing. It reads the file (refusing, without
parsing, one that is larger than four times the limit), checks that the root is an object with a numeric `version` of at
least 1, treats a version above 1 as the file of a newer application, reads the rows, drops the rows that cannot be used
(a tool row without a name, a user or assistant row without words, a `null`), and applies the limits again, in case the
file was edited by hand.

**Files that cannot be used.** A file that is not JSON, has no version or no rows, has a row of an unknown kind, has a
newer version, or is far too big is renamed to `history.json.bad` in its folder (replacing an earlier `.bad` file), and
the history is empty. The rename keeps what the user may want and what a newer application may read, and keeps the file
from being read again. The log gets the path of the file and the reason, never a word of a row.

**Clearing.** `Clear` deletes the folder of the project with everything in it (both files, a `.bad` file, and
temporary files), which is only ever a folder whose name is a key below the root, and treats a project that has no
history as cleared.

**Everything else that the copilot features write.** The audit of this increment found that nothing of the copilot is
written into a project folder besides what the user or the copilot is asked to create: the history is here; the Copilot
runtime data folder is below the local data of the user (the `AgentService` unit); the digest of the restored history is
in memory only; the log gets metadata only; nothing is saved of a rendered picture or of a payload. What a project
folder does hold of the application is the project file, the hidden data folder `.canvaspilot` (empty except for the
transient copies of pictures that were pasted into a message and not sent yet, which are deleted when the message is
sent, discarded, or the project is closed), the copies of the attachments of sent messages under `attachments`, the
style kits under `styles`, and the files that the copilot creates or replaces at the request of the user. The test of
the Chat subsystem checks this after a full scripted turn, a save, a restore, and a clear.

#### Error Handling

Programming errors throw: null arguments and a blank root or path (`ArgumentNullException`, `ArgumentException`).
Everything that depends on the disk is a result or an empty list: a folder that cannot be made, a file that cannot be
written or deleted, or a disk that is full gives a `ChatHistoryResult` with a sentence and a log entry of the type of
the exception, and the temporary file is removed; a file that cannot be read is empty and logged. **A root that is the
project folder or lies inside it is refused for `Load`, `Save`, and `Clear`:** nothing is read, written, or deleted, the
result says that the history would lie inside the project folder, and the log says so. The check compares the full paths
without regard to case, with a separator after the project folder, so a sibling whose name only starts like the
project's is not inside it; it does not follow links.

#### Dependencies

- **ProjectService** (the project information types `ProjectInfo` and `ProjectDocument.FileExtension`): the name, the
  folder, and the file of the project.
- **RollingFileLogger**: facts about files that cannot be used and writes that fail.
- **PathHelpers**: the safe combination of the folder of a project history with the names of its files.
- **System.Text.Json** and **System.Security.Cryptography** (part of the .NET runtime): the files and the key.

#### Callers

- `ChatPanelViewModel` loads when a project is opened, saves when a turn has ended, and clears on the user's request.
- The composition root (`ApplicationComposition`) makes the store over `DefaultHistoryRoot`, gives it to
  the panel, and passes the same root to the `AgentService`, which refuses to run when that root lies inside the
  project.

### ProjectService

![Project Structure](ProjectSubsystemView.svg)

#### Purpose

`ProjectService` creates, opens, saves, and closes projects. It owns the project file format, the rules for
project names, and the open project. Ordinary user problems never throw: they come back as a failed result
with a message and are logged. The `ProjectDocument`, `ProjectInfo`, and `ProjectResult` records and the
`IProjectService` interface belong to this unit.

#### Data Model

**ProjectDocument**: `record` — the content of the project file: `FormatVersion` (`int`, currently 1), `Name`
(`string`), `CreatedUtc` (`DateTimeOffset`), `ModifiedUtc` (`DateTimeOffset?`, absent until the first save),
`ActiveStyleKit` (`string?`, the id of the style kit that the project uses, written as `activeStyleKit` and absent when
the project has no style or the file is from before style kits), and `ExtensionData` (properties this version does not
know, kept so a save does not discard them). The
constants `FileExtension` (`.canvaspilot`) and `DataFolderName` (`.canvaspilot`) name the project file
extension and the reserved hidden folder.

**File format**: a camel-case JSON object, for example
`{"formatVersion":1,"name":"Sky Chart","createdUtc":"2026-03-04T05:06:07+00:00"}`. Reading is
case-insensitive, skips comments, allows trailing commas, and ignores unknown properties.

**ProjectInfo**: `record` — `Name`, `FolderPath`, `FilePath`, and `Document` of an open project.

**ProjectResult**: `record` — `Project` (`ProjectInfo?`), `Error` (`string`, empty on success), and
`Succeeded`; created with `Success` and `Failure`.

**Current**: `ProjectInfo?` — the open project, or `null`.

**Name rules** (`ProjectNameRules`): a name is trimmed and must not be empty, longer than 100 characters,
start or end with a period, contain a character that is invalid in a file name on Windows or any
supported platform (including `< > : " / \ | ? *` and control characters), or be a Windows reserved device
name such as `CON` or `LPT1` (with or without an extension). The same rules apply to file names in the
project explorer.

#### Key Methods

**ValidateProjectName / ValidateParentFolder / ValidateNewProject**: Return a user-readable problem or `null`.
The parent folder must be a rooted path to an existing folder. `ValidateNewProject` adds that the project folder
`{parent}/{name}` is not an existing file and is not an existing folder with content; an existing empty
folder is allowed.

**CreateAsync**: Creates a project.

- *Parameters*: `string parentFolder`, `string name`, `CancellationToken cancellationToken`.
- *Returns*: `Task<ProjectResult>` — the new project, or the reason it could not be created.
- *Preconditions*: none; invalid input is reported as a failed result.
- *Postconditions*: On success `{parent}/{name}/` holds `{name}.canvaspilot` and an empty hidden
  `.canvaspilot` folder, the project is open, and `CurrentChanged` has been raised. On failure the file system
  is as it was.

Algorithm: validate with `ValidateNewProject`; resolve the folder with `PathHelpers.SafePathCombine`; create
the folder, then the data folder (marked hidden where the platform supports it), then write the project file
atomically. If anything fails or the operation is cancelled, remove what was created: the whole folder when it
did not exist before, otherwise only the project file and data folder.

**OpenAsync**: Opens a project file.

- *Parameters*: `string projectFile`, `CancellationToken cancellationToken`.
- *Returns*: `Task<ProjectResult>`.
- *Postconditions*: On success the project is open and `CurrentChanged` has been raised. On failure the
  open project is unchanged.

Algorithm: resolve the full path; require the `.canvaspilot` extension and an existing file; read the text;
parse it as JSON with comments allowed; require an object with a numeric `formatVersion` of at least 1; for a
version above the supported version, fail with a message that the file was created by a newer version of
CanvasPilot and that the user should update; deserialize the document; use the file name as the name when the
file has none.

**SaveAsync**: Saves the open project. Fails with a message when no project is open. Otherwise writes the
document with `ModifiedUtc` set to the current time (the created time, name, and unknown properties are kept)
to a uniquely named temporary file in the project folder and moves it over the project file. On failure the
temporary file is deleted and the failure is returned and logged.

**SetActiveStyleKitAsync**: Sets the style kit of the open project. The id is validated first, with a plain check of the
rule that style kits follow (1 to 48 lower-case letters, digits, and dashes that start with a letter or a digit; `null`
means no style), because the project subsystem does not depend on the style subsystem and the id is only text to it, but
the id is used by other units to build the name of a folder, so it must never be something else. Fails with a message
when the id is invalid or no project is open; returns the open project without writing when the project already has that
id. Otherwise it writes the document with `ActiveStyleKit` changed through the same private step as `SaveAsync`
(modified time set, temporary file, move over the project file), replaces the open project with the saved one when it is
still the open project, and raises `CurrentChanged`. Raising the event is safe for the units that follow it: the
workspace opens the explorer only when the folder is not the one that is open, and the style selection reads the id
again.

**Close**: Clears the open project and raises `CurrentChanged` once; does nothing when no project is open.

#### Error Handling

`IOException`, `UnauthorizedAccessException`, `NotSupportedException`, `ArgumentException` (from path
resolution), and `JsonException` are converted to failed results with messages and logged: user problems at
warning level and input or output failures at error level. Cancellation propagates as
`OperationCanceledException` after the partial creation is removed. A failure to write a log entry is
ignored so that a logging problem never blocks project access. The clock is `TimeProvider`, so tests can fix
the time.

#### Dependencies

- **PathHelpers** — resolves the project folder, project file, data folder, and temporary file paths.
- **RollingFileLogger** — optional; records refused and failed operations.
- **System.Text.Json** and **System.IO** — serialization and file system primitives.

#### Callers

- `NewProjectDialog` — validates the entries.
- `ProjectExplorer` — uses the file extension and data folder constants and the name rules.
- The application shell — creates, opens, saves, and closes projects and shows failure messages.

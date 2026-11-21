### RollingFileLogger

![Logging Structure](LoggingSubsystemView.svg)

#### Purpose

`RollingFileLogger` writes application events and failure details to a bounded set of local files so
users can attach recent logs to bug reports without unbounded disk growth. No telemetry or remote
transmission is performed.

#### Data Model

**LogDirectory**: `string` — folder containing the active and archived log files. Created at
construction when missing.

**BaseFileName**: `string` — stable prefix used to name the active file (`{BaseFileName}.log`) and each
archive (`{BaseFileName}-{UTC timestamp}.log`). Defaults to `canvaspilot`.

**MaximumFileSizeBytes**: `long` — size at which the active log is rotated. Always positive. Defaults to
1,000,000 bytes.

**RetainedFileCount**: `int` — number of archive files preserved before older files are deleted. Always
positive. Defaults to 5.

**ActiveFilePath**: `string` — full path of the active append-target file, resolved with
`PathHelpers.SafePathCombine`.

**DefaultLogDirectory**: `string` (static) — `%LOCALAPPDATA%\CanvasPilot\logs` on Windows, using the
per-user local application data folder reported by the platform. When the platform reports no local
application data folder, the user profile folder is used, and the current directory when no user
profile is reported either.

**LogLevel**: `enum` — `Info`, `Warning`, or `Error`.

#### Key Methods

**Log**: Appends one event to the active log file.

- *Parameters*: `LogLevel level` — severity; `string message` — event text; `Exception? exception` —
  optional failure context.
- *Returns*: `void` — the event is written or an error is propagated.
- *Preconditions*: `message` is not null or whitespace.
- *Postconditions*: The event is appended to the active file as
  `{ISO-8601 UTC timestamp} [{level}] {message}`, followed by the exception text on further lines when
  an exception was supplied, or a failure is reported to the caller.

Algorithm: validate `message`; take the lock; call `RotateIfNeeded`; format the entry; append it with
`File.AppendAllText`, which opens, writes, and closes the file so no buffered state remains.

**RotateIfNeeded**: Applies the retention policy.

- *Parameters*: none.
- *Returns*: `void` — the file set is updated in place.
- *Preconditions*: none; the active file may be absent.
- *Postconditions*: When the active file exists and has reached `MaximumFileSizeBytes`, it is moved to
  `{BaseFileName}-{yyyyMMddHHmmssfff}.log` and only the newest `RetainedFileCount` archives (ordered
  by file name, which sorts by timestamp) remain. Otherwise nothing changes.

**CreateDefault**: Creates a logger for `DefaultLogDirectory` with the default size and retention
settings.

- *Parameters*: none.
- *Returns*: `RollingFileLogger` — a logger writing to the per-user log folder.
- *Preconditions*: none.
- *Postconditions*: The log folder exists.

**Flush**: Satisfies the documented unit contract that accepted entries are committed. Each `Log` call
opens, writes, and closes the file, so no buffered output exists and the method does nothing. It is safe
to call at any time, including before any entry has been written.

- *Parameters*: none.
- *Returns*: `void`.
- *Preconditions*: none.
- *Postconditions*: Previously accepted entries are in the file system.

#### Error Handling

The constructor throws `ArgumentException` for a null or whitespace directory or base file name and for
a non-positive size or retention count. `Log` throws `ArgumentException` for a null or whitespace
message. `ActiveFilePath` resolution throws `ArgumentException` (from `PathHelpers`) when the base file
name would escape the log folder, so no file is written outside the log folder. Persistent write,
permission, and rotation failures are propagated as `IOException`, because the caller may need to tell
the user that diagnostic evidence could not be captured. The logger never drops failures silently.

#### Dependencies

- **PathHelpers** — resolves the active file path and archive file paths inside the log folder.
- **System.IO** — local file system primitives for append, move, enumerate, and delete.

#### Callers

- Application subsystems that report operational events and failures. Specific callers are listed here
  as the subsystems that use the logger are introduced.

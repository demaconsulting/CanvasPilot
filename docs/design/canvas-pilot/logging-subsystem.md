## Logging

![Logging Structure](LoggingSubsystemView.svg)

### Overview

The `Logging` subsystem provides local persistent logging for troubleshooting and for logs that users
can attach to bug reports. Its boundary starts with events and failures reported by other subsystems
and ends with a bounded rolling set of log files on the local disk. It contains one unit,
`RollingFileLogger`. Centralized analysis, telemetry, and network log shipping are outside the
subsystem's scope.

### Interfaces

**Application Logging API**: The in-process interface for recording operational events and failures.

- *Type*: In-process .NET API (`RollingFileLogger.Log`).
- *Role*: Provider.
- *Contract*: Accepts a log level (`Info`, `Warning`, or `Error`), message text, and optional exception
  context from any subsystem in the application.
- *Constraints*: The message shall not be empty or whitespace. Writes from multiple threads are
  serialized so that entries stay intact and ordered within a file. Write failures are propagated to
  the caller as `IOException` rather than swallowed.

**Log File Store**: The local persistent destination for log files.

- *Type*: File system.
- *Role*: Consumer.
- *Contract*: Writes timestamped log records to the active file and keeps a bounded number of
  timestamped archive files in a single local folder, by default `%LOCALAPPDATA%\CanvasPilot\logs`.
- *Constraints*: Rotation caps disk usage. File paths are resolved with the `Utilities` subsystem so a
  malformed file name cannot place a log file outside the log folder.

### Design

1. Application subsystems create or are given a `RollingFileLogger`. `RollingFileLogger.CreateDefault`
   targets the per-user default log folder; the constructor accepts any folder for tests and
   alternate configurations.
2. On each `Log` call, `RollingFileLogger` takes its lock, rotates the active file when it has reached
   the size threshold, formats the entry, and appends it to the active file.
3. Rotation moves the active file to a timestamped archive name and deletes the oldest archives beyond
   the retained count.
4. All file paths are resolved with `PathHelpers.SafePathCombine` from the `Utilities` subsystem.

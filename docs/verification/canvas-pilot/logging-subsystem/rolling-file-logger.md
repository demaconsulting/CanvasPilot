### RollingFileLogger

#### Verification Approach

`RollingFileLogger` is verified with unit tests defined in `RollingFileLoggerTests.cs`. The tests use the
real file system in a temporary folder created per test and removed on disposal. `PathHelpers` is used as
the real collaborator, because it is a pure function with no side effects, so no mocking or test doubles
are required. Rotation tests pre-create archive files with fixed timestamp names rather than sleeping
between writes, so the retention behavior is deterministic.

#### Test Environment

N/A - standard test environment. Tests use temporary local log directories and real file I/O; no
network or external services are required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Entries carry a parseable ISO-8601 timestamp, a level tag, the message, and exception details when
  supplied.
- Concurrent writers each have every entry recorded as an intact line.
- Rotation occurs only when the active file has reached the size threshold, and only the newest
  retained archives survive.
- Invalid configuration, empty messages, and a traversal base file name cause `ArgumentException`.

#### Test Scenarios

**RollingFileLogger_Log_Event_WritesTimestampedEntry**: An informational event is logged; the active
file exists and its entry contains the level tag, the message, and a parseable ISO-8601 timestamp. This
scenario is tested by `RollingFileLogger_Log_Event_WritesTimestampedEntry`.

**RollingFileLogger_Log_AllLevels_RecordsLevelTag**: One event is logged at each of the `Info`,
`Warning`, and `Error` levels; each level tag and message appears in the active file. This scenario is
tested by `RollingFileLogger_Log_AllLevels_RecordsLevelTag`.

**RollingFileLogger_Log_WithException_IncludesExceptionDetails**: An error is logged with an exception;
the entry contains both the message and the exception text. This scenario is tested by
`RollingFileLogger_Log_WithException_IncludesExceptionDetails`.

**RollingFileLogger_Log_ConcurrentWriters_RecordsEveryEntry**: Eight threads each log 25 entries with
rotation disabled; the active file contains exactly 200 intact entry lines. This scenario is tested by
`RollingFileLogger_Log_ConcurrentWriters_RecordsEveryEntry`.

**RollingFileLogger_Flush_BeforeAnyLogEntry_DoesNotThrow**: `Flush` is called on a logger that has not
written an entry; no exception is thrown. This scenario is tested by
`RollingFileLogger_Flush_BeforeAnyLogEntry_DoesNotThrow`.

**RollingFileLogger_DefaultLogDirectory_Default_IsCanvasPilotLogsUnderLocalAppData**: The default log
directory is read; it equals the `CanvasPilot\logs` folder beneath the platform local application data
folder. This scenario is tested by
`RollingFileLogger_DefaultLogDirectory_Default_IsCanvasPilotLogsUnderLocalAppData`.

**RollingFileLogger_Constructor_MissingDirectory_CreatesDirectory**: A logger is constructed for a
nested folder that does not exist; the folder is created and reported by `LogDirectory`. This scenario
is tested by `RollingFileLogger_Constructor_MissingDirectory_CreatesDirectory`.

**RollingFileLogger_RotateIfNeeded_ThresholdReached_RotatesAndPrunesArchives**: Three older archives and
a full active file exist with a retention of two; logging one entry rotates the active file, leaves
exactly two archives (the newest older archive and the new one), deletes the two oldest, and writes the
entry to a new active file. This scenario is tested by
`RollingFileLogger_RotateIfNeeded_ThresholdReached_RotatesAndPrunesArchives`.

**RollingFileLogger_RotateIfNeeded_BelowThreshold_KeepsActiveFile**: A small entry is logged with a
large threshold and rotation is requested; the active file remains and no archive is created. This
scenario is tested by `RollingFileLogger_RotateIfNeeded_BelowThreshold_KeepsActiveFile`.

**RollingFileLogger_Constructor_NonPositiveMaximumFileSize_ThrowsArgumentException**: A logger is
constructed with a zero maximum file size; `ArgumentException` is thrown. This scenario is tested by
`RollingFileLogger_Constructor_NonPositiveMaximumFileSize_ThrowsArgumentException`.

**RollingFileLogger_Constructor_NonPositiveRetainedFileCount_ThrowsArgumentException**: A logger is
constructed with a zero retained file count; `ArgumentException` is thrown. This scenario is tested by
`RollingFileLogger_Constructor_NonPositiveRetainedFileCount_ThrowsArgumentException`.

**RollingFileLogger_Constructor_WhitespaceDirectory_ThrowsArgumentException**: A logger is constructed
with a whitespace directory; `ArgumentException` is thrown. This scenario is tested by
`RollingFileLogger_Constructor_WhitespaceDirectory_ThrowsArgumentException`.

**RollingFileLogger_Log_WhitespaceMessage_ThrowsArgumentException**: A whitespace-only message is
logged; `ArgumentException` is thrown and no log file is created. This scenario is tested by
`RollingFileLogger_Log_WhitespaceMessage_ThrowsArgumentException`.

**RollingFileLogger_Log_TraversalBaseFileName_ThrowsArgumentException**: A logger is created with a base
file name of `../escape`; logging throws `ArgumentException` and no file is written outside the log
folder. This scenario is tested by `RollingFileLogger_Log_TraversalBaseFileName_ThrowsArgumentException`.

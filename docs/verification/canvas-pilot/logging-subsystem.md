## Logging

### Verification Approach

The `Logging` subsystem is verified by integration tests defined in `LoggingSubsystemTests.cs`. The tests
use a real `RollingFileLogger` writing real log files into temporary folders, and verify local
persistence of events and failures, retention-based pruning of archives, and confinement of log files
to the log folder through the `Utilities` subsystem's `PathHelpers`. No mocking is required because the
subsystem depends only on the local file system and on `PathHelpers`.

### Test Environment

N/A - standard test environment. Tests use temporary local log directories and real file I/O; no
network or external services are required.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- An event and a failure with exception details are written to a local log file.
- After sustained logging beyond the retention limit, exactly the retained number of archives remains
  and the oldest archive is deleted.
- A base file name that attempts path traversal causes an `ArgumentException` and no file is written
  outside the log folder.

### Test Scenarios

**LoggingSubsystem_PersistEvents_EventAndFailure_WritesLocalEntries**: An informational event and an
error with an `IOException` are logged; the active log file exists locally and contains the event
message, the failure message, and the exception detail. This scenario is tested by
`LoggingSubsystem_PersistEvents_EventAndFailure_WritesLocalEntries`.

**LoggingSubsystem_RetainHistory_SustainedLogging_PrunesOldArchives**: Five older archives exist, a tiny
size threshold forces a rotation for every entry, and retention is three archives; after ten entries
exactly three archives remain and the oldest archive is gone. This scenario is tested by
`LoggingSubsystem_RetainHistory_SustainedLogging_PrunesOldArchives`.

**LoggingSubsystem_SafePaths_TraversalBaseFileName_ThrowsArgumentException**: A logger is created with a
base file name of `../escape`; logging throws `ArgumentException` and no `escape.log` file appears
outside the log folder. This scenario is tested by
`LoggingSubsystem_SafePaths_TraversalBaseFileName_ThrowsArgumentException`.

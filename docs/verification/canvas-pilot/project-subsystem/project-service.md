### ProjectService

#### Verification Approach

`ProjectService` is verified by unit tests in `ProjectServiceTests.cs`. They use real temporary folders and real
file I/O, a hand-written clock (`TimeProvider`) so created and modified times can be checked exactly, a clock
that throws to force an input or output failure, and a real `RollingFileLogger` on a temporary folder to check
what is logged. Name validation is covered with data-driven tests of invalid and valid names. Cancellation is
tested with an already cancelled token. The tests assert observable results: the returned result, the files on
disk, the open project, and the log content.

#### Test Environment

Standard test environment. Tests use temporary local folders and real file I/O; no network, external services,
or display are required. Where the hidden attribute applies, the check runs only on Windows.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A created project has the folder, the project file with `formatVersion` 1, the name, and the creation time, and
  an empty data folder, and is the open project; an empty existing folder is accepted.
- Invalid, reserved, empty, dot, and too-long names, an unusable parent folder, an existing file, and an
  existing non-empty folder are refused with messages and nothing on disk changes.
- A cancelled or failed creation removes what it added.
- A project file with comments, unusual casing, or unknown properties opens; a newer, damaged, wrong, or
  missing file is refused with a message naming the file, is logged, and leaves the open project unchanged.
- Saving updates the modified time, keeps the created time, name, and unknown properties, leaves no temporary
  file, and fails with a message when nothing is open or the folder is gone.
- Closing clears the project and raises one change event.

#### Test Scenarios

**ProjectService_CreateAsync_ValidName_CreatesFolderProjectFileAndDataFolder**: A project is created in an
existing parent folder; the folder holds only the project file and an empty hidden data folder, the file has
the documented properties, the open project matches, and one change event was raised. This scenario is tested
by `ProjectService_CreateAsync_ValidName_CreatesFolderProjectFileAndDataFolder`.

**ProjectService_CreateAsync_NameWithSurroundingSpaces_TrimsName**: A padded name is trimmed in the name and the
folder. This scenario is tested by `ProjectService_CreateAsync_NameWithSurroundingSpaces_TrimsName`.

**ProjectService_CreateAsync_ExistingEmptyFolder_Succeeds**: An existing empty folder receives the project file.
This scenario is tested by `ProjectService_CreateAsync_ExistingEmptyFolder_Succeeds`.

**ProjectService_CreateAsync_ExistingNonEmptyFolder_FailsAndLeavesContent**: A folder with a file is refused
with a message about it not being empty, its content is unchanged, and no project is open. This scenario is
tested by `ProjectService_CreateAsync_ExistingNonEmptyFolder_FailsAndLeavesContent`.

**ProjectService_CreateAsync_ExistingFileWithName_Fails**: A file with the project's name is refused. This
scenario is tested by `ProjectService_CreateAsync_ExistingFileWithName_Fails`.

**ProjectService_CreateAsync_InvalidName_FailsWithoutCreatingAnything**: Twenty invalid names (empty, blank,
dots, leading and trailing periods, separators, forbidden characters, a tab, reserved device names, and a
traversal) are refused with a message, the parent folder stays empty, and no project is open. This scenario is
tested by `ProjectService_CreateAsync_InvalidName_FailsWithoutCreatingAnything`.

**ProjectService_ValidateProjectName_LengthLimit_IsEnforced**: A 100-character name is accepted and a
101-character name is refused with the limit in the message. This scenario is tested by
`ProjectService_ValidateProjectName_LengthLimit_IsEnforced`.

**ProjectService_ValidateProjectName_OrdinaryName_IsAccepted**: Names with spaces, an inner period, hyphens,
non-ASCII letters, and a word that only resembles a reserved name are accepted. This scenario is tested by
`ProjectService_ValidateProjectName_OrdinaryName_IsAccepted`.

**ProjectService_CreateAsync_UnusableParentFolder_Fails**: A null, empty, blank, relative, or missing parent is
refused with a message and nothing is created. This scenario is tested by
`ProjectService_CreateAsync_UnusableParentFolder_Fails`.

**ProjectService_ValidateNewProject_NameProblemComesFirst**: The name problem is reported before the folder
problem, and a valid combination reports none. This scenario is tested by
`ProjectService_ValidateNewProject_NameProblemComesFirst`.

**ProjectService_CreateAsync_Cancelled_RemovesFolderItCreated**: Creation with a cancelled token throws a
cancellation exception and leaves no folder and no open project. This scenario is tested by
`ProjectService_CreateAsync_Cancelled_RemovesFolderItCreated`.

**ProjectService_CreateAsync_CancelledInExistingFolder_KeepsFolderRemovesAdditions**: The same cancellation in
an existing empty folder keeps the folder and leaves it empty. This scenario is tested by
`ProjectService_CreateAsync_CancelledInExistingFolder_KeepsFolderRemovesAdditions`.

**ProjectService_CreateAsync_IoFailure_ReturnsFailureAndLogsError**: A clock that throws `IOException` makes
creation return a failed result with the reason, log an error, leave nothing behind, and open nothing. This
scenario is tested by `ProjectService_CreateAsync_IoFailure_ReturnsFailureAndLogsError`.

**ProjectService_CreateAsync_InvalidName_LogsWarning**: A refused name is logged at warning level with the
name. This scenario is tested by `ProjectService_CreateAsync_InvalidName_LogsWarning`.

**ProjectService_OpenAsync_CreatedProject_ReturnsSameProject**: A project created by one service is opened by
another with the same name, folder, file, and creation time. This scenario is tested by
`ProjectService_OpenAsync_CreatedProject_ReturnsSameProject`.

**ProjectService_OpenAsync_UnknownPropertiesAndComments_AreIgnored**: A hand-written file with a comment,
unusual casing, a trailing comma, and an unknown property opens with its known values. This scenario is tested
by `ProjectService_OpenAsync_UnknownPropertiesAndComments_AreIgnored`.

**ProjectService_OpenAsync_MissingName_UsesFileName**: A file without a name takes the name from the file name.
This scenario is tested by `ProjectService_OpenAsync_MissingName_UsesFileName`.

**ProjectService_OpenAsync_NewerFormat_FailsWithClearMessageAndKeepsCurrent**: A file with `formatVersion` 2 is
refused with a message about a newer version, and the open project is unchanged. This scenario is tested by
`ProjectService_OpenAsync_NewerFormat_FailsWithClearMessageAndKeepsCurrent`.

**ProjectService_OpenAsync_NotAProjectFile_FailsAndLogs**: Empty, non-JSON, truncated, array, null, no-version,
zero-version, text-version, and bad-date files are each refused with a message naming the file, logged, and
leave nothing open. This scenario is tested by `ProjectService_OpenAsync_NotAProjectFile_FailsAndLogs`.

**ProjectService_OpenAsync_MissingWrongOrBlankPath_Fails**: A missing file, a text file, a blank path, and an
invalid path are each refused with their own message. This scenario is tested by
`ProjectService_OpenAsync_MissingWrongOrBlankPath_Fails`.

**ProjectService_OpenAsync_SecondProject_ReplacesCurrentAndRaisesEvent**: Opening a second project replaces the
first and raises one change event. This scenario is tested by
`ProjectService_OpenAsync_SecondProject_ReplacesCurrentAndRaisesEvent`.

**ProjectService_SaveAsync_OpenProject_UpdatesModifiedTimeAndKeepsContent**: After the clock moves forward, save
writes the later modified time, keeps the created time and name, updates the open project, and leaves no
temporary file. This scenario is tested by
`ProjectService_SaveAsync_OpenProject_UpdatesModifiedTimeAndKeepsContent`.

**ProjectService_SaveAsync_UnknownProperties_ArePreserved**: An unknown property in an opened file is still in
the file after save. This scenario is tested by `ProjectService_SaveAsync_UnknownProperties_ArePreserved`.

**ProjectService_SaveAsync_NoProject_Fails**: Save with nothing open fails with a message. This scenario is
tested by `ProjectService_SaveAsync_NoProject_Fails`.

**ProjectService_SaveAsync_FolderRemoved_FailsAndLogsError**: Save after the project folder was deleted fails
with a message, logs an error naming the file, and keeps the project open. This scenario is tested by
`ProjectService_SaveAsync_FolderRemoved_FailsAndLogsError`.

**ProjectService_Close_OpenProject_ClearsCurrentAndRaisesEventOnce**: Closing twice clears the project and
raises one change event. This scenario is tested by
`ProjectService_Close_OpenProject_ClearsCurrentAndRaisesEventOnce`.

**ProjectService_CloseThenOpen_SameFile_OpensAgain**: A closed project is opened again and saved. This
scenario is tested by `ProjectService_CloseThenOpen_SameFile_OpensAgain`.

**ProjectService_SetActiveStyleKitAsync_ValidId_IsWrittenAndReadBackAfterReopen**: Setting the kit `slides-dark` on a
created project writes `activeStyleKit` and the later modified time into the file, makes the kit part of the open
project and of the returned project, raises one change event, leaves no temporary file, and a second service that opens
the file reads the same kit. This scenario is tested by
`ProjectService_SetActiveStyleKitAsync_ValidId_IsWrittenAndReadBackAfterReopen`.

**ProjectService_SetActiveStyleKitAsync_NullOrSameId_RemovesPropertyOrDoesNothing**: Setting the kit that the project
already has writes nothing and raises nothing, setting none removes the property from the file and raises one event, and
setting none again writes nothing. This scenario is tested by
`ProjectService_SetActiveStyleKitAsync_NullOrSameId_RemovesPropertyOrDoesNothing`.

**ProjectService_SetActiveStyleKitAsync_OlderFileWithUnknownProperty_ReadsAsNoStyleAndKeepsProperty**: A project file
with no style property and an unknown property is read as having no style, and after a kit is set the file has the kit
and the unknown property. This scenario is tested by
`ProjectService_SetActiveStyleKitAsync_OlderFileWithUnknownProperty_ReadsAsNoStyleAndKeepsProperty`.

**ProjectService_SetActiveStyleKitAsync_InvalidId_IsRefused**: An empty id, a blank id, an id with an upper-case letter,
a leading dash, a space, an underscore, `..`, a backslash, a line feed, or 49 characters is refused with the message
that says what an id is, and the file and the open project stay as they were. This scenario is tested by
`ProjectService_SetActiveStyleKitAsync_InvalidId_IsRefused`.

**ProjectService_SetActiveStyleKitAsync_LongestId_IsAccepted**: An id of 48 characters that starts with a digit is
accepted. This scenario is tested by `ProjectService_SetActiveStyleKitAsync_LongestId_IsAccepted`.

**ProjectService_SetActiveStyleKitAsync_NoProject_Fails**: Setting a kit with nothing open fails with a message. This
scenario is tested by `ProjectService_SetActiveStyleKitAsync_NoProject_Fails`.

**ProjectService_SetActiveStyleKitAsync_FolderRemoved_FailsAndKeepsTheOpenProject**: Setting a kit after the project
folder was deleted fails with the message that the project could not be saved, and the open project keeps the style it
had. This scenario is tested by `ProjectService_SetActiveStyleKitAsync_FolderRemoved_FailsAndKeepsTheOpenProject`.

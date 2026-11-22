### ChatHistoryStore

#### Verification Approach

`ChatHistoryStore` and `ProjectHistoryKey` are verified with unit tests in
`test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatHistoryStoreTests.cs`, over the real store and the real files
in temporary folders: a folder for the projects, a folder for the history root (as the folder of the user's computer),
and a folder for the log. Nothing is replaced except the clock, with the manual clock of the chat tests, which dates the
sidecar file. A bad file is made by writing it in the folder where the store looks for it. The tests check what is on
the disk (which files exist, their names and content, that nothing is in the project folder) and what the store answers.

#### Test Environment

The standard test runner. No display, network, or special permission is needed. The tests that depend on whether the
file system ignores case use the platform's answer (`TestPlatformPaths.IgnoresCase`), and the key vectors are made from
already normalized text, so that they hold on every platform.

#### Acceptance Criteria

- All tests of the unit pass with zero failures.
- The key is the first 16 bytes of the SHA-256 hash as 32 lowercase hexadecimal characters (two fixed vectors), every
  spelling of one path (separators, a dot segment, a segment that goes up and down, a trailing separator, and, where the
  file system ignores case, the case) gives one key, another place or another file name gives another key, and a missing
  path is refused.
- A save writes `history.json` of version 1 and `project.json` in the folder of the key below the root and nothing
  else, with every kind of row read back as it was written (words with quotation marks, line breaks, and non-ASCII
  characters included); the file has only the known properties, so it holds no picture, payload, or result; nothing is
  created by reading; nothing is written into the project folder.
- Of 250 rows the newest 200 are kept; of rows that would be 2.7 MB the newest that fit are kept and the file is not
  larger than 2 MB; a text of more than 100,000 characters is cut with an ellipsis.
- A save leaves no temporary file, removes the temporary files that a crash left and does not read them, and replaces
  the file as a whole; a root that cannot be made is a failed result with a sentence and a log entry and not an
  exception.
- A file that is not JSON, has a newer version, has no usable version, is not an object, has no rows, has a row of an
  unknown kind, or is larger than four times the limit is renamed `.bad`, gives no rows, and (when it is logged) is
  named in the log without any of its words; a later save starts a new history; rows that cannot be used are dropped
  and the others are read.
- Clear deletes the folder of one project with its files, including a `.bad` file, and leaves other projects alone; it
  is not an error when there is nothing to delete.
- A root that is the project folder or lies inside it (also below `.canvaspilot`) is refused for load, save, and clear
  with the sentence that says so, nothing in the project changes, and the log says so; a root beside the project whose
  name starts like the project's is not refused.

#### Test Scenarios

**ProjectHistoryKey_HashOf_FixedVectors_Are32LowercaseHexCharactersOfTheFirst16Bytes**: The keys of the normalized paths
`c:\projects\demo\demo.canvaspilot` and `/home/me/demo/demo.canvaspilot` are the two known 32-character values, in
lowercase hexadecimal. This scenario is tested by
`ProjectHistoryKey_HashOf_FixedVectors_Are32LowercaseHexCharactersOfTheFirst16Bytes`.

**ProjectHistoryKey_Compute_SpellingsOfOnePath_GiveOneKey**: The path with slashes of the other kind, with a dot
segment, with a segment that goes up and down, and with a trailing separator gives the key of the plain path, and the
upper-case spelling gives the same key exactly where the file system ignores case. This scenario is tested by
`ProjectHistoryKey_Compute_SpellingsOfOnePath_GiveOneKey`.

**ProjectHistoryKey_Compute_OtherCaseOrOtherPlace_FollowsTheFileSystem**: With case ignored the upper-case path has the
same key and with case kept another one; a project in another folder, and a project file with another name, have other
keys, which is why a moved or renamed project starts a fresh history. This scenario is tested by
`ProjectHistoryKey_Compute_OtherCaseOrOtherPlace_FollowsTheFileSystem`.

**ProjectHistoryKey_Compute_NullOrBlank_Throws**: A missing path throws `ArgumentNullException`, and a blank one
`ArgumentException`. This scenario is tested by `ProjectHistoryKey_Compute_NullOrBlank_Throws`.

**ChatHistoryStore_SaveThenLoad_RoundTripsEveryKindOfRow**: A user row with marks (one without a note) and an
attachment, a reply, a failed tool row, and a stopped reply are saved and read back with the same kinds, times, words,
paths, notes, tool name, outcome, and reply states. This scenario is tested by
`ChatHistoryStore_SaveThenLoad_RoundTripsEveryKindOfRow`.

**ChatHistoryStore_Save_WritesTheKeyedFolderWithSidecar_AndNothingIntoTheProject**: Reading creates nothing, not even
the root. After a save the folder of the key holds exactly `history.json` and `project.json`; the sidecar names the
project and its file and has a time of last use; the project folder is empty. This scenario is tested by
`ChatHistoryStore_Save_WritesTheKeyedFolderWithSidecar_AndNothingIntoTheProject`.

**ChatHistoryStore_Save_FileHasVersion1AndHoldsTextOnly**: The saved file has version 1 and rows, nothing else at the
top, rows whose properties are among the known ones, and the kinds in the order they were given. This scenario is tested
by `ChatHistoryStore_Save_FileHasVersion1AndHoldsTextOnly`.

**ChatHistoryStore_Save_MoreThan200Rows_KeepsTheNewest200**: Of 250 rows the first 50 are dropped. This scenario is
tested by `ChatHistoryStore_Save_MoreThan200Rows_KeepsTheNewest200`.

**ChatHistoryStore_Save_MoreThan2MegabytesOfRows_DropsTheOldestFirst**: Thirty rows of 90,000 characters are saved; the
file is at most 2 MB, fewer than thirty rows are read, the last is the newest, and the rows read are the newest ones in
order. This scenario is tested by `ChatHistoryStore_Save_MoreThan2MegabytesOfRows_DropsTheOldestFirst`.

**ChatHistoryStore_Cap_VeryLongText_IsCutAndShortTextStays**: A text of 100,500 characters is cut to 100,000 ending in
an ellipsis, and a short text is not changed. This scenario is tested by
`ChatHistoryStore_Cap_VeryLongText_IsCutAndShortTextStays`.

**ChatHistoryStore_Save_AfterACrash_RemovesLeftoversAndReplacesTheFileWhole**: Temporary files that a write that did not
end would leave (half of a file) do not disturb reading; the next save removes them, leaves only the two files, and the
file holds the new rows. This scenario is tested by
`ChatHistoryStore_Save_AfterACrash_RemovesLeftoversAndReplacesTheFileWhole`.

**ChatHistoryStore_Save_RootCannotBeMade_ReturnsFailureAndDoesNotThrow**: With a file where the root should be a folder,
a save returns a failed result with the sentence "could not be saved", throws nothing, and logs the fact without the
words. This scenario is tested by `ChatHistoryStore_Save_RootCannotBeMade_ReturnsFailureAndDoesNotThrow`.

**ChatHistoryStore_Load_CorruptFile_IsRenamedBadIgnoredAndLoggedWithoutItsWords**: A file cut off in the middle is
renamed to `history.json.bad` with its content kept, nothing is read, the log names the file and the `.bad` name and
does not contain a word of the file, and a save afterwards makes a new history. This scenario is tested by
`ChatHistoryStore_Load_CorruptFile_IsRenamedBadIgnoredAndLoggedWithoutItsWords`.

**ChatHistoryStore_Load_NewerVersion_IsRenamedBadAndIgnored**: A file of version 2 is renamed `.bad` byte for byte,
gives no rows, and the log says that the version is newer. This scenario is tested by
`ChatHistoryStore_Load_NewerVersion_IsRenamedBadAndIgnored`.

**ChatHistoryStore_Load_FileThatIsNotAHistory_IsRenamedBadAndIgnored**: Files without a version, with a version that is
a string or below 1, that are an array, `null`, empty, without rows, or with a row of an unknown kind are each renamed
`.bad` and give no rows. This scenario is tested by
`ChatHistoryStore_Load_FileThatIsNotAHistory_IsRenamedBadAndIgnored`.

**ChatHistoryStore_Load_FileFarBiggerThanTheLimit_IsRenamedBad**: A file of more than eight million characters is
renamed `.bad` without being parsed. This scenario is tested by
`ChatHistoryStore_Load_FileFarBiggerThanTheLimit_IsRenamedBad`.

**ChatHistoryStore_Load_RowsThatCannotBeUsed_AreDroppedAndTheOthersRead**: Of five entries, a tool row without a name, a
user row without words, and `null` are dropped and the two good rows are read; the file stays. This scenario is tested
by `ChatHistoryStore_Load_RowsThatCannotBeUsed_AreDroppedAndTheOthersRead`.

**ChatHistoryStore_Clear_DeletesTheFolderOfThatProjectOnly**: Two projects have a history and one of them a `.bad` file;
clearing the first deletes its folder, clearing it again succeeds, and the second history is intact. This scenario is
tested by `ChatHistoryStore_Clear_DeletesTheFolderOfThatProjectOnly`.

**ChatHistoryStore_RootInsideTheProject_IsRefusedForEveryOperation**: With the root equal to the project folder, below
it, and below `.canvaspilot`, save and clear return failed results with the sentence that the history lies inside the
project folder, load gives no rows, the project folder holds exactly what it held (a file that must stay is unchanged),
and the log says so. This scenario is tested by `ChatHistoryStore_RootInsideTheProject_IsRefusedForEveryOperation`.

**ChatHistoryStore_RootBesideTheProjectWithTheSamePrefix_IsNotInsideIt**: A root `proj2` beside the project `proj` is
used normally. This scenario is tested by `ChatHistoryStore_RootBesideTheProjectWithTheSamePrefix_IsNotInsideIt`.

**ChatHistoryStore_New_BlankRootThrows_AndTheDefaultRootIsBelowTheLocalData**: A blank root throws
`ArgumentException`, a missing one `ArgumentNullException`, and the default root ends with `CanvasPilot\history`. This
scenario is tested by `ChatHistoryStore_New_BlankRootThrows_AndTheDefaultRootIsBelowTheLocalData`.

**ChatHistoryStore_Operations_NullArguments_Throw**: Every operation refuses a missing project or missing rows with
`ArgumentNullException`. This scenario is tested by `ChatHistoryStore_Operations_NullArguments_Throw`.

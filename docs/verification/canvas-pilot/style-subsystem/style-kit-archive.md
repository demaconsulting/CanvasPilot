### StyleKitArchive

#### Verification Approach

`StyleKitArchive` is verified with unit tests in `StyleKitArchiveTests.cs` on temporary folders. The zip files that the
tests read are made by the tests, as bytes in memory with the zip support of the base class library, so that an entry can
have any name, any attributes, and, by patching the headers of the bytes, any size and checksum that an attacker could put
there. The tests check what is on disk after each attack (the target folder and its parent hold nothing that was not
there), and not how the unit does its work.

#### Test Environment

N/A - standard test environment with temporary local folders.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A kit that is exported and imported is the same kit (the same canonical text), with every example file byte for byte; an
  archive with all its files in one folder is accepted; an export replaces a file that is there, writes no partial file
  when it fails, and reports a missing example file as an `IOException`.
- The attacks below are each refused with a sentence and not by an exception, and in each the target folder is left empty
  and nothing is made outside it.
- A style file with errors is refused with its issues; an archive with no style file and a file that is not a zip file or
  does not exist are refused with a message.
- An id that is already in the target is refused, replaced, or kept with a number as the conflict setting says; a name that
  is already at its longest stays within the limit; a refused import and a replaced kit leave the old kit intact.

#### Test Scenarios

**StyleKitArchive_ExportThenImport_KitWithExamples_RoundTripsByteForByte**: A kit with two example files of different kinds
(a text file and bytes that are not text) is exported and imported into an empty place. The imported kit has the same
canonical text, the captions of its examples, and the same example files byte for byte in a folder named as the id. This
scenario is tested by `StyleKitArchive_ExportThenImport_KitWithExamples_RoundTripsByteForByte`.

**StyleKitArchive_Import_ArchiveWithOneWrapperFolder_IsAccepted**: An archive whose files are all in one folder, such as one
made by zipping a folder, is imported as the kit it holds. This scenario is tested by
`StyleKitArchive_Import_ArchiveWithOneWrapperFolder_IsAccepted`.

**StyleKitArchive_Import_UnsafeOrForeignEntryName_IsRefusedAndNothingIsWritten**: The attacks on names, each as an archive of
a good kit with one more entry: a name that climbs out (`../evil.txt`, `examples/../../evil.png`), the same with
backslashes, a drive (`C:\evil.txt`, `C:evil.txt`), an absolute path and a network path (`/etc/evil`, `//server/share/...`),
a character that a Windows file name cannot have (`:`, a NUL, `?`), a device name (`CON.svg`), a trailing period, a part that
is `.` or empty, and then names that are only not part of a kit (a folder deeper than `examples/`, `readme.txt`, `Style.json`
in another case, and a style file inside two folders). Each is refused whole, the folder of the target is empty, and no
file such as `evil.txt` is anywhere beside it. This scenario is tested by
`StyleKitArchive_Import_UnsafeOrForeignEntryName_IsRefusedAndNothingIsWritten`.

**StyleKitArchive_Import_NameOfMoreThan200Characters_IsRefused**: A name of more than 200 characters is refused. This
scenario is tested by `StyleKitArchive_Import_NameOfMoreThan200Characters_IsRefused`.

**StyleKitArchive_Import_LinkEntry_IsRefused**: An entry made as a symbolic link, with the Unix link mode in its
attributes, is refused. This scenario is tested by `StyleKitArchive_Import_LinkEntry_IsRefused`.

**StyleKitArchive_Import_SameNameTwice_IsRefused**: An archive that holds the same file twice, also in another case, is
refused. This scenario is tested by `StyleKitArchive_Import_SameNameTwice_IsRefused`.

**StyleKitArchive_Import_HeaderThatUnderstatesTheSize_StopsAtTheLimitAndCleansUp**: The bytes of an archive are patched so
that the header of a 20 MiB example file claims 10 bytes (the lying header of a zip bomb). The import does not trust the
claim: the framework gives only the bytes that the header says, the count and the checksum do not match the header, and the
archive is refused as damaged with the sentence that the entry does not match its header; the hidden staging folder is
removed and the target is empty. This scenario is tested by
`StyleKitArchive_Import_HeaderThatUnderstatesTheSize_StopsAtTheLimitAndCleansUp`.

**StyleKitArchive_Import_HonestSizesOverTheLimits_AreRefusedBeforeAnythingIsWritten**: A file of more than 16 MiB, and files
of 16 MiB or less that together are more than 64 MiB, are refused from their headers before any file is written. This scenario
is tested by `StyleKitArchive_Import_HonestSizesOverTheLimits_AreRefusedBeforeAnythingIsWritten`.

**StyleKitArchive_Import_TooManyEntriesOrExamples_AreRefused**: An archive of more than 100 entries, and one with more than
24 example files, are refused. This scenario is tested by `StyleKitArchive_Import_TooManyEntriesOrExamples_AreRefused`.

**StyleKitArchive_Import_NoStyleFileOrInvalidStyleFile_IsRefusedWithTheReason**: An archive with only an example file says
that it has no style file; an archive whose style file has a color that is not a color is refused with the error in the
issues, and one whose style file is not JSON is refused with the errors of the style file; nothing is
left in the target. This scenario is tested by `StyleKitArchive_Import_NoStyleFileOrInvalidStyleFile_IsRefusedWithTheReason`.

**StyleKitArchive_Import_NotAZipOrMissingFile_IsRefusedWithAMessage**: A text file with a zip name and a
path that does not exist each give a result with a message and no exception. This scenario is tested by
`StyleKitArchive_Import_NotAZipOrMissingFile_IsRefusedWithAMessage`.

**StyleKitArchive_Import_IdAlreadyThere_RefuseReplaceAndKeepBoth**: The id of an imported kit is already in the target. With
`Refuse` the old kit is kept and the target holds nothing else; with `KeepBoth` the new kit is `sample-2` named
`Sample (2)`, and then `sample-3`; with `Replace` the old kit is replaced and no backup folder is left. This scenario is
tested by `StyleKitArchive_Import_IdAlreadyThere_RefuseReplaceAndKeepBoth`.

**StyleKitArchive_Import_KeepBothWithLongName_StaysWithinTheNameLimit**: A kit whose name has 60 characters, kept beside
another, is renamed with a number and is still 60 characters, valid, and imported. This scenario is tested by
`StyleKitArchive_Import_KeepBothWithLongName_StaysWithinTheNameLimit`.

**StyleKitArchive_Export_ExistingFileMissingExampleAndNulls_ReplacesFailsAndRefuses**: An export over an existing file
replaces it; an export of a kit whose example file has been deleted is an `IOException`, leaves the existing archive as it
was and no temporary file; a missing entry and a blank path are refused with the argument exceptions. This scenario is
tested by `StyleKitArchive_Export_ExistingFileMissingExampleAndNulls_ReplacesFailsAndRefuses`.

#### Subsystem Test

`StyleSubsystem_ExchangeKitsAsZipFiles_ExportImportAndHostileArchive_MoveKitsAndChangeNothingOutside` in
`StyleSubsystemTests.cs` exports a kit that was read from a folder, imports it to another place, imports it again (refused),
and imports an archive with a name that climbs out of the target (refused); the kit that arrived is equal, and nothing was
created beside the target.

#### Requirements Coverage

- **CanvasPilot-StyleKitArchive-ExportKitAsZip**: the round trip and the export test.
- **CanvasPilot-StyleKitArchive-ImportKitFromZip**: the round trip, wrapper, conflict, long name, no style file or invalid
  style file, and not a zip tests.
- **CanvasPilot-StyleKitArchive-RefuseUnsafeArchives**: the names, long name, link, repeat, header, limits, and count tests.

### AttachmentIntake

#### Verification Approach

`AttachmentIntake` is verified with unit tests in `AttachmentIntakeTests.cs`. The attachment store is replaced by an
NSubstitute substitute, which records every call, so the tests can show both what the intake made and that it asked the
store for nothing it should not. Dropped content is real files and folders in a temporary folder, so that the checks of
existence, kind, and size are the real ones. Tests that show that nothing is opened hold a file open exclusively while
it is dropped, and compare the content of dropped programs and scripts after the drop.

#### Test Environment

A temporary folder per test holds the dropped files and is removed afterwards. A test creates a file that is one byte
larger than 200 MB by setting its length, which needs no real data. No other setup is needed.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each dropped path becomes the chip of its kind with its original path, or is left out with a reason that names what is
  wrong; one bad item does not stop the others.
- A file that another program holds open exclusively, a program, and a script become chips, keep their content, and the
  store receives no call.
- Paths that are not complete, hold `..`, device names, alternate data streams, control characters, or the device
  namespace are left out and the store receives no call.
- Pasted pictures are handed to the store unchanged and become pasted picture chips, or are left out with the store's
  reason.
- Text is cleaned and limited, and the limits for one operation hold.

#### Test Scenarios

**AttachmentIntake_Accept_ExistingFile_BecomesFileChipWithOriginalPath**: A text file is dropped and becomes a file chip
with its original path. This scenario is tested by
`AttachmentIntake_Accept_ExistingFile_BecomesFileChipWithOriginalPath`.

**AttachmentIntake_Accept_ExistingFolder_BecomesFolderChip**: A folder given with a trailing separator becomes a folder
chip for the folder without the separator. This scenario is tested by
`AttachmentIntake_Accept_ExistingFolder_BecomesFolderChip`.

**AttachmentIntake_Accept_PictureFile_BecomesImageChipReferencingOriginalPath**: Files with the extensions `.png`,
`.JPG`, `.jpeg`, `.svg`, `.gif`, `.webp`, `.bmp`, and `.tif` become picture chips that are not marked as pasted, refer
to the original path, and cause no call on the store. This scenario is tested by
`AttachmentIntake_Accept_PictureFile_BecomesImageChipReferencingOriginalPath`.

**AttachmentIntake_Accept_MissingPath_IsSkippedWithReason**: A path that does not exist makes no chip and a note that
says so. This scenario is tested by `AttachmentIntake_Accept_MissingPath_IsSkippedWithReason`.

**AttachmentIntake_Accept_SamePathTwice_IsAcceptedOnce**: A file dropped twice is one chip with a note about the repeat,
and a second spelling of the path with a `..` part is refused by itself. This scenario is tested by
`AttachmentIntake_Accept_SamePathTwice_IsAcceptedOnce`.

**AttachmentIntake_Accept_UnsafePath_IsSkippedWithReasonAndMakesNoChip**: Fourteen unsafe paths, a relative path, paths
with `..`, the device names `con` and `NUL`, an alternate data stream, a trailing dot, characters a name cannot hold,
the device namespace in two forms, a control character, an empty path, and a blank path, each make no chip and one note,
and the store receives no call. This scenario is tested by
`AttachmentIntake_Accept_UnsafePath_IsSkippedWithReasonAndMakesNoChip`.

**AttachmentIntake_Accept_DriveRoot_IsSkipped**: The root of a drive is refused. This scenario is tested by
`AttachmentIntake_Accept_DriveRoot_IsSkipped`.

**AttachmentIntake_Accept_PastedPicture_IsSavedAndBecomesPastedImageChip**: A pasted picture is given to the store with
its exact bytes and becomes a pasted picture chip for the path the store returned. This scenario is tested by
`AttachmentIntake_Accept_PastedPicture_IsSavedAndBecomesPastedImageChip`.

**AttachmentIntake_Accept_PastedPictureRefusedByStore_IsSkippedAndOthersStay**: The store refuses the first of two
pasted pictures; that one is left out with the store's reason, and the second picture and a dropped file are accepted.
This scenario is tested by `AttachmentIntake_Accept_PastedPictureRefusedByStore_IsSkippedAndOthersStay`.

**AttachmentIntake_Accept_PastedPictureCannotBeWritten_IsSkipped**: The store cannot write; the picture is left out with
the reason. This scenario is tested by `AttachmentIntake_Accept_PastedPictureCannotBeWritten_IsSkipped`.

**AttachmentIntake_Accept_TextWithPlaceholderAndControlCharacters_IsSanitized**: Text with the chip placeholder, a bell,
and a Windows line break becomes one typed segment with clean text. This scenario is tested by
`AttachmentIntake_Accept_TextWithPlaceholderAndControlCharacters_IsSanitized`.

**AttachmentIntake_Accept_TextOfOnlyControlCharactersOrNoText_MakesNoSegment**: Text of unwanted characters only, empty
text, and no text make no segment. This scenario is tested by
`AttachmentIntake_Accept_TextOfOnlyControlCharactersOrNoText_MakesNoSegment`.

**AttachmentIntake_Accept_VeryLongText_IsShortenedAndFlagged**: Text one character over the limit is shortened to the
limit and flagged. This scenario is tested by `AttachmentIntake_Accept_VeryLongText_IsShortenedAndFlagged`.

**AttachmentIntake_Accept_FileLargerThanLimit_IsAcceptedAndFlagged**: A file one byte over 200 MB becomes a chip and a
warning that names the limit. This scenario is tested by
`AttachmentIntake_Accept_FileLargerThanLimit_IsAcceptedAndFlagged`.

**AttachmentIntake_Accept_ProgramsAndScripts_AreOnlyChipsAndNeverOpened**: Files with the extensions `.exe`, `.bat`,
`.ps1`, `.docm`, `.lnk`, `.html`, and `.dll`, and an `.exe` that is held open exclusively, each become a file chip, keep
their content, and the store receives no call. This scenario is tested by
`AttachmentIntake_Accept_ProgramsAndScripts_AreOnlyChipsAndNeverOpened`.

**AttachmentIntake_Accept_MoreThanMaximumItems_AcceptsMaximumAndReportsRest**: 205 files are dropped; the first 200 are
accepted in order and each of the other five has a note. This scenario is tested by
`AttachmentIntake_Accept_MoreThanMaximumItems_AcceptsMaximumAndReportsRest`.

**AttachmentIntake_Accept_MixedContent_KeepsOrderAndAcceptsTheRest**: A file, a missing path, a folder, a pasted
picture, and text give the chips and text in the order paths, pictures, text, with one note for the missing path. This
scenario is tested by `AttachmentIntake_Accept_MixedContent_KeepsOrderAndAcceptsTheRest`.

**AttachmentIntake_New_AndAccept_NullArguments_Throw**: A missing store and missing content are refused. This scenario
is tested by `AttachmentIntake_New_AndAccept_NullArguments_Throw`.

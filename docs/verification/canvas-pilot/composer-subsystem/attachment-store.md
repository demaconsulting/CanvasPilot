### AttachmentStore

#### Verification Approach

`AttachmentStore` and its name rules are verified with unit tests in `AttachmentStoreTests.cs` on a temporary folder
that holds a project folder and a folder for the files that are attached from outside the project. Nothing is
substituted: the unit's purpose is what it does to the file system, so the tests assert on the files and folders that
exist afterwards, their bytes, and the results, and that the files that were attached are unchanged. A fixed clock gives
known names to pasted pictures. The test of links makes a junction on Windows (which needs no privilege) and a symbolic
link elsewhere, and removes the link before the folder is deleted. The test of a file that cannot be read holds it open
exclusively. The tests of size limits create a file one byte over the limit by setting its length.

#### Test Environment

A temporary folder per test, removed afterwards. The link test starts `powershell.exe` on Windows to create a junction.
No other setup is needed.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A pasted PNG picture is saved with its exact bytes under a name from the time and a number in
  `.canvaspilot/attachments`; other data is refused and nothing is written.
- Leftover pictures are deleted unless a chip refers to them, and nothing else is deleted.
- Attachments from outside the project are copied into `attachments` byte for byte, a taken name gets a number and is
  never overwritten, folders are copied with everything in them, links are skipped, and the originals are unchanged.
- Things in the project are referred to in place; a pasted picture is moved.
- A message over 500 files, 500 folders, or 200 MB, or with an attachment that cannot be put in the project, fails
  naming the attachment, and nothing is left in the project.
- Only the attachments folder is added to the project; unsafe names become safe names.

#### Test Scenarios

**AttachmentStore_SavePastedImage_Png_SavesInPrivateDraftFolderWithPasteName**: A PNG picture is saved as
`paste-20261120-093015-1.png` in `.canvaspilot/attachments` with exactly its bytes. This scenario is tested by
`AttachmentStore_SavePastedImage_Png_SavesInPrivateDraftFolderWithPasteName`.

**AttachmentStore_SavePastedImage_TwoInSameSecond_UseDifferentFiles**: Two pictures pasted in the same second get the
numbers 1 and 2 and two files. This scenario is tested by
`AttachmentStore_SavePastedImage_TwoInSameSecond_UseDifferentFiles`.

**AttachmentStore_SavePastedImage_NotAPngOrTooLarge_ThrowsArgumentExceptionAndWritesNothing**: Empty data, text that is
not a picture, and a picture one byte over 50 MB are refused, and the draft folder is not created. This scenario is
tested by `AttachmentStore_SavePastedImage_NotAPngOrTooLarge_ThrowsArgumentExceptionAndWritesNothing`.

**AttachmentStore_SavePastedImage_Name_MatchesDocumentedPattern**: With the real clock the name matches
`paste-{date}-{time}-{number}.png`, which is the pattern that leftovers are removed by. This scenario is tested by
`AttachmentStore_SavePastedImage_Name_MatchesDocumentedPattern`.

**AttachmentStore_RemoveLeftovers_UnreferencedPastedPictures_AreDeletedAndOthersStay**: Of three saved pictures, two
that no chip refers to are deleted; the third, a text file, and a folder with a similar name stay. This scenario is
tested by `AttachmentStore_RemoveLeftovers_UnreferencedPastedPictures_AreDeletedAndOthersStay`.

**AttachmentStore_RemoveLeftovers_NoDraftFolder_ReturnsZero**: Without a draft folder nothing is removed and none is
created. This scenario is tested by `AttachmentStore_RemoveLeftovers_NoDraftFolder_ReturnsZero`.

**AttachmentStore_StageForSend_ExternalFile_IsCopiedIntoAttachmentsFolder**: A picture outside the project is copied to
`attachments/photo.png`; the result tells the path, kind, name, size, file count, and that it was copied, and the
original is unchanged. This scenario is tested by
`AttachmentStore_StageForSend_ExternalFile_IsCopiedIntoAttachmentsFolder`.

**AttachmentStore_StageForSend_NameTaken_AddsNumberAndNeverOverwrites**: With `photo.png` already in `attachments`, two
more files of that name from different folders become `photo (2).png` and `photo (3).png` and the existing file is
unchanged. This scenario is tested by `AttachmentStore_StageForSend_NameTaken_AddsNumberAndNeverOverwrites`.

**AttachmentStore_StageForSend_SameFileTwice_IsCopiedOnce**: Two chips for one file give two attachments that refer to
one copy. This scenario is tested by `AttachmentStore_StageForSend_SameFileTwice_IsCopiedOnce`.

**AttachmentStore_StageForSend_Folder_IsCopiedRecursively**: A folder with a file, nested folders with a file, and an
empty folder is copied with the same tree and bytes; the result counts two files and five bytes. This scenario is tested
by `AttachmentStore_StageForSend_Folder_IsCopiedRecursively`.

**AttachmentStore_StageForSend_FolderWithLink_SkipsTheLinkAndCopiesTheRest**: A folder that holds a file and a link to a
folder with a secret file is copied without the link; one link is counted as skipped and the secret was not copied. This
scenario is tested by `AttachmentStore_StageForSend_FolderWithLink_SkipsTheLinkAndCopiesTheRest`.

**AttachmentStore_StageForSend_ItemsInsideProject_AreReferencedInPlace**: A file and a folder inside the project are
referred to by their project-relative paths, are not copied, and no attachments folder is created. This scenario is
tested by `AttachmentStore_StageForSend_ItemsInsideProject_AreReferencedInPlace`.

**AttachmentStore_StageForSend_PastedPicture_IsMovedIntoAttachments**: A pasted picture ends up in `attachments` under
its name and is gone from the draft folder. This scenario is tested by
`AttachmentStore_StageForSend_PastedPicture_IsMovedIntoAttachments`.

**AttachmentStore_StageForSend_NoChips_SucceedsAndCreatesNothing**: No chips give success and no folder. This scenario
is tested by `AttachmentStore_StageForSend_NoChips_SucceedsAndCreatesNothing`.

**AttachmentStore_StageForSend_TooManyFiles_FailsNamingChipAndCopiesNothing**: A small file and a folder with 501 files:
the failure names the folder and the limit of 500, and the small file was not copied either. This scenario is tested by
`AttachmentStore_StageForSend_TooManyFiles_FailsNamingChipAndCopiesNothing`.

**AttachmentStore_StageForSend_FileOverSizeLimit_FailsNamingChipAndCopiesNothing**: A file one byte over 200 MB fails
naming the file and the limit. This scenario is tested by
`AttachmentStore_StageForSend_FileOverSizeLimit_FailsNamingChipAndCopiesNothing`.

**AttachmentStore_StageForSend_TooManyFolders_Fails**: A folder with 501 folders fails with the folder limit. This
scenario is tested by `AttachmentStore_StageForSend_TooManyFolders_Fails`.

**AttachmentStore_StageForSend_FileCannotBeRead_RemovesWhatWasCopiedAndNamesChip**: Of two files, the second is held
open exclusively; the failure names it, and the copy of the first and the attachments folder that the call created are
removed. This scenario is tested by `AttachmentStore_StageForSend_FileCannotBeRead_RemovesWhatWasCopiedAndNamesChip`.

**AttachmentStore_StageForSend_ManyKinds_OnlyAttachmentsFolderIsAddedToProject**: A file whose folder name starts with
two dots, a folder, and a picture are copied; everything new is below `attachments` and every source has its bytes. This
scenario is tested by `AttachmentStore_StageForSend_ManyKinds_OnlyAttachmentsFolderIsAddedToProject`.

**AttachmentStore_StageForSend_ItemsThatCannotBeAttached_FailWithReasonNamingChip**: A path with `..`, a file and a
folder that do not exist, a folder given for a file, a folder that holds the project, the project folder, a file in the
private data folder, and a picture that claims to be pasted are each refused with a reason that names the chip, and
nothing is created. This scenario is tested by
`AttachmentStore_StageForSend_ItemsThatCannotBeAttached_FailWithReasonNamingChip`.

**AttachmentNames_SanitizeName_UnsafeNames_BecomeSafeNames**: Names with characters a name cannot hold, device names
with and without extension, trailing dots and spaces, an empty name, dots only, and a name that starts with two dots
become the safe names that are expected. This scenario is tested by
`AttachmentNames_SanitizeName_UnsafeNames_BecomeSafeNames`.

**AttachmentNames_SanitizeName_LongName_IsShortenedKeepingExtension**: A name of 300 characters is shortened to 100 and
keeps its extension. This scenario is tested by `AttachmentNames_SanitizeName_LongName_IsShortenedKeepingExtension`.

**AttachmentNames_UniqueName_NamesInASet_GetNumbers**: Equal names, also in another case, get numbers before the
extension. This scenario is tested by `AttachmentNames_UniqueName_NamesInASet_GetNumbers`.

**AttachmentStore_New_InvalidArguments_Throw**: A relative or blank project folder, a missing chip list, a missing keep
list, and a null chip are refused. This scenario is tested by `AttachmentStore_New_InvalidArguments_Throw`.

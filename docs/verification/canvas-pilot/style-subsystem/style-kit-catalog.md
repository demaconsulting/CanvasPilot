### StyleKitCatalog

#### Verification Approach

`StyleKitCatalog` is verified with unit tests in `StyleKitCatalogTests.cs` on temporary folders that stand for the five
kinds of place (the program, the user, the project, and two shared folders), with the real settings service (so that a
change of the settings is a real event) and a project service that is a substitute whose open project the test sets. A
shared folder that hangs, is refused, or fails in an unexpected way is imitated with the function that lists the kit
folders, which the internal constructor takes, so no test needs a network share and no test waits for a real timeout:
the time limit of a test is a few hundred milliseconds, and the blocked scan is released at the end of the test. The
tests do not measure time; they check that the refresh finishes while the scan is still blocked.

#### Test Environment

N/A - standard test environment with temporary local folders.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Kits are found in every place with the place, the folder, the read-only flag, and the shared folder; the entries are
  in the order of the places and then by name; the places are listed in the order of precedence; there is no place for
  the project while none is open; and kits that are not found before the first refresh are not listed.
- For every pair of places that hold the same id, the more local kit is the one that is found, and the other is listed
  as overridden with the name of the place; in the chain of five copies the order of the precedence is project, user,
  the shared folders in the order of the settings, and the program; deleting the winner gives the next place.
- A shared folder that is missing, refused, failing, or silent is a place that is not available with the reason, and the
  other places and their kits are still found; the refresh finishes without exception.
- A kit with errors, a second folder with a repeated id, and a folder with no style file are not kits; the first two are
  listed as broken with their reasons, and the last, and a hidden folder, are not mentioned.
- A change of the open project or of the shared folders refreshes the catalog, and a change of another setting does not;
  one refresh raises one change event; a cancelled refresh changes nothing; more than the limit of kit folders in a
  place is read up to the limit with a note.
- The kits of the program, as copied beside the test assembly, load with no issue, have the colors and sizes of the
  design, have text that can be read on the background and surface, have three examples that are drawn without a note
  and that carry the theme and chart configuration of their kit, and are found by a catalog in its default place; the
  same files are in the published folder and the installer package, which the build asserts.
- The kits of the program and the shared kits cannot be saved, created, duplicated into, or deleted, and their folders
  stay.
- A saved kit is in the folder named as its id with its examples kept and is returned as an entry; a kit with an error
  and a project place with no project are refused; a new kit has a valid palette and an id that no known kit has; a
  duplicate has a new id and copies of the examples with the same bytes and captions; a copy into the project keeps the
  id and is refused when the project has it; a failed copy leaves nothing; a delete removes only the folder of a kit
  that is directly in its place.

#### Test Scenarios

**StyleKitCatalog_RefreshAsync_KitsInEveryLocation_AreFoundWithTheirPlaces**: Two kits of the program with names that
sort the other way than their ids, a kit of the user, two kits in two shared folders, and a kit of the open project are
read. The ids are in the order project, user, shared, shared, program (by name), the places and read-only flags are as
expected, the shared folder is given for a shared kit only, and the five places are listed in the order of precedence
with their folders and all available. This scenario is tested by
`StyleKitCatalog_RefreshAsync_KitsInEveryLocation_AreFoundWithTheirPlaces`.

**StyleKitCatalog_Resolve_SameIdInTwoLocations_TheMoreLocalWins**: The same id is written in two places, for each of the
six pairs of kinds of place. The id finds the kit of the more local place, which is not overridden, and the other kit is
listed as overridden with the name of the more local place (`This project`, `My kits`, or `Shared folder`). This
scenario is tested by `StyleKitCatalog_Resolve_SameIdInTwoLocations_TheMoreLocalWins`.

**StyleKitCatalog_Resolve_SameIdEverywhere_ChainFollowsThePrecedence**: One id is in the project, the folder of the
user, two shared folders, and the program. The five entries are in that order, only the first is not overridden, and the
others name the project; after the kit of the project is deleted the kit of the user is found and the first shared kit
names the user's place. This scenario is tested by `StyleKitCatalog_Resolve_SameIdEverywhere_ChainFollowsThePrecedence`.

**StyleKitCatalog_Resolve_UnknownIdOrNull_FindsNothingOrIsRefused**: Nothing is found before the first refresh;
afterwards the id of a kit finds it, an unknown id and the id in another case find nothing, and a missing id is refused.
This scenario is tested by `StyleKitCatalog_Resolve_UnknownIdOrNull_FindsNothingOrIsRefused`.

**StyleKitCatalog_RefreshAsync_MissingSharedFolder_IsReportedAndOthersAreStillRead**: A shared folder that does not
exist is not available with the sentence that it was not found or cannot be reached, and the kits of the other shared
folder and of the user are found. This scenario is tested by
`StyleKitCatalog_RefreshAsync_MissingSharedFolder_IsReportedAndOthersAreStillRead`.

**StyleKitCatalog_RefreshAsync_SharedFolderThatHangs_TimesOutAndOthersAreStillRead**: The listing of the first shared
folder blocks. The refresh finishes while it is blocked, the folder is not available with the sentence that it did not
answer, and the kits of the second shared folder and of the user are found. The block is released at the end. This
scenario is tested by `StyleKitCatalog_RefreshAsync_SharedFolderThatHangs_TimesOutAndOthersAreStillRead`.

**StyleKitCatalog_RefreshAsync_SharedFolderThatIsRefused_IsNotAvailableWithTheReason**: The listing of one shared folder
fails with access denied and of another with an unexpected exception. Both places are not available, the first with the
reason of the exception, and the refresh does not fail. This scenario is tested by
`StyleKitCatalog_RefreshAsync_SharedFolderThatIsRefused_IsNotAvailableWithTheReason`.

**StyleKitCatalog_RefreshAsync_BrokenFolders_AreIsolatedAndListed**: The folder of the user holds a good kit, a kit with
a color that is a name, a folder with no style file, a hidden folder with a broken file, and a copy of the good kit
under another folder name. Only the good kit is an entry; the bad kit is listed as broken with the message about its
color and the copy with the message that its id is also used by another folder. This scenario is tested by
`StyleKitCatalog_RefreshAsync_BrokenFolders_AreIsolatedAndListed`.

**StyleKitCatalog_RefreshAsync_BrokenKit_IsLogged**: A kit with a style file that is not JSON gives a warning in the log
that says it cannot be used. This scenario is tested by `StyleKitCatalog_RefreshAsync_BrokenKit_IsLogged`.

**StyleKitCatalog_RefreshAsync_FolderNamedDifferentlyThanTheId_IsReadWithWarning**: A kit in a folder with another name
is read with the id of its file, the folder, and the warning that the folder name differs. This scenario is tested by
`StyleKitCatalog_RefreshAsync_FolderNamedDifferentlyThanTheId_IsReadWithWarning`.

**StyleKitCatalog_RefreshAsync_MissingLocalFolders_AreEmptyOrReportedAndNoProjectMeansNoProjectPlace**: With nothing
written and the folder of the program removed, there are no kits, the places are the user (available) and the program
(not available, with the sentence that it was not found beside the program), and no place for a project. When a project
without a styles folder is opened the place of the project is listed first and is available. This scenario is tested by
`StyleKitCatalog_RefreshAsync_MissingLocalFolders_AreEmptyOrReportedAndNoProjectMeansNoProjectPlace`.

**StyleKitCatalog_ProjectChanged_RefreshesTheKitsOfTheProject**: A project that has a kit is opened after the first
refresh; the kit appears after the background refresh and is gone when the project is closed, with one change event for
each. This scenario is tested by `StyleKitCatalog_ProjectChanged_RefreshesTheKitsOfTheProject`.

**StyleKitCatalog_SettingsChanged_OnlyAChangeOfSharedFoldersRefreshes**: A change of the model name raises no change
event; listing a shared folder raises one and its kit is found. This scenario is tested by
`StyleKitCatalog_SettingsChanged_OnlyAChangeOfSharedFoldersRefreshes`.

**StyleKitCatalog_RefreshAsync_RaisesChangedOnceAndCancelledRefreshChangesNothing**: One refresh raises one change
event; a refresh with a token that is cancelled throws the cancellation exception and the entries are the same object as
before. This scenario is tested by `StyleKitCatalog_RefreshAsync_RaisesChangedOnceAndCancelledRefreshChangesNothing`.

**StyleKitCatalog_RefreshAsync_MoreKitFoldersThanTheLimit_ReadsTheLimitAndSaysSo**: One more folder than the limit of
200 is read up to 200 kits, and the place says that only the first 200 are read. This scenario is tested by
`StyleKitCatalog_RefreshAsync_MoreKitFoldersThanTheLimit_ReadsTheLimitAndSaysSo`.

**StyleKitCatalog_ReadOnlyPlaces_RefuseEveryChange**: For the places of the program and of the shared folders, save,
create, and duplicate into are refused, and delete of a kit of the program and of a shared kit is refused; both entries
are read only and their folders still exist. This scenario is tested by
`StyleKitCatalog_ReadOnlyPlaces_RefuseEveryChange`.

**StyleKitCatalog_Save_UserKit_WritesFolderKeepsExamplesAndRefusesInvalidKit**: A kit of the user with an example file
is saved with a new description. The returned entry and the file have the change, the example is still there, the folder
holds only the style file and the examples folder, one change event was raised, and a kit with an invalid id and a
missing kit are refused. This scenario is tested by
`StyleKitCatalog_Save_UserKit_WritesFolderKeepsExamplesAndRefusesInvalidKit`.

**StyleKitCatalog_Save_ProjectKit_WritesIntoTheStylesFolderOfTheProjectAndNeedsAProject**: With no project, saving and
creating for the project are refused; with the project open a saved kit is in the styles folder of the project and is an
entry of the place of the project that is not read only. This scenario is tested by
`StyleKitCatalog_Save_ProjectKit_WritesIntoTheStylesFolderOfTheProjectAndNeedsAProject`.

**StyleKitCatalog_CreateBlank_Names_GetUniqueIdsAndAValidKit**: With a kit `clean` in the program, new kits named
`New kit` twice, `Clean`, a blank name, and symbols get the ids `new-kit`, `new-kit-2`, `clean-2`, `new-kit-3`, and
`new-kit-4`, have valid palettes with no error from the check, and do not hide the kit of the program. This scenario is
tested by `StyleKitCatalog_CreateBlank_Names_GetUniqueIdsAndAValidKit`.

**StyleKitCatalog_IdBaseFromName_FollowsTheRules**: Names are turned into ids: spaces and punctuation become single
dashes, the ends are trimmed, letters that are not ASCII are dropped, a name with no letter or digit gives `new-kit`,
and a long name is cut to 40 characters without a trailing dash; every result is a valid id. This scenario is tested by
`StyleKitCatalog_IdBaseFromName_FollowsTheRules`.

**StyleKitCatalog_Duplicate_KitWithExamples_CopiesEverythingUnderANewId**: A kit of the program with two example files
and a caption is duplicated to the user and to the project under the same new name. The ids are `my-clean` and
`my-clean-2`, the name, description, and palette are those of the original with the new name, the examples have the same
names, captions, and bytes with full paths in the new folder, the folders hold nothing else, and the original folder and
style file are unchanged. This scenario is tested by
`StyleKitCatalog_Duplicate_KitWithExamples_CopiesEverythingUnderANewId`.

**StyleKitCatalog_Duplicate_ExampleFileMissing_LeavesNothingBehind**: An example that was removed after the kit was read
makes the duplicate fail with an I/O exception, and the folder of the user is empty, with no hidden folder either. This
scenario is tested by `StyleKitCatalog_Duplicate_ExampleFileMissing_LeavesNothingBehind`.

**StyleKitCatalog_Delete_UserAndProjectKits_RemoveFolderAndRefuseOtherFolders**: Entries that point at a folder beside
the place, at a folder reached with `..`, at the folder of the place itself, or that claim the place of the project are
refused and nothing is removed; the real entries of a kit of the user and of the project are removed with their folders,
the list is empty, and the folder beside them is intact. This scenario is tested by
`StyleKitCatalog_Delete_UserAndProjectKits_RemoveFolderAndRefuseOtherFolders`.

**StyleKitCatalog_CopyIntoProject_SharedKit_FreezesItInTheProject**: Copying a shared kit with no project open is
refused; with the project open the copy is in the styles folder of the project with its id and example, it is the kit
that is found for the id, the shared kit is overridden, and a second copy is refused. This scenario is tested by
`StyleKitCatalog_CopyIntoProject_SharedKit_FreezesItInTheProject`.

**BuiltInStyleKits_ReadFolder_EachKit_LoadsWithNoErrorAndNoWarning**: Each kit folder beside the test assembly (`clean`,
`slides-dark`, and `dema`) is read with no issue at all, has the id and the name of the design, the `base` Mermaid
theme, a Vega-Lite configuration, notes, three examples with captions that are files, and the example names of the
design. This scenario is tested by `BuiltInStyleKits_ReadFolder_EachKit_LoadsWithNoErrorAndNoWarning`.

**BuiltInStyleKits_ExampleFiles_AreDrawnWithoutErrorOrNote**: The nine example files are drawn by the real render
service with the folder of their kit as the only folder they may read. Each draw succeeds, has no note, and is larger
than 100 by 50 pixels. This scenario is tested by `BuiltInStyleKits_ExampleFiles_AreDrawnWithoutErrorOrNote`.

**BuiltInStyleKits_Palette_TextIsReadableOnBackgroundAndSurface**: In each kit the text color on the background, the
text color on the surface, and the muted color on the background each have a contrast of at least 4.5 to 1. This
scenario is tested by `BuiltInStyleKits_Palette_TextIsReadableOnBackgroundAndSurface`.

**BuiltInStyleKits_Values_AreTheOnesOfTheDesign**: The palettes of the three kits equal the colors of the design, in the
same order, the fonts are the generic families, the stroke, radius, and spacing are 2, 12, 16 for Clean and 3, 16, 24
for the two dark kits, and the size presets are those of the design. This scenario is tested by
`BuiltInStyleKits_Values_AreTheOnesOfTheDesign`.

**BuiltInStyleKits_Examples_CarryTheThemeAndChartConfigurationOfTheirKit**: The flowchart of each kit has the front
matter with the `base` theme and every theme variable of the kit with its value, and the chart of each kit has the same
color range and background in its configuration as the Vega-Lite part of the kit. This scenario is tested by
`BuiltInStyleKits_Examples_CarryTheThemeAndChartConfigurationOfTheirKit`.

**BuiltInStyleKits_OutputFolder_HoldsTheStyleFilesAndAllNineExamples**: The folder `StyleKits` beside the test assembly
has three style files and the nine example files, three in the `examples` folder of each kit. This is the evidence that
the content files reach the output folder; the published folder and the package are asserted by the build with
FileAssert (`StyleKits_BuiltInPublished`) and the file table of the package. This scenario is tested by
`BuiltInStyleKits_OutputFolder_HoldsTheStyleFilesAndAllNineExamples` and, for the published folder, by
`StyleKits_BuiltInPublished`.

**BuiltInStyleKits_Catalog_DefaultPlace_FindsTheThreeKitsAsReadOnlyAndTheUserCanOverrideThem**: A catalog that is made
without a folder for the kits of the program finds the three kits in the folder beside the program, all of the built-in
place and read only; after a kit of the user with the id `clean` is written the kit of the user is the one that is
found. This scenario is tested by
`BuiltInStyleKits_Catalog_DefaultPlace_FindsTheThreeKitsAsReadOnlyAndTheUserCanOverrideThem`.

**StyleKitCatalog_Constructor_NullServices_ThrowArgumentNullException**: A missing project service or settings service
is refused. This scenario is tested by `StyleKitCatalog_Constructor_NullServices_ThrowArgumentNullException`.

### RecentProjects

#### Verification Approach

`RecentProjects` is verified by unit tests in `RecentProjectsTests.cs`. Most tests use a real `SettingsService` on a
temporary folder and real project files, so they check the stored list and what a new service loads. Two tests use an
NSubstitute `ISettingsService` whose `Update` throws, to force a failed save. The test of the time of opening
(`RecentProjects_Add_RecordsTheTimeOfOpeningAndRemoveForgetsIt`, in `WelcomeTests.cs`) gives the list a hand-written
clock (`TimeProvider`) so the recorded times can be checked exactly. The tests assert the returned lists, the stored
settings, and the `Changed` event. Two tests of `RememberShownPicture` check decisions of its design rather than a
requirement: that the picture already remembered costs no write and no `Changed` event, and that a failed save is a
`false` result (see the design of `RecentProjects`).

#### Test Environment

Standard test environment. Tests use temporary local folders and real file I/O; no network, external services,
or display are required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Added projects are listed most recent first as full paths, a project added again moves to the top, paths that
  differ only in case are one entry, and only the newest ten are kept.
- Empty and invalid paths are ignored and reported as not stored.
- Missing files are not returned and are dropped from the stored list with one change event; nothing missing
  means no event.
- The list is stored in the settings file next to other settings, and a failed save is reported without an
  exception or event.
- Each opening records its time from the clock that the test sets, a second opening renews it, and removing the project
  forgets it.

#### Test Scenarios

**RecentProjects_GetRecent_NothingAdded_IsEmpty**: A fresh settings file gives an empty list. This scenario is
tested by `RecentProjects_GetRecent_NothingAdded_IsEmpty`.

**RecentProjects_Add_ThreeProjects_ListsMostRecentFirst**: Three projects added in order are listed newest
first. This scenario is tested by `RecentProjects_Add_ThreeProjects_ListsMostRecentFirst`.

**RecentProjects_Add_ExistingProject_MovesToTopWithoutDuplicate**: Adding the first project again puts it on top
once. This scenario is tested by `RecentProjects_Add_ExistingProject_MovesToTopWithoutDuplicate`.

**RecentProjects_Add_PathDifferingInCase_ReplacesEarlierEntry**: The same path in upper case replaces the
earlier entry. This scenario is tested by `RecentProjects_Add_PathDifferingInCase_ReplacesEarlierEntry`.

**RecentProjects_Add_RelativePath_StoresFullPath**: A relative path is stored as a full path. This scenario is
tested by `RecentProjects_Add_RelativePath_StoresFullPath`.

**RecentProjects_Add_MoreThanTen_KeepsNewestTen**: Twelve projects leave the newest ten, newest first. This
scenario is tested by `RecentProjects_Add_MoreThanTen_KeepsNewestTen`.

**RecentProjects_GetRecent_MissingFile_IsDroppedAndStoredListPruned**: A deleted project file is not returned,
is removed from the stored list, and one change event is raised. This scenario is tested by
`RecentProjects_GetRecent_MissingFile_IsDroppedAndStoredListPruned`.

**RecentProjects_GetRecent_NothingMissing_DoesNotAnnounceChange**: Reading a list with no missing file twice
raises no event. This scenario is tested by `RecentProjects_GetRecent_NothingMissing_DoesNotAnnounceChange`.

**RecentProjects_Remove_ListedProject_IsRemoved**: Removing a listed project, spelled in another case, leaves
the other project. This scenario is tested by `RecentProjects_Remove_ListedProject_IsRemoved`.

**RecentProjects_Clear_ListedProjects_EmptiesList**: Clearing empties the list. This scenario is tested by
`RecentProjects_Clear_ListedProjects_EmptiesList`.

**RecentProjects_Add_Project_PersistsInSettingsAndKeepsOtherSettings**: After an add, a restarted settings
service returns the project and still holds another setting. This scenario is tested by
`RecentProjects_Add_Project_PersistsInSettingsAndKeepsOtherSettings`.

**RecentProjects_Add_EmptyOrInvalidPath_IsIgnored**: Empty, blank, and invalid paths are not stored. This
scenario is tested by `RecentProjects_Add_EmptyOrInvalidPath_IsIgnored`.

**RecentProjects_Add_SettingsWriteFails_ReturnsFalseAndDoesNotAnnounce**: A settings service that throws
`IOException` makes `Add` return `false` without an exception or event. This scenario is tested by
`RecentProjects_Add_SettingsWriteFails_ReturnsFalseAndDoesNotAnnounce`.

**RecentProjects_Constructor_NullSettings_Throws**: A null settings service is refused. This scenario is
tested by `RecentProjects_Constructor_NullSettings_Throws`.

**RecentProjects_Add_RecordsTheTimeOfOpeningAndRemoveForgetsIt**: With a real settings service and a clock that the test
sets, adding a project records the time of opening, adding it again three hours later renews the time, and removing the
project leaves no time in the settings. This scenario is tested by
`RecentProjects_Add_RecordsTheTimeOfOpeningAndRemoveForgetsIt`, which is in `WelcomeTests.cs` because the welcome screen
is its reason.

**RecentProjects_RememberShownPicture_StoredPerProjectAndReadBack**: Two listed projects each remember a picture (the
second twice); a list over the same settings after a restart gives back the newest picture of each, the project path is
matched ignoring case, and `Changed` is not raised. This scenario is tested by
`RecentProjects_RememberShownPicture_StoredPerProjectAndReadBack`.

**RecentProjects_RememberShownPicture_UnlistedProjectOrPathOutsideTheFolder_IsRefused**: A picture of a project that is
not listed, an empty or blank path, a rooted path, a path with a `..` part, and an empty project path are each refused,
and nothing is stored. This scenario is tested by
`RecentProjects_RememberShownPicture_UnlistedProjectOrPathOutsideTheFolder_IsRefused`.

**RecentProjects_RememberShownPicture_SamePicture_WritesNothingAndDoesNotAnnounce**: Remembering the picture that is
already remembered succeeds without a write of the settings and without `Changed`. This scenario is tested by
`RecentProjects_RememberShownPicture_SamePicture_WritesNothingAndDoesNotAnnounce`.

**RecentProjects_RememberShownPicture_SettingsWriteFails_ReturnsFalse**: A settings service whose update throws an
`IOException`; remembering a picture returns `false` and raises nothing. This scenario is tested by
`RecentProjects_RememberShownPicture_SettingsWriteFails_ReturnsFalse`.

**RecentProjects_Remove_ForgetsTheShownPicture**: A project with a picture is removed and added again; it has no
picture. This scenario is tested by `RecentProjects_Remove_ForgetsTheShownPicture`.

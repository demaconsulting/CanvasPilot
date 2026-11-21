### RecentProjects

#### Verification Approach

`RecentProjects` is verified by unit tests in `RecentProjectsTests.cs`. Most tests use a real `SettingsService` on
a temporary folder and real project files, so they check the stored list and what a new service loads. One test
uses an NSubstitute `ISettingsService` whose `Update` throws, to force a failed save. The tests assert the
returned lists, the stored settings, and the `Changed` event.

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

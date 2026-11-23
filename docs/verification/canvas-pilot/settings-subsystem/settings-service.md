### SettingsService

#### Verification Approach

`SettingsService` is verified with unit tests defined in `SettingsServiceTests.cs`. The tests use the real file
system in a temporary folder created per test and removed on disposal, and a real `RollingFileLogger` writing
into a second temporary folder to verify what is logged. `PathHelpers` is used as the real collaborator, so no
mocking or test doubles are required. A failed write is produced deterministically by placing a directory at the
settings file path, so the final replace cannot succeed on any platform.

#### Test Environment

N/A - standard test environment. Tests use temporary local folders and real file I/O; no network or external
services are required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every setting survives a save and a load by a fresh service, and the written JSON has the documented
  camel-case names, version, and value shapes.
- The layout of the window, the theme choice, the options of the explorer, and the times of opening survive a save and a
  load; a file of an earlier build gets the default layout (1440 by 900, columns of 264 and 400) and look (follow the
  system, thumbnails on, compact rows); sizes and widths outside the bounds are clamped, a place beyond any screen is
  forgotten, and unknown states and themes fall back; only listed projects keep a time.
- A missing file, missing properties, unknown properties, out-of-range values, and unrecognized enumeration
  values never fail a load.
- A file of an earlier build that stores the reasoning as `reasoningLevel` loads with the level `low`, `medium`, or
  `high` (in any letter case) as the reasoning effort and `auto` or an unusable value as none, keeps its version, loses
  to an effort that the file states, and is written back with `reasoningEffort` and without the old key.
- A corrupt or empty file is renamed `settings.json.bad`, defaults are used, and an error is logged; a newer
  file is read and a warning is logged.
- A save replaces the file without leaving a temporary file, and a failed save throws `IOException`, leaves the
  settings in effect unchanged, and raises no change event.
- The talk key of the speech settings is empty when the file has none (F9 is then in effect), is read from a file and
  written in its normal form (`Ctrl+Shift+Space`) and survives a save and a load, and is the empty string, the default,
  when the file holds a key that is not safe or the old default `F8`; the version of the file stays 1.
- `Changed` is raised once for each load, save, or update, with the settings in effect.

#### Test Scenarios

**SettingsService_DefaultSettingsFolder_Default_IsCanvasPilotUnderApplicationData**: The default folder is
read; it equals the `CanvasPilot` folder beneath the platform application data folder. This scenario is tested
by `SettingsService_DefaultSettingsFolder_Default_IsCanvasPilotUnderApplicationData`.

**SettingsService_SettingsFilePath_Folder_IsSettingsJsonInFolder**: A service is created for a folder; its file
path is `settings.json` in that folder. This scenario is tested by
`SettingsService_SettingsFilePath_Folder_IsSettingsJsonInFolder`.

**SettingsService_Save_MissingFolder_CreatesFolderAndFile**: Default settings are saved to a folder that does
not exist; the folder and file are created. This scenario is tested by
`SettingsService_Save_MissingFolder_CreatesFolderAndFile`.

**SettingsService_Constructor_WhitespaceFolder_ThrowsArgumentException**: A service is created with a
whitespace folder; `ArgumentException` is thrown. This scenario is tested by
`SettingsService_Constructor_WhitespaceFolder_ThrowsArgumentException`.

**SettingsService_SaveLoad_AllSettings_RoundTrips**: Settings with a non-default value in every field, including
a model parameter bag, are saved and loaded by a fresh service; every value is equal. This scenario is tested by
`SettingsService_SaveLoad_AllSettings_RoundTrips`.

**SettingsService_Save_Settings_WritesVersionedCamelCaseJson**: Full settings are saved and the file is parsed
as JSON; `version` is 1 and the recent projects, AI provider, and speech sections contain the documented
camel-case properties with the expected values, including the opt-in and the invariant string parameter. This
scenario is tested by `SettingsService_Save_Settings_WritesVersionedCamelCaseJson`.

**SettingsService_Load_MissingFile_ReturnsDefaults**: A folder has no settings file; load returns default
settings in which unrestricted agent tools are off and the context window is 32768, and the result is the
settings in effect. This scenario is tested by `SettingsService_Load_MissingFile_ReturnsDefaults`.

**SettingsService_Load_MissingProperties_UsesDefaults**: A file sets only a model name; load returns that name
and the default context window, endpoint, and speech values. This scenario is tested by
`SettingsService_Load_MissingProperties_UsesDefaults`.

**SettingsService_DefaultContextWindow_Constants_AreThe32768DefaultAndThe16384Minimum**: The default context window
constant is 32768 and the recommended minimum constant is 16384; the default is not below the minimum and the minimum is
a value that settings accept. This scenario is tested by
`SettingsService_DefaultContextWindow_Constants_AreThe32768DefaultAndThe16384Minimum`.

**SettingsService_Load_NoContextWindowInFile_Uses32768**: A file has an AI provider section without a context window;
load returns 32768 tokens. This scenario is tested by `SettingsService_Load_NoContextWindowInFile_Uses32768`.

**SettingsService_Load_StoredContextWindow8192_IsKept**: A file stores a context window of 8192, the default of an
earlier build; load returns 8192, and the value is still 8192 after the settings are saved and loaded again by a fresh
service. This scenario is tested by `SettingsService_Load_StoredContextWindow8192_IsKept`.

**SettingsService_Load_UnknownProperties_AreIgnored**: A file has extra properties at several levels; load
returns the known values. This scenario is tested by `SettingsService_Load_UnknownProperties_AreIgnored`.

**SettingsService_Load_CorruptFile_RenamesToBadAndUsesDefaults**: A file that is not valid JSON is loaded;
defaults are used, `settings.json` no longer exists, and `settings.json.bad` holds the original text. This
scenario is tested by `SettingsService_Load_CorruptFile_RenamesToBadAndUsesDefaults`.

**SettingsService_Load_CorruptFile_LogsError**: A corrupt file is loaded by a service with a logger; the log
contains an error entry that mentions the corruption and the `.bad` file name. This scenario is tested by
`SettingsService_Load_CorruptFile_LogsError`.

**SettingsService_Load_EmptyFile_TreatedAsCorrupt**: An empty file is loaded; defaults are used and the file is
moved to `settings.json.bad`. This scenario is tested by `SettingsService_Load_EmptyFile_TreatedAsCorrupt`.

**SettingsService_Load_NewerVersion_LoadsKnownValuesAndLogsWarning**: A file with version 99, a known value, and
unknown content is loaded; the known value is returned, the file stays in place, and a warning that mentions
the version is logged. This scenario is tested by
`SettingsService_Load_NewerVersion_LoadsKnownValuesAndLogsWarning`.

**SettingsService_Load_OutOfRangeValues_ReplacedWithDefaults**: A file has a context window of 5, an endpoint
that is not a URL, a null model name, a null speech section, and blank recent-project entries; load returns the
default context window and endpoint, an empty model name, default speech values, and only the non-blank recent
project. This scenario is tested by `SettingsService_Load_OutOfRangeValues_ReplacedWithDefaults`.

**SettingsService_Load_UnknownEnumValue_UsesFirstValue**: A file has an unknown provider name and a numeric
reasoning level of the old key; load returns the first provider and no reasoning effort. This scenario is tested by
`SettingsService_Load_UnknownEnumValue_UsesFirstValue`.

**SettingsService_Load_LegacyReasoningLevel_BecomesTheReasoningEffort**: A version 1 file of an earlier build holds
`reasoningLevel` of `high`, `Medium`, `low`, `auto`, or `extreme` (five cases) and a model name. Load gives the effort
`high`, `medium`, `low`, none, and none; the model name and the version 1 are kept; after a save of what was loaded the
file holds `reasoningEffort` and no `reasoningLevel`. This scenario is tested by
`SettingsService_Load_LegacyReasoningLevel_BecomesTheReasoningEffort`.

**SettingsService_Load_ReasoningEffortAndLegacyLevel_EffortWins**: A file holds both `reasoningEffort` (with spaces
around `xhigh`) and the old `reasoningLevel` of `low`; load gives the trimmed `xhigh`. This scenario is tested by
`SettingsService_Load_ReasoningEffortAndLegacyLevel_EffortWins`.

**SettingsService_SaveLoad_ReasoningEffort_RoundTripsAndDefaultsToEmpty**: A file without either key loads with no
effort; saving the effort `max` and loading it with a new service gives `max`. This scenario is tested by
`SettingsService_SaveLoad_ReasoningEffort_RoundTripsAndDefaultsToEmpty`.

**SettingsService_Save_ExistingFile_ReplacesContentAndLeavesNoTemporaryFile**: Settings are saved over an
existing file; a fresh service loads the new content and the folder holds only `settings.json`. This scenario
is tested by `SettingsService_Save_ExistingFile_ReplacesContentAndLeavesNoTemporaryFile`.

**SettingsService_Save_WriteFails_ThrowsIOExceptionAndKeepsCurrentSettings**: A directory occupies the settings
file name; saving throws an `IOException`, the settings in effect are unchanged, no change event is raised, and
no temporary file remains. This scenario is tested by
`SettingsService_Save_WriteFails_ThrowsIOExceptionAndKeepsCurrentSettings`.

**SettingsService_Save_Settings_RaisesChangedWithSavedSettings**: Settings are saved with a `Changed` handler
attached; the handler receives the saved settings, which are also the settings in effect. This scenario is
tested by `SettingsService_Save_Settings_RaisesChangedWithSavedSettings`.

**SettingsService_Update_Transform_SavesAndReturnsTransformedSettings**: A recent project is added through
`Update`; the transformed settings are returned and stored, built on the settings that were stored, and
`Changed` is raised once. This scenario is tested by
`SettingsService_Update_Transform_SavesAndReturnsTransformedSettings`.

**SettingsService_Load_ExistingFile_RaisesChanged**: An existing file is loaded with a `Changed` handler
attached; the handler receives the loaded settings. This scenario is tested by
`SettingsService_Load_ExistingFile_RaisesChanged`.

**SettingsService_SaveLoad_AllSettings_RoundTrips** and **SettingsService_Save_Settings_WritesVersionedCamelCaseJson**
also cover the style settings: two shared folders, one of them a network path, and a default kit are stored, read by a
fresh service, and written as `styles.sharedKitFolders` and `styles.defaultKitId`.

**SettingsService_Load_FileWithoutStyles_GivesNoSharedFoldersAndNoDefaultKit**: A file from before style kits, and a
file whose `styles` is `null`, give no shared folders and no default kit. This scenario is tested by
`SettingsService_Load_FileWithoutStyles_GivesNoSharedFoldersAndNoDefaultKit`.

**SettingsService_Load_SharedKitFolders_AreTrimmedFilteredAndUniqueInOrder**: A file lists a folder with white space
around it, a relative path, a blank entry, `null`, the first folder again in upper case, a folder that does not exist,
and a drive-relative path. The result is the first folder trimmed, then the folder that does not exist, and the log has
four lines about dropped entries. This scenario is tested by
`SettingsService_Load_SharedKitFolders_AreTrimmedFilteredAndUniqueInOrder`.

**SettingsService_Load_DefaultKitId_IsValidOrEmpty**: A valid id is kept, one with white space around it is trimmed, and
an id with an upper-case letter, a leading dash, a space, `..`, or no text is replaced with none. This scenario is
tested by `SettingsService_Load_DefaultKitId_IsValidOrEmpty`.

**SettingsService_Update_StyleSettings_KeepsOtherSettingsAndPersists**: An update that changes only the style settings
keeps the AI provider and speech settings, and a fresh service reads the new style settings. This scenario is tested by
`SettingsService_Update_StyleSettings_KeepsOtherSettingsAndPersists`.

**SettingsService_StyleSettings_IsValidKitId_FollowsTheRule**: Ids of 1 to 48 lower-case letters, digits, and dashes
that do not start with a dash are valid; 49 characters, none, empty text, a leading dash, upper case, an underscore, and
a line feed are not. This scenario is tested by `SettingsService_StyleSettings_IsValidKitId_FollowsTheRule`.

**SettingsService_SaveLoad_WindowAppearanceAndOpenedTimes_RoundTrips**: A layout with a place, a maximized state, and
custom column widths, a dark theme with no thumbnails and tall rows, and the time of a listed project are saved by one
service and loaded by another; every value is the same. This scenario is tested by
`SettingsService_SaveLoad_WindowAppearanceAndOpenedTimes_RoundTrips`.

**SettingsService_Load_FileOfEarlierBuild_GivesDefaultWindowAndAppearance**: A version 1 file with no layout, look, or
times loads with 1440 by 900, columns of 264 and 400, follow the system, thumbnails on, compact rows, and no times, and
keeps its recent project and model name. This scenario is tested by
`SettingsService_Load_FileOfEarlierBuild_GivesDefaultWindowAndAppearance`.

**SettingsService_Load_OutOfRangeWindowLayout_IsClampedAndUnknownChoicesFallBack**: A file with a width of 100, a height
of 99999, a place of 99999999, an unknown state, columns of 10 and 9999, and an unknown theme loads with the nearest
bounds, no left place, the normal state, and following the system; the other ends of the bounds are checked through an
update. This scenario is tested by `SettingsService_Load_OutOfRangeWindowLayout_IsClampedAndUnknownChoicesFallBack`.

**SettingsService_Load_RecentProjectOpened_KeepsOnlyListedProjects**: A file with times for a listed project, an
unlisted one, and a blank name loads with the time of the listed project only, and the second listed project has none.
This scenario is tested by `SettingsService_Load_RecentProjectOpened_KeepsOnlyListedProjects`.

**SettingsService_Save_WindowAndAppearance_WritesCamelCaseNames**: A saved light theme and maximized window are written
as `appearance.theme` of `Light`, `window.state` of `Maximized`, and the numbers and the empty `recentProjectOpened`
object under their camel-case names. This scenario is tested by
`SettingsService_Save_WindowAndAppearance_WritesCamelCaseNames`.

**SettingsService_Update_Appearance_KeepsOtherSettingsAndPersists**: The theme is changed by an update of full settings;
a fresh service loads the new theme and the other settings as they were. This scenario is tested by
`SettingsService_Update_Appearance_KeepsOtherSettingsAndPersists`.

**SettingsService_Load_PushToTalkKey_IsNormalizedAndUnsafeOrOldF8BecomesTheDefault**: Four files with version 1 are
loaded: one with no talk key, which gives the empty string (the default F9); one with `shift+ctrl+space`, which is read
as `Ctrl+Shift+Space`, and a save of it followed by a load by a fresh service keeps `Ctrl+Shift+Space`; one with the old
default `F8`, which gives the empty string; and one with `Escape`, which cannot be a talk key and gives the empty
string. This scenario is tested by `SettingsService_Load_PushToTalkKey_IsNormalizedAndUnsafeOrOldF8BecomesTheDefault`.

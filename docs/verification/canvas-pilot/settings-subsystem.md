## Settings

### Verification Approach

The `Settings` subsystem is verified by integration tests defined in `SettingsSubsystemTests.cs`. The tests
compose a real `SettingsService`, a real `SettingsDialogViewModel`, and a real
`AiProviderSettingsPageViewModel` on a temporary settings folder, so they verify that a choice made in the
dialog is merged, stored in the settings file, and restored by a new service as after a restart. No mocking is
required because the subsystem depends only on the local file system and on the `Utilities` and `Logging`
subsystems. The window itself is verified by the headless UI tests described in the unit verification
documents.

### Test Environment

N/A - standard test environment. Tests use temporary local settings folders and real file I/O; no network,
external services, or display are required.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- AI provider choices made in the dialog and accepted with OK are present when a new service loads the file,
  and a value that belongs to another subsystem in the same file is preserved.
- The layout of the window, the theme choice, and the time of opening of a recent project are present when a new service
  loads the file, also after the dialog stored its own choices.
- The theme and the explorer options chosen on the Appearance page and accepted with OK are present when a new service
  loads the file, and the choices of the AI provider page are unchanged.
- Choices made in a dialog that is cancelled are not stored.
- The window of the settings shows the pages in a rail with an icon and a title each, the title and the sentence of the
  chosen page above it, and a banner in words when saving fails; every page and entry has a name for assistive
  technology and every button can be reached with the keyboard.
- A corrupt settings file does not prevent the dialog from opening and saving; the corrupt file is kept aside
  as `settings.json.bad` and the new choices are stored.
- The talk key of the speech settings is stored as text, a file without one, with the old default F8, or with a key that
  is not safe gives the default (stored as nothing), and a safe key is kept in its normal form; this is verified by the
  unit test of `SettingsService` (see *SettingsService Verification*), because it needs no dialog.
- The page of the AI provider warns, with the cost of a message, that a high reasoning level on a large model of GitHub
  Copilot is slow and costly, and only then; this is verified by the unit and the headless UI tests of the page (see
  *AiProviderSettingsPage Verification*).

### Test Scenarios

**SettingsSubsystem_PersistSettings_DialogOk_SurvivesRestart**: A recent project is stored, a dialog over the
AI provider page is opened, Ollama is selected, a model name is typed, and the stored unrestricted agent tools value
is set; OK is chosen and a fresh service loads the file. The provider, model name, and stored value are restored
and the recent project is unchanged. This scenario is tested by
`SettingsSubsystem_PersistSettings_DialogOk_SurvivesRestart`.

**SettingsSubsystem_EditSettings_DialogCancel_LeavesStoredSettingsUnchanged**: Default settings are stored, a
model name is typed in the dialog, and Cancel is chosen; neither the settings in effect nor the stored file
holds the typed name. This scenario is tested by
`SettingsSubsystem_EditSettings_DialogCancel_LeavesStoredSettingsUnchanged`.

**SettingsSubsystem_ToleratePoorFiles_CorruptFile_DialogStillWorks**: A corrupt settings file is loaded at
startup, a dialog is opened, a model name is typed, and OK is chosen; a fresh service loads the typed name and
the corrupt file exists as `settings.json.bad`. This scenario is tested by
`SettingsSubsystem_ToleratePoorFiles_CorruptFile_DialogStillWorks`.

**SettingsSubsystem_KeepStyleSettings_SavedWithOthers_SurviveRestartAndOtherChanges**: Style settings with a shared
folder and a default kit are stored, a model name is typed in the dialog, and OK is chosen. A fresh service loads the
model name and the style settings as they were. This scenario is tested by
`SettingsSubsystem_KeepStyleSettings_SavedWithOthers_SurviveRestartAndOtherChanges`.

**SettingsSubsystem_Appearance_DialogOk_StoresThemeAndOptions**: A model name is stored, a dialog over the AI provider
page and the Appearance page is shown on the second page, the dark theme is chosen with no thumbnails, and OK is chosen.
A fresh service loads the dark theme, no thumbnails, compact rows, and the model name as it was. This scenario is tested
by `SettingsSubsystem_Appearance_DialogOk_StoresThemeAndOptions`.

**SettingsSubsystem_RememberLayoutAndAppearance_SavedWithOthers_SurviveRestartAndDialogChanges**: A layout, a pinned
theme with tall rows, a listed project and its time of opening are stored, a model name is typed in the dialog, and OK
is chosen. A fresh service loads the model name and the layout, the look, and the time as they were. This scenario is
tested by `SettingsSubsystem_RememberLayoutAndAppearance_SavedWithOthers_SurviveRestartAndDialogChanges`.

**SettingsDialogView_ChosenPage_ShowsHeadingIconsAndErrorBanner**: Headless UI test of the real dialog over the AI
provider page and a second page. The heading and the sentence follow the page that is chosen, each entry of the rail has
an icon, and a message of a failed save shows in the banner. This scenario is tested by
`SettingsDialogView_ChosenPage_ShowsHeadingIconsAndErrorBanner`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: Headless UI test of the whole application. The settings
dialog is shown with each of its pages in both theme variants and every control is looked at; entries have names, a
button that shows no text has a name and a tooltip, and every enabled button can take the keyboard focus. This scenario
is tested by `Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.

**SettingsSubsystem_AnnounceChosenEntries_PageLists_ReadAsTheirShownWords**: A dialog over the real service, the AI
provider page, and the Appearance page loads stored settings with the dark theme; the text of every provider, reasoning
level, and theme entry, and of the chosen ones, is the label that the list shows. This scenario is tested by
`SettingsSubsystem_AnnounceChosenEntries_PageLists_ReadAsTheirShownWords`.

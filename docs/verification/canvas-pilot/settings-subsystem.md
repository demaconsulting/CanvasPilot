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
- Choices made in a dialog that is cancelled are not stored.
- A corrupt settings file does not prevent the dialog from opening and saving; the corrupt file is kept aside
  as `settings.json.bad` and the new choices are stored.

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

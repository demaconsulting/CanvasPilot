### SpeechSettingsPage

#### Verification Approach

`SpeechSettingsPage` is verified at two levels. Unit tests in `SpeechSettingsPageTests.cs` drive
`SpeechSettingsPageViewModel` over substitute audio device and speech model services (NSubstitute) with real
`AppSettings` records; downloads that wait for cancellation show the cancellation on close. Headless UI tests in
`SpeechSettingsPageViewTests.cs` (the `UiTests` project, using Avalonia's headless platform and hand-written fakes)
show the real settings dialog with the AI Provider and Speech pages, choose the page, and close the window.
`MainWindowViewTests.cs` shows that the Settings menu item of the real main window lists both pages.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled, and use a temporary settings folder. No network, audio
hardware, model store, or display is required, and nothing is downloaded.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The page is titled Speech, is always valid, and does not use either service until loaded.
- Stored settings round-trip through load and apply; choices made by the user are applied by device name, model
  identifier, and parameter value; the other sections of the settings are unchanged; choices that the page could not
  show are kept.
- Choosing another model shows its parameters, edits to the first model are remembered, and a new load shows the
  stored values, not an earlier session's edits.
- Closing the page, or closing the dialog window by any route, cancels a running download.
- The settings dialog lists the Speech page after the AI Provider page, shows its content when chosen, and saves its
  choices with OK.

#### Test Scenarios

**SpeechSettingsPageViewModel_New_IsTitledValidAndDoesNotUseServices**: A page over unused substitute services has the
title `Speech` and is valid, and neither service was asked to list. This scenario is tested by
`SpeechSettingsPageViewModel_New_IsTitledValidAndDoesNotUseServices`.

**SpeechSettingsPageViewModel_LoadApply_StoredSettings_RoundTrips**: Settings that name devices, models, and the
parameter values of a model are loaded; the page shows them, and applying gives the same speech values. This scenario
is tested by `SpeechSettingsPageViewModel_LoadApply_StoredSettings_RoundTrips`.

**SpeechSettingsPageViewModel_Apply_UserChoices_AreStored**: The user picks devices and models and edits a number;
applying stores the device names, the model identifiers, and the number as the text `9`. This scenario is tested by
`SpeechSettingsPageViewModel_Apply_UserChoices_AreStored`.

**SpeechSettingsPageViewModel_Apply_OtherSettings_AreUnchanged**: Settings with a recent project and an AI model name
are applied; both are unchanged. This scenario is tested by
`SpeechSettingsPageViewModel_Apply_OtherSettings_AreUnchanged`.

**SpeechSettingsPageViewModel_SelectModel_ShowsItsParametersAndRemembersEdits**: A number is edited for one
text-to-speech model; choosing the other model shows only its own parameter, and choosing the first model again shows
the edited number, which is also what is applied. This scenario is tested by
`SpeechSettingsPageViewModel_SelectModel_ShowsItsParametersAndRemembersEdits`.

**SpeechSettingsPageViewModel_Load_AfterEdits_ShowsStoredValuesAgain**: A value is edited, the page is closed without
applying, and the settings are loaded again; the stored value is shown. This scenario is tested by
`SpeechSettingsPageViewModel_Load_AfterEdits_ShowsStoredValuesAgain`.

**SpeechSettingsPageViewModel_Apply_StoredChoicesNotShown_AreKept**: A page over an unusable audio system and a catalog
that cannot be read loads settings that name devices and models and applies them; the names and identifiers are
unchanged. This scenario is tested by `SpeechSettingsPageViewModel_Apply_StoredChoicesNotShown_AreKept`.

**SpeechSettingsPageViewModel_OnClosed_RunningDownload_IsCancelled**: A download that waits for its token is running
when the page is closed. Its token is cancelled, nothing is running, and the model is not downloaded. This scenario is
tested by `SpeechSettingsPageViewModel_OnClosed_RunningDownload_IsCancelled`.

**SpeechSettingsPageViewModel_InvalidArguments_Throw**: Missing services and missing settings are refused. This
scenario is tested by `SpeechSettingsPageViewModel_InvalidArguments_Throw`.

**SettingsDialogView_SpeechPage_IsListedAndShownWhenChosen**: A dialog over the two standard pages lists `AI Provider`
and `Speech`; choosing the second shows the model lists and removes the AI provider controls. This scenario is tested
by `SettingsDialogView_SpeechPage_IsListedAndShownWhenChosen`.

**SettingsDialogView_CloseWindow_CancelsRunningDownload**: A download is started on the Speech page of a shown dialog
and the window is closed without OK or Cancel. The token was not cancelled before, is cancelled afterwards, and
nothing is running. This scenario is tested by `SettingsDialogView_CloseWindow_CancelsRunningDownload`.

**SettingsDialogView_OkButton_SavesSpeechChoices**: The second microphone and a model are chosen on the Speech page
and OK is clicked; the settings file holds the microphone name and the model identifier. This scenario is tested by
`SettingsDialogView_OkButton_SavesSpeechChoices`.

**MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses**: The Settings menu item of the main window
opens a dialog that lists the AI Provider and Speech pages, and Cancel closes it. This scenario is tested by
`MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses`.

**AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages**: The application's composition builds a
settings dialog whose pages are titled `AI Provider`, `Speech`, and `Style Kits`, the second being the Speech page. This
scenario is tested by `AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages`.

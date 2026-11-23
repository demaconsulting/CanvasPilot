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
- The page shows the stored talk key (F9 when none is stored or the stored key is not safe), takes a safe key or chord
  that is pressed in the box, refuses an unsafe one with a sentence and keeps the key, waits out a modifier alone,
  cancels the capture on Esc, stores F9 as nothing and any other key in its text form, and puts F9 back with the reset
  button, which is usable only while the key is not F9; the rules themselves are verified in *PushToTalkBinding
  Verification*.
- Choosing another model shows its parameters, edits to the first model are remembered, and a new load shows the
  stored values, not an earlier session's edits.
- Closing the page, or closing the dialog window by any route, cancels a running download.
- The settings dialog lists the Speech page after the AI Provider page, shows its content when chosen, and saves its
  choices with OK.
- In the accessibility sweep of the windows of the application, in both theme variants, the lists of the page have
  names, every button that shows no text has a name and a tooltip, and every enabled button can take the keyboard
  focus.

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

**SpeechSettingsPageViewModel_PushToTalkKey_CaptureValidateResetAndStore**: A page loaded with default settings shows
`F9`, is at the default, and its reset command cannot run. A modifier alone is not taken; the key `A` without a modifier
is refused with a sentence and the key stays `F9`; Esc is not taken and clears the sentence; `Ctrl+Shift+Space` is taken
and applying stores `Ctrl+Shift+Space`; the reset command can then run, and running it shows `F9` and applying stores
the empty string. This scenario is tested by `SpeechSettingsPageViewModel_PushToTalkKey_CaptureValidateResetAndStore`.

**SpeechSettingsPageViewModel_LoadApply_PushToTalkKey_RoundTripsAndUnsafeShowsDefault**: Settings that store
`Ctrl+Shift+T` are loaded; the page shows it and applying stores it unchanged; settings that store `Escape` are loaded
and the page shows `F9`. This scenario is tested by
`SpeechSettingsPageViewModel_LoadApply_PushToTalkKey_RoundTripsAndUnsafeShowsDefault`.

**SettingsDialogView_SpeechPage_IsListedAndShownWhenChosen**: A dialog over the two standard pages lists `AI Provider`
and `Speech`; choosing the second shows the model lists and removes the AI provider controls. This scenario is tested
by `SettingsDialogView_SpeechPage_IsListedAndShownWhenChosen`.

**SettingsDialogView_CloseWindow_CancelsRunningDownload**: A download is started on the Speech page of a shown dialog
and the window is closed without OK or Cancel. The token was not cancelled before, is cancelled afterwards, and
nothing is running. This scenario is tested by `SettingsDialogView_CloseWindow_CancelsRunningDownload`.

**SettingsDialogView_OkButton_SavesSpeechChoices**: The second microphone and a model are chosen on the Speech page
and OK is clicked; the settings file holds the microphone name and the model identifier. This scenario is tested by
`SettingsDialogView_OkButton_SavesSpeechChoices`.

**SpeechSettingsPageView_PushToTalkKeyBox_CapturesValidatesAndResets**: A shown page with the default key has the box
`SpeechPushToTalkKeyBox` showing `F9`, no sentence, and the reset button unusable. With the focus in the box, pressing
`A` leaves `F9`, shows the sentence (which mentions Ctrl or Alt); pressing `Ctrl+Shift+T` shows `Ctrl+Shift+T`, hides
the sentence, enables the reset button, and applying stores `Ctrl+Shift+T`; Esc and a lone Ctrl change nothing; clicking
the reset button shows `F9` and disables it again. This scenario is tested by
`SpeechSettingsPageView_PushToTalkKeyBox_CapturesValidatesAndResets`.

**MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses**: The Settings menu item of the main window
opens a dialog that lists the AI Provider and Speech pages, and Cancel closes it. This scenario is tested by
`MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses`.

**AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages**: The application's composition builds a
settings dialog whose pages are titled `AI Provider`, `Speech`, and `Style Kits`, the second being the Speech page. This
scenario is tested by `AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: The settings dialog of the real application is shown in
both theme variants and each of its pages, this one among them, is chosen and every control of it is looked at; the
lists have names, a button that shows no text has a name and a tooltip, and every enabled button can take the keyboard
focus. This scenario is tested by `Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.

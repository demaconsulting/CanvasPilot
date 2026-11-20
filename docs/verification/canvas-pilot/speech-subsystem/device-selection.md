### DeviceSelection

#### Verification Approach

`DeviceSelection` is verified at two levels. Unit tests in `DeviceSelectionTests.cs` drive
`DeviceSelectionViewModel` directly over substitute audio device services (NSubstitute, with the shared builders of
`SpeechSubsystemFakes.cs`) that report devices, no devices, an unusable audio system, and a scan that is refused.
Headless UI tests in `SpeechSettingsPageViewTests.cs` (the `UiTests` project, using Avalonia's headless platform with
the hand-written fakes of `SpeechUiTestSupport.cs`) show the real views and interact with their controls by
automation identifier.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display, audio hardware, or network is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- A new view model offers only the system default and has not used the audio service.
- Loading lists the system default and every device, selects stored names, selects the system default for an empty
  name, and keeps a stored device that is not connected selected and marked.
- A machine with no devices, and an unusable audio system, are explained in the status text of each list.
- A refresh scans, keeps the choices, shows new devices, and shows the reason when the scan is refused.
- In the page, the combo box shows the stored device and the status line shows the count.

#### Test Scenarios

**DeviceSelectionViewModel_New_OffersSystemDefaultOnlyAndDoesNotEnumerate**: A view model is created over a
substitute service. Its capture list holds only "System default", both names are empty, and the service was not asked
to list. This scenario is tested by `DeviceSelectionViewModel_New_OffersSystemDefaultOnlyAndDoesNotEnumerate`.

**DeviceSelectionViewModel_Load_StoredDevicesPresent_SelectsThemByName**: Two microphones and two speakers are
available and `Mic B` and `Out 1` are stored. The capture list is the default, `Mic A`, and `Mic B`, the stored names
are selected, and the status lines count two devices. This scenario is tested by
`DeviceSelectionViewModel_Load_StoredDevicesPresent_SelectsThemByName`.

**DeviceSelectionViewModel_Load_EmptyNames_SelectSystemDefault**: Empty names are loaded and both lists select
"System default". This scenario is tested by `DeviceSelectionViewModel_Load_EmptyNames_SelectSystemDefault`.

**DeviceSelectionViewModel_Load_StoredDeviceNotConnected_StaysSelectedAndMarked**: `USB headset` is stored on a
machine that has other devices. It stays selected, its label is "USB headset (not connected)", and its name is
unchanged. This scenario is tested by `DeviceSelectionViewModel_Load_StoredDeviceNotConnected_StaysSelectedAndMarked`.

**DeviceSelectionViewModel_Load_NoDevices_ExplainsAndKeepsDefault**: The service finds nothing. Both status lines say
that no device of that kind was found, and the default is still listed. This scenario is tested by
`DeviceSelectionViewModel_Load_NoDevices_ExplainsAndKeepsDefault`.

**DeviceSelectionViewModel_Load_AudioSystemUnavailable_ShowsProblem**: The service reports an unusable audio system.
Both status lines show its problem text and the stored microphone name is kept. This scenario is tested by
`DeviceSelectionViewModel_Load_AudioSystemUnavailable_ShowsProblem`.

**DeviceSelectionViewModel_Refresh_KeepsChoicesAndShowsNewDevices**: A loaded view model whose service gains a
microphone on refresh runs its refresh command. The new device is offered, the choice is unchanged, and the service
was refreshed once. This scenario is tested by `DeviceSelectionViewModel_Refresh_KeepsChoicesAndShowsNewDevices`.

**DeviceSelectionViewModel_Refresh_Refused_ShowsProblem**: The scan is refused. The capture status shows the reason.
This scenario is tested by `DeviceSelectionViewModel_Refresh_Refused_ShowsProblem`.

**DeviceSelectionViewModel_New_NullService_Throws**: A missing service is refused. This scenario is tested by
`DeviceSelectionViewModel_New_NullService_Throws`.

**SpeechSettingsPageView_LoadedSettings_ShowDevicesModelsAndParameters**: A page is shown over a machine with one
microphone and no speakers and settings that name the microphone. The capture combo box selects `Desk mic`, the
capture status reads "1 capture device(s) found.", and the playback status says that no playback device was found.
This scenario is tested by `SpeechSettingsPageView_LoadedSettings_ShowDevicesModelsAndParameters`.

**SpeechSettingsPageView_NothingAvailable_ShowsExplanations**: A page is shown over an unusable audio system. The
capture status shows the problem text. This scenario is tested by
`SpeechSettingsPageView_NothingAvailable_ShowsExplanations`.

**SpeechSettingsPageView_RefreshDevicesButton_ScansAndShowsNewDevice**: The Refresh devices button is pressed after a
speaker appears. The service was asked to scan once and the playback combo box offers `USB speakers`. This scenario is
tested by `SpeechSettingsPageView_RefreshDevicesButton_ScansAndShowsNewDevice`.

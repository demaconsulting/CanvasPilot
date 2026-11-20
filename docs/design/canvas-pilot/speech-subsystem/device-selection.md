### DeviceSelection

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`DeviceSelection` is the view model and view of the two audio device pickers on the Speech page: one for the
microphone (capture) and one for the speakers (playback). Each list starts with the system default and then offers the
devices that the audio system reports. The device name is the only stable identity the audio library gives a device,
so devices are chosen, and stored, by name.

#### Data Model

**DeviceChoice**: `Name` (the stored name, or an empty string for the system default) and `Label` (the text shown).

**CaptureDevices / PlaybackDevices**: `ObservableCollection<DeviceChoice>`. A new view model holds only the system
default entry in each.

**SelectedCaptureDevice / SelectedPlaybackDevice**: the chosen entry, with the derived `CaptureDeviceName` and
`PlaybackDeviceName`.

**CaptureStatus / PlaybackStatus**: `string` — a count of the devices found, an explanation that none was found, or
the problem text when the audio system could not be used.

**View**: `DeviceSelectionView` is an Avalonia user control with two combo boxes, two status lines, and a Refresh
devices button (`SpeechCaptureDeviceComboBox`, `SpeechPlaybackDeviceComboBox`, `SpeechCaptureStatus`,
`SpeechPlaybackStatus`, `SpeechRefreshDevicesButton`).

#### Key Methods

**Load**: Reads the devices through `IAudioDeviceService.Enumerate`, rebuilds both lists, and selects the stored
names. A stored name that is not among the devices is added as "{name} (not connected)" and selected, so the choice
is not lost when a headset is unplugged. This is the first use of the audio system; creating the view model does not
use it.

**Refresh** (command): Calls `IAudioDeviceService.Refresh`, rebuilds both lists, and keeps the current choices. A
scan that was refused shows its reason in both status lines.

#### Error Handling

There are no exceptions to handle: the audio service returns problems as text, which the status lines show. A missing
service is refused with `ArgumentNullException`.

#### Dependencies

- **AudioDeviceService** — through `IAudioDeviceService`; the library's `AudioDeviceDescription` is the device type.
- **CommunityToolkit.Mvvm** and **Avalonia** — property change notification, the command, and the view.

#### Callers

- `SpeechSettingsPage` — composes the unit, loads it, and reads the chosen names.

### AudioDeviceService

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`AudioDeviceService` lists the capture (microphone) and playback (speaker) devices through the speech library's
`AudioDeviceFactory`. It is the only unit that talks to the audio system. Every failure of the audio system, such
as a machine without audio hardware or without the native audio library, becomes an empty list with an explanation
instead of an exception, so the Speech page works on every machine.

#### Data Model

**AudioDeviceSnapshot**: `CaptureDevices` and `PlaybackDevices` (`IReadOnlyList<AudioDeviceDescription>`, each
with `Name`, `Direction`, `ChannelCount`, and `SampleRate`), `Problem` (`string?`, `null` when the audio system
answered), and `IsAvailable` (`Problem` is `null`). `AudioDeviceSnapshot.Unavailable(problem)` creates a snapshot
with no devices.

**IAudioDeviceService**: `Enumerate()` and `Refresh()`, both returning an `AudioDeviceSnapshot`.

#### Key Methods

**Enumerate**: Calls the capture and playback probes of the factory and returns the lists in the order the audio
system reports them. A list that cannot be read is empty and the first failure becomes the problem text.

**Refresh**: Calls `AudioDeviceFactory.RefreshDevices`, which makes the audio system scan for changed hardware and
is refused while a stream is open, and then lists the devices as `Enumerate` does. A failed scan becomes the problem
text and does not prevent the listing.

**Lazy use**: The factory is created on the first call, so creating the service, which the application does at
startup, does not load the native audio library. An internal constructor takes the three backend operations as
functions so that tests can cause failures that real hardware cannot be made to produce.

#### Error Handling

Failures of the audio system are caught, written to the log as warnings, and returned as the problem text, which
starts with "The audio system could not be used." and adds the failure's message. The catch is deliberately broad:
the native audio library can fail in ways that differ by platform and machine, and none of them may stop the
settings dialog. Failures to write a log entry are ignored. The internal constructor refuses missing functions with
`ArgumentNullException`.

#### Dependencies

- **DemaConsulting.Speech** (OTS) — `AudioDeviceFactory`, its capture and playback probes, and
  `AudioDeviceDescription`; see the Speech OTS design.
- **RollingFileLogger** — optional; records audio system problems.

#### Callers

- `DeviceSelection` — lists the devices when the page loads and on Refresh.
- `ApplicationComposition` — creates the service.

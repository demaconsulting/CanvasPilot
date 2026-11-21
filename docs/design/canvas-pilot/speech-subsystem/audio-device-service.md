### AudioDeviceService

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`AudioDeviceService` lists the capture (microphone) and playback (speaker) devices through the speech library's
`AudioDeviceFactory` and creates the capture device that dictation listens through. It is the only unit that talks to
the audio system. Every failure of the audio system, such as a machine without audio hardware or without the native
audio library, becomes an empty list or a missing device with an explanation instead of an exception, so the Speech
page and dictation work, or explain themselves, on every machine.

#### Data Model

**AudioDeviceSnapshot**: `CaptureDevices` and `PlaybackDevices` (`IReadOnlyList<AudioDeviceDescription>`, each
with `Name`, `Direction`, `ChannelCount`, and `SampleRate`), `Problem` (`string?`, `null` when the audio system
answered), and `IsAvailable` (`Problem` is `null`). `AudioDeviceSnapshot.Unavailable(problem)` creates a snapshot
with no devices.

**IAudioDeviceService**: `Enumerate()`, `Refresh()`, and `CreateCaptureDevice(deviceName)`, which return an
`AudioDeviceSnapshot` or a `CaptureDeviceResult`.

**CaptureDeviceResult**: `Device` (the `IAudioCaptureDevice`, or `null`), `Problem` (`string?`, the explanation when
there is no device), and `IsAvailable` (`Device` is not `null`). `CaptureDeviceResult.Unavailable(problem)` creates a
result with no device.

#### Key Methods

**Enumerate**: Calls the capture and playback probes of the factory and returns the lists in the order the audio
system reports them. A list that cannot be read is empty and the first failure becomes the problem text.

**Refresh**: Calls `AudioDeviceFactory.RefreshDevices`, which makes the audio system scan for changed hardware and
is refused while a stream is open, and then lists the devices as `Enumerate` does. A failed scan becomes the problem
text and does not prevent the listing.

**Lazy use**: The factory is created on the first call, so creating the service, which the application does at
startup, does not load the native audio library. An internal constructor takes the backend operations as
functions so that tests can cause failures that real hardware cannot be made to produce.

**CreateCaptureDevice**: Turns the chosen device name into the library's device selection, with the empty name meaning
the system default, and asks the factory for a capture device in the mono format at 16 kHz that speech models take; the
audio system converts from the hardware's own format. The library resolves a name that no device has any more to the
default device. The device is created, not started: the recognition session that is given the device starts and stops
it, and the caller disposes the device when it is disposable. A device that reports itself unavailable, which is how
the library answers a machine without a microphone, and any exception of the audio system become a result with no
device and an explanation that names what to check, including the operating system's permission for desktop
applications to use the microphone.

#### Error Handling

Failures of the audio system are caught, written to the log as warnings, and returned as the problem text, which
starts with "The audio system could not be used." and adds the failure's message; a capture device that is unavailable
is returned with a problem text that starts with "No microphone could be used." The catch is deliberately broad:
the native audio library can fail in ways that differ by platform and machine, and none of them may stop the
settings dialog or dictation. Failures to write a log entry are ignored. The internal constructor refuses missing
functions with `ArgumentNullException`; a service built without a way to create capture devices returns the problem
that none can be created.

#### Dependencies

- **DemaConsulting.Speech** (OTS) — `AudioDeviceFactory`, its capture and playback probes, `AudioDeviceDescription`,
  and the capture device it creates; see the Speech OTS design.
- **RollingFileLogger** — optional; records audio system problems.

#### Callers

- `DeviceSelection` — lists the devices when the page loads and on Refresh.
- `DictationService` — lists the microphones for the availability check and creates the capture device for each hold.
- `ApplicationComposition` — creates the service.

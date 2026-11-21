### AudioDeviceService

#### Verification Approach

`AudioDeviceService` is verified by unit tests in `AudioDeviceServiceTests.cs`. Most tests use the internal
constructor, which takes the three backend operations as functions, so that device lists and failures of the audio
system, such as a missing native library or a refused scan, can be produced on any machine. One test uses the real
audio backend and accepts whatever the machine has, which proves that the service never throws on the machine that
runs the tests, with or without audio hardware.

#### Test Environment

Standard test environment with a temporary folder for the log; no audio hardware, network, external services, or
display are required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Devices of both directions are returned by name in the order the backend reports them, and a backend with no
  devices is an available result with empty lists.
- A refresh scans before it lists, and the new device is visible afterwards.
- A failing backend gives empty lists and a problem text that names the failure, a refused scan keeps the devices
  that could be listed and names the refusal, and failures are written to the log.
- Creating the service does not use the backend, and missing operations are refused.
- The service over the real backend returns snapshots and does not throw.
- A usable microphone is returned as the device, with the chosen name passed on; an unavailable device, a backend that
  throws, and a service without a way to create devices are results with an explanation and no device, and the failure
  is logged; the real backend creates a device or explains why it cannot, and never throws.

#### Test Scenarios

**AudioDeviceService_Enumerate_DevicesPresent_ReturnsBothLists**: A backend with two microphones and one speaker is
listed. The capture names are `Mic A` and `Mic B` in order, the playback name is `Speakers`, and there is no problem.
This scenario is tested by `AudioDeviceService_Enumerate_DevicesPresent_ReturnsBothLists`.

**AudioDeviceService_Enumerate_NoDevices_IsAvailableWithEmptyLists**: A backend that answers with no devices gives
empty lists and an available result. This scenario is tested by
`AudioDeviceService_Enumerate_NoDevices_IsAvailableWithEmptyLists`.

**AudioDeviceService_Refresh_Succeeds_ScansThenListsDevices**: A backend gains a microphone when it is refreshed. The
listing before the refresh is empty and the refresh lists `New mic`. This scenario is tested by
`AudioDeviceService_Refresh_Succeeds_ScansThenListsDevices`.

**AudioDeviceService_Enumerate_BackendThrows_ReturnsUnavailableWithProblem**: Both lists throw (a missing native
library and an invalid operation). No exception escapes, both lists are empty, the result is unavailable, and the
problem names the first failure, `audio library is missing`. This scenario is tested by
`AudioDeviceService_Enumerate_BackendThrows_ReturnsUnavailableWithProblem`.

**AudioDeviceService_Refresh_Refused_ReturnsProblemAndStillLists**: A scan that is refused because a device is in use
still lists `Busy mic`, and the problem is the refusal's message. This scenario is tested by
`AudioDeviceService_Refresh_Refused_ReturnsProblemAndStillLists`.

**AudioDeviceService_Constructed_DoesNotTouchAudioSystem**: The service is created over operations that count their
calls and is not used. No operation was called. This scenario is tested by
`AudioDeviceService_Constructed_DoesNotTouchAudioSystem`.

**AudioDeviceService_Failure_IsLogged**: A failing capture list is enumerated with a logger. The log holds "Listing
the audio devices failed." and the failure's message. This scenario is tested by `AudioDeviceService_Failure_IsLogged`.

**AudioDeviceService_InvalidArguments_Throw**: Each missing backend operation is refused. This scenario is tested by
`AudioDeviceService_InvalidArguments_Throw`.

**AudioDeviceService_RealBackend_EnumerateAndRefresh_NeverThrow**: The service over the real backend enumerates and
refreshes. Both calls return snapshots, and a snapshot that is unavailable has no devices and a problem text. This
scenario is tested by `AudioDeviceService_RealBackend_EnumerateAndRefresh_NeverThrow`.

**AudioDeviceService_CreateCaptureDevice_UsableMicrophone_ReturnsDeviceForChosenName**: A backend creates a working
device for any name and remembers the name. A microphone is asked for by name. The result has the device, no problem,
and the name reached the backend unchanged. This scenario is tested by
`AudioDeviceService_CreateCaptureDevice_UsableMicrophone_ReturnsDeviceForChosenName`.

**AudioDeviceService_CreateCaptureDevice_UnavailableDeviceOrBackendFailure_ReturnsProblem**: One backend hands back the
library's unavailable device and another throws a missing native library error, the second with a log. Both results have
no device and an explanation, the first saying that no microphone could be used and the second naming the missing
library, and the failure is in the log. This scenario is tested by
`AudioDeviceService_CreateCaptureDevice_UnavailableDeviceOrBackendFailure_ReturnsProblem`.

**AudioDeviceService_CreateCaptureDevice_NoCreatorConfigured_ReturnsProblem**: A service built over operations that only
list devices is asked for a microphone. The result has no device and an explanation. This scenario is tested by
`AudioDeviceService_CreateCaptureDevice_NoCreatorConfigured_ReturnsProblem`.

**AudioDeviceService_RealBackend_CreateCaptureDevice_NeverThrows**: The service over the real backend is asked for the
default microphone. The result has either a device or an explanation, never both and never neither, and the device is
disposed again. This scenario is tested by `AudioDeviceService_RealBackend_CreateCaptureDevice_NeverThrows`.

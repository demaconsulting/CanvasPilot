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

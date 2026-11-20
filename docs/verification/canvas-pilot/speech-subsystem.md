## Speech

### Verification Approach

The `Speech` subsystem is verified by integration tests defined in `SpeechSubsystemTests.cs`. The tests compose the
real `AudioDeviceService` (over substitute audio operations, so no hardware is needed), the real
`SpeechModelService` (over the library's real model catalog on a store in a temporary folder that holds substitute
models), the real `SpeechSettingsPageViewModel` with its device, model, and parameter units, a real
`SettingsDialogViewModel`, and a real `SettingsService` on a temporary settings folder. One test replaces the model
service with a substitute whose download waits until it is cancelled, because a real download needs the network.
The window and the controls are verified by the headless UI tests described in the unit verification documents.

### Test Environment

N/A - standard test environment. Tests use temporary local folders and real file I/O; no audio hardware, network,
external services, or display are required, and nothing is downloaded.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Devices, models, and a model parameter chosen in the dialog and accepted with OK are stored by device name and
  model identifier, restored by a new settings service, and shown by a new page.
- A machine whose audio system and model store cannot be used still opens the dialog, explains both problems, saves,
  and keeps the stored device and model choices.
- Canceling the dialog while a download runs cancels the download and leaves the model not downloaded.

### Test Scenarios

**SpeechSubsystem_ConfigureSpeech_DialogOk_StoresDevicesModelsAndParameters**: A dialog over the Speech page lists two
microphones, one speaker, and a catalog with a speech-to-text model and a text-to-speech model that has a voice
choice. The user chooses the second microphone, the speaker, both models, and the third voice, and chooses OK. A
fresh settings service loads the capture and playback device names, both model identifiers, and the voice, and a new
page loaded from the file shows the second microphone and the voice. This scenario is tested by
`SpeechSubsystem_ConfigureSpeech_DialogOk_StoresDevicesModelsAndParameters`.

**SpeechSubsystem_ToleratePoorMachines_NoAudioNoModelStore_DialogStillWorks**: Settings that name a microphone and a
speech-to-text model are stored. A dialog is opened over an audio backend that throws and a model store that cannot be
created. The capture status names the missing audio library, the model message names the store problem, no error is
shown when OK is chosen, and the stored microphone and model are still in the file. This scenario is tested by
`SpeechSubsystem_ToleratePoorMachines_NoAudioNoModelStore_DialogStillWorks`.

**SpeechSubsystem_CancelDownloadsOnClose_DialogCancelled_StopsRunningDownload**: A download is started from the page
and waits for its token. The user cancels the dialog. The download had been running, its token is cancelled, and the
model is left not downloaded. This scenario is tested by
`SpeechSubsystem_CancelDownloadsOnClose_DialogCancelled_StopsRunningDownload`.

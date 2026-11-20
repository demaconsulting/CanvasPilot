## Speech

### Verification Approach

The `Speech` subsystem is verified by integration tests defined in `SpeechSubsystemTests.cs`. The tests compose the real
`AudioDeviceService` (over substitute audio operations, so no hardware is needed), the real `SpeechModelService` (over
the library's real model catalog on a store in a temporary folder that holds substitute models), the real
`SpeechSettingsPageViewModel` with its device, model, and parameter units, a real `SettingsDialogViewModel`, and a real
`SettingsService` on a temporary settings folder. One test replaces the model service with a substitute whose download
waits until it is cancelled, because a real download needs the network. Dictation is verified by composing the real
`DictationService` with the real `SettingsService`, `SpeechModelService`, and `AudioDeviceService` and with the speech
library's own recognition pipeline, run over a speech-to-text model and recognition backend written in managed code
(installed in the temporary model store with a hand-made install manifest) and a recorded sound file that stands in for
the microphone, so no native speech runtime, model download, or microphone is needed and the native runtime is never
loaded. The window and the controls are verified by the headless UI tests described in the unit verification documents.

The delivery of the native speech runtime to the installed application, which is the `ShipSpeechRuntime` requirement,
is verified on the output of the Windows build: the FileAssert test `Speech_NativeRuntimePublished` asserts that the
published win-x64 folder, which the installer takes whole, contains the three native libraries, and a build step reads
the file table of the installer package and fails when any of them is missing. Recognizing real speech with a real model
and a real microphone needs a model download and a person, and is checked by hand before a release (see *Manual
Verification*).

### Test Environment

N/A - standard test environment. Tests use temporary local folders and real file I/O; no audio hardware, network,
external services, or display are required, nothing is downloaded, and no native speech runtime is loaded. The
FileAssert test runs only in the Windows build job after the application is published.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Devices, models, and a model parameter chosen in the dialog and accepted with OK are stored by device name and
  model identifier, restored by a new settings service, and shown by a new page.
- A machine whose audio system and model store cannot be used still opens the dialog, explains both problems, saves,
  and keeps the stored device and model choices.
- Canceling the dialog while a download runs cancels the download and leaves the model not downloaded.
- Holding to talk over the speech library's recognition pipeline returns the final sentence when the user lets go, and
  the next hold can start.
- Each state in which dictation cannot work has its own reason, and none loads a speech engine, including the state in
  which the native speech files are missing.
- The published win-x64 folder contains `sherpa-onnx-c-api.dll`, `onnxruntime.dll`, and `portaudio.dll`, and so does the
  installer package.

### Manual Verification

These checks need a person, a microphone, and a downloaded model, so they are not automated and are not evidence for a
requirement. They are performed before a release, on a machine with the installed application:

- **Real dictation**: With a speech-to-text model downloaded and chosen on the Speech page and a microphone chosen,
  hold the talk control and say a sentence, then let go. The words appear while the control is held, the final text is
  what was said, and the last word is not cut off. Repeat several times without restarting the application; the first
  hold may take a moment, the later ones do not.
- **Native speech files**: In the installed folder, `sherpa-onnx-c-api.dll`, `onnxruntime.dll`, and `portaudio.dll` are
  beside `CanvasPilot.exe`. Temporarily move `onnxruntime.dll` away and hold the talk control: the application says that
  the speech runtime is missing and does not crash. Move it back.
- **Microphone privacy**: Turn off "Let desktop apps access your microphone" in the Windows privacy settings and hold
  the talk control: the application says that no microphone could be used and names the permission. Turn it back on.
- **No model, no microphone**: With no model chosen, then with a model that is not downloaded, then with the microphone
  unplugged, hold the talk control. Each case gives its own message.
- **Real-engine test**: With a speech-to-text and a text-to-speech model downloaded, set the environment variable
  `CANVASPILOT_SPEECH_INTEGRATION` to `1` and run the OTS test
  `Speech_RecognizerFactory_WavFileDevice_WithInstalledModel_ProducesText`, which has the real engine recognize a
  sentence that the other model speaks into a sound file.

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

**SpeechSubsystem_Dictation_FakeEngine_ReturnsFinalTextAfterRelease**: The settings choose a speech-to-text model that
is installed in a temporary store, and the microphone is a recorded sound file. The speech library's recognition
pipeline runs over a recognition backend that reports two provisional results and then the final sentence "Hello
world." The user holds, which succeeds with dictation available, and lets go. The final text is "Hello world.", the
session has no problem, and a second hold starts, which shows that the microphone and the session were released. This
scenario is tested by `SpeechSubsystem_Dictation_FakeEngine_ReturnsFinalTextAfterRelease`.

**SpeechSubsystem_Dictation_UnavailableStates_AreExplainedWithoutLoadingAnEngine**: Over a model whose backend would
throw if it were ever built, dictation is checked with no model chosen and with a chosen model that is not installed.
Then, over a service that finds the native files missing, a model that is installed is checked, and a begin and a
preparation are made. The reasons are no model chosen, model not installed, and speech runtime missing for all three
requests of the last state, and the backend was never built. This scenario is tested by
`SpeechSubsystem_Dictation_UnavailableStates_AreExplainedWithoutLoadingAnEngine`.

**Speech_NativeRuntimePublished**: After the Windows build job publishes the application self-contained for win-x64 and
before the installer is built from that folder, FileAssert asserts that `publish/win-x64` holds exactly one each of
`sherpa-onnx-c-api.dll`, `onnxruntime.dll`, and `portaudio.dll`, each of a plausible size. A later step of the same job
reads the file table of the installer package and fails when any of the three is not listed. This scenario is tested by
`Speech_NativeRuntimePublished`.

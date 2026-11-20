## Speech Verification

This document provides the verification evidence for the Speech OTS software item. Requirements for this OTS item are
defined in the Speech OTS Software Requirements document.

### Required Functionality

Speech (`DemaConsulting.Speech`, with its `DemaConsulting.Speech.Sherpa` model package) lists the capture and
playback devices by name, tolerates machines without audio hardware, creates a capture device for a chosen microphone,
lists speech-to-text and text-to-speech models with their install state from the local store, describes the tunable
parameters of the models, downloads models with cancellation, streams recognition results from a capture device through
a recognition session, and answers a model that is not installed with an unavailable engine. Its packages deliver the
native speech inference runtime, the Sherpa interface, and the audio library to the output folder of a build. The
application relies on each of these behaviors, so each has a requirement and a test.

### Verification Approach

Speech has no self-validation mode. It is verified by integration tests in `test/OtsSoftwareTests/SpeechTests.cs`
that call the library directly, with the same calls the application's services use (`AudioDeviceFactory`,
`SpeechModelCatalog` with `AddSherpaModels`, `SpeechModelCatalog.DownloadAsync`, `SpeechRecognizerFactory`), on a model
store in a temporary folder. The tests never download a model: downloads are requested only in ways that the library
refuses before any transfer, namely an unknown model and a token that is already cancelled. They accept whatever audio
hardware the machine has, including none, because the build servers have none. The streaming of recognition results is
tested through the library's own recognition pipeline with a recognition backend and a model written in managed code and
a sound file as the microphone, which needs no model, no microphone, and no native runtime, and an unavailable engine is
asked for with a model that is not installed, which the library answers before it reaches any native code. The delivery
of the native runtime is tested on 64-bit Windows, the only platform for which the application is installed, by loading
each library from the output folder and checking that the process holds that copy. The tests are the evidence that the
library behaves as the application relies on, and they are run again before a version upgrade is accepted. The unit
tests of the services repeat part of this evidence through the application's own calls.

Three checks complement these tests. The FileAssert test `Speech_NativeRuntimePublished` asserts the three native
libraries in the published folder, and a step of the Windows build reads the file table of the installer package; both
are described in the verification of the Speech subsystem. A test that has the real native engine recognize a sentence
that a second model speaks into a sound file runs only when the environment variable `CANVASPILOT_SPEECH_INTEGRATION` is
`1` and a speech-to-text and a text-to-speech model are downloaded; it is not evidence for a requirement.

A download that installs a model needs the network and transfers a large file. It is checked by hand before a
release: in the Speech page of a build, download the smallest speech-to-text model, watch the progress bar, and
confirm that the model shows as installed and that canceling a second download leaves its model not downloaded.
Dictation with a real microphone and a real model is checked by hand as described in the verification of the Speech
subsystem. These checks are not automated and are not evidence for a requirement.

### Test Scenarios

#### Speech_SherpaCatalog_Enumerate_ListsRecognitionAndSynthesisModels

**Scenario**: A catalog over an empty store in a temporary folder is filled with the Sherpa models and enumerated.

**Expected**: At least one speech-to-text and one text-to-speech model are listed, every model has an identifier and
a display name, identifiers are unique, and every model is not downloaded.

**Requirement coverage**: `CanvasPilot-OTS-Speech-ListSherpaModels`.

#### Speech_SherpaCatalog_ModelParameters_AreTypedWithUsableBoundsAndDefaults

**Scenario**: The parameters of all Sherpa models are collected.

**Expected**: At least one numeric and one choice parameter exist; every parameter has an identifier; each numeric
default lies within its bounds and its step is positive; each choice default is one of its options.

**Requirement coverage**: `CanvasPilot-OTS-Speech-DescribeModelParameters`.

#### Speech_SherpaCatalog_Download_UnknownModelOrCancelledToken_IsRefusedWithoutTransfer

**Scenario**: A download of an unknown model is requested, and a download of a known model is requested with a token
that is already cancelled.

**Expected**: The first throws an argument exception that names the model; the second throws an
operation-cancelled exception; the known model is still not downloaded.

**Requirement coverage**: `CanvasPilot-OTS-Speech-DownloadModels`.

#### Speech_AudioDeviceFactory_Enumerate_ReturnsNamedDevicesOrAnEmptyList

**Scenario**: The capture and playback probes of a new audio device factory are enumerated.

**Expected**: Both lists exist, and every device has a name and the direction of its list. On a machine without audio
hardware both lists are empty.

**Requirement coverage**: `CanvasPilot-OTS-Speech-EnumerateAudioDevices`.

#### Speech_AudioDeviceFactory_DefaultDevicesAndRefresh_ToleratesMissingHardware

**Scenario**: The default capture and playback devices are created, the hardware is scanned again with no stream open,
and the capture devices are listed again.

**Expected**: The devices exist, the new scan does not throw, and the list exists.

**Requirement coverage**: `CanvasPilot-OTS-Speech-ToleratePoorAudioSystems`.

#### Speech_AudioDeviceFactory_CreateCaptureDevice_UnknownNameInMonoFormat_ReturnsDeviceWithoutThrowing

**Scenario**: A capture device is asked for with the name of a microphone that no machine has and in the mono format of
speech models, and another with the system default in the same format.

**Expected**: Both are devices and nothing is thrown. A device that reports itself usable has a positive sample rate and
channel count, and one that reports itself unusable has a sample rate of zero.

**Requirement coverage**: `CanvasPilot-OTS-Speech-CreateCaptureDevice`.

#### Speech_RecognizerFactory_ManagedBackend_StreamsProvisionalThenFinalResultsAndDrainsOnStop

**Scenario**: A speech-to-text model written in managed code, whose backend reports two provisional results once audio
has arrived and a final result when it is flushed, is installed in a store in a temporary folder. The library's
recognizer factory loads it, a session is created over a sound file of one second of silence as the microphone, the
results are read from before the start, with the first step of the enumeration taken before it, the session is started
and stopped, and the stream is awaited.

**Expected**: The engine is available, the results include provisional ones that start with the words the backend
reported, the last result is the final one with its text, every earlier result is provisional, and the stream ends after
the stop.

**Requirement coverage**: `CanvasPilot-OTS-Speech-RecognizeStreamingSpeech`.

#### Speech_RecognizerFactory_ModelNotInstalled_ReturnsUnavailableEngineWithoutThrowing

**Scenario**: A speech-to-text model of the Sherpa catalog over an empty store is loaded by the recognizer factory, and
the engine is asked for a session over the library's unavailable capture device.

**Expected**: Nothing is thrown, the engine reports itself unavailable, and so does the session. No native speech
runtime is needed for this, because the library answers before it reaches any native code.

**Requirement coverage**: `CanvasPilot-OTS-Speech-DegradeWithoutModelOrRuntime`.

#### Speech_NativeRuntime_WindowsLibraries_AreDeliveredAndLoadFromTheApplicationFolder

**Scenario**: On 64-bit Windows, the inference runtime, the Sherpa interface, and the audio library are loaded by full
path from the runtime folder of the test output, in that order. The modules of the process are then listed. On other
platforms the test is skipped.

**Expected**: Each file exists in the output folder and loads, and the process holds exactly one module of each name,
whose file is in the folder of the test output and not in a Windows folder.

**Requirement coverage**: `CanvasPilot-OTS-Speech-DeliverNativeRuntime`, on Windows.

#### Speech_RecognizerFactory_WavFileDevice_WithInstalledModel_ProducesText

**Scenario**: Opt-in; skipped unless `CANVASPILOT_SPEECH_INTEGRATION` is `1` and a speech-to-text and a text-to-speech
model are downloaded. The text-to-speech model speaks "Please make the second box blue." into a sound file, the real
native engine of the speech-to-text model recognizes the sound file through the sound-file capture device, and the final
results are collected.

**Expected**: The native engine loads and the recognized text contains "make the second". The very end of a sentence can
be cut off by a recognizer that is stopped abruptly, so the test does not demand it.

**Requirement coverage**: none; it is manual evidence before a release.

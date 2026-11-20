## Speech Verification

This document provides the verification evidence for the Speech OTS software item. Requirements for this OTS item are
defined in the Speech OTS Software Requirements document.

### Required Functionality

Speech (`DemaConsulting.Speech`, with its `DemaConsulting.Speech.Sherpa` model package) lists the capture and
playback devices by name, tolerates machines without audio hardware, lists speech-to-text and text-to-speech models
with their install state from the local store, describes the tunable parameters of the models, and downloads models
with cancellation. The application relies on each of these behaviors, so each has a requirement and a test.

### Verification Approach

Speech has no self-validation mode. It is verified by integration tests in `test/OtsSoftwareTests/SpeechTests.cs`
that call the library directly, with the same calls the application's two services use (`AudioDeviceFactory`,
`SpeechModelCatalog` with `AddSherpaModels`, `SpeechModelCatalog.DownloadAsync`), on a model store in a temporary
folder. The tests never download a model: downloads are requested only in ways that the library refuses before any
transfer, namely an unknown model and a token that is already cancelled. They accept whatever audio hardware the
machine has, including none, because the build servers have none. The tests are the evidence that the library behaves
as the application relies on, and they are run again before a version upgrade is accepted. The unit tests of the
services repeat part of this evidence through the application's own calls.

A download that installs a model needs the network and transfers a large file. It is checked by hand before a
release: in the Speech page of a build, download the smallest speech-to-text model, watch the progress bar, and
confirm that the model shows as installed and that canceling a second download leaves its model not downloaded. This
check is not automated and is not evidence for a requirement.

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

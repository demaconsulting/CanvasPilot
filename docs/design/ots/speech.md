## Speech

This document describes the integration and usage design for the `Speech` OTS software item.

### Purpose

`DemaConsulting.Speech` is the DEMA Consulting library for speech recognition and synthesis, with its
`DemaConsulting.Speech.Sherpa` package, which supplies the speech-to-text and text-to-speech models that run on the
Sherpa speech engine. CanvasPilot lets its user dictate messages by speech: the Speech settings page uses the library
for what the user must set up first, which audio devices to use and which models to download and how to tune them, and
dictation uses it to turn what the user says into text. The two packages are pinned to one version in the application
project. They are DEMA Consulting software from a separate repository and program, so they are treated as OTS, not as a
shared package of this program.

### Features Used

- `AudioDeviceFactory` with its `CaptureProbe` and `PlaybackProbe` (`Enumerate`) and `RefreshDevices` — lists the
  capture and playback devices by name (`AudioDeviceDescription`) and scans for changed hardware. A machine without
  audio hardware or without the native audio library gets empty lists, not exceptions.
- `AudioDeviceFactory.CreateCaptureDevice` with an `AudioDeviceSelection` and an `AudioFormat` — creates the capture
  device of the chosen microphone, or of the system default when the name is empty or no longer exists, in the mono
  format of speech models. A machine without a usable microphone gets a device that reports `IsAvailable` as false, not
  an exception.
- `SpeechModelCatalog` with the `AddSherpaModels` extension — lists the models (`SpeechModelDescriptor`: `Id`,
  `DisplayName`, `Role`, `State`, and `Model`) over the library's default model store, which is shared with other
  DEMA Consulting applications, and downloads a model with `DownloadAsync(modelId, progress, cancellationToken)`,
  which verifies checksums, installs completely or not at all, reports `SpeechModelDownloadProgress`, refuses an
  unknown model with `ArgumentException`, and stops with `OperationCanceledException` when cancelled.
- `ISpeechModel.Parameters` — the model's tunable parameters as `NumericParameter`, `ChoiceParameter`, and
  `BooleanParameter` descriptors with identifiers, bounds or options, and defaults; their stored values are passed to
  the engine as an untyped bag of numbers, strings, and booleans.
- `SpeechRecognizerFactory.LoadAsync` — loads the engine (`ISpeechRecognizerEngine`) of an installed speech-to-text
  model, with an `ISpeechDiagnostics` sink. It answers a model that is not installed, an absent native runtime, and a
  machine without a microphone with an unavailable engine (`IsAvailable` is false), not an exception. Loading takes a
  noticeable time, so the engine is loaded once and kept.
- `ISpeechRecognizerEngine.CreateSessionAsync` and `IRecognitionSession` — one single-use session per hold of the talk
  control: `StartAsync`, `StopAsync` (which finishes the audio already received), `GetResultsAsync` (a stream of
  provisional results, each the whole current sentence, followed by a final result), and `DisposeAsync`. Only one
  session can be leased from an engine at a time. The recognizer keeps no results for a reader that is not yet reading,
  so the first step of the enumeration is taken before the session is started. With a sound file as the microphone the
  results are delivered when the session is stopped.
- `WavFileAudioCaptureDevice` — a capture device that plays a sound file, which is the test seam that lets recognition
  be tested without a microphone.

### Integration Pattern

The libraries are NuGet package references of the application library. Only `AudioDeviceService` calls the audio
factory, only `SpeechModelService` creates and calls the model catalog and the recognizer factory, and only
`DictationService` creates recognition sessions; the other units use the library's descriptor types, which are plain
data, and talk to the services through `IAudioDeviceService`, `ISpeechModelService`, and `IDictationService`, so their
tests use substitute services. The services create their library objects on first use, so starting the application
neither loads the native audio library, nor reads the model store, nor loads a speech engine.

The native speech runtime is not a separate package of the application: it arrives with the library. The Sherpa package
depends on the `org.k2fsa.sherpa.onnx` package, which depends on the native runtime package of each platform it
supports, and these deliver the Sherpa C interface and the inference runtime; the audio package that the library uses
delivers the native audio library. For 64-bit Windows the publish output therefore contains `sherpa-onnx-c-api.dll`,
`onnxruntime.dll`, and `portaudio.dll` beside the application, and the installer, which takes every file of the
publish output, installs them. The native files are checked in three places, because an engine that is started without
them can crash the process: the FileAssert test `Speech_NativeRuntimePublished` asserts the published output, the
Windows build reads the file table of the installer package and fails when any of the three is missing, and
`DictationService` checks the two files that the engine cannot start without before it loads an engine, and reports
that the speech runtime is missing instead. The crash happens because Windows has an older inference runtime in its
system folder, which it loads when the application's copy is missing, and the Sherpa interface cannot work with it.
A test that loads the three libraries from the output folder of a build is the OTS evidence that the packages deliver
them.

The library has no self-validation mode; the OTS tests in `SpeechTests.cs` call the library the way the services do, on
a model store in a temporary folder, and are the evidence that it behaves as the application relies on. They never
download a model, and a library upgrade re-runs them before it is accepted. The streaming behavior is tested through the
library's own recognition pipeline with a recognition backend written in managed code and a sound file for a microphone,
so no model and no native runtime are needed. An opt-in test, which runs only when an environment variable asks for it
and both a speech-to-text and a text-to-speech model are downloaded, has the real engine recognize a sentence that the
other model speaks into a sound file. Downloading a real model needs the network, and dictating with a real microphone
needs a person; both are checked by hand before a release.

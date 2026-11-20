## Speech

This document describes the integration and usage design for the `Speech` OTS software item.

### Purpose

`DemaConsulting.Speech` is the DEMA Consulting library for speech recognition and synthesis, with its
`DemaConsulting.Speech.Sherpa` package, which supplies the speech-to-text and text-to-speech models that run on the
Sherpa speech engine. CanvasPilot is to converse with its user by speech, so the Speech settings page uses the library
for what the user must set up first: which audio devices to use, and which models to download and how to tune them.
The two packages are pinned to one version in the application project. They are DEMA Consulting software from a
separate repository and program, so they are treated as OTS, not as a shared package of this program.

### Features Used

- `AudioDeviceFactory` with its `CaptureProbe` and `PlaybackProbe` (`Enumerate`) and `RefreshDevices` — lists the
  capture and playback devices by name (`AudioDeviceDescription`) and scans for changed hardware. A machine without
  audio hardware or without the native audio library gets empty lists, not exceptions.
- `SpeechModelCatalog` with the `AddSherpaModels` extension — lists the models (`SpeechModelDescriptor`: `Id`,
  `DisplayName`, `Role`, `State`, and `Model`) over the library's default model store, which is shared with other
  DEMA Consulting applications, and downloads a model with `DownloadAsync(modelId, progress, cancellationToken)`,
  which verifies checksums, installs completely or not at all, reports `SpeechModelDownloadProgress`, refuses an
  unknown model with `ArgumentException`, and stops with `OperationCanceledException` when cancelled.
- `ISpeechModel.Parameters` — the model's tunable parameters as `NumericParameter`, `ChoiceParameter`, and
  `BooleanParameter` descriptors with identifiers, bounds or options, and defaults.

### Integration Pattern

The libraries are NuGet package references of the application library. Only `AudioDeviceService` calls the audio
factory, and only `SpeechModelService` creates and calls the model catalog; the other units use the library's
descriptor types, which are plain data, and talk to the services through `IAudioDeviceService` and
`ISpeechModelService`, so their tests use substitute services. Both services create their library objects on first
use, so starting the application neither loads the native audio library nor reads the model store. The application
does not reference the native Sherpa runtime packages: they are needed only when a speech engine is loaded, which this
build does not do, so listing and downloading models and listing devices work without them. The library has no
self-validation mode; the OTS tests in `SpeechTests.cs` call the library the way the services do, on a model store in a
temporary folder, and are the evidence that it behaves as the application relies on. They never download a model, and a
library upgrade re-runs them before it is accepted. Downloading a real model needs the network and is checked by hand
before a release.

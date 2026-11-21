### SpeechModelService

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`SpeechModelService` lists, downloads, and loads speech models through the speech library's `SpeechModelCatalog`, filled
with the models of the Sherpa package by `AddSherpaModels`. The catalog uses the library's default model store,
`%LOCALAPPDATA%\DemaConsulting.Speech\Models` on Windows, which is shared with other DEMA Consulting applications,
so a model that one application downloaded is installed for all of them. It is also the only unit that loads the speech
engine of an installed speech-to-text model. The service turns every failure into a result that the user interface can
show.

#### Data Model

**SpeechModelList**: `Models` (`IReadOnlyList<SpeechModelDescriptor>`; each has `Id`, `DisplayName`, `Role`,
`State`, and `Model`, whose `Parameters` describe the tunable settings), `Problem` (`string?`), and `IsAvailable`.
`SpeechModelList.Unavailable(problem)` creates a list with no models.

**SpeechModelDownloadStatus**: `Installed`, `Canceled`, or `Failed`.

**SpeechModelDownloadReport**: `Status`, `Message` (empty after an install), and `IsInstalled`.

**RecognizerLoadStatus**: `Loaded`, `ModelNotInstalled` (the model is not in the catalog, is not a speech-to-text
model, or is not downloaded), or `EngineUnavailable` (the engine could not be started).

**RecognizerLoadResult**: `Status`, `Engine` (the library's `ISpeechRecognizerEngine`, which the caller owns and
disposes, or `null`), `Problem` (the explanation, or an empty string), and `IsLoaded`.

**ISpeechModelService**: `Enumerate()`, `DownloadAsync(modelId, progress, cancellationToken)`, and
`LoadRecognizerAsync(modelId, storedParameters, cancellationToken)`.

#### Key Methods

**Enumerate**: Returns `SpeechModelCatalog.Enumerate()`, which reads the local store only. The catalog is created on
the first call, so creating the service at startup does not touch the file system.

**DownloadAsync**: Calls `SpeechModelCatalog.DownloadAsync`, which downloads each file of the model, verifies its
checksum, and installs the model, and reports progress through the supplied sink. An `Installed` outcome becomes an
installed report. A cancellation of the caller's token becomes a canceled report with the message
"Download canceled."; a cancellation that the caller did not ask for, such as a timeout, is a failure.

**DescribeFailure**: Maps each other library outcome to a reason and appends the library's own message when it has
one: a checksum mismatch ("The downloaded files did not match their expected checksums and were discarded."), a
blocked network, an HTTP error, a failure to write the files, and any other failure.

**Dispose**: Releases the catalog. A disposed service returns an unavailable list and failed reports.

**LoadRecognizerAsync**: (1) Finds the model in the catalog and refuses, with a `ModelNotInstalled` result that names
the reason, a model that is not known, is not a speech-to-text model, or is not downloaded; the engine is never asked
for in these cases, because loading the speech engine is the one step that can reach the native speech runtime.
(2) Converts the stored parameter values, which are invariant-culture strings, into the typed values that the library
takes, by the same rules as the model parameter editor: a number is clamped to the model's range and rounded when the
parameter is a whole number, a choice must be one of the options, a switch must be true or false, and any other value is
left out so that the model's default applies. (3) Loads the engine through the library's recognizer factory, with a
diagnostics sink that writes what the library reports to the application log, and returns it in a `Loaded` result that
the caller owns. (4) An engine that reports itself unavailable, which is how the library answers an absent or damaged
native runtime, is disposed and returned as an `EngineUnavailable` result. The caller is expected to keep the engine,
because loading it takes a noticeable time.

**Library diagnostics**: The library reports structural facts, such as a fall back to an unavailable engine, through
`ISpeechDiagnostics`. They are written to the log with their level and category. The library promises never to report
audio or recognized text through this channel.

**Substitute catalog and loader**: An internal constructor takes the function that creates the catalog, so tests use a
store in a temporary folder with substitute models, or a factory that fails, and the function that loads the engine,
so that tests never load a native speech runtime.

#### Error Handling

Failures to create or read the catalog become the problem text of an unavailable list, and any exception during a
download, including an unknown model identifier or a store that cannot be created, becomes a failed report with the
exception's message. A model that cannot be loaded, and an exception while loading, such as a damaged model file or an
invalid parameter, become a `RecognizerLoadResult` without an engine. All are written to the log as warnings. The
catches are deliberately broad: the model store is files in the user's profile, downloads cross the network, and the
native speech engine can fail in many ways, and none may stop the settings dialog or dictation. Only a cancellation
that the caller asked for is thrown from `LoadRecognizerAsync`. A blank model identifier is refused with
`ArgumentException`.

#### Dependencies

- **DemaConsulting.Speech** and **DemaConsulting.Speech.Sherpa** (OTS) — `SpeechModelCatalog`,
  `AddSherpaModels`, `SpeechModelDescriptor`, `SpeechModelDownloadResult`, `SpeechModelDownloadProgress`, the
  recognizer factory, and the diagnostics interface; see the Speech OTS design.
- **RollingFileLogger** — optional; records catalog, download, and engine problems and what the library reports.

#### Callers

- `ModelCatalogPanel` — lists the models when the page loads or refreshes, and downloads on request.
- `DictationService` — lists the models for the availability check and loads the engine of the chosen model.
- `ApplicationComposition` — creates the service and disposes it when the application ends.

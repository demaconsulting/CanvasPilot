### SpeechModelService

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`SpeechModelService` lists and downloads speech models through the speech library's `SpeechModelCatalog`, filled
with the models of the Sherpa package by `AddSherpaModels`. The catalog uses the library's default model store,
`%LOCALAPPDATA%\DemaConsulting.Speech\Models` on Windows, which is shared with other DEMA Consulting applications,
so a model that one application downloaded is installed for all of them. The service turns every failure into a
result that the user interface can show.

#### Data Model

**SpeechModelList**: `Models` (`IReadOnlyList<SpeechModelDescriptor>`; each has `Id`, `DisplayName`, `Role`,
`State`, and `Model`, whose `Parameters` describe the tunable settings), `Problem` (`string?`), and `IsAvailable`.
`SpeechModelList.Unavailable(problem)` creates a list with no models.

**SpeechModelDownloadStatus**: `Installed`, `Canceled`, or `Failed`.

**SpeechModelDownloadReport**: `Status`, `Message` (empty after an install), and `IsInstalled`.

**ISpeechModelService**: `Enumerate()` and `DownloadAsync(modelId, progress, cancellationToken)`.

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

**Substitute catalog**: An internal constructor takes the function that creates the catalog, so tests use a store in
a temporary folder with substitute models, or a factory that fails.

#### Error Handling

Failures to create or read the catalog become the problem text of an unavailable list, and any exception during a
download, including an unknown model identifier or a store that cannot be created, becomes a failed report with the
exception's message. Both are written to the log as warnings. The catches are deliberately broad: the model store is
files in the user's profile and downloads cross the network, so many kinds of failure are possible, and none may stop
the settings dialog. A blank model identifier is refused with `ArgumentException`.

#### Dependencies

- **DemaConsulting.Speech** and **DemaConsulting.Speech.Sherpa** (OTS) — `SpeechModelCatalog`,
  `AddSherpaModels`, `SpeechModelDescriptor`, `SpeechModelDownloadResult`, and `SpeechModelDownloadProgress`; see the
  Speech OTS design.
- **RollingFileLogger** — optional; records catalog and download problems.

#### Callers

- `ModelCatalogPanel` — lists the models when the page loads or refreshes, and downloads on request.
- `ApplicationComposition` — creates the service and disposes it when the application ends.

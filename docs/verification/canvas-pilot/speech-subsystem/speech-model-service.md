### SpeechModelService

#### Verification Approach

`SpeechModelService` is verified by unit tests in `SpeechModelServiceTests.cs`. The tests use the real library
catalog, created by the internal constructor's factory over a model store in a temporary folder, and fill it with
substitute models, so the real listing and the real refusal and cancellation behavior of the catalog are exercised
without the shared per-user store and without any download. A factory that throws gives a catalog that cannot be
created. A download that installs a model needs the network and is not part of the automated tests; it is checked by
hand before a release (see the Speech OTS verification). Loading a speech engine is tested with a speech-to-text model
that is made installed by writing the files that the model store looks for into the temporary store, and with a
substitute for the library's recognizer factory that the internal constructor accepts, so that no native speech runtime
is loaded; the library's own factory is exercised, without natives, by the Speech subsystem tests and the OTS tests.

#### Test Environment

Standard test environment with a temporary folder for the model store and log; no network, audio hardware, external
services, or display are required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The catalog's models are listed with role, parameters, and the install state not downloaded for an empty store.
- The catalog is created on first use only, and once.
- A cancelled token gives a canceled report and the model stays not downloaded; an unknown model gives a failed
  report that names it, and the failure is logged; a catalog that cannot be created gives an unavailable list with the
  reason, and the failure is logged.
- Every library outcome that is not an install has a reason, with the library's detail when there is one.
- A disposed service returns results instead of throwing, and a blank model identifier or a missing factory is
  refused.
- An installed speech-to-text model is loaded with its stored parameters converted to typed values within their ranges,
  invalid stored values are left out, and the engine is handed to the caller.
- A model that is unknown, of the wrong kind, or not downloaded, an engine that is unavailable or fails to load, and a
  catalog that cannot be created are results with reasons; an unavailable engine is disposed; the library's diagnostics
  and the failures are in the log; and only the caller's cancellation is thrown.

#### Test Scenarios

**SpeechModelService_Enumerate_CatalogWithModels_ListsModelsWithState**: A catalog with a speech-to-text and a
text-to-speech model that has a choice parameter is listed. Both models appear in order with their roles, both are
not downloaded, and the second model's parameter is reachable. This scenario is tested by
`SpeechModelService_Enumerate_CatalogWithModels_ListsModelsWithState`.

**SpeechModelService_Enumerate_CatalogCannotBeCreated_ReturnsUnavailableWithProblem**: The catalog factory throws an
I/O error. The list is empty and unavailable, its problem contains the error's message, and the log holds "Listing the
speech models failed." This scenario is tested by
`SpeechModelService_Enumerate_CatalogCannotBeCreated_ReturnsUnavailableWithProblem`.

**SpeechModelService_Catalog_IsCreatedOnFirstUseOnly**: The factory counts its calls. After the service is created the
count is zero; after two listings it is one. This scenario is tested by
`SpeechModelService_Catalog_IsCreatedOnFirstUseOnly`.

**SpeechModelService_DownloadAsync_CancelledToken_ReturnsCanceled**: A download of a known model is requested with a
token that is already cancelled. The report is canceled with the message "Download canceled.", it is not installed, and
the model is still not downloaded. This scenario is tested by
`SpeechModelService_DownloadAsync_CancelledToken_ReturnsCanceled`.

**SpeechModelService_DownloadAsync_UnknownModel_ReturnsFailedWithMessage**: A model that the catalog does not know is
requested. The report is failed, its message names the model, and the log holds "Downloading the speech model
'missing' failed." This scenario is tested by `SpeechModelService_DownloadAsync_UnknownModel_ReturnsFailedWithMessage`.

**SpeechModelService_DescribeFailure_EachOutcome_GivesReasonAndDetail**: For each of the checksum mismatch, blocked
network, HTTP error, I/O failure, and other failed outcomes, a result with an error and one without are described. The
reason for the outcome is always present, and the error's message is appended only when there is one. This scenario is
tested by `SpeechModelService_DescribeFailure_EachOutcome_GivesReasonAndDetail`.

**SpeechModelDownloadReport_IsInstalled_OnlyForInstalledStatus**: A report is installed only for the installed
status. This scenario is tested by `SpeechModelDownloadReport_IsInstalled_OnlyForInstalledStatus`.

**SpeechModelService_Dispose_RefusesFurtherUseWithResults**: A service that was used is disposed twice. Listing then
gives an unavailable list and a download gives a failed report. This scenario is tested by
`SpeechModelService_Dispose_RefusesFurtherUseWithResults`.

**SpeechModelService_InvalidArguments_Throw**: A missing catalog factory and a blank model identifier are refused.
This scenario is tested by `SpeechModelService_InvalidArguments_Throw`.

**SpeechModelService_LoadRecognizerAsync_InstalledModel_ReturnsEngineWithTypedParameters**: A speech-to-text model with
a whole-number, a choice, and a boolean parameter is installed in the temporary store, and the loader that stands in for
the library's recognizer factory records what it receives. Stored values of 99.6, "c", and "true" are loaded. The result
is loaded with the engine the loader returned, and the loader received 10 (clamped and rounded), the text "c", and true.
This scenario is tested by `SpeechModelService_LoadRecognizerAsync_InstalledModel_ReturnsEngineWithTypedParameters`.

**SpeechModelService_ToParameterValues_InvalidOrMissingValues_AreLeftOut**: Stored values that are not numbers, are not
finite, are not an option, or are not true or false are converted, and so are valid values, an unknown parameter, no
values, and an empty set of values. Invalid values are left out and no values at all is no bag; valid ones are typed,
and a whole number is rounded away from zero. This scenario is tested by
`SpeechModelService_ToParameterValues_InvalidOrMissingValues_AreLeftOut`.

**SpeechModelService_LoadRecognizerAsync_UnknownWrongKindOrNotDownloaded_ReturnsModelNotInstalled**: A model identifier
that the catalog does not know, a text-to-speech model, and a speech-to-text model that is not downloaded are loaded.
Each result is a model that is not installed with its own explanation ("is not known", "not a speech-to-text model",
and "not downloaded"), and the loader was never called. This scenario is tested by
`SpeechModelService_LoadRecognizerAsync_UnknownWrongKindOrNotDownloaded_ReturnsModelNotInstalled`.

**SpeechModelService_LoadRecognizerAsync_EngineUnavailableOrLoaderThrows_ReturnsEngineUnavailable**: An installed model
is loaded twice, the loader first returning an engine that reports itself unavailable and then throwing. Both results
are an unavailable engine, the first explains that the speech runtime may be missing and its engine was disposed, the
second carries the failure's message, and the failure is in the log. This scenario is tested by
`SpeechModelService_LoadRecognizerAsync_EngineUnavailableOrLoaderThrows_ReturnsEngineUnavailable`.

**SpeechModelService_LoadRecognizerAsync_LibraryDiagnostics_AreWrittenToTheLog**: The loader reports a warning with a
category and a message through the diagnostics sink it is given. The log holds the category and the message. This
scenario is tested by `SpeechModelService_LoadRecognizerAsync_LibraryDiagnostics_AreWrittenToTheLog`.

**SpeechModelService_LoadRecognizerAsync_CancelledOrCatalogBroken_CancelsOrReturnsProblem**: A load with a token that is
already cancelled ends with a cancellation, a load from a service whose catalog cannot be created is a model that is not
installed with the store's message, and a blank identifier is refused. This scenario is tested by
`SpeechModelService_LoadRecognizerAsync_CancelledOrCatalogBroken_CancelsOrReturnsProblem`.

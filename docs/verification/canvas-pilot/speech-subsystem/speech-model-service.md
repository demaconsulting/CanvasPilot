### SpeechModelService

#### Verification Approach

`SpeechModelService` is verified by unit tests in `SpeechModelServiceTests.cs`. The tests use the real library
catalog, created by the internal constructor's factory over a model store in a temporary folder, and fill it with
substitute models, so the real listing and the real refusal and cancellation behavior of the catalog are exercised
without the shared per-user store and without any download. A factory that throws gives a catalog that cannot be
created. A download that installs a model needs the network and is not part of the automated tests; it is checked by
hand before a release (see the Speech OTS verification).

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

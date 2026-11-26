### ModelCatalogPanel

#### Verification Approach

`ModelCatalogPanel` is verified at two levels. Unit tests in `ModelCatalogPanelTests.cs` drive
`ModelCatalogPanelViewModel` and `ModelListItemViewModel` over substitute speech model services (NSubstitute)
whose downloads are held open with a task completion source, report progress when a test asks, or wait for their
token to be cancelled, so each stage of a download can be observed without a network or a delay. The progress sink is
replaced by one that runs inline, so progress is seen at once. Headless UI tests in `SpeechSettingsPageViewTests.cs`
(the `UiTests` project, using Avalonia's headless platform and the hand-written `FakeSpeechModelService`) show the
real view, press its buttons through their bound commands, and read the controls by automation identifier.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled. No network, display, or model store is required, and nothing
is downloaded.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The lists are split by role with the stored models selected, the state of each model is readable, and a stored
  identifier that the list cannot show is kept and still reported.
- A catalog that cannot be read and a catalog with no models are explained in text.
- A download shows its state, progress, and end state in the model's row, a failure is explained and can be
  retried, a service that throws does not escape, and an installed model starts no download.
- Canceling one download or all downloads cancels the tokens and returns the models to not downloaded with the
  message "Download canceled."
- A refresh is refused with an explanation while a download runs, and otherwise reads the catalog again and keeps the
  chosen models.

#### Test Scenarios

**ModelCatalogPanelViewModel_Load_CatalogWithModels_SplitsByRoleAndSelectsStoredModels**: A catalog with two
speech-to-text models and one text-to-speech model, one installed, is loaded with stored choices. The lists are split,
the stored models are selected, the identifier reports the stored model, and the states read "Installed" and "Not
downloaded". This scenario is tested by
`ModelCatalogPanelViewModel_Load_CatalogWithModels_SplitsByRoleAndSelectsStoredModels`.

**ModelCatalogPanelViewModel_Load_StoredModelNotListed_KeepsStoredIdentifier**: Models that the catalog does not list
are stored. Nothing is selected and the identifiers are still reported. This scenario is tested by
`ModelCatalogPanelViewModel_Load_StoredModelNotListed_KeepsStoredIdentifier`.

**ModelCatalogPanelViewModel_SelectModel_ChangesReportedIdentifier**: The user selects the other speech-to-text
model and the reported identifier follows. This scenario is tested by
`ModelCatalogPanelViewModel_SelectModel_ChangesReportedIdentifier`.

**ModelCatalogPanelViewModel_Load_CatalogUnavailable_ShowsProblemAndKeepsStoredIdentifiers**: The service reports a
catalog that cannot be read. The message shows the problem, the lists are empty, and the stored identifiers are kept.
This scenario is tested by `ModelCatalogPanelViewModel_Load_CatalogUnavailable_ShowsProblemAndKeepsStoredIdentifiers`.

**ModelCatalogPanelViewModel_Load_EmptyCatalog_ShowsEmptyMessage**: A catalog with no models shows the empty-catalog
message. This scenario is tested by `ModelCatalogPanelViewModel_Load_EmptyCatalog_ShowsEmptyMessage`.

**ModelCatalogPanelViewModel_Download_Succeeds_ShowsProgressThenInstalled**: A download is started and held open. The
row is downloading, cannot be downloaded again, and the panel reports a running download. Half of the bytes are
reported and the row shows 0.5. The download then ends as installed, and the row is installed with progress 1, no
failure, and no running download. This scenario is tested by
`ModelCatalogPanelViewModel_Download_Succeeds_ShowsProgressThenInstalled`.

**ModelCatalogPanelViewModel_Download_Fails_ShowsFailureAndAllowsRetry**: A download ends as failed with a message.
The row is failed or corrupt with the message, has no progress, and can be downloaded again. This scenario is tested
by `ModelCatalogPanelViewModel_Download_Fails_ShowsFailureAndAllowsRetry`.

**ModelCatalogPanelViewModel_Download_ServiceThrows_ShowsFailure**: A service that throws instead of returning a
report gives a failed row with the exception's message. This scenario is tested by
`ModelCatalogPanelViewModel_Download_ServiceThrows_ShowsFailure`.

**ModelCatalogPanelViewModel_Download_InstalledModelOrNoRow_DoesNothing**: Downloading an installed model, no row,
and canceling no row do nothing, and the service is never asked to download. This scenario is tested by
`ModelCatalogPanelViewModel_Download_InstalledModelOrNoRow_DoesNothing`.

**ModelCatalogPanelViewModel_CancelDownload_RunningDownload_CancelsAndRestoresRow**: A download that waits for its
token is cancelled through the cancel command. The row is not downloaded with the message "Download canceled.", can be
downloaded again, and nothing runs. This scenario is tested by
`ModelCatalogPanelViewModel_CancelDownload_RunningDownload_CancelsAndRestoresRow`.

**ModelCatalogPanelViewModel_CancelAllDownloads_RunningDownloads_CancelsEach**: Two running downloads are cancelled
together and both rows return to not downloaded. This scenario is tested by
`ModelCatalogPanelViewModel_CancelAllDownloads_RunningDownloads_CancelsEach`.

**ModelCatalogPanelViewModel_Refresh_WhileDownloading_IsRefusedOtherwiseRereads**: A refresh while a download runs
shows the busy message and does not read the catalog. After the download is cancelled, a refresh reads the catalog
once more, keeps the chosen model, and clears the message. This scenario is tested by
`ModelCatalogPanelViewModel_Refresh_WhileDownloading_IsRefusedOtherwiseRereads`.

**ModelCatalogPanelViewModel_New_NullService_Throws**: A missing service and a missing catalog entry are refused. This
scenario is tested by `ModelCatalogPanelViewModel_New_NullService_Throws`.

**SpeechSettingsPageView_LoadedSettings_ShowDevicesModelsAndParameters**: A page is shown over a catalog with one
model of each kind, the first installed. Each list holds one model, the stored speech-to-text model is selected, and
the states read "Installed" and "Not downloaded". This scenario is tested by
`SpeechSettingsPageView_LoadedSettings_ShowDevicesModelsAndParameters`.

**SpeechSettingsPageView_NothingAvailable_ShowsExplanations**: A page is shown over a catalog that cannot be read. The
message line is visible and shows the problem. This scenario is tested by
`SpeechSettingsPageView_NothingAvailable_ShowsExplanations`.

**SpeechSettingsPageView_DownloadButton_ShowsProgressThenInstalled**: The Download button is pressed. The progress bar
is visible, the button is disabled, and the row reads "Downloading"; after half of the bytes are reported the bar
shows 0.5; when the download ends the bar is hidden and the row reads "Installed". This scenario is tested by
`SpeechSettingsPageView_DownloadButton_ShowsProgressThenInstalled`.

**SpeechSettingsPageView_DownloadFails_ShowsFailureAndAllowsRetry**: A download ends as failed. The failure line shows
the message, the Download button is enabled, and the row reads "Failed or corrupt". This scenario is tested by
`SpeechSettingsPageView_DownloadFails_ShowsFailureAndAllowsRetry`.

**SpeechSettingsPageView_CancelButton_StopsDownloadAndExplains**: The Cancel button, visible while downloading, is
pressed. The token is cancelled, the Download button is enabled, the row reads "Not downloaded", and the failure line
reads "Download canceled." This scenario is tested by `SpeechSettingsPageView_CancelButton_StopsDownloadAndExplains`.

**ModelListItemViewModel_ToString_IsTheDisplayName**: The text of a row is the model name that it shows and not the
name of its type. This scenario is tested by `ModelListItemViewModel_ToString_IsTheDisplayName`.

**SpeechSettingsPageView_ModelLists_RowsAreNamedByTheModelName**: A shown Speech page over two recognition models and
one synthesis model; the name that automation gives each row of both lists is the model name. This scenario is tested
by `SpeechSettingsPageView_ModelLists_RowsAreNamedByTheModelName`.

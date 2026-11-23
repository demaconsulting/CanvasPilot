### ModelCatalogPanel

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`ModelCatalogPanel` is the view model and view of the speech model lists on the Speech page. It lists the
speech-to-text and the text-to-speech models separately, lets the user choose one of each kind, and downloads models
with progress, cancellation, and an explanation of failures. Each row is a `ModelListItem`.

#### Data Model

**ModelListItem** (`ModelListItemViewModel`): `Model` (the library model, whose parameters the parameter editor
shows), `Id`, `DisplayName`, `Role`, and the observable `State`, `ProgressFraction` (`double?`, 0 to 1 for the file
being transferred), and `FailureMessage`, with the derived `StatusText` ("Not downloaded", "Downloading",
"Installed", or "Failed or corrupt"), `CanDownload` (not downloaded, or failed, so a download can be retried),
`IsDownloading`, and `HasFailureMessage`.

**SttModels / TtsModels**: `ObservableCollection<ModelListItemViewModel>` split by `SpeechModelRole`.

**SelectedSttModel / SelectedTtsModel**: the chosen rows, with `SttModelId` and `TtsModelId`. When the list cannot show
the stored model, the identifier that was stored is kept and reported.

**Message / HasMessage**: `string` — the catalog's problem text, the empty-catalog text, or the text that explains a
refused refresh.

**IsDownloading**: whether any download runs.

**View**: `ModelCatalogPanelView` is an Avalonia user control with a section title (`Speech models`) and an explanation
in the hint look, two labeled list boxes (`SpeechSttModelList`, `SpeechTtsModelList`, named `Speech to text models` and
`Text to speech models`), a row template with the name of the model, `SpeechModelStatus` (hint look),
`SpeechModelProgress`, `SpeechModelFailure` (error look), `SpeechModelDownloadButton`, and `SpeechModelCancelButton`
(secondary look, at least 28 high), a message line in the error look (`SpeechModelsMessage`), and a Refresh models
button (`SpeechRefreshModelsButton`). The rows keep the stock list item look. The settings page puts the view in a
raised card; the view writes no color.

#### Key Methods

**Load**: Remembers the stored identifiers, cancels downloads that are running, reads the catalog through
`ISpeechModelService.Enumerate`, rebuilds the lists, and selects the stored models.

**DownloadAsync** (command, with the row as its parameter): Does nothing for a missing row or a model that cannot be
downloaded. Otherwise it sets the row to downloading, creates a progress sink that runs on the user interface
thread, and calls `ISpeechModelService.DownloadAsync` with a token of its own. The report decides the row's end
state: installed; not downloaded, with the message "Download canceled."; or failed, with the message. Progress sets
`ProgressFraction` from the library's per-file fraction. An exception from the service, which should not happen, is
shown as a failure.

**CancelDownload** (command, with the row as its parameter): Cancels the token of that row's download.

**CancelAllDownloads**: Cancels every running download; used when the dialog closes and when the panel is loaded.
The running downloads are kept in a concurrent dictionary because, where there is no synchronization context (as in
tests), downloads that were cancelled together end on different threads and remove themselves at the same time.

**Refresh** (command): Reads the catalog again, keeping the chosen models. It is refused with an explanation while
a download runs, because rebuilding the rows would discard the rows that show the downloads.

#### Error Handling

Failures are shown in the row of the model that failed, or in `Message`; none is thrown. A missing service is
refused with `ArgumentNullException`. The catch around the service call is deliberate: a download crosses the
network and the file system, and a surprise from the service must not take the settings dialog down.

#### Dependencies

- **SpeechModelService** — through `ISpeechModelService`; the library's `SpeechModelDescriptor`, `SpeechModelState`,
  `SpeechModelRole`, and `SpeechModelDownloadProgress` are the model types.
- **CommunityToolkit.Mvvm** and **Avalonia** — property change notification, the commands, and the view.

#### Callers

- `SpeechSettingsPage` — composes the unit, loads it, cancels its downloads on close, and reads the chosen
  identifiers and models.

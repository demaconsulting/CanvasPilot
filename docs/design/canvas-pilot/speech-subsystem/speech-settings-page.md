### SpeechSettingsPage

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`SpeechSettingsPage` is the "Speech" page of the settings dialog. It composes the device pickers, the model catalog
panel, and the parameter editors of the chosen models, and maps them to and from the `SpeechSettings` record. The
values are persisted and read through `ISettingsService` by `DictationService`, which loads the chosen model and
opens the chosen microphone. Model downloads are not part of OK or Cancel: they start when the user asks, and the
page cancels the ones that are still running when the dialog closes.

#### Data Model

**Title**: `Speech`. **Description**: `The microphone and speakers, and the models that turn speech into text and text
into speech.` **IconKey**: `CpIconMic`; the settings dialog shows them in its list of pages and above the page.
**IsValid**: always `true`, because the page offers only choices and has nothing to mistype.

**Devices**: a `DeviceSelectionViewModel`. **Models**: a `ModelCatalogPanelViewModel`. **SttParameters /
TtsParameters**: `ModelParameterEditorViewModel`s for the chosen speech-to-text and text-to-speech models.

**Parameter values**: the page keeps a working copy of `SpeechSettings.ModelParameters` (model identifier, then
parameter identifier, then the value as an invariant-culture string). The editors write into it when the chosen
model changes and when the page is applied.

**View**: `SpeechSettingsPageView` is an Avalonia user control that stacks the views of the four view models, which
the view locator finds, in a scrolling panel, each in a raised card (`SpeechDevices`, `SpeechModels`,
`SpeechSttParameters`, and `SpeechTtsParameters`; the identifiers did not change). It is bound to the page by the view
locator. The look of the part views is the shared look of the settings pages: a section title, labels, hints, and error
lines from the shared text styles, secondary buttons, and no color written in a view. The part views only gained names
for their entries and these styles, and no behavior: the list of microphones is named `Microphone`, the list of speakers
`Speakers`, and the two lists of models `Speech to text models` and `Text to speech models`; the failure of a download
and the message about the catalog are in the error look, and the status lines in the hint look. The rows of the model
lists keep the stock list item look.

#### Key Methods

**Load**: Frees the editors so values of an earlier dialog session cannot be kept, copies the stored parameter
values, loads the device pickers with the stored names, and loads the model panel with the stored identifiers, which
shows the parameters of the chosen models with their stored values.

**Apply**: Stores the values the editors show, and returns the settings with the `Speech` section replaced:
the chosen device names, the chosen model identifiers, and the parameter values (including those of other models that
were stored), with every other section unchanged. An identifier or name that the page could not show is kept as it was
stored.

**Choosing a model**: When the chosen model of a kind changes, the page stores the values the editor shows under the
old model's identifier and shows the new model's parameters with the values stored for it.

**OnClosed**: Cancels every running download.

#### Error Handling

The page has no failure of its own: problems with the audio system or the model store appear as text in the device
and model units, and the stored choices survive them. Missing arguments are refused with `ArgumentNullException`.

#### Dependencies

- **SettingsDialog** — hosts the page through `ISettingsPage`.
- **SettingsService** — owns the `AppSettings` and `SpeechSettings` records.
- **DeviceSelection**, **ModelCatalogPanel**, **ModelParameterEditor** — the units the page composes.
- **CommunityToolkit.Mvvm** and **Avalonia** — property change notification and the view.

#### Callers

- The application composes this page into the settings dialog, after the AI Provider page.

### SpeechSettingsPage

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`SpeechSettingsPage` is the "Speech" page of the settings dialog. It composes the device pickers, the model catalog
panel, and the parameter editors of the chosen models, and maps them to and from the `SpeechSettings` record. The
values are persisted only; nothing in the application consumes them yet, and a later speech feature reads them
through `ISettingsService`. Model downloads are not part of OK or Cancel: they start when the user asks, and the
page cancels the ones that are still running when the dialog closes.

#### Data Model

**Title**: `Speech`. **IsValid**: always `true`, because the page offers only choices and has nothing to mistype.

**Devices**: a `DeviceSelectionViewModel`. **Models**: a `ModelCatalogPanelViewModel`. **SttParameters /
TtsParameters**: `ModelParameterEditorViewModel`s for the chosen speech-to-text and text-to-speech models.

**Parameter values**: the page keeps a working copy of `SpeechSettings.ModelParameters` (model identifier, then
parameter identifier, then the value as an invariant-culture string). The editors write into it when the chosen
model changes and when the page is applied.

**View**: `SpeechSettingsPageView` is an Avalonia user control that stacks the views of the four view models, which
the view locator finds, in a scrolling panel. It is bound to the page by the view locator.

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

### SpeechSettingsPage

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`SpeechSettingsPage` is the "Speech" page of the settings dialog. It lets the user choose the key or chord that is held
to talk, composes the device pickers, the model catalog
panel, and the parameter editors of the chosen models, and maps them to and from the `SpeechSettings` record. The
values are persisted and read through `ISettingsService` by `DictationService`, which loads the chosen model and
opens the chosen microphone, and by the `PushToTalkSetting`, which follows the chosen key. Model downloads are not part
of OK or Cancel: they start when the user asks, and the
page cancels the ones that are still running when the dialog closes.

#### Data Model

**Title**: `Speech`. **Description**: `The microphone and speakers, the key you hold to talk, and the models that turn
speech into text and text into speech.` **IconKey**: `CpIconMic`; the settings dialog shows them in its list of pages
and above the page.
**IsValid**: always `true`, because the page offers only choices and a key that cannot be used is refused where it is
pressed, so there is nothing to mistype.

**Push-to-talk key** (`PushToTalkKeyText`, `PushToTalkProblem` / `HasPushToTalkProblem`, `IsPushToTalkDefault`,
`ResetPushToTalkCommand`, and the static `ResetPushToTalkText`): the page keeps the chosen key as a `PushToTalkBinding`
(F9 until another is taken). `PushToTalkKeyText` is its text, for example `F9` or `Ctrl+Shift+Space`.
`PushToTalkProblem` is the sentence that says why the key that was last pressed was refused and what to do, and is empty
otherwise. `IsPushToTalkDefault` tells whether the key is F9. `ResetPushToTalkCommand` puts F9 back and can run only
while the key is not F9; `ResetPushToTalkText` is the label of its button, "Reset to default (F9)", which names the key
from `PushToTalkBinding.Default`.

**Devices**: a `DeviceSelectionViewModel`. **Models**: a `ModelCatalogPanelViewModel`. **SttParameters /
TtsParameters**: `ModelParameterEditorViewModel`s for the chosen speech-to-text and text-to-speech models.

**Parameter values**: the page keeps a working copy of `SpeechSettings.ModelParameters` (model identifier, then
parameter identifier, then the value as an invariant-culture string). The editors write into it when the chosen
model changes and when the page is applied.

**View**: `SpeechSettingsPageView` is an Avalonia user control that stacks the card of the talk key and the views of the
four view models, which the view locator finds, in a scrolling panel, each in a raised card (`SpeechDevices`,
`SpeechModels`, `SpeechSttParameters`, and `SpeechTtsParameters`; the identifiers did not change). It is bound to the
page by the view locator. The card `Push-to-talk key` comes first. It has a short hint ("Hold this key anywhere in the
window to dictate a message, and let go to add the words to it. Click the box and press the key, or the keys, that you
want."), a read-only text box that shows the key (`SpeechPushToTalkKeyBox`, accessible name `Push-to-talk key`), the
secondary button that puts the default back (`SpeechPushToTalkResetButton`), and the sentence of a refused key
(`SpeechPushToTalkProblem`, in the caution look, visible only while there is one). The code behind of the view adds one
handler to the box, for the key-down event on the tunneling route, so that the box sees every key before it would edit
its text or move the focus; the handler gives the key and the modifiers to `TryCapturePushToTalk` and marks the event
handled whatever the answer, so that Space, the arrow keys, and Tab do not act as keys of the page while the box has the
focus (the box is left by clicking elsewhere). The look of the part views is the shared look of the settings pages: a
section title, labels, hints, and error lines from the shared text styles, secondary buttons, and no color written in a
view. The part views only gained names for their entries and these styles, and no behavior: the list of microphones is
named `Microphone`, the list of speakers `Speakers`, and the two lists of models `Speech to text models` and
`Text to speech models`; the failure of a download and the message about the catalog are in the error look, and the
status lines in the hint look. The rows of the model lists keep the stock list item look.

#### Key Methods

**Load**: Frees the editors so values of an earlier dialog session cannot be kept, copies the stored parameter
values, loads the device pickers with the stored names, and loads the model panel with the stored identifiers, which
shows the parameters of the chosen models with their stored values. It also clears the refusal sentence and takes the
stored talk key through `PushToTalkBinding.FromStored`, so that an empty or unsafe stored value shows as F9.

**Apply**: Stores the values the editors show, and returns the settings with the `Speech` section replaced:
the chosen device names, the chosen model identifiers, the talk key as `PushToTalkBinding.ToStored` (empty for F9, so
that the default can follow a later release, and otherwise the text of the chord), and the parameter values (including
those of other models that
were stored), with every other section unchanged. An identifier or name that the page could not show is kept as it was
stored. The talk key is therefore applied on OK or Apply like every other choice, and takes effect, in the window and in
every hint, when the settings service announces the change.

**TryCapturePushToTalk(key, modifiers)**: Takes a key that the user pressed in the box and answers whether it became the
talk key. A modifier alone (Ctrl, Alt, Shift, or the Windows key) is waited out: nothing changes, and the box waits for
the key that goes with it. Esc with no modifier cancels the capture: the key stays as it was and the refusal sentence is
cleared. Any other press is offered to `PushToTalkBinding.TryCreate` with the modifiers that were held; when it is
refused the sentence is shown in `PushToTalkProblem` and the key stays as it was, and when it is accepted it becomes the
key (`SetPushToTalk`), the sentence is cleared, and the text and the state of the reset button are announced. The answer
is `true` only in the last case.

**ResetPushToTalk**: The command of the reset button: makes F9 the key through the same step, which clears the sentence
and announces the text and the reset button's availability.

**Choosing a model**: When the chosen model of a kind changes, the page stores the values the editor shows under the
old model's identifier and shows the new model's parameters with the values stored for it.

**OnClosed**: Cancels every running download.

#### Error Handling

The page has no failure of its own: problems with the audio system or the model store appear as text in the device and
model units, and the stored choices survive them. A talk key that cannot be used is not an error but a refusal: the
sentence of `PushToTalkBinding` is shown next to the box and the key that was chosen before stays. A stored key that is
not safe shows as F9 and is stored as the default by the next Apply. Missing arguments are refused with
`ArgumentNullException`.

**Known limitation**: the box takes every key, Tab included, so a keyboard-only user who has put the focus in it leaves
it by clicking elsewhere; a refused key never changes the chosen key.

#### Dependencies

- **SettingsDialog** — hosts the page through `ISettingsPage`.
- **SettingsService** — owns the `AppSettings` and `SpeechSettings` records.
- **PushToTalkBinding** — the rules that accept or refuse a pressed key, the text of the key, and the stored form.
- **DeviceSelection**, **ModelCatalogPanel**, **ModelParameterEditor** — the units the page composes.
- **CommunityToolkit.Mvvm** and **Avalonia** — property change notification, relay commands, the key types, and the
  view.

#### Callers

- The application composes this page into the settings dialog, after the AI Provider page.

## Speech

![Speech Structure](SpeechSubsystemView.svg)

### Overview

The `Speech` subsystem lets the user choose the audio devices and speech models that the application will use for
speaking and listening, and download the models. Its boundary starts with the audio system and the model store of
the DemaConsulting.Speech library and ends with the "Speech" page of the settings dialog and the speech section
of the settings file. It contains six units: `AudioDeviceService`, which lists the audio devices; `SpeechModelService`,
which lists and downloads the speech models; and four units that make up the page, `DeviceSelection` (the device
pickers), `ModelCatalogPanel` (the model lists and downloads), `ModelParameterEditor` (the tunable settings of a
model), and `SpeechSettingsPage`, which composes the other three and plugs into the settings dialog. The choices
are stored only; nothing in the application consumes them yet, and the speech engines, which need the native
Sherpa runtime, are not part of this build. The speech library is used in exactly two units, the two services, so
the page and its tests work with substitute services.

### Interfaces

**Audio Device API**: The in-process interface through which audio devices are listed.

- *Type*: In-process .NET API (`IAudioDeviceService`).
- *Role*: Provider.
- *Contract*: `Enumerate` and `Refresh` return an `AudioDeviceSnapshot` with the capture and playback devices
  (name, direction, channel count, and sample rate) and, when the audio system could not be used, a problem text.
  `Refresh` first makes the audio system scan for changed hardware.
- *Constraints*: Neither operation throws. A machine without devices is a snapshot with empty lists and no problem.

**Speech Model API**: The in-process interface through which models are listed and downloaded.

- *Type*: In-process .NET API (`ISpeechModelService`).
- *Role*: Provider.
- *Contract*: `Enumerate` returns a `SpeechModelList` of the catalog's models with role, parameters, and install
  state, or a problem text; `DownloadAsync(modelId, progress, cancellationToken)` returns a
  `SpeechModelDownloadReport` that says installed, canceled, or failed, with a message.
- *Constraints*: Failures are results, never exceptions. `Enumerate` does not use the network.

**Settings Page Contract**: How the page plugs into the settings dialog.

- *Type*: In-process .NET API (`ISettingsPage` of the `Settings` subsystem).
- *Role*: Consumer.
- *Contract*: `Load` shows the `SpeechSettings` of the settings in effect, `Apply` returns settings whose speech
  section holds the page's choices and whose other values are unchanged, and `OnClosed` is called when the dialog
  closes.

**Model Store**: The local files of downloaded models.

- *Type*: File system, through the speech library.
- *Role*: Consumer.
- *Contract*: The library's default store, `%LOCALAPPDATA%\DemaConsulting.Speech\Models` on Windows, which is
  shared with other DEMA Consulting applications. Downloads fetch the models' files over HTTPS and verify their
  checksums before installing them.
- *Constraints*: A model is installed completely or not at all.

**Speech Settings Page View**: The user interface.

- *Type*: Avalonia control (`SpeechSettingsPageView`) shown by the settings dialog.
- *Role*: Provider.
- *Contract*: Two drop-down lists for the microphone and speakers with a Refresh devices button; a list of
  speech-to-text models and a list of text-to-speech models, each row with its state, a Download button, a
  progress bar, and a Cancel button while downloading; and the settings of the chosen model of each kind.

### Design

1. The application composes one `AudioDeviceService` and one `SpeechModelService`, which touch neither the audio
   system nor the model store until they are first used, and one `SpeechSettingsPageViewModel` over them, which it
   adds to the settings dialog's pages after the AI Provider page.
2. When the dialog opens, the page loads. `DeviceSelection` lists the devices and selects the stored ones by name;
   `ModelCatalogPanel` lists the models by role and selects the stored ones by identifier; the page then shows the
   parameters of the chosen models in two `ModelParameterEditor`s, with the stored values.
3. A download starts at once when the user presses Download and is not part of OK or Cancel. Progress is shown in
   the model's row, and the row shows the installed state, or the failure's explanation, when the download ends.
4. When the user chooses another model, the values edited for the previous one are kept in the page, and the other
   model's parameters are shown with their stored values.
5. On OK or Apply the page merges its choices into the speech section of the settings. When the dialog closes by any
   route, the page cancels every download that is still running.

A choice that the page cannot show, for example a stored device that is not connected or a stored model while the
catalog cannot be read, is kept and stored again rather than erased.

### Third-party items

The subsystem consumes the `DemaConsulting.Speech` library, with its `DemaConsulting.Speech.Sherpa` model package,
in `AudioDeviceService` and `SpeechModelService` and for the descriptor types that the other units show (see the
Speech OTS design), and Avalonia and CommunityToolkit.Mvvm in the view models and views.

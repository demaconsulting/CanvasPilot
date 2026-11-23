## Speech

![Speech Structure](SpeechSubsystemView.svg)

### Overview

The `Speech` subsystem lets the user choose the audio devices and speech models that the application uses for
speaking and listening, download the models, and dictate: turn what the user says while holding a talk control into
text. Its boundary starts with the audio system, the model store, and the recognition engine of the
DemaConsulting.Speech library and ends with the "Speech" page of the settings dialog, the speech section of the
settings file, and the text that dictation returns. It contains seven units: `AudioDeviceService`, which lists the audio
devices and creates the capture device of the chosen microphone; `SpeechModelService`, which lists and downloads the
speech models and loads the engine of an installed speech-to-text model; `DictationService`, which listens while the
talk control is held and returns the text; and four units that make up the page, `DeviceSelection` (the device
pickers), `ModelCatalogPanel` (the model lists and downloads), `ModelParameterEditor` (the tunable settings of a
model), and `SpeechSettingsPage`, which composes the other three and plugs into the settings dialog. The talk button
of the chat panel and the talk key (F8) of the main window call the dictation service and show the words. This
subsystem provides what they call. The native speech runtime, which the speech engine needs, arrives with the library's
packages and is shipped beside the application and in the installer; `DictationService` checks that it is there before
it loads an engine, because an engine started without it can crash the process. The speech library is used in exactly
three units, the two services and `DictationService`, so the page and its tests work with substitute services.

### Interfaces

**Audio Device API**: The in-process interface through which audio devices are listed.

- *Type*: In-process .NET API (`IAudioDeviceService`).
- *Role*: Provider.
- *Contract*: `Enumerate` and `Refresh` return an `AudioDeviceSnapshot` with the capture and playback devices
  (name, direction, channel count, and sample rate) and, when the audio system could not be used, a problem text.
  `Refresh` first makes the audio system scan for changed hardware. `CreateCaptureDevice(deviceName)` returns a
  `CaptureDeviceResult` with the capture device of the named microphone, or of the system default, in the mono format
  of speech models, or no device and an explanation.
- *Constraints*: No operation throws. A machine without devices is a snapshot with empty lists and no problem.

**Speech Model API**: The in-process interface through which models are listed, downloaded, and loaded.

- *Type*: In-process .NET API (`ISpeechModelService`).
- *Role*: Provider.
- *Contract*: `Enumerate` returns a `SpeechModelList` of the catalog's models with role, parameters, and install
  state, or a problem text; `DownloadAsync(modelId, progress, cancellationToken)` returns a
  `SpeechModelDownloadReport` that says installed, canceled, or failed, with a message;
  `LoadRecognizerAsync(modelId, storedParameters, cancellationToken)` returns a `RecognizerLoadResult` with the loaded
  speech engine, which the caller owns, or the reason there is none.
- *Constraints*: Failures are results, never exceptions, except that a cancellation by the caller is thrown by
  `LoadRecognizerAsync`. `Enumerate` does not use the network.

**Dictation API**: The in-process interface through which the chat turns speech into text.

- *Type*: In-process .NET API (`IDictationService` and `IDictationSession`).
- *Role*: Provider.
- *Contract*: `CheckAvailability` returns a `DictationAvailability` that says whether dictation can be used and, when it
  cannot, the reason and a message with the next step; `PrepareAsync` loads the speech engine ahead of time;
  `BeginAsync` starts listening and returns a `DictationStart` with the listening session or the reason there is none.
  The session reports the words recognized so far through `PartialText` and `PartialTextChanged`, and `EndAsync` stops
  listening and returns the final text.
- *Constraints*: Nothing throws except a cancellation by the caller. A request to begin while a session is listening is
  ignored. The recognized text is never written to the log.

**Speech Settings Read**: How dictation learns the user's choices.

- *Type*: In-process .NET API (`ISettingsService` of the `Settings` subsystem).
- *Role*: Consumer.
- *Contract*: `Current.Speech` gives the chosen speech-to-text model, its stored parameter values, and the chosen
  microphone, which `DictationService` reads for every request, so a change made in the settings dialog is used by the
  next hold.

**Native Speech Runtime**: The native files that the speech engine runs on.

- *Type*: Native libraries beside the application, delivered by the packages of the speech library.
- *Role*: Consumer.
- *Contract*: The inference runtime and the Sherpa interface to it, and the audio library, are in the application's
  folder in the published and installed application and in the runtime folder of a development build.
- *Constraints*: `DictationService` loads no engine unless the first two are present, because an engine started
  without them can crash the process.

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

**Microphone**: The user's audio input.

- *Type*: Operating system audio input, through the speech library's audio device factory.
- *Role*: Consumer.
- *Contract*: Audio is captured only while a dictation session is listening, from the microphone chosen in the settings
  or the system default; the capture ends and the device is released as soon as the session ends.
- *Constraints*: Windows can block desktop applications from using the microphone; dictation then reports that no
  microphone could be used and names the permission as something to check.

**Speech Settings Page View**: The user interface.

- *Type*: Avalonia control (`SpeechSettingsPageView`) shown by the settings dialog.
- *Role*: Provider.
- *Contract*: Two named drop-down lists for the microphone and speakers with a Refresh devices button; a list of
  speech-to-text models and a list of text-to-speech models, each row with its state, a Download button, a
  progress bar, and a Cancel button while downloading; and the settings of the chosen model of each kind. The four
  parts are raised cards on the page, which has an icon and a sentence in the list of pages of the dialog.

### Design

1. The application composes one `AudioDeviceService`, one `SpeechModelService`, and one `DictationService` over them
   and the settings, which touch neither the audio system, the model store, nor the speech engine until they are first
   used, and one `SpeechSettingsPageViewModel` over the two services, which it adds to the settings dialog's pages
   after the AI Provider page.
2. When the dialog opens, the page loads. `DeviceSelection` lists the devices and selects the stored ones by name;
   `ModelCatalogPanel` lists the models by role and selects the stored ones by identifier; the page then shows the
   parameters of the chosen models in two `ModelParameterEditor`s, with the stored values.
3. A download starts at once when the user presses Download and is not part of OK or Cancel. Progress is shown in
   the model's row, and the row shows the installed state, or the failure's explanation, when the download ends.
4. When the user chooses another model, the values edited for the previous one are kept in the page, and the other
   model's parameters are shown with their stored values.
5. On OK or Apply the page merges its choices into the speech section of the settings. When the dialog closes by any
   route, the page cancels every download that is still running.
6. When the user holds the talk control, the caller asks `DictationService` to begin. The service checks the model and
   the native files, gets the engine, which `SpeechModelService` loaded from the stored choices the first time and which
   the service keeps, creates the microphone through `AudioDeviceService`, and starts a recognition session; the words
   recognized so far are available while it listens. When the user lets go, the caller ends the session, which keeps
   listening for a moment, finishes the audio already heard, releases the microphone, and returns the final text.
7. When the user changes the model or its settings, the next hold or preparation loads a new engine and releases the old
   one; a hold in progress keeps the engine it started with.

A choice that the page cannot show, for example a stored device that is not connected or a stored model while the
catalog cannot be read, is kept and stored again rather than erased.

### Third-party items

The subsystem consumes the `DemaConsulting.Speech` library, with its `DemaConsulting.Speech.Sherpa` model package,
in `AudioDeviceService`, `SpeechModelService`, and `DictationService` and for the descriptor types that the other units
show (see the Speech OTS design), and Avalonia and CommunityToolkit.Mvvm in the view models and views. The native
speech runtime arrives with those packages; see the Speech OTS design.

### Dependencies

The dependencies of the units come from the SysML2 model (`sysml2tools query dependencies`). The hub supplier
`RollingFileLogger`, which many units use, is left out of the view.

- `AudioDeviceService` and `SpeechModelService` depend on the Speech library (OTS).
- `DictationService` depends on `AudioDeviceService`, `SpeechModelService`, `SettingsService` (`Settings`), and the
  Speech library.
- `DeviceSelection` depends on `AudioDeviceService`.
- `ModelCatalogPanel` depends on `SpeechModelService` and the Speech library.
- `ModelParameterEditor` depends on the Speech library.
- `SpeechSettingsPage` depends on `DeviceSelection`, `ModelCatalogPanel`, `ModelParameterEditor`, and
  `SettingsDialog` (`Settings`).

![Speech dependencies](SpeechSubsystemDependencyView.svg)

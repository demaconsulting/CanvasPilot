### DictationService

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`DictationService` turns what the user says while holding a talk button or key into text. It listens through the
microphone and the speech-to-text model that the user chose in the settings, shows the words recognized so far while
the talk control is held, and returns the final text when it is released. It is the only unit that creates recognition
sessions and the only one that keeps a speech engine loaded. It also guarantees that the engine is never loaded when it
cannot work: every state in which dictation is impossible, such as no model chosen, a model that is not downloaded,
missing native speech files, no microphone, or an engine that will not start, is returned as a result with its own
explanation and next step, never as an exception.

#### Data Model

**DictationAvailability**: `Reason` (`DictationAvailabilityReason`: `Available`, `NoModelChosen`, `ModelNotInstalled`,
`SpeechRuntimeMissing`, `NoCaptureDevice`, `EngineUnavailable`), `Message` (the explanation for the user, which names
the next step; empty when available), and `IsAvailable`.

**DictationStart**: `Session` (the listening `IDictationSession`, or `null`), `Availability` (why there is no session;
`Available` when the request was ignored because a session was already listening), and `IsListening`.

**IDictationSession** (one hold of the talk control, used once): `PartialText` (the finished sentences followed by the
sentence still being recognized, trimmed and joined by single spaces), `PartialTextChanged` (raised on a background
thread only when `PartialText` changes), `Problem` (the explanation of a failure of the recognition, or an empty
string), and `EndAsync(cancellationToken)`, which returns the final text.

**IDictationService**: `CheckAvailability()`, `PrepareAsync(cancellationToken)`, and `BeginAsync(cancellationToken)`.

**State**: the loaded engine with a key made of the model identifier and the stored parameter values in a fixed order,
a flag that says a session is listening or being started, the session that is listening, and a disposed flag. Loading,
replacing, and releasing the engine are serialized by one gate; the flag and the session are guarded by a lock.

**Native runtime files**: `onnxruntime` and `sherpa-onnx-c-api`, named for the platform (`.dll`, `lib….so`, or
`lib….dylib`).

#### Key Methods

**CheckAvailability**: Reads the speech settings in effect and checks, in this order, that a model is chosen, that the
chosen model is in the catalog, is a speech-to-text model, and is downloaded, that the native speech files are
present, and that the audio system lists at least one microphone. The first failing check decides the reason and the
message. It never loads the engine, so the chat can call it when it opens to show the state before the user speaks. A
failure inside one of the services is logged and returned as `EngineUnavailable`.

**Native files check**: `FindMissingNativeLibraries(baseDirectory)` looks for each required file beside the application,
which is where the published build keeps it, and in `runtimes/{rid}/native` below it, which is where a build for
development keeps it, and returns the names that were not found. `{rid}` is made from the platform (`win`, `osx`, or
`linux` and the process architecture). The application's folder is the folder of the running program. Only the
inference runtime and the Sherpa interface to it are checked, because they are the files without which the engine can
crash the process; a missing audio library is reported by the audio system as no microphone.

**PrepareAsync**: Does the checks of `CheckAvailability` and, when dictation is available, loads the engine ahead of
time so that the first press responds at once. While a session is listening it leaves a loaded engine alone even when
the settings have changed, because the session is using it; the next press loads the new one. Failures are logged and
returned as `EngineUnavailable`; only a cancellation by the caller is thrown. The chat panel calls it when it is shown;
calling it when the speech settings change is added by a later step.

**BeginAsync**: (1) Claims the single listening slot; a request while the slot is taken is ignored and returns no
session with an available result. (2) Checks the model and the native files, without the microphone. (3) Gets the
engine, loading it through `ISpeechModelService.LoadRecognizerAsync` on a worker thread when none is loaded or the model
or its stored parameters changed, and releasing the engine that it replaces. (4) Asks `IAudioDeviceService` for the
capture device of the chosen microphone. (5) Creates a recognition session over the engine and the device, creates the
object that reads its results, and only then starts the session. The reader is subscribed by taking the first step of
the enumeration of the results on the calling thread, before the start, because the recognizer keeps no results for a
reader that is not yet reading, and a microphone that delivers audio quickly would otherwise lose the first words. (6)
Returns the session. Any failure in steps 2 to 5 releases whatever was created, gives the slot back, and returns a
result with the reason; a cancellation by the caller does the same and is then thrown.

**Reading results**: Each recognition result replaces the sentence being recognized when it is provisional and ends it
when it is final. `Compose` joins the finished sentences and the current sentence with single spaces. The recognizer
reports the whole current sentence every time, not only what is new, so the current sentence is replaced, never
appended to.

**EndAsync**: (1) Keeps listening for a short release tail (300 milliseconds by default), because the audio of the last
word is still on its way from the microphone when the user lets go. (2) Stops the recognition session, which finishes
the audio it has already received, and waits for the stream of results to end. (3) Releases the recognition session and
disposes the microphone when it is disposable. (4) Gives the listening slot back. (5) Returns the final text: the
finished sentences joined by single spaces, plus a last sentence that was never finished. A second call returns the same
text without doing the work again. If the caller cancels while it waits, the results stop being read and the text so
far is returned.

**DisposeAsync of a session**: Ends the session in the same way and discards the text, so that letting go of a session
without ending it still frees the microphone.

**Dispose**: Ends the session that is listening, releases the loaded engine, and refuses later requests with an
explanation.

#### Error Handling

Every state in which dictation cannot work is a `DictationAvailability` with a reason and a message of its own; none is
thrown. The model service's and the audio service's explanations are carried in the messages. Unexpected failures of
those services and of the native recognizer are logged as warnings and returned as `EngineUnavailable` (or
`NoCaptureDevice` for a microphone that is in use or blocked). A failure of the recognizer while listening does not
discard the text recognized before it: the explanation is in `Problem` and `EndAsync` still returns the text. A handler
of `PartialTextChanged` that throws does not stop the recognition; its failure becomes the `Problem`. A cancelled
`BeginAsync` or `PrepareAsync` releases what it created and rethrows the cancellation. Failures to write a log entry are
ignored. The recognized text is never written to the log; only events (the engine loaded and how long it took,
dictation started and ended) and failures are. The constructors refuse missing services with `ArgumentNullException`.

The checks are deliberately broad: a missing file that crashes the process cannot be caught after the fact, so the
service never loads an engine unless the native files and the model are verified to be present, and the broad catches
keep the remaining native failures from reaching the user as a crash.

#### Dependencies

- **SettingsService** (through `ISettingsService`) — the current speech settings: the chosen model, its stored
  parameters, and the chosen microphone.
- **SpeechModelService** (through `ISpeechModelService`) — lists the models and loads the engine; the library's
  `ISpeechRecognizerEngine` and the model descriptor types are the types it returns.
- **AudioDeviceService** (through `IAudioDeviceService`) — lists the microphones and creates the capture device.
- **DemaConsulting.Speech** (OTS) — `ISpeechRecognizerEngine`, `IRecognitionSession`, and the recognition result
  types; see the Speech OTS design.
- **RollingFileLogger** — optional; records events and problems, never recognized text.

#### Callers

- `ApplicationComposition` — creates the service over the settings, the model service, and the audio service, and
  disposes it.
- The chat panel (through `ComposerViewModel` for the talk button, and itself for `PrepareAsync`) calls
  `CheckAvailability`, `PrepareAsync`, and `BeginAsync`; the talk key of a later step does the same.

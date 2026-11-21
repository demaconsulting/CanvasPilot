### DictationService

#### Verification Approach

`DictationService` is verified by unit tests in `DictationServiceTests.cs`. The model service, the audio service, the
settings, the speech engine, and the recognition session are substitutes or hand-written fakes
(`DictationTestSupport.cs`): a fake recognition session that a test drives result by result and that counts its starts,
stops, and disposals, a fake engine that hands out those sessions, a fake microphone that remembers that it was
disposed, and a harness that wires them to a settings service with a chosen model. The check for the native speech files
is a substitute function, so no test needs the files, no engine is ever loaded from the native runtime, and a missing
runtime can be simulated; the check itself is tested on temporary folders laid out like the published build and like a
development build. Because the results arrive on a background thread, tests wait for a condition with a time limit and
never for a fixed time, except for the one test of the release tail, which uses a tail of one second and releases within
it. The same service is also exercised in the Speech subsystem tests and the system test over the speech library's own
recognition pipeline.

#### Test Environment

Standard test environment with a temporary folder for the log and for file layouts; no audio hardware, microphone, model
download, native speech runtime, network, or display are required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Availability is reported with a reason and a message of its own for no model chosen, a model that is not installed,
  missing native files, and no microphone, and with an explanation for an audio system that failed and for a service
  that throws; the check loads no engine.
- A begin or a preparation with the native files missing loads no engine.
- Provisional and final results update the text as the finished sentences and the current sentence, and the change event
  is raised once per real change.
- The final text is trimmed and joined with single spaces, includes what the recognizer finishes after the release,
  keeps a sentence that was never finished, is empty when nothing was said, and survives a failure of the recognizer.
- The microphone keeps listening for the release tail, so a word that arrives during it is kept.
- The engine is loaded once for several holds and a preparation, replaced and disposed when the model or its stored
  settings change, and left alone by a preparation while a session is listening.
- A second begin while listening is ignored without a problem; a cancelled begin gives the slot back.
- Ending a session, disposing it, a session that cannot start, and disposing the service release the recognition
  session, the microphone, and the engine, and the next begin works; the recognized text is not in the log.
- A model that is gone, a microphone that cannot be created or started, an engine that is busy, unavailable, or throws,
  and a session that is unavailable are reported with their reasons and leave the service able to try again.

#### Test Scenarios

**DictationService_CheckAvailability_EverythingPresent_IsAvailable**: A machine with a chosen and installed model, the
native files, and a microphone is checked. The result is available with no message, and no engine was requested. This
scenario is tested by `DictationService_CheckAvailability_EverythingPresent_IsAvailable`.

**DictationService_CheckAvailability_EachProblem_HasItsOwnReasonAndMessage**: Four machines are checked: no model
chosen, a model that is not downloaded, native files that are missing, and no microphone. The reasons are no model
chosen, model not installed, speech runtime missing, and no capture device; the four messages are different, each names
its next step or the missing file, and no engine was requested. This scenario is tested by
`DictationService_CheckAvailability_EachProblem_HasItsOwnReasonAndMessage`.

**DictationService_CheckAvailability_WrongOrUnknownOrUnreadableModel_ReportsModelNotInstalled**: A synthesis model
chosen as the speech-to-text model, an unknown model, and a catalog that cannot be read are checked. All three are a
model that is not installed, and the last carries the catalog's explanation. This scenario is tested by
`DictationService_CheckAvailability_WrongOrUnknownOrUnreadableModel_ReportsModelNotInstalled`.

**DictationService_CheckAvailability_AudioSystemUnusable_ReportsNoCaptureDeviceWithProblem**: The audio system reports
that its library is missing. The result is no capture device, and its message contains the explanation. This scenario is
tested by `DictationService_CheckAvailability_AudioSystemUnusable_ReportsNoCaptureDeviceWithProblem`.

**DictationService_CheckAvailability_ServiceThrows_ReturnsExplanationAndLogsIt**: The model service throws. The result
is an unavailable explanation, nothing is thrown, and the failure is in the log. This scenario is tested by
`DictationService_CheckAvailability_ServiceThrows_ReturnsExplanationAndLogsIt`.

**DictationService_BeginAndPrepare_NativeFilesMissing_NeverLoadEngine**: The native files are missing. A preparation and
a begin both report that the speech runtime is missing, and no engine was ever requested. This scenario is tested by
`DictationService_BeginAndPrepare_NativeFilesMissing_NeverLoadEngine`.

**DictationService_BeginAsync_ProvisionalThenFinalResults_UpdatesPartialText**: A session receives a provisional result,
a longer provisional result, a final result, a provisional result of the next sentence, the same provisional result
again, and a longer one. The text is the current sentence, then the longer one, then the final sentence, then the final
sentence followed by the next one, and the change event was raised five times, not six. This scenario is tested by
`DictationService_BeginAsync_ProvisionalThenFinalResults_UpdatesPartialText`.

**DictationService_EndAsync_FinalResultsIncludingDrained_ReturnsTrimmedJoinedText**: A session receives a final sentence
with leading spaces and a final result of spaces only, and the recognizer finishes one more sentence, with surrounding
spaces, when it is stopped. Ending returns the two sentences joined by one space, and the session has no problem. This
scenario is tested by `DictationService_EndAsync_FinalResultsIncludingDrained_ReturnsTrimmedJoinedText`.

**DictationService_EndAsync_SentenceNeverFinished_KeepsItsLastProvisionalText**: A session only produced a provisional
result. Ending returns it. This scenario is tested by
`DictationService_EndAsync_SentenceNeverFinished_KeepsItsLastProvisionalText`.

**DictationService_EndAsync_NothingSaid_ReturnsEmptyString**: A session without results is ended at once. The text and
the partial text are empty. This scenario is tested by `DictationService_EndAsync_NothingSaid_ReturnsEmptyString`.

**DictationService_EndAsync_WordsArriveDuringReleaseTail_AreKept**: A service with a tail of one second has a session
with one sentence. The session is ended, a word arrives before the recognizer is stopped, and the end completes. The
recognizer had not been stopped at the moment of the release, the word is in the text, and the recognizer was stopped
once. This scenario is tested by `DictationService_EndAsync_WordsArriveDuringReleaseTail_AreKept`.

**DictationService_EndAsync_ReleasesSessionAndMicrophone_AllowsNextBegin**: A session is ended twice and another is
begun. Both ends return the same text, the recognizer was started, stopped, and disposed once, the microphone was
disposed, and the next begin listens through a new session. This scenario is tested by
`DictationService_EndAsync_ReleasesSessionAndMicrophone_AllowsNextBegin`.

**DictationService_BeginAsync_WhileListening_IsIgnored**: A second begin is made while a session listens. It returns no
session and an available result, and only one session and one microphone were created. This scenario is tested by
`DictationService_BeginAsync_WhileListening_IsIgnored`.

**DictationService_BeginAsync_SeveralHolds_ReuseTheLoadedEngine**: Three holds are made in a row. One engine was loaded,
served three sessions, and was not disposed. This scenario is tested by
`DictationService_BeginAsync_SeveralHolds_ReuseTheLoadedEngine`.

**DictationService_PrepareAsync_Available_LoadsEngineOnceForLaterHolds**: A preparation is made twice and then a begin.
Everything is available and one engine was loaded. This scenario is tested by
`DictationService_PrepareAsync_Available_LoadsEngineOnceForLaterHolds`.

**DictationService_PrepareAsync_FailureOrUnavailable_IsReturnedAndLoggedNotThrown**: A preparation without a chosen
model reports that and loads nothing; a preparation whose load throws reports the engine as unavailable, and the failure
is in the log. This scenario is tested by
`DictationService_PrepareAsync_FailureOrUnavailable_IsReturnedAndLoggedNotThrown`.

**DictationService_BeginAsync_SettingsChanged_ReloadsEngineAndDisposesOldOne**: After a hold, a stored model parameter
is changed and another hold is made. A second engine was loaded with the stored parameter, and the first was disposed.
This scenario is tested by `DictationService_BeginAsync_SettingsChanged_ReloadsEngineAndDisposesOldOne`.

**DictationService_PrepareAsync_WhileListeningWithChangedSettings_KeepsTheEngineInUse**: The settings change while a
session listens and a preparation is made. The result is available, no second engine was loaded, and the engine in use
was not disposed. This scenario is tested by
`DictationService_PrepareAsync_WhileListeningWithChangedSettings_KeepsTheEngineInUse`.

**DictationService_BeginAsync_EngineLoadFails_ReportsReasonAndTriesAgainNextTime**: The load reports that the engine is
unavailable. The begin reports it with the explanation; after the cause is fixed the next begin loads an engine and
listens. This scenario is tested by `DictationService_BeginAsync_EngineLoadFails_ReportsReasonAndTriesAgainNextTime`.

**DictationService_BeginAsync_ModelDisappeared_ReportsModelNotInstalled**: The load reports that the model is not
installed. The reason is a model that is not installed, and the message says where to fix it. This scenario is tested by
`DictationService_BeginAsync_ModelDisappeared_ReportsModelNotInstalled`.

**DictationService_BeginAsync_NoMicrophone_ReportsNoCaptureDeviceAndKeepsEngine**: The audio system cannot create the
microphone. The begin reports it with the explanation; after the microphone works the next begin listens without loading
the engine again. This scenario is tested by
`DictationService_BeginAsync_NoMicrophone_ReportsNoCaptureDeviceAndKeepsEngine`.

**DictationService_BeginAsync_SessionCannotStart_ReportsReasonReleasesMicrophoneAndRecovers**: Four begins are made on
one engine: the engine is busy, the microphone is in use by another program, the recognizer throws while starting, and
finally everything works. The reasons are engine unavailable, no capture device with the explanation, and engine
unavailable with the failure's message; each failed session was disposed once, and the last begin listens. This scenario
is tested by `DictationService_BeginAsync_SessionCannotStart_ReportsReasonReleasesMicrophoneAndRecovers`.

**DictationService_BeginAsync_SessionUnavailable_ReportsEngineUnavailable**: The engine creates a session that says it
is unavailable. The begin reports the engine as unavailable, the session was disposed, and nothing was started. This
scenario is tested by `DictationService_BeginAsync_SessionUnavailable_ReportsEngineUnavailable`.

**DictationService_BeginAsync_Cancelled_ThrowsAndLeavesNextBeginPossible**: A begin with a token that is already
cancelled and one whose token is cancelled while the engine loads both end with a cancellation. A later begin listens,
which shows that the slot was given back. This scenario is tested by
`DictationService_BeginAsync_Cancelled_ThrowsAndLeavesNextBeginPossible`.

**DictationService_EndAsync_RecognizerFaulted_KeepsTextAndReportsProblem**: A session recognizes a sentence and then the
recognizer faults. Ending returns the sentence, and the problem contains the failure's message. This scenario is tested
by `DictationService_EndAsync_RecognizerFaulted_KeepsTextAndReportsProblem`.

**DictationService_PartialTextChanged_HandlerThrows_RecognitionContinues**: A handler of the change event throws. Two
results still arrive, the text holds both, and the handler's failure is the problem. This scenario is tested by
`DictationService_PartialTextChanged_HandlerThrows_RecognitionContinues`.

**DictationService_DisposeSession_WithoutEnd_ReleasesEverythingAndLogsNoText**: A session recognizes a sentence
containing a secret word and is disposed without being ended, with a log. The recognizer was stopped and disposed once,
the microphone was disposed, the log holds "Dictation started." and "Dictation ended.", and the secret word is not in
the log. This scenario is tested by `DictationService_DisposeSession_WithoutEnd_ReleasesEverythingAndLogsNoText`.

**DictationService_Dispose_WhileListening_EndsSessionAndReleasesEngine**: The service is disposed twice while a session
listens, and a begin is made afterwards. The session was stopped, the engine and the microphone were disposed, and the
later begin reports that speech recognition has been shut down. This scenario is tested by
`DictationService_Dispose_WhileListening_EndsSessionAndReleasesEngine`.

**DictationService_InvalidArguments_Throw**: Each missing service, and a blank folder for the native files check, is
refused. This scenario is tested by `DictationService_InvalidArguments_Throw`.

**DictationService_FindMissingNativeLibraries_EmptyOrPartialFolder_NamesWhatIsMissing**: An empty folder is checked and
then one with only the inference runtime. The first names both required files and the second only the Sherpa interface.
This scenario is tested by `DictationService_FindMissingNativeLibraries_EmptyOrPartialFolder_NamesWhatIsMissing`.

**DictationService_FindMissingNativeLibraries_PublishedAndDevelopmentLayouts_AreBothPresent**: One folder holds the
files beside it, as the published build does, and another holds them in the runtime folder below it, as a development
build does. Nothing is missing in either. This scenario is tested by
`DictationService_FindMissingNativeLibraries_PublishedAndDevelopmentLayouts_AreBothPresent`.

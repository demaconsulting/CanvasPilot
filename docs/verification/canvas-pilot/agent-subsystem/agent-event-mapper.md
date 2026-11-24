### AgentEventMapper

#### Verification Approach

`AgentEventMapper` is verified with unit tests in `AgentEventMapperTests.cs`, `AgentEventMapperProgressTests.cs`,
`AgentEventMapperUsageTests.cs`, and `AgentLoggingTests.cs`, and with one subsystem test in `AgentSubsystemTests.cs`.
The unit tests build the updates by hand from the content types of Microsoft.Extensions.AI and stream them the way an
agent does (an asynchronous stream that yields, and may throw after its last update), which lets a test control exactly
the order of the content, the shape of a result, the id of a call, and the exception that ends a turn. One test runs a
real agent of the AI library on a scripted chat client with one tool, so that the mapping is also proven against the
updates that the library really makes (a call and its result arrive in different updates). The exception tests use the
exceptions that the libraries raise: an `HttpRequestException` with a status code, which is all that OllamaSharp carries
for a model that is missing, and the `InvalidOperationException` with the words of the Copilot SDK for a protocol
mismatch and a missing credential. The test of the prefix of a refusal builds a real refusal with `ToolResult.Denied`
and checks that it starts with the prefix that the mapper looks for.

The progress and the usage are verified on events that the tests build by hand, because the release of AgentKit in use
does not forward the delta events and a real Copilot runtime cannot be part of the tests that run everywhere: the events
of the Copilot SDK for a piece of the arguments of a call, the size of a response, reasoning, and the usage of a call
are constructed with the types of the SDK and carried as the raw representation of the content or of the update, exactly
where the library puts them. The progress tests give the mapper a manual clock and move it by hand, so the throttle is
tested without waiting, and they put a sentinel text into the arguments and into the reasoning and check that no event
holds it. The tests of the log read the log that a whole turn wrote, with sentinel texts in the arguments, the results,
and the answer, and check the start and the end of each call (the outcomes ok, refused, and failed, the path made safe,
the bytes) and that no sentinel reaches the log.

The subsystem test runs a whole turn: a real agent on a scripted model that asks for three calls of the real project
tools (a drawing is written, the drawing is looked at, and a write outside the project is tried) and then answers. It
checks the events that the chat will get and that the drawing is in the project and nothing is outside it.

#### Test Environment

The unit tests use no file except the log test, which makes one temporary folder for the log and deletes it. The
subsystem test makes one temporary project folder and deletes it. No network, display, or language model is used.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Two pieces of text in one update are one event; an update with no content, with empty text, or with no text gives no
  event; a piece of white space is kept; text before and after a call stays before and after it.
- A call and its result carry the same id, the result has the name of the call, a call that is sent twice is reported
  once, a result whose call was never seen is named `tool`, and calls and results without ids are paired in order.
- The summary of arguments shows a short value as it is (numbers, truth values, null, JSON strings, and JSON objects),
  shows a text of more than 60 characters as `(1,234 characters)`, and is at most 200 characters for any number of
  arguments; the summary of a result is at most 200 characters and ends with `...` when it was cut.
- The answer `File 'cover.svg' created (...)` and the answer `File 'cover.svg' replaced (...)` appear at the start of
  the summary of the result.
- A refusal of the AI library and a result with an exception are not succeeded (the exception gives `The tool failed:`
  and its message on one line); a result with a picture, alone, with a caption, or in a list, has `hasImage`, and a
  picture alone is summarized as `A picture.`; a JSON string, a JSON object, a number, and nothing are summarized.
- Usage carries the counts that were reported and null where they were not; reasoning gives `Thinking` without its
  words and a picture outside a tool result gives no event; an error in the content is a failure and nothing after it in
  the update is mapped.
- Usage with the raw event of the Copilot runtime carries the tokens of the AI library and the multiplier (15), the
  reasoning tokens (200), and the model (`claude-opus-5.5`) of the raw event; usage without a raw event carries the
  tokens only, takes the reasoning tokens of the AI library, and has no multiplier and no model.
- The pieces of the arguments of a call that the model is writing are counted in UTF-8 bytes (a piece of `aé€` is six
  bytes), give a progress event with no path first and then one with the whole count and the path `diagrams/flow.svg`
  once the path is whole (at once, within the interval), and no event holds the sentinel content; a path with an escaped
  quote that arrives in two pieces is read whole (`a"b.svg`); a path that begins after the first 256 characters is not
  reported; a piece without a call id, and a piece of a call without a tool name, give nothing.
- A hundred pieces within the interval give one progress event, and the first piece after the interval gives one with
  the count of all of them (1,010 bytes); the size of the response gives the first `ModelWorking` at once and the next
  one only after the interval; reasoning, as content and as the raw event, gives one `Thinking` for the phase and none
  for the pieces after it, and another after text; a change of phase (model, tool, the call itself, model) gives an
  event each at once.
- A raw event on an update without content is mapped, and a raw event of another kind is ignored.
- A stream that ends is followed by `Completed`, an empty stream gives only `Completed`, and a stream with an exception
  after some text gives the text and then `Failed`, with no `Completed`.
- A 404 of Ollama is `ModelNotFound` with exactly the sentence that names `ollama pull gemma3:12b`; 401 and 403 are
  `Authentication` with the Ollama sentence; another status is `Unexpected` with the status; no status, a socket error,
  an I/O error, and a timeout reported as a canceled request are `ConnectionLost`.
- For GitHub Copilot, the words of the SDK are classified as protocol mismatch, authentication, model not found,
  connection lost, or unexpected, each of the eight sample messages as its kind; the authentication sentence is the
  sentence of the readiness check (it tells the user to run `copilot` and type `/login`, and names `gh auth login` and
  the token variables as alternatives), the protocol sentence names
  `winget upgrade GitHub.Copilot`, and the model sentences name the model, or tell to clear the name when there is none.
- A cause below wrapping exceptions, and in an aggregate, is found; an exception with no message gives the sentence with
  the type name.
- A turn that is canceled by the caller ends after the event that was in flight with no `Failed` and no `Completed`; an
  `IOException` after the token was canceled and an operation that was canceled by itself give no event; `MapFailure`
  returns `null` for a cancellation.
- The log has the lines `Turn completed: provider=Ollama textEvents=1 toolCalls=1`, `Turn failed: provider=GitHubCopilot
  kind=Unexpected source=InvalidOperationException`, and `Turn canceled: provider=Ollama`, and does not contain the
  sentinel text that was put into a message, an argument, a result, and the message of an exception.
- The log of the calls has, for a turn numbered 7, `Tool started: turn=7 call=a name=project_read path=one two.svg
  argBytes=…` for a path with a line break, a path of 300 characters cut to 120 and `...` with `argBytes=300`, and
  `path=(none) argBytes=0` for a call without a path; `Tool finished` with the outcome `refused` for a refusal, `failed`
  for a result with an exception, and `ok` with `resultBytes=8` for an answer of eight bytes; the log of a failed HTTP
  request has `httpStatus=500`; and the sentinel content is not in the log.
- A real agent with one tool over a scripted client gives the text of both answers in order, one start and one finish of
  the call with the same id, the finish after the start, `Completed` last, and no `Failed`.
- Missing arguments are refused.

#### Test Scenarios

**AgentEventMapper_Constants_Rule_AreTheDocumentedValues**: The prefix `Denied (`, the stand-in name `tool`, and the 60
characters are as documented, the Copilot sentence is the one of the readiness check, and the prefix is what
`ToolResult.Denied` writes. This scenario is tested by `AgentEventMapper_Constants_Rule_AreTheDocumentedValues`.

**AgentEventMapper_Arguments_Null_AreRefused**: A missing update, a missing stream, and a missing exception are refused.
This scenario is tested by `AgentEventMapper_Arguments_Null_AreRefused`.

**AgentEventMapper_Map_TextContent_JoinsPiecesAndDropsEmptyUpdates**: An update of two pieces gives one event, an update
with nothing and an update with empty pieces give none, and a space is kept. This scenario is tested by
`AgentEventMapper_Map_TextContent_JoinsPiecesAndDropsEmptyUpdates`.

**AgentEventMapper_Map_TextAroundACall_KeepsTheOrder**: Text, a call, and text in one update give three events in that
order. This scenario is tested by `AgentEventMapper_Map_TextAroundACall_KeepsTheOrder`.

**AgentEventMapper_Map_CallAndResult_AreTiedByIdWithSummaries**: A call with seven kinds of argument and its result, in
another update. The arguments are summarized as in the criteria and the result is tied to the call and says `created`.
This scenario is tested by `AgentEventMapper_Map_CallAndResult_AreTiedByIdWithSummaries`.

**AgentEventMapper_Map_ReplacedFileAnswer_SummaryShowsReplacedAndIsShort**: An answer of 400 more characters than a
summary may hold starts with `File 'cover.svg' replaced` and is cut to 200 characters with `...`. This scenario is
tested by `AgentEventMapper_Map_ReplacedFileAnswer_SummaryShowsReplacedAndIsShort`.

**AgentEventMapper_Map_ManyArguments_SummaryIsShortened**: Thirty arguments give a summary of 200 characters that starts
with the first two. This scenario is tested by `AgentEventMapper_Map_ManyArguments_SummaryIsShortened`.

**AgentEventMapper_Map_RefusalAndException_AreNotSucceeded**: A real refusal and a result with an exception that has a
line break in its message. This scenario is tested by `AgentEventMapper_Map_RefusalAndException_AreNotSucceeded`.

**AgentEventMapper_Map_ImageResult_SetsHasImage**: A picture with a caption, a picture alone, and a list with text and a
picture. This scenario is tested by `AgentEventMapper_Map_ImageResult_SetsHasImage`.

**AgentEventMapper_Map_OtherResultShapes_AreSummarized**: A JSON string, a JSON object, a number, and nothing. This
scenario is tested by `AgentEventMapper_Map_OtherResultShapes_AreSummarized`.

**AgentEventMapper_Map_DuplicateCallAndUnknownResult_AreHandled**: A call sent twice, a call without a name, and a
result whose call was never seen. This scenario is tested by
`AgentEventMapper_Map_DuplicateCallAndUnknownResult_AreHandled`.

**AgentEventMapper_Map_CallsWithoutIds_GetMadeIdsAndPairInOrder**: Two calls and two results, all without ids. This
scenario is tested by `AgentEventMapper_Map_CallsWithoutIds_GetMadeIdsAndPairInOrder`.

**AgentEventMapper_Map_UsageAndOtherContent_UsageIsMappedReasoningIsThinkingOthersAreIgnored**: Reasoning, a picture,
usage with counts, and usage without. The reasoning gives one `Thinking`, the picture gives nothing, and the two usages
are mapped with their counts and with null where none were reported. This scenario is tested by
`AgentEventMapper_Map_UsageAndOtherContent_UsageIsMappedReasoningIsThinkingOthersAreIgnored`.

**AgentEventMapper_Map_UsageWithRawCopilotEvent_CarriesMultiplierReasoningAndModel**: A usage content whose raw
representation is the usage event of the Copilot runtime for a call of `claude-opus-5.5` with a multiplier of 15 and 200
tokens of reasoning, beside 1,200 input and 340 output tokens. The event has all five values. This scenario is tested by
`AgentEventMapper_Map_UsageWithRawCopilotEvent_CarriesMultiplierReasoningAndModel`.

**AgentEventMapper_Map_UsageWithoutRawEvent_CarriesTokensOnly**: A usage content with no raw representation (as Ollama
sends it), with the reasoning tokens in the details of the AI library. The event carries the three counts and no
multiplier and no model. This scenario is tested by `AgentEventMapper_Map_UsageWithoutRawEvent_CarriesTokensOnly`.

**AgentEventMapper_Map_ToolCallDeltas_CountBytesAndFindThePathWithoutTheContent**: Two pieces of the arguments of one
call, the second of which completes the path and carries a sentinel as the content, within one interval. The first gives
a progress event with the bytes and no path, the second gives one with the bytes of both and the path
`diagrams/flow.svg` at once, and no event holds the sentinel. This scenario is tested by
`AgentEventMapper_Map_ToolCallDeltas_CountBytesAndFindThePathWithoutTheContent`.

**AgentEventMapper_Map_ToolCallDelta_CountsUtf8Bytes**: A piece of three characters of one, two, and three bytes. The
event says six bytes. This scenario is tested by `AgentEventMapper_Map_ToolCallDelta_CountsUtf8Bytes`.

**AgentEventMapper_Map_ManyDeltas_AreSpacedByTheIntervalAndNotLost**: A hundred pieces of ten bytes, a millisecond
apart, and then one more after the interval. One event for the first interval and a second with 1,010 bytes. This
scenario is tested by `AgentEventMapper_Map_ManyDeltas_AreSpacedByTheIntervalAndNotLost`.

**AgentEventMapper_Map_PathAfterTheScanLength_IsNotReported**: Arguments that begin with more than 256 characters of
content and name the path afterwards. The later event has no path. This scenario is tested by
`AgentEventMapper_Map_PathAfterTheScanLength_IsNotReported`.

**AgentEventMapper_Map_EscapedPathAndUnidentifiedCalls_AreHandled**: A piece without a call id, a piece of a call
without a name, and a path with an escaped quote in two pieces. The first two give nothing and the path is `a"b.svg`.
This scenario is tested by `AgentEventMapper_Map_EscapedPathAndUnidentifiedCalls_AreHandled`.

**AgentEventMapper_Map_StreamingDeltas_AreModelWorkingSpacedByTheInterval**: Three sizes of the response, the third
after the interval. The first and the third give `ModelWorking` (100 and 300 bytes) and the second gives nothing. This
scenario is tested by `AgentEventMapper_Map_StreamingDeltas_AreModelWorkingSpacedByTheInterval`.

**AgentEventMapper_Map_Reasoning_IsThinkingOncePerPhaseWithoutWords**: Reasoning as content and as the raw event, with a
sentinel as its words, and with a long time between them, then text, then reasoning again. One `Thinking` for the first,
none for the next two, and another after the text. This scenario is tested by
`AgentEventMapper_Map_Reasoning_IsThinkingOncePerPhaseWithoutWords`.

**AgentEventMapper_Map_PhaseChange_IsReportedAtOnce**: The response size, the arguments of a call, the call itself, and
the response size again, all at one instant. Each gives its event. This scenario is tested by
`AgentEventMapper_Map_PhaseChange_IsReportedAtOnce`.

**AgentEventMapper_Map_RawEventOnTheUpdate_IsMappedAndOtherRawEventsAreIgnored**: An update with no content whose raw
representation is the event of a tool call that is being written, and an update whose content carries an object that is
no event of the SDK. The first gives a progress event and the second gives nothing. This scenario is tested by
`AgentEventMapper_Map_RawEventOnTheUpdate_IsMappedAndOtherRawEventsAreIgnored`.

**AgentEventMapper_Logging_Tools_HaveOutcomesAndSafePaths**: A mapper made for turn 7 on Ollama is given four calls and
the results of three: a call whose path has a line break and whose result is a refusal, a call with a path of 300
characters whose result carries an exception with a sentinel as its text, a call without a path, and a call with no
result. The log has the start of each call with the name, the path on one line (the line break became a space, the
long path cut to 120 characters and `...`, and `(none)`), and the bytes of the arguments; the end of each of the
three with the outcome `refused`, `failed`, and `ok` (with eight bytes of result); and not the sentinel. This scenario
is tested by `AgentEventMapper_Logging_Tools_HaveOutcomesAndSafePaths`.

**AgentService_Logging_ProviderSession_HasIdsRunKindsEventsFileAndReleaseReason**: The scenario of the `AgentService`
unit that also shows the HTTP status of a failed request on the line `Turn failed`. This scenario is tested by
`AgentService_Logging_ProviderSession_HasIdsRunKindsEventsFileAndReleaseReason`.

**AgentEventMapper_Map_ErrorContent_BecomesFailedAndStopsTheUpdate**: Text, an error with the words of a protocol
mismatch, and more text. This scenario is tested by `AgentEventMapper_Map_ErrorContent_BecomesFailedAndStopsTheUpdate`.

**AgentEventMapper_MapAsync_StreamEnds_EndsWithCompleted**: A stream of two updates and an empty stream. This scenario
is tested by `AgentEventMapper_MapAsync_StreamEnds_EndsWithCompleted`.

**AgentEventMapper_MapAsync_CancelledByCaller_EndsWithoutFailureOrCompletion**: A stream of twenty updates that the
consumer cancels after the first event. This scenario is tested by
`AgentEventMapper_MapAsync_CancelledByCaller_EndsWithoutFailureOrCompletion`.

**AgentEventMapper_MapAsync_CancelLikeExceptions_AreNotFailures**: A stream that throws an `IOException` with the token
already canceled, and a stream that throws an `OperationCanceledException` with the token not canceled. This scenario is
tested by `AgentEventMapper_MapAsync_CancelLikeExceptions_AreNotFailures`.

**AgentEventMapper_MapAsync_ExceptionAfterText_YieldsTextThenFailed**: A stream with one text and then an
`HttpRequestException`. This scenario is tested by `AgentEventMapper_MapAsync_ExceptionAfterText_YieldsTextThenFailed`.

**AgentEventMapper_MapAsync_ErrorContent_EndsWithFailedOnly**: An error in the first update. This scenario is tested by
`AgentEventMapper_MapAsync_ErrorContent_EndsWithFailedOnly`.

**AgentEventMapper_MapFailure_Ollama404_IsModelNotFoundWithPullCommand**: A 404 with the model `gemma3:12b`. This
scenario is tested by `AgentEventMapper_MapFailure_Ollama404_IsModelNotFoundWithPullCommand`.

**AgentEventMapper_MapFailure_OllamaCauses_AreClassified**: Status 401, 403, and 500, a refused connection, a socket
error inside two exceptions, an I/O error, and a timeout reported as a canceled request. This scenario is tested by
`AgentEventMapper_MapFailure_OllamaCauses_AreClassified`.

**AgentEventMapper_MapFailure_CopilotMessages_AreClassifiedByTheirWords**: Eight messages of the Copilot SDK and of its
neighbors, each with its kind. This scenario is tested by
`AgentEventMapper_MapFailure_CopilotMessages_AreClassifiedByTheirWords`.

**AgentEventMapper_MapFailure_CopilotGuidance_NamesTheRemedy**: The exact remedies for a missing sign-in (the sentence
names `copilot`, `/login`, `gh auth login`, and `COPILOT_GITHUB_TOKEN`), a protocol
mismatch, a missing model with and without a name, and a lost connection. This scenario is tested by
`AgentEventMapper_MapFailure_CopilotGuidance_NamesTheRemedy`.

**AgentEventMapper_MapFailure_OllamaWithoutModelName_StillGivesASentence**: A 404 with a blank model name names
`ollama list`, and a sign-in failure of Ollama is not worded as the Copilot guidance. This scenario is tested by
`AgentEventMapper_MapFailure_OllamaWithoutModelName_StillGivesASentence`.

**AgentEventMapper_MapFailure_WrappedCause_IsFound**: A 404 inside an `InvalidOperationException`, and an aggregate with
an unrelated exception and a refused connection. This scenario is tested by
`AgentEventMapper_MapFailure_WrappedCause_IsFound`.

**AgentEventMapper_MapFailure_ExceptionWithoutMessage_GivesTheTypeName**: An exception with an empty message. This
scenario is tested by `AgentEventMapper_MapFailure_ExceptionWithoutMessage_GivesTheTypeName`.

**AgentEventMapper_MapFailure_Cancellation_IsNull**: An `OperationCanceledException`, a `TaskCanceledException`, and an
`IOException` after the token was canceled. This scenario is tested by
`AgentEventMapper_MapFailure_Cancellation_IsNull`.

**AgentEventMapper_Logging_Turn_HoldsMetadataOnly**: A completed, a failed, and a canceled turn with a sentinel text in
every kind of text, and the log read afterwards. This scenario is tested by
`AgentEventMapper_Logging_Turn_HoldsMetadataOnly`.

**AgentEventMapper_MapAsync_RealAgentToolTurn_GivesTheEventsInOrder**: A real agent on a scripted client with one tool.
This scenario is tested by `AgentEventMapper_MapAsync_RealAgentToolTurn_GivesTheEventsInOrder`.

**AgentSubsystem_RunTurn_ScriptedModelWritesAndLooksAtADrawing_EventsShowTheCreatedFileThePictureAndTheRefusal**: The
subsystem scenario that is described in the verification of the Agent subsystem. This scenario is tested by
`AgentSubsystem_RunTurn_ScriptedModelWritesAndLooksAtADrawing_EventsShowTheCreatedFileThePictureAndTheRefusal`.

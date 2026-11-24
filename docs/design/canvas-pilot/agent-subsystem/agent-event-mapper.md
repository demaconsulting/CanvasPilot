### AgentEventMapper

![Agent Runtime](AgentSubsystemRuntimeView.svg)

#### Purpose

`AgentEventMapper` turns the stream of `AgentResponseUpdate` that an AI agent produces for one turn into the events of
the provider-independent contract (`AgentEvent`), and turns the exception of a failed turn into a `Failed` event with a
sentence that tells the user what to do. GitHub Copilot and Ollama produce the same kinds of content (text, a function
call, a function result, usage), so one mapping serves both and the chat never learns which provider answered. While
the model works, a provider may also stream progress (reasoning, the arguments of a tool call that is being written, the
size of the response); the mapper turns it into events that say that the turn is alive and what it is doing, without
any of the content. One instance serves one turn, because it remembers the calls of the turn, the progress that it has
reported, and the token that cancels it.

#### The Mapping

The updates are mapped by the kind of content:

- **Text** gives `TextDelta`. The pieces of one update are joined, an empty piece and an update without text give
  nothing, and a piece of white space is kept. The text before another kind of content is flushed first, so the order of
  the content is the order of the events.
- **A function call** gives `ToolStarted(callId, name, argumentSummary)`. A call with the same id is reported once. A
  call without an id gets the id `call-n` made by the mapper. A call without a name is ignored.
- **A function result** gives `ToolFinished(callId, name, succeeded, summary, hasImage)`. The name is the one of the
  call with the same id, or `tool` when the call was never seen; a result without an id is paired with the oldest made
  id that has no result yet.
- **Usage** gives `Usage(inputTokens, outputTokens, reasoningTokens, multiplier, model)` for each model call (see
  *Usage*).
- **Reasoning** (`TextReasoningContent`) gives `Thinking`, once for each phase, and never its words (see *Progress*).
- **The raw event of the provider** on a piece of content, or on an update that has no content, gives progress when it
  is one of the three events of the Copilot SDK that tell it (see *Progress*).
- **An error** gives a `Failed` event (the kind comes from the words of the error); the rest of the update and the
  stream are not mapped.
- **Anything else** gives nothing: pictures outside a tool result and the raw events that are not progress are not shown
  in the chat.

When the updates end, `Completed` follows, unless the turn was canceled or ended with an exception.

**The summaries.** An argument is shown as `name=value`; the arguments are joined with a comma and a space and cut by
`AgentEvent.Summarize` to 200 characters. A value is shown as it is when its single-line form is at most 60 characters
and otherwise by its length, `(1,234 characters)`, so that a drawing that is sent to `project_write` never fills the
chat. The summary of a result is its text on one line, whatever shape the AI library gave the result: a string, a
`TextContent`, a list of content (the text parts are joined and a picture sets `hasImage`), a `DataContent` or
`UriContent` of an image media type, a JSON string (its text) or any other JSON value (its raw text), or any other
object by its text. The answer of `project_write`, which says that the file was `created` or `replaced`, therefore
reaches the chat unchanged. A result with no text and a picture is summarized as `A picture.`.

**Succeeded.** A result did not succeed when it carries an exception (the summary is `The tool failed:` and the message)
or when its text starts with `Denied (`, which is how the AI library words a refusal (`ToolResult.Denied`); a test pins
that the prefix is what the library really writes. Everything else, including the answer of a drawing that could not be
made, is an answer of the tool and counts as succeeded; the text says what happened.

#### Progress

A turn can take minutes, and the user has to see that it is alive and what it is doing, but the content of a drawing is
the user's data and large. The mapper therefore reports three kinds of progress, and only as counts and a file name:

- **Reasoning** gives `Thinking`, which has no fields. Reasoning content gives it, and so does the raw event of the SDK
  for a reasoning piece; the words of the reasoning are never read.
- **The arguments of a tool call that the model is writing** (the SDK event with the call id, the tool name, and a piece
  of the arguments) give `ToolProgress(callId, name, argumentBytes, path)`. The mapper keeps a small record for each
  call: the bytes of every piece, counted in UTF-8 and not in characters, and the first 256 characters of the arguments
  (`PathScanLength`), which are searched for the path and then discarded. The search looks for the property `path` and
  reads its string value from the characters it has so far, so a path that arrives split over two pieces is found when
  it is whole; the escapes `\\`, `\/`, and `\"` are read as the character they stand for, any other escape is written as
  `?`, and an empty value is no path. A path that begins after the first 256 characters is never reported, and nothing
  of the arguments after those characters is kept. A piece without a call id, and a piece of a call that has no tool
  name yet, are ignored.
- **The size of the response** (the SDK event with the total bytes so far) gives `ModelWorking(bytes)`.

**The throttle.** A stream of thousands of pieces must reach the screen as a few events, so a progress event is made
only when one of three things holds: the phase changed (the phases are one for each call, one for the response, and one
for reasoning), the event is forced (the path just became known), or `ProgressInterval` (250 milliseconds) of the clock
that the mapper was given has passed since the last progress event. The bytes are counted for every piece whether or not
an event is made, so the next event carries the total so far and nothing is lost. `Thinking` is made once for each phase
and not again after the interval, because it has nothing to count. The phase is forgotten, so that the next progress is
reported at once, whenever text is flushed, a call starts, or a result arrives; the start of the call that the model was
writing therefore follows its progress, and progress of the next phase is not held back. The clock is injected, so the
tests move time by hand.

**Where the events come from.** The mapper reads the progress from the raw representation of a piece of content or of
the update (`AIContent.RawRepresentation` and `AgentResponseUpdate.RawRepresentation`), which carries the event of the
Copilot SDK when the library that made the update forwards it. The release of AgentKit in use forwards the whole
messages, the tool calls and their results, and the usage, but none of the delta events, so on it the progress events
are not produced, and on a runtime that buffers a long answer
there is nothing to report until the answer arrives (see *Known Limits and Upstream Requests* in the design of the
AgentKit OTS item). The mapper is written and tested against hand-built events of the SDK, so progress appears as soon
as the library forwards them, with no change here. A raw event of another kind is ignored.

#### Usage

The mapper makes a `Usage` event for each `UsageContent`. The tokens come from the AI library (`UsageDetails`): the
input and output tokens, and the reasoning tokens as a fallback. GitHub Copilot sends more on the raw representation of
the same content, the event of the runtime that holds the model multiplier of the call (how many premium requests a
message of the model counts), the tokens of reasoning, and the model that served the call, and the AI library has no
field for them. `CopilotUsageReader` is the adapter that reads them: it takes the raw representation and returns the
multiplier, the reasoning tokens, and the model (each `null` when missing), and returns nothing for any other kind of
raw event, so Ollama gives no multiplier. The multiplier (`AssistantUsageData.Cost`) is a member that the Copilot SDK
marks as experimental (diagnostic `MCPSDK001`); `CopilotUsageReader` is the only place that uses it, behind one narrow
warning suppression around one statement, so a change of the SDK touches one method and the rest of the application
only sees the record `CopilotUsage`. The multiplier belongs to a model call and not to the message: the same message
makes several calls, so a consumer must not add the multipliers (the `AgentService` unit logs the largest).

#### Failures

`MapFailure(exception)` returns `null` when the turn was canceled, which is not a failure (see below), and otherwise a
`Failed` event whose kind and sentence come from the cause. The exception and its inner exceptions (an aggregate is
searched in all its branches, to a depth of eight) are examined from the outside in; for each one the type is looked at
first and the words of its message second, and the first that tells wins.

The causes and their sentences:

- **Status 404** of an `HttpRequestException` is `ModelNotFound`. For Ollama the sentence is "The model 'm' was not
  found on the Ollama server. Run `ollama pull m` or choose another model in Settings." (OllamaSharp carries only the
  status, so the model of the settings is named). For Copilot it is "The model 'm' is not available to your Copilot
  account. Clear the model name in Settings to use the default model, or choose another one."
- **Status 401 or 403**, or the words *oauth token, hmac key, not authenticated, unauthorized, authentication, bad
  credentials, not signed in, login required*, is `Authentication`. For Copilot the sentence is the short one of the
  readiness check, "Copilot is not signed in for CanvasPilot. Run `copilot`, then type `/login` to sign in (or sign in
  with `gh auth login`, or set the COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then choose
  Check connection." (it has no digest of what the library found, because it is shown in the middle of a turn). For
  Ollama it is "The Ollama server refused the request. Check the address in Settings and
  any gateway or proxy in front of the server, then send the message again."
- **The words** *protocol version* or *protocol mismatch* (the SDK says "SDK protocol version mismatch: SDK supports
  versions 4-4, but server reports version 3") are `ProtocolMismatch`, with the sentence "The Copilot CLI and
  CanvasPilot's AI library do not speak the same protocol version. Run `winget upgrade GitHub.Copilot`, then choose
  Check connection."
- **The word** *model* together with *not found, not available, unknown, invalid, not supported,* or *does not exist* is
  `ModelNotFound`, with the sentences of status 404.
- **No status** on an `HttpRequestException`, a `SocketException`, an `IOException`, a `TimeoutException`, or the words
  *connection, disconnected, broken pipe, pipe is being closed, stream closed, has exited, timed out,* or *timeout* is
  `ConnectionLost`. For Ollama the sentence is "The connection to the Ollama server was lost or could not be made. Check
  that Ollama is running and that the address in Settings is right, then send the message again." For Copilot it is "The
  connection to GitHub Copilot was lost. Check your network and that the Copilot CLI still runs, then send the message
  again."
- **Another status** of an `HttpRequestException` is `Unexpected`: "The AI provider answered with an error (HTTP 500): "
  and the message on one line.
- **Anything else** is `Unexpected`: "The AI provider reported a problem: " and the message on one line, cut to 200
  characters, or the type name when there is no message.

The words are searched in the order protocol, authentication, model, connection, because the Copilot SDK reports its
problems only as text and a message may hold more than one of them.

**Cancellation is not a failure.** When the token of the turn was canceled, whatever exception follows (a provider may
raise an `IOException` for a closed stream, or anything else) is the stop of the turn: `MapFailure` returns `null` and
`MapAsync` ends without a `Failed` and without a `Completed`. An `OperationCanceledException` with no cancellation of
the token is also not a failure, and also not a completion, with one exception: a `TaskCanceledException` whose inner
exception is a `TimeoutException` is the way the HTTP client reports a request that timed out, and is a lost connection.

#### Logging

The log gets metadata only, and every line of a turn carries `turn=N`, the number that the `AgentService` unit gave the
turn (zero when the mapper was made for no turn), so that the lines of one turn can be read together.

- **The start of a call**: `Tool started: turn=N call=<id> name=<tool> path=<path> argBytes=<n>`. The path is the value
  of the argument `path`, or else `folder`, with runs of white space made one space and control characters made `?`, and
  cut to 120 characters with `...`; it names a file of the project and is never its content; a call with neither says
  `(none)`. `argBytes` is the sum of the UTF-8 bytes of the values of the arguments, so the log can say how large a
  drawing was without holding any of it.
- **The end of a call**: `Tool finished: turn=N call=<id> name=<tool> outcome=<ok|failed|refused> resultBytes=<n>
  hasImage=<bool> milliseconds=<n>`. The outcome is `failed` when the result carries an exception, `refused` when its
  text starts with `Denied (`, and `ok` otherwise; the milliseconds are those of the clock of the mapper between the
  start and the end, or -1 for a result whose call was never seen.
- **The end of a turn**: `Turn completed: provider=… textEvents=n toolCalls=n turn=N`, `Turn failed: provider=… kind=…
  source=<type of the exception> turn=N`, with ` httpStatus=<code>` added when a failed HTTP request is in the chain of
  the exception, or `Turn canceled: provider=… turn=N`.

It never gets the text of a message, an argument or a result of a tool, or the message of an exception, which a test
checks with a sentinel text in each. A log that cannot be written never fails a turn.

#### Data Model

The unit is a `sealed class` with the constants `RefusalPrefix`, `UnknownToolName`, `MaxArgumentTextLength` (60),
`PathScanLength` (256), the static `ProgressInterval` (250 milliseconds), and the sentences of the failures. The
constructor takes the provider kind, the model name of the settings, an optional logger, the token that cancels the
turn, the clock that spaces the progress events (the system clock when absent), and the number of the turn that the
lines of the log carry. Two supporting types are private or internal and have no part definition of their own:
`StreamedCall` (private to the mapper), which holds the bytes, the tool name, and the path of one call that the model is
writing, and `CopilotUsageReader` (internal static, with the record `CopilotUsage`), the adapter of the usage.

#### Key Methods

**Map(update)**: The events of one update, in the order of its content. An update that has no content but carries a raw
event is mapped as progress. The text before another kind of content is flushed first.

**MapAsync(updates)**: The events of a whole turn. The enumerator of the updates is read by hand so that an exception
of the provider is caught at the point where it is raised, and the stream of events ends with the failure.

**MapFailure(exception)**: The failure of an exception, or `null` for a cancellation.

**MapProgress(raw)** (private): Maps the raw event of a provider to a progress event, or to none: the event of a tool
call that is being written, the size of the response, and reasoning (the raw event or the reasoning content). Anything
else is ignored. **Offer(phase, force, make)** (private): Makes the progress event when the phase changed, when it is
forced, or when the interval has passed; the precondition is a clock that does not go back, and the postcondition is
that the phase and the time of the latest progress event are the ones of the event that was made.

**CopilotUsageReader.Read(raw)** (internal): Returns the multiplier, the reasoning tokens, and the model of the raw
usage event of the Copilot runtime, or nothing for any other raw representation, including none.

#### Error Handling

The mapper never throws for what a provider does: every exception from the updates is a failure or a stop. It throws
`ArgumentNullException` for a missing argument.

#### Security

The summaries are shown in the chat of the user and hold at most 200 characters of an argument or a result. The progress
events carry counts and a file name only: the text of the arguments of a call is never kept beyond the first 256
characters that are searched for the path, and the words of reasoning are never read. The log holds metadata only. A
sentence of a failure that names a model uses the name from the settings, never text from the server. The text of an
exception that is not recognized is shown to the user, on one line and cut, because it is the only information there
is; it is not logged.

#### Dependencies

`AgentEvent` and `AgentFailureKind` of the `AgentContract` unit; `AgentReadiness` for the sentence of a refused sign-in
(so that the chat and the settings dialog say the same); `AiProviderKind` of the `Settings` subsystem;
`RollingFileLogger` of the `Logging` subsystem; `AgentResponseUpdate` of the Microsoft Agent Framework; the content
types of Microsoft.Extensions.AI (`TextContent`, `TextReasoningContent`, `FunctionCallContent`,
`FunctionResultContent`, `UsageContent`, `ErrorContent`, `DataContent`, `UriContent`); and the events of the GitHub
Copilot SDK that the library carries as raw representations (the usage of a call, the arguments of a tool call that is
being written, the size of a response, and reasoning). The experimental multiplier of the SDK is used only in
`CopilotUsageReader`.

#### Callers

The `AgentService` unit makes one mapper for each turn and maps the updates of the agent; the tests do the
same.

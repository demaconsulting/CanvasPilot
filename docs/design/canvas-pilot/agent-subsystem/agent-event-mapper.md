### AgentEventMapper

![Agent Runtime](AgentSubsystemRuntimeView.svg)

#### Purpose

`AgentEventMapper` turns the stream of `AgentResponseUpdate` that an AI agent produces for one turn into the events of
the provider-independent contract (`AgentEvent`), and turns the exception of a failed turn into a `Failed` event with a
sentence that tells the user what to do. GitHub Copilot and Ollama produce the same kinds of content (text, a function
call, a function result, usage), so one mapping serves both and the chat never learns which provider answered. One
instance serves one turn, because it remembers the calls of the turn and the token that cancels it.

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
- **Usage** gives `Usage(inputTokens, outputTokens)` with the counts that the provider reported, which may be missing.
- **An error** gives a `Failed` event (the kind comes from the words of the error); the rest of the update and the
  stream are not mapped.
- **Anything else** gives nothing: reasoning and pictures outside a tool result are not shown in the chat.

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

The log gets one line for the end of a turn: `Turn completed: provider=… textEvents=n toolCalls=n`, `Turn failed:
provider=… kind=… source=<type of the exception>`, or `Turn canceled: provider=…`. It never gets the text of a message,
an argument or a result of a tool, or the message of an exception, which a test checks with a sentinel text in each.
A log that cannot be written never fails a turn.

#### Data Model

The unit is a `sealed class` with the constants `RefusalPrefix`, `UnknownToolName`, `MaxArgumentTextLength` (60), and
the sentences of the failures, and has no supporting type. The constructor takes the provider kind, the model name of
the settings, an optional logger, and the token that cancels the turn.

#### Key Methods

**Map(update)**: The events of one update, in the order of its content.

**MapAsync(updates)**: The events of a whole turn. The enumerator of the updates is read by hand so that an exception
of the provider is caught at the point where it is raised, and the stream of events ends with the failure.

**MapFailure(exception)**: The failure of an exception, or `null` for a cancellation.

#### Error Handling

The mapper never throws for what a provider does: every exception from the updates is a failure or a stop. It throws
`ArgumentNullException` for a missing argument.

#### Security

The summaries are shown in the chat of the user and hold at most 200 characters of an argument or a result. The log
holds metadata only. A sentence of a failure that names a model uses the name from the settings, never text from the
server. The text of an exception that is not recognized is shown to the user, on one line and cut, because it is the
only information there is; it is not logged.

#### Dependencies

`AgentEvent` and `AgentFailureKind` of the `AgentContract` unit; `AgentReadiness` for the sentence of a refused sign-in
(so that the chat and the settings dialog say the same); `AiProviderKind` of the `Settings` subsystem;
`RollingFileLogger` of the `Logging` subsystem; `AgentResponseUpdate` of the Microsoft Agent Framework; and the content
types of Microsoft.Extensions.AI (`TextContent`, `FunctionCallContent`, `FunctionResultContent`, `UsageContent`,
`ErrorContent`, `DataContent`, `UriContent`).

#### Callers

The `AgentService` unit makes one mapper for each turn and maps the updates of the agent; the tests do the
same.

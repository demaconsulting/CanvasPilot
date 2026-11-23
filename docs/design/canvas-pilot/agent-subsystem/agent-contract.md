### AgentContract

![Agent Structure](AgentSubsystemView.svg)

#### Purpose

`AgentContract` is the provider-independent contract through which the chat runs the AI copilot. It is one interface and
a set of plain records: it holds no behavior beyond the rules that the records enforce on themselves, and it names no
type of an AI library, so the chat can be written and tested without any provider.

#### Data Model

**IAgentService**: `interface`, `IDisposable` — `GetReadinessAsync(CancellationToken)` returns
`Task<AgentReadinessReport>`; `RunTurnAsync(AgentTurnRequest, CancellationToken)` returns
`IAsyncEnumerable<AgentEvent>`, and canceling its token is the Stop of the turn; `ResetConversation()` returns nothing;
the event `FileWritten`
(`EventHandler<FileWrittenEventArgs>`) tells the shell which project file the copilot created or replaced.

**AgentTurnRequest**: `record` — `Payload` (`MessagePayload`, never `null`) and `RestoredHistory`
(`IReadOnlyList<AgentHistoryTurn>`, oldest first, empty by default). The history is the text of earlier turns for a
provider session that starts after a restart; it holds words only, no pictures and no payload.

**AgentHistoryTurn / AgentHistoryRole**: `record` — `Role` (`User` or `Assistant`) and `Text`.

**AgentEvent**: abstract `record` whose constructor is `private protected`, so that only types of the application
library can derive from it and the family is closed. `MaxSummaryLength` is 200. The nested sealed records are:

- `TextDelta(Text)` — a piece of the answer; `Text` is never `null` and may be white space.
- `ToolStarted(CallId, Name, ArgumentSummary)` — a tool call began; `CallId` and `Name` are never empty; the summary is
  trimmed and cut.
- `ToolFinished(CallId, Name, Succeeded, Summary, HasImage)` — the call ended; the `CallId` is that of its
  `ToolStarted`.
- `FileWritten(RelativePath, Replaced)` — the copilot created (`Replaced` is false) or replaced a project file.
- `Usage(InputTokens, OutputTokens, ReasoningTokens, Multiplier, Model, ContextSize)` — what the provider counted for
  one model call; each member is `null` when the provider does not report it. `Multiplier` is the model multiplier that
  a provider bills a message with (the premium requests that it counts; only GitHub Copilot reports one) and is per
  call, not a total for the message; `ReasoningTokens`, `Multiplier`, `Model`, and `ContextSize` are optional and
  default to `null`. `ContextSize` is not reported by the provider: the service stamps it on the usage of a turn from
  the settings, as words (`long context`, `standard context`), only for GitHub Copilot with a named model and a chosen
  context size.
- `Thinking` — the model is thinking: the provider reports reasoning that is not shown. It says that the turn is alive
  and in what phase; it carries no text.
- `ToolProgress(CallId, Name, ArgumentBytes, Path)` — the model is still writing the arguments of a tool call that the
  provider streams before it runs the tool. It carries the bytes received so far (never negative) and the name of the
  file once it is known (`null` before; white space is `null`; a long name is cut like a summary). It never carries the
  content of the arguments. `CallId` and `Name` are never empty, and the `CallId` is that of the `ToolStarted` that
  follows.
- `ModelWorking(BytesReceived)` — the model is writing its answer, and the provider says how many bytes of the response
  were received so far.
- `Completed` — the turn ended normally.
- `Failed(Kind, Message)` — the turn failed; `Message` is never empty and is meant for the user.

The events fall in two groups that the pipeline of the `AgentService` unit treats differently. *Facts* (`ToolStarted`,
`ToolFinished`, `FileWritten`, `Usage`, `Completed`, `Failed`) happen once and are delivered exactly once and in order.
*Text and progress* (`TextDelta`, `Thinking`, `ToolProgress`, `ModelWorking`) say what is going on and may be merged
while a consumer lags: texts are joined, and only the latest progress counts. The records do not enforce this; it is the
rule of the pipeline.

**AgentFailureKind**: `enum` — `NotReady`, `Authentication`, `ProtocolMismatch`, `ModelNotFound`, `ConnectionLost`,
`Busy`, `DataInsideProject` (a folder where the application keeps its own data lies inside the project, so the turn is
refused), and `Unexpected`.

**AgentReadinessReport / AgentReadinessStatus**: `record` — `Status` and `Message`. The statuses are `Ready`,
`NotConfigured`, `CopilotCliMissing`, `CopilotCliTooOld`, `CopilotNotSignedIn`, `CopilotRuntimeUnavailable`,
`OllamaUnreachable`, `OllamaModelMissing`, and `OllamaContextTooSmall`. `IsUsable` is true for `Ready` and
`OllamaContextTooSmall` only.

#### Key Methods

**AgentEvent.Summarize(text)**: Returns the text trimmed; a text longer than 200 characters is cut to 197 characters and
`...` is appended, so the result is never longer than the limit. `null` gives an empty string. The `ToolStarted` and
`ToolFinished` constructors apply it to their summaries, so an event can never carry a longer one.

**Constructors of the records**: Check what each record needs and refuse the rest: `AgentTurnRequest` a `null` payload,
the tool events and `ToolProgress` an empty call id or name, `Failed` and `AgentReadinessReport` an empty or white-space
message, and `TextDelta` a `null` text. `ToolProgress` also clamps a negative byte count to zero and turns a path that
is empty or white space into `null`, and applies `Summarize` to a path, so a progress event can never carry more than a
file name.

**AgentReadinessReport.IsUsable**: The single rule that decides which statuses allow a turn.

#### Error Handling

The records refuse invalid arguments with `ArgumentNullException` or `ArgumentException` when they are built, because an
event that cannot be shown is a defect of the producer. The interface itself reports every expected problem as data (a
readiness report or a `Failed` event); what a service may throw, and how a cancelled turn ends, is part of the contract
of `IAgentService` in the design of the `Agent` subsystem.

#### Dependencies

`MessagePayload` of the `Payload` subsystem (the message in a request). No other subsystem or third-party item is used;
in particular no AI library; this is a design rule that is kept by review and by the dependency view of the model, not
by a test.

#### Callers

The `Chat` subsystem (to run turns and show the readiness report) and the `AppShell` subsystem (which builds the
implementation) use the contract. The units of the `Agent` subsystem that implement and map to it use the records.

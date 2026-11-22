### AgentContract

![Agent Structure](AgentSubsystemView.svg)

#### Purpose

`AgentContract` is the provider-independent contract through which the chat runs the AI copilot. It is one interface and
a set of plain records: it holds no behavior beyond the rules that the records enforce on themselves, and it names no
type of an AI library, so the chat can be written and tested without any provider.

#### Data Model

**IAgentService**: `interface`, `IDisposable` — `GetReadinessAsync(CancellationToken)` returns
`Task<AgentReadinessReport>`; `RunTurnAsync(AgentTurnRequest, CancellationToken)` returns
`IAsyncEnumerable<AgentEvent>`; `ResetConversation()` returns nothing.

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
- `Usage(InputTokens, OutputTokens)` — counts so far; either is `null` when the provider does not report it.
- `Completed` — the turn ended normally.
- `Failed(Kind, Message)` — the turn failed; `Message` is never empty and is meant for the user.

**AgentFailureKind**: `enum` — `NotReady`, `Authentication`, `ProtocolMismatch`, `ModelNotFound`, `ConnectionLost`,
`Busy`, and `Unexpected`.

**AgentReadinessReport / AgentReadinessStatus**: `record` — `Status` and `Message`. The statuses are `Ready`,
`NotConfigured`, `CopilotCliMissing`, `CopilotCliTooOld`, `CopilotNotSignedIn`, `CopilotRuntimeUnavailable`,
`OllamaUnreachable`, `OllamaModelMissing`, and `OllamaContextTooSmall`. `IsUsable` is true for `Ready` and
`OllamaContextTooSmall` only.

#### Key Methods

**AgentEvent.Summarize(text)**: Returns the text trimmed; a text longer than 200 characters is cut to 197 characters and
`...` is appended, so the result is never longer than the limit. `null` gives an empty string. The `ToolStarted` and
`ToolFinished` constructors apply it to their summaries, so an event can never carry a longer one.

**Constructors of the records**: Check what each record needs and refuse the rest: `AgentTurnRequest` a `null` payload,
the tool events an empty call id or name, `Failed` and `AgentReadinessReport` an empty or white-space message, and
`TextDelta` a `null` text.

**AgentReadinessReport.IsUsable**: The single rule that decides which statuses allow a turn.

#### Error Handling

The records refuse invalid arguments with `ArgumentNullException` or `ArgumentException` when they are built, because an
event that cannot be shown is a defect of the producer. The interface itself reports every expected problem as data (a
readiness report or a `Failed` event); what a service may throw, and how a cancelled turn ends, is part of the contract
of `IAgentService` in the design of the `Agent` subsystem.

#### Dependencies

`MessagePayload` of the `Payload` subsystem (the message in a request). No other subsystem or third-party item is used;
in particular no AI library, which is checked by a test.

#### Callers

The `Chat` subsystem (to run turns and show the readiness report) and the `AppShell` subsystem (which builds the
implementation) use the contract. The units of the `Agent` subsystem that implement and map to it use the records.

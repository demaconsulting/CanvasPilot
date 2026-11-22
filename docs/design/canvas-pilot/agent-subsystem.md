## Agent

![Agent Structure](AgentSubsystemView.svg)

### Overview

The `Agent` subsystem is the AI copilot behind the chat. Its boundary starts with a message that the user sent, in the
form of the `MessagePayload` of the `Payload` subsystem, and with the AI provider choices of the `Settings` subsystem;
it ends with a stream of provider-independent events that the `Chat` subsystem shows, and with a report on whether the
chosen provider can be used. It has no user interface. Its parts are the readiness checks for the provider, the tools
that the copilot may call and the instructions that it is given, the agent of the chosen provider, and the service that
keeps one conversation for the open project and runs one turn at a time.

The subsystem is built contract first. It contains two units so far. `AgentContract` is `IAgentService`, the request and
history records, the event records, and the readiness report, all free of any AI library. `CopilotCliLocator` finds the
GitHub Copilot CLI that the user installed, tells it from the launchers that editors put on the `PATH`, and checks its
version; it comes with the seams that make it testable (a process runner, a file system, and an environment reader). The
units that implement the contract are added to this design, and to the model, the requirements, and the verification
design, together with their code; until then no implementation of `IAgentService` exists in the application.

The subsystem will depend on `Settings` (the provider choices), `Project` (the open project folder), `Payload` (the
message), `Knowledge`, `Style`, and `Preview` (the content and the rendering that its tools give the copilot),
`Logging`, `Utilities`, and the third-party AI libraries. Nothing depends on it except `Chat`, through `IAgentService`,
and `AppShell`, which builds it. `Settings` does not depend on it: `Settings` owns the `IProviderCheck` interface
through which the settings dialog can ask whether a provider works, and the `Agent` subsystem is expected to implement
that interface, as the `Composer` subsystem owns `IMessageBuilder` for the `Payload` subsystem.

### Interfaces

**IAgentService**: The AI copilot as the chat sees it.

- *Type*: In-process .NET interface; disposable. It is a part of the `AgentContract` unit.
- *Role*: Provider (the chat and the application shell are consumers).
- *Contract*: `GetReadinessAsync` returns an `AgentReadinessReport` for the settings in effect. `RunTurnAsync` takes an
  `AgentTurnRequest` and a cancellation token and returns the events of the turn in order as an
  `IAsyncEnumerable<AgentEvent>`. `ResetConversation` forgets the conversation so that the next turn starts a new
  provider session.
- *Constraints*: Only one turn runs at a time; a request made while a turn runs gets a single `Failed` event of kind
  `Busy`. A turn ends with exactly one `Completed` or `Failed` event, or simply ends when the caller cancels; no event
  follows either. A provider that cannot be used is reported, not thrown.

**Turn records**: What one turn is asked and what it answers.

- *Type*: Immutable .NET records (`AgentTurnRequest`, `AgentHistoryTurn`, the `AgentEvent` family, and
  `AgentReadinessReport`), described in the design of the `AgentContract` unit.
- *Role*: Provider (data).
- *Contract*: A request carries the message and, after a restart, the words of the earlier turns; a turn answers with
  events that end with `Completed` or `Failed`; the readiness report carries a status and one sentence.
- *Constraints*: None of the records names a type of an AI library.

**CopilotCliLocator**: The installed GitHub Copilot CLI, as the readiness checks need it.

- *Type*: In-process .NET class with the result record `CopilotCliInfo`, documented in the design of the
  `CopilotCliLocator` unit.
- *Role*: Provider (the readiness checks and the Copilot runtime start are consumers).
- *Contract*: `LocateAsync` takes the path from the settings (empty for automatic detection) and returns the status,
  path, version, one sentence for the user, and the candidates that were rejected with the reason for each.
- *Constraints*: The CLI is never included or downloaded; a path from the settings is the only candidate and never falls
  back to automatic detection; a shim of an editor is never used for automatic detection; every problem is a result and
  only cancellation is thrown.

**IProviderCheck**: The connection check that the settings dialog can ask for.

- *Type*: In-process .NET interface owned by the `Settings` subsystem, with the `ProviderCheckResult(IsUsable, Message)`
  record; both are documented in the design of the `AiProviderSettingsPage` unit.
- *Role*: Consumer (the `Agent` subsystem implements it when its readiness checks exist).
- *Contract*: `CheckAsync(AiProviderSettings, CancellationToken)` checks settings that may not have been saved yet and
  returns a result; a provider that cannot be used is a result, not an exception.
- *Constraints*: The interface names no type of the `Agent` subsystem, so `Settings` does not depend on it.

### Design

The contract is deliberately made of plain records and one interface. The chat needs to run a turn, stop it, show its
events as they arrive, and explain a failure; none of that needs to know whether GitHub Copilot or Ollama answers, so no
contract type names a type of an AI library, which a test of the `AgentContract` unit checks. Failure is data, not
control flow: a provider that is missing, not signed in, or out of reach is a readiness report, and a turn that fails is
a `Failed` event with a kind that the chat can map to advice. Cancellation is not a failure: the consumer asked for it,
so the stream just ends and the chat marks the reply as stopped.

The subsystem tests act as the chat will: a hand-written scripted implementation of `IAgentService` stands in for an AI
provider, and the tests run a turn through the interface and read the readiness report, so that the contract is proven
usable before any provider is written against it. The Copilot CLI is found by a rule that is part of the design of the
`CopilotCliLocator` unit; the subsystem test reproduces the layout of a real machine that has both the CLI and the VS
Code shim, and checks that the CLI is found, that a machine with only the shim is told so, and that a path chosen in the
settings is used.

The sections of the units are added to this design as the units are built: the readiness checks, the tool registry and
the project tools, the instructions and the mapper of the payload to a provider message, the mapper of provider updates
to events and the provider factory, and the service that implements `IAgentService`.

### AgentProviderFactory

![Agent Runtime](AgentSubsystemRuntimeView.svg)

#### Purpose

`AgentProviderFactory` makes the agent of a conversation (`AIAgent` of the Microsoft Agent Framework) for the AI
provider that the settings choose, from the settings, the tools of the project, and the instructions. Everything above
it is the same for every provider, so the choice of the provider is made here and nowhere else. The `AgentService` unit
asks it for an agent when a conversation starts and when the project or the relevant settings change.

#### Interfaces

**IAgentFactory** is the seam of the unit: `CreateAsync(AgentCreationRequest, cancellationToken)` returns an
`AgentCreationResult`, `DeleteSessionAsync(AgentSession, cancellationToken)` deletes what the provider kept for a
conversation that ends, and the factory is `IAsyncDisposable`. Tests of the service and of the chat use a factory that
puts an agent on a scripted chat client in place of a real provider.

**AgentCreationRequest(Settings, Tools, Instructions, ProjectFolder)** carries the `AiProviderSettings`, the tools that
the agent may call and nothing else (the registry has confined them to the project), the instructions, and the full path
of the project folder. Two optional members, `CopilotDataDirectory` and `CopilotLogger`, name the folder where the
Copilot runtime keeps its data and the logger that it writes to; the factory hands both to the starter of the runtime
when it starts the runtime and uses neither for Ollama. The agent service chooses them, and the factory never decides
where the runtime keeps its files.

**AgentCreationResult** is either an agent (`IsCreated`, `Agent`) or the reason that none could be made (`Failure`, an
`AgentReadinessReport`, the same status and sentence that the readiness check uses for the same cause). It is
`IAsyncDisposable` and disposes what belongs to its agent alone.

**ICopilotAgentSource** is the started Copilot runtime as the factory uses it: `CreateAgent(CopilotAgentRequest)`,
`DeleteSessionAsync(sessionId)`, and `DisposeAsync`. The runtime of the AI library (`CopilotRuntime`) is a sealed class
that can only be started when a Copilot CLI can be run, so the interface lets the tests stand in for it. The starter of
the runtime hands one over in `CopilotStartResult.Agents` when it was asked to start for real.
**CopilotRuntimeAgentSource** is the implementation on a real `CopilotRuntime`; it holds the one call of
`CopilotAgentFactory.Create` of the application.

#### The Ollama Agent

For a provider of Ollama the factory checks the settings first. The address must be an absolute `http` or `https`
address (`AiProviderSettings.IsValidOllamaEndpoint`), the model name must not be blank, and the context window must be
in the range of the settings (512 to 2,000,000 tokens). Otherwise the result is `NotConfigured` with a sentence that
says what to enter, and no request is made. Address and model are trimmed.

With usable settings the factory builds, for each agent:

1. an `HttpClient` with `BaseAddress` set to the address and `Timeout` infinite (a local model may need minutes to load,
   and Stop is what ends a turn), over the handler that it was given for tests or over the real network;
2. an `OllamaApiClient` for the model, which is an `IChatClient`;
3. an `OllamaContextSizingChatClient` around it with the context window of the settings, which puts `num_ctx` on every
   request, because Ollama loads a model at the window that a request names and otherwise at its own small default;
4. the agent, by `ChatClientAgentFactory.Create(client, tools, instructions, "CanvasPilot")`, which adds the decorator
   that lets the model see a picture that a tool returned.

The result owns the sizing client (which disposes the Ollama client) and the `HttpClient`; disposing it releases both
once. The handler that a test supplies is never disposed by the factory. When the library refuses the tools (none, or a
repeated name) the exception is thrown after the clients were released.

#### The Copilot Agent

For GitHub Copilot the factory finds the CLI with `CopilotCliLocator` (the path of the settings, or automatic detection;
the locator caches its answer) and reports a CLI that is missing or too old with the status and sentence of the
readiness check, without asking the starter. It then asks the `ICopilotRuntimeStarter` to start the runtime on that CLI
(`CheckOnly` false) under a gate, so that concurrent requests start it once, and keeps the `ICopilotAgentSource` that it
gets. Only a started runtime is kept: a starter that says `NeedsAgentKitUpdate` or `Failed`, or says `Started` without a
source, gives the result `CopilotRuntimeUnavailable` with the sentence of the starter (or `NoAgentSourceMessage`), and
the next request asks the starter again.

An agent is made with `CopilotAgentRequest(Tools, Instructions, "CanvasPilot", Model, WorkingDirectory)`: the model of
the settings, trimmed, or `null` for the default model of the runtime when the name is blank, and the project folder as
the working directory (it grants nothing; the tools decide what may be touched). The one call of
`CopilotAgentFactory.Create` passes no permission handler, so the AI library installs its default-deny handler, which
approves exactly the supplied tools, one call at a time, and refuses everything else, including the tools of the Copilot
CLI itself. A test reads the source to show that there is one call and that it names no handler.

**This build is gated.** The starter that is shipped, `GatedCopilotRuntimeStarter`, answers `NeedsAgentKitUpdate` and
never touches the runtime of the AI library, because the library cannot yet be pointed to the CLI that the user
installed and the application must not bundle or download one. The factory therefore answers a request for Copilot with
`CopilotRuntimeUnavailable`, "Copilot support needs an AgentKit update; use Ollama meanwhile.", and no agent, which is
the typed failure that the service and the chat show. The day the library can be pointed to a CLI, the starter is
replaced by one that starts a `CopilotRuntime` with the path of the locator and returns a `CopilotRuntimeAgentSource`;
nothing else in this unit changes.

**The package stays referenced.** The application keeps its reference to `DemaConsulting.AgentKit.Agents.Copilot`
although no runtime can be started in this build: `CopilotRuntimeAgentSource` is compiled against it, so the Copilot
branch is written and reviewed now, the tests of the OTS items prove that no runtime file is shipped
(`CopilotSkipCliDownload`), and the day the starter is replaced is a change of one class.

#### Ownership

- A **result** owns the clients of its Ollama agent. A Copilot agent owns nothing, because the runtime is shared.
- The **factory** owns the Copilot runtime that its starter gave it, and disposes it once when the factory is disposed.
  Disposing a result never disposes the runtime. A request for Copilot after the factory was disposed throws
  `ObjectDisposedException`.
- Sessions that the runtime keeps are deleted through `IAgentFactory.DeleteSessionAsync(session)`, which the service
  calls when a conversation ends. The factory deletes only a Copilot session that has an id (the library gives it one
  when a run begins), through the runtime that it started; a session of Ollama, a session without an id, and a session
  of a runtime that was never started are left alone, so the call is always safe.

#### Logging

The log gets one line for each agent that is made (`Agent created: provider=Ollama model=… tools=n contextTokens=n`,
`Agent created: provider=GitHubCopilot model=… tools=n`) and one for a Copilot agent that could not be made (`Agent not
created: provider=GitHubCopilot status=…`). It never gets the instructions, the address of the server, a token, or a
path. A log that cannot be written never fails the request.

#### Data Model

`AgentProviderFactory` is a `sealed class` with the constants `AgentName` (`CanvasPilot`), `InvalidEndpointMessage`,
`MissingModelMessage`, `InvalidContextWindowMessage`, and `NoAgentSourceMessage`. The constructor takes the locator, the
starter, the handler of the HTTP requests, and the logger, each optional (the real ones when absent). The supporting
types are `AgentCreationRequest`, `AgentCreationResult`, `IAgentFactory`, `ICopilotAgentSource`, `CopilotAgentRequest`,
and `CopilotRuntimeAgentSource`; none has a part definition of its own.

#### Error Handling

A provider that cannot be used is a result, not an exception: unusable settings (`NotConfigured`), a CLI that is missing
or too old, and a runtime that cannot be started (`CopilotRuntimeUnavailable`). Exceptions are for programming errors
and for what the AI library refuses: a missing argument, a blank project folder, a set of tools that the library
refuses, a cancellation of the start, and a request after the factory was disposed. The start of the runtime is guarded
by a gate that is always released.

#### Security

The agent can call only the supplied tools, which the registry has confined to the project folder. For Copilot this is
enforced twice: the AI library derives the permissions from the same list and installs its default-deny handler, and the
factory supplies no handler of its own. The application starts no process itself: only the starter does, on the CLI that
the locator found by its rule. The factory holds no credential; the credentials that the Copilot runtime uses are found
by the readiness check and read by the runtime from the environment. The address of Ollama is the user's setting, used
as given.

#### Dependencies

`AgentCreationResult` carries the `AgentReadinessReport` of the `AgentContract` unit; the Copilot branch uses
`CopilotCliLocator` and, through the starter interface, the `AgentReadiness` unit (which defines the starter and the
gated starter); `AiProviderSettings` and `AiProviderKind` of the `Settings` subsystem; `RollingFileLogger` of the
`Logging` subsystem; DemaConsulting.AgentKit (`ChatClientAgentFactory`, `OllamaContextSizingChatClient`,
`CopilotAgentFactory`, `CopilotRuntime`), OllamaSharp (`OllamaApiClient`), the Microsoft Agent Framework (`AIAgent`),
and Microsoft.Extensions.AI (`AIFunction`, `IChatClient`).

#### Callers

The `AgentService` unit holds one factory for the application, asks it for the agent of each conversation, deletes the
session and disposes each result when the conversation ends, and disposes the factory when the application ends.

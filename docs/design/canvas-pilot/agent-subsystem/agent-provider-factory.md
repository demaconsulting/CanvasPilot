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
conversation that ends, `DescribeSession(AgentSession)` tells the log what the provider calls a session and where it
recorded it (see *The Description of a Session*), and the factory is `IAsyncDisposable`. `DescribeSession` has a default
implementation that answers nothing, so a provider or a test double that has no session of its own needs no code for it.
Tests of the service and of the chat use a factory that puts an agent on a scripted chat client in place of a real
provider.

**AgentCreationRequest(Settings, Tools, Instructions, ProjectFolder)** carries the `AiProviderSettings`, the tools that
the agent may call and nothing else (the registry has confined them to the project), the instructions, and the full path
of the project folder. Two optional members, `CopilotDataDirectory` and `CopilotLogger`, name the folder where the
Copilot runtime keeps its data and the logger that it writes to; the factory hands both to the starter of the runtime
when it starts the runtime and uses neither for Ollama. The agent service chooses them, and the factory never decides
where the runtime keeps its files.

**AgentCreationResult** is either an agent (`IsCreated`, `Agent`) or the reason that none could be made (`Failure`, an
`AgentReadinessReport`, the same status and sentence that the readiness check uses for the same cause). It is
`IAsyncDisposable` and disposes what belongs to its agent alone.

**ICopilotAgentSource** is the started Copilot runtime as the factory uses it, with `CopilotAgentRequest` and
`CopilotRuntimeAgentSource`; they belong to the `CopilotRuntimeStarter` unit, whose design describes them, including the
one call of `CopilotAgentFactory.Create` and the default-deny permission handler.

#### The Ollama Agent

For a provider of Ollama the factory checks the settings first. The address must be an absolute `http` or `https`
address (`AiProviderSettings.IsValidOllamaEndpoint`), the model name must not be blank, and the context window must be
in the range of the settings (512 to 2,000,000 tokens). Otherwise the result is `NotConfigured` with a sentence that
says what to enter, and no request is made. Address and model are trimmed.

The model settings are then validated by the catalog of the server (`IOllamaModelCatalogs`), in 15 seconds at most:
`SelectAsync(ModelSettings(model){ReasoningEffort, ContextWindowTokens}, ToolCalls)` of AgentKit. A model that the
server does not have, that cannot call tools, that cannot be used (an embedding model), that does not offer the chosen
effort, or that was trained for fewer tokens than the window is the result `ModelSettingsInvalid` with the problems of
the library joined by a space, and no agent is made and no chat request is sent. The name is replaced by the id that the
catalog spells (`qwen3` becomes `qwen3:latest`). A server that does not answer, answers something else, or is too slow
is not an error here: the model is then tried as it was typed, with no selection, so that the typed name still works as
it did before the model list. (That is the manual-entry path; the readiness check and the settings page say why the list
is missing.)

With usable settings the factory builds, for each agent:

1. an `HttpClient` with `BaseAddress` set to the address and `Timeout` infinite (a local model may need minutes to load,
   and Stop is what ends a turn), over the handler that it was given for tests or over the real network;
2. an `OllamaApiClient` for the model (the id of the catalog), which is an `IChatClient`;
3. an `OllamaContextSizingChatClient` around it with the context window of the settings, which puts `num_ctx` on every
   request, because Ollama loads a model at the window that a request names and otherwise at its own small default;
4. the agent, by `ChatClientAgentFactory.Create(client, tools, instructions, "CanvasPilot", selection)`, which puts the
   model, the thinking level of the reasoning effort, and the window of the selection on every request (the window is
   the one that the sizing client sends, so the two agree) and adds the decorator that lets the model see a picture that
   a tool returned.

The result owns the sizing client (which disposes the Ollama client) and the `HttpClient`; disposing it releases both
once. The handler that a test supplies is never disposed by the factory. When the library refuses the tools (none, or a
repeated name) the exception is thrown after the clients were released.

#### The Copilot Agent

For GitHub Copilot the factory finds the CLI with `CopilotCliLocator` (the path of the settings, or automatic detection;
the locator caches its answer) and reports a CLI that is missing or too old with the status and sentence of the
readiness check, without asking the starter. It then asks the shared `ICopilotRuntimeStarter` for the runtime on that
CLI (`StartAsync` with the path, the version, the data folder, and the logger of the request). The factory does not
serialize or cache the start: the starter starts the runtime once for all callers, including the readiness check, and
gives every caller the same running runtime. A start that is not `Started` gives the result with the status of
`CopilotStartResult.ReadinessStatus` (`CopilotCliTooOld`, `CopilotNotSignedIn`, or `CopilotRuntimeUnavailable`) and the
sentence of the starter; a `Started` answer without a source gives `CopilotRuntimeUnavailable` with
`NoAgentSourceMessage`, "The Copilot runtime started but cannot make an agent. Choose Check connection to try again."
The next request asks the starter again, so a fix is noticed.

The model is then validated with the catalog of the running runtime (`ICopilotAgentSource.Models`): when the settings
name a model, `SelectAsync(ModelSettings(model){ReasoningEffort, ContextTier}, ToolCalls)` gives the selection, and a
model that is not offered to the account, cannot be used, cannot call tools, or does not offer the chosen effort or
context size is the result `ModelSettingsInvalid` with the problems of the library and no agent. No context window is
stated, because the runtime sets its own and the library refuses one for a plain Copilot agent; the context size (tier)
is the Copilot way to choose a larger window, and AgentKit sends it to the session as `config.Inference.ContextTier`,
with the catalog's own spelling of the id, on every create and resume of the session. A catalog that cannot be read (the
runtime is not connected, or was stopped) is `CopilotRuntimeUnavailable` with its message. Settings that name no model
make no selection, and the runtime uses its default model; a reasoning effort or a context size without a model is not
used, because it cannot be checked. The log line `Agent created: provider=GitHubCopilot model effort contextTier tools`
names the validated model, effort, and size, each `(default)` when none was chosen.

An agent is made with `CopilotAgentRequest(Tools, Instructions, "CanvasPilot", WorkingDirectory, Selection)`: exactly
the supplied tools, instructions, and name, the project folder as the working directory (it grants nothing; the tools
decide what may be touched), and the selection (or `null`), from which the library takes the model and the reasoning
effort. The one call of `CopilotAgentFactory.Create` (in `CopilotRuntimeAgentSource`) passes no permission handler, so
the AI library installs its default-deny handler, which approves exactly the supplied tools, one call at a time, and
refuses everything else, including the tools of the Copilot CLI itself. This is a property of the running Copilot
runtime, so no test reads the source to infer it; it is verified by the manual check of the live runtime (check 10 of
the manual verification in the verification of the system).

**The installed CLI is run, never bundled.** The runtime runs on the CLI that the user installed, at the path that the
locator found, so nothing is shipped or downloaded: the packaging guards (`CopilotSkipCliDownload`, the FileAssert test
`Copilot_RuntimeNotPublished`, and the OTS test that the build output holds no Copilot runtime file) are unchanged. The
sign-in that the runtime uses and the way that its source, login, and host are reported are described in the design of
the `CopilotRuntimeStarter` unit.

#### The Description of a Session

A turn that runs for minutes and ends in a failure or a Stop must be diagnosable from the log alone, so the service asks
the factory what the provider calls the session of the conversation. `DescribeSession(session)` returns an
`AgentSessionInfo(Id, EventsFile)`, a record with the identifier of the session at the provider and the full path of the
file where the provider records the events of the session, each `null` when there is none. `AgentSessionInfo.None` is
the answer for a provider with no session of its own (Ollama), for a session that has no identifier yet, and for any
other kind of session.

For GitHub Copilot the session is the `CopilotAgentSession` of AgentKit, which gets its identifier when the first run
creates the session at the runtime (a conversation that has not run yet has none). The events file is
`{data folder of the runtime}\session-state\{id}\events.jsonl`; the factory keeps the data folder of the request that
made the latest Copilot agent (or the default folder of the service when the request names none), because that is where
the runtime that it started keeps its sessions. The file exists until the session is deleted, which is when the
conversation is dropped. The call reads two fields and touches no file, so it is safe at any time, including after the
runtime was stopped. The service uses it to write the identifier into the lines of the log (whether a run created or
resumed the session, and the reason for a release) and, for a turn that failed or was canceled, the path of the events
file, which is the runtime's own record of the session.

#### Ownership

- A **result** owns the clients of its Ollama agent. A Copilot agent owns nothing, and the result never owns the
  runtime.
- The **runtime** belongs to the starter and is shared by the readiness check and every agent. The factory keeps only
  the last source that made an agent, to delete sessions. A factory made without a starter makes its own
  `CopilotRuntimeStarter` and owns it; one made with a starter owns it only when `ownsStarter` is true (the application
  composition passes the starter that it shares with the readiness check). Disposing an owning factory stops the runtime
  once; disposing any other factory leaves it running. A request for Copilot after the factory was disposed throws
  `ObjectDisposedException`.
- Sessions that the runtime keeps are deleted through `IAgentFactory.DeleteSessionAsync(session)`, which the service
  calls when a conversation ends. The factory deletes only a Copilot session that has an id (the library gives it one
  when a run begins), through the last source that it got; a session of Ollama, a session without an id, and a session
  when no runtime was used are left alone, so the call is always safe. When the runtime was replaced or stopped, the
  source throws `ObjectDisposedException`, which is tolerated, because the runtime took its sessions with it. A chat
  turn that runs on a runtime that was replaced fails with the error for a stopped runtime (the known limit in the
  design of `CopilotRuntimeStarter`).

#### Logging

The log gets one line for each agent that is made
(`Agent created: provider=Ollama model=… effort=… tools=n contextTokens=n`,
`Agent created: provider=GitHubCopilot model=… effort=… tools=n`, the effort being `(default)` when none is chosen) and
one for an agent that could not be made (`Agent not created: provider=… status=…`, and
`Ollama model settings not validated: the server did not describe its models` when the typed model is tried as it is).
It never gets the instructions, the address of the server, a token, or a path. A log that cannot be written never fails
the request.

#### Data Model

`AgentProviderFactory` is a `sealed class` with the constants `AgentName` (`CanvasPilot`), `InvalidEndpointMessage`,
`MissingModelMessage`, `InvalidContextWindowMessage`, and `NoAgentSourceMessage`. The constructor takes the locator, the
starter, the handler of the HTTP requests, the logger, `ownsStarter`, and the Ollama catalogs, each optional (the real
ones when absent, the catalogs over the given handler; a starter that the factory makes is always owned). The supporting
types are `AgentCreationRequest`, `AgentCreationResult`, `AgentSessionInfo`, and `IAgentFactory`; none has a part
definition of its own. The Copilot source types are those of the `CopilotRuntimeStarter` unit. Besides the last source
that made an agent, the factory remembers the data folder of the request that made it, for `DescribeSession`.

#### Error Handling

A provider that cannot be used is a result, not an exception: unusable settings (`NotConfigured`), a CLI that is missing
or too old, a runtime that cannot be started (`CopilotCliTooOld`, `CopilotNotSignedIn`, or
`CopilotRuntimeUnavailable`), and model settings that the catalog refuses (`ModelSettingsInvalid`). Exceptions are for
programming errors and for what the AI library refuses: a missing
argument, a blank project folder, a set of tools that the library refuses, a cancellation of the start, and a request
after the factory was disposed.

#### Security

The agent can call only the supplied tools, which the registry has confined to the project folder. For Copilot this is
enforced twice: the AI library derives the permissions from the same list and installs its default-deny handler, and the
factory supplies no handler of its own. The application starts no process itself: only the starter does, on the CLI that
the locator found by its rule. The factory holds no credential and passes no token; the credentials that the Copilot
runtime uses are found by the runtime itself (in the environment, in the sign-in of the Copilot CLI, or in the GitHub
CLI). The address of Ollama is the user's
setting, used as given.

#### Dependencies

`AgentCreationResult` carries the `AgentReadinessReport` of the `AgentContract` unit; the Copilot branch uses
`CopilotCliLocator` and the `CopilotRuntimeStarter` unit (through `ICopilotRuntimeStarter`, `CopilotStartResult`, and
`ICopilotAgentSource`); both branches use `ModelRequirements` (the policy of the `ProviderModelCatalog` unit) and
`IOllamaModelCatalogs`; `AiProviderSettings` and `AiProviderKind` of the `Settings` subsystem; `RollingFileLogger` of
the `Logging` subsystem; DemaConsulting.AgentKit (`ChatClientAgentFactory`, `OllamaContextSizingChatClient`, and the
model catalog, `ModelSettings`, and `ModelSelection` types of its Core package), OllamaSharp (`OllamaApiClient`), the
Microsoft Agent Framework (`AIAgent`), and Microsoft.Extensions.AI (`AIFunction`, `IChatClient`).

#### Callers

The `AgentService` unit holds one factory for the application, asks it for the agent of each conversation, asks it to
describe the session of a conversation for its log, deletes the
session and disposes each result when the conversation ends, and disposes the factory when the application ends. The
`AppShell` subsystem builds the factory with the starter that it shares with the readiness check.

### CopilotRuntimeStarter

![Agent Runtime](AgentSubsystemRuntimeView.svg)

#### Purpose

`CopilotRuntimeStarter` starts the GitHub Copilot runtime of the AI library on the Copilot CLI that the user installed,
shares that one runtime among everyone who asks for it, and turns every way that the start can fail into one sentence
for the user. The readiness check ("Check connection"), the factory of the agents (for every conversation), and the list
of models of the settings page all get the same running runtime, because the runtime keeps its data in one folder that
two runtimes must not use. The unit also holds its supporting types: `ICopilotRuntimeStarter` with
`CopilotStartRequest`, `CopilotStartResult`, and `CopilotStartStatus`, and `ICopilotAgentSource` with
`CopilotAgentRequest` and `CopilotRuntimeAgentSource`, the running runtime as the factory uses it.

#### Data Model

**CopilotRuntimeStarter**: `sealed class`, implements `ICopilotRuntimeStarter` and `IAsyncDisposable`. The constructor
takes the folder for the data of the runtime that a request does not name (`null` for the folder that
`AgentService.GetCopilotDataDirectory()` gives) and the function that starts a runtime. Fields: the start function
(`Func<CopilotRuntimeOptions, CancellationToken, Task<ICopilotAgentSource>>`), the default folder, a semaphore that
serializes starts and the disposal, the running source (or `null`), its key (the CLI path and the data folder), and the
flag that the starter was disposed. The default start function starts a real `CopilotRuntime` and wraps it in a
`CopilotRuntimeAgentSource`; a test supplies a stand-in, because the runtime is a sealed class that needs a real CLI.
Constants: `NotSignedInMessage`, `CliPathMessage`, and `StartFailedMessage`.

**CopilotStartRequest**: `record(CliPath, CliVersion)` with the optional `DataDirectory` (the folder where the runtime
keeps its home and its sessions) and `Logger` (the logger that records metadata only). `CliPath` is the full path that
the locator found, from the path in the settings first. The request has no flag that asks for a check only: the
readiness check and the factory ask the same question and get the same runtime.

**CopilotStartStatus**: `enum` of `Started`, `CliTooOld`, `NotSignedIn`, and `Failed`.

**CopilotStartResult**: `record(Status, Message, Agents)`. `Agents` is the running runtime as an `ICopilotAgentSource`
when the status is `Started`, otherwise `null`. `ReadinessStatus` maps `Started` to `Ready`, `CliTooOld` to
`CopilotCliTooOld`, `NotSignedIn` to `CopilotNotSignedIn`, and `Failed` to `CopilotRuntimeUnavailable`.

**ICopilotAgentSource**: the started runtime as the factory uses it: `Models` (the `IModelCatalog` of the runtime),
`CreateAgent(CopilotAgentRequest)`, `DeleteSessionAsync(sessionId, cancellationToken)`, and `DisposeAsync`.
**CopilotAgentRequest** carries the tools, the instructions, the name, the working directory, and the `ModelSelection`
(`null` for the default model of the runtime) that the catalog validated for the settings. **CopilotRuntimeAgentSource**
is the implementation on a real `CopilotRuntime`; it makes one `CopilotModelCatalog` for the runtime when it is made
(the catalog keeps what it read for five minutes, and `RefreshAsync` reads again), so the one catalog is shared by the
factory, the readiness check, and the model list. It holds the one call of `CopilotAgentFactory.Create` of the
application, which takes the model and the reasoning effort from the selection, and passes no permission handler
(`onPermissionRequest` is `null`), so the AI library installs its default-deny handler, which approves exactly the
supplied tools, one call at a time, and refuses everything else, including the tools of the Copilot CLI itself. This is
a property of the running runtime, so no test reads the source to infer it; it is verified by the manual check of the
live runtime (check 10 of the manual verification in the verification of the system).

#### How the Runtime Is Started

The options are `CopilotRuntimeOptions(dataDirectory, gitHubToken: null, logger, CopilotRuntimeSource.InstalledCli,
cliPath)`. The installed CLI is the source, so nothing is bundled or downloaded, and the `Bundled` source is never
chosen. `cliPath` is the path that the locator found. The data folder is, in this order, the folder of the request, the
folder that the starter was made with, and the default of `AgentService.GetCopilotDataDirectory()`; it is always below
the local data of the user and never inside a project. No token is read, stored, logged, or passed: the field is `null`,
and the application has no token setting and no sign-in page. The runtime signs in by itself, from the environment of
the application or from a signed-in GitHub CLI (see *Sign-in*).

The library refuses some paths while the options are built (a relative path, a `.cmd` or `.bat` file, and so on). That
`ArgumentException` is answered before anything is stopped or started.

#### Sharing

One runtime is shared. All requests are serialized by the semaphore (single flight), and the runtime is identified by
the key (CLI path, data folder):

- A request with the key of the running runtime gets that runtime, and nothing is started.
- A request with another key stops the running runtime and starts a new one, because only one runtime may use one data
  folder at a time.
- A start that fails is not remembered: nothing is kept, so the next request tries again, and a user who fixed the cause
  (signed in, upgraded the CLI) only chooses Check connection.
- A start that is cancelled throws `OperationCanceledException`, as the cancellation of the caller, and is not
  remembered.
- `DisposeAsync` stops the running runtime once (a second call does nothing), and a start after it throws
  `ObjectDisposedException`.

The sources that the starter hands out belong to it. A receiver (the factory, the readiness check) never disposes one,
and a source that was stopped because the runtime was replaced or stopped throws `ObjectDisposedException` when it is
used, which the factory tolerates when it deletes a session.

#### Sign-in

The AI library gives its runtime a home of its own (the data folder) and switches the keychain off, so the sign-in that
the Copilot CLI keeps for itself (`copilot`, then `/login`) is **not** used. This was verified on a machine where the
CLI was signed in with `/login` and the GitHub CLI was signed in: with `gh` on the `PATH` and no token variable the
runtime started and was authenticated; with `gh` removed from the `PATH` and no token variable it started but reported
that it was not authenticated, and the library refused it; with `COPILOT_GITHUB_TOKEN` set and no `gh` it started. The
credentials that work are therefore a signed-in GitHub CLI (`gh auth login`, with `gh` on the `PATH`) and the variables
`COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, and `GITHUB_TOKEN`. This is a documented limitation, not hidden, and a request to
the AI library (see the design of the AgentKit item): to be able to use the session of the Copilot CLI.

#### Key Methods

**StartAsync(request, cancellationToken)**: Returns the answer for the request.

1. Preconditions: the request is not `null` and has a non-blank `CliPath`.
2. Choose the data folder and build the options. An `ArgumentException` from building them is
   `Failed("The Copilot CLI path in Settings cannot be used: " + cause)`, where the cause is the message without the
   suffix `(Parameter '...')`.
3. Wait for the semaphore. Throw `ObjectDisposedException` when the starter was disposed.
4. When a runtime runs with the same key, return `Started` with it and the sentence "GitHub Copilot CLI {version} is
   running." Otherwise stop the running runtime (if any), call the start function, keep the source and the key, and
   return `Started`.
5. When the start function throws `InvalidOperationException`, `IOException`, `ArgumentException`, or
   `NotSupportedException`, return the result of the next section. Anything else, including the cancellation, goes on
   to the caller. The semaphore is always released.

**DisposeAsync()**: Waits for the semaphore, marks the starter disposed once, and stops the running runtime.

#### Error Handling

The AI library throws `InvalidOperationException` with an explanatory text and has no type for the causes, so the
starter recognizes the words of the message, ignoring case. The words are the one assumption of the unit, and the manual
check of the live runtime confirms them with a real CLI.

| The message of the library holds | Status | Sentence |
| --- | --- | --- |
| "is not signed in" | `NotSignedIn` | `NotSignedInMessage`, below |
| "too old" or "protocol version" | `CliTooOld` | the message of the library, which has the upgrade commands |
| anything else (any of the four exception types) | `Failed` | the library's message, or `StartFailedMessage` if blank |

- `NotSignedInMessage`: "Copilot is not signed in for CanvasPilot. Sign in with `gh auth login` (or set the
  COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then choose Check connection. Signing in to the
  Copilot CLI with /login does not apply to CanvasPilot."
- `StartFailedMessage`: "The Copilot runtime could not be started. Choose Check connection to try again."
- Started: "GitHub Copilot CLI {version} is running."

A start that fails is a result. Exceptions are for programming errors (a `null` request, a blank path), the cancellation
of the caller, and a start after the starter was disposed. A log that cannot be written never fails a start.

#### Security

The unit passes no token and reads none, so it cannot log or store one. The messages that it returns are the sentence of
this unit or the message of the AI library, which holds an explanation and no credential. The CLI is run only at the
path that the locator accepted by its rule (an `.exe` that reports a new enough version). The runtime has no tool of the
application except the supplied ones, because the factory passes no permission handler (see the factory design). The
data folder of the runtime is below the local data of the user, so the files of the runtime never go into a project.

#### Dependencies

`CopilotCliLocator` (its path and version arrive in the request; the unit does not call it), `AgentReadinessStatus` of
the contract, `AgentService.GetCopilotDataDirectory` for the default folder, and DemaConsulting.AgentKit
(`CopilotRuntime`, `CopilotRuntimeOptions`, `CopilotRuntimeSource`, `CopilotAgentFactory`), the Microsoft Agent
Framework (`AIAgent`), and Microsoft.Extensions.AI (`AIFunction`) with its logger abstractions (`ILogger`).

#### Callers

The `AgentReadiness` unit (the last checks of GitHub Copilot), the `AgentProviderFactory` unit (every Copilot
conversation), and the `ProviderModelCatalog` unit (the list of models), all through `ICopilotRuntimeStarter`. The
`AppShell` subsystem builds the one starter that all three share and stops it when the application ends.

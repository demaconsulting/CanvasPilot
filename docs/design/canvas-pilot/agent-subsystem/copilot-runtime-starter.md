### CopilotRuntimeStarter

![Agent Runtime](AgentSubsystemRuntimeView.svg)

#### Purpose

`CopilotRuntimeStarter` starts the GitHub Copilot runtime of the AI library on the Copilot CLI that the user installed,
shares that one runtime among everyone who asks for it, and turns every way that the start can fail into one sentence
for the user. The readiness check ("Check connection"), the factory of the agents (for every conversation), and the list
of models of the settings page all get the same running runtime, because the runtime keeps its data in one folder that
two runtimes must not use. The unit also holds its supporting types: `ICopilotRuntimeStarter` with
`CopilotStartRequest`, `CopilotStartResult`, `CopilotStartStatus`, and `CopilotSignInInfo`, and `ICopilotAgentSource`
with `CopilotAgentRequest` and `CopilotRuntimeAgentSource`, the running runtime as the factory uses it.

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
`SignIn` (the `CopilotSignInInfo` of how the runtime signed in, or `null` when not known),
`CreateAgent(CopilotAgentRequest)`, `DeleteSessionAsync(sessionId, cancellationToken)`, and `DisposeAsync`.

**CopilotSignInInfo**: `record(Source, Login, Host)` with `Source` a `CopilotSignInSource` of the AI library (unknown,
GitHub token, token provider, environment variable, Copilot CLI, data folder, or GitHub CLI), `Login` the GitHub login,
and `Host` the host as the runtime reports it (for example `https://github.com`). The record has no field for a token.
`Describe()` words it as one sentence that ends with a full stop: "Signed in as {login} on {host} ({source})." The host
is shown without its scheme and a closing slash. The source words are "GitHub token" (token or token provider), "token
variable", "Copilot CLI sign-in", "stored sign-in" (the data folder), and "GitHub CLI sign-in". A part that is not
known (an unknown source, a blank login or host) is left out, and when all three are unknown the sentence is empty, so
the sentence never claims more than the runtime reported. The method never throws.
**CopilotAgentRequest** carries the tools, the instructions, the name, the working directory, and the `ModelSelection`
(`null` for the default model of the runtime) that the catalog validated for the settings. **CopilotRuntimeAgentSource**
is the implementation on a real `CopilotRuntime`; it builds its `SignIn` from `SignInSource`, `GitHubLogin`, and
`GitHubHost` of the runtime, and it makes one `CopilotModelCatalog` for the runtime when it is made
(the catalog keeps what it read for five minutes, and `RefreshAsync` reads again), so the one catalog is shared by the
factory, the readiness check, and the model list. It holds the one call of `CopilotAgentFactory.Create` of the
application, which takes the model and the reasoning effort from the selection, and passes no permission handler
(`onPermissionRequest` is `null`), so the AI library installs its default-deny handler, which approves exactly the
supplied tools, one call at a time, and refuses everything else, including the tools of the Copilot CLI itself. This is
a property of the running runtime, so no test reads the source to infer it; it is verified by the manual check of the
live runtime (check 10 of the manual verification in the verification of the system).

#### How the Runtime Is Started

The options are
`CopilotRuntimeOptions(dataDirectory, gitHubToken: null, logger, CopilotRuntimeSource.InstalledCli, cliPath)`. The
installed CLI is the source, so nothing is bundled or downloaded, and the `Bundled` source is never chosen. `cliPath` is
the path that the locator found. The data folder is, in this order, the folder of the request, the folder that the
starter was made with, and the default of `AgentService.GetCopilotDataDirectory()`; it is always below the local data of
the user and never inside a project. No token is read, stored, logged, or passed: the field is `null`, and the
application has no token setting and no sign-in page. The token provider and the GitHub host are not given either, and
the switch for the sign-in of the Copilot CLI is left at its default of on (see *Sign-in*). The runtime signs in by
itself.

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
- **Known limit.** A request with another key stops the runtime that a running chat turn may still use, and that turn
  then ends with the error for a stopped runtime (`ObjectDisposedException`, shown as the sentence for a stopped
  runtime). The trigger is another Copilot CLI path typed on the AI Provider page (the listing, **Refresh models**, or
  **Check connection**, which use the entries as typed) while a turn runs. After Cancel, the runtime stays on the path
  that was typed until the next agent request asks for the saved path and restarts it. There is no reference counting,
  by decision: one data folder allows one runtime, the trigger needs a deliberate action in a modal dialog during a
  turn, and the failure is clear and recovers by sending the message again.

The sources that the starter hands out belong to it. A receiver (the factory, the readiness check) never disposes one,
and a source that was stopped because the runtime was replaced or stopped throws `ObjectDisposedException` when it is
used, which the factory tolerates when it deletes a session.

#### Sign-in

The runtime start decides the sign-in, and the starter does not look for one first. The AI library takes the first
source that applies, in this order: a token that the host passes (the starter passes none), the variables
`COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, and `GITHUB_TOKEN`, the sign-in of the Copilot CLI itself (the user runs `copilot`
and types `/login`), a sign-in stored in the data folder, and last a signed-in GitHub CLI (`gh auth login`, with `gh` on
the `PATH`). The host follows the account that signed in, so no host is passed. A token variable that holds a token that
the host does not accept ends the search, so the user clears it. The runtime reports the source, the login, and the
host, and the started result carries them as `CopilotSignInInfo` on the source, so the readiness check can show which
account is in use. A runtime that is changed to another account in the CLI is seen only after the starter has stopped
the runtime, because the runtime that runs is returned for the same key. A setting for the GitHub host and a switch for
the sign-in of the CLI are possible future options of the AI library; the starter uses neither.

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

**DisposeAsync()**: Waits for the semaphore, marks the starter disposed once, and stops the running runtime. A start
that hangs holds the semaphore, so the wait lasts as long as the start. The application composition owns the starter
and, when the application closes, waits at most ten seconds for `DisposeAsync` (a bounded wait, accepted by decision:
the start gate is released only by the start itself, and a token would change the contract of the starter); after that
the application closes anyway.

#### Error Handling

The AI library throws `InvalidOperationException` with an explanatory text and has no type for the causes, so the
starter recognizes the words of the message, ignoring case. The words are the one assumption of the unit, and the manual
check of the live runtime confirms them with a real CLI.

| The message of the library holds | Status | Sentence |
| --- | --- | --- |
| "is not signed in" | `NotSignedIn` | `NotSignedInMessage` and the digest of what was found, below |
| "too old" or "protocol version" | `CliTooOld` | the message of the library, which has the upgrade commands |
| anything else (any of the four exception types) | `Failed` | the library's message, or `StartFailedMessage` if blank |

- `NotSignedInMessage`: "Copilot is not signed in for CanvasPilot. Run `copilot`, then type `/login` to sign in (or sign
  in with `gh auth login`, or set the COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then choose
  Check connection."
- The digest: the message of the library for a runtime that is not signed in is several lines. After a line that says
  that the library tried these sign-in sources in order, it has one indented numbered line for each source, "1. ..." to
  "4. ...", with what it found there (token redacted), and ends with its own advice on how to sign in. The starter
  recognizes the numbered lines by their shape (a number, a full stop, and text), drops line 1 (the token that a host
  passes, which this application never uses), and joins the others with semicolons after the words " What was found: ",
  ending with a full stop, so the message is one line that ends with a full stop, as every report message is. The advice
  of the library is left out because the sentence of the starter gives it. When no numbered line is found, the message
  is `NotSignedInMessage` alone.
- `StartFailedMessage`: "The Copilot runtime could not be started. Choose Check connection to try again."
- Started: "GitHub Copilot CLI {version} is running."

A start that fails is a result. Exceptions are for programming errors (a `null` request, a blank path), the cancellation
of the caller, and a start after the starter was disposed. A log that cannot be written never fails a start.

#### Security

The unit passes no token and reads none, so it cannot log or store one. The messages that it returns are the sentence of
this unit or the message of the AI library (which holds an explanation with every token redacted, and no credential),
and `CopilotSignInInfo` has no field that could hold a token. The CLI is run only at the
path that the locator accepted by its rule (an `.exe` that reports a new enough version). The runtime has no tool of the
application except the supplied ones, because the factory passes no permission handler (see the factory design). The
data folder of the runtime is below the local data of the user, so the files of the runtime never go into a project.

#### Dependencies

`CopilotCliLocator` (its path and version arrive in the request; the unit does not call it), `AgentReadinessStatus` of
the contract, `AgentService.GetCopilotDataDirectory` for the default folder, and DemaConsulting.AgentKit
(`CopilotRuntime`, `CopilotRuntimeOptions`, `CopilotRuntimeSource`, `CopilotAgentFactory`), the Microsoft Agent
Framework (`AIAgent`), and Microsoft.Extensions.AI (`AIFunction`) with its logger abstractions (`ILogger`).

#### Callers

The `AgentReadiness` unit (the third check of GitHub Copilot, and the sign-in sentence of a provider that can be used),
the `AgentProviderFactory` unit (every Copilot conversation), and the `ProviderModelCatalog` unit (the list of models),
all through `ICopilotRuntimeStarter`. The `AppShell` subsystem builds the one starter that all three share and stops it
when the application ends.

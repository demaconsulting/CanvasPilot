### AgentReadiness

![Agent Structure](AgentSubsystemView.svg)

#### Purpose

`AgentReadiness` checks whether the AI provider that the settings choose can be used and says, in one sentence, what it
found and what to do about it. The chat shows the sentence before the first message, and the settings dialog shows it
when the user chooses Check connection. The unit implements `IProviderCheck`, which the Settings subsystem owns, so the
dialog never depends on the Agent subsystem. The unit also holds its supporting types: `ICopilotRuntimeStarter` with the
`GatedCopilotRuntimeStarter` that this build ships, and `IOllamaProbe` with its real `OllamaProbe`.

#### Data Model

**AgentReadiness**: `sealed class`, implements `IProviderCheck`. The constructor takes `CopilotCliLocator`,
`ICopilotRuntimeStarter`, `IProcessRunner`, `IEnvironmentReader`, `ICliFileSystem`, and `IOllamaProbe`, each defaulting
to the real implementation (the starter defaults to the gated one). Its fields are these collaborators only: it holds no
text, no settings, and no logger. Constants: `TokenVariableNames` (`COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, `GITHUB_TOKEN`),
`CredentialProbeTimeout` (5 seconds), `OllamaProbeTimeout` (5 seconds), `NotSignedInMessage`, and
`RuntimeUnavailableMessage`. The result is the `AgentReadinessReport` of the contract (a status and one sentence).

**ICopilotRuntimeStarter**: `StartAsync(CopilotStartRequest, cancellationToken)` returns a `CopilotStartResult`.
`CopilotStartRequest(CliPath, CliVersion, CheckOnly)` says which CLI the locator found and whether the caller only wants
to know that the runtime could be started (the readiness check) or wants it started.
`CopilotStartResult(Status, Message, Agents)` has the status `Started`, `NeedsAgentKitUpdate`, or `Failed`; `Agents` is
the started runtime as an `ICopilotAgentSource` when the request was not a check only (it is documented in the design of
the `AgentProviderFactory` unit, which keeps and disposes it) and is always `null` for a check only.
**GatedCopilotRuntimeStarter** is the starter that this build ships; it always answers `NeedsAgentKitUpdate` and never
touches the AI library.

**IOllamaProbe**: `ListModelsAsync(endpoint, timeout, cancellationToken)` returns `OllamaModelList(Reachable, Models)`,
and `GetContextLengthAsync(endpoint, model, timeout, cancellationToken)` returns the largest context window of a model
in tokens or `null`. **OllamaProbe** is the real probe; its constructor can take an `HttpMessageHandler` so that a test
can stand in for the server.

#### The Checks

**GitHub Copilot**, in order; the first check that fails gives the answer, and the checks after it are not run.

1. *CLI.* `CopilotCliLocator.LocateAsync(settings.CopilotCliPath, refresh)` finds the installed CLI by its rule. A CLI
   that is not found, is not an `.exe`, or cannot report its version is `CopilotCliMissing`; one older than 1.0.113 is
   `CopilotCliTooOld`. The sentence is the one of the locator, so the user sees the same words in both places.
2. *Version.* The locator already refuses a version below 1.0.113, so this check is the second half of step 1.
3. *Credentials.* A non-empty (not white space) value of `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, or `GITHUB_TOKEN`, read in
   this order through the environment reader, is enough; the GitHub CLI is then not run. Otherwise the unit looks for
   `gh.exe` in the folders of the `PATH` and then in `%ProgramFiles%\GitHub CLI`, and runs `gh auth token` with a limit
   of 5 seconds. The result counts only when the program started, did not time out, ended with exit code 0, and printed
   at least one character other than white space. Only the length of the output is examined; the text is a local value
   that is not kept, shown, or logged. No other credential is looked for.
4. *Runtime.* The starter is asked, with `CheckOnly` set, whether the runtime can be started. `Started` is `Ready` with
   the sentence "GitHub Copilot CLI {version} is ready."; any other answer is `CopilotRuntimeUnavailable`, with the
   sentence of the starter. The starter that this build ships answers `NeedsAgentKitUpdate`, so Copilot is **never**
   reported ready in this build.

The sentences (exact text; the design and the tests are the same words):

- `CopilotCliMissing`: "The GitHub Copilot CLI was not found. Install it with `winget install GitHub.Copilot` or set its
  path in Settings." followed, when the only copilot.exe found was the editor shim, by " Only the VS Code copilot shim
  was found at {path}."; a path in the settings that fails gives the sentence of the locator about that path.
- `CopilotCliTooOld`: "Copilot CLI {version} is older than 1.0.113. Run `winget upgrade GitHub.Copilot`."
- `CopilotNotSignedIn`: "Copilot is not signed in for CanvasPilot. Sign in with `gh auth login` (or set the
  COPILOT_GITHUB_TOKEN environment variable), then choose Check connection. The Copilot CLI's own /login session is not
  shared with CanvasPilot's AI library yet."
- `CopilotRuntimeUnavailable`: "Copilot support needs an AgentKit update; use Ollama meanwhile." (the sentence of the
  starter when a starter other than the gated one fails).
- `Ready` (Copilot): "GitHub Copilot CLI {version} is ready." (only a starter that can start gives it).

**Why the sign-in sentence says what it says.** The AI library starts its own Copilot runtime and signs in with a GitHub
token, from the variables above or from a signed-in GitHub CLI. The sign-in that the Copilot CLI keeps for itself (its
`/login`) is not used by that runtime, so a user who is signed in to the CLI is still not signed in for CanvasPilot.
This is one of the two requests recorded for the AI library (see the design of the AgentKit item): a way to name the CLI
to run, and a way to use the CLI's own session.

**Ollama**, in order.

1. *Settings.* An address that is not a valid `http` or `https` address, or a model name that is empty, is
   `NotConfigured`, and the server is not asked. Sentences: "The Ollama address in Settings is not a valid http or https
   address; fix it, then choose Check connection." and "Enter the name of an Ollama model in Settings, then choose Check
   connection."
2. *Server.* `ListModelsAsync` with a limit of 5 seconds. No answer is `OllamaUnreachable`: "Ollama did not answer at
   {address}; start Ollama or fix the address in Settings, then choose Check connection."
3. *Model.* The model matches a listed name ignoring case, where a name without a tag means the `latest` tag, as it does
   for `ollama pull`. No match is `OllamaModelMissing`: "Ollama does not have the model {model}; run
   `ollama pull {model}`, then choose Check connection."
4. *Context window.* `GetContextLengthAsync` (5 seconds, optional) gives the largest window of the model, read from the
   entry of the model information whose key ends in `.context_length`. The effective window is the smaller of the
   configured window and that value (the configured window alone when it is not known). Below 16,384 tokens
   (`AiProviderSettings.RecommendedMinimumContextTokens`) the status is `OllamaContextTooSmall`, which can still run a
   turn. When the setting is the smaller value: "The context window ({n} tokens) is below 16,384; drawings with images
   and tools may fail. Raise it in Settings (32,768 recommended)." When the model's limit is the smaller value (so
   raising the setting cannot help): "The model supports only {d} tokens, below 16,384; drawings with images and tools
   may fail, so choose a model with a larger context window." Numbers use thousands separators.
5. *Ready.* "Ollama is running with the model {model}; pictures work only with a vision-capable model." The hint is
   only in this sentence; the check cannot tell whether the model can see pictures.

#### Key Methods

**GetReportAsync(settings, refresh, cancellationToken)**: Runs the checks of the provider in the settings and returns
the report. `refresh` makes the locator search again instead of using its cached answer.

**CheckAsync(settings, cancellationToken)**: The `IProviderCheck` of the Settings subsystem. Always refreshes, then
returns `ProviderCheckResult(report.IsUsable, report.Message)`.

**DescribeSmallContext(configured, detected)**: Public and static so that the settings page can warn with the same
words; returns the sentence of step 4 or `null`.

**OllamaProbe.ListModelsAsync / GetContextLengthAsync**: Use the OllamaSharp client (`ListLocalModelsAsync`,
`ShowModelAsync`) against a client made for each call, with a time limit that is linked to the token of the caller.

#### Error Handling

Every expected problem is a report. In the probe, a refused connection, an error status, an answer that is not what
Ollama sends, and a time out are an unreachable server (for the list) and an unknown length (for the window); only a
cancellation by the caller is thrown, from both providers' checks. A program that cannot be started, times out, or exits
with an error is "no credentials". The shape of the `/api/show` answer (the key that ends in `.context_length`, a whole
number) is an assumption that a real server must confirm; until then it is checked only against a stand-in server, and
an answer in another shape means "not known", which is the same as a server that does not tell.

#### Security

A token is a secret. The unit looks only at whether a variable or the output of `gh auth token` is empty. It stores
nothing (its fields are its collaborators), writes no log, puts no program output into a sentence, and the sentences are
fixed text with the CLI version, a path, an address, or a model name as the only variable parts.

#### Dependencies

`CopilotCliLocator` (the CLI and its version, and the process runner, file system reader, and environment reader that
the locator and the credential check share), `AiProviderSettings` and `IProviderCheck` of the Settings subsystem, the
`AgentReadinessReport` of the contract, and the OllamaSharp library (`OllamaApiClient`) in `OllamaProbe` only. The unit
does not use AgentKit or the Copilot SDK: the Copilot runtime is behind the starter.

#### Callers

The `AgentService` (readiness before a turn and for the chat banner), added with the service, and the settings dialog
through `IProviderCheck`. The `AppShell` subsystem builds the unit.

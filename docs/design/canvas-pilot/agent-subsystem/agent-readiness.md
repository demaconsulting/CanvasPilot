### AgentReadiness

![Agent Structure](AgentSubsystemView.svg)

#### Purpose

`AgentReadiness` checks whether the AI provider that the settings choose can be used and says, in one sentence, what it
found and what to do about it. The chat shows the sentence before the first message, and the settings dialog shows it
when the user chooses Check connection. The unit implements `IProviderCheck`, which the Settings subsystem owns, so the
dialog never depends on the Agent subsystem. The unit also holds its supporting types: `IOllamaProbe` with its real
`OllamaProbe`. The starter of the Copilot runtime, which the check asks, is the `CopilotRuntimeStarter` unit; the
catalogs of the models, with which the check validates the model settings, come from the running runtime (GitHub
Copilot) or from `IOllamaModelCatalogs` (Ollama; see the `ProviderModelCatalog` unit).

#### Data Model

**AgentReadiness**: `sealed class`, implements `IProviderCheck`. The constructor takes `CopilotCliLocator`,
`ICopilotRuntimeStarter`, `IOllamaProbe`, and `IOllamaModelCatalogs`, each defaulting to the real implementation (the
starter defaults to a `CopilotRuntimeStarter`; the application passes the one that it shares with the factory of the
agents, and the one set of Ollama catalogs that it shares with them). The unit takes no process runner, environment
reader, or file system of its own, because it looks for no credentials and runs no program: the locator has its own.
Its fields are these collaborators only: it holds no text, no settings, and no logger. Constants:
`OllamaProbeTimeout` (5 seconds) and `NotSignedInMessage`, which equals `CopilotRuntimeStarter.NotSignedInMessage`. The
result is the `AgentReadinessReport` of the contract (a status and one sentence).

**ICopilotRuntimeStarter**, with `CopilotStartRequest`, `CopilotStartResult`, `CopilotStartStatus`, and
`CopilotSignInInfo`, is described in the design of the `CopilotRuntimeStarter` unit. The check passes the path and the
version that the locator found and no data folder, so the shared starter uses the folder that it was made with.

**IOllamaProbe**: `ListModelsAsync(endpoint, timeout, cancellationToken)` returns `OllamaModelList(Reachable, Models)`,
and `GetContextLengthAsync(endpoint, model, timeout, cancellationToken)` returns the largest context window of a model
in tokens or `null`. **OllamaProbe** is the real probe; its constructor can take an `HttpMessageHandler` so that a test
can stand in for the server.

#### The Checks

**GitHub Copilot**, in order; the first check that fails gives the answer, and the checks after it are not run. The
check looks for no credentials of its own and runs no `gh`: the runtime start decides the sign-in, in the order that the
AI library defines (explicit and environment tokens, then the sign-in of the Copilot CLI, then the GitHub CLI).

1. *CLI.* `CopilotCliLocator.LocateAsync(settings.CopilotCliPath, refresh)` finds the installed CLI by its rule. A CLI
   that is not found, is not an `.exe`, or cannot report its version is `CopilotCliMissing`; one older than 1.0.113 is
   `CopilotCliTooOld`. The sentence is the one of the locator, so the user sees the same words in both places.
2. *Version.* The locator already refuses a version below 1.0.113, so this check is the second half of step 1.
3. *Runtime.* The shared starter is asked to start the runtime on the CLI that the locator found. This is the real,
   authoritative check, including the sign-in: the runtime is started, the library checks the version of the CLI again,
   signs in from the first source that applies, and refuses a runtime that is not signed in. `Started` goes on to the
   model settings; `CliTooOld`, `NotSignedIn`, and `Failed` are `CopilotCliTooOld`, `CopilotNotSignedIn`, and
   `CopilotRuntimeUnavailable`, each with the sentence of the starter. The check starts no second runtime: the runtime
   that it starts is the one that the conversations then use, so a ready check means that the first message finds a
   running runtime.
4. *Model settings.* When the settings name a model, `ICopilotAgentSource.Models.SelectAsync` of the running runtime
   validates it with the reasoning effort and the context size of the settings and the requirement that the model can
   call tools (the policy of `ModelRequirements`, see the design of `ProviderModelCatalog`). Settings that name no model
   use the default model of the runtime, and there is nothing to validate (an effort without a model is not used). A
   refusal of the catalog is `ModelSettingsInvalid` with the sentence of the AI library, which names what is supported;
   a catalog that cannot be read (the runtime is not connected, or was stopped) is `CopilotRuntimeUnavailable` with its
   message; a model that cannot take pictures is the usable `ModelLacksVision`. A provider that can be used (`Ready` and
   `ModelLacksVision`) gets the sentence that `ICopilotAgentSource.SignIn` words appended (see below).

The sentences (exact text; the design and the tests are the same words):

- `CopilotCliMissing`: "The GitHub Copilot CLI was not found. Install it with `winget install GitHub.Copilot` or set its
  path in Settings." followed, when the only copilot.exe found was the editor shim, by " Only the VS Code copilot shim
  was found at {path}."; a path in the settings that fails gives the sentence of the locator about that path.
- `CopilotCliTooOld`: "Copilot CLI {version} is older than 1.0.113. Run `winget upgrade GitHub.Copilot`." (the
  locator's sentence), or the message of the AI library when the starter found the CLI too old.
- `CopilotNotSignedIn`: "Copilot is not signed in for CanvasPilot. Run `copilot`, then type `/login` to sign in (or
  sign in with `gh auth login`, or set the COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then
  choose Check connection." followed by " What was found: " and the digest of what the AI library found (the sentence of
  the starter, see its design).
- `CopilotRuntimeUnavailable`: the sentence of the starter for a start that failed for another reason (the message of
  the AI library, or "The Copilot runtime could not be started. Choose Check connection to try again.").
- `Ready` (Copilot): "GitHub Copilot CLI {version} is ready." (no model named), or "GitHub Copilot CLI {version} is
  ready with the model {id}.", or, when the settings chose a context size, "GitHub Copilot CLI {version} is ready with
  the model {id} at the {size name} size ({window} tokens)." (for example "at the Long context size (1,000,000
  tokens)"; the name and window are the catalog's, the window is left out when it is not known), each followed by the
  sign-in sentence when the runtime reports one. This sentence is the tooltip of the provider pill, which is where the
  user reads which context size the model runs with.
- `ModelSettingsInvalid`: the sentence of the AI library, for example "Model 'x' does not support tool calling, which
  this application requires." or "Model 'claude-haiku-5.5' does not support reasoning effort 'bogus'. Supported: low,
  medium, high, xhigh, max." or "Model 'x' is not available from provider 'copilot'. It may have been removed or you may
  no longer have access." (problems are joined with a space).
- `ModelLacksVision`: "The model {id} does not accept pictures, so it cannot see the drawings and marks; choose a model
  with vision for the best results." followed by the sign-in sentence when the runtime reports one. The status is
  usable.

**The sign-in sentence.** The runtime reports the kind of sign-in, the login, and the host, and
`CopilotSignInInfo.Describe` words them, for example "Signed in as octocat on github.com (Copilot CLI sign-in)." The
unit appends the sentence after a space, so Check connection and the tooltip of the provider pill (which shows the
sentence of the report) tell the user which account the copilot uses, because a machine may offer several. The unit
appends nothing when the runtime does not say how it signed in, so the sentence of a ready provider is then exactly the
one above. The sentence holds no token, because the runtime reports none. The check does not look at the CLI account
itself: the library reads that account only inside the runtime start, so the runtime start is also the only proof that
the sign-in works.

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
4. *Model settings.* The catalog of the server (`IOllamaModelCatalogs.For(address)`, which describes each model from
   `/api/show` and keeps the answer for a few minutes) validates the model with the context window and the reasoning
   effort of the settings and the requirement that the model can call tools; the name is matched to the id that the
   catalog spells (`qwen3` is `qwen3:latest`). A refusal is `ModelSettingsInvalid` with the sentence of the AI library:
   the model cannot call tools or is an embedding model, the window is larger than the model was trained for ("Model 'x'
   supports at most 8,192 tokens; a 32,768-token window is not supported."), or the effort is not offered. A catalog
   that cannot be read in 15 seconds (`ProviderModelCatalog.OllamaListTimeout`) leaves the settings unchecked, because
   the list of models just answered: the first turn finds a problem, if there is one.
5. *Context window.* `GetContextLengthAsync` (5 seconds, optional) gives the largest window of the model, read from the
   entry of the model information whose key ends in `.context_length`, or, when the server does not answer, the length
   that the catalog gave. The effective window is the smaller of the
   configured window and that value (the configured window alone when it is not known). Below 16,384 tokens
   (`AiProviderSettings.RecommendedMinimumContextTokens`) the status is `OllamaContextTooSmall`, which can still run a
   turn. When the setting is the smaller value: "The context window ({n} tokens) is below 16,384; drawings with images
   and tools may fail. Raise it in Settings (32,768 recommended)." When the model's limit is the smaller value (so
   raising the setting cannot help): "The model supports only {d} tokens, below 16,384; drawings with images and tools
   may fail, so choose a model with a larger context window." Numbers use thousands separators.
6. *Ready.* With the catalog: "Ollama is running with the model {id}." and, for a model that cannot take pictures, the
   usable `ModelLacksVision` with the sentence above; when the window of step 5 is also too small, the status is
   `OllamaContextTooSmall` and the sentence is the one of step 5 followed by the sentence about pictures. Without the
   catalog the hint stays: "Ollama is running with the model {model}; pictures work only with a vision-capable model."
   The hint is only in this sentence; without the catalog the check cannot tell whether the model can see pictures.

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
cancellation by the caller is thrown, from both providers' checks. A runtime that fails to start is a status of the
starter that the check passes on as a report. The shape of the `/api/show` answer (the key that ends in
`.context_length`, a whole number) is an assumption that a real server must confirm; until then it is checked only
against a stand-in server, and an answer in another shape means "not known", which is the same as a server that does not
tell.

#### Security

A token is a secret. The unit never sees one: it looks for no credentials, runs no program, and the sign-in sentence
that it appends holds only the kind of sign-in, the login, and the host. It stores nothing (its fields are its
collaborators), writes no log, puts no program output into a sentence, and the sentences are fixed text with the CLI
version, a path, an address, a model name, or the sign-in sentence as the only variable parts.

#### Dependencies

`CopilotCliLocator` (the CLI and its version, with the process runner, file system reader, and environment reader that
it uses itself), `ICopilotRuntimeStarter` and `CopilotSignInInfo`, `AiProviderSettings` and `IProviderCheck` of the
Settings subsystem, the
`AgentReadinessReport` of the contract, `ModelRequirements` (the policy of what the application needs of a model, of the
`ProviderModelCatalog` unit), the model types and catalogs of DemaConsulting.AgentKit.Core (`ModelSelection`,
`ModelSettingsException`, `SelectAsync`), and the OllamaSharp library (`OllamaApiClient`) in `OllamaProbe` only. The
unit does not use the Copilot SDK: the Copilot runtime is behind the starter, which the `CopilotRuntimeStarter` unit
implements, and the Ollama catalogs are behind `IOllamaModelCatalogs`.

#### Callers

The `AgentService` unit (readiness before a turn and for the chat banner) and the settings dialog
through `IProviderCheck`. The `AppShell` subsystem builds the unit.

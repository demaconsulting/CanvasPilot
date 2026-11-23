## AgentKit

This document describes the integration and usage design for the `DemaConsulting.AgentKit` OTS software item: the
Core, Agents.Copilot, Agents.ChatClient, and Agents.Ollama packages.

### Purpose

AgentKit is DEMA Consulting's library for building confined AI agents. CanvasPilot's AI copilot is built on it: Core
provides the path policy and the tool framework that keep the copilot's tools inside the project folder, and the agent
packages turn a provider into an agent that runs a tool-calling turn. It was chosen because it was written for exactly
this use, with refusals as results and not as exceptions, and because it carries the image handling that a drawing
copilot needs. It is a library from another repository of the same organization that CanvasPilot consumes through
NuGet, and it is treated as OTS because its internal design is out of scope here.

The `DemaConsulting.AgentKit.Tools` and `DemaConsulting.AgentKit.Tools.Visual` packages are deliberately not referenced
in this change. The tools package now holds only the file and text tool packs; the visual tools package holds the image
view tool and the diagram tools and pins the same CanvasNet viewer as CanvasPilot's own CanvasNet tool and viewer, so
adopting it later causes no version conflict (see *Direction: selectable tool packs*). CanvasPilot still builds its
tools itself, and a later change will compare the visual tools of the library with CanvasPilot's own help and render
tools before any is adopted.

### Features Used

- **Core**: `PathPolicy` and `PathRule` (the grants and their denied folder names), `IToolPack` and `ToolPackBuilder`
  (composition of tool families with a unique prefix each), `GuardedToolFactory` (tool names and the delivery of a
  result in the shape a provider can read, and `AsGuarded` to bound any function by `ToolLimits`), and `ToolResult`
  (text, structured, image, and refusal results); and the model catalog and settings:
  `IModelCatalog` and `ModelInfo` (what a model can do: `ModelCapabilities`, the reasoning efforts, the context window,
  and whether it can be used), `ModelSettings` (the model, the reasoning effort, and the window that the application
  chooses) with `SelectAsync`, which validates them against the catalog and gives a `ModelSelection` or a
  `ModelSettingsException` whose problems are sentences, and `ModelSettingsChatClient`, which applies a selection to the
  requests of a chat client.
- **Agents.ChatClient**: `ChatClientAgentFactory.Create`, which builds an agent on any chat client with the
  image-promoting decorator installed (and, given a selection, the decorator that applies it), so that a picture
  returned by a tool reaches a model whose tool channel cannot carry one. The Ollama provider is built this way.
- **Agents.Ollama**: `OllamaContextSizingChatClient`, which adds the Ollama option `num_ctx` to every request, and
  `OllamaModelCatalog`, which lists the models of a server and describes each from `/api/show`.
- **Agents.Copilot**: `CopilotRuntime`, `CopilotRuntimeOptions`, `CopilotRuntimeSource`, and `CopilotAgentFactory`,
  which start a Copilot runtime on the Copilot CLI that the user installed and build a Copilot agent on it (with a model
  selection, from which the model and the reasoning effort are taken), and `CopilotModelCatalog`, which lists the models
  that the signed-in account may use. The installed CLI is the default source (`CopilotRuntimeSource.InstalledCli`);
  `CopilotCli.MinimumVersion` is the oldest CLI that the library accepts. The runtime reports how it signed in
  (`SignInSource`, `GitHubLogin`, and `GitHubHost`, none of which holds a secret). The agent that it builds streams
  `AgentResponseUpdate`s of text, tool calls and results, and usage; the usage content carries the event of the Copilot
  SDK as its raw representation, from which CanvasPilot reads the model multiplier, the reasoning tokens, and the model.
  The agent opens or resumes the runtime session for each run and releases it after the run (see *The session of a
  Copilot conversation*).

### Integration Pattern

The libraries are NuGet package references of the application library and are called in process from the units of the
Agent subsystem. The seams of that subsystem keep the rest of the application and its tests independent of them, and
the OTS tests in `test/OtsSoftwareTests/AgentKitTests.cs` call the libraries the way the subsystem does, with a
scripted chat client and no language model. Refusals of the path policy are results, so the tools of the application
return them to the model; the policy judges a path by its normalized spelling and does not follow links, which is why
the application's own file tools check for links themselves.

### The Copilot runtime on the installed CLI

AgentKit (the Copilot SDK, the Microsoft Agent Framework, OllamaSharp, and
Microsoft.Extensions.AI.Abstractions are pinned by it) runs the Copilot CLI that the user installed, at an explicit
path. The `CopilotRuntimeStarter` unit builds
`CopilotRuntimeOptions(dataDirectory, gitHubToken: null, logger, CopilotRuntimeSource.InstalledCli, cliPath)` and starts
the runtime with `CopilotRuntime.StartAsync`. These are the rules of the library that the design relies on:

- `cliPath` must be an absolute path and must not be a `.cmd` or `.bat` file; the library refuses anything else with an
  `ArgumentException` when the options are built. The variable `COPILOT_CLI_PATH` is ignored.
- The library checks the version of the CLI by running it with `--no-auto-update --version` and refuses a CLI older than
  `CopilotCli.MinimumVersion` (1.0.113). The locator of CanvasPilot uses the same probe and minimum, and an OTS test
  asserts that the minimum of the library is never above the one of the locator.
- The runtime signs in from the first source that applies, in this order: a token or token provider that the host passes
  (unused: CanvasPilot passes `null` and keeps no token), the variables `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, and
  `GITHUB_TOKEN`, the sign-in that the Copilot CLI keeps for itself (`copilot`, then `/login`; allowed by default and
  read by the library through a short-lived helper runtime on the home of the CLI, which makes the first start one to
  three seconds longer), a sign-in stored in the data folder, and last a signed-in GitHub CLI (`gh auth login`, with
  `gh` on the `PATH`). The library reads only the host and the login of the CLI account from its configuration file, so
  the host follows the account, including a GitHub Enterprise Cloud host, with no setting. A token variable that holds a
  token that the host does not accept is refused and the later sources are not tried. The runtime exposes
  `SignInSource`, `GitHubLogin`, and `GitHubHost` for the user to see, and they carry no secret. The options also have a
  GitHub host and a switch for the sign-in of the CLI; CanvasPilot uses neither (see *Direction: future options*). The
  OTS tests `AgentKit_CopilotRuntimeOptions_CanvasPilotArguments_AllowTheCopilotCliSignInAndLeaveTokenAndHostUnset` and
  `AgentKit_CopilotRuntimeOptions_HostAndTokenArguments_AreNormalizedAndCombinationsRefused` pin these defaults.
- The data directory must be empty, missing, or a directory that an earlier version of the library made; the runtime
  keeps its home and its sessions there, so only one runtime may use it at a time.
- A runtime that cannot be used fails with an `InvalidOperationException` whose text explains the cause and the remedy
  (a CLI that is too old, a runtime that is not signed in, and so on). For a runtime that is not signed in the text has
  several lines: a line that the runtime is not signed in, a line that the library tried these sign-in sources in order,
  one indented numbered line for each source with what it found (with every token redacted), and the advice of the
  library on how to sign in. The library has no typed causes, so the starter recognizes the words of the message and
  digests the numbered lines into one sentence (see the design of the `CopilotRuntimeStarter` unit); this text is a
  behavior of the library that an upgrade must be checked against.

CanvasPilot must not ship or download the GitHub Copilot runtime. The Copilot SDK package would download and copy a
bundled runtime, about 180 MB, into every project that references it, so `CopilotSkipCliDownload` is set to `true` in
`Directory.Build.props`, which makes every project of the solution skip the download. The `Bundled` source is never
chosen, so the installed CLI is the only runtime that is started, and no Copilot runtime file is in the build output,
the published output, or the MSI. The OTS test `AgentKit_BuildOutput_HasNoBundledCopilotRuntime` asserts the build
output, the FileAssert test `Copilot_RuntimeNotPublished` asserts the published output, and the MSI file table is
checked in the same build.

### The model catalogs and the model settings

The `ProviderModelCatalog` unit lists models from the two catalogs, and the readiness check and the factory validate
with them. These are the rules of the library that the design relies on (read from the source of the library and
verified on 2026-11-22 against a Copilot CLI 1.0.114 that was signed in through the GitHub CLI, which listed 23 models):

- A catalog lists `ModelInfo` for the signed-in account. `ModelQuery.IncludeUnusable` also lists the models that the
  provider disables for the account, each with the reason; the plain list leaves them out. `RefreshAsync` makes the
  catalog read again; otherwise it keeps the answer for five minutes.
- `CopilotModelCatalog` treats tool calling as supported unless the service says otherwise, vision only when the service
  says so, and leaves out the pseudo-model `auto`. Its efforts are the ids of the service (none, minimal, low, medium,
  high, xhigh, max), no default effort is invented (the live list had none), and the context window is the maximum of
  the model. A runtime that is not signed in makes the catalog fail with an `InvalidOperationException` that names the
  working routes.
- `OllamaModelCatalog` takes the capabilities from `/api/show` (an older server is inferred and noted), lists an
  embedding model as unusable, offers `none`, `low`, `medium`, and `high` for a model that thinks (not `none` for a
  model that always thinks), and gives the context length that the model was trained for as its maximum window.
  `ModelSettings.Validate` refuses a window above that maximum before any request. A name without a tag is matched to
  the `latest` tag, and the selection carries the catalog's own id.
- `ModelSettings.Validate(model, required)` gives a list of sentences: a capability that the application requires and
  the model lacks, a model that cannot be used, an effort that the model does not offer (with the ones that it offers),
  and a window above the maximum. `SelectAsync` throws a `ModelSettingsException` (a kind of
  `InvalidOperationException`, to be caught first) with those problems. A plain Copilot agent refuses a selection that
  carries a window, so the application states a window for Ollama only.
- A selection reaches the provider through the agent factory: `CopilotAgentFactory.Create(..., selection:)` takes the
  model, the effort, and the context size (tier), and `ChatClientAgentFactory.Create(..., selection:)` installs
  `ModelSettingsChatClient`, which sets the model, the standard reasoning effort, and nothing that the caller already
  set. An effort with no provider-neutral equivalent (`xhigh`, `max`) cannot be applied through a chat client, and the
  library refuses it when the client is built; Ollama offers only `none`, `low`, `medium`, and `high`, which have one.

- **Context sizes (tiers).** `ModelInfo.ContextTiers` lists the sizes that a model offers
  (`ModelContextTier(Id, DisplayName, WindowTokens)`), `DefaultContextTier` the one that applies when none is chosen,
  and `ModelAdjustments.ContextTier` says that one can be chosen; `ModelSettings.ContextTier` states the choice,
  validated like the effort (a size that the model does not offer is a sentence), and the Copilot agent factory sends it
  to the session as `config.Inference.ContextTier` (the SDK's `Model.SupportedContextTiers` and
  `ModelSwitchToRequest.ContextTier` are the same idea). `CopilotModelCatalog` derives the sizes from the billing of the
  model (`default`, named "Standard", and `long_context`, named "Long context", when the model has token prices for a
  long-context tier) and from the tiers that the model declares. Verified on 2026-11-23 against the Copilot CLI 1.0.114
  of the machine: `claude-opus-5.5` lists `default` at a window of 328,000 tokens and `long_context` at 1,000,000 (the
  window of a size is its prompt budget plus the output limit of the model, capped at the maximum of the model), and the
  events file of a session that was created with the larger size records `session.start` with
  `contextTier: long_context`, which is the runtime's own confirmation. The library publishes no price for a size, only
  that the larger one is billed separately. A usage event of the provider does not name the size, so CanvasPilot shows
  the size that it asked for.
- A plain Copilot agent refuses a selection that carries a window (`ContextWindowTokens`), so the application states no
  window for Copilot; the context size is the way to choose a larger window there.

The OTS tests `AgentKit_ModelSettings_Validate_RefusesWhatTheModelCannotDoAndBindsWhatFits`,
`AgentKit_ModelSettingsChatClient_GetResponse_AddsTheModelAndEffortWithoutChangingCallersOptions`, and
`AgentKit_OllamaModelCatalog_ListAsync_DescribesCapabilitiesWindowAndEfforts` call the library as the application does
and pin these rules.

### The session of a Copilot conversation

The Copilot agent of AgentKit keeps the runtime session of a conversation per run, not per application: each run of the
agent opens the session (the first run) or resumes it (every later run), and when the run ends the library cancels the
run and releases the session handle. The session itself, with its recorded events, stays at the runtime until the host
deletes it (CanvasPilot does when it drops the conversation). These are the rules of the library that the `AgentService`
unit relies on:

- A session gets its identifier when the first run creates it; `AgentSession` of a conversation that has not run has
  none. The identifier and the folder of the session below the data directory of the runtime are what the log of
  CanvasPilot names.
- The runtime therefore closes the session after every run, completed or stopped, and resumes it before the next one.
  Resuming is quick (about a tenth of a second) and replays nothing, so the application keeps one conversation, one
  agent and one session object, for the open project and pays no cost for the per-run release.
- Canceling the token of a run ends the run on the host side; the library has no explicit abort of the runtime turn on
  that path (see *Known Limits and Upstream Requests*).

### Known Limits and Upstream Requests

The design of the `Agent` subsystem depends on what AgentKit and the Copilot runtime really do, which is less than a
live display of a long turn would need. The facts below were established with a probe program that starts the runtime
the way CanvasPilot does and records every raw session event with a time stamp, and with the log and the events file of
the runtime. They explain why CanvasPilot shows only the elapsed time and the phase of a long turn for some models, and
they are the requests that CanvasPilot makes upstream. Nothing here is worked around by reflection into the library:
the library is used through its public API only.

- **U1 — BLOCKS only the model-originated part of live progress.** In `CopilotAgent.UpdateMapper.ToUpdate`, stop
  dropping progress events: map `AssistantToolCallDeltaEvent` and `AssistantStreamingDeltaEvent` to an update whose
  content is `new AIContent { RawRepresentation = sessionEvent }` (the same pattern already used for the final
  `AssistantMessageEvent`), `AssistantReasoningDeltaEvent` to `TextReasoningContent(delta) { RawRepresentation =
  sessionEvent }`, and `AssistantIntentEvent` / `ToolExecutionProgressEvent` likewise raw; keep root-agent-only
  filtering; document the contract in `copilot-agent.md`; do not coalesce (host throttles). Release as the next AgentKit
  beta release (or later). *CanvasPilot cannot work around this: `Events.OnEvent` is overwritten, `Client` is internal,
  deltas are ephemeral (not in `events.jsonl`); reflection into internals is rejected.* The release in use drops these
  events (`default: return null`), so only whole messages, tool calls and results, and usage reach the application. The
  `AgentEventMapper` unit already maps the events from the raw representation, so no change of CanvasPilot is needed
  when the library forwards them.
- **U2 — recommended (not blocking).** In `CopilotAgent.FinishAsync`, when the run was canceled and the turn has not
  ended, call `CopilotSession.AbortAsync()` and wait (bounded) for the session to go idle **before**
  `channel.DisposeAsync()`; expose the abort through `ICopilotTurnChannel`. Evidence: token cancel alone left the
  runtime turn running (follow-up waited 37 s); dispose happened to end it in the app.
- **U3 — investigation request (the Copilot CLI, the Copilot SDK, and the AgentKit release in use).** Repro: start
  `CopilotRuntime`, create a session through `BuildSessionConfig`, send any prompt with any model, account on
  `*.ghe.com`: runtime logs `turn model is in neither the picker nor the unfiltered catalog … falling back to default
  capabilities and the chat-completions wire {latest_model_list_fetch_succeeded:false}` while `Rpc.Models.ListAsync`
  succeeds and passing `gitHubTokenProvider`+`gitHubHost` changes nothing. For Claude models the response is then
  buffered (7 deltas, ≈ 45 s silence, ≈ 4500 deltas in 0.4 s) and the runtime logs `Session event stream lagged …
  skipped:N`. Ask: (a) find why the session-level model-list fetch fails (GHE host? token routing?) and whether
  github.com accounts are affected; (b) let AgentKit pass the catalog's capabilities to the session if (a) cannot be
  fixed: the SDK 2.0.1 has the member for it (`SessionOptionsBase.Inference.Capabilities`, a
  `ModelCapabilitiesOverride` with `Supports{ToolCalls, Vision, ReasoningEffort, AdaptiveThinking}` and
  `Limits{MaxContextWindowTokens, MaxOutputTokens, MaxPromptTokens, Vision}`, and the same type as
  `ModelSwitchToRequest.ModelCapabilities`), but AgentKit 0.1.0-beta.5 sets only `Inference.ReasoningEffort` and
  `Inference.ContextTier` in `CopilotAgentFactory.CreateSessionConfig` (internal), has no option or callback that lets a
  host add to the session options, and does not set `Capabilities`; so the ask is that `CreateSessionConfig` and
  `BuildResumeSessionConfig` copy the capabilities and limits of the `ModelInfo` of a `ModelSelection` into
  `Inference.Capabilities` (or that `CopilotAgentRequest` accepts an `Action<SessionOptionsBase>` hook). Whether this
  also stops the fall-back to the chat-completions wire is not known (UNVERIFIED: the runtime message says the model is
  in neither the picker nor the unfiltered catalog, which the override may not change), so CanvasPilot does not wire an
  override that cannot be reached and cannot be shown to help; (c) confirm that Claude streams incrementally once the
  model is in the picker. The probe is in `%TEMP%\ck-probe\Program.cs` (not committed anywhere).

**Not an issue.** Three things in the logs of the runtime look like faults and are not; the application's own log names
the provider session so that they can be told apart from real problems:

- The per-run `session.shutdown`, `Closing session`, and `Resumed session` lines after and before every turn are
  AgentKit's documented lifecycle (open or resume per run, release after it), as described in *The session of a Copilot
  conversation*.
- `received response for unknown request id` after every turn, also after a short one, is a benign race of the SDK.
- The `events.jsonl` of a session is gone after the application ends, because CanvasPilot deletes the session when it
  drops the conversation (the "forget the conversation" rule); it is kept while the conversation lives, and the log
  gives its path for a failed or canceled turn.

### Direction: selectable tool packs

A later increment may let the user choose further AgentKit tool packs in the settings (a list of packs, each on or off,
in the AI Provider page). Neither `DemaConsulting.AgentKit.Tools` (the file and text packs) nor
`DemaConsulting.AgentKit.Tools.Visual` (the image view tool and the diagram packs) is referenced in this change, and the
visual package pins the same CanvasNet viewer as CanvasPilot, so there is no version obstacle. Before any of it is
adopted, a later change will compare the visual tools of the library with the help and render tools of CanvasPilot
(`canvasnet_help` and `render_image`). Until then the copilot has only the fixed, locked-down tools of CanvasPilot, and
the application's registry already offers an empty catalog of optional packs that the settings could fill.

### Direction: future options

The options of the library have a GitHub host (for an account on a GitHub Enterprise Cloud host that is not the host of
the Copilot CLI account) and a switch that turns the sign-in of the Copilot CLI off. They are possible future settings
of CanvasPilot; this change uses neither, because the host follows the account that signed in and the sign-in of the CLI
is wanted.

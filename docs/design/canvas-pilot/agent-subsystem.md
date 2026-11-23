## Agent

![Agent Structure](AgentSubsystemView.svg)

### Overview

The `Agent` subsystem is the AI copilot behind the chat. Its boundary starts with a message that the user sent, in the
form of the `MessagePayload` of the `Payload` subsystem, and with the AI provider choices of the `Settings` subsystem;
it ends with a stream of provider-independent events that the `Chat` subsystem shows, and with a report on whether the
chosen provider can be used. It has no user interface. Its parts are the readiness checks for the provider, the tools
that the copilot may call and the instructions that it is given, the agent of the chosen provider, and the service that
keeps one conversation for the open project and runs one turn at a time.

The subsystem is built contract first. It contains twelve units. `AgentContract` is `IAgentService`, the request and
history records, the event records, and the readiness report, all free of any AI library. `CopilotCliLocator` finds the
GitHub Copilot CLI that the user installed, tells it from the launchers that editors put on the `PATH`, and checks its
version; it comes with the seams that make it testable (a process runner, a file system, and an environment reader).
`AgentReadiness` checks whether the chosen provider can be used and implements the `IProviderCheck` of the `Settings`
subsystem; it comes with the probe of the Ollama server, for Copilot it asks the shared runtime to start (the runtime
start decides the sign-in) and appends the account that the runtime signed in as to the sentence of a provider that can
be used, and for both providers it validates the model settings against the catalog of the provider.
`CopilotRuntimeStarter` starts the GitHub Copilot runtime of the AI library on the CLI that the user installed, shares
that one runtime among the readiness check, the list of models, and every conversation, and words every way that the
start can fail; it holds the supporting types of the starter and of the source of Copilot agents (which also gives the
catalog of the models of the runtime). `ProviderModelCatalog` lists the models of the chosen provider with what each can
do, as the `IProviderModelSource` of the `Settings` subsystem, and holds the policy of what the application needs of a
model. copilot may use for one project: it confines them to the project folder with a path policy, offers the drawing
knowledge and the style tools as functions of the AI library, adds the packs that the application supplies for the
project and the optional packs that the user chose (the catalog is empty in this version), and refuses a set that a
provider could not be given. `ProjectTools` is the copilot's hands on the project: the tools that list, read, and write
the files of the project and the tool that draws a picture, all confined to the project folder and refusing links
themselves; a write replaces a file directly (the user decided that the application keeps no backup and asks nothing, so
the change is made visible instead). `AgentInstructions` writes the instructions that the copilot is given: a role
prompt, the index of the drawing knowledge, the facts of the project, and the style of the project, in a full form or a
compact form depending on the context window of the model, with sizes that its tests measure. `PayloadMessageMapper`
turns the message that the user sent (the `MessagePayload`) into the user message that a model is given: the words, a
compact description of the attachments, pictures, and marks, and the pictures as PNG images, annotated pictures first
and then the close-ups of the marks, at most ten. `AgentEventMapper` turns the stream of updates that an agent produces
for a turn into the events of the contract, the same for both providers, and turns the exception of a failed turn into a
failure with a sentence that says what to do. It also reports the progress that a provider streams while the model works
(reasoning, the size of the arguments of a tool call that the model is writing, the size of a response) as events that
carry counts and a file name and never content, spaced by a clock; maps the usage of each model call, with the model
multiplier that GitHub Copilot reports, through one narrow adapter (`CopilotUsageReader`) that isolates an experimental
member of the Copilot SDK; and logs the start and the end of each tool call. `AgentProviderFactory` makes the agent of a
conversation for the chosen provider: for Ollama an agent on an Ollama client that is told the context window on every
request, and for GitHub Copilot an agent on the shared runtime that the starter of the runtime gives, with only the
supplied tools and the default-deny permission handler of the AI library; it also tells the log what the provider calls
the session of a conversation and where it recorded it. `AgentService` implements `IAgentService`: it makes nothing
until the first message is sent, then builds the tools, the instructions, the agent, and one provider session for the
open project, runs one turn at a time on them, rebuilds them when the project or a setting that decides the agent
changes, deletes what the provider kept when a conversation ends, gives the Copilot runtime a data folder that is never
inside a project, and writes metadata only to the log. It reads the provider on a thread of the pool into a queue that
merges what says the same thing again (`AgentEventCoalescer`, an internal part of the unit), makes a Stop safe at any
point of a turn, and numbers each turn so that its log lines can be read together.

The subsystem depends on `Settings` (the provider choices), `Project` (the open project folder), `Payload` (the
message), `Knowledge`, `Style`, and `Preview` (the content and the rendering that its tools give the copilot),
`Logging`, and the third-party AI libraries. Nothing depends on it except `Chat`, through `IAgentService`,
and `AppShell`, which builds it. `Settings` does not depend on it: `Settings` owns the `IProviderCheck` interface
through which the settings dialog can ask whether a provider works, and the `Agent` subsystem implements
that interface, as the `Composer` subsystem owns `IMessageBuilder` for the `Payload` subsystem.

### Interfaces

**IAgentService**: The AI copilot as the chat sees it.

- *Type*: In-process .NET interface; disposable. It is a part of the `AgentContract` unit.
- *Role*: Provider (the chat and the application shell are consumers).
- *Contract*: `GetReadinessAsync` returns an `AgentReadinessReport` for the settings in effect. `RunTurnAsync` takes an
  `AgentTurnRequest` and a cancellation token and returns the events of the turn in order as an
  `IAsyncEnumerable<AgentEvent>`. `ResetConversation` forgets the conversation so that the next turn starts a new
  provider session. The event `FileWritten` tells the shell, on the thread of the tool, which file the copilot created
  or replaced. Canceling the token of a turn is the Stop: it ends the stream soon, at any point of the turn, and leaves
  the conversation usable for the next message.
- *Constraints*: Only one turn runs at a time; a request made while a turn runs gets a single `Failed` event of kind
  `Busy`. A turn ends with exactly one `Completed` or `Failed` event, or simply ends when the caller cancels; no event
  follows either, so a caller that stops reading at the `Completed` event has read the whole turn. The events that
  carry facts arrive exactly once and in order; progress events may be merged while the caller lags. A provider that
  cannot be used is reported, not thrown. A project that contains a data folder of the application is refused with a
  `Failed` event of kind `DataInsideProject`.

**Turn records**: What one turn is asked and what it answers.

- *Type*: Immutable .NET records (`AgentTurnRequest`, `AgentHistoryTurn`, the `AgentEvent` family, and
  `AgentReadinessReport`), described in the design of the `AgentContract` unit.
- *Role*: Provider (data).
- *Contract*: A request carries the message and, after a restart, the words of the earlier turns; a turn answers with
  events that end with `Completed` or `Failed`; the readiness report carries a status and one sentence. The events are
  the answer (`TextDelta`), the tool calls (`ToolStarted`, `ToolFinished`), the files (`FileWritten`), the usage of each
  model call (`Usage`, with the tokens, the reasoning tokens, the model multiplier, and the model), the progress while
  the model works (`Thinking`, `ToolProgress`, `ModelWorking`), and the end.
- *Constraints*: None of the records names a type of an AI library. The progress events carry counts of bytes and the
  name of a file, never the content of an argument or of a response.

**CopilotCliLocator**: The installed GitHub Copilot CLI, as the readiness checks need it.

- *Type*: In-process .NET class with the result record `CopilotCliInfo`, documented in the design of the
  `CopilotCliLocator` unit.
- *Role*: Provider (the readiness checks and the Copilot runtime start are consumers).
- *Contract*: `LocateAsync` takes the path from the settings (empty for automatic detection) and returns the status,
  path, version, one sentence for the user, and the candidates that were rejected with the reason for each.
- *Constraints*: The CLI is never included or downloaded; a path from the settings is the only candidate and never falls
  back to automatic detection; a shim of an editor is never used for automatic detection; every problem is a result and
  only cancellation is thrown.

**AgentReadiness**: The check of whether the chosen AI provider can be used.

- *Type*: In-process .NET class that implements `IProviderCheck`, with `IOllamaProbe` (and the real probe), documented
  in the design of the `AgentReadiness` unit.
- *Role*: Provider (the service, the chat banner, and the connection check of the settings dialog are consumers).
- *Contract*: `GetReportAsync(settings, refresh, cancellationToken)` returns an `AgentReadinessReport`; `CheckAsync` is
  the settings dialog's connection check, which always searches again. For Copilot the checks run in order (CLI found,
  version, the shared runtime, the model settings) and the first failure gives the sentence; the runtime start decides
  the sign-in, so there is no check of credentials of its own and no run of the GitHub CLI. A provider that can be used
  also says how the runtime signed in (the login, the host, and the kind of sign-in). For Ollama they are the settings,
  the server, the model, the model settings, and the window.
- *Constraints*: Copilot is reported ready only when the shared runtime was started on the installed CLI and signed in
  (the Copilot runtime is neither bundled nor downloaded) and the model settings are valid for it; a model that cannot
  call tools is not usable and one that cannot take pictures is a usable warning; a token is never stored, shown, or
  logged (only the kind of sign-in, the login, and the host are shown); every problem is a report and only cancellation
  is thrown.

**CopilotRuntimeStarter**: The one Copilot runtime, on the CLI that the user installed.

- *Type*: In-process .NET class that implements `ICopilotRuntimeStarter` and `IAsyncDisposable`, with
  `CopilotStartRequest`, `CopilotStartResult`, `CopilotStartStatus`, `CopilotSignInInfo`, `ICopilotAgentSource`,
  `CopilotAgentRequest`, and `CopilotRuntimeAgentSource`, documented in the design of the `CopilotRuntimeStarter` unit.
- *Role*: Provider (the readiness check, the model list, and the agent factory are consumers); it consumes the runtime
  of the AI library.
- *Contract*: `StartAsync(request, cancellationToken)` returns a `CopilotStartResult` with the status `Started`,
  `CliTooOld`, `NotSignedIn`, or `Failed`, one sentence, and, when started, the running runtime as an
  `ICopilotAgentSource` (which makes agents, gives the `IModelCatalog` of the runtime, and says how the runtime signed
  in); every caller gets the same running runtime for the same CLI and data folder. Disposing the
  starter stops the runtime once. A runtime that is not signed in gives the sentence that says to run `copilot` and type
  `/login` (the GitHub CLI and the token variables are the alternatives) and a one-line digest of what the AI library
  found at each place it looked for a sign-in.
- *Constraints*: The CLI is the installed one at the path that the locator found and nothing is bundled or downloaded;
  only one runtime uses the data folder, which is below the local data of the user and never in a project; no token is
  read, stored, logged, or passed, and no host and no switch for the sign-in of the CLI is passed; a failed start is a
  result and is not remembered; a cancelled start is thrown.

**ProviderModelCatalog**: The models that the chosen provider offers.

- *Type*: In-process .NET class that implements `IProviderModelSource` of the `Settings` subsystem, with
  `ModelRequirements` and `IOllamaModelCatalogs` (and the real `OllamaModelCatalogs`), documented in the design of the
  `ProviderModelCatalog` unit.
- *Role*: Provider (the settings page is the consumer); it consumes the shared runtime, the locator, and the Ollama
  server.
- *Contract*: `ListModelsAsync(settings, refresh, cancellationToken)` returns the models of the provider in the settings
  (every one, usable or not, the usable ones first) with their capabilities, or no models and one sentence that says
  why.
- *Constraints*: No second Copilot runtime is started for the list; a provider that cannot be asked is a result and not
  an exception; a wait on an Ollama server ends after 15 seconds; only cancellation is thrown.

**AgentToolRegistry**: The tools that the copilot may use for one project.

- *Type*: In-process .NET class, with `DescriptorToolPack` (an `IToolPack` of the AI library), `AgentToolContext`, and
  `OptionalPackEntry`, documented in the design of the `AgentToolRegistry` unit.
- *Role*: Provider (the agent service is the consumer); it consumes the plain tool descriptions
  (`KnowledgeTool`) of the `Knowledge` and `Style` subsystems.
- *Contract*: `CreateTools(AgentToolContext, optionalPackIds)` returns the `AIFunction`s of the project in a fixed order
  (`canvasnet_help`, `style_get`, `style_check`, the project packs, the chosen optional packs); the static
  `CreatePolicy(projectFolder, limits)` makes the path policy that every pack is given.
- *Constraints*: Every tool is confined to the project folder, with the data folder (`.canvaspilot`) and the project
  files (`*.canvaspilot`) denied; the path policy of the AI library does not follow links, so tools that touch files
  must refuse links themselves; a call never throws except for the cancellation of the turn; a set with no tool, a
  repeated name or prefix, or the name of a built-in tool of the Copilot CLI is refused when it is built.

**ProjectTools**: The tools with which the copilot works on the files of the project.

- *Type*: In-process .NET class with two packs of the AI library (`project` and `render`), the event
  `FileWrittenEventArgs`, and supporting types, documented in the design of the `ProjectTools` unit.
- *Role*: Provider (the tool registry takes it as its supplier of project packs, and the application shell handles its
  event); it consumes the render service of the `Preview` subsystem and the active style of the `Style` subsystem.
- *Contract*: `CreatePacks(AgentToolContext)` returns the packs that offer `project_list`, `project_read`,
  `project_write`, and `render_image`; the event `FileWritten` gives the project-relative path of a file that was
  created or replaced and whether it is new.
- *Constraints*: Every path goes through the path policy of the project and a guard that refuses links, stream names,
  device names, short names, the data folder, and the project file; only text image sources and `.csv`, `.json`, `.txt`
  files of at most 256 KiB are written, never below the `styles` folder of the project; a write replaces a file directly
  with no copy and no question and says `created` or `replaced`; a picture is drawn in memory, scaled to at most 1,280
  pixels, and never saved; the log has metadata only; a call never throws except for the cancellation of the turn.

**AgentInstructions**: The instructions that the copilot is given for one project.

- *Type*: In-process .NET class with `AgentProjectFacts` and `InstructionTier`, and the role prompt as an embedded
  Markdown resource, documented in the design of the `AgentInstructions` unit.
- *Role*: Provider (the agent service is the consumer); it consumes the index of the drawing knowledge
  of the `Knowledge` subsystem and the style block of the `Style` subsystem.
- *Contract*: `ChooseTier(provider, contextWindowTokens)` gives the full tier for GitHub Copilot and for a window of at
  least 16,384 tokens, otherwise the compact tier; `Build(facts, tier)` writes the role prompt, the index, the facts of
  the project, and the style, in that order.
- *Constraints*: The full text is at most 20,000 characters and the compact text at most 11,000 for every kit that comes
  with the program, and the text of all tools is at most 7,000, each measured by a test; every value of the project is
  written on one line; the text holds no setting, token, message, or file content.

**PayloadMessageMapper**: The message of the user as the model is given it.

- *Type*: In-process .NET static class, documented in the design of the `PayloadMessageMapper` unit.
- *Role*: Provider (the agent service is the consumer); it consumes the `MessagePayload` of the
  `Payload` subsystem.
- *Contract*: `Map(payload, projectFolder)` returns one `ChatMessage` of the role user: the text of the user, followed
  by a block of context with the attachments, the pictures, and the marks (with the parts of the source under each),
  then the annotated pictures and the close-ups of the marks as `image/png` content, at most ten close-ups, with the
  list of the images in the text.
- *Constraints*: Pure and deterministic, the same in every culture; only paths relative to the project folder are
  written, and every value that the user supplies is written on one line.

**AgentEventMapper**: What happens in a turn, as the events of the contract.

- *Type*: In-process .NET class, one instance for each turn, documented in the design of the `AgentEventMapper` unit.
- *Role*: Provider (the agent service is the consumer); it consumes the `AgentResponseUpdate` stream
  of the Microsoft Agent Framework and the content types of Microsoft.Extensions.AI.
- *Contract*: `MapAsync(updates)` returns the events of the turn and ends them with `Completed`, with `Failed`, or, when
  the token of the turn was canceled, with neither; `Map(update)` maps one update; `MapFailure(exception)` returns the
  `Failed` event for an exception, or `null` for a cancellation. The constructor also takes the clock that spaces the
  progress events and the number of the turn that its log lines carry.
- *Constraints*: The same for GitHub Copilot and Ollama; a call is tied to its result by id and a summary is at most 200
  characters, a long text argument being shown by its length; every failure has a kind and a sentence that names the
  remedy; a progress event is made at most once for every 250 milliseconds of the clock, or at once when the phase
  changes, and carries only counts and a path; the model multiplier is read in one adapter only; the log gets metadata
  only.

**AgentProviderFactory**: The agent of a conversation for the chosen provider.

- *Type*: In-process .NET class behind the interface `IAgentFactory` (the test seam), with `AgentCreationRequest`,
  `AgentCreationResult`, and `IAgentFactory`, documented in the design of the `AgentProviderFactory` unit.
- *Role*: Provider (the agent service is the consumer); it consumes the `AiProviderSettings` of the
  `Settings` subsystem, the tools of the registry, and the instructions.
- *Contract*: `CreateAsync(request)` returns an `AgentCreationResult`, which holds an `AIAgent` of the Microsoft Agent
  Framework or the readiness status and sentence that say why none could be made; disposing the result releases what the
  agent owns, and disposing a factory that owns its starter stops the Copilot runtime. `DescribeSession(session)` gives
  an `AgentSessionInfo`: the identifier of the provider session and the file where the provider records its events (for
  Copilot, the `events.jsonl` of the session below the data folder of the runtime), or nothing for a provider with no
  session of its own and for a session that has not run yet.
- *Constraints*: Ollama gets the address, the model, and the context window of the settings on every request; GitHub
  Copilot is reached only through the shared runtime of the starter, and gets exactly the supplied tools and the project
  folder with the default-deny permission handler; settings that cannot be used are a result, not an exception.

**AgentService**: The conversation of the open project, which implements `IAgentService`.

- *Type*: In-process .NET class that implements `IAgentService`, with the digest of the restored history and the
  adapter that lets the Copilot runtime log, documented in the design of the `AgentService` unit.
- *Role*: Provider (the chat and the application shell are consumers); it consumes the project, the settings, the tool
  registry, the instructions, the message and event mappers, the readiness checks, and the factory.
- *Contract*: The constructor takes the services above, optionally the project tools (whose `FileWritten` event it
  passes on), the active style, the logger, and the local data root. `RunTurnAsync` runs a turn on the conversation of
  the open project, made now if it does not exist; `ResetConversation` drops it; `Dispose` ends the turn, releases the
  conversation, and disposes the factory.
- *Constraints*: Nothing is made before the first turn; one turn at a time; the provider is read off the thread of the
  caller and a Stop ends a turn within five seconds even when the provider ignores it; the Copilot data folder is below
  the local data root and never inside a project; the restored history is one block of at most 6,000 characters, once,
  in memory only; the log has metadata only.

**IProviderCheck**: The connection check that the settings dialog can ask for.

- *Type*: In-process .NET interface owned by the `Settings` subsystem, with the `ProviderCheckResult(IsUsable, Message)`
  record; both are documented in the design of the `AiProviderSettingsPage` unit.
- *Role*: Consumer (the `AgentReadiness` unit of the `Agent` subsystem implements it).
- *Contract*: `CheckAsync(AiProviderSettings, CancellationToken)` checks settings that may not have been saved yet and
  returns a result; a provider that cannot be used is a result, not an exception.
- *Constraints*: The interface names no type of the `Agent` subsystem, so `Settings` does not depend on it.

**IProviderModelSource**: The list of models that the settings page can ask for.

- *Type*: In-process .NET interface owned by the `Settings` subsystem, with `ProviderModelList`, `ProviderModelInfo`,
  and `ProviderEffortChoice`; all are documented in the design of the `AiProviderSettingsPage` unit.
- *Role*: Consumer (the `ProviderModelCatalog` unit of the `Agent` subsystem implements it).
- *Contract*: `ListModelsAsync(AiProviderSettings, refresh, CancellationToken)` lists the models of the provider that
  settings, which may not have been saved yet, describe; a provider that cannot be asked is a list with no models and a
  sentence, not an exception.
- *Constraints*: The interface names no type of the `Agent` subsystem or of the AI library, so `Settings` depends on
  neither.

### Design

The contract is deliberately made of plain records and one interface. The chat needs to run a turn, stop it, show its
events as they arrive, and explain a failure; none of that needs to know whether GitHub Copilot or Ollama answers, so no
contract type names a type of an AI library; this design rule is kept by review and by the dependency view of the model,
not by a test. Failure is data, not control flow: a provider that is missing, not signed in, or out of reach is a
readiness report, and a turn that fails is a `Failed` event with a kind that the chat can map to advice. Cancellation is
not a failure: the consumer asked for it, so the stream just ends and the chat marks the reply as stopped.

The subsystem tests act as the chat will: a hand-written scripted implementation of `IAgentService` stands in for an AI
provider, and the tests run a turn through the interface and read the readiness report, so that the contract is proven
usable before any provider is written against it. The Copilot CLI is found by a rule that is part of the design of the
`CopilotCliLocator` unit; the subsystem test reproduces the layout of a real machine that has both the CLI and the VS
Code shim, and checks that the CLI is found, that a machine with only the shim is told so, and that a path chosen in the
settings is used. The readiness checks are tested through `IProviderCheck`, as the settings dialog uses them: GitHub
Copilot is ready when a stand-in starter starts (with no check of credentials of the machine), names the account that
the stand-in runtime reports in its sentence, is not usable with the sentence of the starter when the starter says that
it is not signed in, is not usable with the sentence of the AI library when the catalog of the stand-in runtime does not
offer the model, cannot call tools, or does not offer the reasoning effort, and is a usable warning for a model that
cannot take pictures; and Ollama with a small window is usable with a warning. The models of the provider are listed
from stand-in catalogs and, for Ollama, through the real catalog of the AI library over a stand-in server. The tools are
tested the same way: the registry is built over the real drawing knowledge and a pack that keeps the policy it receives,
and the test shows that only the application's own tools are offered and that the policy permits a picture file of the
project and refuses the data folder, a project file, and every place outside the project. The project tools are tested
on a real project folder: a drawing is written, listed, read, replaced, and drawn by the real render service, and the
test shows that nothing but the drawing is added to the project and that paths that leave the project or reach the
project file are refused. The instructions are tested with the real cards and the real kits: their sizes are measured
for the full and the compact tier and for the tools, and a test builds the instructions and the tools of one project
together and shows that the tools that the role prompt names are exactly the tools that are offered. The mapper of the
message is tested with golden text for each rule, and with a real payload that the real builder made from a real picture
and real marks, which shows the text and the images that a model would receive. The mapper of the updates is tested with
scripted streams for every kind of content, every cause of failure, and the rules for a canceled turn, and with a real
agent, on a scripted model, that writes a drawing, looks at it, and tries to write outside the project with the real
project tools, which shows the events that the chat will get. The factory is tested against a stand-in Ollama server
with the real tools of a project, which shows a conversation from the settings to the drawing in the project (the model,
the window, and the thinking level reach the wire), and for Copilot with stand-in starters, which shows the typed answer
without an agent for each way that a start fails and an agent that is made with exactly the tools, the folder, and the
validated model selection of the project. The service is tested with a scripted model on the real tools, instructions,
and mappers, a stand-in for the open project and for the settings that raise the events of the real services, and a log
file: the tests run whole turns, change the project and the settings, stop, reset, and dispose, and show that nothing is
written into the project and that no word of a message, an answer, a tool, or a file reaches the log. The event pipeline
and the Stop are tested on a scripted model that can wait inside a tool call, before its first update, or without ever
looking at the token (with a manual clock for the grace period), and with a consumer that lags, stops reading, or reads
only up to the completion; the coalescer is tested on its own, and the progress mapping with hand-built events of the
Copilot SDK on a manual clock. Two subsystem
tests run it as the application will, once over the stand-in Ollama server with the real factory and once for Copilot
with a stand-in starter. The starter itself is tested over a stand-in function that starts the runtime, because the
runtime is a sealed class that needs a real CLI: sharing, replacement, failure mapping, and disposal are shown there.

**The event pipeline of a turn.** The caller does not read the provider directly. `AgentService` starts a pump on a
thread of the pool that reads the updates of the agent, maps them with the `AgentEventMapper` of the turn, and adds each
event to an `AgentEventCoalescer`; the caller, which is the chat on the thread of the user interface, reads the
coalescer at its own pace. Some providers send thousands of small events in one burst (a Claude model on the Copilot
runtime was seen to send nothing for about 45 seconds and then about 4,500 events in 0.4 seconds, and the runtime then
reports that its event stream lagged; the lag was reproduced without any user interface, so it is not caused by the
application). The pipeline makes sure that the application is never the slower side: while the caller lags, events that
are next to each other and say the same thing again are merged (the pieces of text are joined, only the latest progress
of a call and the latest size of the response are kept, and a repeated `Thinking` is dropped), and the events that carry
facts (a tool that started or finished, a file that was written, the usage, `Completed`, `Failed`) are never merged,
dropped, or reordered. A file that a tool wrote while the model worked is put into the queue before the event that
follows it, so a file comes before the answer of the call that wrote it. The number of merged events is logged. When the
provider's stream ends with an exception, the caller gets the events before it and then the failure that the mapper made
of it.

**Honest progress.** What the user can be told about a running turn depends on what the provider really sends, and the
subsystem never invents progress. The mapper turns three things into events: reasoning (`Thinking`, once for each phase
and without its words), the bytes of the arguments of a tool call that the model is still writing (`ToolProgress`, with
the file name once the first characters of the arguments name it), and the size of the response so far (`ModelWorking`).
Each carries counts and at most a file name, never the content of a drawing, which belongs to the user and is large; the
events are spaced by the clock of the mapper so that a stream of thousands of pieces reaches the screen as a few events.
These events come from the raw events of the Copilot SDK that the provider carries next to its content, so they exist
only for a provider and a library release that forward them: the release of AgentKit in use forwards whole messages
only, and a Claude model on the Copilot runtime is silent for a minute or two in the middle of a long answer. For such a
turn the only truthful status is the time that has passed and the phase that the facts of the turn give (waiting for the
model, a tool that runs); the `Chat` subsystem shows exactly that, and shows byte progress as soon as the events arrive.
See the design of the `AgentEventMapper` unit for the rules and *Known Limits and Upstream Requests* in the design of
the AgentKit OTS item.

**Stop.** Canceling the token of a turn must work at any point, including in the middle of a tool call, and the
conversation must be usable afterwards. The token of a turn is linked to the token of the caller, to the end of the
service, and to a reset or a change of the project; the turn records which of them canceled it first (the caller, a
reset, the project, the end of the service, or a consumer that stopped reading) and logs it. The token of the turn is
also the token that the provider's stream is read with, so a provider that honors it ends its stream at once, and the
next turn begins only after the stream was disposed. A provider that does not end its stream within five seconds of the
cancellation is left to finish in the background, the turn ends without a failure, and the conversation is dropped, so
that the caller is never held by it. What happens to the conversation depends on how far the provider got: a Stop before
the provider said anything drops the conversation, because it is not known what the provider session holds, and the next
turn starts on a new provider session that gets the digest of the history once; a Stop later keeps the conversation and
marks it as interrupted. If the next turn on an interrupted conversation fails with a lost connection or an unexpected
error before it says anything, the failure is not shown: the conversation is replaced and the turn is run once more on
the new one, with the line `Session recovered: turn=N reason=...` in the log, and never otherwise, so a genuine failure
is never hidden. A turn that completed is not a canceled turn: nothing follows `Completed`, so a consumer that stops
reading there (as the chat does) has read the whole turn, and the conversation is not marked as interrupted. A Stop does
not refund the request at the provider: the model multiplier of the stopped message is still counted.

**The provider session per run.** AgentKit opens or resumes the runtime session for each run of the agent and releases
the handle after it; the session itself, and its events file, stay until the conversation is dropped. The runtime
therefore logs a `session.shutdown` and a close of the session after every turn, completed or stopped, and a resume
before the next one; resuming takes about a tenth of a second and replays nothing. The runtime also logs `received
response for unknown request id` after every turn, which is a benign race of the SDK. None of this is a fault, and the
application keeps one conversation, with one provider session, for the open project for as long as the project and the
settings do not change. What was missing was clarity in the log, which is why the log names the provider session.

**Logging.** The log is for people who attach it to a bug report, and it must let them tell what happened to a turn of
several minutes without a word of the user's content. Every line of a turn carries its number (`turn=N`, counted for the
life of the service). `Turn started` names the provider, the model, the reasoning effort, `historyTurns` (the count of
earlier turns that the caller handed over, which is its whole saved conversation, not what is sent) and whether the
digest of them was added (only when the provider session is new). `Tool started` and `Tool finished` name the tool, the
call, the file that it works on (made safe for one line and cut to 120 characters), the bytes of the arguments and of
the result, the outcome (`ok`, `failed`, or `refused`), and the time. `Provider session` names the identifier of the
session, whether the run `created` or `resumed` it, and whether the handle was released after the run. `Turn ended`
names the outcome, the cancel source, the time of the turn, the time to the first event and the longest gap between
events (the number that shows the silence of a buffering model), the number of events and of merged events, the length
of the answer text, whether the provider reasoned, the usage, the reasoning tokens, and the model multiplier. A turn
that failed or was canceled also gets the line `Provider session events` with the file where the provider recorded the
events of its session, which exists until the conversation is dropped. A conversation is released with its reason
(reset, changed, settings, project, dispose, unused, stopped-early, stop-timeout, or recovered) and its session
identifier. The rule that protects the user does not change: the log never gets the words of a message, the answer of
the model, the arguments or the result of a tool, or the content of a file; a test puts sentinel texts into each and
reads the whole log.

**Usage and cost.** The mapper makes a `Usage` event for each model call with the tokens that the AI library reports
(input, output, and reasoning) and, for GitHub Copilot, the model multiplier, the model, and the reasoning tokens that
the runtime reports on the raw event. The multiplier is a member of the Copilot SDK that is marked experimental; one
adapter (`CopilotUsageReader`)
reads it behind a single narrow warning suppression, so that a change of the SDK touches one method. The multiplier is
per call and not a total for the message (a message is billed once, however many model calls it makes), so the service
logs the largest it saw, and the `Chat` subsystem decides how to show it. A provider that reports none, such as Ollama,
gives no multiplier.

**Where the data of the copilot lives.** The copilot's own data never goes into the project folder. The folder of a
project receives only files that the user or the copilot is asked to create.

- *Files the copilot creates or replaces* (`project_write`): the project folder; intentional content, with no backup and
  no side files.
- *Pictures of an unsent message, attachments of a sent one, and the style kits of the project*: `.canvaspilot/`,
  `attachments/`, and `styles/` of the project; existing behavior of the Composer and Style subsystems, and intentional
  content.
- *Data of the Copilot runtime* (its home and its sessions): `{local data root}\CanvasPilot\copilot`; chosen by
  `AgentService` (and passed to the one `CopilotRuntimeStarter` by the composition), never the default of the AI
  library, and its sessions are deleted when a conversation ends.
- *Chat history*: `{local data root}\CanvasPilot\history\{key}`; kept by the Chat subsystem outside the project, and
  this subsystem refuses a project that contains it.
- *Restored history sent to a new provider session*: memory only, as one block that is never written.
- *Log*: `{local data root}\CanvasPilot\logs`; metadata only.
- *Rendered pictures for the copilot*: memory only; nothing is saved.

The local data root is the local application data of the user unless the application composition injects another, as
the tests do. A project that contains the folder of the Copilot data or of the chat history (a project in the profile
folder of the user, for example) is refused with a typed failure, because a configuration mistake must never write into
the project.

### Dependencies

- **CopilotCliLocator** unit: the Copilot CLI and its version, used by the readiness checks, the model list, and the
  factory.
- **CopilotRuntimeStarter** unit: the one shared Copilot runtime, used by the readiness check, the model list, and the
  factory.
- **ProviderModelCatalog** unit: the policy of what the application needs of a model and the catalogs of the Ollama
  servers, used by the readiness check and the factory.
- **Knowledge** subsystem: the plain tool description (`KnowledgeTool`) that the tool registry offers (the style tools
  are given to the registry in the same form), and the index of the drawing knowledge that the instructions carry.
- **Project** subsystem: the open project (`IProjectService`: its folder, its file, its name, and the event that tells
  when it changes or closes), the names of the data folder and of the project file that the tool registry and the
  project tools deny, and the filter that tells which files are image sources.
- **Preview** subsystem: the render service through which `render_image` draws pictures in memory.
- **Style** subsystem: the active style kit, whose example images `render_image` may draw and whose style the
  instructions carry (`StyleGuidance`), and the name of the styles
  folder of a project, which the project tools never write.
- **Logging** subsystem: the logger that gets the metadata of each tool call, of each turn, and of the life of each
  conversation.
- **Payload** subsystem: the message of the user (`MessagePayload` with its attachments, pictures, and marks) that the
  mapper turns into the message of the model; the mapper reads the value types of the Annotation, Composer, and Preview
  subsystems that the payload exposes, and names none of their services.
- **AgentKit** and **Microsoft.Extensions.AI** (OTS): the path policy, the tool packs, the limits for answers, and the
  function type that carry the tools, the agent factories (for a chat client and for the Copilot runtime), the context
  sizing of the Ollama client that the provider factory uses, and the model catalogs, model settings, and selections
  that list and validate models.
- **Microsoft Agent Framework** (OTS): the stream of updates (`AgentResponseUpdate`) of an agent that the event mapper
  reads; **Microsoft.Extensions.AI** also gives it the content types (text, call, result, usage, error, picture,
  reasoning).
- **GitHub Copilot SDK** (OTS, brought in by AgentKit): the raw events that the Copilot agent carries next to its
  content (the usage of a call with its experimental model multiplier, the arguments of a tool call that the model is
  writing, the size of a response, reasoning), which the event mapper reads and the usage adapter isolates.
- **Settings** subsystem: the AI provider choices (`SettingsService`), the recommended minimum context window that
  chooses the tier of the instructions, and the `IProviderCheck` and `IProviderModelSource` interfaces that
  `AgentReadiness` and `ProviderModelCatalog` implement (documented with `AiProviderSettingsPage`); `Settings` does not
  depend on this subsystem.
- **OllamaSharp** (OTS): the client that lists the models of an Ollama server and shows the context length of a model,
  used by the probe of the readiness checks and by the Ollama model catalog, and the chat client that the provider
  factory runs the Ollama agent on.

The views below show these dependencies from the SysML2 model, in four focused views because the subsystem has many
edges: the readiness checks and their suppliers, the tools, and the dependencies of the service for a turn and for the
project.

![Agent runtime dependencies](AgentSubsystemRuntimeView.svg)

![Agent tools dependencies](AgentToolsView.svg)

![Agent service turn dependencies](AgentServiceTurnView.svg)

![Agent service project dependencies](AgentServiceProjectView.svg)

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
failure with a sentence that says what to do. `AgentProviderFactory` makes the agent of a conversation for the chosen
provider: for Ollama an agent on an Ollama client that is told the context window on every request, and for GitHub
Copilot an agent on the shared runtime that the starter of the runtime gives, with only the supplied tools and the
default-deny permission handler of the AI library. `AgentService` implements `IAgentService`: it makes nothing until the
first message is sent, then builds the tools, the instructions, the agent, and one provider session for the open
project, runs one turn at a time on them, rebuilds them when the project or a setting that decides the agent changes,
deletes what the provider kept when a conversation ends, gives the Copilot runtime a data folder that is never inside a
project, and writes metadata only to the log.

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
  or replaced.
- *Constraints*: Only one turn runs at a time; a request made while a turn runs gets a single `Failed` event of kind
  `Busy`. A turn ends with exactly one `Completed` or `Failed` event, or simply ends when the caller cancels; no event
  follows either. A provider that cannot be used is reported, not thrown. A project that contains a data folder of the
  application is refused with a `Failed` event of kind `DataInsideProject`.

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
  `Failed` event for an exception, or `null` for a cancellation.
- *Constraints*: The same for GitHub Copilot and Ollama; a call is tied to its result by id and a summary is at most 200
  characters, a long text argument being shown by its length; every failure has a kind and a sentence that names the
  remedy; the log gets metadata only.

**AgentProviderFactory**: The agent of a conversation for the chosen provider.

- *Type*: In-process .NET class behind the interface `IAgentFactory` (the test seam), with `AgentCreationRequest`,
  `AgentCreationResult`, and `IAgentFactory`, documented in the design of the `AgentProviderFactory` unit.
- *Role*: Provider (the agent service is the consumer); it consumes the `AiProviderSettings` of the
  `Settings` subsystem, the tools of the registry, and the instructions.
- *Contract*: `CreateAsync(request)` returns an `AgentCreationResult`, which holds an `AIAgent` of the Microsoft Agent
  Framework or the readiness status and sentence that say why none could be made; disposing the result releases what the
  agent owns, and disposing a factory that owns its starter stops the Copilot runtime.
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
- *Constraints*: Nothing is made before the first turn; one turn at a time; the Copilot data folder is below the local
  data root and never inside a project; the restored history is one block of at most 6,000 characters, once, in memory
  only; the log has metadata only.

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
written into the project and that no word of a message, an answer, a tool, or a file reaches the log. Two subsystem
tests run it as the application will, once over the stand-in Ollama server with the real factory and once for Copilot
with a stand-in starter. The starter itself is tested over a stand-in function that starts the runtime, because the
runtime is a sealed class that needs a real CLI: sharing, replacement, failure mapping, and disposal are shown there.

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
  reads; **Microsoft.Extensions.AI** also gives it the content types (text, call, result, usage, error, picture).
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

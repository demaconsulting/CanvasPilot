## Agent

![Agent Structure](AgentSubsystemView.svg)

### Overview

The `Agent` subsystem is the AI copilot behind the chat. Its boundary starts with a message that the user sent, in the
form of the `MessagePayload` of the `Payload` subsystem, and with the AI provider choices of the `Settings` subsystem;
it ends with a stream of provider-independent events that the `Chat` subsystem shows, and with a report on whether the
chosen provider can be used. It has no user interface. Its parts are the readiness checks for the provider, the tools
that the copilot may call and the instructions that it is given, the agent of the chosen provider, and the service that
keeps one conversation for the open project and runs one turn at a time.

The subsystem is built contract first. It contains ten units. `AgentContract` is `IAgentService`, the request
and history records, the event records, and the readiness report, all free of any AI library. `CopilotCliLocator` finds
the GitHub Copilot CLI that the user installed, tells it from the launchers that editors put on the `PATH`, and checks
its version; it comes with the seams that make it testable (a process runner, a file system, and an environment reader).
`AgentReadiness` checks whether the chosen provider can be used and implements the `IProviderCheck` of the `Settings`
subsystem; it comes with the starter of the Copilot runtime (the one this build ships is gated) and the probe of the
Ollama server. `AgentToolRegistry` builds the tools that the copilot may use for one project: it confines them to the
project folder with a path policy, offers the drawing knowledge and the style tools as functions of the AI library, adds
the packs that the application supplies for the project and the optional packs that the user chose (the catalog is empty
in this version), and refuses a set that a provider could not be given. `ProjectTools` is the copilot's hands on the
project: the tools that list, read, and write the files of the project and the tool that draws a picture, all confined
to the project folder and refusing links themselves; a write replaces a file directly (the user decided that the
application keeps no backup and asks nothing, so the change is made visible instead). `AgentInstructions` writes the
instructions that the copilot is given: a role prompt, the index of the drawing knowledge, the facts of the project, and
the style of the project, in a full form or a compact form depending on the context window of the model, with sizes that
its tests measure. `PayloadMessageMapper` turns the message that the user sent (the `MessagePayload`) into the user
message that a model is given: the words, a compact description of the attachments, pictures, and marks, and the
pictures as PNG images, annotated pictures first and then the close-ups of the marks, at most ten. `AgentEventMapper`
turns the stream of updates that an agent produces for a turn into the events of the contract, the same for both
providers, and turns the exception of a failed turn into a failure with a sentence that says what to do.
`AgentProviderFactory` makes the agent of a conversation for the chosen provider: for Ollama an agent on an Ollama
client that is told the context window on every request, and for GitHub Copilot an agent on the runtime that the starter
of the runtime gives, with only the supplied tools and the default-deny permission handler of the AI library. The
starter that this build ships is gated, so for Copilot the factory answers with a typed result and no agent.
`AgentService` implements `IAgentService`: it makes nothing until the first message is sent, then builds the tools, the
instructions, the agent, and one provider session for the open project, runs one turn at a time on them, rebuilds them
when the project or a setting that decides the agent changes, deletes what the provider kept when a conversation ends,
gives the Copilot runtime a data folder that is never inside a project, and writes metadata only to the log.

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

- *Type*: In-process .NET class that implements `IProviderCheck`, with `ICopilotRuntimeStarter` (and the gated starter),
  `IOllamaProbe` (and the real probe), documented in the design of the `AgentReadiness` unit.
- *Role*: Provider (the service, the chat banner, and the connection check of the settings dialog are consumers).
- *Contract*: `GetReportAsync(settings, refresh, cancellationToken)` returns an `AgentReadinessReport`; `CheckAsync` is
  the settings dialog's connection check, which always searches again. For Copilot the checks run in order (CLI,
  version, credentials, runtime) and the first failure gives the sentence; for Ollama they are the settings, the server,
  the model, and the window.
- *Constraints*: Copilot is never reported ready by this build (the starter that is shipped answers that an update of
  the AI library is needed, and the Copilot runtime is neither bundled nor downloaded); a token is never stored, shown,
  or logged; every problem is a report and only cancellation is thrown.

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
  `AgentCreationResult`, and `ICopilotAgentSource`, documented in the design of the `AgentProviderFactory` unit.
- *Role*: Provider (the agent service is the consumer); it consumes the `AiProviderSettings` of the
  `Settings` subsystem, the tools of the registry, and the instructions.
- *Contract*: `CreateAsync(request)` returns an `AgentCreationResult`, which holds an `AIAgent` of the Microsoft Agent
  Framework or the readiness status and sentence that say why none could be made; disposing the result releases what the
  agent owns, and disposing the factory stops the Copilot runtime that it started.
- *Constraints*: Ollama gets the address, the model, and the context window of the settings on every request; GitHub
  Copilot is reached only through the starter of the runtime (gated in this build, so there is no agent), starts once,
  and gets exactly the supplied tools and the project folder with the default-deny permission handler; settings that
  cannot be used are a result, not an exception.

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
settings is used. The readiness checks are tested through `IProviderCheck`, as the settings dialog uses them: GitHub
Copilot on a machine with a signed-in GitHub CLI is not usable because this build cannot start the runtime, and Ollama
with a small window is usable with a warning. The tools are tested the same way: the registry is built over the real
drawing knowledge and a pack that keeps the policy it receives, and the test shows that only the application's own tools
are offered and that the policy permits a picture file of the project and refuses the data folder, a project file, and
every place outside the project. The project tools are tested on a real project folder: a drawing is written, listed,
read, replaced, and drawn by the real render service, and the test shows that nothing but the drawing is added to the
project and that paths that leave the project or reach the project file are refused. The instructions are tested with
the real cards and the real kits: their sizes are measured for the full and the compact tier and for the tools, and a
test builds the instructions and the tools of one project together and shows that the tools that the role prompt names
are exactly the tools that are offered. The mapper of the message is tested with golden text for each rule, and with a
real payload that the real builder made from a real picture and real marks, which shows the text and the images that a
model would receive. The mapper of the updates is tested with scripted streams for every kind of content, every cause of
failure, and the rules for a canceled turn, and with a real agent, on a scripted model, that writes a drawing, looks at
it, and tries to write outside the project with the real project tools, which shows the events that the chat will get.
The factory is tested against a stand-in Ollama server with the real tools of a project, which shows a conversation from
the settings to the drawing in the project, and for Copilot with the gated starter and with a fake starter, which shows
the typed answer without an agent and an agent that is made with exactly the tools and the folder of the project.
The service is tested with a scripted model on the real tools, instructions, and mappers, a stand-in for the open
project and for the settings that raise the events of the real services, and a log file: the tests run whole turns,
change the project and the settings, stop, reset, and dispose, and show that nothing is written into the project and
that no word of a message, an answer, a tool, or a file reaches the log. Two subsystem tests run it as the application
will, once over the stand-in Ollama server with the real factory and once for Copilot with the gated starter.

**Where the data of the copilot lives.** The copilot's own data never goes into the project folder. The folder of a
project receives only files that the user or the copilot is asked to create.

- *Files the copilot creates or replaces* (`project_write`): the project folder; intentional content, with no backup and
  no side files.
- *Pictures of an unsent message, attachments of a sent one, and the style kits of the project*: `.canvaspilot/`,
  `attachments/`, and `styles/` of the project; existing behavior of the Composer and Style subsystems, and intentional
  content.
- *Data of the Copilot runtime* (its home and its sessions): `{local data root}\CanvasPilot\copilot`; chosen by
  `AgentService`, never the default of the AI library, and its sessions are deleted when a conversation ends.
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

- **CopilotCliLocator** unit: the Copilot CLI and its version, used by the readiness checks.
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
  function type that carry the tools, and the agent factories (for a chat client and for the Copilot runtime) and the
  context sizing of the Ollama client that the provider factory uses.
- **Microsoft Agent Framework** (OTS): the stream of updates (`AgentResponseUpdate`) of an agent that the event mapper
  reads; **Microsoft.Extensions.AI** also gives it the content types (text, call, result, usage, error, picture).
- **Settings** subsystem: the AI provider choices (`SettingsService`), the recommended minimum context window that
  chooses the tier of the instructions, and the `IProviderCheck` interface that
  `AgentReadiness` implements (documented with `AiProviderSettingsPage`); `Settings` does not depend on this subsystem.
- **OllamaSharp** (OTS): the client that lists the models of an Ollama server and shows the context length of a model,
  used by the probe of the readiness checks, and the chat client that the provider factory runs the Ollama agent on.

The views below show these dependencies from the SysML2 model, in four focused views because the subsystem has many
edges: the readiness checks and their suppliers, the tools, and the dependencies of the service for a turn and for the
project.

![Agent runtime dependencies](AgentSubsystemRuntimeView.svg)

![Agent tools dependencies](AgentToolsView.svg)

![Agent service turn dependencies](AgentServiceTurnView.svg)

![Agent service project dependencies](AgentServiceProjectView.svg)

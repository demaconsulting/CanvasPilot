### AgentService

![Agent Service Turn](AgentServiceTurnView.svg)

#### Purpose

`AgentService` is the AI copilot as the chat uses it: the one implementation of `IAgentService`. It keeps one
conversation with the AI provider for the open project, runs one turn at a time on it, and streams what happens as the
provider-independent events of the contract. Everything that decides when a conversation starts, when it must be made
again, and when it ends is in this unit, so the chat only sends messages and reads events. The unit also keeps the data
of the copilot out of the project and keeps message text out of the log.

#### Data Model

**AgentService**: `sealed class`, implements `IAgentService`. The constructor takes the project service, the settings
service, the tool registry, the instructions writer, the factory (which the service owns and disposes), and the
readiness checks, and optionally the project tools (the service passes on their `FileWritten` event), the active style,
the logger, the local data root, and the history root. It subscribes to the change of the settings and of the open
project. Public constants: the names of the folders (`CanvasPilot`, `copilot`, `history`) and the sentences for a busy
service, no open project, a closing application, and a data folder inside the project.

**Conversation** (private): the project file it was made for, the signature of the settings it was made from, the
provider, the `AgentCreationResult` that owns the agent, the agent, the provider session, and whether the restored
history was given to the provider. **TurnState** (private): the source of the token of the turn, the queue of files that
tools wrote during the turn, the conversation the turn runs on, and a task that completes when the turn is over.

**RestoredHistoryDigest** (supporting, internal): writes the one block that stands for the restored turns.
**RuntimeLogAdapter** (supporting, internal): the `ILogger` that the Copilot runtime writes to. Neither has a part
definition of its own.

#### Lifecycle of a Conversation

- **Nothing before the first turn.** The constructor builds nothing. `GetReadinessAsync` only asks the readiness checks.
  The first turn finds the open project, builds the tools (`AgentToolRegistry.CreateTools`) and the instructions
  (`AgentInstructions.Build`, with the facts of the project: its name, its folder, the first 200 files below it without
  the data folder, the project files, and links, and the active style), asks the factory for the agent, and creates one
  provider session. The factory asks the starter for the Copilot runtime only for the provider that needs it, and the
  runtime is shared by the whole application and not owned by the conversation.
- **One conversation per open project.** The conversation is kept for the next turns. It is dropped, and made again by
  the next turn, when the open project changes or closes (the event of the project service), when a setting that the
  agent was made from changes (the *signature*: the provider, the model, the Ollama address, the context window, and the
  path of the Copilot CLI; the reasoning level and the other settings are not part of it), and when `ResetConversation`
  is called.
- **Release.** Dropping a conversation never blocks the caller. A background task waits (at most ten seconds) for the
  turn that runs on the conversation, deletes the session that the provider kept through
  `IAgentFactory.DeleteSessionAsync` (for Copilot the runtime would otherwise keep every conversation in its data
  folder), and disposes what the agent owns. A reset or a change of the project also cancels the turn that runs on the
  conversation; a change of a setting does not, because the old agent can finish its turn.
- **End of the service.** `Dispose` cancels every turn, releases the conversation, waits (at most ten seconds) for the
  releases, and disposes the factory once, which stops the Copilot runtime when the factory owns its starter. Later
  turns fail with a sentence and later events of the other services are ignored.

#### One Turn

`RunTurnAsync` returns the events of a turn. The turn begins when the stream is first read.

1. **One at a time.** If another turn runs, the stream is one `Failed` event of kind `Busy` and nothing else. The
   running turn is not touched.
2. **Preparation** (`PrepareAsync`). No project open gives `Failed(NotReady)`. A data folder inside the project gives
   `Failed(DataInsideProject)` (see below). Otherwise the conversation is kept, dropped, or made as above; a factory
   that cannot make an agent gives `Failed(NotReady)` with the sentence of the readiness check for the same cause (for
   Copilot without credentials, the sign-in sentence of the starter), and is asked again by the next turn so that a fix
   is noticed. An exception becomes `Failed(Unexpected)` whose sentence holds the message of
   the exception (the user sees it; the log does not).
3. **The message.** `PayloadMessageMapper.Map(payload, projectFolder)` builds the user message. If the conversation has
   not yet given the restored history to the provider and the request carries history, the digest is inserted as the
   first text part of that message, so the provider gets it once, with the first message of its session.
4. **Streaming.** The agent runs on the message and the provider session with the token of the turn, and one
   `AgentEventMapper` (made for the turn with the same token) turns the updates into events. Each file that a project
   tool wrote during the turn is put into the stream as `AgentEvent.FileWritten` immediately before the event that
   follows it, which places it before the `ToolFinished` of the call that wrote it. The restored history counts as given
   as soon as the provider answered with anything but a failure, so a first turn that failed before an answer leaves it
   pending.
5. **The token of the turn** is linked to the token of the caller, to the end of the service, and, through the turn
   state, to a reset or a change of the project. A cancelled turn ends the stream with neither `Completed` nor `Failed`,
   because the mapper treats an exception after cancellation as a stop.

The service raises its own `FileWritten` event for every file that the project tools write, on the thread of the tool,
so that the shell can select the file in the explorer.

#### The Restored History

The text of the earlier turns of a restored conversation (`AgentTurnRequest.RestoredHistory`) is turned into one block:
a header line that says that the text was restored and that pictures and tool results were not kept, a note that counts
the older turns that were left out when they do not fit, and then the newest turns that fit as `User: …` and
`Assistant: …` lines. Each turn is made one line (runs of white space become one space) and cut to 1,200 characters
with an ellipsis, and the block is at most 6,000 characters however long the history is. It is built in memory for the
first message of a new provider session and is never written to a file or to the log.

#### Where the Data of the Copilot Lives

`GetCopilotDataDirectory(localDataRoot)` is `{local data root}\CanvasPilot\copilot`; the local data root is the local
application data of the user (with the same fallbacks as the log) unless the application composition injects another,
which the tests do. The service passes that folder to the factory in every request, and from there to the starter of the
Copilot runtime, so the runtime never keeps its files in a place of its own choosing. The folder of the chat history is
`{local data root}\CanvasPilot\history` (the Chat subsystem keeps it; the service only knows it to check it).

Before a conversation is made, `IsInside(folder, project folder)` compares the two folders by their full paths, whole
names, and without regard to case on Windows and macOS (the comparison of the project path guard). A turn is refused
with `Failed(DataInsideProject)` and a sentence that names the folder when the Copilot data folder or the history folder
lies inside the project folder, which is the case when the project is, for example, the profile folder of the user. The
service itself writes nothing: tests show that a turn that writes nothing leaves the project folder as it was and that
the local data root is not created by the service.

#### Logging

The log gets one line for the start of a conversation (`Conversation started: provider, tier, tools, files`), one for
the start of a turn (`Turn started: provider, restoredTurns, digestAdded`), one for the end
(`Turn ended: outcome, events, milliseconds`), one for the release (`Conversation released: provider, reason`), and a
line for a refusal, a failure to prepare (`exception=` the type only), a session that could not be deleted, and a
conversation that could not be disposed. It never gets the words of a message, the answer of the model, the restored
history, the arguments or the result of a tool, the content of a file, the path of a project, or the text of an
exception; the mapper and the project tools add their own lines under the same rule. The Copilot runtime writes through
`RuntimeLogAdapter`, which logs the level, the category, the event number, and the type of an exception and never calls
the formatter, so the message is never built. A log that cannot be written never fails a turn.

#### Error Handling

A provider that cannot be used, a project that is not open, a data folder inside the project, and a second request while
a turn runs are `Failed` events, never exceptions. Missing constructor arguments and a missing request are refused with
`ArgumentNullException`. An exception while a conversation is made is caught and reported as `Failed(Unexpected)`; an
exception while the agent streams is turned into a failure by the mapper. Cancellation ends the stream quietly. The
release of a conversation catches and logs its own exceptions.

#### Security

The service gives the agent only the tools that the registry built for the open project, confined to its folder. It
holds no credential. Everything it writes (the log) is metadata. The Copilot runtime's data never lies inside a
project, so nothing that the runtime keeps travels with a project that is shared or put under source control, and the
sessions that it keeps are deleted when a conversation ends.

#### Dependencies

`IProjectService` and `ProjectInfo` of the `Project` subsystem (the open project and the event that tells when it
changes); `ISettingsService` and `AiProviderSettings` of the `Settings` subsystem (the choices and the event that tells
when they change); the `AgentToolRegistry`, `AgentInstructions`, `PayloadMessageMapper`, `AgentEventMapper`,
`AgentReadiness`, `ProjectTools`, and `AgentProviderFactory` units and the contract of the same subsystem;
`IActiveStyle` of the `Style` subsystem (the style of the project, for the instructions); `RollingFileLogger` of the
`Logging` subsystem; the Microsoft Agent Framework (`AIAgent`, `AgentSession`) and Microsoft.Extensions.AI
(`ChatMessage`, `TextContent`).

#### Callers

The `Chat` subsystem runs turns, stops them by canceling the token, resets the conversation, and shows the readiness
report. The application shell builds the service with the real collaborators, passes the same `ProjectTools` to the
registry and to the service, handles `FileWritten` to select the file in the explorer, and disposes the service before
the settings and project services. Tests build it on stand-ins, with a scripted chat client behind a scripted factory.

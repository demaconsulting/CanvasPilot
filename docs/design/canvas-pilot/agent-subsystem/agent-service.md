### AgentService

![Agent Service Turn](AgentServiceTurnView.svg)

#### Purpose

`AgentService` is the AI copilot as the chat uses it: the one implementation of `IAgentService`. It keeps one
conversation with the AI provider for the open project, runs one turn at a time on it, and streams what happens as the
provider-independent events of the contract. Everything that decides when a conversation starts, when it must be made
again, and when it ends is in this unit, so the chat only sends messages and reads events. The unit also reads the
provider off the thread of the caller, keeps a lagging caller from falling behind the provider, makes a Stop safe at any
point of a turn, keeps the data of the copilot out of the project, and keeps message text out of the log.

#### Data Model

**AgentService**: `sealed class`, implements `IAgentService`. The constructor takes the project service, the settings
service, the tool registry, the instructions writer, the factory (which the service owns and disposes), and the
readiness checks, and optionally the project tools (the service passes on their `FileWritten` event), the active style,
the logger, the local data root, the history root, and the clock (the system clock when absent; the tests move it by
hand). It subscribes to the change of the settings and of the open project. Public constants: the names of the folders
(`CanvasPilot`, `copilot`, `history`) and the sentences for a busy service, no open project, a closing application, and
a data folder inside the project. `StopGrace` (5 seconds, internal) is how long a turn waits for the provider to end
its stream after a cancellation.

**Conversation** (private): the project file it was made for, the signature of the settings it was made from, the
provider, the `AgentCreationResult` that owns the agent, the agent, the provider session, whether the restored history
was given to the provider (`HistoryPrefixed`), and whether the latest turn on it was canceled after the provider had
begun to answer, so that the session may not be usable (`Interrupted`; cleared when the provider answers again).
**TurnState** (private): the number of the turn (counted from one for the life of the service), the source of the token
of the turn, the first source that canceled it (`TurnCancelSource`, below), the queue of files that tools wrote during
the turn, the conversation the turn runs on, and a task that completes when the turn is over. **TurnCancelSource**
(private enum): `None`, `User` (the caller's token: Stop, or the caller was closed), `Reset`, `Project` (the open
project changed or closed), `Dispose` (the service ended), and `Consumer` (the caller stopped reading the events of a
turn that was not over). The first source that is recorded is the one that is kept, and it is recorded before the token
is canceled, so whoever sees the cancellation sees who asked for it. **TurnStats** (private): what a turn counts for
the log (the events given to the caller and the events that were merged, the time to the first event and the longest
gap between events of the provider, the length of the answer text, whether the provider reasoned, the tokens, the
reasoning tokens and the largest multiplier that it reported, the outcome, and the reason for a recovery); the pump
observes the events as they come from the provider and a lock guards what it writes.

**AgentEventCoalescer** (supporting, internal): the queue between the pump and the caller, described in *The Event
Pipeline*. **RestoredHistoryDigest** (supporting, internal): writes the one block that stands for the restored turns.
**RuntimeLogAdapter** (supporting, internal): the `ILogger` that the Copilot runtime writes to. None has a part
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
  agent was made from changes (the *signature*: the provider, the model, the reasoning effort, the Ollama address, the
  context window, and the
  path of the Copilot CLI; the other settings are not part of it), and when `ResetConversation`
  is called.
- **Release.** Dropping a conversation never blocks the caller. A background task waits (at most ten seconds) for the
  turn that runs on the conversation, deletes the session that the provider kept through
  `IAgentFactory.DeleteSessionAsync` (for Copilot the runtime would otherwise keep every conversation in its data
  folder), and disposes what the agent owns. A reset or a change of the project also cancels the turn that runs on the
  conversation; a change of a setting does not, because the old agent can finish its turn. Each release is logged with
  its reason (`reset`, `changed`, `settings`, `project`, `dispose`, `unused`, `stopped-early`, `stop-timeout`, or
  `recovered`) and the identifier of the provider session.
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
   Copilot that is not signed in, the sign-in sentence of the starter), and is asked again by the next turn so that a
   fix is noticed. An exception becomes `Failed(Unexpected)` whose sentence holds the message of the exception (the user
   sees it; the log does not).
3. **The message.** `PayloadMessageMapper.Map(payload, projectFolder)` builds the user message. If the conversation has
   not yet given the restored history to the provider and the request carries history, the digest is inserted as the
   first text part of that message, so the provider gets it once, with the first message of its session.
4. **Streaming.** The agent runs on the message and the provider session with the token of the turn, and one
   `AgentEventMapper` (made for the turn with the same token, the clock of the service, and the number of the turn)
   turns the updates into events. The provider is read on a thread of the pool and the events wait in an
   `AgentEventCoalescer` that the caller reads (see *The Event Pipeline*). Each file that a project
   tool wrote during the turn is put into the queue immediately before the event that follows it, which places it before
   the `ToolFinished` of the call that wrote it. The restored history counts as given as soon as the provider answered
   with anything but a failure, so a first turn that failed before an answer leaves it pending.
5. **The token of the turn** is linked to the token of the caller, to the end of the service, and, through the turn
   state, to a reset or a change of the project. A cancelled turn ends the stream with neither `Completed` nor `Failed`,
   because the mapper treats an exception after cancellation as a stop (see *Stop*).
6. **The end.** Whatever ended the turn, its attempt ends only after the provider's stream was disposed, so the next
   turn can never start on a session whose turn is still running; then the files of the provider session are named in
   the log when the turn failed or was canceled, the turn is ended, and the line `Turn ended` is written.

The service raises its own `FileWritten` event for every file that the project tools write, on the thread of the tool,
so that the shell can select the file in the explorer.

#### The Event Pipeline

The caller of `RunTurnAsync` is the chat, on the thread of the user interface. It must never be what the provider
waits for, because some providers send thousands of small events in one burst, and the Copilot runtime reports that its
event stream lagged for such a burst. An attempt of a turn therefore has two sides that meet in an
`AgentEventCoalescer`:

- **The pump** runs on a thread of the pool (`Task.Run`, not tied to the token of the turn, so that it always ends by
  reading the provider's stream to its end and disposing it). It enumerates `AgentEventMapper.MapAsync(updates)`. For
  each event it first moves into the queue the files that tools wrote since the last event (so a file comes before the
  event that follows it), records the event in the statistics of the turn (the time, the length of the text, the usage),
  and adds it. When the stream ends it completes the queue, or completes it with the exception that ended it.
- **The reader** is the iterator that the caller reads. It takes the events from the queue in order and gives them to
  the caller. It waits for the next event with a token that is canceled only when the grace period after a cancellation
  has passed (see *Stop*), and it hands over every event that waits before it gives up.

The `AgentEventCoalescer` is a lock-guarded linked list with one signal. `Add` looks only at the event at the end of the
queue, so the order never changes, and merges the new event into it when they say the same thing again: a `TextDelta`
after a `TextDelta` is joined to it, a `ToolProgress` replaces a `ToolProgress` of the same call (progress of another
call stays), a `ModelWorking` replaces a `ModelWorking`, and a `Thinking` after a `Thinking` is dropped. Every other
event (`ToolStarted`, `ToolFinished`, `FileWritten`, `Usage`, `Completed`, `Failed`) is added as it is and is never
merged, dropped, or reordered. The number of events that were merged or dropped is counted and logged for the turn.
`Complete` ends the stream after the events that wait; a fault that it is given is thrown to the reader once the events
before it were read (the mapper turns every exception of the provider into a `Failed` event, so a fault here is a
defect of the mapper). Nothing is added after the stream ended. One thread adds and one reads. A reader whose token was
canceled still receives what waits before the cancellation takes effect, so a cancellation never loses an event that was
already queued.

#### Stop

Stop must work at any point of a turn, in the middle of a tool call as well, and the conversation must be usable
afterwards. The pieces:

1. **Who canceled.** The token of the turn is cancelled by the caller's token (`User`), by a reset (`Reset`), by a
   change of the project (`Project`), by the end of the service (`Dispose`), or by the service itself when the caller
   stops reading a turn that is not over (`Consumer`). The first source is recorded and logged as `cancelSource` on the
   line `Turn ended`.
2. **The provider is told.** The token of the turn is the token that the agent runs with, so a provider that honors it
   ends its stream, the mapper sees the exception after the cancellation and ends without a failure and without a
   completion, and the turn ends with the events so far.
3. **The grace period.** When the token of the turn is canceled, a timer on the clock of the service starts for
   `StopGrace` (5 seconds). When it fires, the reader gives up and the attempt ends; the pump is left to finish in the
   background, and the conversation is dropped with the reason `stop-timeout` and the line "The provider did not stop
   within 5 seconds; its session is dropped", so the next turn starts on a new provider session instead of waiting
   behind the old one. A provider that ends its stream sooner is waited for until it has ended, so the next turn never
   starts on a session whose turn is still running.
4. **Before the provider said anything.** A cancellation after which no event reached the caller drops the conversation
   (reason `stopped-early`): it is not known what the provider session holds, and the next turn creates a new session
   and sends the digest of the history once, in the one message that goes to it. If the session were kept, the digest
   would be sent again into a session that has it.
5. **After the provider began to answer.** The conversation is kept and marked as interrupted. The next turn answers on
   the same agent and the same session. The mark is cleared when the provider answers again with anything but a failure.
6. **Recovery after a Stop, once.** If the next turn on an interrupted conversation fails before it says anything and
   the failure is a lost connection or an unexpected error, the failure is not given to the caller: the attempt ends,
   the conversation is dropped (reason `recovered`), the line `Session recovered: turn=N reason=<kind of the failure>`
   is written, and the turn is run once more, on a new conversation that gets the digest of the history. The second
   attempt cannot recover again, so its failure is shown. A failure of a conversation that nobody stopped, a failure
   that comes after the first event, and a failure of another kind (an authentication or a model that is missing) are
   shown at once. Nothing else is retried.
7. **A completed turn is not a canceled turn.** Nothing follows `Completed`, so a consumer that stops reading at it, as
   the chat does, has read the whole turn. The attempt counts a yielded `Completed` as drained. Before this was so, the
   chat's stop at `Completed` disposed the iterator before the end of the queue was seen, the service took that for a
   consumer that walked away, canceled the turn that had completed (the log said `cancelSource=Consumer` after every
   turn), and marked the conversation as interrupted; a connection failure of the next turn was then recovered silently
   on a new session, replaying the digest, instead of being shown, which is the opposite of "recovery only after a
   Stop". A completed turn now ends with `cancelSource=None` and leaves the conversation as it was.

A Stop does not refund the request at the provider: the message is counted by GitHub Copilot at its model multiplier
whether the turn ended or was stopped, so the chat tells the user that a stopped request may still count.

#### The Provider Session per Run

For GitHub Copilot, AgentKit opens or resumes the runtime session for each run of the agent and releases the handle
when the run ends (it cancels the run and disposes the channel); the session, with its events file, stays until the
service deletes it. This is by design and has visible traces that are not faults: after every turn, completed or
stopped, the runtime logs the shutdown of the session and a resume before the next turn (about a tenth of a second, no
replay), and `received response for unknown request id`, a benign race of the SDK. The service keeps one conversation,
one agent and one provider session object, for the open project. After each attempt it writes
`Provider session: turn=N id=<id> run=<created|resumed> handleReleased=<true|false>` through
`IAgentFactory.DescribeSession`: `created` when the session had no identifier before the run, `resumed` when it had, and
`handleReleased` true when the provider's stream ended and was disposed. A provider that has no identifier for its
sessions (Ollama) gives no such line.

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

The log is for people who attach it to a bug report; it says what happened and never what was said. Every line of a turn
carries `turn=N`, counted from one for the life of the service. The lines are:

- `Conversation started: provider, tier, tools, files` and `Conversation released: provider, reason, session` (the
  reason and the provider session identifier, or `none`).
- `Turn started: turn, provider, model, effort, historyTurns, digestAdded`. The model and the effort are the settings,
  or `(default)`. `historyTurns` is the number of earlier turns that the caller handed over, which is its whole saved
  conversation and not what is sent (an earlier version of the log called it `restoredTurns`, which read as if all of
  them were sent for every message); `digestAdded` is true only for the first message of a new provider session, which
  is the only time that the provider gets the digest.
- `Provider session: turn, id, run (created or resumed), handleReleased` (see *The Provider Session per Run*), and, for
  a turn that failed or was canceled, `Provider session events: turn, id, file`, the full path of the file where the
  provider recorded the events of the session, which exists until the conversation is dropped (the path is under the
  data folder of the runtime, never in a project).
- The lines of the mapper for each call: `Tool started` and `Tool finished` (name, path of a file of the project, bytes,
  outcome, time), and `Turn completed`, `Turn failed` (with the kind, the source, and the HTTP status), or
  `Turn canceled`; see the design of the `AgentEventMapper` unit.
- `Turn ended: turn, outcome, cancelSource, milliseconds, events, merged, firstEventMs, maxGapMs, textChars, reasoning,
  usageIn, usageOut, reasoningTokens, premiumMultiplier`. The outcome is `Completed`, `Failed:<kind>`, or `Canceled`;
  `cancelSource` is `None`, `User`, `Reset`, `Project`, `Dispose`, or `Consumer`; `firstEventMs` and `maxGapMs` are the
  time to the first event of the provider and the longest silence between two events, which are the numbers that show a
  model that buffers its answer; `merged` is the number of events that the pipeline merged for a lagging caller;
  `textChars` is the length of the answer and never the answer; `reasoning` is true when the provider reported reasoning
  and never carries its words; the tokens and the largest model multiplier are `n/a` when the provider did not report
  them.
- `Session recovered: turn, reason`, `The provider did not stop within 5 seconds; its session is dropped`, a line for a
  refusal (`Turn refused: reason`), a failure to prepare (`Turn could not start`, with `exception=` the type only), a
  session that could not be deleted, and a conversation that could not be disposed.

It never gets the words of a message, the answer of the model, the restored history, the arguments or the result of a
tool, the content of a file, the path of a project, or the text of an exception; the mapper and the project tools add
their own lines under the same rule. The Copilot runtime writes through `RuntimeLogAdapter`, which logs the level, the
category, the event number, and the type of an exception and never calls the formatter, so the message is never built. A
log that cannot be written never fails a turn.

#### Error Handling

A provider that cannot be used, a project that is not open, a data folder inside the project, and a second request while
a turn runs are `Failed` events, never exceptions. Missing constructor arguments and a missing request are refused with
`ArgumentNullException`. An exception while a conversation is made is caught and reported as `Failed(Unexpected)`; an
exception while the agent streams is turned into a failure by the mapper, and only a lost connection or an unexpected
error before the first event of a conversation that a Stop left interrupted is recovered once instead of reported (see
*Stop*). Cancellation ends the stream quietly, and a provider that does not honor it is given up after the grace period
instead of holding the caller. The release of a conversation catches and logs its own exceptions.

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
`Logging` subsystem; `TimeProvider` of .NET (the clock of the grace period and of the times in the log); the Microsoft
Agent Framework (`AIAgent`, `AgentSession`) and Microsoft.Extensions.AI (`ChatMessage`, `TextContent`). The factory is
asked through `IAgentFactory` for the agent, for the deletion of a session, and for the description of a session
(`DescribeSession`, which gives the identifier and the events file for the log).

#### Callers

The `Chat` subsystem runs turns, reads their events (stopping at `Completed`), stops them by canceling the token, resets
the conversation, and shows the readiness
report. The application shell builds the service with the real collaborators, passes the same `ProjectTools` to the
registry and to the service, handles `FileWritten` to select the file in the explorer, and disposes the service before
the settings and project services. Tests build it on stand-ins, with a scripted chat client behind a scripted factory.

### ChatTurnRunner

![Chat Turn Dependencies](ChatSubsystemTurnView.svg)

#### Purpose

`ChatTurnRunner` is the logic of one turn of the conversation, without a window. It gives the message that the user sent
to the AI copilot, reads the events of the turn, keeps the rows of the transcript that show what happens (the answer
that streams, one line for each tool the copilot calls, and the notices), stops the turn on request, and never lets a
failure reach the interface. It tells the panel when it is busy, so that the panel can block the composer while the
copilot works, and what the turn is doing, so that a turn that takes minutes never looks dead: the words of the phase,
the time that has passed, and, after a long silence, the honest hint that explains the wait. When the turn has ended it
puts what the turn cost under its last reply. It exists so that the panel stays small and so that the rules of a turn,
which are many (order of rows, coalescing, status, stop, failure, usage), are verified with a scripted copilot and a
manual clock and no display.

#### Data Model

- `ChatTurnRunner`: `sealed class`, implements `IDisposable`. The constructor takes the `IAgentService`, the transcript
  (any `ICollection<TranscriptRowViewModel>` to which it adds rows), an optional `TimeProvider` (the system's by
  default), an optional logger, and an optional action that opens the settings, which the error notices it makes call
  from their "Open Settings" button. `IsBusy` and the `BusyChanged` event tell whether a turn runs. `UpdateInterval` is
  50 milliseconds.
- The status of the turn, which the panel shows in its busy row: `StatusText` (what the turn is doing), `ElapsedText`
  (how long it has run), `HintText` (the explanation of a long wait), `IsStopping`, and the event `StatusChanged`, which
  is raised, after the change, whenever any of the first three or `IsStopping` changed. All of them are empty or false
  when no turn runs. `TickInterval` (one second) is the period of the timer of the elapsed time, `HintDelay` (30
  seconds) is how long nothing may be heard from the copilot before the hint shows, and the words are constants:
  `WorkingText` ("The copilot is working..."), `ThinkingText` ("Thinking..."), `AnsweringText` ("Writing the
  answer..."), `WaitingText` ("Waiting for the model..."), `ModelWritingText` ("The model is writing..."),
  `StoppingText` ("Stopping..."), and `LongWaitHint` ("Large drawings can take a few minutes, and some models show
  nothing until the drawing is complete. Stop cancels the turn, but the provider may still count the request."). The
  hint says only what is true of every provider: some models send a large answer only when it is whole, and a provider
  may count a request that was started even when the turn was stopped.
- `RunTurnAsync(SentMessage, restoredHistory)` and `RunTurnAsync(AgentTurnRequest)` run a turn and return a task of
  `ChatTurnOutcome` (`Completed`, `Stopped`, `Failed`, `Refused`) that completes when the turn has ended. `Stop` stops
  the turn that runs; `Dispose` stops it and refuses new turns.
- `TurnUsage` (public, sealed): the helper of the runner that holds what a turn cost, summed from the `AgentEvent.Usage`
  events of the turn, and writes it as one caption. It holds the tokens sent (`InputTokens`), written (`OutputTokens`),
  and spent on reasoning (`ReasoningTokens`), each `null` until the provider reports it, and the model multiplier
  (`Multiplier`, also `null` until reported) and the words for the context size that the settings asked for
  (`ContextSize`, `null` when none was chosen, taken from the `Usage` events, which the service stamps with it). `Add`
  takes one usage event; `Format(elapsed, stopped)` writes the
  caption; `FormatTokens` writes a count of tokens for a person; `StoppedNote` is the words that end the caption of a
  stopped turn. The runner makes one for each turn and nothing else uses it; it belongs to this unit and has no unit of
  its own.
- `ToolActivityText` (internal, static): the sentences of the tool rows and the notices. It knows the names of the tools
  that CanvasPilot gives the copilot (`project_write`, `project_read`, `project_list`, `render_image`, `canvasnet_help`,
  `style_get`, `style_check`) as plain text, because the chat depends only on the contract of the Agent subsystem and
  not on the units that build the tools; a tool that is not known gets `Running {name}` and `Ran {name}`, so a new tool
  needs no change to be shown. `Progress` gives the sentence of a file that is being written with the bytes so far, and
  `FormatSize` writes a count of bytes for a person (`512 B`, `9.8 KB`, `1.2 MB`, in the culture of the user). It also
  gives, for each kind of failure, the sentence about what to do and whether the settings can help.
- The rows that the runner changes while a call is under way: `ToolActivityRowViewModel.UpdateRunning` changes the
  sentence (and, when it is given, the description of the arguments) of a call that has not ended, and does nothing for
  one that has. `AssistantReplyViewModel.SetUsageCaption` puts the caption on a reply; the caption is not part of the
  text, is not saved in the history, and can be set after the reply has ended.
- Internal state of one turn (`Turn`): the synchronization context the turn started on, the cancellation source, the
  text that waits, the reply that is being written and the last reply of the turn, the rows of the tool calls by call id
  and in order, the time of the last update of the text, the timer of that update, the timer of the elapsed time, the
  time the turn started, the time the copilot was last heard from, the phase, the elapsed text, whether the hint is
  shown, the `TurnUsage`, and whether the turn was stopped or has ended.

#### Key Methods

**Starting a turn.** `RunTurnAsync(SentMessage, history)` refuses, with an error notice, a message whose payload is not
a `MessagePayload`, and otherwise makes an `AgentTurnRequest` with the payload and the history. `RunTurnAsync(request)`
returns `Refused` at once, touching nothing, when a turn runs or the runner was disposed. Otherwise it makes the turn,
notes the time the turn started (which is also the time the copilot was last heard from), sets the timer of the elapsed
time, sets `IsBusy`, and raises `BusyChanged`, all before its first `await`, so that the panel can block the composer in
the same call that starts the turn; the status is then the working text and `0:00`. It then reads the events and, when
the turn has ended (in a `finally`), releases the turn, and raises `BusyChanged` again.

**Events to rows.** `AgentEvent.TextDelta` goes to the coalescing below. Any other event that has a row first adds the
text that waits, so the rows keep their order. `ToolProgress` (the model is writing the arguments of a call, with the
bytes so far and the path when it is known) goes to the progress row below. `ToolStarted` ends the reply that is being
written (it is complete: its text is whole) and adds a `ToolActivityRowViewModel` with the sentence for what is under
way, giving it the runner's own clock, which the row uses to time the call (the manual clock of the tests therefore
times it too); when the progress of the same call already made the row, the row is not made again but says what runs
now, with the arguments of the call. `ToolFinished` finds the row by call id (it makes one when the start was not seen)
and finishes it with the sentence for what was done, or for what could not be done and why. `FileWritten` finds the
latest `project_write` row that has no report of a file and reports that the file was created or replaced (the sentence
is "Created" or "Replaced" and the path in code format), whichever of the file event and the tool result comes first;
when no such row exists it adds a row of its own. `Thinking` and `ModelWorking` make no row; they only change the
status. `Usage` makes no row either; it is added to the `TurnUsage` of the turn. `Completed` adds the waiting text,
completes the reply, ends the calls that have no result as `(no result)`, shows the usage, and ends the turn. The runner
stops reading the stream at `Completed`, because nothing follows it, and the service treats a `Completed` that it has
yielded as the end of the turn and not as a turn that its consumer canceled.

**Progress rows.** A file that the model is writing is the longest wait of a turn, and the model reports its size while
it writes. `ToolProgress` makes the row of the call as soon as it arrives (after it ended the reply that was being
written, so that the order of the transcript is the order of the events) and says on it what is under way with the bytes
so far, for example `Writing x.svg... 9.8 KB` with the file name in code format, or `Writing a file... 100 B` while the
path is not yet known. Every later progress of the same call changes the sentence of that one row (and gives it the
path when it becomes known), so the transcript shows one running row that grows and not a row for each report. The start
and the end of the call go on to update the same row. A progress event for a call whose row has ended changes nothing.
Only the file name and the bytes are reported, never the content that is being written.

**Status.** While a turn runs the runner keeps three short texts for the busy row, and an update of any of them raises
`StatusChanged`. They are brought up to date by one method that every event and every tick of the timer calls, under the
lock of the runner.

- *The phase* (`StatusText`) follows what the copilot reports and nothing else: `Thinking` gives "Thinking...";
  `ModelWorking` gives "The model is writing..." with the bytes received so far; `ToolProgress` gives the sentence of
  the progress row; `ToolStarted` gives the sentence of the tool with three dots; `ToolFinished` gives "Waiting for the
  model..."; and a piece of text gives "Writing the answer...". Events that say nothing about what is under way (the
  usage, a written file) leave the phase as it was. Nothing is made up: a model that reports no progress shows the
  working text, the elapsed time, and, after a while, the hint.
- *The elapsed time* (`ElapsedText`) is the time since the turn started, written as `m:ss` (and `h:mm:ss` from an hour
  on, by `FormatElapsed`). The timer of the elapsed time is made from the clock of the runner, fires once for each
  `TickInterval`, and hands its work to the synchronization context that the turn started on (or runs it directly when
  there was none), as the timer of the text does. It is the one sign of life that is true for every model, also when the
  provider is silent for minutes.
- *The hint* (`HintText`) is `LongWaitHint` while the copilot has not been heard from (no event of the turn at all) for
  `HintDelay`, no tool call is running, and the turn was not asked to stop. A running tool explains the wait itself, so
  the hint is for the wait for the model alone. Any event from the copilot takes the hint away at once, and the wait
  starts to be counted again.

The status, the timers, and the `TurnUsage` belong to the turn and end with it: when the turn has ended the three texts
are empty again and no timer is left.

**Usage.** Each `AgentEvent.Usage` is added to the `TurnUsage` of the turn: the tokens of the model calls are summed,
and the model multiplier is not, because a provider bills a message once with the multiplier of the model however many
model calls the message needed, so the largest multiplier that was reported is kept. A figure that was not reported
stays unknown and is left out. When the turn ends, in any way, `Format` writes one caption from the figures that were
reported, for example `12.4k in, 7.2k out, 400 reasoning, model x15 premium, 3:25`: the tokens sent and written (a
thousand is `k`, a million `M`), the reasoning tokens when there are any, `model x{multiplier} premium` for a multiplier
above zero or `no premium requests` for zero, the context size when one was chosen (for example `long context`, which is
billed and served separately by the provider), and then the time the turn took. A turn that the user stopped adds
`stopped, the request may still count`, because a provider counts a request when it starts the model and a stop does not
give it back. The caption goes on the last reply that the turn made (also when a tool row follows it), where the view
shows it small and quiet under the reply; a turn that made no reply gets an information notice with the caption instead.
Nothing is shown when the provider reported no figure and the turn was not stopped, so a provider that reports nothing
(or a copilot that is scripted without usage) adds no row. Providers that report only tokens, such as Ollama, get a
caption with the tokens and the time and no multiplier. The caption is not saved in the history.

**Coalescing.** A piece of text goes to a buffer. When no update was made yet, or at least `UpdateInterval` has passed
since the last one (by the `TimeProvider`), the buffer is appended to the reply at once, which creates the reply when
the turn has none. Otherwise a one-shot timer is set for the rest of the interval. When it fires, on a thread of the
timer, the append is posted to the synchronization context that the turn started on (the interface thread in the
application), or run directly when there was none (tests). The buffer, the timer, and the append are guarded by one lock
so that the turn and the timer never add the same text twice or out of order. At the end of the turn the buffer is
cleared and the timers disposed; the turn marks itself ended so that a timer that fires late does nothing.

**Stop.** `Stop` marks the turn as stopped, shows the stopping state at once and cancels its token, which is the token
given to the copilot. A provider needs a moment to end what it was doing (a model that writes a drawing does not stop at
the moment of the request), so the user must see that the Stop was heard before the stream ends: from the call of `Stop`
until the turn has ended `IsStopping` is true, `StatusText` is "Stopping..." whatever the copilot still reports, the
hint is not shown, and `StatusChanged` is raised. A second `Stop` changes nothing. The service ends the stream of a
canceled turn without an event, and a stream may also throw the cancellation: both end in `Stopped`. The runner adds the
waiting text, marks the reply that is being written as stopped (the text is kept), ends the calls that have no result as
`(stopped)`, adds a notice `The copilot was stopped.` when there was no reply to mark, shows the usage with the note
that the request may still count, and ends the turn, which empties the status. A stop that arrives after the `Completed`
event, or when no turn runs, changes nothing.

**Failure.** A `Failed` event, an exception of the stream, or a stream that ends with neither `Completed` nor `Failed`
ends the turn as `Failed`: the waiting text is added, the reply that is being written is marked as failed (the text is
kept), calls without a result end, the usage that was reported is shown, and an error `NoticeRowViewModel` is added with
the message of the event, the sentence about what to do for the kind of failure, and `CanOpenSettings` for the kinds
whose cause is in the settings of the AI provider (`NotReady`, `Authentication`, `ProtocolMismatch`, `ModelNotFound`,
`ConnectionLost`). An exception of the stream is an `Unexpected` failure whose message includes the exception's message
for the user; the log gets only the kind or the type of the exception.

#### Error Handling

Programming errors throw: null arguments (`ArgumentNullException`). Nothing that the copilot or its provider does
reaches the caller: a failure is a row and an outcome. An `OutOfMemoryException` is not caught. The log gets facts only
(the kind of a failure and the type of an exception) and never the words of a message, an answer, or a tool.

The class is owned by the interface thread. Its continuations run on the context that the turn started on; the timer of
the text and the timer of the elapsed time are the places where another thread enters, and each hands its work to that
context. The status, the text that waits, and the rows that a timer and the turn both reach are guarded by one lock, and
`StatusChanged` is raised outside the lock, so a handler may read the status.

#### Dependencies

- **AgentContract** (the `IAgentService` interface, `AgentTurnRequest`, `AgentHistoryTurn`, `AgentEvent` including its
  progress events `Thinking`, `ModelWorking`, and `ToolProgress` and its `Usage`, and `AgentFailureKind` of the Agent
  subsystem): the only part of the Agent subsystem that the chat uses; no provider, tool, or library of the Agent
  subsystem is referenced.
- **ChatPanel** (the row types `TranscriptRowViewModel`, `AssistantReplyViewModel`, `ToolActivityRowViewModel`, and
  `NoticeRowViewModel`): the rows that the runner makes and changes.
- **ComposerViewModel** (`SentMessage`): the message that is sent.
- **MessagePayload**: the payload that is given to the copilot.
- **RollingFileLogger**: facts about failures.
- **TimeProvider** (part of the .NET runtime): the clock, the timers of the text and of the elapsed time, and the time
  of the last sign of life of the copilot.

#### Callers

- The chat panel gives it the message when the composer raises `MessageSent`, blocks and unblocks the composer from
  `BusyChanged`, follows `StatusChanged` for the busy row (the phase, the elapsed time, the hint, and the stopping
  state), calls `Stop` from the Stop button, and disposes it.

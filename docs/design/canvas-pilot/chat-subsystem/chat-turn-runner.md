### ChatTurnRunner

![Chat Turn Dependencies](ChatSubsystemTurnView.svg)

#### Purpose

`ChatTurnRunner` is the logic of one turn of the conversation, without a window. It gives the message that the user sent
to the AI copilot, reads the events of the turn, keeps the rows of the transcript that show what happens (the answer
that streams, one line for each tool the copilot calls, and the notices), stops the turn on request, and never lets a
failure reach the interface. It tells the panel when it is busy, so that the panel can block the composer while the
copilot works. It exists so that the panel stays small and so that the rules of a turn, which are many (order of rows,
coalescing, stop, failure), are verified with a scripted copilot and a manual clock and no display.

#### Data Model

- `ChatTurnRunner`: `sealed class`, implements `IDisposable`. The constructor takes the `IAgentService`, the transcript
  (any `ICollection<TranscriptRowViewModel>` to which it adds rows), an optional `TimeProvider` (the system's by
  default), an optional logger, and an optional action that opens the settings, which the error notices it makes call
  from their "Open Settings" button. `IsBusy` and the `BusyChanged` event tell whether a turn runs. `UpdateInterval` is
  50 milliseconds.
- `RunTurnAsync(SentMessage, restoredHistory)` and `RunTurnAsync(AgentTurnRequest)` run a turn and return a task of
  `ChatTurnOutcome` (`Completed`, `Stopped`, `Failed`, `Refused`) that completes when the turn has ended. `Stop` stops
  the turn that runs; `Dispose` stops it and refuses new turns.
- `ToolActivityText` (internal, static): the sentences of the tool rows and the notices. It knows the names of the tools
  that CanvasPilot gives the copilot (`project_write`, `project_read`, `project_list`, `render_image`, `canvasnet_help`,
  `style_get`, `style_check`) as plain text, because the chat depends only on the contract of the Agent subsystem and
  not on the units that build the tools; a tool that is not known gets `Running {name}` and `Ran {name}`, so a new tool
  needs no change to be shown. It also gives, for each kind of failure, the sentence about what to do and whether the
  settings can help.
- Internal state of one turn (`Turn`): the synchronization context the turn started on, the cancellation source, the
  text that waits, the reply that is being written, the rows of the tool calls by call id and in order, the time of the
  last update, the timer, and whether the turn was stopped or has ended.

#### Key Methods

**Starting a turn.** `RunTurnAsync(SentMessage, history)` refuses, with an error notice, a message whose payload is not
a `MessagePayload`, and otherwise makes an `AgentTurnRequest` with the payload and the history. `RunTurnAsync(request)`
returns `Refused` at once, touching nothing, when a turn runs or the runner was disposed. Otherwise it makes the turn,
sets `IsBusy`, and raises `BusyChanged`, all before its first `await`, so that the panel can block the composer in the
same call that starts the turn; it then reads the events and, when the turn has ended (in a `finally`), releases the
turn and raises `BusyChanged` again.

**Events to rows.** `AgentEvent.TextDelta` goes to the coalescing below. Any other event first adds the text that waits,
so the rows keep their order. `ToolStarted` ends the reply that is being written (it is complete: its text is whole) and
adds a `ToolActivityRowViewModel` with the sentence for what is under way. `ToolFinished` finds the row by call id (it
makes one when the start was not seen) and finishes it with the sentence for what was done, or for what could not be
done and why. `FileWritten` finds the latest `project_write` row that has no report of a file and reports that the file
was created or replaced (the sentence is "Created" or "Replaced" and the path in code format), whichever of the file
event and the tool result comes first; when no such row exists it adds a row of its own. `Usage` is not shown.
`Completed` adds the waiting text, completes the reply, ends the calls that have no result as `(no result)`, and ends
the turn.

**Coalescing.** A piece of text goes to a buffer. When no update was made yet, or at least `UpdateInterval` has passed
since the last one (by the `TimeProvider`), the buffer is appended to the reply at once, which creates the reply when
the turn has none. Otherwise a one-shot timer is set for the rest of the interval. When it fires, on a thread of the
timer, the append is posted to the synchronization context that the turn started on (the interface thread in the
application), or run directly when there was none (tests). The buffer, the timer, and the append are guarded by one lock
so that the turn and the timer never add the same text twice or out of order. At the end of the turn the buffer is
cleared and the timer disposed; the turn marks itself ended so that a timer that fires late does nothing.

**Stop.** `Stop` marks the turn as stopped and cancels its token, which is the token given to the copilot. The service
ends the stream of a canceled turn without an event, and a stream may also throw the cancellation: both end in
`Stopped`. The runner adds the waiting text, marks the reply that is being written as stopped (the text is kept), ends
the calls that have no result as `(stopped)`, and adds a notice `The copilot was stopped.` when there was no reply to
mark. A stop that arrives after the `Completed` event, or when no turn runs, changes nothing.

**Failure.** A `Failed` event, an exception of the stream, or a stream that ends with neither `Completed` nor `Failed`
ends the turn as `Failed`: the waiting text is added, the reply that is being written is marked as failed (the text is
kept), calls without a result end, and an error `NoticeRowViewModel` is added with the message of the event, the
sentence about what to do for the kind of failure, and `CanOpenSettings` for the kinds whose cause is in the settings of
the AI provider (`NotReady`, `Authentication`, `ProtocolMismatch`, `ModelNotFound`, `ConnectionLost`). An exception of
the stream is an `Unexpected` failure whose message includes the exception's message for the user; the log gets only the
kind or the type of the exception.

#### Error Handling

Programming errors throw: null arguments (`ArgumentNullException`). Nothing that the copilot or its provider does
reaches the caller: a failure is a row and an outcome. An `OutOfMemoryException` is not caught. The log gets facts only
(the kind of a failure and the type of an exception) and never the words of a message, an answer, or a tool.

The class is owned by the interface thread. Its continuations run on the context that the turn started on; the timer is
the one place where another thread enters, and it is handed to that context.

#### Dependencies

- **AgentContract** (the `IAgentService` interface, `AgentTurnRequest`, `AgentHistoryTurn`, `AgentEvent`, and
  `AgentFailureKind` of the Agent subsystem): the only part of the Agent subsystem that the chat uses; no provider,
  tool, or library of the Agent subsystem is referenced.
- **ChatPanel** (the row types `TranscriptRowViewModel`, `AssistantReplyViewModel`, `ToolActivityRowViewModel`, and
  `NoticeRowViewModel`): the rows that the runner makes and changes.
- **ComposerViewModel** (`SentMessage`): the message that is sent.
- **MessagePayload**: the payload that is given to the copilot.
- **RollingFileLogger**: facts about failures.

#### Callers

- The chat panel gives it the message when the composer raises `MessageSent`, blocks and unblocks the composer from
  `BusyChanged`, calls `Stop` from the Stop button, and disposes it. (The panel is joined to the runner in a later step;
  in this step only the tests call it.)

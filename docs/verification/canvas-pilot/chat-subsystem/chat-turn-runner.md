### ChatTurnRunner

#### Verification Approach

`ChatTurnRunner` is verified with unit tests in
`test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatTurnRunnerTests.cs` and `ToolActivityTextTests.cs`, over the
real runner and the real rows. Two things are replaced, because they are not deterministic: the AI copilot, by
`ControlledAgentService` in `ChatTurnTestSupport.cs`, an implementation of `IAgentService` that streams the events that
the test gives it at the moment the test gives them (so a test can look at the transcript between two events), ends its
stream without an event when the turn is canceled, as the real service does, or throws the cancellation, and can fault;
and the clock, by `ManualTimeProvider`, a `TimeProvider` whose time and timers move only when the test advances them.
The synchronization context of the turn is tested with a context that runs what is posted to it at once and counts it.
The tests check what a user can see: the rows of the transcript and their state, the outcome, the busy state and its
changes, the requests that reached the copilot, and the log.

#### Test Environment

The standard test runner. No display, network, model, or process is needed, and no real time passes: the tests do not
sleep, and a wait for a turn to end is bounded by 10 seconds only as a guard against a hang.

#### Acceptance Criteria

- All tests of the unit pass with zero failures.
- A turn gives the copilot the payload of the sent message and the restored history; the runner is busy from the call
  until the turn has ended, announces each change once, refuses a second turn without asking the copilot or changing the
  transcript, refuses a message without a payload with an error notice, and is ready again afterwards.
- The text of the copilot streams into one reply, which is whole and complete when the turn completes; a tool call ends
  the reply, and the text after it is a new reply, so the rows are in the order of the events.
- The text of a reply changes at most once for every 50 milliseconds: the first piece at once, the pieces within the
  interval together after it (by the timer when the stream pauses), the waiting text before any other row and at the end
  of the turn, no timer is left, and the text that waits is added through the synchronization context of the turn.
- Each tool call is one row that says what is under way and what was done, for every tool of the application and for an
  unknown one; a failed tool says what could not be done and why; a result with a picture is marked; a written file is
  reported as created or replaced in either order of the file event and the result; a call without a result at the end
  of the turn is ended as without a result.
- Stopping keeps all the text, including text that waited, in a reply marked as stopped, whether the stream ends or
  throws the cancellation; ends the calls without a result as stopped; adds a notice when there is no reply to mark;
  frees the runner; and does nothing when idle or after completion.
- A failure that the copilot reports, an exception of the stream, and a stream that ends early each end the turn as
  failed, keep the text in a failed reply, and add an error notice with the message, a remedy for the kind, and the
  offer of the settings exactly for the kinds whose cause is in the settings; nothing is thrown to the caller; the log
  holds the kind and the type of the exception and none of the words.
- A disposed runner stops the turn that runs and refuses new turns.

#### Test Scenarios

**ChatTurnRunner_RunTurn_GivesTheCopilotThePayloadAndTheRestoredHistory**: A turn for a message with a text payload and
two restored turns reaches the copilot as one request with that very payload and those turns, and completes with an
empty transcript when the copilot only completes. This scenario is tested by
`ChatTurnRunner_RunTurn_GivesTheCopilotThePayloadAndTheRestoredHistory`.

**ChatTurnRunner_RunTurn_StreamedText_GrowsOneReplyAndCompletesIt**: Two pieces of text a full interval apart go into
one reply, which is streaming and not complete after the first, holds both after the second, and is completed when the
turn completes. This scenario is tested by `ChatTurnRunner_RunTurn_StreamedText_GrowsOneReplyAndCompletesIt`.

**ChatTurnRunner_RunTurn_ManySmallPieces_UpdateTheReplyAtMostOncePerInterval**: After a first piece that is shown at
once, nineteen pieces one millisecond apart are not shown and one timer is set; when the interval has passed the timer
adds them as one update and no timer is left; a piece after a long pause is shown at once, so the text changed twice
after its first piece. This scenario is tested by
`ChatTurnRunner_RunTurn_ManySmallPieces_UpdateTheReplyAtMostOncePerInterval`.

**ChatTurnRunner_RunTurn_TextToolText_AreThreeRowsInOrder**: Text that waits for the interval, a tool call, and more
text make a complete reply with all the text, the tool row, and a second reply, in that order. This scenario is tested
by `ChatTurnRunner_RunTurn_TextToolText_AreThreeRowsInOrder`.

**ChatTurnRunner_RunTurn_ToolCall_RowSaysWhatIsUnderWayAndThenWhatWasDone**: For each of the tools of the application
and an unknown one, the row made at the start says what is under way, with the path when there is one, and is running;
after the result it says what was done and has ended. This scenario is tested by
`ChatTurnRunner_RunTurn_ToolCall_RowSaysWhatIsUnderWayAndThenWhatWasDone`.

**ChatTurnRunner_RunTurn_ToolFailureAndPicture_AreShownOnTheRow**: A failed render says what could not be done and why,
a result with a picture is marked, and a result whose start was not seen still gets a row. This scenario is tested by
`ChatTurnRunner_RunTurn_ToolFailureAndPicture_AreShownOnTheRow`.

**ChatTurnRunner_RunTurn_FileWritten_RowSaysCreatedOrReplaced**: A write call and a file event, in both orders and for a
new and a replaced file, make one row that says the file was created or replaced, has ended, and worked. This scenario
is tested by `ChatTurnRunner_RunTurn_FileWritten_RowSaysCreatedOrReplaced`.

**ChatTurnRunner_RunTurn_FileWrittenWithoutCallAndFailedWrite_AreHandled**: A file event with no write call waiting gets
a row of its own, and a write that failed afterwards shows its refusal and not the earlier report. This scenario is
tested by `ChatTurnRunner_RunTurn_FileWrittenWithoutCallAndFailedWrite_AreHandled`.

**ChatTurnRunner_RunTurn_CompletedWithRunningTool_EndsTheToolRowWithoutResult**: A call that never returns is ended as
without a result when the turn completes. This scenario is tested by
`ChatTurnRunner_RunTurn_CompletedWithRunningTool_EndsTheToolRowWithoutResult`.

**ChatTurnRunner_RunTurn_CompletedWhileTextWaits_AddsTheTextAndLeavesNoTimer**: Text that waits for the interval is
added when the turn completes, the reply is complete, and advancing the clock afterwards changes nothing and leaves no
timer. This scenario is tested by `ChatTurnRunner_RunTurn_CompletedWhileTextWaits_AddsTheTextAndLeavesNoTimer`.

**ChatTurnRunner_RunTurn_TimerFlush_RunsOnTheContextOfTheTurn**: A turn that started on a counting context adds the
waiting text when the timer fires, and the addition was posted to that context. This scenario is tested by
`ChatTurnRunner_RunTurn_TimerFlush_RunsOnTheContextOfTheTurn`.

**ChatTurnRunner_RunTurn_IsBusyWhileRunningAndRefusesASecondTurn**: The runner is busy during a turn and announces
`true` and then `false`; a second turn is refused and reaches no copilot; a third turn after the end works. This
scenario is tested by `ChatTurnRunner_RunTurn_IsBusyWhileRunningAndRefusesASecondTurn`.

**ChatTurnRunner_RunTurn_MessageWithoutPayload_IsRefusedWithANotice**: A message with some other payload is refused with
an error notice and no request, and missing arguments are refused. This scenario is tested by
`ChatTurnRunner_RunTurn_MessageWithoutPayload_IsRefusedWithANotice`.

**ChatTurnRunner_Stop_KeepsPartialTextAndMarksTheReplyStopped**: With text shown and text that waits, a stop (with a
stream that ends quietly and with one that throws the cancellation) keeps all the text in a stopped reply, cancels the
token that the copilot got, frees the runner, and leaves no timer. This scenario is tested by
`ChatTurnRunner_Stop_KeepsPartialTextAndMarksTheReplyStopped`.

**ChatTurnRunner_Stop_WithoutTextAndWithRunningTool_AddsNoticeAndEndsTheToolRow**: A stop during a tool call with no
text ends the tool row as stopped and adds the notice that the copilot was stopped. This scenario is tested by
`ChatTurnRunner_Stop_WithoutTextAndWithRunningTool_AddsNoticeAndEndsTheToolRow`.

**ChatTurnRunner_Stop_WhenIdle_DoesNothingAndCompletedTurnStaysCompleted**: A stop with no turn, and a stop after a turn
that completed, change nothing. This scenario is tested by
`ChatTurnRunner_Stop_WhenIdle_DoesNothingAndCompletedTurnStaysCompleted`.

**ChatTurnRunner_RunTurn_Failure_KeepsPartialTextAndAddsErrorNoticeWithRemedy**: For each kind of failure the text is
kept in a failed reply, and the notice holds the message, the remedy of the kind, and the offer of the settings for the
five kinds whose cause is in the settings. This scenario is tested by
`ChatTurnRunner_RunTurn_Failure_KeepsPartialTextAndAddsErrorNoticeWithRemedy`.

**ChatTurnRunner_RunTurn_StreamThrowsOrEndsEarly_IsAFailureRowAndNeverThrows**: A stream that throws, and one that ends
with no end event, each give a failed outcome and an error notice and throw nothing. This scenario is tested by
`ChatTurnRunner_RunTurn_StreamThrowsOrEndsEarly_IsAFailureRowAndNeverThrows`.

**ChatTurnRunner_RunTurn_FailureAndException_LogFactsOnly**: A failure whose message and answer hold a sentinel word,
and an exception with the sentinel in its message, are logged as the kind and the type, and the log does not contain the
sentinel. This scenario is tested by `ChatTurnRunner_RunTurn_FailureAndException_LogFactsOnly`.

**ChatTurnRunner_Dispose_StopsTheRunningTurnAndRefusesNewOnes**: Disposing stops the turn that runs, which ends as
stopped, and a turn asked for afterwards is refused. This scenario is tested by
`ChatTurnRunner_Dispose_StopsTheRunningTurnAndRefusesNewOnes`.

**ToolActivityText_Started_FindsThePathInTheArguments**: The path is found in the short description of the arguments,
also when it holds a comma, and a missing or empty path gives the fallback of the tool. This scenario is tested by
`ToolActivityText_Started_FindsThePathInTheArguments`.

**ToolActivityText_FinishedWrittenAndAbandoned_SayWhatHappened**: The sentences of an unknown tool, of a failure with
and without a reason, of a created and a replaced file, and of a call that was stopped or has no result are as
specified. This scenario is tested by `ToolActivityText_FinishedWrittenAndAbandoned_SayWhatHappened`.

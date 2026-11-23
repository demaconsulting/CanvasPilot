### ChatTurnRunner

#### Verification Approach

`ChatTurnRunner` is verified with unit tests in
`test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/ChatTurnRunnerTests.cs` (the rows, the coalescing, stop, and
failure), `ChatTurnRunnerStatusTests.cs` (the status of a running turn and the stopping state),
`ChatTurnRunnerUsageTests.cs` (the usage and its caption, with the helper `TurnUsage`), and `ToolActivityTextTests.cs`,
over the real runner and the real rows. Two things are replaced, because they are not deterministic: the AI copilot, by
`ControlledAgentService` in `ChatTurnTestSupport.cs`, an implementation of `IAgentService` that streams the events that
the test gives it at the moment the test gives them (so a test can look at the transcript between two events), ends its
stream without an event when the turn is canceled, as the real service does, throws the cancellation, ignores the token
as a provider does that is slow to stop (`IgnoreCancellation`, until the test ends the stream), and can fault; and the
clock, by `ManualTimeProvider`, a `TimeProvider` whose time and timers move only when the test advances them, and whose
timers can repeat, so that the one-second timer of the elapsed time ticks when the test moves the clock by a second.
Because every turn has that timer besides the timer of the text, a test that counts timers counts both. The
synchronization context of the turn is tested with a context that runs what is posted to it at once and counts it. The
tests check what a user can see: the rows of the transcript and their state, the outcome, the busy state and its
changes, the status (the words, the elapsed time, the hint, the stopping state, and the changes that were announced),
the caption of the usage, the requests that reached the copilot, and the log.

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
- While a turn runs the status says what the copilot reports and nothing else: the working text at the start, then
  thinking, the size of the response, the file that is written with the bytes so far, the tool that runs, the wait for
  the model, and the answer, each as it is reported; the elapsed time starts at `0:00`, changes once for every second of
  the clock of the runner, is written as `m:ss` and as `h:mm:ss` from an hour on, and every change of the status is
  announced; after a failure, a stop, or a completion the status is empty and no timer is left.
- The progress of a file that is being written is one running row that is made by the first report (in the order of the
  events, after the text that was written before it), is updated by every later report and by the start of the call, is
  ended by its result, and is never made twice for one call; the path shows as soon as it is known.
- The hint about a long wait shows when nothing was heard from the copilot for 30 seconds and no tool runs, not a second
  earlier, is taken away by the next event, and is not shown while a tool runs or after the turn was asked to stop.
- From the moment the turn is asked to stop until it has ended the runner is busy and stopping, says "Stopping..."
  whatever the copilot still reports, shows no hint, treats a second stop as no change, announces the change, and ends
  the turn as stopped with the text kept and an empty status.
- The tokens that the provider reported for the calls of a turn are summed and the largest multiplier is kept; the
  caption has only the figures that were reported, in the written form
  (`12.4k in, 7.2k out, 400 reasoning, model x15 premium, 3:25`, `no premium requests` for a multiplier of zero), and
  the time; it is on the last reply of the turn and on no earlier one; a stopped turn says that the request may still
  count, in the reply or, without one, in an information notice; a turn without a reported figure that was not stopped
  has no caption and no row.

#### Test Scenarios

**ChatTurnRunner_RunTurn_GivesTheCopilotThePayloadAndTheRestoredHistory**: A turn for a message with a text payload and
two restored turns reaches the copilot as one request with that very payload and those turns, and completes with an
empty transcript when the copilot only completes. This scenario is tested by
`ChatTurnRunner_RunTurn_GivesTheCopilotThePayloadAndTheRestoredHistory`.

**ChatTurnRunner_RunTurn_StreamedText_GrowsOneReplyAndCompletesIt**: Two pieces of text a full interval apart go into
one reply, which is streaming and not complete after the first, holds both after the second, and is completed when the
turn completes. This scenario is tested by `ChatTurnRunner_RunTurn_StreamedText_GrowsOneReplyAndCompletesIt`.

**ChatTurnRunner_RunTurn_ManySmallPieces_UpdateTheReplyAtMostOncePerInterval**: After a first piece that is shown at
once, nineteen pieces one millisecond apart are not shown and one timer for them is set (besides the timer of the
elapsed time of the turn, so two timers are active); when the interval has passed the timer adds them as one update and
only the timer of the elapsed time is left; a piece after a long pause is shown at once, so the text changed twice after
its first piece. This scenario is tested by
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

**ChatTurnRunner_Status_Elapsed_IsTickedOnceASecondAndClearedAtTheEnd**: A turn starts with the working text, `0:00`, no
hint, and no change announced. After 900 milliseconds the elapsed time is still `0:00` and nothing was announced; after
the full second it is `0:01` and one change was announced; after 65 more seconds it is `1:06`; when the turn completes
the status, the elapsed time, and the hint are empty and no timer is active. This scenario is tested by
`ChatTurnRunner_Status_Elapsed_IsTickedOnceASecondAndClearedAtTheEnd`.

**ChatTurnRunner_FormatElapsed_WritesMinutesAndSeconds**: For 0, 59, 60, 3599, 3600, and 7384 seconds the text is
`0:00`, `0:59`, `1:00`, `59:59`, `1:00:00`, and `2:03:04`. This scenario is tested by
`ChatTurnRunner_FormatElapsed_WritesMinutesAndSeconds`.

**ChatTurnRunner_Status_FollowsWhatTheCopilotReports**: Thinking, the size of the response (2 KB), the progress of
`art/flow.svg` at 10 KB, the start of the call, its end, and a piece of text give, one after the other, "Thinking...",
"The model is writing... 2.0 KB", "Writing `art/flow.svg`... 10.0 KB", "Writing `art/flow.svg`...", "Waiting for the
model...", and "Writing the answer...", and every change was announced. This scenario is tested by
`ChatTurnRunner_Status_FollowsWhatTheCopilotReports`.

**ChatTurnRunner_Progress_MakesOneRowThatTheCallUpdatesAndFinishes**: After a piece of text the first progress of a
write call, with no path, makes one running tool row after the reply that says "Writing a file... 100 B"; a second
progress with the path `a.svg` changes the sentence of that row to the file and the size and makes no second row; the
start of the call keeps the one row, running, with the arguments of the call; its result ends the same row with "Wrote"
and the file, successful; the transcript is the reply and that one row. This scenario is tested by
`ChatTurnRunner_Progress_MakesOneRowThatTheCallUpdatesAndFinishes`.

**ChatTurnRunner_Hint_AppearsAfterALongSilenceAndIsClearedByAnEvent**: One second before the delay there is no hint; at
the delay the hint is the long-wait hint; an event takes it away. A tool that runs for twice the delay gives no hint;
after the tool has ended, a wait of the delay shows it again. This scenario is tested by
`ChatTurnRunner_Hint_AppearsAfterALongSilenceAndIsClearedByAnEvent`.

**ChatTurnRunner_Status_FailureAndStop_ClearTheStatus**: After a turn that fails, and after a turn that is stopped, the
status is empty and no timer is active. This scenario is tested by
`ChatTurnRunner_Status_FailureAndStop_ClearTheStatus`.

**ChatTurnRunner_Stop_ShowsStoppingUntilTheStreamEnds**: On a copilot that ignores the cancellation, a turn has written
some text and is stopped twice; the runner is still busy, is stopping, says "Stopping...", and announced the change.
After the copilot reports thinking and more than the delay has passed, it still says "Stopping...", shows no hint, and
is still stopping. When the stream ends the outcome is stopped, the runner is idle with an empty status, and the reply
is stopped with its text. This scenario is tested by `ChatTurnRunner_Stop_ShowsStoppingUntilTheStreamEnds`.

**TurnUsage_Format_SumsTokensKeepsTheLargestMultiplier**: Two usage reports of 8,000 and 4,400 tokens in, 1,200 and
6,000 out, and 100 and 300 reasoning, both with the multiplier 15, make the caption "12.4k in, 7.2k out, 400 reasoning,
model x15 premium, 3:25" for a turn of 205 seconds. This scenario is tested by
`TurnUsage_Format_SumsTokensKeepsTheLargestMultiplier`.

**TurnUsage_Format_LeavesOutWhatWasNotReportedAndNotesAStop**: Nothing reported gives no caption; nothing reported with
a stop gives the time and the note that the request may still count; a report with only 500 tokens out and a multiplier
of zero gives "500 out, no premium requests, 0:05". This scenario is tested by
`TurnUsage_Format_LeavesOutWhatWasNotReportedAndNotesAStop`.

**ChatTurnRunner_Completed_PutsTheUsageCaptionOnTheLastReply**: A turn has a reply, a tool call, a second reply, and two
usage reports of the multiplier 3; after 65 seconds it completes: the caption "3k in, 300 out, model x3 premium, 1:05"
is on the second reply and the first has none. This scenario is tested by
`ChatTurnRunner_Completed_PutsTheUsageCaptionOnTheLastReply`.

**ChatTurnRunner_Stopped_SaysThatTheRequestMayStillCount**: A turn that reported 900 tokens in with the multiplier 15
and wrote no reply is stopped: the outcome is stopped and an information notice says "900 in, model x15 premium, 0:00,
stopped, the request may still count". This scenario is tested by
`ChatTurnRunner_Stopped_SaysThatTheRequestMayStillCount`.

**ChatTurnRunner_CompletedWithoutUsage_HasNoCaption**: A turn that answers "Hello." and reports no usage completes with
a reply that has no caption and no notice. This scenario is tested by
`ChatTurnRunner_CompletedWithoutUsage_HasNoCaption`.

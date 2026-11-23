### AgentService

#### Verification Approach

`AgentService` is verified with unit tests in `AgentServiceTests.cs`, `AgentServicePipelineTests.cs`,
`AgentServiceStopTests.cs`, `AgentLoggingTests.cs`, `AgentEventCoalescerTests.cs`, and `RestoredHistoryDigestTests.cs`,
and with two subsystem tests in `AgentSubsystemTests.cs` (described in the verification of the `Agent` subsystem). The
tests use the real units that the service composes: the tool registry and the project tools over a real temporary
project folder, the instructions writer over the real drawing cards, the payload mapper, the event mapper, and the
readiness checks. Only the edges are replaced. A rig (`AgentServiceTestRig.cs`) makes the stand-ins for the project
service and the settings service with NSubstitute and raises their events as the real services do (the project changes
or closes, the settings change); a scripted agent factory (`AgentProviderTestDoubles.cs`) makes the agent of every
request on a scripted chat client, records the requests it got, the sessions it was asked to delete, and how often it
was disposed, can describe the session of a conversation as a provider with session identifiers does
(`DescribeSession`), and can be made to say that no agent can be made; the chat clients are a queue of scripted answers
(`ScriptedChatClient.cs`) and a client whose answer comes from a function (`ProgrammableChatClient`), which can wait
until it is released, so that a test can hold a turn open while it asks for another, cancels, resets, or disposes. The
log is a real `RollingFileLogger` on a temporary folder that the tests read back, and the rig can give the service a
manual clock.

The tests of the turn run whole turns through the real agent of the AI library, so the function calls are made and
answered by the real tools: a model that asks `project_write` shows the file event in the stream between the start and
the end of the call and the file in the project. The tests of the lifecycle count what the factory was asked for and
what it was asked to delete, and wait (with a time limit of ten seconds) for the release that runs in the background.
The tests of the data folders use real temporary folders for the project and the local data root, and compare snapshots
of the project folder before and after, so that "nothing was written" is a comparison. The test of the log puts a
different sentinel text into the message, the answer of the model, the file the model writes, and the restored history,
and an exception with a sentinel in its text into a factory, and then reads the whole log: the lifecycle lines must be
there and no sentinel may be. The adapter of the Copilot runtime is tested on a log file with sentinels in the state,
the scope, the formatted message, and the exception.

The tests of the pipeline and of the Stop use a scripted model that is made to misbehave in the three ways that matter:
it waits inside a tool call after the tool ran (`Hang`), it says nothing at all before its first update
(`HangBeforeFirstUpdate`), and it never looks at the token (`HangIgnoringCancellation`), plus one that sends a burst of
2,000 pieces of text and one that fails with a connection error. A consumer that lags takes one event and then waits
until the provider has sent everything; a consumer that stops reading disposes its enumerator; a consumer of the chat's
kind reads only up to the `Completed` event. The grace period after a Stop is tested on a manual clock: the turn is held
before the clock moves by `StopGrace` and ends after, so no test sleeps. The coalescer is tested on its own, without a
provider. The tests of the log add a sentinel to each place where words could leak and read the whole log; the
identifiers of the provider session come from the scripted factory.

No Copilot runtime, Copilot CLI, Ollama server, language model, or network is used; the Copilot branch is the subject of
the tests of the factory, and here the real starter over a start function that fails with the "not signed in" message is
used for the one scenario that shows its sentence at the service. That a real Copilot runtime behaves as the pipeline
assumes (a long turn, a Stop, a follow-up on the same provider session) is shown by the opt-in live tests in
`LiveCopilotLongTurnTests.cs`, which belong to no requirement and are recorded in the manual verification of the system.

#### Test Environment

The tests make temporary folders (the project, a second project, the local data root, and the log folder) and delete
them. No display, network, process, or external service is used. The tests that wait for the background release poll
with a limit of ten seconds.

#### Acceptance Criteria

- All unit tests pass with zero failures, repeatedly.
- Before the first turn the factory was not asked for anything, not even after a readiness check; the first turn asks
  once, with the tools of the open project (including `project_write` and `canvasnet_help`), instructions that name the
  project, and the folder of the project.
- A scripted model that talks, asks to write `art/box.svg`, and then says it is done gives the events text, call start,
  file `art/box.svg` (not replaced), call end (succeeded), text, and completed, in that order; the file has the content;
  the event of the service told the shell once; the message of the user reached the model.
- Two turns in one project make one agent, delete nothing, and the second request to the model holds the first question
  and answer before the second question.
- Opening another project drops the first conversation, deletes its session once, and the next turn makes an agent for
  the folder of the other project whose request does not contain the first project's words; closing the project does the
  same and the next turn says that a project must be opened.
- A change of the provider, the model, the Ollama address, the context window, or the path of the Copilot CLI makes the
  next turn use a new agent and deletes the old session; a change of the reasoning level or of the unrestricted tools
  setting, or a change to the same values, does not.
- While a turn is held open, five requests each give exactly one failed event of kind busy with the documented sentence;
  the held turn then completes with its whole text; the turn after it runs.
- Canceling the token of a running turn ends the stream with the text so far and with neither a failed nor a completed
  event, and the next turn runs. A reset while a turn runs, and disposing the service while a turn runs, end that turn
  the same way, delete the session once, and (for disposal) dispose the factory once; later turns after disposal give
  one failed event with the closing sentence and the later events of the project and the settings do nothing.
- A factory that has no agent gives one failed event of kind not ready with its sentence, and the next turn asks again
  and works when the cause is fixed. With no project open the failed event says to open one and nothing is made.
- The readiness check follows the settings: a server without the model gives the missing-model status, and with it gives
  ready.
- A long restored history (120 turns of 400 characters) is given once, as the first part of the first user message of
  the session, starts with the header, is at most 6,000 characters, holds the newest turn and not the oldest, and says
  that earlier turns were left out; the second turn of the session does not repeat it. A first turn that fails before
  the model answers leaves the history pending, so the next turn carries it.
- The digest of a history without words is none; a short history keeps every turn in order with the role of each, white
  space made single spaces, and blank turns dropped; for any history it is at most 6,000 characters, holds the newest
  turn, and counts the turns left out in the note; a very long turn is cut to 1,200 characters with an ellipsis.
- The data folder that the factory is given is `{local data root}\CanvasPilot\copilot` below the injected root and not
  inside the project; without an injected root it is below the local application data of the user, and so is the history
  folder.
- A local data root inside the project, a history root inside the project, and a project that contains the local data
  root each give one failed event of kind data inside project that names the folder, make no agent, and leave the
  project folder as it was. A turn that writes nothing, a reset, and the release of the conversation leave the project
  folder unchanged and create nothing in the local data root.
- `IsInside` is true for the same folder, with or without a trailing separator, and for a folder below it; true in
  another letter case on Windows and macOS, and false there on Linux; false for a sibling whose name starts with the
  same letters, for a parent, for another drive (on Windows), and for a path that climbs out with `..`; and it refuses
  empty arguments.
- The log of a whole turn holds `Conversation started`, `Turn started` with the count of earlier turns that the caller
  handed over (`historyTurns`) and the digest flag, `Turn ended` with the outcome, `Conversation released` with the
  reason, and the path of the written file, and holds none of the sentinel texts. An exception while the agent is made
  is reported to the user with its message, is logged as `Turn could not start` with the name of its type, and its text
  is not in the log.
- The logger of the Copilot runtime writes the category, the level, the event number, and the type of an exception; it
  writes nothing for the level none, never calls the formatter, and its scope is empty.
- While the consumer of a turn has taken one event, a model that sends 2,000 small pieces of text is read to its end by
  the pump; the consumer then gets all the text in the same order in fewer than half as many events, and `Completed`
  last, and the log of the turn has the count of merged events. A consumer that stops reading and disposes its
  enumerator ends the provider's stream, and the next turn is not refused as busy and completes.
- The coalescer joins adjacent text (three pieces are one event and two merges), keeps only the latest progress of the
  same call and the latest size of the response and drops a repeated thinking while keeping progress of another call
  (three merges), and keeps the facts and the text between them exactly as given and in order (no merge); a reader that
  waits is woken, gets the events in order, and ends with the stream, and an event added after the end is not read; a
  fault is thrown to the reader after the events that came before it; canceling the token ends the wait, and an event
  that waits is handed over before the reader is canceled; a missing event is refused.
- A Stop in the middle of a tool call ends the turn with the call started and finished and with neither a failed nor a
  completed event, and the next turn answers on the same conversation, with one agent made and no session deleted, and
  the log names the cancel source `User`.
- A Stop before the provider said anything gives no event, deletes the session once, and the next turn is on a new agent
  and gets the words of the earlier conversation in exactly one message; the log names the reason `stopped-early`.
- A provider that ignores the token holds the turn until the clock has moved by the grace period and not after it; the
  turn then ends with the text so far and neither a failed nor a completed event, the session is deleted, the next turn
  runs on a new agent while the old stream is still blocked, and the log says that the provider did not stop and names
  the reason `stop-timeout`.
- After a Stop, a next turn that fails with a lost connection before any update shows no failure: it answers from a new
  conversation that got the digest once, the model was asked three times in all, the old session was deleted, and the
  log has `Session recovered: turn=2 reason=ConnectionLost`. When the new conversation fails too, the failure is shown
  and nothing is sent a third time, and a failure of a conversation that nobody stopped is shown at once, after one
  request.
- A turn that completed and whose consumer stopped reading at `Completed` ends with `cancelSource=None`; the failure of
  the next turn before any update is shown (a lost connection), there is no line `Session recovered`, and the model was
  asked twice.
- A reset and a change of the project that end a running turn give neither a failed nor a completed event and the log
  names the source `Reset` or `Project`.
- The log of a whole turn on Ollama has `Turn started: turn=1 provider=Ollama model=gemma3:12b effort=high
  historyTurns=1 digestAdded=True`, `Tool started` with the name, the path `art/flow.txt`, and the bytes of the
  arguments, `Tool finished` with `outcome=ok`, the bytes of the result, `hasImage=False`, and the milliseconds, and
  `Turn ended: turn=1 outcome=Completed cancelSource=None` with `textChars`, `reasoning=False`, `usageIn=120`,
  `usageOut=30`, `firstEventMs`, and `maxGapMs`, and neither the words of the answer nor the content of the file.
- The turn numbers count up for the life of the service (1, 2, 3) and no turn after the third is numbered. A provider
  that reports reasoning gives `reasoning=True` and `usageIn=n/a usageOut=n/a` when it reports no
  usage, and none of its words are in the log.
- The log of the provider session has `Provider session: turn=1 id=sess-1 run=created handleReleased=True` for the run
  that created it and `run=resumed` for the next; the line `Provider session events` with the turn, the identifier, and
  the full path of the events file is written for the turn that failed (with `Turn failed … httpStatus=500` and
  `Turn ended: turn=3 outcome=Failed:Unexpected`) and for a canceled turn, and for no turn that completed; and the
  release of the conversation says `reason=reset session=sess-1`.
- Missing required arguments are refused, and the documented constants are the folder names that the other units rely
  on.

#### Test Scenarios

**AgentService_RunTurn_FirstTurn_StartsLazilyWithTheToolsAndInstructionsOfTheProject**: A service on a scripted model.
After a readiness check the factory has no request; after the first turn it has one with the tools, the instructions,
and the folder of the project, and the stream is the text and completed. This scenario is tested by
`AgentService_RunTurn_FirstTurn_StartsLazilyWithTheToolsAndInstructionsOfTheProject`.

**AgentService_RunTurn_ScriptedModelWritesAFile_StreamsTextCallFileAndCompletion**: A model that writes a drawing with
the real tool. The order of the events, the file, the event of the service, and the message that reached the model are
checked. This scenario is tested by `AgentService_RunTurn_ScriptedModelWritesAFile_StreamsTextCallFileAndCompletion`.

**AgentService_RunTurn_SameProjectTwice_KeepsOneAgentAndTheSession**: Two turns; one agent; the second request to the
model carries the first exchange. This scenario is tested by
`AgentService_RunTurn_SameProjectTwice_KeepsOneAgentAndTheSession`.

**AgentService_RunTurn_ProjectChanges_MakesAgentPerProjectAndDeletesTheOldSession**: A turn, the open project changes,
and a turn. This scenario is tested by
`AgentService_RunTurn_ProjectChanges_MakesAgentPerProjectAndDeletesTheOldSession`.

**AgentService_ProjectClosed_DropsTheConversationAndTheNextTurnNeedsAProject**: A turn, the project closes, and a turn.
This scenario is tested by `AgentService_ProjectClosed_DropsTheConversationAndTheNextTurnNeedsAProject`.

**AgentService_SettingsChanged_ByKind_RebuildsOnlyForTheSettingsThatDecideTheAgent**: Nine changes of the settings,
seven that rebuild (the provider, the model, the Ollama address, the context window, the Copilot path, the reasoning
effort, and the context size) and two that do not (the unrestricted-tools flag, and no change). This scenario is tested
by `AgentService_SettingsChanged_ByKind_RebuildsOnlyForTheSettingsThatDecideTheAgent`.

**AgentService_RunTurn_RequestsWhileATurnRuns_FailBusyAndTheRunningTurnCompletes**: A turn held open, five more
requests, the release, and a next turn. This scenario is tested by
`AgentService_RunTurn_RequestsWhileATurnRuns_FailBusyAndTheRunningTurnCompletes`.

**AgentService_RunTurn_TokenCancelled_EndsWithoutFailureOrCompletionAndAllowsTheNextTurn**: A turn held open and
cancelled, and a next turn. This scenario is tested by
`AgentService_RunTurn_TokenCancelled_EndsWithoutFailureOrCompletionAndAllowsTheNextTurn`.

**AgentService_ResetConversation_AfterATurn_DeletesTheSessionAndStartsAFreshAgent**: A turn, a reset, and a turn. This
scenario is tested by `AgentService_ResetConversation_AfterATurn_DeletesTheSessionAndStartsAFreshAgent`.

**AgentService_ResetConversation_WhileATurnRuns_StopsItAndDeletesTheSession**: A turn held open and a reset. This
scenario is tested by `AgentService_ResetConversation_WhileATurnRuns_StopsItAndDeletesTheSession`.

**AgentService_Dispose_WithARunningTurn_StopsItReleasesTheConversationAndDisposesTheFactoryOnce**: A turn held open,
disposal twice, events of the other services, and a turn after disposal. This scenario is tested by
`AgentService_Dispose_WithARunningTurn_StopsItReleasesTheConversationAndDisposesTheFactoryOnce`.

**AgentService_RunTurn_FactoryHasNoAgent_FailsNotReadyWithTheSentenceAndAsksAgain**: A factory with no agent, then a
factory that works. This scenario is tested by
`AgentService_RunTurn_FactoryHasNoAgent_FailsNotReadyWithTheSentenceAndAsksAgain`.

**AgentService_RunTurn_NoProjectOpen_FailsNotReady**: A turn with no project. This scenario is tested by
`AgentService_RunTurn_NoProjectOpen_FailsNotReady`.

**AgentService_GetReadiness_OllamaSettings_ReportsWhatTheProbeSees**: A server without and with the model. This scenario
is tested by `AgentService_GetReadiness_OllamaSettings_ReportsWhatTheProbeSees`.

**AgentService_RunTurn_RestoredHistory_IsPrefixedOnceAsOneBoundedBlock**: Two turns that carry a long history. This
scenario is tested by `AgentService_RunTurn_RestoredHistory_IsPrefixedOnceAsOneBoundedBlock`.

**AgentService_RunTurn_FirstTurnFailsBeforeAnswer_RestoredHistoryIsStillPending**: A model that fails before it answers,
then answers. This scenario is tested by
`AgentService_RunTurn_FirstTurnFailsBeforeAnswer_RestoredHistoryIsStillPending`.

**RestoredHistoryDigest_Build_NoWords_ReturnsNull**, **RestoredHistoryDigest_Build_ShortHistory_KeepsEveryTurnInOrder**,
**RestoredHistoryDigest_Build_LongHistory_StaysWithinTheLimitAndKeepsTheNewestTurns**, and
**RestoredHistoryDigest_Build_VeryLongTurn_IsCutWithAnEllipsis**: The digest on histories without words, short, long in
five sizes, and with one very long turn. These scenarios are tested by the tests of the same names.

**AgentService_DataResidency_CopilotDataDirectory_IsBelowTheLocalDataRootAndOutsideTheProject**: The folder that the
factory is given and the default roots. This scenario is tested by
`AgentService_DataResidency_CopilotDataDirectory_IsBelowTheLocalDataRootAndOutsideTheProject`.

**AgentService_DataResidency_DataRootInsideProject_RefusesTheTurnAndWritesNothing**: A local data root and a history
root inside the project. This scenario is tested by
`AgentService_DataResidency_DataRootInsideProject_RefusesTheTurnAndWritesNothing`.

**AgentService_DataResidency_ProjectContainsTheLocalDataRoot_RefusesTheTurn**: A project that is the parent of the local
data root. This scenario is tested by `AgentService_DataResidency_ProjectContainsTheLocalDataRoot_RefusesTheTurn`.

**AgentService_DataResidency_TurnThatWritesNothing_LeavesTheProjectFolderUnchanged**: A turn with restored history, a
reset, and a release, with a snapshot of the project folder before and after. This scenario is tested by
`AgentService_DataResidency_TurnThatWritesNothing_LeavesTheProjectFolderUnchanged`.

**AgentService_IsInside_Paths_ComparesWholeNamesWithoutCase**: Pairs of folders written as full paths of the platform
(eight on Windows, which adds a folder on another drive), where letter case counts only on Linux. This scenario is
tested by `AgentService_IsInside_Paths_ComparesWholeNamesWithoutCase`.

**AgentService_Logging_TurnWithSentinelTexts_WritesMetadataOnly**: A whole turn, a reset, and the log read back. This
scenario is tested by `AgentService_Logging_TurnWithSentinelTexts_WritesMetadataOnly`.

**AgentService_Logging_ExceptionWhileMakingTheAgent_LogsTheTypeAndNotTheMessage**: A factory that throws an exception
with a sentinel in its text. This scenario is tested by
`AgentService_Logging_ExceptionWhileMakingTheAgent_LogsTheTypeAndNotTheMessage`.

**RuntimeLogAdapter_Log_MessageStateAndScope_AreNeverWritten**: The adapter with sentinels in every place. This scenario
is tested by `RuntimeLogAdapter_Log_MessageStateAndScope_AreNeverWritten`.

**AgentService_RunTurn_SlowConsumer_TheProviderIsReadToItsEndAndTheTextIsMerged**: A model that sends 2,000 pieces of
text and a consumer that takes the first event and then waits until the provider has sent them all. The consumer reads
the rest: all the text in order, in fewer than 1,000 text events, `Completed` last, and `merged=` in the log. This
scenario is tested by `AgentService_RunTurn_SlowConsumer_TheProviderIsReadToItsEndAndTheTextIsMerged`.

**AgentService_RunTurn_ConsumerDisposed_EndsTheProviderStreamBeforeTheNextTurn**: A model that never finishes by itself
and a consumer that reads one event and disposes its enumerator. The next turn is not busy, answers, and completes, and
the model was asked twice. This scenario is tested by
`AgentService_RunTurn_ConsumerDisposed_EndsTheProviderStreamBeforeTheNextTurn`.

**AgentEventCoalescer_Add_AdjacentText_IsJoined**: Three pieces of text added in turn. One event `Hello world` and two
merges. This scenario is tested by `AgentEventCoalescer_Add_AdjacentText_IsJoined`.

**AgentEventCoalescer_Add_Progress_KeepsOnlyTheLatestOfTheSameKind**: Two `Thinking`, two `ModelWorking`, two progress
events of one call and one of another. A `Thinking`, the latest `ModelWorking`, the latest progress of the first call,
and the progress of the second call, with three merges. This scenario is tested by
`AgentEventCoalescer_Add_Progress_KeepsOnlyTheLatestOfTheSameKind`.

**AgentEventCoalescer_Add_FactsAndTextAroundThem_AreKeptInOrder**: Text, a tool start, text, a file, a tool end, two
usages, and `Completed`. Exactly what was given, in order, and no merge. This scenario is tested by
`AgentEventCoalescer_Add_FactsAndTextAroundThem_AreKeptInOrder`.

**AgentEventCoalescer_ReadAll_WaitsForEventsAndEndsWithTheStream**: A reader that starts before anything was added, two
events, the end of the stream, and an event after it. The reader gets the two events in order, ends, and does not get
the late one. This scenario is tested by `AgentEventCoalescer_ReadAll_WaitsForEventsAndEndsWithTheStream`.

**AgentEventCoalescer_Complete_WithAFault_ThrowsAfterTheEventsBeforeIt**: An event and then a fault. The reader gets the
event and then the exception. This scenario is tested by
`AgentEventCoalescer_Complete_WithAFault_ThrowsAfterTheEventsBeforeIt`.

**AgentEventCoalescer_ReadAll_TokenCancelled_EndsTheWait** and
**AgentEventCoalescer_ReadAll_TokenCancelled_EndsTheWaitAfterTheEventsThatWait**: A reader that waits and a token that
is canceled, which ends the wait, a missing event that is refused, and a reader whose token was canceled before it
began, which still gets the event that waits and is then canceled. These scenarios are tested by the tests of the same
names.

**AgentService_Stop_MidToolCall_EndsTheTurnAndTheNextTurnAnswersOnTheSameConversation**: A model that calls
`project_list` and then waits. The caller's token is canceled while it waits, and the turn is run again. The first turn
has the call started and finished and neither a failed nor a completed event, the second answers `after` and completes
on the same agent, no session was deleted, and the log names `cancelSource=User`. This scenario is tested by
`AgentService_Stop_MidToolCall_EndsTheTurnAndTheNextTurnAnswersOnTheSameConversation`.

**AgentService_Stop_BeforeTheFirstUpdate_DropsTheSessionAndTheNextTurnGetsTheDigestOnce**: A model that says nothing
until it is released, a Stop, and a next turn with a history. The stopped turn has no event, its session is deleted, the
next turn runs on a second agent, the words of the history are in exactly one message that the model got, and the log
names the reason `stopped-early`. This scenario is tested by
`AgentService_Stop_BeforeTheFirstUpdate_DropsTheSessionAndTheNextTurnGetsTheDigestOnce`.

**AgentService_Stop_ProviderIgnoresTheToken_EndsAfterTheGraceAndDropsTheConversation**: A model that says `working ` and
then waits for ever without looking at the token, on a manual clock. After the Stop the turn is still held; when the
clock moves by the grace period it ends with `working ` and neither a failed nor a completed event, the session is
deleted, the next turn answers on a second agent while the old stream is blocked, and the log says that the provider did
not stop and names the reason `stop-timeout`. This scenario is tested by
`AgentService_Stop_ProviderIgnoresTheToken_EndsAfterTheGraceAndDropsTheConversation`.

**AgentService_NextTurnAfterStop_FailsBeforeAnyUpdate_IsRecoveredOnceOnANewConversation**: A stopped turn, then a model
that fails with a closed connection, then one that answers `recovered`. The next turn shows no failure, answers
`recovered` and completes, two agents were made and the model was asked three times, the digest is in the third request,
the first session was deleted, and the log has `Session recovered: turn=2 reason=ConnectionLost`. This scenario is
tested by `AgentService_NextTurnAfterStop_FailsBeforeAnyUpdate_IsRecoveredOnceOnANewConversation`.

**AgentService_Recovery_IsOnlyOnceAndOnlyAfterAStop**: A stopped conversation whose model fails every time, and a
conversation that nobody stopped whose model fails every time. Each shows one failed event of the kind connection lost;
the stopped one was asked three times in all and the other once. This scenario is tested by
`AgentService_Recovery_IsOnlyOnceAndOnlyAfterAStop`.

**AgentService_CompletedTurnReadOnlyToItsCompletion_IsNotInterruptedAndTheNextFailureIsShown**: A model that answers
once and then loses the connection. The first turn is read only up to its `Completed` event, as the chat does, and the
next turn is run. The log has `Turn ended: turn=1 outcome=Completed cancelSource=None`, the next turn is one failed
event of the kind connection lost, there is no `Session recovered`, and the model was asked twice. The test failed
before the service counted a yielded `Completed` as drained (the log said `cancelSource=Consumer` and the failure was
recovered silently). This scenario is tested by
`AgentService_CompletedTurnReadOnlyToItsCompletion_IsNotInterruptedAndTheNextFailureIsShown`.

**AgentService_Cancel_ByAResetOrAProjectChange_LogsItsSource**: A turn that waits, ended by a reset and, in a second
run, by closing the project. Neither gives a failed or a completed event, and the log names `cancelSource=Reset` and
`cancelSource=Project`. This scenario is tested by `AgentService_Cancel_ByAResetOrAProjectChange_LogsItsSource`.

**AgentService_Logging_Turn_CarriesIdsToolFactsAndAnswerFactsWithoutContent**: A turn on Ollama with the model
`gemma3:12b`, the effort `high`, and one earlier turn, in which the model talks (a sentinel), writes `art/flow.txt` with
a sentinel as its content, and answers, with a usage of 120 and 30. The log has the lines of the criteria for the start
of the turn, the call, and the end, and neither sentinel. This scenario is tested by
`AgentService_Logging_Turn_CarriesIdsToolFactsAndAnswerFactsWithoutContent`.

**AgentService_RunTurn_CopilotContextTier_IsOnTheUsageAndInTheLog**: A scripted GitHub Copilot conversation on
`claude-opus-5.5` that reports usage, with the context size `long_context` chosen, then `default`, then none (each
change rebuilds the conversation). The `Usage` event of the three turns carries `long context`, `standard context`, and
`null`; the start line of the first turn is
`Turn started: turn=1 provider=GitHubCopilot model=claude-opus-5.5 effort=(default) contextTier=long_context` and a
later one has `contextTier=(default)`. This scenario is tested by
`AgentService_RunTurn_CopilotContextTier_IsOnTheUsageAndInTheLog`.

**AgentService_RunTurn_ContextTierOfAnotherProviderOrNoModel_IsNotOnTheUsage**: A stored size with Ollama, and a stored
size with GitHub Copilot on the default model: the `Usage` event carries no context size in either. This scenario is
tested by `AgentService_RunTurn_ContextTierOfAnotherProviderOrNoModel_IsNotOnTheUsage`.

**AgentService_Logging_TurnNumbers_CountUpForTheLifeOfTheService**: Three turns one after the other. The lines carry
`turn=1`, `turn=2`, and `turn=3`, and no `turn=4`. This scenario is tested by
`AgentService_Logging_TurnNumbers_CountUpForTheLifeOfTheService`.

**AgentService_Logging_Reasoning_IsAFlagWithoutWords**: A model that reports reasoning with a sentinel as its words and
no usage. `reasoning=True`, `usageIn=n/a usageOut=n/a`, and not the sentinel. This scenario is tested by
`AgentService_Logging_Reasoning_IsAFlagWithoutWords`.

**AgentService_Logging_ProviderSession_HasIdsRunKindsEventsFileAndReleaseReason**: A factory that gives the session an
identifier from its first run and a model that answers twice and then fails with an HTTP 500, three turns and a reset.
The lines for the session, the events file of the failed turn only, the failure with its status, and the release with
its reason and session are as in the criteria. This scenario is tested by
`AgentService_Logging_ProviderSession_HasIdsRunKindsEventsFileAndReleaseReason`.

**AgentService_Logging_CanceledTurn_NamesTheSourceAndTheEventsFile**: A turn that says something and waits, on a
provider with a session identifier, stopped by the caller. `outcome=Canceled cancelSource=User` and the line
`Provider session events` with the identifier and the file. This scenario is tested by
`AgentService_Logging_CanceledTurn_NamesTheSourceAndTheEventsFile`.

**AgentService_Constructor_MissingArguments_AreRefused**: Every required argument missing in turn. This scenario is
tested by `AgentService_Constructor_MissingArguments_AreRefused`.

**AgentSubsystem_ConverseWithOllama_ServiceOnStandInServer_WritesTheDrawingAndKeepsTheProjectClean** and
**AgentSubsystem_ConverseWithCopilot_ServiceWithNotSignedInStarter_TurnFailsWithTheSignInSentence**: The subsystem
scenarios
that are described in the verification of the Agent subsystem. These scenarios are tested by the tests of the same
names.

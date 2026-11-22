### AgentService

#### Verification Approach

`AgentService` is verified with unit tests in `AgentServiceTests.cs` and `RestoredHistoryDigestTests.cs`, and with two
subsystem tests in `AgentSubsystemTests.cs` (described in the verification of the `Agent` subsystem). The tests use the
real units that the service composes: the tool registry and the project tools over a real temporary project folder, the
instructions writer over the real drawing cards, the payload mapper, the event mapper, and the readiness checks. Only
the edges are replaced. A rig (`AgentServiceTestRig.cs`) makes the stand-ins for the project service and the settings
service with NSubstitute and raises their events as the real services do (the project changes or closes, the settings
change); a scripted agent factory (`AgentProviderTestDoubles.cs`) makes the agent of every request on a scripted chat
client, records the requests it got, the sessions it was asked to delete, and how often it was disposed, and can be made
to say that no agent can be made; the chat clients are a queue of scripted answers (`ScriptedChatClient.cs`) and a
client whose answer comes from a function (`ProgrammableChatClient`), which can wait until it is released, so that a
test can hold a turn open while it asks for another, cancels, resets, or disposes. The log is a real `RollingFileLogger`
on a temporary folder that the tests read back.

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

No Copilot runtime, Copilot CLI, Ollama server, language model, or network is used; the Copilot branch is the subject of
the tests of the factory, and here the real starter over a start function that fails with the "not signed in" message is
used for the one scenario that shows its sentence at the service.

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
- The log of a whole turn holds `Conversation started`, `Turn started` with the count of restored turns and the digest
  flag, `Turn ended` with the outcome, `Conversation released` with the reason, and the path of the written file, and
  holds none of the sentinel texts. An exception while the agent is made is reported to the user with its message, is
  logged as `Turn could not start` with the name of its type, and its text is not in the log.
- The logger of the Copilot runtime writes the category, the level, the event number, and the type of an exception; it
  writes nothing for the level none, never calls the formatter, and its scope is empty.
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

**AgentService_SettingsChanged_ByKind_RebuildsOnlyForTheSettingsThatDecideTheAgent**: Eight changes of the settings,
five that rebuild and three that do not. This scenario is tested by
`AgentService_SettingsChanged_ByKind_RebuildsOnlyForTheSettingsThatDecideTheAgent`.

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

**AgentService_Constructor_MissingArguments_AreRefused**: Every required argument missing in turn. This scenario is
tested by `AgentService_Constructor_MissingArguments_AreRefused`.

**AgentSubsystem_ConverseWithOllama_ServiceOnStandInServer_WritesTheDrawingAndKeepsTheProjectClean** and
**AgentSubsystem_ConverseWithCopilot_ServiceWithNotSignedInStarter_TurnFailsWithTheSignInSentence**: The subsystem
scenarios
that are described in the verification of the Agent subsystem. These scenarios are tested by the tests of the same
names.

### AgentProviderFactory

#### Verification Approach

`AgentProviderFactory` is verified with unit tests in `AgentProviderFactoryTests.cs` and with two subsystem tests in
`AgentSubsystemTests.cs`. The Ollama agent is verified against a stand-in for the server: an HTTP handler that answers
the chat requests from a queue of newline-delimited JSON bodies (the form in which Ollama streams) and records the
address and the body of every request. A real `OllamaApiClient`, the real context sizing client, and the real agent of
the AI library run on it, so the tests see what would go over the wire: the address, the model, the `num_ctx` of every
request, the instructions, the tool schema, and the result of the tool in the second request, and they run a whole tool
turn whose updates are mapped by the real `AgentEventMapper`. The Copilot runtime cannot be started in a test, so the
Copilot branch is verified with a fake starter that hands over a fake `ICopilotAgentSource` whose agents talk to a
scripted chat client, and with the gated starter that the build ships. The locator is the real one over the fake file
system and the fake process runner of the locator tests. One test reads the source of the unit to show that the
application calls `CopilotAgentFactory.Create` once and passes no permission handler, because the runtime that applies
the permission handler cannot be started in a test; the library's own behavior is the subject of the OTS tests.

The subsystem tests act as the application will. One runs a conversation with a stand-in Ollama server and the real
tools of a project: the factory is given the settings, the server asks for a drawing to be written, and the events show
the created file while the drawing is in the project. The other makes the Copilot agent through the gated starter and
through a fake starter, with the real tools of a project, and shows that the gate gives no agent and that the runtime
gets exactly the tools and the folder of the project.

#### Test Environment

The unit tests use no network and no process; the locator works on a fake file system and a fake process runner, and one
test reads the source files of the application. The subsystem tests make one temporary project folder each and delete
it. No Ollama server, language model, or Copilot CLI is used.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The agent for Ollama sends its requests to the address of the settings (the host, the port, and the path
  `/api/chat`), for the model of the settings, with `"stream":true`, with `"num_ctx"` of the settings in the first and
  in the second request, with the instructions and the name of the tool in the first request, and with the result of
  the tool in the second request; a turn on it gives the words, one start and one finish of the call with the same id
  and `note=hi` as the summary of the arguments, and `Completed`.
- An address with spaces around it and a model with spaces around it are trimmed.
- A server that answers 404 gives, through the mapper, a failure of the kind `ModelNotFound` whose sentence has the
  command `ollama pull gemma3:12b`.
- An address that is not an address, a blank address, a blank model, and a context window of 100 give `NotConfigured`
  with the sentences for the address, the address, the model, and the range, no agent, and no request.
- No tools is refused with an `ArgumentException`, and the factory makes agents afterwards; missing arguments and a
  blank project folder are refused.
- Disposing the result of one Ollama agent, twice, leaves the agent of another result of the same factory working.
- With the gated starter on a machine with a good CLI, a request for Copilot gives no agent and
  `CopilotRuntimeUnavailable` with "Copilot support needs an AgentKit update; use Ollama meanwhile."
- On a machine with no CLI the result is `CopilotCliMissing` with the install sentence, with a CLI of 1.0.100 it is
  `CopilotCliTooOld` that names the version, and the starter was not asked in either case.
- With a fake starter the runtime is asked once, on the real CLI of the machine with its version and not as a check;
  two agents are made on it, each with the supplied tools, the instructions, the name `CanvasPilot`, and the project
  folder, the first with no model for a blank name and the second with the model `gpt-5.4-mini` for the name with
  spaces around it; a turn on the first runs through its agent; disposing the results does not dispose the runtime,
  and disposing the factory twice disposes it once.
- A starter that fails gives `CopilotRuntimeUnavailable` with its sentence, a starter that says started without a
  source gives `NoAgentSourceMessage`, and in both cases the starter is asked again by the next request.
- Eight requests for Copilot at once ask the starter once and give eight agents; after the factory was disposed a
  request for Copilot throws `ObjectDisposedException`.
- The source of the application has one call of `CopilotAgentFactory.Create`, which passes `onPermissionRequest: null`
  and names no permission type.

#### Test Scenarios

**AgentProviderFactory_Constants_Rule_AreTheDocumentedValues**: The name of the agent and the sentences for an
address and a model. This scenario is tested by `AgentProviderFactory_Constants_Rule_AreTheDocumentedValues`.

**AgentProviderFactory_Create_BadArguments_AreRefused**: A missing request, missing parts of it, a blank project
folder, a missing agent for a result, a missing failure for a result, and a missing runtime for the source. This
scenario is tested by `AgentProviderFactory_Create_BadArguments_AreRefused`.

**AgentProviderFactory_Create_Ollama_SettingsReachTheWireAndAToolTurnRuns**: A stand-in server that asks for a tool
and then answers, an address, a model, and a window in the settings, and a whole turn on the agent. The requests and
the events are checked as in the criteria. This scenario is tested by
`AgentProviderFactory_Create_Ollama_SettingsReachTheWireAndAToolTurnRuns`.

**AgentProviderFactory_Create_Ollama_MissingModel_TurnFailsWithThePullCommand**: A server that answers 404 to the
chat. This scenario is tested by `AgentProviderFactory_Create_Ollama_MissingModel_TurnFailsWithThePullCommand`.

**AgentProviderFactory_Create_Ollama_SettingsThatCannotBeUsed_AreNotConfigured**: Four settings that cannot be used,
and a handler that records that it was never asked. This scenario is tested by
`AgentProviderFactory_Create_Ollama_SettingsThatCannotBeUsed_AreNotConfigured`.

**AgentProviderFactory_Create_Ollama_NoTools_ThrowsAndTheFactoryStillWorks**: A request with no tools, then a request
with an address and a model that have spaces around them. This scenario is tested by
`AgentProviderFactory_Create_Ollama_NoTools_ThrowsAndTheFactoryStillWorks`.

**AgentProviderFactory_Create_Ollama_DisposingAResult_LeavesOtherAgentsWorking**: Two results over one handler, the
first disposed twice, and a turn on the second. This scenario is tested by
`AgentProviderFactory_Create_Ollama_DisposingAResult_LeavesOtherAgentsWorking`.

**AgentProviderFactory_Create_Copilot_GatedStarter_ReturnsRuntimeUnavailableAndNoAgent**: The development machine
with the starter that the build ships. This scenario is tested by
`AgentProviderFactory_Create_Copilot_GatedStarter_ReturnsRuntimeUnavailableAndNoAgent`.

**AgentProviderFactory_Create_Copilot_NoCliOrOldCli_ReportsTheCliAndDoesNotStart**: A machine with nothing installed
and a machine with a CLI of 1.0.100, with a starter that records its requests. This scenario is tested by
`AgentProviderFactory_Create_Copilot_NoCliOrOldCli_ReportsTheCliAndDoesNotStart`.

**AgentProviderFactory_Create_Copilot_FakeStarter_StartsOnceAndMakesAgentsOnTheRuntime**: A fake starter and a fake
runtime whose agents talk to a scripted client, two agents, and a turn. This scenario is tested by
`AgentProviderFactory_Create_Copilot_FakeStarter_StartsOnceAndMakesAgentsOnTheRuntime`.

**AgentProviderFactory_Create_Copilot_StarterFailureOrNoSource_IsUnavailableAndNotCached**: A starter that fails and a
starter that says started without a source, each asked twice. This scenario is tested by
`AgentProviderFactory_Create_Copilot_StarterFailureOrNoSource_IsUnavailableAndNotCached`.

**AgentProviderFactory_Create_Copilot_ConcurrentRequestsAndDisposedFactory_StartOnceAndRefuse**: Eight requests at
once, the disposal of the factory, and a request after it. This scenario is tested by
`AgentProviderFactory_Create_Copilot_ConcurrentRequestsAndDisposedFactory_StartOnceAndRefuse`.

**AgentProviderFactory_CopilotSource_PermissionHandler_IsTheDefaultDenyOfTheLibrary**: The source of the subsystem is
read for the calls of `CopilotAgentFactory.Create` and for the permission handler. This scenario is tested by
`AgentProviderFactory_CopilotSource_PermissionHandler_IsTheDefaultDenyOfTheLibrary`.

**AgentSubsystem_ConverseWithOllama_ScriptedServerAndProjectTools_WritesTheDrawingAndStreamsEvents**: The subsystem
scenario that is described in the verification of the Agent subsystem. This scenario is tested by
`AgentSubsystem_ConverseWithOllama_ScriptedServerAndProjectTools_WritesTheDrawingAndStreamsEvents`.

**AgentSubsystem_ConverseWithCopilot_GatedAndFakeStarter_GateGivesNoAgentAndRuntimeGivesAnAgentWithTheProjectTools**:
The subsystem scenario that is described in the verification of the Agent subsystem. This scenario is tested by
`AgentSubsystem_ConverseWithCopilot_GatedAndFakeStarter_GateGivesNoAgentAndRuntimeGivesAnAgentWithTheProjectTools`.

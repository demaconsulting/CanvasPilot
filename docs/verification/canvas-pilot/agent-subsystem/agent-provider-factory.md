### AgentProviderFactory

#### Verification Approach

`AgentProviderFactory` is verified with unit tests in `AgentProviderFactoryTests.cs` and with two subsystem tests in
`AgentSubsystemTests.cs`. The Ollama agent is verified against a stand-in for the server: an HTTP handler that answers
the chat requests from a queue of newline-delimited JSON bodies (the form in which Ollama streams) and records the
address and the body of every request. A real `OllamaApiClient`, the real context sizing client, and the real agent of
the AI library run on it, so the tests see what would go over the wire: the address, the model, the `num_ctx` of every
request, the instructions, the tool schema, and the result of the tool in the second request, and they run a whole tool
turn whose updates are mapped by the real `AgentEventMapper`. The model settings are verified two ways: with a fake
`IOllamaModelCatalogs` (and, for Copilot, the fake catalog of the fake runtime) that holds the models of a test and can
fail, and with the real `OllamaModelCatalog` of the AI library over the same stand-in server, to show that the real
catalog's answers reach the validation and the wire. The Copilot runtime cannot be started in a test that runs
everywhere, so the Copilot branch is verified with a fake starter that hands over a fake `ICopilotAgentSource` whose
agents talk to a scripted chat client, with starters that refuse the runtime for each cause, and with the real
`CopilotRuntimeStarter` over a start function that stands in for the runtime (to show the sharing, the data folder, and
the disposal). The locator is the real one over the fake file
system and the fake process runner of the locator tests. That the copilot may call only the supplied tools (the
library's default-deny permission handling) is a behavior of the running Copilot runtime, which no test reads the source
of the application to infer; it is verified by the manual check of the live Copilot runtime (check 10 of the manual
verification in the verification of the system), and the library's own behavior is the subject of the OTS tests. A turn
on a real runtime is shown by the opt-in live tests that are recorded in the same manual check.

The subsystem tests act as the application will. One runs a conversation with a stand-in Ollama server and the real
tools of a project: the factory is given the settings, the server asks for a drawing to be written, and the events show
the created file while the drawing is in the project. The other makes the Copilot agent through a starter that refuses
the runtime and through a fake starter, with the real tools of a project, and shows that the refusal gives no agent and
that the runtime gets exactly the tools and the folder of the project.

#### Test Environment

The unit tests use no network and no process; the locator works on a fake file system and a fake process runner. The
subsystem tests make one temporary project folder each and delete it. No Ollama server, language model, or Copilot CLI
is used.

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
- A starter that refuses the runtime because the CLI is too old, Copilot is not signed in, or the start failed gives
  `CopilotCliTooOld`, `CopilotNotSignedIn`, or `CopilotRuntimeUnavailable` with the sentence of the starter and no
  agent.
- On a machine with no CLI the result is `CopilotCliMissing` with the install sentence, with a CLI of 1.0.100 it is
  `CopilotCliTooOld` that names the version, and the starter was not asked in either case.
- With a fake starter every request asks the starter with the path and the version of the real CLI of the machine; two
  agents are made on the runtime of the starter, each with the supplied tools, the instructions, the name `CanvasPilot`,
  and the project folder, the first with no model selection for a blank name and the second with the selection of the
  model `gpt-5.4-mini` for the name with spaces around it; a turn on the first runs through its agent; disposing the
  results and a factory that does not own the starter does not dispose the runtime, which belongs to the starter.
- The model and the reasoning effort of Copilot settings are validated by the catalog of the runtime: the selection that
  reaches the agent has the id of the model and the effort and no window; an effort without a model is not used; a model
  that cannot call tools, a model that the catalog does not have, an effort that the model does not offer (with the
  effort that it offers named), an effort for a model that offers none, and a model that cannot be used each give
  `ModelSettingsInvalid` with the sentence of the library and no agent; a catalog that fails gives
  `CopilotRuntimeUnavailable` with its message.
- The model, window, and effort of Ollama settings are validated by the catalog of the server: through the real catalog
  and a stand-in server, `qwen3` with the effort `high` and a window of 24,576 sends the chat request for `qwen3:latest`
  with `num_ctx` 24576 and a thinking level; a model without tool calling, a window above what a model was trained for,
  an effort that a model does not offer, and a model that the server does not have give `ModelSettingsInvalid` with the
  sentence of the library, no agent, and no chat request; a catalog that cannot be read (an unreachable server) leaves
  the typed model to be tried as it was typed.
- A factory that owns its starter stops the runtime once when it is disposed twice, and then refuses a request.
- A starter that fails gives `CopilotRuntimeUnavailable` with its sentence, a starter that says started without a
  source gives `NoAgentSourceMessage`, and in both cases the starter is asked again by the next request.
- Eight requests for Copilot at once, on the real starter over a counting start function, start the runtime once and
  give eight agents; after the factory was disposed a request for Copilot throws `ObjectDisposedException` and the
  runtime was stopped once.
- The data folder and the logger of a request reach the starter of the runtime unchanged, and a start request has none
  by default.
- Deleting a session reaches the started runtime only for a Copilot session that has an id (`session-42` here); an
  Ollama session, a Copilot session without an id, a Copilot session on a factory whose runtime was never started, and
  the call with a missing session each delete nothing, and the missing session is refused.

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

**AgentProviderFactory_Create_Copilot_StarterRefuses_ReturnsTheMatchingStatusAndNoAgent**: The development machine and a
starter that answers CLI too old, not signed in, or failed in turn, with the sentence "The sentence of the starter."
Each gives the matching status, that sentence, and no agent. This scenario is tested by
`AgentProviderFactory_Create_Copilot_StarterRefuses_ReturnsTheMatchingStatusAndNoAgent`.

**AgentProviderFactory_Create_Copilot_ModelAndEffort_ReachTheAgentAsTheSelection**: A runtime whose catalog offers
`claude-haiku-5.5` with the efforts `low`, `medium`, and `xhigh`. Settings with that model and `xhigh` make an agent
whose request carries a selection with that model, that effort, and no window; settings with an effort and no model make
an agent with no selection. This scenario is tested by
`AgentProviderFactory_Create_Copilot_ModelAndEffort_ReachTheAgentAsTheSelection`.

**AgentProviderFactory_Create_Copilot_ModelSettingsTheCatalogRefuses_AreModelSettingsInvalid**: Five settings against a
catalog of four models: a model without tool calling, a model that the catalog does not have, an effort `bogus` (the
sentence names `low, medium`), an effort for a model that offers none, and a model that cannot be used. Each gives
`ModelSettingsInvalid`, the matching part of the sentence of the library, and no agent request. This scenario is tested
by `AgentProviderFactory_Create_Copilot_ModelSettingsTheCatalogRefuses_AreModelSettingsInvalid`.

**AgentProviderFactory_Create_Copilot_CatalogCannotBeRead_IsRuntimeUnavailable**: A catalog that fails with the message
of a runtime that is not signed in gives `CopilotRuntimeUnavailable` with that message. This scenario is tested by
`AgentProviderFactory_Create_Copilot_CatalogCannotBeRead_IsRuntimeUnavailable`.

**AgentProviderFactory_Create_Ollama_ModelEffortAndWindow_ReachTheWire**: The real catalog of the AI library over a
stand-in server whose `/api/tags` lists `qwen3:latest` and whose `/api/show` says that it can complete, call tools, and
think and was trained for 40,960 tokens. Settings with the model `qwen3`, the effort `high`, and a window of 24,576 make
an agent; a turn sends one chat request that names `qwen3:latest`, `num_ctx` 24576, and a `think` setting. This scenario
is tested by `AgentProviderFactory_Create_Ollama_ModelEffortAndWindow_ReachTheWire`.

**AgentProviderFactory_Create_Ollama_ModelSettingsTheCatalogRefuses_AreModelSettingsInvalid**: A fake catalog with an
ordinary model, a model without tool calling, and a model trained for 8,192 tokens. A model without tools ("does not
support tool calling"), a window of 24,576 for the small model ("supports at most 8,192 tokens; a 24,576-token window is
not supported"), the effort `high` for a model that offers none, and a model that the catalog does not have each give
`ModelSettingsInvalid` and the sentence, and the server was never asked a chat request. This scenario is tested by
`AgentProviderFactory_Create_Ollama_ModelSettingsTheCatalogRefuses_AreModelSettingsInvalid`.

**AgentProviderFactory_Create_Ollama_CatalogUnreachable_UsesTheTypedModelWithoutACheck**: A catalog that fails like a
server that does not answer. The agent is made and a turn runs with the typed model name on the wire. This scenario is
tested by `AgentProviderFactory_Create_Ollama_CatalogUnreachable_UsesTheTypedModelWithoutACheck`.

**AgentProviderFactory_Create_Copilot_NoCliOrOldCli_ReportsTheCliAndDoesNotStart**: A machine with nothing installed
and a machine with a CLI of 1.0.100, with a starter that records its requests. This scenario is tested by
`AgentProviderFactory_Create_Copilot_NoCliOrOldCli_ReportsTheCliAndDoesNotStart`.

**AgentProviderFactory_Create_Copilot_FakeStarter_MakesAgentsOnTheRuntimeThatBelongsToTheStarter**: A fake starter and a
fake runtime whose agents talk to a scripted client, two agents, and a turn; the factory does not own the starter. This
scenario is tested by
`AgentProviderFactory_Create_Copilot_FakeStarter_MakesAgentsOnTheRuntimeThatBelongsToTheStarter`.

**AgentProviderFactory_DisposeAsync_OwnedStarter_StopsTheRuntimeOnce**: The real starter over a start function that
gives a fake runtime, a factory that owns the starter, and one agent made. The factory is disposed twice; the runtime
was stopped once and a further request is refused. This scenario is tested by
`AgentProviderFactory_DisposeAsync_OwnedStarter_StopsTheRuntimeOnce`.

**AgentProviderFactory_Create_Copilot_StarterFailureOrNoSource_IsUnavailableAndNotCached**: A starter that fails and a
starter that says started without a source, each asked twice. This scenario is tested by
`AgentProviderFactory_Create_Copilot_StarterFailureOrNoSource_IsUnavailableAndNotCached`.

**AgentProviderFactory_Create_Copilot_ConcurrentRequestsAndDisposedFactory_StartOnceAndRefuse**: Eight requests at once
on the real starter over a counting start function, the disposal of the owning factory, and a request after it. This
scenario is tested by `AgentProviderFactory_Create_Copilot_ConcurrentRequestsAndDisposedFactory_StartOnceAndRefuse`.

**AgentProviderFactory_Create_Copilot_DataDirectoryAndLogger_ReachTheStarter**: A request that names a data folder and a
logger, and a fake starter that records its request. This scenario is tested by
`AgentProviderFactory_Create_Copilot_DataDirectoryAndLogger_ReachTheStarter`.

**AgentProviderFactory_DeleteSession_CopilotSessionOnly_DeletesThroughTheRuntime**: Sessions of both providers, one
without an id, and a factory that never started a runtime; the Copilot session is made by reflection, because the AI
library makes it only while a conversation runs. This scenario is tested by
`AgentProviderFactory_DeleteSession_CopilotSessionOnly_DeletesThroughTheRuntime`.

**AgentSubsystem_ConverseWithOllama_ScriptedServerAndProjectTools_WritesTheDrawingAndStreamsEvents**: The subsystem
scenario that is described in the verification of the Agent subsystem. This scenario is tested by
`AgentSubsystem_ConverseWithOllama_ScriptedServerAndProjectTools_WritesTheDrawingAndStreamsEvents`.

**AgentSubsystem_ConverseWithCopilot_RefusingAndRunningStarter_RefusalGivesNoAgentAndRuntimeGivesAnAgentWithTheProjectTools**:
The subsystem scenario that is described in the verification of the Agent subsystem. This scenario is tested by
`AgentSubsystem_ConverseWithCopilot_RefusingAndRunningStarter_RefusalGivesNoAgentAndRuntimeGivesAnAgentWithTheProjectTools`.

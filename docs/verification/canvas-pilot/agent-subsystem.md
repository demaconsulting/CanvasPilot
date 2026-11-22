## Agent

### Verification Approach

The `Agent` subsystem is verified by integration tests defined in `AgentSubsystemTests.cs`. The subsystem is built
contract first, so the tests verify that the contract of its `AgentContract` unit can be used as the chat will use it: a
hand-written scripted implementation of `IAgentService` streams a fixed list of events and returns fixed readiness
reports, and the test runs a turn through the interface, collects the events, and checks their order, the ending, and
what the service received with the request. The scripted service is the only substitute and stands in for an AI
provider; no real provider, model, network connection, or Copilot runtime is used. The Copilot CLI locator is verified
at this level with the file system, environment, and process runner replaced by the layout of a real machine (the paths
and sizes of the development machine, which has both the installed CLI and the VS Code shim). The rules of the records
and of the locator themselves are verified in the unit verification documents.

The units that implement the contract add their own verification documents and tests as they are built; their scenarios
are not part of this document until then.

### Test Environment

N/A - standard test environment. The tests use no files, network, display, or external services.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A turn run through the interface delivers the events in the order the service produced them, ends with the completed
  event, and the service sees the payload and the restored history of the request unchanged.
- A refused turn is a single failed event that names its kind and carries a message for the user.
- Readiness reports pass through the interface unchanged, and a reset of the conversation reaches the service.
- On a machine with the installed Copilot CLI and the VS Code shim the CLI is found and the shim is not; a machine with
  only the shim is told that only the shim was found; a path chosen in the settings is used.

### Test Scenarios

**AgentSubsystem_RunTurn_ScriptedService_StreamsEventsInOrderAndReceivesTheRequest**: A scripted service answers with
two text pieces, a tool call that starts and finishes, a written file, a usage count, and the completed event. A turn is
run through `IAgentService` with a request that carries a payload and two restored history turns. Seven events arrive in
order, the text pieces join to the whole answer, the file event names the file, the last event is the completed event,
and the service received the same payload and the same history. This scenario is tested by
`AgentSubsystem_RunTurn_ScriptedService_StreamsEventsInOrderAndReceivesTheRequest`.

**AgentSubsystem_RunTurn_ScriptedBusyService_EndsWithFailedEventOfKindBusy**: A scripted service refuses the turn with a
failed event of kind busy. The turn yields that single event, with the kind and a message that tells the user to press
Stop. This scenario is tested by `AgentSubsystem_RunTurn_ScriptedBusyService_EndsWithFailedEventOfKindBusy`.

**AgentSubsystem_Readiness_ScriptedReports_SeparateUsableFromBlockedProviders**: A scripted service returns, in turn, a
ready report, a context-too-small report, and a runtime-unavailable report. Each arrives unchanged, the first two are
usable and the third is not, and a reset of the conversation reaches the service once. This scenario is tested by
`AgentSubsystem_Readiness_ScriptedReports_SeparateUsableFromBlockedProviders`.

**AgentSubsystem_LocateCopilotCli_MachinesWithShim_FindsRealCliAndExplainsShimOnly**: The file system, environment, and
process runner are replaced by the layout of the development machine, which has the winget Copilot CLI (196,113,184
bytes), a link to it, and the VS Code shim (791,904 bytes) on the `PATH`. The locator finds the CLI of the winget
package folder and not the shim. On a machine whose `PATH` holds only the shim, the result is not found and the message
says that only the VS Code copilot shim was found; with the path of the real CLI chosen in the settings on that machine,
the CLI is found. This scenario is tested by
`AgentSubsystem_LocateCopilotCli_MachinesWithShim_FindsRealCliAndExplainsShimOnly`.

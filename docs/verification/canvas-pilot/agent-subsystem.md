## Agent

### Verification Approach

The `Agent` subsystem is verified by integration tests defined in `AgentSubsystemTests.cs`. The subsystem is built
contract first, so the tests verify that the contract of its `AgentContract` unit can be used as the chat will use it: a
hand-written scripted implementation of `IAgentService` streams a fixed list of events and returns fixed readiness
reports, and the test runs a turn through the interface, collects the events, and checks their order, the ending, and
what the service received with the request. The scripted service is the only substitute and stands in for an AI
provider; no real provider, model, network connection, or Copilot runtime is used. The Copilot CLI locator is verified
at this level with the file system, environment, and process runner replaced by the layout of a real machine (the paths
and sizes of the development machine, which has both the installed CLI and the VS Code shim). The readiness checks are
verified at this level through `IProviderCheck`, the interface that the settings dialog uses, with the same replaced
machine, a scripted starter, and a scripted Ollama server. The rules of the records, of the locator, and of the
readiness checks themselves are verified in the unit verification documents.

The units that implement the contract add their own verification documents and tests as they are built; their scenarios
are not part of this document until then.

### Test Environment

N/A - standard test environment. The tests use no network, display, or external services. The tests of the tools make a
temporary project folder and delete it.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A turn run through the interface delivers the events in the order the service produced them, ends with the completed
  event, and the service sees the payload and the restored history of the request unchanged.
- A refused turn is a single failed event that names its kind and carries a message for the user.
- Readiness reports pass through the interface unchanged, and a reset of the conversation reaches the service.
- On a machine with the installed Copilot CLI and the VS Code shim the CLI is found and the shim is not; a machine with
  only the shim is told that only the shim was found; a path chosen in the settings is used.
- Through `IProviderCheck`, GitHub Copilot on a machine with a signed-in GitHub CLI is not usable and says that an
  update of the AI library is needed, and Ollama with a window of 8,192 tokens is usable and warns about it.
- The tools built for a project are only the application's own tools, in a fixed order, the knowledge tool answers from
  the real cards, and the policy that every pack receives permits a picture file of the project and refuses the data
  folder, a project file, and every place outside the project.
- The project tools, supplied to the registry, write a drawing into a real project, list and read it, replace it
  directly, and draw it with the real render service; the shell is told of both writes; nothing but the drawing is added
  to the project; and a path that climbs out of the project, the project file, and a secret beside the project are
  refused.
- The instructions and the tools of a project written together agree: the large window gets the full tier and the small
  window the compact tier, which is less than half the size and has no example images, both have the project facts, and
  the tool names that the role prompt gives are exactly the names of the tools that the registry offers.

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

**AgentSubsystem_ConfineTools_ProjectFolder_OffersOnlyApplicationToolsUnderTheConfiningPolicy**: A registry is built
over the real drawing knowledge, a stand-in style tool, and a supplied pack that keeps the policy it is given. For a
project folder the tools are `canvasnet_help`, `style_get`, and the pack's tool, in that order; calling the knowledge
tool answers with the index of the real cards; and the policy that the pack received permits writing a picture file in
the project and refuses the data folder, a project file, and a path that climbs out of the project. This scenario is
tested by `AgentSubsystem_ConfineTools_ProjectFolder_OffersOnlyApplicationToolsUnderTheConfiningPolicy`.

**AgentSubsystem_ProjectTools_ProjectFolder_WriteListReadAndDrawStayInsideTheProject**: A registry over the real drawing
knowledge is supplied by the project tools, which use the real render service, for a real project folder with a project
file and a secret beside it. A drawing is written into a new folder (the answer says created), the project is listed,
the drawing is read back exactly, it is replaced (the answer says replaced), and it is drawn into a picture of 40 by 20
pixels. Then a write above the project folder, a write over the project file, and a read of the secret by its full path
are tried, and none of them has an effect. The shell was told of the two writes with the relative path and the new flag,
the secret and the project file are as they were, and the project holds only the project file and the folder with the
drawing. This scenario is tested by
`AgentSubsystem_ProjectTools_ProjectFolder_WriteListReadAndDrawStayInsideTheProject`.

**AgentSubsystem_InstructTheCopilot_ProjectWithKit_InstructionsNameExactlyTheToolsThatAreOffered**: For a project with
the kit `clean` and two files, the instructions are written for an Ollama window of 32,768 tokens (full tier) and of
8,192 tokens (compact tier) over the real cards, and the registry builds the tools of the project with the real project
tools. The compact text is less than half the full text, both have the name of the project and its files, only the full
text has the example images of the kit, and the tool names in the role prompt are exactly the tools that the registry
offers. This scenario is tested by
`AgentSubsystem_InstructTheCopilot_ProjectWithKit_InstructionsNameExactlyTheToolsThatAreOffered`.

**AgentSubsystem_CheckProviderReadiness_BothProviders_ReportThroughTheSettingsInterface**: The development machine has a
good Copilot CLI and a GitHub CLI under the program files that prints a token, and a scripted Ollama server has one
model with a large window. Asked through `IProviderCheck`, GitHub Copilot is not usable and the sentence says that
Copilot support needs an AgentKit update; Ollama with a configured window of 8,192 tokens is usable and the sentence
names the window. This scenario is tested by
`AgentSubsystem_CheckProviderReadiness_BothProviders_ReportThroughTheSettingsInterface`.

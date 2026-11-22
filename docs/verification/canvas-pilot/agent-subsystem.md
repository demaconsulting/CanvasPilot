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
readiness checks themselves are verified in the unit verification documents. The starter of the Copilot runtime
(`CopilotRuntimeStarter`) is verified in its own unit verification document over a start function that stands in for the
runtime, so no Copilot CLI is run; the tests at this level use it or scripted starters.

The service that implements the contract (`AgentService`) is also verified here as the application will use it: one test
runs it over the real factory and a stand-in Ollama server with the real tools of a project, and one runs it for GitHub
Copilot with the real starter over a start function that says the runtime is not signed in. Its lifecycle, its turn, its
data folders, and its log are verified in the unit verification document of `AgentService`. A turn of a real Copilot
runtime cannot be part of the tests that run everywhere, because it needs the installed CLI and a sign-in; it is
verified by two opt-in tests (`LiveCopilotTurnTests.cs`, skipped unless the variable `CANVASPILOT_LIVE_COPILOT` is `1`)
whose runs are recorded in the manual verification of the system.

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
- Through `IProviderCheck`, GitHub Copilot on a machine with a signed-in GitHub CLI and a runtime that starts is usable
  and says "GitHub Copilot CLI 1.0.114 is ready.", and Ollama with a window of 8,192 tokens is usable and warns about
  it.
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
- A message with marks, built by the real payload builder from a real picture, is mapped to a user message whose text
  has the picture by its path in the project and the numbers of the marks, with no full path of the computer, and whose
  images are three real PNG files in the stated order: the annotated picture and one close-up for each mark.
- A whole turn of a real agent on a scripted model, with the real project tools, gives the events that the chat will
  get: the text of both answers, three calls tied by id with their summaries (the drawing sent to `project_write` shown
  by its length, the answer that the file was created), the picture flag of the look at the drawing, the refused write
  outside the project as a call that did not succeed, and `Completed` last; the drawing is in the project and nothing is
  outside it.
- A conversation with a stand-in Ollama server, from the settings to the project, works: the factory makes the agent on
  the address, the model, and the context window of the settings (each request carries them), the server asks for a
  drawing to be written with the real tools, the events show the created file, the drawing is in the project, and
  nothing else is.
- The Copilot agent is made only on the runtime that the starter hands over: a starter that refuses the runtime gives no
  agent and the matching status, and a starter that runs a runtime gives an agent that is made on it with exactly the
  names of the tools of the project, the project folder as its working directory, and no model for the default.
- Through the agent service over the real factory and a stand-in Ollama server, a message gets the streamed answer, the
  call of `project_write`, a file event for the new drawing, and `Completed`; the shell is told of the file; the server
  gets the model and the window of the settings; and the project folder holds only its earlier files and the drawing.
- Through the agent service with the real starter over a start function that says the runtime is not signed in, a turn
  for GitHub Copilot is one failed event of kind not ready with the sign-in sentence of the starter, and the log holds
  no word of the message.

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

**AgentSubsystem_SendMessage_RealPayloadWithMarks_MapsToTextAndPngImagesInOrder**: A project has a picture
`art/logo.svg`. A rectangle and an arrow are marked on it, the message is composed from the two chips, and the real
payload builder makes the payload with the real composer of mark pictures. Mapped for the project, the message is of the
role user; its text starts with the two references, names `art/logo.svg` with its format and size, describes the
rectangle with its box and the arrow, and says that an SVG gives no element positions; it holds none of the folder of
the project. The contents after the text are three PNG files named `picture-1.png`, `mark-1.png`, and `mark-2.png`, each
starting with the PNG signature. This scenario is tested by
`AgentSubsystem_SendMessage_RealPayloadWithMarks_MapsToTextAndPngImagesInOrder`.

**AgentSubsystem_RunTurn_ScriptedModelWritesAndLooksAtADrawing_EventsShowTheCreatedFileThePictureAndTheRefusal**: A
project folder and the real tools of the registry with the real project tools. A scripted model says a sentence and asks
for three calls at once: `project_write` of `art/box.svg`, `render_image` of it, and `project_write` of
`../planted.txt`; it then answers with a second sentence. The events have the two sentences in order, the three starts
with the summary `path=art/box.svg, content=(n characters)` for the first, the answer `File 'art/box.svg' created` for
the first finish, a finish with a picture for the look, a finish that did not succeed for the write outside the project,
and `Completed` last. The drawing is in the project and the file outside it was not written. This scenario is tested by
`AgentSubsystem_RunTurn_ScriptedModelWritesAndLooksAtADrawing_EventsShowTheCreatedFileThePictureAndTheRefusal`.

**AgentSubsystem_ConverseWithOllama_ScriptedServerAndProjectTools_WritesTheDrawingAndStreamsEvents**: Settings of Ollama
(address, model `gemma3:12b`, window 32,768), the real tools of an empty project, and a stand-in server that streams a
sentence and a call of `project_write` for `box.svg` and then a second sentence. The factory makes the agent, a turn
runs through the mapper, and the events have the two sentences, one finish of `project_write` that succeeded and says
`File 'box.svg' created`, and `Completed`. The drawing is in the project, it is the only entry in the project folder,
and every request that the server got names the model and the window. This scenario is tested by
`AgentSubsystem_ConverseWithOllama_ScriptedServerAndProjectTools_WritesTheDrawingAndStreamsEvents`.

**AgentSubsystem_ConverseWithCopilot_RefusingAndRunningStarter_RefusalGivesNoAgentAndRuntimeGivesAnAgentWithTheProjectTools**:
The development machine with the real CLI and the tools of an empty project. The factory with a starter that says the
runtime is not signed in gives no agent and the status not signed in. The factory with a fake starter that hands over a
fake runtime gives an agent, and the runtime was asked for one agent with the same tool names as the registry, the
project folder as the working directory, and no model. This scenario is tested by
`AgentSubsystem_ConverseWithCopilot_RefusingAndRunningStarter_RefusalGivesNoAgentAndRuntimeGivesAnAgentWithTheProjectTools`.

**AgentSubsystem_CheckProviderReadiness_BothProviders_ReportThroughTheSettingsInterface**: The development machine has a
good Copilot CLI and a GitHub CLI under the program files that prints a token, a starter that starts the runtime, and a
scripted Ollama server has one model with a large window. Asked through `IProviderCheck`, GitHub Copilot is usable with
the sentence "GitHub Copilot CLI 1.0.114 is ready." and the starter was asked once with the path of the CLI that the
locator found; Ollama with a configured window of 8,192 tokens is usable and the sentence names the window. This
scenario is tested by `AgentSubsystem_CheckProviderReadiness_BothProviders_ReportThroughTheSettingsInterface`.

**AgentSubsystem_ConverseWithOllama_ServiceOnStandInServer_WritesTheDrawingAndKeepsTheProjectClean**: An agent service
on the real factory and a stand-in Ollama server that asks for `project_write` and then answers, a project folder with
only its project file, settings for a model with a window of 32,768 tokens, and a handler for the event of the service.
A turn is run through the service. The events are the two pieces of the answer, one call that succeeded, one file event
for `box.svg`, and the completed event; the shell was told of `box.svg`; the drawing is in the project and is the only
file added; and both requests to the server carry the model and the window. This scenario is tested by
`AgentSubsystem_ConverseWithOllama_ServiceOnStandInServer_WritesTheDrawingAndKeepsTheProjectClean`.

**AgentSubsystem_ConverseWithCopilot_ServiceWithNotSignedInStarter_TurnFailsWithTheSignInSentence**: The development
machine with a good Copilot CLI, the real factory with the real `CopilotRuntimeStarter` over a start function that fails
with the library's "not signed in" message, and settings that choose GitHub Copilot. A turn is run through the service.
The stream is one failed event of kind not ready with the sign-in sentence of the starter, and the log holds no word of
the message. This scenario is tested by
`AgentSubsystem_ConverseWithCopilot_ServiceWithNotSignedInStarter_TurnFailsWithTheSignInSentence`.

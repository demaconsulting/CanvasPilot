## OllamaSharp Verification

This document provides the verification evidence for the OllamaSharp OTS software item. Requirements for this OTS item
are defined in the OllamaSharp OTS Software Requirements document.

### Required Functionality

OllamaSharp lists the models of the server by their tags, reports an unreachable server and a missing model in ways that
can be told apart, streams the text and the tool calls of an answer, and sends the context length as `num_ctx`. The
readiness check and the Ollama provider rely on each of these behaviors, so each has a requirement and a test.

### Verification Approach

The library has no self-validation mode. It is verified by tests in `test/OtsSoftwareTests/OllamaSharpTests.cs` that
give its client an HTTP handler that stands in for the server and answers from the test, so that no Ollama installation,
no model, and no network are needed. The answers are in the format that the Ollama documentation describes; how a real
server answers, in particular for the maximum context of a model, is not covered and is checked by hand where Ollama is
installed, as listed in the manual checks of the system verification. The tests are run again before a version upgrade
is accepted.

### Test Scenarios

#### OllamaSharp_ListLocalModels_AsksForTagsAndReturnsModelNames

**Scenario**: The models are listed from a stand-in server that has two models.

**Expected**: The request is a GET of `/api/tags`, and both model names come back in order.

**Requirement coverage**: `CanvasPilot-OTS-OllamaSharp-ListLocalModels`.

#### OllamaSharp_ListLocalModels_ServerNotReachable_ThrowsHttpRequestException

**Scenario**: The models are listed from a stand-in that refuses the connection.

**Expected**: The call fails with an HTTP request exception.

**Requirement coverage**: `CanvasPilot-OTS-OllamaSharp-ListLocalModels`.

#### OllamaSharp_ChatClient_UnknownModel_FailsWithNotFoundStatus

**Scenario**: A streamed chat is run against a stand-in that answers with the error of a model that is not installed.

**Expected**: The call fails with an HTTP request exception whose status code is "not found", and the request named the
model.

**Requirement coverage**: `CanvasPilot-OTS-OllamaSharp-ReportMissingModel`.

#### OllamaSharp_ChatClient_StreamingResponse_ReturnsTextAndToolCall

**Scenario**: A streamed chat with one tool offered is run against a stand-in that streams a piece of text, a tool call,
and the end of the answer.

**Expected**: The text piece and the call, with its name and its argument by name, come back; the request asked for a
stream and named the tool.

**Requirement coverage**: `CanvasPilot-OTS-OllamaSharp-StreamTextAndToolCalls`.

#### OllamaSharp_ChatClient_WithContextSizing_SendsNumCtxOption

**Scenario**: A response is requested through AgentKit's sizing client for 32768 tokens over the Ollama client, against
a stand-in that answers with one text.

**Expected**: The answer comes back, and the body of the request holds `"num_ctx":32768`.

**Requirement coverage**: `CanvasPilot-OTS-OllamaSharp-SendContextLength`.

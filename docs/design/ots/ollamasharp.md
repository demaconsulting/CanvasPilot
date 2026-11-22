## OllamaSharp

This document describes the integration and usage design for the `OllamaSharp` OTS software item.

### Purpose

OllamaSharp is the .NET client of the Ollama server, which runs open language models on the user's own computer. It is
CanvasPilot's way to the Ollama provider: its client is the chat client that AgentKit wraps, and CanvasPilot's readiness
check uses it to see whether the server runs and which models it has. It was chosen because it is the client that
AgentKit's Ollama package is built on and it implements the common chat client type. It is a third-party library from an
unrelated program.

### Features Used

- `OllamaApiClient`, created from an HTTP client with the server's address and a model name, which is an `IChatClient`
  whose streamed responses carry text pieces and tool calls.
- `ListLocalModelsAsync`, which asks the server for its tags and returns the installed models, and which fails with an
  HTTP request exception when the server cannot be reached.
- The failure of a chat with a model that is not installed, which is an HTTP request exception with the status code "not
  found". The message of the server is not part of the exception, so the application names the model itself and tells
  the user to pull it.
- The Ollama option `num_ctx`, which the context sizing client of AgentKit puts on every request and which OllamaSharp
  sends in the options of the chat request.

### Integration Pattern

The package is a transitive reference of the application library through AgentKit. The readiness check and the provider
factory of the Agent subsystem create the client from the settings (endpoint and model), through a seam that the tests
replace, so that no unit test needs a server. The OTS tests in `test/OtsSoftwareTests/OllamaSharpTests.cs` give the
client an HTTP handler that stands in for the server, and check the tag listing, the unreachable server, the missing
model, a streamed answer with a tool call, and the context length on the wire. They need no Ollama installation and no
model. How a real model behaves, and the shape of the server's answer for the maximum context of a model, are checked by
hand where Ollama is installed (see the manual checks in the system verification).

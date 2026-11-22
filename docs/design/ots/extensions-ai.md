## Microsoft.Extensions.AI

This document describes the integration and usage design for the `Microsoft.Extensions.AI` OTS software item: the
`Microsoft.Extensions.AI.Abstractions` package and the function-invocation pipeline of `Microsoft.Extensions.AI`.

### Purpose

Microsoft.Extensions.AI defines the common types for working with language models in .NET: chat messages, content
(text, data, function calls and results), chat clients, and functions that a model can call. CanvasPilot's tools, the
messages it builds from the user's text, marks, and pictures, and both providers are expressed in these types. It was
chosen because the Agent Framework, AgentKit, and OllamaSharp all use it. It is a third-party library from an unrelated
program.

### Features Used

- `AIFunction`, made from a delegate by `AIFunctionFactory` or written as a class of its own, with the JSON schema of
  its parameters and the binding of named arguments from JSON values; CanvasPilot's existing tool descriptors are
  carried by a function class of their own.
- `FunctionInvokingChatClient`, the loop that runs the functions a model calls and gives the results back to the model.
- `DataContent` for pictures, with its bytes, media type, top-level media type, and data address.
- `ChatMessage`, `ChatResponseUpdate`, and `ToChatResponse`, which joins streamed updates into one response.
- `IChatClient`, which the Ollama client implements and which AgentKit's decorators wrap.

### Integration Pattern

The packages are transitive references of the application library through AgentKit and the Agent Framework, and the
Agent subsystem uses the types directly where it builds messages and tools. The OTS tests in
`test/OtsSoftwareTests/ExtensionsAiTests.cs` use the types the way the subsystem does, without a language model: a
function bound from JSON values, a function class run by the invocation loop with a scripted client, picture content,
and the joining of updates. A library upgrade re-runs them before it is accepted. The package also brings
`Microsoft.Extensions.AI.Evaluation`, `Microsoft.Extensions.Logging.Abstractions`, and the dependency injection
abstractions as transitive dependencies; they are not called by CanvasPilot and are not tested.

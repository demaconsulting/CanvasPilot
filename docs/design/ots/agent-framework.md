## Microsoft Agent Framework

This document describes the integration and usage design for the `Microsoft.Agents.AI` and
`Microsoft.Agents.AI.Abstractions` OTS software item, the Microsoft Agent Framework.

### Purpose

The Microsoft Agent Framework provides the agent abstraction (`AIAgent`), the conversation holder (`AgentSession`), and
the streamed turn (`RunStreamingAsync`) that CanvasPilot's chat runs on. AgentKit builds its agents on it, and the
application's chat reads the updates of a turn. It was chosen because it is what AgentKit is built on and it is the
common abstraction over the Copilot and Ollama providers. It is a third-party library from an unrelated program.

### Features Used

- `AIAgent` and the `ChatClientAgent` that AgentKit's chat client factory returns, with its function-invocation loop
  that runs the tools of a turn.
- `AgentSession`, created by the agent, which holds the conversation of one chat; a new session starts without the
  earlier turns.
- `RunStreamingAsync`, which streams `AgentResponseUpdate` values whose contents are text, function calls, function
  results, and usage, and which stops with an operation-cancelled exception when its cancellation token is cancelled.

### Integration Pattern

The packages are transitive references of the application library through AgentKit. The units of the Agent subsystem
hold the agent and its session, run one turn at a time, and map the updates to the application's own events, so that the
chat and the tests are independent of the framework's types. The OTS tests in
`test/OtsSoftwareTests/AgentFrameworkTests.cs` run an agent over a scripted chat client, and check the streamed pieces
and their order, the conversation kept by a session, and the stop by cancellation. A library upgrade re-runs them,
together with the unit tests of the Agent subsystem, before it is accepted.

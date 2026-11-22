## AgentKit

This document describes the integration and usage design for the `DemaConsulting.AgentKit` OTS software item: the
Core, Agents.Copilot, Agents.ChatClient, and Agents.Ollama packages.

### Purpose

AgentKit is DEMA Consulting's library for building confined AI agents. CanvasPilot's AI copilot is built on it: Core
provides the path policy and the tool framework that keep the copilot's tools inside the project folder, and the agent
packages turn a provider into an agent that runs a tool-calling turn. It was chosen because it was written for exactly
this use, with refusals as results and not as exceptions, and because it carries the image handling that a drawing
copilot needs. It is a library from another repository of the same organization that CanvasPilot consumes through
NuGet, and it is treated as OTS because its internal design is out of scope here.

The `DemaConsulting.AgentKit.Tools` package is deliberately not referenced. It pins an older `DemaConsulting.CanvasNet`
than the one that CanvasPilot uses, and CanvasPilot builds its tools itself.

### Features Used

- **Core**: `PathPolicy` and `PathRule` (the grants and their denied folder names), `IToolPack` and `ToolPackBuilder`
  (composition of tool families with a unique prefix each), `GuardedToolFactory` (tool names and the delivery of a
  result in the shape a provider can read, and `AsGuarded` to bound any function by `ToolLimits`), and `ToolResult`
  (text, structured, image, and refusal results).
- **Agents.ChatClient**: `ChatClientAgentFactory.Create`, which builds an agent on any chat client with the
  image-promoting decorator installed, so that a picture returned by a tool reaches a model whose tool channel cannot
  carry one. The Ollama provider is built this way.
- **Agents.Ollama**: `OllamaContextSizingChatClient`, which adds the Ollama option `num_ctx` to every request.
- **Agents.Copilot**: `CopilotRuntime`, `CopilotRuntimeOptions`, and `CopilotAgentFactory`, which build a Copilot agent
  on a Copilot runtime that AgentKit starts itself. CanvasPilot references the package so that the Copilot branch of its
  provider factory is written and tested against a fake starter, but it does not start AgentKit's runtime (see below).

### Integration Pattern

The libraries are NuGet package references of the application library and are called in process from the units of the
Agent subsystem. The seams of that subsystem keep the rest of the application and its tests independent of them, and
the OTS tests in `test/OtsSoftwareTests/AgentKitTests.cs` call the libraries the way the subsystem does, with a
scripted chat client and no language model. Refusals of the path policy are results, so the tools of the application
return them to the model; the policy judges a path by its normalized spelling and does not follow links, which is why
the application's own file tools check for links themselves.

### The Copilot runtime is not used

AgentKit starts the Copilot CLI only as a runtime that is bundled beside the application
(`runtimes/<rid>/native/copilot-runtime[.exe]` with its runtime library). The Copilot SDK package would download and
copy that runtime, about 180 MB, into every project that references it. CanvasPilot must not ship or download the GitHub
Copilot runtime, so `CopilotSkipCliDownload` is set to `true` in `Directory.Build.props`, which makes every project of
the solution skip the download, and the runtime is therefore unused and absent. The OTS tests assert that the build
output holds no Copilot runtime file and that starting AgentKit's runtime fails cleanly, naming the missing wrapper; the
FileAssert test `Copilot_RuntimeNotPublished` asserts the published output, and the MSI file table is checked in the
same build.

The consequence is that the Copilot provider of CanvasPilot is gated: the user's installed Copilot CLI cannot be used
with AgentKit 0.1.0-beta.3. Two findings explain it. The `COPILOT_CLI_PATH` variable is ignored, because AgentKit gives
the SDK an environment of its own that withholds every `COPILOT_*` variable except a short allow-list, and AgentKit
passes no `path` to `RuntimeConnection.ForSpawnedStdio`. And the sign-in of the CLI itself (`copilot`, then `/login`) is
not shared with AgentKit's runtime, which starts with its own data directory; the credentials that do work are a
signed-in GitHub CLI (`gh auth login`) or the environment variables `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, and
`GITHUB_TOKEN`.

### Upstream asks

Two changes to AgentKit would remove the gate, and neither is worked around in CanvasPilot:

1. `CopilotRuntimeOptions.CliPath`, passed on as `RuntimeConnection.ForSpawnedStdio(path:)`. The SDK already gives an
   explicit path the top precedence, which the OTS tests of the Copilot SDK show. CanvasPilot would then pass the path
   of the installed CLI that its locator found.
2. A way to use the signed-in session of the Copilot CLI that the user already has, for example by allowing the user's
   `COPILOT_HOME` and keychain, or through an explicit option.

When AgentKit ships the first change, the Copilot starter of CanvasPilot is replaced by one that uses it and the
package versions are raised; the gated wording is then removed.

### Direction: selectable tool packs

A later increment may let the user choose further AgentKit tool packs in the settings (a list of packs, each on or off,
in the AI Provider page). That depends on `DemaConsulting.AgentKit.Tools` moving to the CanvasNet 0.2 line, which is the
prerequisite recorded for it: the `main` branch of AgentKit has already raised its CanvasNet reference to 0.2.0-beta.13
(pull request 36), but no release carries that change yet, so the package is still not referenced. Until then the
copilot has only the fixed, locked-down tools of CanvasPilot, and the application's registry already offers an empty
catalog of optional packs that the settings could fill.

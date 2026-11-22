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

The `DemaConsulting.AgentKit.Tools` package is deliberately not referenced in this change. Since version 0.1.0-beta.4 it
pins the same `DemaConsulting.CanvasNet` (0.2.0-beta.13) as CanvasPilot's own CanvasNet tool and viewer, where the
earlier version pinned 0.1.0, so the obstacle to a later increment with selectable tool packs is gone (see *Direction:
selectable tool packs*); CanvasPilot still builds its tools itself.

### Features Used

- **Core**: `PathPolicy` and `PathRule` (the grants and their denied folder names), `IToolPack` and `ToolPackBuilder`
  (composition of tool families with a unique prefix each), `GuardedToolFactory` (tool names and the delivery of a
  result in the shape a provider can read, and `AsGuarded` to bound any function by `ToolLimits`), and `ToolResult`
  (text, structured, image, and refusal results).
- **Agents.ChatClient**: `ChatClientAgentFactory.Create`, which builds an agent on any chat client with the
  image-promoting decorator installed, so that a picture returned by a tool reaches a model whose tool channel cannot
  carry one. The Ollama provider is built this way.
- **Agents.Ollama**: `OllamaContextSizingChatClient`, which adds the Ollama option `num_ctx` to every request.
- **Agents.Copilot**: `CopilotRuntime`, `CopilotRuntimeOptions`, `CopilotRuntimeSource`, and `CopilotAgentFactory`,
  which start a Copilot runtime on the Copilot CLI that the user installed and build a Copilot agent on it. The
  installed CLI is the default source (`CopilotRuntimeSource.InstalledCli`); `CopilotCli.MinimumVersion` is the oldest
  CLI that the library accepts. The model catalog and the model settings that this version offers are not used yet; they
  are a later step.

### Integration Pattern

The libraries are NuGet package references of the application library and are called in process from the units of the
Agent subsystem. The seams of that subsystem keep the rest of the application and its tests independent of them, and
the OTS tests in `test/OtsSoftwareTests/AgentKitTests.cs` call the libraries the way the subsystem does, with a
scripted chat client and no language model. Refusals of the path policy are results, so the tools of the application
return them to the model; the policy judges a path by its normalized spelling and does not follow links, which is why
the application's own file tools check for links themselves.

### The Copilot runtime on the installed CLI

AgentKit (version 0.1.0-beta.4; the Copilot SDK, the Microsoft Agent Framework, OllamaSharp, and
Microsoft.Extensions.AI.Abstractions are pinned as before) runs the Copilot CLI that the user installed, at an explicit
path. The `CopilotRuntimeStarter` unit builds
`CopilotRuntimeOptions(dataDirectory, gitHubToken: null, logger, CopilotRuntimeSource.InstalledCli, cliPath)` and starts
the runtime with `CopilotRuntime.StartAsync`. These are the rules of the library that the design relies on:

- `cliPath` must be an absolute path and must not be a `.cmd` or `.bat` file; the library refuses anything else with an
  `ArgumentException` when the options are built. The variable `COPILOT_CLI_PATH` is ignored.
- The library checks the version of the CLI by running it with `--no-auto-update --version` and refuses a CLI older than
  `CopilotCli.MinimumVersion` (1.0.113). The locator of CanvasPilot uses the same probe and minimum, and an OTS test
  asserts that the minimum of the library is never above the one of the locator.
- The runtime signs in from a token option (unused: CanvasPilot passes `null` and keeps no token), from the variables
  `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, and `GITHUB_TOKEN`, or from a signed-in GitHub CLI (`gh auth login`, with `gh` on
  the `PATH`). The sign-in that the Copilot CLI keeps for itself (`copilot`, then `/login`) is **not** used, because the
  library gives the runtime its own home (the data folder) and switches the keychain off. This was verified on
  2026-11-22 with a CLI that was signed in with `/login`: with `gh` removed from the `PATH` and no token variable the
  runtime started but was not authenticated, and the library refused it.
- The data directory must be empty, missing, or a directory that an earlier version of the library made; the runtime
  keeps its home and its sessions there, so only one runtime may use it at a time.
- A runtime that cannot be used fails with an `InvalidOperationException` whose text explains the cause and the remedy
  (a CLI that is too old, a runtime that is not signed in, and so on). The library has no typed causes, so the starter
  recognizes the words of the message (see the design of the `CopilotRuntimeStarter` unit).

CanvasPilot must not ship or download the GitHub Copilot runtime. The Copilot SDK package would download and copy a
bundled runtime, about 180 MB, into every project that references it, so `CopilotSkipCliDownload` is set to `true` in
`Directory.Build.props`, which makes every project of the solution skip the download. The `Bundled` source is never
chosen, so the installed CLI is the only runtime that is started, and no Copilot runtime file is in the build output,
the published output, or the MSI. The OTS test `AgentKit_BuildOutput_HasNoBundledCopilotRuntime` asserts the build
output, the FileAssert test `Copilot_RuntimeNotPublished` asserts the published output, and the MSI file table is
checked in the same build.

### Upstream request

The sign-in of the Copilot CLI itself (`/login`) cannot be reused by CanvasPilot, so a user who is signed in only to the
CLI must also sign in with `gh auth login` or set a token variable. A way for the library to use the signed-in session
of the Copilot CLI that the user already has (for example by allowing the user's `COPILOT_HOME` and keychain, or through
an explicit option) is a request to AgentKit. It is a documented limitation of CanvasPilot and is not worked around.

### Direction: selectable tool packs

A later increment may let the user choose further AgentKit tool packs in the settings (a list of packs, each on or off,
in the AI Provider page). The prerequisite recorded earlier is met: `DemaConsulting.AgentKit.Tools` now pins the same
CanvasNet line (0.2.0-beta.13) as CanvasPilot, but the package is still not referenced in this change. Until then the
copilot has only the fixed, locked-down tools of CanvasPilot, and the application's registry already offers an empty
catalog of optional packs that the settings could fill.

## Microsoft.Copilot.SDK

This document describes the integration and usage design for the `Microsoft.Copilot.SDK` OTS software item.

### Purpose

Microsoft.Copilot.SDK is the client library for the GitHub Copilot CLI: it starts the CLI as a child process, speaks
its protocol over standard input and output, and exposes sessions, tools, and events. CanvasPilot does not use it
directly. It arrives with `DemaConsulting.AgentKit.Agents.Copilot`, which drives it, and it is listed as an OTS item
because it is the library that decides how a Copilot CLI is found and started, and because its build targets would copy
a Copilot runtime into the output of every project that references it. It is a third-party library from an unrelated
program.

### Features Used

- **Connection to the CLI**: `RuntimeConnection.ForSpawnedStdio`, whose `path` argument names a CLI to start and takes
  precedence over everything else, and whose `environment` argument is the environment that the SDK reads
  `COPILOT_CLI_PATH` from when the connection supplies one. AgentKit supplies its own environment and no path.
- **Start-up failure**: a client that cannot find a CLI or a bundled runtime wrapper fails to start with an error that
  names what is missing.
- **The CLI version it is built for**: the package declares it as `CopilotCliVersion` in its build props, and the
  protocol it speaks is the one of that CLI version.
- **Build targets**: with `CopilotSkipCliDownload` set to `true`, the targets neither download nor copy a runtime.

### Integration Pattern

The package is a transitive reference of the application library through AgentKit. The test project references it
directly as well, only so that its build props reach the OTS tests, which read the declared CLI version from an
assembly metadata attribute. The tests in `test/OtsSoftwareTests/CopilotSdkTests.cs` start no Copilot CLI and use no
model: they start clients that must fail, and they check the documented minimum CLI version of CanvasPilot against the
version the SDK is built for. The facts that they pin are those on which the gating of the Copilot provider rests (see
the AgentKit design): an explicit path wins, the CLI path is read from the connection's own environment and not from the
process, and no runtime is present. The SDK is not started in the application in this version, and no Copilot runtime
is bundled or downloaded; this is guarded by `CopilotSkipCliDownload` in `Directory.Build.props`.

The two changes that would let the application use the SDK with the CLI that the user installed are asks of AgentKit,
not of this library: a `CliPath` option of AgentKit's runtime options that it passes as the `path` of
`RuntimeConnection.ForSpawnedStdio`, and a way to use the session of the CLI that is already signed in. They are
recorded in the AgentKit design, and the SDK already supports the first (an explicit path wins).

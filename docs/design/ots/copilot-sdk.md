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
  `COPILOT_CLI_PATH` from when the connection supplies one. AgentKit supplies its own environment, and passes the path
  of its runtime options when it runs the installed CLI.
- **Start-up failure**: a client that cannot find a CLI or a bundled runtime wrapper fails to start with an error that
  names what is missing.
- **The CLI version it is built for**: the protocol it speaks is the one of one CLI version, and the AgentKit Copilot
  package states the oldest CLI that matches it as `CopilotCli.MinimumVersion`.
- **Build targets**: with `CopilotSkipCliDownload` set to `true`, the targets neither download nor copy a runtime.

### Integration Pattern

The package is a transitive reference of the application library through AgentKit. The test project references it
directly as well, only so that the OTS tests can use its client types. The tests in
`test/OtsSoftwareTests/CopilotSdkTests.cs` start no Copilot CLI and use no model: they start clients that must fail, and
they check the documented minimum CLI version of CanvasPilot against `CopilotCli.MinimumVersion` of AgentKit. The facts
that they pin are those on which the use of the installed CLI rests (see the AgentKit design): an explicit path wins,
the CLI path is read from the connection's own environment and not from the process, and no runtime is present. The SDK
is started in the application through AgentKit, which runs the CLI that the user installed at the path that the locator
found; no Copilot runtime is bundled or downloaded, which is guarded by `CopilotSkipCliDownload` in
`Directory.Build.props`. The connection settings (the path and the environment) are those of AgentKit's runtime options,
not of the application; the order in which the runtime signs in and the account that it reports are recorded in the
AgentKit design.

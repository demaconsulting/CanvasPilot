## Microsoft.Copilot.SDK Verification

This document provides the verification evidence for the Microsoft.Copilot.SDK OTS software item. Requirements for this
OTS item are defined in the Microsoft.Copilot.SDK OTS Software Requirements document.

### Required Functionality

CanvasPilot does not start the Copilot SDK itself: the runtime of AgentKit, which is built on the SDK, starts the
Copilot CLI that the user installed at an explicit path. The facts on which that rests are verified at the level of the
SDK: an explicit CLI path on a connection is honored and named in its error, the CLI path is read from the connection's
own environment and not from the host process, and the oldest CLI version that AgentKit runs is not above the minimum
that CanvasPilot documents and checks.

### Verification Approach

The SDK has no self-validation mode. It is verified by tests in `test/OtsSoftwareTests/CopilotSdkTests.cs` that start
clients that must fail, so that no Copilot CLI runs and no model or sign-in is needed, and that read the minimum CLI
version from the AgentKit library (`CopilotCli.MinimumVersion`) and compare it with the minimum of the locator of
CanvasPilot. The tests are run again before a version upgrade is accepted. An upgrade of the SDK or of AgentKit is also
the moment to run the live tests of the manual verification of the system, which start a real runtime on the installed
CLI.

### Test Scenarios

#### CopilotSdk_AgentKitMinimumCliVersion_IsNotAboveCanvasPilotMinimum

**Scenario**: The oldest version of the Copilot CLI that AgentKit runs (`CopilotCli.MinimumVersion`) is compared with
the minimum CLI version that CanvasPilot documents and checks (`CopilotCliLocator.MinimumCliVersion`, 1.0.113, the first
with the protocol that the SDK speaks).

**Expected**: The minimum of the library is not above the minimum of CanvasPilot, so the version check that the user
sees never passes a CLI that the library then refuses.

**Requirement coverage**: `CanvasPilot-OTS-CopilotSdk-DeclareSupportedCliVersion`.

#### CopilotSdk_Client_StartAsync_ExplicitMissingCliPath_FailsNamingThePath

**Scenario**: A client whose connection names a CLI path that does not exist is started.

**Expected**: The start fails, and the error names the path that was given and not the bundled runtime wrapper.

**Requirement coverage**: `CanvasPilot-OTS-CopilotSdk-HonorExplicitCliPath`.

#### CopilotSdk_Client_StartAsync_OwnEnvironmentWithoutCliPath_FailsNamingTheWrapper

**Scenario**: A client whose connection supplies an empty environment is started in an application that carries no
Copilot runtime, on a machine that may have `COPILOT_CLI_PATH` set.

**Expected**: The start fails with an error that names the missing `copilot-runtime` wrapper, whatever the variable of
the process is.

**Requirement coverage**: `CanvasPilot-OTS-CopilotSdk-ReadCliPathFromConnectionEnvironment`.

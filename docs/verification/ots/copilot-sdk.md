## Microsoft.Copilot.SDK Verification

This document provides the verification evidence for the Microsoft.Copilot.SDK OTS software item. Requirements for this
OTS item are defined in the Microsoft.Copilot.SDK OTS Software Requirements document.

### Required Functionality

CanvasPilot does not start the Copilot SDK in this version. The facts on which the gating of the Copilot provider rests
are verified instead: an explicit CLI path on a connection is honored and named in its error, the CLI path is read from
the connection's own environment and not from the host process, and the CLI version that the SDK is built for is not
below the minimum that CanvasPilot documents.

### Verification Approach

The SDK has no self-validation mode. It is verified by tests in `test/OtsSoftwareTests/CopilotSdkTests.cs` that start
clients that must fail, so that no Copilot CLI runs and no model or sign-in is needed, and that read the CLI version
from the build props of the package, which the test project receives through a direct package reference and records in
an assembly metadata attribute. The tests are run again before a version upgrade is accepted, and an upgrade is also the
moment to check whether AgentKit has gained the CLI path option that removes the gate.

### Test Scenarios

#### CopilotSdk_BuiltForCliVersion_IsNotBelowCanvasPilotMinimum

**Scenario**: The version of the Copilot CLI that the SDK package declares is read from the test assembly and compared
with the minimum CLI version that CanvasPilot documents, 1.0.113, the first with the protocol that the SDK speaks.

**Expected**: The declared version is a version and is not below the minimum.

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

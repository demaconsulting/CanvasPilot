## xUnit Verification

This document provides the verification evidence for the xUnit OTS software item. Requirements
for this OTS item are defined in the xUnit OTS Software Requirements document.

### Required Functionality

xUnit v3 (xunit.v3 and xunit.runner.visualstudio) is the unit-testing framework used by the
project. It discovers and runs all test methods and writes TRX result files that feed into coverage
reporting and requirements traceability. Passing tests confirm the framework is functioning
correctly.

### Verification Approach

xUnit is verified by self-validation evidence from the CI pipeline. Each scenario names a specific
test method that xUnit must discover, execute, and record in a TRX result file. A passing pipeline
run for all scenarios constitutes evidence that both requirements are satisfied.

### Test Scenarios

#### PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly

**Scenario**: xUnit discovers and runs this test; the test verifies that SafePathCombine correctly
joins valid path segments.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasPilot-OTS-xUnit-Execute`, `CanvasPilot-OTS-xUnit-Report`.

#### RollingFileLogger_Log_Event_WritesTimestampedEntry

**Scenario**: xUnit discovers and runs this test; the test verifies that logging an event writes a
timestamped, leveled entry to the active log file.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasPilot-OTS-xUnit-Execute`, `CanvasPilot-OTS-xUnit-Report`.

#### RollingFileLogger_Log_WithException_IncludesExceptionDetails

**Scenario**: xUnit discovers and runs this test; the test verifies that logging a failure records the
exception details alongside the message.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasPilot-OTS-xUnit-Execute`, `CanvasPilot-OTS-xUnit-Report`.

#### RollingFileLogger_Constructor_NonPositiveMaximumFileSize_ThrowsArgumentException

**Scenario**: xUnit discovers and runs this test; the test verifies that constructing a logger with a
non-positive maximum file size raises an exception.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasPilot-OTS-xUnit-Execute`, `CanvasPilot-OTS-xUnit-Report`.

#### MainWindowShell_New_NoProject_TitleIsApplicationNameAndProjectCommandsDisabled

**Scenario**: xUnit discovers and runs this test; the test verifies that a newly created application
shell reports the application name as its title and disables the commands that need a project.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasPilot-OTS-xUnit-Execute`, `CanvasPilot-OTS-xUnit-Report`.

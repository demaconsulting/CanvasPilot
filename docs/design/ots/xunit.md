## xUnit

This document describes the integration and usage design for the `xUnit` OTS software item.

### Purpose

xUnit (`xunit.v3` together with `xunit.runner.visualstudio`) is chosen as the unit-testing framework
for the project. It provides test discovery and execution and emits TRX result files that serve as
the traceability evidence consumed by ReqStream.

### Features Used

- `[Fact]` and `[Theory]` test discovery and execution
- Visual Studio test runner integration for `dotnet test`
- TRX result file output
- Assembly fixtures (`AssemblyFixture`), which the shared `SerializedTestRun` fixture uses to make the test projects run
  one after the other

### Integration Pattern

xUnit is consumed as a NuGet package reference in the test projects. Tests run via `dotnet test`,
which discovers and executes the `[Fact]` and `[Theory]` tests and writes TRX result files that
ReqStream consumes for requirements traceability. xUnit does not provide a self-validation mode; the
project's own passing test suite is the integration evidence. No additional initialization or
disposal is required beyond the standard test runner lifecycle.

`dotnet test` on the solution starts the test host of every test project at the same moment, and the hosts compete
for the processor; tests that depend on timing then fail now and then, although they pass when their project runs
alone. The one source file `test/SerializedTestRun.cs` is therefore compiled into each of the three test projects as an
assembly fixture. Before its first test a host takes an exclusive lock on one file in the temporary folder, and it
releases the lock after its last test, so the projects run one after the other. The lock belongs to the operating
system's open file, so a host that is killed releases it, and a host that cannot get the lock within ten minutes, or
cannot use the file, runs its tests anyway.

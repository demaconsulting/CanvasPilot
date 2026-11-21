## CommunityToolkit.Mvvm Verification

This document provides the verification evidence for the CommunityToolkit.Mvvm OTS software item. Requirements for
this OTS item are defined in the CommunityToolkit.Mvvm OTS Software Requirements document.

### Required Functionality

CommunityToolkit.Mvvm generates observable properties with dependent-property and command notifications, commands with
can-execute rules, and asynchronous commands that report their run and refuse a second start. The application's view
models rely on each of these behaviors, so each has a requirement and a test.

### Verification Approach

CommunityToolkit.Mvvm has no self-validation mode. It is verified by tests in
`test/OtsSoftwareTests/CommunityToolkitMvvmTests.cs` that use a sample view model built with the same attributes as
the application's view models (`[ObservableProperty]`, `[NotifyPropertyChangedFor]`, `[NotifyCanExecuteChangedFor]`,
and `[RelayCommand]`, including a can-execute rule and an asynchronous method). The tests are the evidence that the
generated code behaves as the application relies on, and they are run again before a version upgrade is accepted. The
unit tests of the application's view models repeat part of this evidence through the application's own classes.

### Test Scenarios

#### CommunityToolkitMvvm_ObservableProperty_RaisesOwnAndDependentNotifications

**Scenario**: A property with a dependent property is changed to a new value and then set to the same value again.

**Expected**: The change reports the property and then the dependent property once each, the repeated value reports
nothing, and the dependent property shows the new state.

**Requirement coverage**: `CanvasPilot-OTS-CommunityToolkitMvvm-NotifyPropertyChanges`.

#### CommunityToolkitMvvm_RelayCommand_FollowsCanExecuteRule

**Scenario**: A command that can run only when a property has a value is asked whether it can run, the property is
given a value, the command is asked again and run.

**Expected**: The command cannot run without a value and can run with one, it raises its can-execute change event when
the property changes, and its method runs once.

**Requirement coverage**: `CanvasPilot-OTS-CommunityToolkitMvvm-FollowCommandAvailability`.

#### CommunityToolkitMvvm_AsyncRelayCommand_ReportsRunningAndRefusesConcurrentRuns

**Scenario**: An asynchronous command that waits for a signal is started, asked whether it can start again while it
waits, and then released.

**Expected**: The command reports that it is running while it waits, cannot start again, and after it finishes is not
running and can run again.

**Requirement coverage**: `CanvasPilot-OTS-CommunityToolkitMvvm-RunAsyncCommandsOnce`.

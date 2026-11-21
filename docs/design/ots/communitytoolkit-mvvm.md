## CommunityToolkit.Mvvm

This document describes the integration and usage design for the `CommunityToolkit.Mvvm` OTS software item.

### Purpose

CommunityToolkit.Mvvm is the Microsoft-maintained library of building blocks for the Model-View-ViewModel pattern.
CanvasPilot uses its source generators so that view models declare their state and actions with short attributes and
the library writes the repetitive property change notification and command code at build time. It was chosen because
it is small, widely used with Avalonia, and has no run-time dependencies of its own. It is a third-party library from an
unrelated program, so it is treated as OTS.

### Features Used

- `ObservableObject` as the base of view models, and the `[ObservableProperty]` attribute that generates a property
  with change notification from a field, with `[NotifyPropertyChangedFor]` for dependent properties and
  `[NotifyCanExecuteChangedFor]` for commands whose availability depends on the property.
- The `[RelayCommand]` attribute that generates a command from a method, with the `CanExecute` option naming the rule
  that decides whether the command can run, and the asynchronous form (`IAsyncRelayCommand`) for methods that return
  a task, which reports that it is running and refuses a second start while it runs.

### Integration Pattern

The library is a NuGet package reference of the application library. View models are `partial` classes, so that the
generators can add their members; the rest of the application uses only the generated properties and commands through
normal property and command interfaces, so views bind to them without knowing the library. The library has no
self-validation mode; the OTS tests in `CommunityToolkitMvvmTests.cs` use a sample view model that applies the
attributes the way the application's view models do, and are the evidence that the generated code behaves as the
application relies on. A library upgrade re-runs them, together with the unit tests of the view models, before it is
accepted.

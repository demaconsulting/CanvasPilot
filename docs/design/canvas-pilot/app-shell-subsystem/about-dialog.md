### AboutDialog

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`AboutDialog` is the modal window opened from the Help menu. It identifies the application and acknowledges the
open source libraries it is built on.

#### Data Model

**Name** / **Tagline** / **Copyright**: `string` — `CanvasPilot`, `AI-assisted drawing creation with CanvasNet`, and
`Copyright (c) DEMA Consulting`.

**Version** / **VersionText**: `string` — the version supplied to the constructor, or the informational version of
the application assembly with any build metadata after a plus sign removed (`0.0.0` when the assembly has none),
and the same prefixed with `Version `.

**Dependencies**: `IReadOnlyList<AboutDependency>` — `Avalonia`, `CommunityToolkit.Mvvm`,
`DemaConsulting.CanvasNet`, and `DemaConsulting.Speech`, each with a description of what the application uses it for.

**CloseCommand** / **CloseRequested**: Close raises the event and the window closes itself.

**View**: `AboutDialogView` shows the name, tagline, version, copyright, the library list, and a Close button.
Controls carry automation identifiers (`AboutName`, `AboutTagline`, `AboutVersion`, `AboutCopyright`,
`AboutDependencies`, `AboutCloseButton`). The view has a parameterless constructor for the XAML loader and a
constructor that takes the view model.

#### Key Methods

**Version selection**: A supplied version that is not blank is trimmed and used; otherwise the assembly's
informational version is read and cut at the first plus sign.

**Close** (command): Requests the host to close the dialog.

#### Error Handling

No operation fails: a missing version attribute falls back to `0.0.0`.

#### Dependencies

- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **Avalonia** — the window and controls.

#### Callers

- **MainWindowShell** — creates the view model and has the dialog service show the window.

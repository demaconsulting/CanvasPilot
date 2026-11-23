### AboutDialog

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`AboutDialog` is the modal window opened from the Help menu. It identifies the application with its brand mark, name,
tagline, version, and copyright, and it acknowledges the open source libraries and the icon set that the application is
built on.

#### Data Model

**Name** / **Tagline** / **Copyright**: `string` — `CanvasPilot`, `AI-assisted drawing creation with CanvasNet`, and
`Copyright (c) DEMA Consulting`.

**Version** / **VersionText**: `string` — the version supplied to the constructor, or the informational version of
the application assembly with any build metadata after a plus sign removed (`0.0.0` when the assembly has none),
and the same prefixed with `Version `.

**Dependencies**: `IReadOnlyList<AboutDependency>` — six entries in this order: `Avalonia`, `Avalonia.AvaloniaEdit`,
`CommunityToolkit.Mvvm`, `DemaConsulting.CanvasNet`, `DemaConsulting.Speech`, and `Fluent System Icons` (the line
icons of the user interface, by Microsoft Corporation, MIT license), each with a description of what the application
uses it for. The icon set is vendored as assets and is not an OTS package; listing it here acknowledges it next to the
libraries, and its license notice ships with the application (see *ThemeResources Design*).

**CloseCommand** / **CloseRequested**: Close raises the event and the window closes itself.

**View**: `AboutDialogView` is a 520 by 520 window that cannot be resized, with the pane color. A header shows the brand
mark (a 56 by 56 rounded tile with the accent gradient and the sparkle icon) beside the name (page title style, 24
pixels) and the tagline; under it the version in semibold and the copyright; then a card (`Border.card`) titled "Built
with open source software" that holds the list of dependencies in a scroll viewer, each as its name in semibold over its
description. A footer bar under a subtle line holds the Close button, which is the primary button and both the default
and the cancel button of the window. Controls carry automation identifiers (`AboutName`, `AboutTagline`, `AboutVersion`,
`AboutCopyright`, `AboutDependencies`, `AboutCloseButton`), unchanged by the restyle. The view has a parameterless
constructor for the XAML loader and a constructor that takes the view model.

#### Key Methods

**Version selection**: A supplied version that is not blank is trimmed and used; otherwise the assembly's
informational version is read and cut at the first plus sign.

**Close** (command): Requests the host to close the dialog.

#### Error Handling

No operation fails: a missing version attribute falls back to `0.0.0`.

#### Dependencies

- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **ThemeResources** — the tokens, the card and heading styles, the primary button look, and the icon of the brand mark.
- **Avalonia** — the window and controls.

#### Callers

- **MainWindowShell** — creates the view model and has the dialog service show the window.

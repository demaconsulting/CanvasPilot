### AboutDialog

#### Verification Approach

`AboutDialog` is verified by unit tests in `AboutDialogTests.cs`, which drive the view model directly, and by a
headless UI test in `MainWindowViewTests.cs` that opens the real `AboutDialogView` from the Help menu command,
reads its controls by automation identifier, and clicks Close.

#### Test Environment

Unit tests use the standard test environment. The UI test runs under the Avalonia headless platform, serialized
with test parallelization disabled; no display is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The dialog shows the name, tagline, copyright, and a version without build metadata; a supplied version is
  trimmed and a blank one falls back to the assembly version.
- Six libraries are listed, each with a description, the last being the Fluent System Icons.
- Close asks the host to close and the window closes.

#### Test Scenarios

**AboutDialogViewModel_New_ShowsIdentityAndVersion**: The texts are shown and the version has no plus sign. This
scenario is tested by `AboutDialogViewModel_New_ShowsIdentityAndVersion`.

**AboutDialogViewModel_New_SuppliedVersion_IsShownTrimmed**: Supplied versions are shown trimmed, including a
pre-release version. This scenario is tested by `AboutDialogViewModel_New_SuppliedVersion_IsShownTrimmed`.

**AboutDialogViewModel_New_BlankVersion_UsesAssemblyVersion**: A blank version equals the default version. This
scenario is tested by `AboutDialogViewModel_New_BlankVersion_UsesAssemblyVersion`.

**AboutDialogViewModel_Dependencies_ListsOpenSourceLibraries**: The six libraries are listed in order (Avalonia,
Avalonia.AvaloniaEdit, CommunityToolkit.Mvvm, DemaConsulting.CanvasNet, DemaConsulting.Speech, and Fluent System Icons),
each with a description. This scenario is tested by `AboutDialogViewModel_Dependencies_ListsOpenSourceLibraries`.

**AboutDialogViewModel_Close_RequestsClose**: Close raises one close request. This scenario is tested by
`AboutDialogViewModel_Close_RequestsClose`.

**MainWindowView_AboutMenuItem_OpensAboutDialogAndCloseButtonCloses**: The shown window has the identity texts and
six libraries, and clicking Close closes it. This scenario is tested by
`MainWindowView_AboutMenuItem_OpensAboutDialogAndCloseButtonCloses`.

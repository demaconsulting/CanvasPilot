### NewProjectDialog

![Project Structure](ProjectSubsystemView.svg)

#### Purpose

`NewProjectDialog` is the modal window and view model in which the user chooses the name of a new project
and the folder that will contain it. It checks the entries as they are typed and enables OK only when a
project can be created. It does not create the project: the caller does that when the dialog was accepted.
Browsing for a folder is delegated to an `IFolderPicker` that the application supplies, so the platform
folder dialog stays out of the view model.

#### Data Model

**ProjectName** / **ParentFolder**: `string` — the entries as typed. The parent folder starts with the
folder supplied to the constructor, or empty.

**NameError** / **FolderError** / **TargetError**: `string` — the problem with the name, with the parent
folder, and with the project folder itself (for example, an existing non-empty folder), or an empty string.
The target problem is empty while the name or folder has its own problem.

**TargetPath**: `string` — the full path of the project folder that would be created
(`{parent}/{name}`, resolved with `PathHelpers.SafePathCombine`), or empty while the entries are not valid.

**IsValid**: `bool` — true when all three problems are empty.

**Accepted**: `bool` — true after the user chose OK.

**Commands**: `BrowseCommand` (asynchronous), `OkCommand` (enabled only while `IsValid`), and `CancelCommand`.

**CloseRequested**: event with `NewProjectCloseRequestedEventArgs.Accepted` — raised when the dialog asks its
host to close.

**View**: `NewProjectDialogView` is an Avalonia window, 560 by 360 and not resizable, drawn with the design tokens of
`ThemeResources`: its body is the pane surface, and the content is a column of a `label` text, an input, and an `error`
text for the name; a `label` text, the location input with a Browse button (a `SecondaryButton`), and the `error` texts
for the folder and the target; and the `hint` text with the target path. A footer bar (the window surface with a subtle
line above it) holds OK, the main action (a `PrimaryButton`, the default button of the window), and Cancel (a
`SecondaryButton`, the cancel button of the window), both at least 88 px wide. The name input and the location input
have automation names ("Project name" and "Location"). Controls carry automation identifiers (`NewProjectNameTextBox`,
`NewProjectFolderTextBox`, `NewProjectBrowseButton`, `NewProjectNameError`, `NewProjectFolderError`,
`NewProjectTargetError`, `NewProjectTargetPath`, `NewProjectOkButton`, `NewProjectCancelButton`), which the restyle did
not change. The view has a parameterless constructor for the XAML loader and a constructor that takes the view model,
and it closes itself when the view model asks.

#### Key Methods

**Validation**: `NameError` and `FolderError` call `IProjectService.ValidateProjectName` and
`ValidateParentFolder`; `TargetError` calls `ValidateNewProject` once both are clear. Changing either entry
announces the message properties, `TargetPath`, `IsValid`, and the OK command state.

**Browse** (command): Calls `IFolderPicker.PickFolderAsync` with the current folder, or `null` when the entry is
blank, and, when a folder is returned, makes it the parent folder. Canceling changes nothing.

**Ok** (command): Sets `Accepted` and requests the host to close with acceptance.

**Cancel** (command): Requests the host to close without acceptance.

#### Error Handling

The constructor throws `ArgumentNullException` for a null service or folder picker. Invalid entries are
reported through the message properties, never thrown. The OK command cannot run while the entries are
invalid.

#### Dependencies

- **ProjectService** (through `IProjectService`) — validates the entries.
- **PathHelpers** — resolves the target path.
- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **Avalonia** — window and controls.
- **ThemeResources** — the design tokens, the `PrimaryButton` and `SecondaryButton` control themes, and the label, hint,
  and error text styles that the view uses; see its design.

#### Callers

- The application shell — builds the view model with the project service, a folder picker implemented by the
  main window, and the starting folder, shows the window modally, and creates the project when `Accepted` is
  true.

### ProjectWorkspace

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`ProjectWorkspace` is the content area of the main window: the project explorer on the left, a splitter, and on
the right an area that shows supplied preview content or explains what is missing. It is the place where the
image preview is plugged in. It also asks the user to confirm deleting the selected file and to enter a new name
when renaming it, which the explorer leaves to its caller.

#### Data Model

**Explorer**: `ProjectExplorerViewModel` — the explorer shown on the left; the workspace owns and disposes it.

**Preview** / **HasPreview**: `object?` and whether it is set. The view shows the content through the view locator
when it is set.

**HasProject** / **HasSelectedFile** / **SelectedFile**: taken from the explorer.

**PlaceholderText**: `string` — `No project open` without a project; `No file selected` with a project and no
selected file; `No preview available` with a selected file and no preview content.

**HintText**: `string` — a sentence about the File menu while no project is open, otherwise empty.

**Commands**: `DeleteSelectedCommand` and `RenameSelectedCommand`, enabled while a file is selected.

**View**: `ProjectWorkspaceView` has three columns: the explorer, a `GridSplitter`, and the right-hand area, which
holds either the preview host or the placeholder and hint. A context menu on the explorer offers Rename and
Delete, and F2 and Delete are bound to the same commands. Controls carry automation identifiers
(`WorkspaceExplorerHost`, `WorkspaceSplitter`, `WorkspacePreviewHost`, `WorkspacePlaceholder`, `WorkspaceHint`,
`RenameFileMenuItem`, `DeleteFileMenuItem`).

#### Key Methods

**ShowProject**: Takes the open `ProjectInfo` or `null`. A project opens its folder in the explorer unless that
folder is already shown, so showing the same project again keeps the selection; `null` closes the explorer.

**DeleteSelected** (command): Asks `ConfirmAsync` naming the file; only when confirmed, calls
`ProjectExplorerViewModel.DeleteSelected`, and shows a returned problem with `ShowMessageAsync`.

**RenameSelected** (command): Asks `PromptAsync` for a new name, starting with the current name; when a name was
entered, calls `ProjectExplorerViewModel.RenameSelected` and shows a returned problem.

**AttachDialogService**: Supplies the dialog service; until then both commands do nothing.

**Following the explorer**: The workspace listens to the explorer's property changes and announces the properties
it derives from them and re-evaluates its commands.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing explorer. Operation problems come back from the
explorer as text and are shown to the user, not thrown.

#### Dependencies

- **ProjectExplorer** — lists, selects, renames, and deletes the project's image source files.
- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **Avalonia** — the user control, splitter, and context menu.

#### Callers

- **MainWindowShell** — hosts the workspace as the window content, calls `ShowProject` when the open project
  changes, and supplies the dialog service.
- The image preview (a later increment) — sets `Preview` and reads `SelectedFile`.

### ProjectWorkspace

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`ProjectWorkspace` is the content area of the main window, in three columns: the project explorer on the left, a
splitter, an area in the middle that shows supplied preview content or explains what is missing, and, while a project
is open, a second splitter and the chat column on the right. It is the place where the image preview and the chat panel
are plugged in. It also asks the user to confirm deleting the selected file and to enter a new name
when renaming it, which the explorer leaves to its caller.

#### Data Model

**Explorer**: `ProjectExplorerViewModel` — the explorer shown on the left; the workspace owns and disposes it.

**Preview** / **HasPreview**: `object?` and whether it is set. The view shows the content through the view locator
when it is set. The application sets it to the preview pane, whose own placeholder then replaces the workspace's.
When the workspace is disposed it also disposes a `Preview` that is `IDisposable`.

**HasProject** / **HasSelectedFile** / **SelectedFile**: taken from the explorer.

**Chat** / **IsChatVisible**: `object?`, the content of the chat column, and whether the column is shown: the content
is set and a project is open (a message is written in the project, so there is nothing to write without one). The view
shows the content through the view locator. The workspace does not own it: it is not disposed with the workspace,
because the composition root disposes the chat panel. When `Chat` is a `ChatPanelViewModel` the workspace makes the
dialog service its `InspectorLauncher`; this happens when the dialog service is attached and when the chat is set, so
the order of the two does not matter.

**PlaceholderText**: `string` — `No project open` without a project; `No file selected` with a project and no
selected file; `No preview available` with a selected file and no preview content.

**HintText**: `string` — a sentence about the File menu while no project is open, otherwise empty.

**Commands**: `DeleteSelectedCommand` and `RenameSelectedCommand`, enabled while a file is selected.

**View**: `ProjectWorkspaceView` has five grid columns: the explorer (260 wide), a `GridSplitter`, the middle area
(takes the rest), a second splitter, and the chat column (380 wide at first). The middle area holds either the
preview host or the placeholder and hint. The code of the view gives the last two columns their widths, because a
column cannot be hidden by a binding: they are 0 wide while `IsChatVisible` is false, and the chat column has a
minimum of 280 while shown; it keeps the width the user chose when it is hidden and shown again. A context menu on
the explorer offers Rename and
Delete, and F2 and Delete are bound to the same commands. Controls carry automation identifiers
(`WorkspaceExplorerHost`, `WorkspaceSplitter`, `WorkspacePreviewHost`, `WorkspaceChatSplitter`,
`WorkspaceChatHost`, `WorkspacePlaceholder`, `WorkspaceHint`, `RenameFileMenuItem`, `DeleteFileMenuItem`).

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
- **Chat** (through `ChatPanelViewModel.InspectorLauncher`) — the workspace tells the chat panel which dialog service
  shows its payload inspector.
- **Avalonia** — the user control, splitter, and context menu.

#### Callers

- **MainWindowShell** — hosts the workspace as the window content, calls `ShowProject` when the open project
  changes, and supplies the dialog service.
- The `PreviewPane` of the `Preview` subsystem — the application sets `Preview` to it, and it follows the
  explorer's selection; the workspace disposes it with the explorer.
- The `ChatPanel` of the `Chat` subsystem — the application sets `Chat` to it; it is shown in the chat column while a
  project is open, and the composition root disposes it.

### ProjectWorkspace

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`ProjectWorkspace` is the content area of the main window, in three columns: the project explorer on the left, a
splitter, an area in the middle (the stage) that shows supplied preview content or explains what is missing, and, while
a project is open, a second splitter and the chat column on the right. It is the place where the image preview and the
chat panel are plugged in. It asks the user to confirm deleting the selected file and to enter a new name when renaming
it, and the name of a new file or folder, which the explorer leaves to its caller. It lets the user hide the chat column
and bring it back, keeps the widths of the explorer and the chat column that the user chose within their limits and
fitted to the window, and tells its owner when the user chose other widths. The unit also holds the pure rules in
`WindowLayout.cs`: how a saved window is fitted to the screens (`WindowPlacement`) and how the column widths are fitted
to the window (`WorkspaceLayout`).

#### Data Model

**Explorer**: `ProjectExplorerViewModel` — the explorer shown on the left; the workspace owns and disposes it.

**Preview** / **HasPreview**: `object?` and whether it is set. The view shows the content through the view locator
when it is set. The application sets it to the preview pane, whose own placeholder then replaces the workspace's.
When the workspace is disposed it also disposes a `Preview` that is `IDisposable`.

**HasProject** / **HasSelectedFile** / **SelectedFile**: taken from the explorer.

**Chat** / **IsChatVisible** / **IsChatCollapsed** / **CanRestoreChat**: `Chat` is `object?`, the content of the chat
column. `IsChatCollapsed` is whether the user hid the column with the button of the chat header. `IsChatVisible` says
whether the column is shown: the content is set, a project is open (a message is written in the project, so there is
nothing to write without one), and the column is not collapsed. `CanRestoreChat` says whether the chip that brings the
column back is shown: the content is set, a project is open, and the column is collapsed. The view shows the content
through the view locator. The workspace does not own it: it is not disposed with the workspace, because the composition
root disposes the chat panel. When `Chat` is a `ChatPanelViewModel` the workspace makes the dialog service its
`InspectorLauncher` and its `AttachmentPicker` (the dialog service is both); this happens when the dialog service is
attached and when the chat is set, so the order of the two does not matter. It also listens to the panel's
`CollapseRequested` event (the button in the header of the chat) and sets `IsChatCollapsed`; the subscription follows a
replaced panel. `RestoreChatCommand` clears `IsChatCollapsed`. The collapsed state is not saved in the settings.

**PlaceholderText**: `string` — `No project open` without a project; `No file selected` with a project and no
selected file; `No preview available` with a selected file and no preview content.

**HintText**: `string` — a sentence about the File menu while no project is open, otherwise empty. With the welcome
screen in the window the placeholder and the hint are not seen without a project (the workspace view is built when the
first project opens), but the properties keep their meaning.

**ExplorerWidth** / **ChatWidth**: `int` — the widths that the user chose, from the saved settings or a splitter; they
start at 264 and 400 and are clamped to 180 to 420 and 320 to 640 (the limits of `WindowSettings`). The chat width is
kept while the column is hidden and shown again. **LayoutChanged** is raised when the user chose other widths.
`RestoreColumnWidths(explorer, chat)` sets the saved widths without raising it, and `SetColumnWidths(explorer, chat)` is
the choice of the user: it raises it only when a width really changed.

**Commands**: `DeleteSelectedCommand` and `RenameSelectedCommand`, enabled while a file is selected, and
`NewFileCommand`, `NewFolderCommand`, enabled while a project is open (the workspace also gives them to the explorer,
whose header buttons run them), and `RestoreChatCommand`.

**View**: `ProjectWorkspaceView` is a grid (`WorkspaceGrid`, the window surface color) with five columns: the explorer
(`WorkspaceExplorerHost`, 264 wide at first, 180 to 420), a splitter column of 1 pixel, the stage (takes the rest, at
least 360), a second splitter column, and the chat column (`WorkspaceChatHost`, 400 wide at first, 320 to 640 while
shown). The panes are the pane color (explorer, chat) and the stage color (`WorkspacePreviewHost` or the placeholder and
hint). The two splitters (`WorkspaceSplitter`, `WorkspaceChatSplitter`) have the `SplitterGrip` theme of
`ThemeResources`, a 9 pixel hit area centered on their 1 pixel line that overlaps the panes beside them, and the names
"Resize the explorer" and "Resize the chat"; they are placed after the panes so that their hit areas lie over them, and
the chat splitter is visible only while the chat column is. The arrow keys move a focused splitter by 16 pixels. The
restore chip (`ChatRestoreButton`, an icon button named and with the tooltip "Show the conversation", in a floating
raised border) is on the stage at its right top edge while `CanRestoreChat`. A context menu on the explorer offers
Rename and Delete, and F2 and Delete are bound to the same commands. Controls carry automation identifiers
(`WorkspaceExplorerHost`, `WorkspaceSplitter`, `WorkspacePreviewHost`, `WorkspaceChatSplitter`, `WorkspaceChatHost`,
`WorkspacePlaceholder`, `WorkspaceHint`, `ChatRestoreButton`, `RenameFileMenuItem`, `DeleteFileMenuItem`).

**View layout code**: the code of the view gives the columns their widths, because they follow what the user chose and
the width of the window, and a column cannot be hidden by a binding. `ApplyLayout` runs when the data context is set,
when the width of the view changes, and when `IsChatVisible`, `ExplorerWidth`, or `ChatWidth` change. It asks
`WorkspaceLayout.Fit` for the widths that fit, gives the explorer column its width, gives the second splitter and the
chat column 0 width while no chat is shown, and sets the chat column minimum to 320 while it is shown. The splitters
change the widths of the grid columns while they are dragged; when a drag ends (`DragCompleted`) or a key is released on
a splitter (`KeyUp`), the view reads the width of the column that the splitter changed and gives it to the view model
with `SetColumnWidths`, which clamps it, raises `LayoutChanged`, and so lets the shell save it.

**WindowPlacement** (`WindowLayout.cs`): a record (`Width`, `Height`, `Position`, `State`) that is the result of
`Resolve(saved, workAreas, scaling)`. It is pure logic, tested with made-up screens, and the window applies it at
construction.

**WorkspaceLayout** (`WindowLayout.cs`): a static class with `MinimumStageWidth` (360), `SplitterWidth` (1), and
`Fit(totalWidth, explorer, chat, chatVisible)`.

#### Key Methods

**ShowProject**: Takes the open `ProjectInfo` or `null`. A project opens its folder in the explorer unless that
folder is already shown, so showing the same project again keeps the selection; `null` closes the explorer.

**DeleteSelected** (command): Asks `ConfirmAsync` naming the file; only when confirmed, calls
`ProjectExplorerViewModel.DeleteSelected`, and shows a returned problem with `ShowMessageAsync`.

**RenameSelected** (command): Asks `PromptAsync` for a new name, starting with the current name; when a name was
entered, calls `ProjectExplorerViewModel.RenameSelected` and shows a returned problem.

**NewFile** (command): Does nothing without a dialog service or without a target folder of the explorer. Asks
`PromptAsync` ("New File", with the name `untitled.svg` suggested and a sentence that says the name needs its
extension); when a name was entered, calls `ProjectExplorerViewModel.CreateFile` (which makes the file in the selected
folder, the folder of the selected file, or the project folder, with a blank drawing for SVG, Mermaid, and Vega-Lite)
and shows a returned problem.

**NewFolder** (command): The same with the title "New Folder", the suggested name `New folder`, and
`ProjectExplorerViewModel.CreateFolder`.

**AttachDialogService**: Supplies the dialog service; until then the file commands do nothing. It also connects the
chat.

**Fit** (`WorkspaceLayout.Fit`): Precondition: any widths. The chosen widths are clamped to their limits (the chat width
is 0 while the chat is not shown). `over` is the sum of the two widths, the splitter lines (two with a chat, otherwise
one), and the least stage width of 360, minus the total width. When `over` is 0 or less the chosen widths are used.
Otherwise the explorer gives up `min(over, explorer - 180)` first, and the chat column gives up what is still missing,
at most down to 320. Postcondition: the explorer is at least 180 and the chat column, when shown, at least 320, and the
stage is at least 360 whenever the total is at least 862 (180 + 320 + 2 + 360), which a window at the least width of 960
always is. At 1440 the widths are 264 and 400; at 1100 they still are; at 1000 the explorer is 238; at 960 it is 198.

**Resolve** (`WindowPlacement.Resolve`): Precondition: `saved` and `workAreas` are not null (an `ArgumentNullException`
otherwise). The width and the height are raised to the least window (960 by 600). When no screen is known or the layout
has no place, the size and state are returned with no place, so the system places the window. Otherwise the saved
rectangle (the size times the display scale, at the saved place) is compared with every work area and the one that holds
most of it is chosen; when less than 160 by 80 device pixels are visible on it (a screen was removed), the place is
dropped. Otherwise the size is limited to what that screen holds (but not below the least window), and the place is
moved just far enough that the window lies inside the screen. The state is the saved one.

**Following the explorer**: The workspace listens to the explorer's property changes and announces the properties
it derives from them and re-evaluates its commands: a change of the selected file updates the placeholder and the delete
and rename commands, and a change of whether a project is open updates the chat visibility, the restore chip, the
placeholder, the hint, and the new file and new folder commands.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing explorer, and `WindowPlacement.Resolve` for a missing
layout or list of screens. Operation problems come back from the explorer as text and are shown to the user, not thrown.
A width that is out of range is clamped, never refused.

#### Dependencies

- **ProjectExplorer** — lists, selects, renames, deletes, and creates the project's files and folders.
- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **Chat** (through `ChatPanelViewModel.InspectorLauncher`, `AttachmentPicker`, and `CollapseRequested`) — the workspace
  tells the chat panel which dialog service shows its payload inspector and picks its attachments, and hears when the
  user hides the chat.
- **SettingsService** (the `WindowSettings` record) — the limits and default widths, and the saved layout that
  `WindowPlacement.Resolve` fits.
- **ThemeResources** — the surface colors of the panes, the `SplitterGrip` theme, the icon button theme, and the radius
  and shadow of the restore chip.
- **Avalonia** — the user control, splitter, context menu, and the screens (`PixelRect`) of the window.

#### Callers

- **MainWindowShell** — hosts the workspace as the window content, calls `ShowProject` when the open project changes,
  supplies the dialog service, gives it the saved widths and saves the widths it announces; the main window calls
  `WindowPlacement.Resolve` with the saved layout.
- The `PreviewPane` of the `Preview` subsystem — the application sets `Preview` to it, and it follows the
  explorer's selection; the workspace disposes it with the explorer.
- The `ChatPanel` of the `Chat` subsystem — the application sets `Chat` to it; it is shown in the chat column while a
  project is open, and the composition root disposes it.

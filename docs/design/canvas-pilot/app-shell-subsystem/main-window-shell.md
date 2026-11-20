### MainWindowShell

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`MainWindowShell` is the view model of the main window together with the window (`MainWindowView`) that shows it.
It owns the File, Image, Tools, and Help commands, the window title, and the recent projects menu, and it follows
the open project. It talks to the user only through an `IDialogService`, which the window implements, so the shell
is tested without a window. The small message, confirmation, and prompt dialog that the window shows for
`ShowMessageAsync`, `ConfirmAsync`, and `PromptAsync` (`MessageDialogViewModel` and `MessageDialogView`) belongs to
this unit.

#### Data Model

**Title**: `string` — `{project name} - CanvasPilot` while a project is open, otherwise `CanvasPilot`.

**CurrentProject** / **HasProject**: the open `ProjectInfo` or `null`, and whether there is one.

**Workspace**: `ProjectWorkspaceViewModel` — the content area; the shell owns and disposes it.

**RecentProjects** / **HasRecentProjects**: a read-only collection of `RecentProjectItem` (`Name`, `Header`,
`FilePath`, `OpenCommand`) built from `IRecentProjects.GetRecent`. `Header` doubles underscores so the menu does
not treat them as access keys.

**ImageExporter**: `IImageExporter` of the `Preview` subsystem — `NullImageExporter` unless one is supplied to the
constructor; the application supplies the exporter of the `Preview` subsystem.

**Commands**: `NewProjectCommand`, `OpenProjectCommand`, `SaveProjectCommand` and `CloseProjectCommand` (enabled
while a project is open), `ExitCommand`, `ExportPngCommand` and `CopyImageCommand` (enabled while the exporter can
export), `OpenSettingsCommand`, and `ShowAboutCommand`.

**ExitRequested**: event raised by Exit; the window closes itself in response.

**MessageDialogViewModel**: `Title`, `Message`, `OkText`, `ShowCancel`, `IsPrompt`, `InputText`, `Accepted`, `OkCommand`
(unavailable for a prompt whose entry is blank), `CancelCommand`, and `CloseRequested`.

**Main window**: `MainWindowView` has a `Menu` (`MainMenu`) with the four menus and a host for the workspace. It
implements `IDialogService` with `ShowDialog`, `StorageProvider`, the clipboard, and the message dialog. Its
constructor passes
itself to `AttachDialogService`, and `ExitRequested` closes it.

#### Key Methods

**NewProject**: Builds a `NewProjectDialogViewModel` over the project service with the dialog service as folder
picker and the start folder, shows it, and, when it was accepted, calls `IProjectService.CreateAsync` with the
dialog's folder and name. On success the project file is added to the recent projects and the shell is brought in
line with the project service; on failure the problem is shown.

**OpenProject**: Asks `PickProjectFileAsync` for a file and opens it with `IProjectService.OpenAsync`. A failure
is shown, and when the file no longer exists it is also removed from the recent projects. Choosing an entry of the
Recent Projects menu runs the same operation for that entry.

**SaveProject** / **CloseProject** / **Exit**: Save calls `SaveAsync` and shows a failure. Close calls `Close`.
Exit raises `ExitRequested`.

**OpenSettings**: Shows a `SettingsDialogViewModel` over the settings service and the supplied pages, and tells
the dialog it has closed afterwards so the pages can release resources. **ShowAbout** shows an
`AboutDialogViewModel`.

**ExportPng** / **CopyImage**: When the exporter can export, call it with the dialog service. The commands are
re-evaluated when `CanExportChanged` is raised.

**Start folder**: The folder that holds the most recent project's folder, otherwise the default projects folder
given to the constructor, otherwise none; pickers start there if the folder exists.

**Following the services**: The shell subscribes to `IProjectService.CurrentChanged` and
`IRecentProjects.Changed`. A notification raised on another thread is posted to the synchronization context of the
thread that created the shell. Updating the title and the workspace is idempotent, so the shell also does it after
its own operations. `Dispose` unsubscribes and disposes the workspace.

**Dialog service**: `AttachDialogService` gives the shell, and through it the workspace, the dialog service. Until
it is attached the commands that need the user do nothing.

**Clipboard image**: `IDialogService.CopyImageToClipboardAsync(image)` is implemented by the window: it creates a
bitmap from the picture (`RenderedImageBitmap.Create`), puts it on the window's clipboard with `SetBitmapAsync`,
and flushes the clipboard so the image survives the application exiting. It answers `false` when the platform has
no clipboard or refuses the image.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing collaborator, and `AttachDialogService` does the
same. Problems with projects are results shown in a message dialog, never exceptions. A platform picker that
cannot be shown answers `null`, which the shell reads as cancelled. `MessageDialogViewModel` throws for missing
texts or a blank accepting label.

#### Dependencies

- **ProjectService** (through `IProjectService`) — creates, opens, saves, and closes projects.
- **RecentProjects** (through `IRecentProjects`) — the recent projects menu.
- **NewProjectDialog** — collects a project name and location.
- **SettingsDialog** and **SettingsService** (through `ISettingsService` and `ISettingsPage`) — the settings dialog.
- **ProjectWorkspace** and **AboutDialog** — the content area and the About dialog.
- **ImageExporter** (through `IImageExporter` and `IExportDialogs`) — the Image menu commands run it, and the
  dialog service extends `IExportDialogs` so the exporter can ask the user through it.
- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **Avalonia** — the window, menus, dialogs, storage provider, and clipboard.

#### Callers

- `App` (through `ApplicationComposition`) — creates the shell and shows `MainWindowView` over it.
- `ApplicationComposition` — connects the `Preview` subsystem: `ImageExporter` is passed as the `IImageExporter`,
  and the preview pane is the workspace's preview content.

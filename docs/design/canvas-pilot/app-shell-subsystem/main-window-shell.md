### MainWindowShell

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`MainWindowShell` is the view model of the main window together with the window (`MainWindowView`) that shows it.
It owns the File, Edit, Image, Tools, and Help commands, the window title, and the recent projects menu, and it follows
the open project. It routes Undo and Redo to the part of the window that has the focus and the talk key to the chat
panel. It talks to the user only through an `IDialogService`, which the window implements, so the shell
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

**Commands**: `NewProjectCommand`, `OpenProjectCommand`, `SaveProjectCommand` and `CloseProjectCommand` (enabled while a
project is open), `ExitCommand`, `ExportPngCommand` and `CopyImageCommand` (enabled while the exporter can export),
`OpenSettingsCommand`, `ShowStyleKitsCommand` (enabled while the application has a window that manages style kits),
`ShowDrawingKnowledgeCommand` (enabled while the application has knowledge to show), and `ShowAboutCommand`, and
`UndoCommand` and `RedoCommand` (enabled while `EditTarget` can undo or redo), and `CheckAgainstStyleCommand` (enabled
while a picture can be checked against the style).

**EditTarget**: `IUndoRedoTarget?` (a `Composer` seam) - what Edit > Undo and Redo act on: the part of the window
that has the focus, or `null` while the focus is in a part that has nothing to undo. The window sets it. The shell
follows `UndoRedoStateChanged` of the current target (a notification on another thread is posted to the user
interface thread) and re-evaluates both commands, and it stops following a target that was replaced.

**PushToTalk** / **PushToTalkKey** / **IsPushToTalkActive**: `PushToTalk` is the `IPushToTalkTarget?` that listens
while the talk key is held, which the composition root sets to the chat panel. `PushToTalkKey` is the constant
`Key.F8`. `IsPushToTalkActive` is true between a start and the stop that follows it.

**ExitRequested**: event raised by Exit; the window closes itself in response.

**StylePicker** / **ShowStatusBar**: `StylePicker` is the optional `StylePickerViewModel` of the `Style` subsystem, the
list with which the user chooses the style kit of the project; the composition root supplies it and keeps it, so the
shell does not dispose it. `ShowStatusBar` is true while there is a picker and it is visible, which it is while a
project is open; the shell follows the picker's `IsVisible` and raises the change of `ShowStatusBar`.

**FolderPicker**: the folder picker of the dialog service (`IFolderPicker`), or `null` until the window attached itself.
The composition root gives it to the Style Kits settings page through a function, because the pages are made before the
window exists.

**MessageDialogViewModel**: `Title`, `Message`, `OkText`, `ShowCancel`, `IsPrompt`, `InputText`, `Accepted`, `OkCommand`
(unavailable for a prompt whose entry is blank), `CancelCommand`, and `CloseRequested`.

**Main window**: `MainWindowView` has a `Menu` (`MainMenu`) with the five menus (File, Edit, Image, Tools, Help) and a
host for the workspace. The Edit menu has Undo and Redo, whose `InputGesture` (Ctrl+Z, Ctrl+Y) is only text, because the
message editor handles those keys itself. The window implements `IDialogService` with `ShowDialog`, `StorageProvider`,
the clipboard, and the message dialog. Its constructor passes itself to `AttachDialogService`, and `ExitRequested`
closes it. It also tracks the focus and the talk key (see **Focus tracking** and **Talk key** below). Its Help menu has
`DrawingKnowledgeMenuItem` (`_Drawing Knowledge...`), a separator, and `AboutMenuItem`. The window shows the browser of
the drawing knowledge with `Show` and itself as the owner, so that it does not block the window, and keeps the one that
is open. Its Tools menu has `StyleKitsMenuItem` (`Style _Kits...`) above `SettingsMenuItem`. The window also implements
`IStyleKitDialogs`, which `IDialogService` includes: it shows the window that manages style kits with `Show` and itself
as the owner, one at a time (a request while it is open brings it to the front, restored if it was minimized, and
disposes the view model that was made for the request), shows the editor of a kit modally over that window (or over
itself when that is not open), and shows the pickers of a zip file to import, of a zip file to write, and of example
images (which offers the file types that CanvasNet draws). Below the content the window has a status bar (`StatusBar`, a
`Border` docked at the bottom) whose only content is the style picker (`StylePickerHost`); the bar is shown only while
`ShowStatusBar` is true.

#### Key Methods

**NewProject**: Builds a `NewProjectDialogViewModel` over the project service with the dialog service as folder
picker and the start folder, shows it, and, when it was accepted, calls `IProjectService.CreateAsync` with the
dialog's folder and name. On success the project file is added to the recent projects and the shell is brought in
line with the project service; on failure the problem is shown. When the shell has a style picker and the project
was created, the picker is asked to give the project the default style kit of the user before the shell shows the
project, so a new project says which style it uses from the start; opening a project does not do this.

**OpenProject**: Asks `PickProjectFileAsync` for a file and opens it with `IProjectService.OpenAsync`. A failure
is shown, and when the file no longer exists it is also removed from the recent projects. Choosing an entry of the
Recent Projects menu runs the same operation for that entry.

**SaveProject** / **CloseProject** / **Exit**: Save calls `SaveAsync` and shows a failure. Close calls `Close`.
Exit raises `ExitRequested`.

**OpenSettings**: Shows a `SettingsDialogViewModel` over the settings service and the supplied pages, and tells
the dialog it has closed afterwards so the pages can release resources. **ShowAbout** shows an
`AboutDialogViewModel`.

**ShowStyleKits**: The constructor takes an optional function that makes the `StyleKitManagerViewModel` from an
`IStyleKitDialogs`; the composition root passes the one that builds it over the catalog of kits, the selection, the
render service, and the project service. The command is unavailable when there is no function and does nothing until a
dialog service is attached. Each time it runs it calls the function with the dialog service and gives the view model to
`IStyleKitDialogs.ShowStyleKitManagerAsync`, which completes when the window is shown, not when it is closed.

**ShowDrawingKnowledge**: The constructor takes an optional function that gives the catalog of the drawing knowledge and
an optional logger; the composition root passes the function that gives the catalog of the application, which reads the
cards when it is first called, so the start of the application does not pay for them. The command is unavailable when
there is no function and does nothing until a dialog service is attached. It calls the function, makes a
`KnowledgeBrowserViewModel` over the catalog and a search of its cards, and gives it to
`IKnowledgeBrowserLauncher.ShowKnowledgeBrowserAsync`; the dialog service completes that when the window is shown, not
when it is closed. When the catalog cannot be made, the failure is written to the log and a message dialog says that the
drawing knowledge could not be loaded, and no window is shown. The window keeps one browser: a request while it is open
brings that window to the front, restored if it was minimized, and leaves what the user was reading, and a request after
it was closed shows a new one.

**ExportPng** / **CopyImage**: When the exporter can export, call it with the dialog service. The commands are
re-evaluated when `CanExportChanged` is raised.

**CheckAgainstStyle**: The constructor takes an optional `ICheckAgainstStyle` of the `Style` subsystem. The command
(`CheckAgainstStyleCommand`, the item `CheckAgainstStyleMenuItem`, `Check _Against Style`, last in the Image menu after
a separator) can run only while the check says that `CanCheck`, and the shell re-evaluates it when the check raises
`CanCheckChanged` (on the user interface thread). Running it awaits `RunAsync`, which shows its result among the notes
of the preview itself; when it answers with a sentence (no picture, no style kit, a kit that cannot be found, a check
that failed) the shell shows the sentence in a message dialog titled `Check Against Style`. The shell does not dispose
the check.

**Start folder**: The folder that holds the most recent project's folder, otherwise the default projects folder
given to the constructor, otherwise none; pickers start there if the folder exists.

**Following the services**: The shell subscribes to `IProjectService.CurrentChanged` and
`IRecentProjects.Changed`. A notification raised on another thread is posted to the synchronization context of the
thread that created the shell. Updating the title and the workspace is idempotent, so the shell also does it after
its own operations. `Dispose` unsubscribes and disposes the workspace.

**Dialog service**: `AttachDialogService` gives the shell, and through it the workspace, the dialog service. Until
it is attached the commands that need the user do nothing. The workspace passes it on to the chat panel as the
launcher of the payload inspector, which the window implements with `ShowPayloadInspectorAsync`.

**Undo and Redo**: `Undo` and `Redo` call the same method of `EditTarget`; they can run only while the target
reports a step to undo or redo, and do nothing without a target.

**Focus tracking** (window): a handler on the window for the `GotFocus` routed event, which also receives events
that a control marked as handled, looks at the control that took the focus and then at its parents, and sets
`EditTarget` to the first data context that is an `IUndoRedoTarget`, or to `null` when none is. A control that is
part of a menu is ignored, so choosing a menu item does not change what the item acts on.

**Talk key**: `BeginPushToTalkAsync` remembers the `PushToTalk` target and starts it, and does nothing when there is
no target or a start is already in effect (so the repeats of a held key are ignored). `EndPushToTalkAsync` stops the
target that was started, even when `PushToTalk` was replaced meanwhile, and does nothing when none was started. The
window calls them from tunneling `KeyDown` and `KeyUp` handlers for `PushToTalkKey` (marking the key handled while
there is a target) and from its `Deactivated` event and when it closes, so the microphone is never left open.

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
- **KnowledgeBrowser** and **KnowledgeCatalog** (through `IKnowledgeBrowserLauncher` and `IKnowledgeCatalog`) — what
  the Drawing Knowledge command shows; the dialog service extends the launcher, which the `Knowledge` subsystem owns.
- **ImageExporter** (through `IImageExporter` and `IExportDialogs`) — the Image menu commands run it, and the
  dialog service extends `IExportDialogs` so the exporter can ask the user through it.
- **ExportPresetDialog** (through `IExportDialogs.PickExportPresetAsync`) — the main window shows the Export PNG dialog
  when the exporter asks how to export.
- **StylePicker** (of the `Style` subsystem) - the list in the status bar that the shell hosts and that it asks to give
  a new project the default style kit; the shell does not know the kits.
- **StyleKitManager** (of the `Style` subsystem) - the window that manages style kits, which the shell asks the dialogs
  to show; the dialog service includes `IStyleKitDialogs`, so the same main window shows it, its editor, and its
  pickers.
- **StyleChecker** (through `ICheckAgainstStyle`) - what the Check Against Style command runs; the interface belongs to
  the `Style` subsystem.
- **Composer** (through `IUndoRedoTarget` and `IPushToTalkTarget`) — what the Edit menu and the talk key reach; the
  seams are owned by the `Composer` subsystem and the shell does not know what implements them.
- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **Avalonia** — the window, menus, dialogs, storage provider, and clipboard.

#### Callers

- `App` (through `ApplicationComposition`) — creates the shell and shows `MainWindowView` over it.
- `ApplicationComposition` — connects the `Preview` subsystem: `ImageExporter` is passed as the `IImageExporter`,
  and the preview pane is the workspace's preview content. It sets `PushToTalk` to the chat panel, which the talk
  key of the window then starts and stops.

### MainWindowShell

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`MainWindowShell` is the view model of the main window together with the window (`MainWindowView`) that shows it. It
owns the File, Edit, Image, Tools, and Help commands, the window title, and the recent projects menu, and it follows the
open project. It routes Undo and Redo to the part of the window that has the focus and the talk key to the chat panel.
The window has a title bar that holds the compact menu, the name of the open project, the list that chooses the style
kit, and the AI provider pill; a body that shows the welcome screen while no project is open and the workspace once one
is; and a status bar at the bottom. The shell also remembers the size, place, and state of the window and the widths of
the explorer and the chat column, and applies the thumbnail and row height settings of the Appearance page to the
explorer. It talks to the user only through an `IDialogService`, which the window implements, so the shell is tested
without a window. The small message, confirmation, and prompt dialog that the window shows for `ShowMessageAsync`,
`ConfirmAsync`, and `PromptAsync` (`MessageDialogViewModel` and `MessageDialogView`) belongs to this unit. The status
bar, the provider pill, and the welcome screen are units of their own (see *StatusBar Design*, *ProviderPill Design*,
and *Welcome Design*); this unit creates them and places them in the window.

#### Data Model

**Title**: `string` — `{project name} - CanvasPilot` while a project is open, otherwise `CanvasPilot`. It is the title
that the system shows in the task bar and the list of windows.

**TitleBarName** / **TitleBarSuffix**: `string` — what the title bar of the window shows: the project name (or
`CanvasPilot` while no project is open), which the view sets in bold, and, while a project is open, a middle dot and
the application name (` · CanvasPilot`) in the quiet color; otherwise the suffix is empty. Both follow `CurrentProject`.

**CurrentProject** / **HasProject**: the open `ProjectInfo` or `null`, and whether there is one.

**Workspace**: `ProjectWorkspaceViewModel` — the content area; the shell owns and disposes it.

**StatusBar**: `StatusBarViewModel` — the status bar at the bottom of the window, made over the workspace. It is always
there; the shell disposes it.

**Welcome**: `WelcomeViewModel` — the welcome screen that the window shows instead of the workspace while no project is
open. The shell makes it over itself and disposes it.

**ProviderPill** / **HasProviderPill**: the optional `ProviderPillViewModel` that tells which AI provider is chosen and
whether it can be used, which the composition root sets after it has made the chat panel; the shell disposes it. The
window shows it in the title bar only while there is one.

**RecentProjects** / **HasRecentProjects**: a read-only collection of `RecentProjectItem` built from
`IRecentProjects.GetRecent`, most recent first. An item has the project `Name`, the menu `Header` (which doubles
underscores so the menu does not treat them as access keys), the `FilePath`, and `OpenCommand`; for the cards of the
welcome screen it also carries the time of the last opening (read from `AppSettings.RecentProjectOpened`), the number of
pictures in the project folder, a thumbnail, and a command that removes it from the list (see *Welcome Design*). Only
the first `WelcomeViewModel.MaximumCards` items have their pictures counted and their thumbnail drawn.

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

**StylePicker**: the optional `StylePickerViewModel` of the `Style` subsystem, the list with which the user chooses the
style kit of the project; the composition root supplies it and keeps it, so the shell does not dispose it. The window
shows it, as a pill, in the title bar (`StylePickerHost`); the picker itself is visible only while a project is open.
The shell sets the picker's `ManageKitsCommand` to `ShowStyleKitsCommand`, which is how the last entry of the list opens
the Style Kits window. There is no `ShowStatusBar` property any more: the style list left the status bar, and the status
bar is always shown.

**SavedWindow**: `WindowSettings` — the layout that was saved when the window was last closed (`AppSettings.Window`),
which is the default layout (1440 by 900, the explorer 264 and the chat column 400 wide, no place) when none was saved.
`SaveWindow(width, height, left, top, maximized)` writes a new size, place, and state back, and the widths of the
explorer and the chat column are written when the workspace announces a choice.

**FolderPicker**: the folder picker of the dialog service (`IFolderPicker`), or `null` until the window attached itself.
The composition root gives it to the Style Kits settings page through a function, because the pages are made before the
window exists.

**MessageDialogViewModel**: `Title`, `Message`, `OkText`, `ShowCancel`, `IsPrompt`, `InputText`, `Accepted`, `OkCommand`
(unavailable for a prompt whose entry is blank), `CancelCommand`, and `CloseRequested`. `MessageDialogView` is 480 by
240 with the pane color; the message is body-large primary text, the entry of a prompt has the accessible name `Answer`,
and a footer bar under a subtle line holds the OK button (`PrimaryButton`, the default) and the Cancel button
(`SecondaryButton`, shown only for a confirmation or a prompt). The ids `MessageDialogText`, `MessageDialogInput`,
`MessageDialogOkButton`, and `MessageDialogCancelButton` are unchanged.

**Main window**: `MainWindowView` is a window that is 1440 by 900 at first and cannot be made smaller than 960 by 600,
and that extends its client area into the frame of the system (`ExtendClientAreaToDecorationsHint`, with a title bar
hint of 44 pixels). Its content is a dock: the title bar on top, the status bar at the bottom, and the body in between.
Its menu is `MainMenu`, with the five menus (File, Edit, Image, Tools, Help), and it lives in the title bar. The Edit
menu has Undo and Redo, whose `InputGesture` (Ctrl+Z, Ctrl+Y; Cmd+Z, Cmd+Shift+Z on macOS, chosen with `OnPlatform`) is
only text, because the message editor handles those keys itself. The `HotKey` of New Project, Open Project, and Save
Project is Ctrl+N, Ctrl+O, and Ctrl+S, or Cmd+N, Cmd+O, and Cmd+S on macOS. Every menu and every item has an access key
(the letter after the underscore of its header) that no other entry of the same menu has; the keys are File `F`, Edit
`E`, Image `I`, Tools `T`, Help `H`, and within File `N`, `O`, `R`, `S`, `C`, and `x` (Exit), so that a person can use
the whole menu with the keyboard alone. The window implements `IDialogService` with `ShowDialog`, `StorageProvider`, the
clipboard, and the message dialog; the dialog service includes `IAttachmentPicker` of the `Composer` subsystem, whose
`PickAttachmentsAsync` opens the file picker of the platform ("Attach Files", several files of any type) for the attach
button of the chat panel, which the workspace connects to the chat panel when it attaches the dialog service. Its
constructor passes itself to `AttachDialogService`, and `ExitRequested` closes it. It also tracks the focus and the talk
key (see **Focus tracking** and **Talk key** below). Its Help menu has `DrawingKnowledgeMenuItem`
(`_Drawing Knowledge...`), a separator, and `AboutMenuItem`. The window shows the browser of the drawing knowledge with
`Show` and itself as the owner, so that it does not block the window, and keeps the one that is open. Its Tools menu has
`StyleKitsMenuItem` (`Style _Kits...`) above `SettingsMenuItem`. The window also implements `IStyleKitDialogs`, which
`IDialogService` includes: it shows the window that manages style kits with `Show` and itself as the owner, one at a
time (a request while it is open brings it to the front, restored if it was minimized, and disposes the view model that
was made for the request), shows the editor of a kit modally over that window (or over itself when that is not open),
and shows the pickers of a zip file to import, of a zip file to write, and of example images (which offers the file
types that CanvasNet draws).

**Title bar**: a `Border` (`AppTitleBar`) 44 pixels high with the window surface color and a subtle line under it,
marked as the title bar of the window (`WindowDecorationProperties.ElementRole`) so that the platform treats the empty
part as the title bar (it drags the window, and a double click maximizes it, which is checked by hand on a real window),
while the menu and the style list are marked as user elements that take clicks. When the platform cannot extend the
client area, the same strip is an ordinary row below the title bar of the system. Its grid has seven columns from left
to right: a leading reserve (`TitleBarLeadingReserve`), the brand mark (a 24 by 24 rounded tile with the accent gradient
and the sparkle icon), the menu, the title text (`AppTitleBarText`, centered, with `TitleBarName` in semibold primary
text and `TitleBarSuffix` in tertiary text, trimmed with an ellipsis), the style list host (`StylePickerHost`), the
provider pill host (`ProviderPillHost`), and a trailing reserve (`TitleBarCaptionReserve`). The menu is quiet: no fill,
items with 8 by 4 padding and secondary text, but all its items, keys, and access keys are kept.

**Caption reserve**: the system draws its window buttons over the strip when the client area is extended, so the strip
keeps room for them. `MainWindowView.CaptionReserve(isExtended, isWindows, isMacOs)` is a pure function that returns the
room on the left and on the right: 138 pixels on the right on Windows, 78 pixels on the left on macOS (the traffic
lights), and none elsewhere or when the window is not extended. These are the fallback: on Windows the window then
measures the real room, because the buttons that Avalonia draws over the strip are 45 pixels wide with a gap, and the
theme insets them by the frame and the shadow of the window, so they cover more than 138 pixels at the right edge (and
the full-screen button can add more). `MeasureCaptionButtonsReserve` finds the overlay panel of the drawn window
decorations (`PART_OverlayPanel`) and takes the distance from its left edge to the right edge of the window; the window
sets the trailing reserve to it after it opens, when its size or its state changes, so the pill is never under a
button. The window applies the fallback to the two reserve borders when
`IsExtendedIntoWindowDecorations` changes, so the headless platform, which does not extend, keeps both at width 0. A
real window raises that change while the XAML loads, when the window has no name scope and the reserve controls do not
exist (a name lookup then throws and crashed the published program at the start). So the window ignores the change until
the component is loaded (`_componentReady`), applies the value once at the end of the constructor, and sets the
generated fields through `ApplyCaptionReserve`, which leaves a missing control alone. This gating cannot be tested
in a headless test; it is guarded by the launch smoke test (`smoke.ps1`) and the manual launch check.

**System title text**: on Windows and Linux, Avalonia 12 draws the title of the window at the left of the strip when the
frame is extended, which would lie over the brand mark and the menu. A style in the window hides that text
(WindowDrawnDecorations /template/ Panel#PART_TitleTextPanel > TextBlock); the strip shows its own centered title.

**Body and status bar**: the body is a grid with two siblings that fill it: `WorkspaceHost` (visible while
`HasProject`) with the workspace as content, and `WelcomeHost` (visible while there is no project) with `Welcome`. The
workspace view is made by the view locator when its content is first shown, which is when the first project is opened,
so the workspace is not built before; when the project is closed the workspace view stays but is hidden, and the welcome
screen shows again. The status bar is a `StatusBarView` docked at the bottom over `StatusBar`; it is always there.

**Saved layout** (window): the constructor asks `WindowPlacement.Resolve` (see *ProjectWorkspace Design*) with the saved
layout, the work areas of the screens that are connected, and the scale of the primary screen, and gives the window the
resulting size, state, and, when there is one, a manual place. `OnWindowPositionChanged` remembers the place while the
window is in its normal state, because the place that the system reports while it is maximized is not the place it
returns to. `OnClosing` (when the closing is not canceled) calls `SaveWindow` with the width and the height, the place
of the normal state, and whether the window is maximized.

**Accessibility**: the controls of the window follow the rules that `AccessibilityViewTests` checks over the main window
(with and without a project), the About dialog, the knowledge browser, the New Project dialog, the Style Kits window,
and every page of the settings, in both theme variants: a button or toggle that shows no text has an
`AutomationProperties.Name` and a tooltip, every enabled button can take the focus with the keyboard, such a button is
at least 28 by 28 pixels, and a text field, list, drop-down, slider, or tree has a name (or a placeholder). The chevron
of a tree row is left out on purpose, because the keyboard opens and closes the row itself with the Right and Left keys.

**AutomationIds**: new are `AppTitleBar` (the strip) and `AppTitleBarText` (the title text); `StylePickerHost` and
`ProviderPillHost` are the hosts of the two pills; `WorkspaceHost` and `WelcomeHost` are the two body hosts. `MainMenu`
and every menu and item id are unchanged, and the name of each item equals its identifier. `StatusBar` is now the border
inside the status bar view and `PreviewStatus` a panel in it (see *StatusBar Design*).

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

**Following the services**: The shell subscribes to `IProjectService.CurrentChanged`, `IRecentProjects.Changed`, and
`ISettingsService.Changed`. A notification raised on another thread is posted to the synchronization context of the
thread that created the shell. Updating the title and the workspace is idempotent, so the shell also does it after
its own operations. `Dispose` unsubscribes and disposes the status bar, the provider pill, the welcome screen, the
recent project items, and the workspace.

**Recent projects menu and cards**: `RefreshRecentProjects` reads the list first (reading drops files that are gone and
may announce a change), then makes one `RecentProjectItem` for each file, with the time of the last opening from the
settings, and, for the first `WelcomeViewModel.MaximumCards` only, the count of pictures of the project folder
(`ProjectPictureCounter`) and the request to draw the first picture as the thumbnail (`RecentThumbnailLoader`, over the
optional `IThumbnailSource`, which draws off the user interface thread). The items that are replaced are disposed and
the requests for their pictures are dropped. The command of an item opens its project; its remove command removes the
project from the list only.

**Thumbnail and row settings**: The constructor gives the explorer `AppSettings.Appearance.ShowThumbnails` and
`AppSettings.Appearance.ComfortableRows`, and `OnSettingsChanged` gives it the new values on every change of the
settings, on the user interface thread, so that the choices on the Appearance page take effect in the open window.

**Saving the layout**: `SaveWindow` writes the size (rounded), the place, and the state into `AppSettings.Window`
through `ISettingsService.Update`. The shell also listens to `ProjectWorkspaceViewModel.LayoutChanged` and writes the
widths of the explorer and the chat column when the user chose other ones; at construction it gives the workspace the
saved widths with `RestoreColumnWidths`, which does not announce a choice. A write that fails with an `IOException` is
logged as a warning and never thrown, because the window is closing or the user is dragging a splitter.

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
texts or a blank accepting label. A failure to write the layout of the window or the widths of the columns to the
settings is logged and swallowed (it is an `IOException`), so closing the window and dragging a splitter never fail.
A drawing of a recent project card that fails is handled inside the welcome screen units and leaves the card without a
picture.

#### Dependencies

- **ProjectService** (through `IProjectService`) — creates, opens, saves, and closes projects.
- **RecentProjects** (through `IRecentProjects`) — the recent projects menu and the cards.
- **NewProjectDialog** — collects a project name and location.
- **SettingsDialog** and **SettingsService** (through `ISettingsService` and `ISettingsPage`) — the settings dialog, the
  saved window layout (`WindowSettings`), the time of each opening of a project, and the thumbnail setting.
- **ProjectWorkspace** and **AboutDialog** — the content area and the About dialog; the window also uses
  `WindowPlacement` of the `ProjectWorkspace` unit to fit the saved layout to the screens.
- **StatusBar**, **ProviderPill**, and **Welcome** — the units that the shell creates and the window places at the
  bottom, in the title bar, and in the body.
- **ThemeResources** — every color, radius, type size, shadow, icon, and button look of the window and its dialogs
  comes from the tokens and the control themes.
- **KnowledgeBrowser** and **KnowledgeCatalog** (through `IKnowledgeBrowserLauncher` and `IKnowledgeCatalog`) — what
  the Drawing Knowledge command shows; the dialog service extends the launcher, which the `Knowledge` subsystem owns.
- **ImageExporter** (through `IImageExporter` and `IExportDialogs`) — the Image menu commands run it, and the
  dialog service extends `IExportDialogs` so the exporter can ask the user through it.
- **ExportPresetDialog** (through `IExportDialogs.PickExportPresetAsync`) — the main window shows the Export PNG dialog
  when the exporter asks how to export.
- **StylePicker** (of the `Style` subsystem) - the list in the title bar that the shell hosts and that it asks to give
  a new project the default style kit and to open the Style Kits window; the shell does not know the kits.
- **StyleKitManager** (of the `Style` subsystem) - the window that manages style kits, which the shell asks the dialogs
  to show; the dialog service includes `IStyleKitDialogs`, so the same main window shows it, its editor, and its
  pickers.
- **StyleChecker** (through `ICheckAgainstStyle`) - what the Check Against Style command runs; the interface belongs to
  the `Style` subsystem.
- **Composer** (through `IUndoRedoTarget`, `IPushToTalkTarget`, and `IAttachmentPicker`) — what the Edit menu and the
  talk key reach, and the picker of the files that the attach button asks for; the seams are owned by the `Composer`
  subsystem and the shell does not know what implements them.
- **Preview** (through `IThumbnailSource`) — draws the pictures of the recent project cards; optional.
- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **Avalonia** — the window, menus, dialogs, storage provider, and clipboard.

#### Callers

- `App` (through `ApplicationComposition`) — creates the shell and shows `MainWindowView` over it.
- `ApplicationComposition` — connects the `Preview` subsystem: `ImageExporter` is passed as the `IImageExporter`,
  and the preview pane is the workspace's preview content. It sets `PushToTalk` to the chat panel, which the talk
  key of the window then starts and stops. It also sets `ProviderPill` after it has made the chat panel, and passes
  the style list, the thumbnail source, and the other optional collaborators to the constructor.

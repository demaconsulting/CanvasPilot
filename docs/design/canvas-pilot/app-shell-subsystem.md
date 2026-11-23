## AppShell

![AppShell Structure](AppShellSubsystemView.svg)

### Overview

The `AppShell` subsystem is the part of CanvasPilot the user sees and starts. Its boundary starts with the user's
choices in the main window, its menus, and its dialogs, and ends with calls on the `Project` and `Settings` subsystems
and the files and windows the user sees as a result. It contains seven units: `MainWindowShell`, the main window and its
view model, which owns the File, Edit, Image, Tools, and Help commands, the title bar with the project name, the
window title, the recent projects menu, the Undo and Redo of the focused part, the talk key, and the saved window
layout; `ProjectWorkspace`, the content area with the project explorer, the stage where the image preview is shown, and
the chat column that the user can resize and hide; `StatusBar`, the bar at the bottom that reports the shown picture;
`ProviderPill`, the pill in the title bar that says which AI provider answers and whether it can be used; `Welcome`, the
start screen with the recent project cards that takes the place of the workspace while no project is open;
`AboutDialog`, the window that identifies the application and its open source libraries; and `ThemeResources`, the
design tokens (colors, spacing, radii, type, and shadows) of the dark and the light theme, the vendored line icons, the
shared control themes, and the palettes that make the standard controls follow the brand, from which every view takes
its look. The subsystem also contains the composition root that builds the application's units at startup
(`ApplicationComposition`, started by `App`), the `ViewLocator` that maps a view model to its view, and the thin desktop
head `Program` that starts the Avalonia application. These items have no requirements of their own and are described
here because they only join the units together.

### Interfaces

**Dialog Service**: How the view models ask the user questions, show dialogs, and open platform pickers.

- *Type*: In-process .NET API (`IDialogService`, which extends `IFolderPicker` of the `Project` subsystem,
  `IExportDialogs` of the `Preview` subsystem, `IPayloadInspectorLauncher` of the `Chat` subsystem, and
  `IKnowledgeBrowserLauncher` of the `Knowledge` subsystem).
- *Role*: Provider of the contract; the main window (`MainWindowView`) is the implementation.
- *Contract*: `ShowNewProjectAsync`, `ShowSettingsAsync`, and `ShowAboutAsync` show modal dialogs;
  `PickFolderAsync`, `PickProjectFileAsync`, and `PickSaveFileAsync` return a path or `null` when cancelled;
  `ShowMessageAsync`, `ConfirmAsync`, and `PromptAsync` show a message, ask for confirmation, and ask for a line
  of text; `CopyImageToClipboardAsync` puts a picture on the clipboard and answers whether it succeeded. Every
  other method completes when the user has closed the dialog. `PickSaveFileAsync`, `ShowMessageAsync`, and
  `CopyImageToClipboardAsync` are declared by `IExportDialogs`, which the `Preview` subsystem owns, so the
  image exporter needs nothing of the shell. `ShowPayloadInspectorAsync` is declared by `IPayloadInspectorLauncher`,
  which the `Chat` subsystem owns; the window shows the payload inspector as a dialog that it owns.
  `ShowKnowledgeBrowserAsync` is declared by `IKnowledgeBrowserLauncher`, which the `Knowledge` subsystem owns; the
  window shows the browser of the drawing knowledge as a window that it owns and that does not block it, one at a time,
  and completes the request when the window is shown. `PickAttachmentsAsync` is declared by `IAttachmentPicker`, which
  the `Composer` subsystem owns; the window shows the system file dialog for the Attach button of the chat panel and
  answers the paths that were chosen, or an empty list when it was cancelled or cannot be shown.
- *Constraints*: A platform picker that cannot be shown answers `null`, which reads as cancelled.

**Image Exporter**: The extension point behind the Image menu.

- *Type*: In-process .NET API (`IImageExporter` and `NullImageExporter` of the `Preview` subsystem).
- *Role*: Consumer of the contract; the `ImageExporter` of the `Preview` subsystem is the implementation, and the
  shell uses `NullImageExporter` when none is supplied.
- *Contract*: `CanExport` and `CanExportChanged` say whether an image can be exported or copied;
  `ExportPngAsync(dialogs)` and `CopyImageAsync(dialogs)` perform the operations using the dialogs, which the
  shell passes as its dialog service.

**Preview Content**: The extension point in the workspace.

- *Type*: In-process .NET API (`ProjectWorkspaceViewModel.Preview`, an object property).
- *Role*: Provider; the preview pane supplies a view model and the view locator shows its view.
- *Contract*: While `Preview` is `null` the workspace shows a placeholder; otherwise it shows the content.

**Project, Recent Projects, and Settings APIs**: What the shell uses.

- *Type*: In-process .NET APIs (`IProjectService`, `IRecentProjects`, `ISettingsService`, `ISettingsPage`).
- *Role*: Consumer.

**Undo and Redo Target**: What the Edit menu acts on.

- *Type*: In-process .NET API (`IUndoRedoTarget` of the `Composer` subsystem; `MainWindowShell.EditTarget`).
- *Role*: Consumer; the composer of the message is the implementation.
- *Contract*: `CanUndo`, `CanRedo`, `UndoRedoStateChanged`, `Undo`, and `Redo`. The window sets `EditTarget` to the
  nearest data context of the control that has the focus that implements the interface, or to `null`.

**Push-to-Talk Target**: What the talk key starts and stops.

- *Type*: In-process .NET API (`IPushToTalkTarget` of the `Composer` subsystem; `MainWindowShell.PushToTalk`).
- *Role*: Consumer; the chat panel is the implementation.
- *Contract*: `BeginPushToTalkAsync` when the F8 key goes down and `EndPushToTalkAsync` when it goes up, the window
  closes, or the window is no longer the active one; a held key's repeats are ignored.

**Main Window and Dialogs**: The user interface.

- *Type*: Avalonia windows (`MainWindowView`, `AboutDialogView`, `MessageDialogView`) and a control
  (`ProjectWorkspaceView`).
- *Role*: Provider.
- *Contract*: The main window has a File, Edit, Image, Tools, and Help menu; every menu item has a name and an
  automation identifier that are the same (`FileMenu`, `NewProjectMenuItem`, `OpenProjectMenuItem`,
  `RecentProjectsMenuItem`, `SaveProjectMenuItem`, `CloseProjectMenuItem`, `ExitMenuItem`, `EditMenu`, `UndoMenuItem`,
  `RedoMenuItem`, `ImageMenu`, `ExportPngMenuItem`, `CopyImageMenuItem`, `CheckAgainstStyleMenuItem`, `ToolsMenu`,
  `StyleKitsMenuItem`, `SettingsMenuItem`, `HelpMenu`, `DrawingKnowledgeMenuItem`, `AboutMenuItem`). The menu sits in
  a title bar (`AppTitleBar`, 44 pixels high, with the project name in bold and the application name after it,
  `AppTitleBarText`) that also holds the style pill (`StylePicker`) and the AI provider pill (`ProviderPill`) while a
  project is open and leaves room for the buttons of the system. The window opens 1440 by 900 at first and is never
  smaller than 960 by 600, or at the size, place, and state that the user left, fitted to the screens. While no
  project is open the content area shows the welcome screen (`WelcomeView`); while a project is open it shows the
  workspace, with the explorer on the left (264 wide at first), the stage with the preview in the middle, a chat column
  on the right (400 wide at first), and between them splitters with a one pixel line and a nine pixel hit area. A
  status bar of one height (`StatusBar`, 26 pixels) is always shown at the bottom.

### Design

0. The application merges the dictionary of design tokens of `ThemeResources` after the Fluent theme and sets the
   palettes of the theme for each variant (`App.axaml`), so the standard controls follow the brand accent and the pane
   surface, and the application asks for the theme variant of the system (`RequestedThemeVariant=Default`), so it is
   dark or light as the system is unless the settings choose a theme (the composition root makes a
   `ThemeVariantApplier`, which shows the choice of the settings and follows its changes). Views take colors from the
   tokens with dynamic resources, and draw icons with a `PathIcon` of class `icon` over the vendored line glyphs of
   `ThemeResources`; buttons, toggles, chips, and splitters take the control themes of `ThemeResources`, which show the
   keyboard focus with a ring. The project copies the notice of those glyphs (`THIRD-PARTY-NOTICES.md`) beside the
   executable, so the installer ships it.
1. At startup `App` calls `ApplicationComposition.CreateDefault`, which creates the logger, the settings service (and
   loads the settings), the project service, the recent projects list, the project explorer with its image source filter
   and refresh scheduler, the render service, the preview pane, the workspace (which shows the preview pane), the image
   exporter, the settings pages (the AI Provider page, the Speech page over the speech services, which use neither the
   audio system nor the model store until the Speech page is loaded, and the Style Kits page over the catalog of style
   kits, which gets the folder picker from the shell once the window has attached itself), and the shell, in that order,
   and logs the start. The shell is given a function that gives the catalog of the drawing knowledge, which reads the
   cards the first time the Help menu asks for them. The speech model service that the composition creates is disposed
   with the composition. After the shell, the composition makes the mark registry, the composer of the message, the
   layer on which the user marks the picture, and the chat panel. It puts the layer in the overlay slot of the preview
   pane and the layer's tool bar in the toolbar slot that floats at the top center of the stage, and gives the layer the
   switch for dragging the picture out. The chat panel joins the layer to the message: it allows marks while the
   composer can edit, tells the layer which mark chips the message holds whenever the composer changes, and adds the
   chip of each mark that the layer reports, so that an undo of the chip hides the mark. The composition places the
   panel in the chat column of the workspace and makes it the push-to-talk target of the shell; the workspace shows the
   column while a project is open and makes the window's dialog service the panel's launcher of the payload inspector.
   The style kits are built before the image exporter and the shell: the catalog of kits, the selection that says which
   kit the open project uses (which is also the exporter's source of export sizes), the picker that chooses it, and the
   descriptors of the two style tools. The composition starts reading the kits on a worker and does not wait for it, so
   the start of the application never waits for a folder; the catalog, the selection, and the picker are disposed before
   the services they listen to. The shell is given the picker, which it shows as a pill in the title bar while a project
   is open and asks to give a new project the default style kit. The composition also makes the check of the shown
   picture against the style (`StyleCheckCommand`, over the preview pane as the source of pictures and as the sink of
   notes) and gives it to the shell for the Check Against Style item of the Image menu. It gives the shell, too, a
   function that makes the view model of the window that manages style kits (over the catalog, the selection, the render
   service, and the project service) from the dialogs that the window asks through, for the Style Kits item of the Tools
   menu. The composition also makes the `ThumbnailProvider` of the `Preview` subsystem over the render service, gives it
   to the project explorer (which asks for the small pictures of its rows) and to the shell (which loads the pictures of
   the recent project cards with it), owns it, and disposes it with the other units; the shell switches the small
   pictures of the explorer on or off as the Appearance page of the settings says, at the start and whenever the
   settings change. After the chat panel exists the composition makes the `ProviderPillViewModel` over the chat panel
   (which holds the result of the readiness check), the settings, and the settings command of the shell, and gives it to
   the shell, which shows it in the title bar and disposes it. The shell itself makes the `StatusBarViewModel` over the
   workspace (which holds the preview pane and the open project), the `RecentThumbnailLoader` over the thumbnail
   provider, and the `WelcomeViewModel` over itself, so that the welcome screen shows its recent projects and commands;
   the window shows the welcome screen and the workspace as siblings in one area and shows the welcome screen while no
   project is open, and the workspace view is not built until the first project is opened. Last, the composition makes
   the `ThemeVariantApplier` that shows the theme choice of the settings. The window layout is kept by the shell and the
   window together: the window applies the saved size, place, and state through `WindowPlacement.Resolve` when it is
   built, which fits them to the screens that are connected, the workspace restores the saved widths of the explorer and
   the chat column through `WorkspaceLayout.Fit`, and the window saves them when the splitters have been moved and when
   it closes (see the *MainWindowShell* and *ProjectWorkspace* designs).
2. `App` creates the `MainWindowView` over the shell. The window passes itself to the shell as the dialog
   service. It follows the focus (the nearest data context that can undo and redo becomes the shell's `EditTarget`)
   and the F8 key (start and stop of the push-to-talk target, also on deactivation and close). `App` records
   unhandled errors in the log and disposes the composition when the application exits.
3. The shell follows the project service: when the open project changes it updates the title and shows the
   project's folder in the workspace, which opens or closes the explorer. Changes raised on another thread are
   posted to the user interface thread.
4. The File, Edit, Image, Tools, and Help commands are methods of the shell that talk to the user through the dialog
   service. The workspace gets the same dialog service for the confirmed delete and the rename prompt.
5. The view locator maps a view model `XxxViewModel` to the view `XxxView` of the same namespace, so the workspace
   shows the explorer and the preview without referencing their views.
6. The composition builds the AI copilot only when it is given the folder for the local data of the application
   (`localDataRoot`; `CreateDefault` passes the local application data folder of the user, and a test that does not
   give it gets no copilot and touches nothing of the computer). It makes the folder of the chat history from it
   (`CanvasPilot\history`, or the `historyRoot` it is given), so that neither the Copilot runtime data nor the history
   is ever a place in a project. From there it builds, in this order, the process runner, the file system and the
   environment reader that the locator and the readiness check use (each replaceable by a test), one
   `CopilotCliLocator` shared by the readiness check and the factory, one `CopilotRuntimeStarter` (its data folder is
   `CanvasPilot\copilot` below the local data root, or the starter that a test passes as `copilotStarter`) that the
   readiness check and the factory share, `AgentReadiness`, `ProjectTools` (over the render
   service and the active style), `AgentToolRegistry` (over `KnowledgeTools.CreateAll` of the catalog of the drawing
   knowledge, the two style tool descriptors that the composition already holds, and the packs of the project tools),
   `AgentInstructions` (over the prompt builder of the same catalog and the role prompt that comes with the program),
   the factory of the agents (`AgentProviderFactory` over the locator and the shared starter, which it owns when the
   composition made the starter, or the `IAgentFactory`
   that a test passes, in which case the composition stops the starter that it made itself and never one that a test
   passed), and `AgentService`. It gives `AgentReadiness` to the AI Provider page as its `IProviderCheck`
   (the Check connection button), and the service, the `ChatHistoryStore`, and the project service to the chat panel.
   A small inner class, `CopilotWiring`, joins the rest: a file that the service reports as written is shown by
   refreshing the project explorer and selecting the file, which makes the preview draw it; the chat panel's request for
   the settings runs the settings command of the shell; and a change of the AI provider settings (and only of those)
   makes the chat panel check the provider again. The three run on the thread of the user interface that created the
   composition, or on the calling thread when there was none, and a failure is logged and never reaches the copilot.
   The wiring is disposed first, then the copilot (which stops its turn, releases its conversation, and disposes the
   factory), and then the style units, so the copilot stops listening to the project service and the settings service
   before they go.

The composition is deliberately plain construction code with no container, so the dependencies of every unit are
visible in one place. System tests call the same composition with temporary folders and no window.

### Dependencies

The dependencies of the units come from the SysML2 model (`sysml2tools query dependencies`) and are split into three
views. The hub suppliers `ProjectService` and `RollingFileLogger` are left out; they are described with the `Project`
and `Logging` subsystems. The
composition root is not a unit of the model, so what it creates and joins (the units of the `Agent` subsystem, the
chat history store, and the readiness check that the AI Provider page asks) is described in the design above and not
as dependency edges of the model; the units themselves state what they depend on, for example `ChatPanel` on the
contract of the `Agent` subsystem.

The first view shows what the main window and the workspace host. `MainWindowShell` depends on `ProjectWorkspace`,
`AboutDialog`, `PayloadInspector` (`Chat`), `KnowledgeBrowser` (`Knowledge`), and `ComposerViewModel` (`Composer`);
`ProjectWorkspace` depends on `ProjectExplorer` (`Project`) and `ChatPanel` (`Chat`).

![AppShell workspace dependencies](AppShellSubsystemWorkspaceView.svg)

The second view shows what the window shows around the work. `MainWindowShell` depends on `StatusBar`, `ProviderPill`,
and `Welcome`, which it creates and holds, on `SettingsService` (`Settings`), which it saves the window layout in and
follows, and on `ThemeResources`; `StatusBar` depends on `ProjectWorkspace` and `PreviewPane` (`Preview`), whose picture
and project it reports; `ProviderPill` depends on `ChatPanel` (`Chat`), the contract of the `Agent` subsystem, and
`SettingsService`; and `Welcome` depends on `ProviderPill` and `ProjectExplorer` (`Project`). The three units do not
depend on `MainWindowShell` in the model, because it creates them: they receive what they need through their
constructors and the commands that the shell gives them.

![AppShell chrome dependencies](AppShellSubsystemChromeView.svg)

The third view shows the dialogs and actions that the main window opens. `MainWindowShell` depends on
`RecentProjects` and `NewProjectDialog` (`Project`), `SettingsDialog` (`Settings`), `ImageExporter` and
`ExportPresetDialog` (`Preview`), and `StylePicker`, `StyleKitManager`, and `StyleChecker` (`Style`).

![AppShell dialog and action dependencies](AppShellSubsystemActionsView.svg)

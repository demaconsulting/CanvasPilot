## AppShell

![AppShell Structure](AppShellSubsystemView.svg)

### Overview

The `AppShell` subsystem is the part of CanvasPilot the user sees and starts. Its boundary starts with the
user's choices in the main window, its menus, and its dialogs, and ends with calls on the `Project` and
`Settings` subsystems and the files and windows the user sees as a result. It contains three units:
`MainWindowShell`, the main window and its view model, which owns the File, Image, Tools, and Help commands, the
window title, and the recent projects menu; `ProjectWorkspace`, the content area that holds the project explorer
and the place where the image preview is shown; and `AboutDialog`, the window that identifies the application and
its open source libraries. The subsystem also contains the composition root that builds the application's units at
startup (`ApplicationComposition`, started by `App`), the `ViewLocator` that maps a view model to its view, and the
thin desktop head `Program` that starts the Avalonia application. These items have no requirements of their own
and are described here because they only join the units together.

### Interfaces

**Dialog Service**: How the view models ask the user questions, show dialogs, and open platform pickers.

- *Type*: In-process .NET API (`IDialogService`, which extends `IFolderPicker` of the `Project` subsystem).
- *Role*: Provider of the contract; the main window (`MainWindowView`) is the implementation.
- *Contract*: `ShowNewProjectAsync`, `ShowSettingsAsync`, and `ShowAboutAsync` show modal dialogs;
  `PickFolderAsync`, `PickProjectFileAsync`, and `PickSaveFileAsync` return a path or `null` when cancelled;
  `ShowMessageAsync`, `ConfirmAsync`, and `PromptAsync` show a message, ask for confirmation, and ask for a line
  of text. Every method completes when the user has closed the dialog.
- *Constraints*: A platform picker that cannot be shown answers `null`, which reads as cancelled.

**Image Exporter**: The extension point behind the Image menu.

- *Type*: In-process .NET API (`IImageExporter`).
- *Role*: Provider of the contract; the image preview is expected to supply the implementation. The shell uses
  `NullImageExporter` until then.
- *Contract*: `CanExport` and `CanExportChanged` say whether an image can be exported or copied;
  `ExportPngAsync(dialogs)` and `CopyImageAsync(dialogs)` perform the operations using the dialog service.

**Preview Content**: The extension point in the workspace.

- *Type*: In-process .NET API (`ProjectWorkspaceViewModel.Preview`, an object property).
- *Role*: Provider; the image preview supplies a view model and the view locator shows its view.
- *Contract*: While `Preview` is `null` the workspace shows a placeholder; otherwise it shows the content.

**Project, Recent Projects, and Settings APIs**: What the shell uses.

- *Type*: In-process .NET APIs (`IProjectService`, `IRecentProjects`, `ISettingsService`, `ISettingsPage`).
- *Role*: Consumer.

**Main Window and Dialogs**: The user interface.

- *Type*: Avalonia windows (`MainWindowView`, `AboutDialogView`, `MessageDialogView`) and a control
  (`ProjectWorkspaceView`).
- *Role*: Provider.
- *Contract*: The main window has a File, Image, Tools, and Help menu; every menu item has a name and an
  automation identifier that are the same (`FileMenu`, `NewProjectMenuItem`, `OpenProjectMenuItem`,
  `RecentProjectsMenuItem`, `SaveProjectMenuItem`, `CloseProjectMenuItem`, `ExitMenuItem`, `ImageMenu`,
  `ExportPngMenuItem`, `CopyImageMenuItem`, `ToolsMenu`, `SettingsMenuItem`, `HelpMenu`, `AboutMenuItem`).

### Design

1. At startup `App` calls `ApplicationComposition.CreateDefault`, which creates the logger, the settings service
   (and loads the settings), the project service, the recent projects list, the project explorer with its image
   source filter and refresh scheduler, the workspace, and the shell, in that order, and logs the start.
2. `App` creates the `MainWindowView` over the shell. The window passes itself to the shell as the dialog
   service. `App` records unhandled errors in the log and disposes the composition when the application exits.
3. The shell follows the project service: when the open project changes it updates the title and shows the
   project's folder in the workspace, which opens or closes the explorer. Changes raised on another thread are
   posted to the user interface thread.
4. The File, Image, Tools, and Help commands are methods of the shell that talk to the user through the dialog
   service. The workspace gets the same dialog service for the confirmed delete and the rename prompt.
5. The view locator maps a view model `XxxViewModel` to the view `XxxView` of the same namespace, so the workspace
   shows the explorer, and later the preview, without referencing their views.

The composition is deliberately plain construction code with no container, so the dependencies of every unit are
visible in one place. System tests call the same composition with temporary folders and no window.

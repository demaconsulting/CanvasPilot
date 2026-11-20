## Project

![Project Structure](ProjectSubsystemView.svg)

### Overview

The `Project` subsystem manages the user's projects. A project is a folder that holds a
`{name}.canvaspilot` JSON project file, a hidden `.canvaspilot` folder reserved for future application data,
and the image source files the user works on. Its boundary starts with names, folders, and project files
chosen by the user and ends with those folders and files on disk, the recent projects list in the
application settings, and the list of image source files shown to the user. It contains four units:
`ProjectService`, which creates, opens, saves, and closes projects; `RecentProjects`, the list of recently
opened project files; `NewProjectDialog`, the window in which the user chooses a name and location; and
`ProjectExplorer`, the list of image source files in the open project's folder. Wiring these units into the
main window, including the folder picker and the confirmation of deletions, belongs to the application
shell, which supplies the folder picker and the refresh scheduler.

### Interfaces

**Project API**: The in-process interface through which other subsystems manage projects.

- *Type*: In-process .NET API (`IProjectService`).
- *Role*: Provider.
- *Contract*: `CreateAsync(parentFolder, name)`, `OpenAsync(projectFile)`, and `SaveAsync()` return a
  `ProjectResult` that holds the `ProjectInfo` or a user-readable error; `Close()` closes the project;
  `Current` is the open project or `null`; `CurrentChanged` is raised after the open project changes;
  `ValidateProjectName`, `ValidateParentFolder`, and `ValidateNewProject` return a problem text or `null`.
- *Constraints*: Ordinary user problems are returned, never thrown. A failed open leaves the open project
  unchanged.

**Recent Projects API**: The in-process interface through which the application reads and updates the list.

- *Type*: In-process .NET API (`IRecentProjects`).
- *Role*: Provider.
- *Contract*: `GetRecent()` returns the existing project files, most recent first; `Add`, `Remove`, and
  `Clear` change the list and return whether it was stored; `Changed` is raised after a change.
- *Constraints*: At most ten entries.

**Project File**: The local persistent store for one project.

- *Type*: File system.
- *Role*: Consumer.
- *Contract*: A UTF-8 JSON file `{name}.canvaspilot` with `formatVersion` (1), `name`, `createdUtc`, and an
  optional `modifiedUtc`. Reading ignores unknown properties and keeps them when the file is saved. The
  hidden `.canvaspilot` folder is created empty.
- *Constraints*: File paths are resolved with the `Utilities` subsystem. Problems are reported through the
  `Logging` subsystem.

**Recent Projects Store**: The persisted list.

- *Type*: In-process .NET API (`ISettingsService`) of the `Settings` subsystem.
- *Role*: Consumer.
- *Contract*: The list is the `RecentProjects` value of `AppSettings`, changed through `Update`.
- *Constraints*: Other settings are never changed by the list.

**Folder Picker**: How the New Project dialog asks the user to choose a folder.

- *Type*: In-process .NET API (`IFolderPicker`).
- *Role*: Consumer; implemented by the application's main window.
- *Contract*: `PickFolderAsync(initialFolder)` returns the chosen folder or `null` when cancelled.

**Image Source Filter and Refresh Scheduler**: How the project explorer decides which files to list and when
to refresh.

- *Type*: In-process .NET APIs (`IImageSourceFilter`, `IRefreshScheduler`).
- *Role*: Consumer; the application supplies `CanvasNetImageSourceFilter` and `DispatcherRefreshScheduler`.
- *Contract*: `IsImageSource(fileName)` says whether a file is listed; `Schedule(delay, callback)` runs a
  refresh on the user interface thread after a delay unless cancelled.

**New Project Dialog and Project Explorer**: The user interface.

- *Type*: Avalonia window (`NewProjectDialogView`) shown modally by its caller, and Avalonia control
  (`ProjectExplorerView`) hosted by the main window.
- *Role*: Provider.
- *Contract*: The dialog has a name entry, a location entry with a Browse button, validation messages, and
  OK and Cancel buttons. The explorer is a list of file names with an empty-state message and an error line.

### Design

1. The application composes one `ProjectService` and one `RecentProjects` over the `SettingsService`.
2. To create a project, the caller builds a `NewProjectDialogViewModel` from the service and a folder picker
   and shows a `NewProjectDialogView`. When the dialog was accepted, the caller calls `CreateAsync` with
   the dialog's name and folder and, on success, adds the project file to `RecentProjects`.
3. To open a project, the caller calls `OpenAsync`; on success the project file is added to
   `RecentProjects`, and a failure message is shown to the user.
4. When the open project changes, the caller opens the project folder in `ProjectExplorerViewModel`, or closes
   the explorer when no project is open. The explorer lists the image source files in the root of the
   folder, watches the folder, and refreshes after a short delay when files change.
5. The user selects a file in the explorer; the caller reads `SelectedFile`. The caller asks the user to
   confirm before calling `DeleteSelected` and prompts for a new name before calling `RenameSelected`.

The `NewProjectDialog` and `ProjectExplorer` are bound to their views through the application's view locator,
which maps a view model `XxxViewModel` to the view `XxxView` in the same namespace. The explorer uses the
extension list of the CanvasNet viewer library to decide which files are image sources; that library is the
only third-party item the subsystem consumes beyond Avalonia and CommunityToolkit.Mvvm.

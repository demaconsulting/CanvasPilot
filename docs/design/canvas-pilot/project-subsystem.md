## Project

![Project Structure](ProjectSubsystemView.svg)

### Overview

The `Project` subsystem manages the user's projects. A project is a folder that holds a `{name}.canvaspilot` JSON
project file, a hidden `.canvaspilot` folder for application data (the pictures that the user pastes into a message
while it is a draft), and the image source files the user works on. Its boundary starts with names, folders, and project
files chosen by the user and ends with those folders and files on disk, the recent projects list in the application
settings, and the tree of folders and files shown to the user. It contains four units: `ProjectService`, which creates,
opens, saves, and closes projects; `RecentProjects`, the list of recently opened project files; `NewProjectDialog`, the
window in which the user chooses a name and location; and `ProjectExplorer`, the tree of folders and files of the open
project, with the actions that create, add, rename, and delete files. Wiring these units into the main window, including
the folder picker and the confirmation of deletions, belongs to the application shell, which supplies the folder picker,
the refresh scheduler, and the source of thumbnails.

### Interfaces

**Project API**: The in-process interface through which other subsystems manage projects.

- *Type*: In-process .NET API (`IProjectService`).
- *Role*: Provider.
- *Contract*: `CreateAsync(parentFolder, name)`, `OpenAsync(projectFile)`, and `SaveAsync()` return a `ProjectResult`
  that holds the `ProjectInfo` or a user-readable error; `SetActiveStyleKitAsync(id)` keeps the id of the style kit of
  the project (or none) in the project file and returns the same kind of result; `Close()` closes the project; `Current`
  is the open project or `null`; `CurrentChanged` is raised after the open project changes, and after the style kit that
  it uses changes; `ValidateProjectName`, `ValidateParentFolder`, and `ValidateNewProject` return a problem text or
  `null`.
- *Constraints*: Ordinary user problems are returned, never thrown. A failed open leaves the open project
  unchanged.

**Recent Projects API**: The in-process interface through which the application reads and updates the list.

- *Type*: In-process .NET API (`IRecentProjects`).
- *Role*: Provider.
- *Contract*: `GetRecent()` returns the existing project files, most recent first; `Add` puts a project at the top and
  records the time of the opening, `Remove` forgets a project and its time, and `Clear` empties the list; all three
  return whether it was stored; `Changed` is raised after a change.
- *Constraints*: At most ten entries.

**Project File**: The local persistent store for one project.

- *Type*: File system.
- *Role*: Consumer.
- *Contract*: A UTF-8 JSON file `{name}.canvaspilot` with `formatVersion` (1), `name`, `createdUtc`, an
  optional `modifiedUtc`, and an optional `activeStyleKit` (the id of the style kit the project uses; absent for no
  style). Reading ignores unknown properties and keeps them when the file is saved. The
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

**Image Source Filter, Refresh Scheduler, and Thumbnail Source**: How the project explorer decides which files to list,
when to refresh, and where the small pictures of the drawings come from.

- *Type*: In-process .NET APIs (`IImageSourceFilter`, `IRefreshScheduler`, `IThumbnailSource`).
- *Role*: Consumer; the application supplies `CanvasNetImageSourceFilter`, `DispatcherRefreshScheduler`, and, from the
  `Preview` subsystem, `ThumbnailProvider`.
- *Contract*: `IsImageSource(fileName)` says whether a file is drawn and listed; `Schedule(delay, callback)` runs a
  refresh on the user interface thread after a delay unless cancelled; `CreateAsync(filePath, projectFolder, token)`
  makes, off the user interface thread, a new bitmap that the caller owns, or gives `null` for a file that cannot be
  drawn. The thumbnail source is optional: without one the explorer shows no pictures.

**New Project Dialog and Project Explorer**: The user interface.

- *Type*: Avalonia window (`NewProjectDialogView`) shown modally by its caller, and Avalonia control
  (`ProjectExplorerView`) hosted by the main window.
- *Role*: Provider.
- *Contract*: The dialog has a name entry, a location entry with a Browse button, validation messages, and OK and Cancel
  buttons in a footer bar. The explorer has a header with the label EXPLORER and the buttons New file, New folder, and
  Refresh, a tree of folders and files with an empty-state message, an error line, and a drop zone for pictures and PDF
  files. The view model exposes the tree (`Roots`), the selected node and file, the list of drawn files (`Items`),
  `CreateFile`, `CreateFolder`, `AddFiles`, `DeleteSelected`, `RenameSelected`, and the thumbnail setting
  (`ShowThumbnails`). The application shell sets the commands behind New file and New folder, because it asks the user
  for the names.

### Design

1. The application composes one `ProjectService` and one `RecentProjects` over the `SettingsService`.
2. To create a project, the caller builds a `NewProjectDialogViewModel` from the service and a folder picker
   and shows a `NewProjectDialogView`. When the dialog was accepted, the caller calls `CreateAsync` with
   the dialog's name and folder and, on success, adds the project file to `RecentProjects`.
3. To open a project, the caller calls `OpenAsync`; on success the project file is added to
   `RecentProjects`, and a failure message is shown to the user.
4. When the open project changes, the caller opens the project folder in `ProjectExplorerViewModel`, or closes
   the explorer when no project is open. The explorer shows the folders of the project and in them the image source
   files and the assets as a tree, watches the folder and its sub-folders, and refreshes after a short delay when files
   change. It asks the thumbnail source for a small picture of each drawing whose row can be seen.
5. The user selects a node in the explorer; the caller reads `SelectedFile`, which is a drawing or none (a folder or an
   asset selects no file). The caller asks the user to confirm before calling `DeleteSelected` and prompts for a new
   name before calling `RenameSelected`. The buttons of the explorer header call `CreateFile` and `CreateFolder` through
   commands that the caller sets after it has prompted for the name, and files that the user drops on the explorer are
   copied by `AddFiles`.

The `NewProjectDialog` and `ProjectExplorer` are bound to their views through the application's view locator, which maps
a view model `XxxViewModel` to the view `XxxView` in the same namespace. Their looks come from the design tokens and
control themes of the `AppShell` subsystem (`ThemeResources`). The explorer uses the extension list of the CanvasNet
viewer library to decide which files are image sources (see the CanvasNet OTS design); that library is the only
third-party item the subsystem consumes beyond Avalonia and CommunityToolkit.Mvvm. Drawing the selected file, and making
the small pictures of the files, is the job of the `Preview` subsystem; the explorer asks for the pictures only through
`IThumbnailSource` and never draws a file itself. It names the copies of added files with `PathHelpers.UniqueFileName`
of the `Utilities` subsystem.

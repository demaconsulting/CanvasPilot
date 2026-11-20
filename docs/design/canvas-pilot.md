# CanvasPilot

![CanvasPilot Structure](CanvasPilotView.svg)

## Architecture

CanvasPilot is a desktop application built on .NET and Avalonia. It is structured as one system. The
software items documented are eight subsystems, `AppShell`, `Composer`, `Logging`, `Preview`, `Project`, `Settings`,
`Speech`, and `Utilities`.

```mermaid
flowchart TD
    subgraph AppShell
        MainWindowShell
        ProjectWorkspace
        AboutDialog
    end
    subgraph Composer
        ComposerDocument
        ComposerHistory
    end
    subgraph Logging
        RollingFileLogger
    end
    subgraph Preview
        ImageRenderService
        PreviewPane
        ImageExporter
    end
    subgraph Project
        ProjectService
        RecentProjects
        NewProjectDialog
        ProjectExplorer
    end
    subgraph Settings
        SettingsService
        SettingsDialog
        AiProviderSettingsPage
    end
    subgraph Speech
        AudioDeviceService
        SpeechModelService
        DeviceSelection
        ModelCatalogPanel
        ModelParameterEditor
        SpeechSettingsPage
    end
    subgraph Utilities
        PathHelpers
    end
    RollingFileLogger --> PathHelpers
    ComposerHistory --> ComposerDocument    ImageRenderService --> RollingFileLogger
    PreviewPane --> ImageRenderService
    PreviewPane --> ProjectExplorer
    PreviewPane --> ProjectService
    PreviewPane --> RollingFileLogger
    ImageExporter --> ImageRenderService
    ImageExporter --> RollingFileLogger
    ProjectService --> PathHelpers
    ProjectService --> RollingFileLogger
    RecentProjects --> SettingsService
    NewProjectDialog --> ProjectService
    NewProjectDialog --> PathHelpers
    ProjectExplorer --> ProjectService
    ProjectExplorer --> PathHelpers
    ProjectExplorer --> RollingFileLogger
    SettingsService --> PathHelpers
    SettingsService --> RollingFileLogger
    SettingsDialog --> SettingsService
    AiProviderSettingsPage --> SettingsDialog
    AudioDeviceService --> RollingFileLogger
    SpeechModelService --> RollingFileLogger
    DeviceSelection --> AudioDeviceService
    ModelCatalogPanel --> SpeechModelService
    SpeechSettingsPage --> SettingsDialog
    SpeechSettingsPage --> DeviceSelection
    SpeechSettingsPage --> ModelCatalogPanel
    SpeechSettingsPage --> ModelParameterEditor
    MainWindowShell --> ProjectService
    MainWindowShell --> RecentProjects
    MainWindowShell --> NewProjectDialog
    MainWindowShell --> SettingsDialog
    MainWindowShell --> ProjectWorkspace
    MainWindowShell --> ImageExporter
    MainWindowShell --> AboutDialog
    ProjectWorkspace --> ProjectExplorer
```

`PathHelpers` provides safe path combination and has no dependencies on other local items.
`RollingFileLogger` is the application's only log writer. It uses `PathHelpers` to resolve the active
log file and archive file paths inside its log folder, so that a malformed file name can never place a
log file outside that folder.

`SettingsService` owns the persisted settings and is the only item that reads or writes the settings file.
`SettingsDialog` hosts settings pages through a page contract and saves them through the service;
`AiProviderSettingsPage` is the page for the AI provider choices and implements that contract. The dialog
receives its pages from the application, so the `Settings` subsystem does not depend on the subsystems that
contribute pages. The AI provider choices are stored only; no agent or provider client is part of the
current build, and one added later reads the stored choices through the service.

`ProjectService` owns projects: a project is a folder holding a `{name}.canvaspilot` project file, a hidden
`.canvaspilot` folder reserved for future application data, and the image source files. It is the only item that
writes project files. `RecentProjects` keeps the list of recently opened project files in the application
settings through `SettingsService`. `NewProjectDialog` collects and validates a project name and location, using
`ProjectService`, and `ProjectExplorer` lists, watches, selects, renames, and deletes the image source files in
the root of the open project's folder. The application supplies the folder picker, the image source filter, and
the refresh scheduler that these units use, so the `Project` subsystem does not depend on the main window.

`MainWindowShell` is the view model of the main window and the only item that talks to the user about projects and
settings: it runs the File, Image, Tools, and Help commands and shows its dialogs through an `IDialogService` that
the window implements. `ProjectWorkspace` holds the project explorer and the preview pane, and
`AboutDialog` identifies the application. At startup the application builds all units in one place,
`ApplicationComposition`, which system tests also use without a window.

`AudioDeviceService` is the only item that uses the audio system and `SpeechModelService` the only item that uses
the speech model catalog and its model store, both through the DemaConsulting.Speech library; each returns a machine
without audio hardware, an unreadable model store, and a failed download as a result with an explanation, never as an
exception. `DeviceSelection`, `ModelCatalogPanel`, and `ModelParameterEditor` are the view models and views of the
device pickers, the model lists with their downloads, and the tunable settings of a model, and `SpeechSettingsPage`
composes them into the "Speech" page of the settings dialog and maps them to and from the stored speech settings. The
application adds the page to the dialog after the AI Provider page, so the `Settings` subsystem does not depend on
speech. The speech choices are stored only; no speech engine is part of the current build, and one added later reads
the stored choices through the settings service.

`ImageRenderService` is the only item that draws files: it calls the CanvasNet viewer library in the background,
confined to the project folder, and returns the pixels, notes, or a coded error. Each picture it returns also says
exactly which picture it is (the file, a fingerprint of the file, and a fingerprint of the pixels) and where the
elements of a diagram, chart, or model were drawn, so that something said about the picture later can be tied to what
the user saw. `PreviewPane` also provides a layer over the shown picture, laid out in the picture's own pixel
coordinates and scaled with it, which other items fill with content that refers to places in the picture. `PreviewPane`
follows the selected file of `ProjectExplorer`, shows what `ImageRenderService` returns, discards results of renders
that are no longer wanted, and renders again when files in the project change. `ImageExporter` saves the shown picture
as a PNG file or copies it to the clipboard for the Image menu. `ImageDragSource` prepares the shown picture for
dragging it into another program, such as a presentation, a mail message, or a folder: it writes the picture as a PNG
file in a folder of its own, and the preview view offers that file and the picture's bitmap to the program the user
drops them on. The `Preview` subsystem owns the two interfaces that this needs, `IImageExporter` and `IExportDialogs`,
and does not refer to the main window.

`ComposerDocument` holds the message that the user is composing for the chat as an ordered sequence of typed text,
dictated text, and chips for attached files, folders, and images and for marks on the picture, and is the single source
of truth about the message. `ComposerHistory` applies each change that the user makes to the message as one undo step,
so that a typed character, a dictated block, a drop or paste of any number of items, an added mark, and a deleted chip
are each one step, and clears the steps, together with the message, when the message is sent. Neither unit depends on
the user interface, on another subsystem, or on the file system; the units that show the message and attach files to it
are added later and build on them. No subsystem uses the `Composer` subsystem yet.

### Dependency Direction

The dependencies between the subsystems run one way: `AppShell` depends on `Preview`, and `Preview` does not depend on
`AppShell`. The seams belong to the provider, the lower subsystem. `Preview` owns `IImageExporter`, the contract the
Image menu uses, and `IExportDialogs`, the three platform operations that an export needs: choosing where to save,
putting a picture on the clipboard, and showing a message. `ImageExporter` asks the user only through `IExportDialogs`.
`AppShell` owns `IDialogService`, which extends `IExportDialogs` and adds the dialogs of the shell, so the main window
implements one interface and passes itself to the exporter. `MainWindowShell` holds the exporter as an `IImageExporter`,
using a disabled `NullImageExporter` when none is supplied, and `ApplicationComposition`, the composition root, builds
the render service, the preview pane, and the exporter, which is its job for every subsystem. `MainWindowView` turns the
`RenderedImage` of `Preview` into an Avalonia bitmap with `RenderedImageBitmap` when it puts it on the clipboard.
`ProjectWorkspace` still holds the preview as an untyped content object, found by the view locator.

Because the direction is fixed, a subsystem added later, such as the chat, depends on the subsystems below it and
never on `AppShell`; only the composition root and the window refer to every subsystem. The direction is shown in the
model and in the diagram above, and the verification of `ImageExporter` uses a substitute for `IExportDialogs`, so no
test of `Preview` needs the dialog service of `AppShell`.

### AI Provider Seam

The chat that will use an AI provider is not part of this increment and the application holds no reference to any AI
provider library, the GitHub Copilot SDK, Ollama, or an agent framework. What exists is the stored choice: the
`AiProviderSettings` of the settings file (provider, model name, context window, reasoning level, Ollama endpoint,
Copilot command-line tool path, and the opt-in for unrestricted agent tools, which is off by default) and the
`AiProviderSettingsPage` that edits it. The seam for the later increment is `ISettingsService`: an agent that is added
as a separate subsystem reads the stored `AiProviderSettings` through the service and reacts to its `Changed` event,
and no existing unit has to change. The agent is to be built detached from the user interface: it receives its settings,
the project folder, and the user's text through its own interfaces, and answers through them, so that it can be tested
without a window and replaced by another provider. The model-name text box will then be replaced by a list that the
provider supplies, and the opt-in will decide whether the agent's tools are limited to the project folder. None of this
exists yet, no interface is declared for it, and no requirement depends on it.

## External Interfaces

**Log Files**: Local diagnostic output written by the application.

- *Direction*: Output.
- *Type*: Text files on the local file system.
- *Format*: One timestamped, leveled entry per line (exception details follow on additional lines),
  UTF-8, in `canvaspilot.log` plus timestamped archive files in the log folder.
- *Constraints*: The default location is `%LOCALAPPDATA%\CanvasPilot\logs`. Files are written locally
  only; no telemetry or remote transmission is performed. Disk usage is bounded by the rotation size
  and the retained archive count.

**Settings File**: The user's persisted settings.

- *Direction*: Input and output.
- *Type*: JSON file on the local file system.
- *Format*: One UTF-8 `settings.json` document with a `version`, the recent projects, the AI provider
  choices, and the speech choices. Unknown properties are ignored.
- *Constraints*: The default location is `%APPDATA%\CanvasPilot\settings.json`. The file is written
  atomically. A corrupt file is renamed `settings.json.bad` and defaults are used. The file never
  contains credentials.

**Settings Dialog**: The window in which the user edits settings.

- *Direction*: Input and output.
- *Type*: Avalonia modal window.
- *Format*: A page list with OK, Cancel, and Apply buttons.
- *Constraints*: Changes take effect only on OK or Apply, and invalid entries cannot be applied.

**Project Folder and Project File**: The user's projects.

- *Direction*: Input and output.
- *Type*: A folder on the local file system holding a `{name}.canvaspilot` JSON file, a hidden `.canvaspilot`
  folder, and the image source files.
- *Format*: The project file holds `formatVersion` (1), `name`, `createdUtc`, and an optional `modifiedUtc`;
  unknown properties are ignored when reading and kept when saving.
- *Constraints*: A project is created only in a new or empty folder. A project file with a newer
  `formatVersion` is refused with a message. Project and file names follow rules that hold on every supported
  platform.

**New Project Dialog and Project Explorer**: The windows and controls in which the user creates a project and
works with its files.

- *Direction*: Input and output.
- *Type*: Avalonia modal window and list control.
- *Format*: A name and location entry with OK and Cancel; a list of the image source file names of the open
  project.
- *Constraints*: OK is enabled only for a project that can be created. The list shows only image source files in
  the project root and follows file changes.

**Image Preview and Export**: The picture of the selected file, and the files and clipboard it is exported to.

- *Direction*: Input (the selected image source file and the files it uses) and output (the picture, a PNG file,
  and a clipboard image).
- *Type*: Avalonia control in the main window, files on the local file system, and the system clipboard.
- *Format*: The picture is drawn by CanvasNet at the file's natural size. An export is a PNG file with the same
  pixels, including transparency; a copy is a bitmap on the clipboard.
- *Constraints*: The drawn file may read other files only from inside the project folder. A file that cannot be
  drawn is explained with its error code, message, and hint, never an unhandled failure. Export and copy are
  enabled only while a picture is shown.

**Picture Drag-out**: The shown picture, dragged out of the preview into another program.

- *Direction*: Output.
- *Type*: The operating system's drag and drop, started from the preview in the main window, and PNG files in a
  folder named `drag` in the application's folder of the per-user local application data folder.
- *Format*: One drag carries one item that offers two formats: a file (a PNG named after the source file and the
  first eight characters of the picture's pixel fingerprint, for example `flow-1a2b3c4d.png`) and a bitmap of the
  same picture. The allowed effect is copy.
- *Constraints*: A drag starts only with the left button, after the pointer has moved four pixels, while a picture is
  shown and dragging out is not turned off. The application writes only files of that name pattern in its folder,
  deletes the files of earlier runs when it starts and its own files when it ends, and never changes the file
  the user dropped. A drop target that refuses the picture, or a file that cannot be written, is logged and never an
  unhandled failure. Whether a particular program accepts the file, the bitmap, or both is checked by hand (see the
  verification of `ImageDragSource`).

**Speech Settings, Audio Devices, and Model Store**: The speech choices, the audio system, and the downloaded models.

- *Direction*: Input and output.
- *Type*: The speech section of the settings file; the operating system's audio system, through the
  DemaConsulting.Speech library; the library's default model store on the local file system; and HTTPS downloads of
  model files.
- *Format*: Devices are stored by name, models by identifier, and each model's parameter values as
  invariant-culture strings, in the `speech` section of `settings.json`. The model store is
  `%LOCALAPPDATA%\DemaConsulting.Speech\Models` on Windows and is shared with other DEMA Consulting applications.
- *Constraints*: The audio system and the model store are used only when the Speech page is opened. Downloads start
  only when the user asks, verify checksums before installing, and are cancelled when the settings dialog closes. A
  machine without audio hardware, a blocked network, or an unreadable store is explained on the page and never
  causes a failure.

**Windows Installer Package**: The way the application is delivered.

- *Direction*: Output (built by the release pipeline) and input (to the user's Windows Installer).
- *Type*: One per-machine, 64-bit MSI file named `CanvasPilot-{version}-win-x64.msi`, built by the WiX Toolset from the
  installer project, which is not part of the solution.
- *Format*: The package carries the self-contained single-file `CanvasPilot.exe` publish output, a Start Menu
  shortcut, and the Add/Remove Programs icon and links; its version is the numeric part of the build version.
- *Constraints*: Installing a package replaces any installed version, older or newer, and asks a running
  `CanvasPilot.exe` to close first. The package contains no user data, and uninstalling leaves the user's settings,
  logs, and projects in place.

**Main Window**: The application's window and menus.

- *Direction*: Input and output.
- *Type*: Avalonia window with File, Image, Tools, and Help menus, a project explorer, a preview area, and modal
  dialogs (New Project, Settings, About, and message, confirmation, and prompt dialogs).
- *Format*: The window title is `{project name} - CanvasPilot` while a project is open, otherwise `CanvasPilot`.
  Every menu item has a name and an automation identifier that are the same.
- *Constraints*: Menu items are enabled only when their command can run. Problems are shown in a dialog, never
  as an unhandled failure.

## Dependencies

The `AppShell`, `Composer`, `Logging`, `Preview`, `Project`, `Settings`, `Speech`, and `Utilities` subsystems use .NET
base class library types, `System.Text.Json` for the settings and project files, and, for dialogs, pages, and lists,
Avalonia and CommunityToolkit.Mvvm, which are runtime third-party items (the `Composer` units so far use base class
library types only). The `ImageRenderService` unit draws files and encodes PNG
with the `DemaConsulting.CanvasNet.Viewer` library, and the `ProjectExplorer` unit uses its extension list to decide
which files are image sources. The `AudioDeviceService` and `SpeechModelService` units list audio devices and speech
models and download models with the `DemaConsulting.Speech` library and its Sherpa model package, and the speech
descriptor types are shown by the other units of the `Speech` subsystem; both libraries are documented as OTS items
(see *OTS Dependencies*). Build, documentation, and quality tools are documented in the OTS design documents. The
Windows installer package is built from the published application by the WiX Toolset, also documented as an OTS
item; no run-time item depends on it.

## Risk Control Measures

**Path confinement**: Every file path built from a caller-supplied name passes through
`PathHelpers.SafePathCombine`, which rejects traversal sequences and absolute paths. `RollingFileLogger`
applies this to its configurable base file name, so logging cannot create or overwrite files outside the
log folder. `SettingsService` applies it to the settings and temporary file names, and `ProjectService`
and `ProjectExplorer` apply it to project folders, project files, and renamed files. Segregation of the
checking logic into the `Utilities` subsystem lets it be reviewed and verified once for every consumer.

**Bounded diagnostics**: Log rotation caps the disk space consumed by diagnostic output, so a failure
that logs repeatedly cannot exhaust the user's disk.

**Safe settings handling**: A save writes a temporary file and then replaces the settings file, so a failure
cannot leave a truncated file. A corrupt or newer file is tolerated with defaults and a log entry, so a
damaged file cannot prevent startup. Values are validated when read and when entered, so an out-of-range
value cannot reach the rest of the application.

**Safe agent defaults**: The opt-in for unrestricted agent tools is stored as off by default and changes
only when the user explicitly checks it in the dialog.

**Safe project handling**: A project is never created in a folder that already has content, and a failed or
cancelled creation removes what it added. Project files are saved by writing a temporary file and replacing
the project file, and a file from a newer version is refused rather than rewritten, so it cannot be
damaged. Renaming a file never replaces an existing file or folder, and deleting is left to the caller to
confirm with the user. Names are checked against rules that reject separators, traversal, hidden names, and
reserved device names.

**Confined drawing**: The files that a drawn file references (embedded pictures, model includes, chart data) are read
only from inside the project folder, through CanvasNet's `UnderRoots` file access policy, so an image source file
cannot pull unrelated files of the user's computer into a picture that is exported or shared. A denied file is
reported as a note. Drawing runs in the background and can be cancelled, so a slow or hostile file cannot block the
window.

**Bounded drag files**: The files written for dragging a picture out are named from the source file's name with
characters that a file name cannot hold replaced and the length limited, and are built with
`PathHelpers.SafePathCombine`, so a file name cannot lead outside the drag folder. The unit deletes only files that
match its own name pattern, never folders or other files, so cleaning up cannot remove the user's files.

**Clean message text**: Text that enters the message being composed, whether typed, pasted, dropped, or recognized from
speech, is data from outside the application. `ComposerDocument` removes from it the character that stands for a chip
and every control character other than tab and line feed, so that text can never be taken for a chip or confuse the
editing control or the tools that read the message. The draft and its undo steps are kept in memory only, are never
written to disk, and are cleared when the message is sent.

**Safe speech handling**: Downloads of speech models start only when the user asks, run over HTTPS, and are verified
against checksums by the speech library before a model is installed completely or not at all, so a damaged or
tampered download is discarded. A download never outlives the settings dialog that shows it. A choice that the page
cannot show, such as a device that is not connected or a model while the model store cannot be read, is kept in the
settings instead of being erased, and failures of the audio system and the model store are results, never
unhandled exceptions.

## Data Flow

Application subsystems call `RollingFileLogger.Log` with a severity, a message, and optional exception
context. `RollingFileLogger` asks `PathHelpers` to resolve the active file path inside the log folder,
rotates and prunes archives when the active file has reached its size threshold, and appends the
formatted entry to the active file. Nothing flows back to the caller except a propagated `IOException`
when an entry cannot be written.

At startup the application loads the settings through `SettingsService`, which reads and normalizes
`settings.json`. To change settings, the application shows `SettingsDialog` with the pages to host. Each
page loads its working state from the settings in effect; on OK or Apply the dialog merges every page into the
latest settings and calls `SettingsService.Save`, which writes the file atomically and raises `Changed`.

To create a project, the application shows `NewProjectDialog`, which validates the entries through
`ProjectService`; when the dialog was accepted, the application calls `ProjectService.CreateAsync`, which
creates the project folder, the project file, and the data folder. To open a project, the application calls
`ProjectService.OpenAsync`. Either way, the project file is added to `RecentProjects`, which stores the list
through `SettingsService`, and the project folder is opened in `ProjectExplorer`, which lists its image source
files, watches the folder, and refreshes the list after file changes.

At startup `App` asks `ApplicationComposition` to build the units: the logger, the settings service (whose settings
are loaded at once), the project service, the recent projects list, the project explorer, the render service, the
image drag source (which deletes the files of earlier drags), the
preview pane, the workspace, the image exporter, and the shell. The `MainWindowView` is created over the shell and
gives itself to the shell as the dialog service. From
then on the user's menu choices become shell commands, which call the `Project` and `Settings` subsystems, show
dialogs through the dialog service, and, when the open project changes, update the window title and open or close
the project in the workspace. On exit `App` disposes the composition.

When the user selects a file in `ProjectExplorer`, `PreviewPane` cancels any render that is running and asks
`ImageRenderService` to draw the file in the background; the result, a picture with notes or an error with its
code and hint, is shown unless a newer render was started meanwhile. When files in the project change, the pane
renders again after a short delay. The Image menu runs `ImageExporter`, which encodes the shown picture as PNG and
writes it where the user chooses, or hands it to the window to put on the clipboard. When the user presses the left
button on the shown picture and moves the pointer, the preview view asks `PreviewPane` for the picture as a file;
`ImageDragSource` writes the PNG in its folder, and the view starts the operating system's drag with that file and
a bitmap of the picture.

The message being composed is held by `ComposerDocument`, and every change the user makes to it, typing, dictating,
dropping, pasting, adding a mark, and deleting, is made as one `ComposerEdit` that `ComposerHistory.Execute` applies to
the document and records. Undo and redo revert and apply whole edits and return the caret to restore. When the message
is sent, `ComposerHistory.CompleteSend` returns a snapshot of the document, from which the message is built, and empties
the document and the history.
When the user opens Settings and chooses the Speech page, `SpeechSettingsPage` loads: `DeviceSelection` asks
`AudioDeviceService` for the devices and selects the stored ones by name, `ModelCatalogPanel` asks
`SpeechModelService` for the models and selects the stored ones by identifier, and `ModelParameterEditor` shows the
parameters of the chosen models with their stored values. A download requested in the panel goes through
`SpeechModelService` to the speech library, which reports progress to the row and ends as installed, canceled, or
failed. On OK or Apply the page merges the choices into the speech section of the settings, which
`SettingsService.Save` writes. When the dialog closes the page cancels downloads that are still running.

## Design Constraints

- Log files shall be written only to local disk; no telemetry or remote transmission is performed.
- The default log folder shall be under the per-user local application data folder so that no
  administrator rights are required to write logs.
- All file paths built from caller-supplied names shall be resolved with `PathHelpers.SafePathCombine`.
- Logging shall be safe to call from multiple threads.
- The settings file shall be under the per-user application data folder, written atomically, and tolerant
  of missing, corrupt, and newer content.
- Settings pages shall be supplied to the settings dialog, never referenced by it.
- The settings subsystem shall hold no reference to any AI provider SDK or client.
- The project file shall carry a format version, and a file with a newer version shall never be rewritten.
- The hidden `.canvaspilot` folder of a project is reserved for application data; the project explorer shall
  never list its content and nothing else shall be written there until a requirement defines it.
- The project subsystem shall not depend on the main window; the folder picker and refresh scheduler are
  supplied by the application.
- View models shall talk to the user only through `IDialogService`, which the main window implements, so no
  view model references a window.
- Every control that a test or assistive technology must find shall carry an automation identifier.
- The image preview plugs into the shell through `IImageExporter` and `ProjectWorkspaceViewModel.Preview`
  and does not require changes to the shell's commands. `Preview` shall not depend on `AppShell`: it owns the
  exporter seam `IImageExporter` and the dialog seam `IExportDialogs`, and `AppShell` refers to `Preview` only for
  these interfaces, the picture type, and the composition root (see *Dependency Direction*).
- Only `ImageRenderService` shall call the CanvasNet viewer to draw, and it shall confine the files a drawn file
  reads to the project folder.
- Rendering shall never run on the user interface thread, and the result of a render that is no longer wanted shall
  never be shown.
- Only `ImageDragSource` shall write the files for dragging a picture out, only in its own folder, and a failure to
  prepare or to drag shall be logged and shall never reach the user as an unhandled failure.
- The application's units shall be built only in `ApplicationComposition`.
- Every change that the user makes to the message being composed shall be one `ComposerEdit` executed through
  `ComposerHistory`, so that the message and the undo steps never disagree; the units of the `Composer` subsystem that
  hold the message shall not write it anywhere.
- Only `AudioDeviceService` shall use the audio system and only `SpeechModelService` shall use the speech model
  catalog; neither shall use its backend before it is first asked, and neither shall throw for a missing audio
  system, an unreadable model store, or a failed download.
- Speech devices shall be stored by name, speech models by identifier, and model parameter values as
  invariant-culture strings.
- A speech model download shall start only on the user's request and shall be cancelled when the settings dialog
  closes.
- The application shall not reference the native speech engine runtime until a speech engine is loaded.
- The installer project shall stay outside the solution so that building and restoring the solution never needs the
  WiX Toolset or Windows; it shall be built only by the Windows build job from the published win-x64 output.
- The installer shall replace any installed version and shall not write or remove user data.

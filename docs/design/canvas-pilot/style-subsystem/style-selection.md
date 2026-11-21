### StyleSelection

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleSelection` answers which style kit the open project uses and keeps that choice. The project file keeps only the id
of the kit; this unit turns the id into the kit that applies to it, by the order of the places, through the catalog of
kits, and writes a new choice into the project file through the project service. It also gives a new project the default
kit of the user, and it supplies the export sizes for the export dialog of the `Preview` subsystem: the sizes of the kit
first, then the sizes of the application. It has no user interface.

#### Data Model

- `IActiveStyle`: the contract that the window, the status bar, the tools, and the checker use: `Active` (the kit entry
  or `null`), `ActiveId` (the id in the project file or `null`), `IsMissing`, `SetActiveAsync(id)`, and `Changed`.
- `StyleSelection` keeps no copy of the choice. Every answer is read from the open project (`IProjectService.Current`)
  and the catalog (`IStyleKitCatalog.Resolve`) at the time of the question, so it is never stale. The only state is a
  counter of the announcements, which tells whether a call raised `Changed`.
- `AppPresets`: the sizes of the application, as `ExportPreset` values with the origin `Application`: `PowerPoint 16:9`
  (1920 by 1080), `Email (800 wide)` (800 wide, the height follows the picture), and `A4 portrait @150 DPI` (1240 by
  1754 with 150 dots per inch). None is transparent, and each has a white background until `GetPresets` gives it the
  background of the kit.
- The identifier of a size of a kit is `kit:{kit id}:{name in lower case}` and that of a size of the application is
  `app:` followed by a fixed word, so the export dialog can find the choice of the last time.

#### Key Methods

**Active / ActiveId / IsMissing**: `ActiveId` is the `ActiveStyleKit` of the document of the open project, or `null`
when no project is open or the project names none. `Active` is the entry that `Resolve` finds for the id, which is the
kit of the most local place, so a copy of a kit in the project hides the copy of the program. `IsMissing` is true when
there is an id and no kit has it. Until the catalog has been refreshed once, every id is missing; the catalog announces
its first refresh and `Changed` follows it.

**SetActiveAsync**: Calls `IProjectService.SetActiveStyleKitAsync`, which refuses an id that no kit can have and a
missing project, writes the project file atomically, and announces the change. A refusal is logged and thrown as an
`InvalidOperationException` that carries the message of the project service. `Changed` is raised once for the choice
even when the same kit is chosen again, which writes nothing and is not announced by the project service.

**ApplyDefaultToNewProjectAsync**: Does nothing when no project is open, when the project already names a kit (even one
that cannot be found), when the user has no default kit, or when no kit has the id of the default kit. Otherwise it
chooses the default kit as `SetActiveAsync` does. The shell calls it after it created a project, so that a project says
which style it uses and a later change of the default moves no existing project. A project file that cannot be written
is logged and the project starts with no style.

**GetPresets**: Takes the kit that `Active` finds, if any, and its `background` color, or white. The sizes of the kit
come first, in the order of the kit, as `ExportPreset` values with the origin `StyleKit`; a size that is not transparent
gets the background of the kit and a transparent size has none. Then come the sizes of the application whose names are
not the name of a size of the kit, ignoring case, each over the same background.

**Changed**: Raised when the project service announces a change of the open project (which includes a change of the kit
it uses) and when the catalog announces a change of the kits, and once after `SetActiveAsync`. `Dispose` stops
listening.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing collaborator. `SetActiveAsync` throws
`InvalidOperationException` for a choice that cannot be kept. A missing kit is a state (`IsMissing`), not an error.
Failures to write a log entry are ignored.

#### Dependencies

- **ProjectService** (through `IProjectService`) — the open project, its document, the change of the kit it uses, and
  the announcement of a change.
- **StyleKitCatalog** (through `IStyleKitCatalog`) — the kit of an id, and the announcement of a change of the kits.
- **SettingsService** (through `ISettingsService`) — the default kit of the user.
- **ExportPreset** and **IExportPresetSource** of the `Preview` subsystem — the value and the interface that this unit
  supplies.
- **RollingFileLogger** — optional; records a refused choice and a default that could not be applied.

#### Callers

- `ImageExporter` (through `IExportPresetSource`) — asks for the sizes when the user exports a picture.
- `ApplicationComposition` and `MainWindowShell` — build the unit and call `ApplyDefaultToNewProjectAsync` after a
  project is created (a later step).
- The picker of the status bar, the guidance, the tools, and the check of a picture (later steps) — read the active kit.

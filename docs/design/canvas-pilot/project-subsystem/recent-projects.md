### RecentProjects

![Project Structure](ProjectSubsystemView.svg)

#### Purpose

`RecentProjects` keeps the list of the most recently opened project files in the application settings, so the
user can reopen a project with one choice. It never offers a file that no longer exists.

#### Data Model

**MaxCount**: `int` constant — 10, the most entries the list holds.

**Stored list**: `IReadOnlyList<string>` — the `RecentProjects` value of `AppSettings`: full paths, most
recent first. The list shares the settings file with the other subsystems.

**Changed**: event raised after the list was stored, whether by `Add`, `Remove`, `Clear`, or by dropping
missing files in `GetRecent`.

#### Key Methods

**GetRecent**: Returns the stored files that still exist, at most ten, most recent first. When any stored file
is missing, the stored list is updated to drop it and `Changed` is raised; a failure to store does not affect
the result.

**Add**: Puts a project file at the top.

- *Parameters*: `string projectFile`.
- *Returns*: `bool` — whether the list was stored; `false` for an empty or invalid path or a failed save.
- *Postconditions*: The full path is first, any earlier entry that differs only in letter case is gone, and the
  list holds at most ten entries.

**Remove**: Removes a project file, ignoring letter case. **Clear**: Empties the list.

All three change the list with `ISettingsService.Update`, which applies the change to the latest stored list
and writes it atomically, so values owned by other subsystems are not overwritten.

#### Error Handling

The constructor throws `ArgumentNullException` for a null settings service. An `IOException` from the settings
service, which has already logged it, is converted to a `false` result and no change is announced. Invalid
paths are ignored rather than thrown.

#### Dependencies

- **SettingsService** (through `ISettingsService`) — holds the list and writes it.
- **System.IO** — file existence checks and full-path resolution.

#### Callers

- The application shell — adds a project file when a project is created or opened, removes one that cannot
  be opened, and shows the list in the File menu.

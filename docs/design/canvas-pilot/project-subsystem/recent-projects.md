### RecentProjects

![Project Structure](ProjectSubsystemView.svg)

#### Purpose

`RecentProjects` keeps the list of the most recently opened project files in the application settings, so the
user can reopen a project with one choice, and remembers the picture last shown in each, so the project opens at it
again. It never offers a file that no longer exists.

#### Data Model

**MaxCount**: `int` constant — 10, the most entries the list holds.

**Stored list**: `IReadOnlyList<string>` — the `RecentProjects` value of `AppSettings`: full paths, most
recent first. The list shares the settings file with the other subsystems.

**Changed**: event raised after the list was stored, whether by `Add`, `Remove`, `Clear`, or by dropping
missing files in `GetRecent`.

**Time of opening**: the `RecentProjectOpened` value of `AppSettings`, a dictionary from the full path of a project file
(compared ignoring case) to the `DateTimeOffset` at which it was last opened. The time comes from a `TimeProvider` that
the constructor takes (the system clock when none is given), so a test can set the clock. The settings service keeps the
dictionary to the projects that are in the list, so a project that leaves the list takes its time along.

**Picture last shown**: the `RecentProjectPicture` value of `AppSettings`, a dictionary from the full path of a project
file (compared ignoring case) to the path of the picture last shown in it, relative to the project folder (for example
`diagrams/flow.svg`). A relative path names the same picture wherever the folder is opened from. Only a path that
`AppSettings.IsValidShownPicture` accepts is kept: not blank, not rooted, and without a `..` part, so it stays inside
the folder. The settings service keeps the dictionary to the projects that are in the list, as for the times.

The picture is kept here, with the recent projects of the user, and not in the project file, by design: a project folder
is copied, shared, and put under source control, and which picture a person looked at is that person's own view state.
Writing it into the project file would change a shared, versioned file on every selection and hand one user's view to
the whole team. The chat history makes the same choice for the words of a conversation.

#### Key Methods

**GetRecent**: Returns the stored files that still exist, at most ten, most recent first. When any stored file
is missing, the stored list is updated to drop it and `Changed` is raised; a failure to store does not affect
the result.

**Add**: Puts a project file at the top.

- *Parameters*: `string projectFile`.
- *Returns*: `bool` — whether the list was stored; `false` for an empty or invalid path or a failed save.
- *Postconditions*: The full path is first, any earlier entry that differs only in letter case is gone, the list holds
  at most ten entries, and the time of opening of the path is the present time, renewing an earlier one. The list and
  the time are stored in one update of the settings.

**Remove**: Removes a project file, ignoring letter case, and with it the time of its opening. **Clear**: Empties the
list.

All three change the settings with `ISettingsService.Update`, which applies the change to the latest stored settings
and writes them atomically, so values owned by other subsystems are not overwritten.

**GetShownPicture**: Gives the picture last shown in a project.

- *Parameters*: `string projectFile`.
- *Returns*: `string?` — the relative path of the picture, or `null` when the path of the project is empty or invalid,
  the project has no picture, or the stored path is not valid. It does not check that the picture still exists; the
  explorer selects only a file that it lists.

**RememberShownPicture**: Remembers the picture that is shown now in a project.

- *Parameters*: `string projectFile`, `string picture` (relative to the project folder).
- *Preconditions*: The project is in the list and the path is valid; otherwise the result is `false` and nothing is
  written.
- *Returns*: `bool` — `true` when the picture is remembered; `false` for an unlisted project, an invalid path, or a
  failed save.
- *Postconditions*: The picture of the project is the given path. When it already was, nothing is written, so storing
  the same picture again costs no write of the settings. `Changed` is not raised, because the list did not change and
  the welcome screen, which follows `Changed`, need not be built again. Each call that changes the picture writes the
  settings file, so the caller keeps the shown picture in memory and calls this once per project, not on every
  selection (see *MainWindowShell*).

#### Error Handling

The constructor throws `ArgumentNullException` for a null settings service. An `IOException` from the settings
service, which has already logged it, is converted to a `false` result and no change is announced. Invalid
paths, of a project or of a picture, are ignored rather than thrown.

#### Dependencies

- **SettingsService** (through `ISettingsService`) — holds the list, the times of opening, and the pictures last shown,
  and writes them; `AppSettings.IsValidShownPicture` is the rule for a picture path.
- **TimeProvider** — tells the time of an opening; the system clock unless a test supplies another.
- **System.IO** — file existence checks and full-path resolution.

#### Callers

- The application shell — adds a project file when a project is created or opened, removes one that cannot
  be opened or that the user removes from a welcome card, and shows the list in the File menu and on the welcome screen,
  which also shows the time of opening. It keeps the picture that the user shows last in the open project in memory,
  stores it here once when the project is closed, another project is opened, or the application exits, and selects the
  remembered picture when a project is opened.

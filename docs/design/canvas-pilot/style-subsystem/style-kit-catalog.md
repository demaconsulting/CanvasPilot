### StyleKitCatalog

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleKitCatalog` finds the style kits in the places that hold them, tells which kit applies to an id when more than one
place has it, and creates, saves, duplicates, copies, and deletes the kits of the user and of the open project. It is the
one unit that knows where kits are kept; the units that use a kit ask it by id. It has no user interface.

#### Data Model

- `StyleKitEntry`: a `StyleKit` with its `Location`, `FolderPath`, `SharedRoot` (the shared folder, for a shared kit),
  `IsReadOnly` (true for a shared kit and a kit of the program), `IsOverridden`, `OverriddenBy` (the name of the place that
  hides it), and the `Warnings` of reading it.
- `StyleLocationStatus`: a place with its `Location`, `Path`, `IsAvailable`, a `Problem` text, and `Broken` (the folders of
  the place whose style file cannot be used, each a `StyleBrokenKit` with its `FolderPath` and `Issues`).
- `IStyleKitCatalog`: `Entries`, `Locations`, `Resolve`, `RefreshAsync`, `Save`, `CreateBlank`, `Duplicate`, `Delete`,
  `CopyIntoProject`, and the event `Changed`.
- The places, in the order of precedence: the `styles` folder of the open project (`ProjectFolderName`), the `styles`
  folder in the data folder of the user (`UserFolderName`), the shared folders of the settings in their order, and the
  `StyleKits` folder beside the program (`BuiltInFolderName`). The folder of a kit is the sub-folder named as its id; a
  folder with another name is read, and the id of its file wins.
- `Entries` are ordered by place (in the order above), then by name ignoring case, then by id. A kit that is hidden by a
  kit of a more local place with the same id is still an entry with `IsOverridden` and the name of the place that hides it
  (`This project`, `My kits`, `Shared folder {path}`, or `Built-in kits`); only the entry that is not overridden is found
  by `Resolve`.
- `Locations` has one `StyleLocationStatus` per place, in the order of precedence, and none for the project while no
  project is open. The place of the user and of the project are available even when their folder does not exist yet,
  because they are made when the first kit is saved; a shared folder or the folder of the program that does not exist is
  not available.

#### Key Methods

**RefreshAsync**: Reads the places again. It notes a number for the refresh and the count of changes made to the folders
of the user and of the project, reads the folders of the program, of the user, and of the project together on one worker,
reads every shared folder on a worker of its own, and waits for all of them. Each shared folder has a time limit (3 seconds
by default) that is applied with `Task.WhenAny` on the scan and a delay, so the refresh takes about the longest limit and
no more; a scan that exceeds the limit is left to finish by itself, its result is not used, and the place is reported as not
available with the sentence that it did not answer, and that it may be offline. The scan of a place never throws for a
folder problem: a missing folder, a refused folder, and an unexpected failure of the scan of a shared folder are a place
that is not available with the reason. The result is applied, replacing the snapshot as a whole, only when no refresh with
a higher number was applied meanwhile, and the scans of the user and of the project only when no change was made to those
two folders meanwhile, because the change has read them more recently. `Changed` is raised after the result was applied.
Cancelling the token before the result is applied throws `OperationCanceledException` and changes nothing.

**Scan of a place** (`StyleKitScanner`): lists the sub-folders in name order, without those whose name starts with a
period (a kit that is being written is in such a folder until it is moved into place), reads at most 200 kit folders, and for
each folder that has a style file calls `StyleKitSerializer.ReadFolder`. A kit with no error is a kit with its warnings; a
kit with an error is a broken folder with all its issues and is written to the log; a second folder of the place with an id
that an earlier folder has is a broken folder with the reason; a folder with no style file is not a kit and is not
mentioned. A place with more than 200 kit folders says so in its `Problem` and is still available.

**Resolve**: the entry of an id that is not overridden, by exact comparison of the id; `null` for an unknown id.

**Automatic refresh**: The catalog subscribes to the change of the open project and to the changes of the settings. A
change of the project starts a refresh in the background; a change of the settings starts one only when the list of shared
folders is different from the list that was last read, so saving another setting costs nothing. A failure of such a
refresh is logged. `PendingRefresh` (internal) is the task of the last such refresh, which the tests wait for.

**Save**: only for the folder of the user and of the project; any other place is an `InvalidOperationException`. A kit that
has an error when it is checked with `StyleKitSerializer.Validate` is an `ArgumentException` that lists the errors, so an
invalid kit is never written. The folder is made when needed (named as the id, joined with `PathHelpers.SafePathCombine`),
the file is written to a temporary file in the same folder and moved over the style file, and the example files that are in
the folder are not touched. The folders of the user and of the project are then read again at once, on the calling thread,
which is quick because they are on this computer, `Changed` is raised, and the entry is returned. The shared folders are
not read again by a change of a local kit.

**CreateBlank**: makes a kit with a valid palette (white background, dark text, blue primary), the default fonts, and the
name that was given (`New kit` when blank, at most 60 characters), with an id made from the name by `IdBaseFromName`: lower
case letters and digits, a dash for each run of other characters, no dash at an end, at most 40 characters, and `new-kit`
when nothing is left. The id is made unique by adding `-2`, `-3`, and so on while any known kit of any place has it or the
folder exists in the target. Every known kit counts, not only those of the target, because a new kit with the id of a kit of
the program would hide that kit, which is what a copy is for and not a new kit.

**Duplicate and CopyIntoProject**: copy the folder of a kit to a hidden folder (`.{id}.{guid}.tmp`) in the target and move
it to the folder named as the id, so that a failure leaves no half kit and the hidden folder is removed. The copy has the
style file with the new id and name and each example file that the kit lists, joined to the new folder with the safe
combination and copied byte for byte; the captions go with the entries. Because the examples that a kit lists were found as
files of its examples folder, links and hidden files are not copied. `Duplicate` gives the copy a new unique id made from the
new name; `CopyIntoProject` keeps the id and is refused when the project already has a folder of that id or when no project
is open.

**Delete**: only a kit of the user or of the project. The folder of the entry must be a direct child of the folder of that
place as it is now (a crafted entry with another folder, with `..`, or of another project is an
`InvalidOperationException`), and then it is removed with its contents.

#### Error Handling

Constructors and methods throw `ArgumentNullException` for missing arguments. A change of a read-only place, a project place
with no project, a repeated id in a copy into the project, and a folder that is not where the kit is expected are an
`InvalidOperationException` with a sentence; a kit with errors is an `ArgumentException`; a file that cannot be written is an
`IOException`. Reading never throws for a folder problem. Failures to log are ignored.

**Design properties.** The catalog is safe for concurrent use: the scans and the snapshot are guarded by a lock that is held
only for the small reads and for the scan of the two local folders of a change, never for a scan of a shared folder, and the
readers read the snapshot without a lock. Nothing is read on the user interface thread by `RefreshAsync`, and the shared
folders are never read by anything else.

#### Dependencies

- **StyleKitDocument** unit: reads and writes the style file and lists the examples.
- **ProjectService** (Project subsystem): `Current` and `CurrentChanged`.
- **SettingsService** (Settings subsystem): `Current.Styles.SharedKitFolders`, `Changed`, and the default folder of the data
  of the application, which holds the folder of the user.
- **PathHelpers** unit (Utilities subsystem): joins every folder and file name that comes from a kit.
- **RollingFileLogger** (Logging subsystem): optional; broken kits, folders that cannot be read, and changes.

#### Callers

- The unit that holds the active kit of the project, the guidance and tools for an assistant, the window of the kits, the
  settings page, the picker, and the application composition, in later steps.

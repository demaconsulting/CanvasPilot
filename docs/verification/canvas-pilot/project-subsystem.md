## Project

### Verification Approach

The `Project` subsystem is verified by integration tests defined in `ProjectSubsystemTests.cs`. The tests
compose a real `ProjectService`, a real `RecentProjects` over a real `SettingsService`, a real
`NewProjectDialogViewModel`, and a real `ProjectExplorerViewModel` with the real `CanvasNetImageSourceFilter`
on temporary folders, so they verify that a project made through the dialog exists on disk and reopens, that the
recent list survives a restart and drops deleted projects, and that the explorer lists only the root image
files. No mocking is required for the behavior under test: the folder picker is a substitute because it would
show a platform dialog, and the refresh scheduler is a hand-written fake so no test waits for a delay. The
views themselves are verified by the headless UI tests described in the unit verification documents.

### Test Environment

N/A - standard test environment. Tests use temporary local folders and real file I/O; no network, external
services, or display are required.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A project created from the dialog's entries is on disk with its project file and empty data folder, can be
  saved, and is opened by a new service with the same name, folder, and creation time.
- Opened projects are listed most recent first after a restart, and a project whose folder was deleted is no
  longer listed or stored.
- The explorer of a created project lists only the image source files in the project root, never the project
  file, sub-folder assets, or data folder content, and reflects a rename and a delete.
- The style kit that is chosen for a project is in its file, is read by another service that opens the file, and leaves
  the other properties of the file, the image files, and the data folder as they were; choosing none removes it.

### Test Scenarios

**ProjectSubsystem_ManageProjects_DialogCreateSaveReopen_RoundTrips**: The New Project dialog is given a folder
and a name; OK is chosen; a project is created from the dialog's entries, saved, and opened by a second
service. The dialog was accepted, the project reopens with the same name, folder, and creation time and a
modified time, and the data folder exists. This scenario is tested by
`ProjectSubsystem_ManageProjects_DialogCreateSaveReopen_RoundTrips`.

**ProjectSubsystem_RememberRecentProjects_OpenedProjects_ListedMostRecentFirst**: Two projects are created and
added to the recent list, settings are loaded by a new service, and the first project's folder is deleted. After
the restart both are listed newest first; after the deletion only the second is listed and stored. This
scenario is tested by `ProjectSubsystem_RememberRecentProjects_OpenedProjects_ListedMostRecentFirst`.

**ProjectSubsystem_BrowseImages_ProjectFolder_ListsOnlyRootImageSources**: A created project holds two SVG
files, a text file, a sub-folder asset, and an SVG inside the data folder. The explorer lists the two root SVG
files; one is renamed and the other deleted, after which only the renamed file is listed, and the asset and the
project file remain. This scenario is tested by
`ProjectSubsystem_BrowseImages_ProjectFolder_ListsOnlyRootImageSources`.

**ProjectSubsystem_KeepTheStyleChoice_SetAndReopen_GivesTheSameChoiceAndKeepsTheProject**: A created project has an image
file and, in its project file, a property that this version does not know. A style kit is chosen, and a second service
that opens the file as after a restart reads the same kit. The unknown property is still in the file, the image file and
the data folder are untouched, and choosing no style removes the property of the style and keeps the unknown one. This
scenario is tested by `ProjectSubsystem_KeepTheStyleChoice_SetAndReopen_GivesTheSameChoiceAndKeepsTheProject`.

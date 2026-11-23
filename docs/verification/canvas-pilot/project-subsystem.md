## Project

### Verification Approach

The `Project` subsystem is verified by integration tests defined in `ProjectSubsystemTests.cs`. The tests compose a real
`ProjectService`, a real `RecentProjects` over a real `SettingsService`, a real `NewProjectDialogViewModel`, and a real
`ProjectExplorerViewModel` with the real `CanvasNetImageSourceFilter` on temporary folders, so they verify that a
project made through the dialog exists on disk and reopens, that the recent list survives a restart and drops deleted
projects, and that the explorer shows the tree of the project but never its data folder. No mocking is required for the
behavior under test: the folder picker is a substitute because it would show a platform dialog, and the refresh
scheduler is a hand-written fake so no test waits for a delay. The views themselves are verified by the headless UI
tests described in the unit verification documents. The subsystem requirements about adding files by dropping them, the
look of the explorer, thumbnails, and accessibility are verified by those view tests, which run the explorer over real
project folders, and by the accessibility sweep of the application, since there is no separate subsystem test for them.

### Test Environment

N/A - standard test environment. Tests use temporary local folders and real file I/O; no network, external
services, or display are required.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A project created from the dialog's entries is on disk with its project file and empty data folder, can be
  saved, and is opened by a new service with the same name, folder, and creation time.
- Opened projects are listed most recent first after a restart, and a project whose folder was deleted is no
  longer listed or stored.
- The explorer of a created project shows the folders and the image source files and assets of the project as a tree,
  never the project file or data folder content, and reflects a rename and a delete.
- Files dropped on the explorer are copied where they fall and a text file is refused; the explorer shows rows with
  badges, a header with its actions, and thumbnails that follow the setting.
- The controls of the explorer and of the New Project dialog have accessible names, focus, and targets that are large
  enough.
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

**ProjectSubsystem_BrowseImages_ProjectFolder_ListsImagesOfTheTreeButNotTheDataFolder**: A created project holds two
SVG files, a text file, an SVG in a sub-folder, and an SVG inside the data folder. The explorer lists the sub-folder's
SVG and the two root SVG files in tree order; one is renamed and the other deleted, after which the renamed file and
the sub-folder's file are listed, and the project file remains. This scenario is tested by
`ProjectSubsystem_BrowseImages_ProjectFolder_ListsImagesOfTheTreeButNotTheDataFolder`.

**ProjectSubsystem_KeepTheStyleChoice_SetAndReopen_GivesTheSameChoiceAndKeepsTheProject**: A created project has an
image file and, in its project file, a property that this version does not know. A style kit is chosen, and a second
service that opens the file as after a restart reads the same kit. The unknown property is still in the file, the image
file and the data folder are untouched, and choosing no style removes the property of the style and keeps the unknown
one. This scenario is tested by `ProjectSubsystem_KeepTheStyleChoice_SetAndReopen_GivesTheSameChoiceAndKeepsTheProject`.

**ProjectExplorerView_DropFiles_OnZoneAndOnFolderRow_CopiesAndHighlights**: A project with a folder is shown in the
explorer view, and two pictures from outside are dragged onto the drop zone and onto the folder row. The zone and the
row are lit during the drag and not after the drop, one picture is copied into the attachments folder and the other into
the folder, and the sources stay. This scenario is tested by
`ProjectExplorerView_DropFiles_OnZoneAndOnFolderRow_CopiesAndHighlights` and, for a drop that is text or a file of
another kind, by `ProjectExplorerView_DropTextOrOtherFile_ChangesNothingOrExplains`.

**ProjectExplorerView_FoldersAndFiles_ShowAsRowsWithBadgesAndIndent**: The shown explorer has rows 26 pixels high with
badges, a folder that expands to children indented 16 pixels, and a selection that is no file for a folder. The header
test `ProjectExplorerView_Header_ShowsLabelAndButtonsThatRunTheCommands` adds the label and the buttons that run their
commands. This scenario is tested by `ProjectExplorerView_FoldersAndFiles_ShowAsRowsWithBadgesAndIndent` and
`ProjectExplorerView_Header_ShowsLabelAndButtonsThatRunTheCommands`.

**ProjectExplorerView_Thumbnails_ShownOnRowsOfDrawnFilesAndFollowTheSetting**: The explorer over a real thumbnail
provider shows a small picture on the row of a drawing only, takes it away when thumbnails are turned off, and makes it
again when they are turned on. This scenario is tested by
`ProjectExplorerView_Thumbnails_ShownOnRowsOfDrawnFilesAndFollowTheSetting`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: The accessibility sweep walks the workspace of an open
project, which holds the explorer, and the New Project dialog, in both theme variants, and finds names, tooltips,
keyboard focus, and targets of at least 28 by 28 pixels. This scenario is tested by
`Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.

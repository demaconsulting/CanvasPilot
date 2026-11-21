### StyleSelection

#### Verification Approach

`StyleSelection` is verified with unit tests in `StyleSelectionTests.cs` over the real `ProjectService`, the real
`SettingsService`, and the real `StyleKitCatalog` on temporary folders. Projects are created with the project service,
so the choice is written to and read from a real project file, and the kits are real folders (two kits of the program
and kits that a test saves for the user or copies into the project). Nothing is substituted: the unit has no logic that
is worth isolating, and its job is the agreement of the three services. The tests check what is observable: the answers
(`Active`, `ActiveId`, `IsMissing`), the text of the project file, the announcements, and the values of the export
sizes. The way the sizes are used by the exporter is verified by a subsystem test.

#### Test Environment

N/A - standard test environment with temporary local folders and real file I/O. A background refresh that a project
change starts may still be reading the files when a test ends, so the removal of the folder tries again for a short
time.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- With no project, and with a project that has no style, there is no kit, no id, and nothing is missing.
- A project that names a kit that the catalog has uses the entry of the catalog; a kit that nothing has is missing with
  its id kept, is found when it appears, and is missing again when it is deleted; a copy of a kit in the project is the
  active kit; the answer follows another project and a closed project.
- A choice is in the project file as `activeStyleKit`, is read back when the file is opened again, is announced every
  time (also for the same kit), and no style removes the property. With no project, and with a text that is not an id,
  the choice is refused with a message and the file does not change.
- The default kit is applied to a project with no style once; it does not replace another choice, a missing kit that the
  project names, an unknown default, or an empty default.
- The sizes are those of the application with white backgrounds when there is no kit or the kit is missing; with a kit
  they are the sizes of the kit first, the sizes of the application it does not rename (ignoring case) after them, the
  background of the kit on every size that is not transparent, and no background on a transparent size.

#### Test Scenarios

**StyleSelection_Active_NoProjectOrNoStyle_IsNullAndNotMissing**: With no project and with a new project that has no
style there is no active kit and no id, and nothing is missing. This scenario is tested by
`StyleSelection_Active_NoProjectOrNoStyle_IsNullAndNotMissing`.

**StyleSelection_Active_ProjectNamesAKnownKit_ResolvesItThroughTheCatalog**: A project that uses a kit of the program
has that entry of the catalog as its active kit, with the place `BuiltIn`, and it is not missing. This scenario is
tested by `StyleSelection_Active_ProjectNamesAKnownKit_ResolvesItThroughTheCatalog`.

**StyleSelection_Active_UnknownKitThenAddedThenDeleted_IsMissingFoundAndMissingAgain**: A project names a kit that
nobody has: no active kit, missing, and the id kept. When the kit is saved for the user it is found; when it is deleted
it is missing again; the changes are announced. This scenario is tested by
`StyleSelection_Active_UnknownKitThenAddedThenDeleted_IsMissingFoundAndMissingAgain`.

**StyleSelection_Active_ProjectSwitchedOrClosed_FollowsTheOpenProject**: A second project with no style answers with no
kit; opening the first project again answers with its kit; closing the project answers with nothing; the switches are
announced. This scenario is tested by `StyleSelection_Active_ProjectSwitchedOrClosed_FollowsTheOpenProject`.

**StyleSelection_Active_KitCopiedIntoProject_IsTheProjectCopy**: After a kit of the program is copied into the project,
the active kit has the place `Project` and a folder inside the project. This scenario is tested by
`StyleSelection_Active_KitCopiedIntoProject_IsTheProjectCopy`.

**StyleSelection_SetActiveAsync_Choice_IsKeptInTheProjectFileAndAnnounced**: A choice is in the project file, a second
service reads it back, choosing the same kit again is announced again, and no style removes the property. This scenario
is tested by `StyleSelection_SetActiveAsync_Choice_IsKeptInTheProjectFileAndAnnounced`.

**StyleSelection_SetActiveAsync_NoProjectOrBadId_ThrowsWithTheReason**: A choice with no project fails with a message
that says that there is no open project; a text that is not an id fails with a message that says it is not a valid style
kit id; the project file is byte for byte as it was. This scenario is tested by
`StyleSelection_SetActiveAsync_NoProjectOrBadId_ThrowsWithTheReason`.

**StyleSelection_ApplyDefaultToNewProjectAsync_DefaultKit_IsAppliedOnceAndNotTwice**: With no project nothing happens;
for a new project the default kit becomes the style; after another kit is chosen a second call keeps the choice. This
scenario is tested by `StyleSelection_ApplyDefaultToNewProjectAsync_DefaultKit_IsAppliedOnceAndNotTwice`.

**StyleSelection_ApplyDefaultToNewProjectAsync_NoDefaultUnknownDefaultOrMissingKit_ChangesNothing**: With no default,
with a default that no kit has, and with a project that already names a missing kit, nothing is applied. This scenario
is tested by `StyleSelection_ApplyDefaultToNewProjectAsync_NoDefaultUnknownDefaultOrMissingKit_ChangesNothing`.

**StyleSelection_GetPresets_NoKit_IsTheApplicationSizesOverWhite**: With no kit the sizes are `PowerPoint 16:9` (1920 by
1080), `Email (800 wide)` (800 wide), and `A4 portrait @150 DPI` (1240 by 1754 at 150 dots per inch), each white and
from the application, with the identifiers and values that the test lists. This scenario is tested by
`StyleSelection_GetPresets_NoKit_IsTheApplicationSizesOverWhite`.

**StyleSelection_GetPresets_KitWithSizes_KitFirstThenApplicationNotRenamedOverTheKitBackground**: A kit with the sizes
`Banner`, `powerpoint 16:9`, and a transparent `Logo` offers those first, with their values and the background `#102030`
(none for the logo); the slide size of the application is hidden by the kit's size of the same name in another case; the
other two sizes of the application follow over `#102030`. This scenario is tested by
`StyleSelection_GetPresets_KitWithSizes_KitFirstThenApplicationNotRenamedOverTheKitBackground`.

**StyleSelection_GetPresets_MissingKit_IsTheApplicationSizesOverWhite**: A project that names a missing kit offers the
three sizes of the application over white. This scenario is tested by
`StyleSelection_GetPresets_MissingKit_IsTheApplicationSizesOverWhite`.

**StyleSelection_Changed_ProjectOrCatalogChange_IsRaisedUntilDisposed**: Opening a project and saving a kit each raise
the announcement; after disposal nothing is raised. This scenario is tested by
`StyleSelection_Changed_ProjectOrCatalogChange_IsRaisedUntilDisposed`.

**StyleSelection_Constructor_NullArguments_Throw**: A missing project service, catalog, or settings service is refused.
This scenario is tested by `StyleSelection_Constructor_NullArguments_Throw`.

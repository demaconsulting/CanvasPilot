### StyleKitManager

#### Verification Approach

`StyleKitManager` is verified with unit tests in `StyleKitManagerTests.cs` over a real `StyleKitCatalog`, the real
`StyleKitArchive`, and the real editor on temporary folders, with hand-written dialogs and pickers
(`FakeStyleKitDialogs` records what the window asked for and answers as the test says), a hand-written active style, and
a render service that completes at once, fails for the files that the test lists, or waits for the test
(`RecordingRenderService`). The tests read the groups, the preview values, the commands that can run, the status
message, the files and folders that a command leaves, and the zip files that it writes. The cache of small pictures is
tested through the same render service and through a substitute that throws. The subsystem test
`StyleSubsystem_ManageKitsInWindow_ManagerAndEditor_CreateEditExportImportAndDeleteAKit` makes, edits, draws, exports,
deletes, and imports a kit through the window. Headless UI tests in `StyleKitManagerViewTests.cs` (the `UiTests`
project, using Avalonia's headless platform with the Skia renderer) show the real window over a real catalog, project
service, and selection with the kits that come with the program and the real render service, so the example images are
really drawn; they click the cards and the buttons with the mouse and read the controls and the files, with hand-written
pickers and questions. The tests of the main window show the window from the Tools menu. The platform pickers cannot be
shown headless; they are checked by hand before a release together with the other pickers of the application.

#### Test Environment

Unit tests use the standard test environment with temporary local folders and real file I/O. No network, no window, and
no platform picker is used. A render that has to be slow is a render that waits for a task of the test. UI tests run
under the Avalonia headless platform with the Skia renderer, serialized with test parallelization disabled; no display
is required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The groups follow the places in the order of precedence, the group of the project exists only while a project is open,
  a shared folder that cannot be reached and a folder that has a kit with errors are noted in their group, and an empty
  group says what to do.
- A kit that another kit hides is dimmed, says what hides it, and cannot be used in the project; the kit that the
  project uses is marked.
- A chosen kit shows its colors with roles and label colors that read, fonts, numbers, Mermaid and Vega-Lite lines,
  advice, sizes, warnings, the text for an assistant and its length (never over 4,000 characters), and the place.
- The pictures of the examples are drawn 240 pixels wide from the folder of their kit, once for each file, not again
  when the kit is chosen again, again when the file has changed, with the reason in place of a picture that cannot be
  drawn, and not shown for a kit that is no longer chosen.
- Edit and Delete are unavailable for a kit of the program and for a shared kit and available for the kit of the user;
  Duplicate, Export, Use in project, and Copy into project are available for the former; Use in project and Copy into
  project need a project.
- Use in project writes the id and marks the kit, and a refusal is said. New makes the kit in the place that is chosen,
  with a prompt that names the place, opens the editor, and a cancel and a missing project change nothing. Duplicate
  makes a copy with the examples under the typed name. Edit shows the saved kit. Delete asks, removes the folder when
  confirmed, and leaves the kit when not. Export writes a zip file with the style file and the examples under the
  suggested name, and a place that cannot be written is said. Import brings a kit back, asks to replace, keep both, or
  stop when the id is taken, says why an unsafe archive and a file that is not a zip file are refused and writes nothing
  outside the place, and needs a project when the project is the place. Copy into project keeps the id and examples, and
  a second copy is refused with the reason.
- Refresh reads the places again, Close asks the window to close, the group of the project follows the project, and
  after Dispose the view model does not follow the catalog any more.
- The cache draws a file that is asked for twice once, keeps the picture, keeps drawing when a caller stops waiting,
  forgets the least recently used picture when full, does not remember a failure that may pass, and reports a missing
  file.
- Missing arguments are refused.
- The real window lists the groups and a card for each kit that comes with the program, shows the preview of a chosen
  kit (name, colors, text for an assistant and its length) with a picture drawn by the real renderer for each example,
  tints the chosen card and gives only that card a check icon, and shows the note of a shared folder that cannot be
  reached. Its buttons follow the rules above: Edit and Delete are off for a kit of the program, Use in this project
  and Copy into project need a project, and the buttons use the project (the id is written into the project file and
  the card of the kit in use is marked), edit (the list shows the saved kit), delete (after a question), export,
  import, make a kit, refresh, and close the window.
- In the accessibility sweep of the windows of the application, in both theme variants, every button of this window that
  shows no text has a name and a tooltip, every text field, list, and drop-down has a name, and every enabled button can
  take the keyboard focus.

#### Test Scenarios

**StyleKitManager_Groups_PlacesKitsAndNotes_AreListedByPlace**: With kits in the places of the program, the user, and a
shared folder, a second shared folder that is not there, and a shared folder with a kit whose style file is not JSON,
the groups are My kits, Shared, and Built-in without a project and This project, My kits, Shared, and Built-in with one;
the kits of the user are in order of name; the shared group notes the folder that is not reachable and the kit that
cannot be used; the group of the project is empty and says to copy a kit into the project. This scenario is tested by
`StyleKitManager_Groups_PlacesKitsAndNotes_AreListedByPlace`.

**StyleKitManager_Groups_EmptyPlaces_SayWhatToDo**: With no kits, each group says what to do (New or Duplicate, no
shared folders are set, no kits come with the program) and nothing is chosen. This scenario is tested by
`StyleKitManager_Groups_EmptyPlaces_SayWhatToDo`.

**StyleKitManager_Groups_OverriddenKit_IsDimmedAndCannotBeUsed**: A built-in kit with the id of a kit of the user is
dimmed (opacity 0.5), says that it is hidden by the kit in My kits, and cannot be used in the project, while the kit of
the user is shown in full and can. This scenario is tested by
`StyleKitManager_Groups_OverriddenKit_IsDimmedAndCannotBeUsed`.

**StyleKitManager_Select_Kit_ShowsPreviewData**: A kit with every part and a font that is not bundled is chosen; the
colors, their label colors (black on white and amber, white on dark and blue), the strip, the font lines, the numbers,
the Mermaid and Vega-Lite lines, the advice, the sizes (`1920 x 1080 px` and `1600 px wide, 150 dpi, transparent`), the
one warning that names `fonts.heading`, the text for an assistant and its length, the place, and the flags are as the
kit says, and the other kit is not marked as chosen. This scenario is tested by
`StyleKitManager_Select_Kit_ShowsPreviewData`.

**StyleKitManager_Thumbnails_ChosenKit_AreDrawnOnceAndAgainWhenTheFileChanges**: A kit with two examples is chosen; both
are drawn with width 240 from the folder of the kit; choosing nothing and the kit again draws nothing; a file that was
changed is drawn again; the pictures are shown with their captions and none has a placeholder. This scenario is tested
by `StyleKitManager_Thumbnails_ChosenKit_AreDrawnOnceAndAgainWhenTheFileChanges`.

**StyleKitManager_Thumbnails_FailingRender_ShowsAPlaceholderWithTheReason**: One of two examples cannot be drawn; the
other is shown, and the first has the text `Could not be drawn:` and the reason. This scenario is tested by
`StyleKitManager_Thumbnails_FailingRender_ShowsAPlaceholderWithTheReason`.

**StyleKitManager_Thumbnails_AnotherKitChosenWhileDrawing_DoesNotShowTheOldPictures**: While a picture is being drawn
another kit is chosen; when the drawing ends the picture of the first kit is not shown and the kit that has no example
has no picture. This scenario is tested by
`StyleKitManager_Thumbnails_AnotherKitChosenWhileDrawing_DoesNotShowTheOldPictures`.

**StyleKitManager_Commands_BuiltInSharedAndOwnKits_EnableOnlyWhatApplies**: With nothing chosen only New, Import, and
Refresh can run; with a built-in or a shared kit chosen Edit and Delete cannot run and Duplicate, Export, Use in
project, and Copy into project can, and the kit says that it cannot be changed; with a kit of the user everything can.
This scenario is tested by `StyleKitManager_Commands_BuiltInSharedAndOwnKits_EnableOnlyWhatApplies`.

**StyleKitManager_UseInProject_ChosenKit_IsMadeTheActiveStyleOrTheReasonIsSaid**: The command is not available without a
project; with one it writes the id of the kit, says so, and the kit is marked as in use when the active style announces
it; when the active style refuses, its message is said. This scenario is tested by
`StyleKitManager_UseInProject_ChosenKit_IsMadeTheActiveStyleOrTheReasonIsSaid`.

**StyleKitManager_New_NamePromptAndTargets_MakesTheKitAndOpensTheEditor**: A cancelled prompt makes nothing; a name
makes the kit in the kits of the user, chooses it, and opens the editor; the project as the place with no project says
that a project is needed and opens no editor; with a project the kit is made in the `styles` folder of the project. This
scenario is tested by `StyleKitManager_New_NamePromptAndTargets_MakesTheKitAndOpensTheEditor`.

**StyleKitManager_Duplicate_BuiltInKit_MakesAnEditableCopyWithItsExamples**: A built-in kit is duplicated under a typed
name; the prompt starts with the name and `copy`; the copy has the example file, is chosen, is not read only, and is
opened in the editor; a cancelled prompt makes nothing. This scenario is tested by
`StyleKitManager_Duplicate_BuiltInKit_MakesAnEditableCopyWithItsExamples`.

**StyleKitManager_Edit_UserKit_OpensTheEditorAndShowsTheSavedKit**: An editor that is canceled leaves the name; an
editor that renames the kit and saves is reflected in the list, the kit stays chosen, and the file has the new name.
This scenario is tested by `StyleKitManager_Edit_UserKit_OpensTheEditorAndShowsTheSavedKit`.

**StyleKitManager_Edit_ExamplePicker_IsTheWindowPicker**: The editor that the window opens adds the example files that
the window's picker returns. This scenario is tested by `StyleKitManager_Edit_ExamplePicker_IsTheWindowPicker`.

**StyleKitManager_Delete_UserKit_AsksAndRemovesTheFolder**: Saying no leaves the folder; saying yes removes it, leaves
the other kit, clears the choice, and says so; the question names the kit. This scenario is tested by
`StyleKitManager_Delete_UserKit_AsksAndRemovesTheFolder`.

**StyleKitManager_Export_ChosenKit_WritesAZipFileOrSaysWhy**: A cancelled picker writes nothing; a file is written with
the style file and the example, under the suggested name `team.zip`; a folder that is not there is said. This scenario
is tested by `StyleKitManager_Export_ChosenKit_WritesAZipFileOrSaysWhy`.

**StyleKitManager_Import_ZipFile_ImportsAndAsksAboutAnExistingKit**: A cancelled picker imports nothing; a zip file that
was exported from a kit that was then deleted brings the kit back with its example, chooses it, and asks nothing; the
same file again asks whether to replace, then whether to keep both, and keeps both as `team-2` named `Team (2)`; saying
no to both says that nothing was imported. This scenario is tested by
`StyleKitManager_Import_ZipFile_ImportsAndAsksAboutAnExistingKit`.

**StyleKitManager_Import_HostileZipAndMissingProject_AreRefusedWithAReason**: A zip file with an entry that climbs out
of the place and a file that is not a zip file are refused with their reason and nothing is written outside the place;
with the project as the place and no project, the user is told to open one; no question is asked. This scenario is
tested by `StyleKitManager_Import_HostileZipAndMissingProject_AreRefusedWithAReason`.

**StyleKitManager_CopyIntoProject_BuiltInKit_FreezesACopyInTheProject**: Without a project the command is not available;
with one, the built-in kit is copied with its example into the `styles` folder of the project, the copy is chosen, and
the command is not available for it; copying the hidden built-in kit again says that the project has a kit with that id.
This scenario is tested by `StyleKitManager_CopyIntoProject_BuiltInKit_FreezesACopyInTheProject`.

**StyleKitManager_RefreshCloseAndProjectChanges_FollowTheCatalogAndTheProject**: A kit that was written is listed after
Refresh, which says so; Close asks the window to close once; the group of the project appears when a project is opened
and goes when it is closed. This scenario is tested by
`StyleKitManager_RefreshCloseAndProjectChanges_FollowTheCatalogAndTheProject`.

**StyleKitManager_Dispose_StopsFollowingAndArgumentsAreChecked**: The place for a new kit is held to the two places;
after Dispose a change of the catalog does not change the groups, and Dispose may be called twice; each missing argument
of the view model and of the item, thumbnail, and group is refused. This scenario is tested by
`StyleKitManager_Dispose_StopsFollowingAndArgumentsAreChecked`.

**StyleThumbnailCache_GetAsync_ConcurrentRepeatedAndAbandoned_DrawsOnce**: Two callers that ask for a file at once, one
that stops waiting, and a later caller cause one drawing and get the same picture. This scenario is tested by
`StyleThumbnailCache_GetAsync_ConcurrentRepeatedAndAbandoned_DrawsOnce`.

**StyleThumbnailCache_GetAsync_FullCache_ForgetsTheOneUsedLeastRecently**: In a cache for two pictures, a third file
pushes out the picture that was asked for longest ago and not the one that was asked for again. This scenario is tested
by `StyleThumbnailCache_GetAsync_FullCache_ForgetsTheOneUsedLeastRecently`.

**StyleThumbnailCache_GetAsync_MissingFileAndFailures_AreReportedAndNotRemembered**: A file that is not there is
reported without a drawing; a render service that throws is a failure that is drawn again the next time; arguments are
checked. This scenario is tested by `StyleThumbnailCache_GetAsync_MissingFileAndFailures_AreReportedAndNotRemembered`.

**StyleSubsystem_ManageKitsInWindow_ManagerAndEditor_CreateEditExportImportAndDeleteAKit**: A kit is made through the
window, edited in the editor with a new color and an example image with a caption, drawn as a small picture from the
folder of the kit, exported, deleted, and imported again; it is back with its name, color, example, and caption. This
scenario is tested by `StyleSubsystem_ManageKitsInWindow_ManagerAndEditor_CreateEditExportImportAndDeleteAKit`.

**StyleKitManagerView_Show_ListsGroupsAndChoosingAKitShowsThePreview**: The window shows the groups My kits, Shared, and
Built-in, a card for Clean, DEMA, and Slides Dark, and the note that no kit is chosen; clicking the card of DEMA tints
it, hides the note, shows its name, one swatch for each color, a picture for each of its three examples (drawn by the
real renderer) with their labels, and the length of the text for an assistant out of 4,000 characters. This scenario is
tested by `StyleKitManagerView_Show_ListsGroupsAndChoosingAKitShowsThePreview`.

**StyleKitManagerView_Buttons_BuiltInAndOwnKits_EnableWhatApplies**: With a built-in kit chosen Edit, Delete, Use in
this project, and Copy into project are off and Duplicate, Export, New, and Import are on, and the preview says the kit
cannot be changed; with a kit of the user chosen Edit and Delete are on. This scenario is tested by
`StyleKitManagerView_Buttons_BuiltInAndOwnKits_EnableWhatApplies`.

**StyleKitManagerView_UseInProjectAndCopyIntoProject_ChangeTheProject**: With a project open, Copy into project makes a
copy of the chosen built-in kit in the `styles` folder of the project and chooses it; Use in this project writes `clean`
as the `activeStyleKit` of the project file; the card of the copy is marked as in use and the group of the project is
listed first. This scenario is tested by `StyleKitManagerView_UseInProjectAndCopyIntoProject_ChangeTheProject`.

**StyleKitManagerView_DeleteExportImportAndEdit_ChangeTheListAndTheFiles**: An editor that renames the kit and saves is
reflected in the card; Export writes the zip file; Delete asks and leaves the kit when the answer is no and removes the
folder and the card when it is yes; Import brings the kit back and says so in the status line. This scenario is tested
by `StyleKitManagerView_DeleteExportImportAndEdit_ChangeTheListAndTheFiles`.

**StyleKitManagerView_NewNotesAndClose_ShowTheKitTheNoteAndCloseTheWindow**: New makes the kit named in the prompt and
shows it in the preview; a shared folder that is not there is noted in its group; Refresh says the kits were read again;
Close closes the window. This scenario is tested by
`StyleKitManagerView_NewNotesAndClose_ShowTheKitTheNoteAndCloseTheWindow`.

**StyleKitManagerView_ChoosingAKit_MarksOnlyThatCardWithACheck**: The window is shown over the kits that come with the
program and the second card is clicked; that card has the class `selected` and shows one check icon, and no other card
shows one. This scenario is tested by `StyleKitManagerView_ChoosingAKit_MarksOnlyThatCardWithACheck`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: The main window with and without a project and each
dialog and window of the application, among them the Style Kits window, are shown in both theme variants and every
control is looked at; a button or toggle that shows no text has a name and a tooltip, a text box, drop-down, list, and
slider has a name, every enabled button can take the keyboard focus, and an icon button is at least 28 by 28. This
scenario is tested by `Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.

**StyleKitManagerView_TargetList_AnnouncesTheShownPlace**: A shown window over the kits that come with the program; the
value that the closed list of where new kits go gives to automation is `My kits`, which is the text that the list
shows. This scenario is tested by `StyleKitManagerView_TargetList_AnnouncesTheShownPlace`.

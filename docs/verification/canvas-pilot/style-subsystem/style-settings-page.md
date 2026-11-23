### StyleSettingsPage

#### Verification Approach

`StyleSettingsPage` is verified with unit tests in `StyleSettingsPageTests.cs` over a real `StyleKitCatalog` and a real
`SettingsService` on temporary folders (the catalog reads real folders, so the status of a folder that exists, a folder
that does not, and a folder that the catalog was not told about are all real), with a hand-written folder picker that
answers as the test says. The tests read the rows, the choices, the selection, the commands, and the settings that the
page returns when applied. Headless UI tests in `StyleSettingsPageViewTests.cs` (the `UiTests` project, using Avalonia's
headless platform) show the real page, find controls by automation identifier, type into the text box and click the
buttons with the mouse, and show the page in the real settings dialog and click OK, then read the settings file. The
platform folder picker cannot be shown headless; the page is tested with a picker that answers, and the real picker is
checked by hand before a release together with the other pickers of the application. That the composed settings dialog
has the page in the right place is verified in the tests of the main window and of the AppShell subsystem.

#### Test Environment

Unit tests use the standard test environment with temporary local folders and real file I/O. UI tests run under the
Avalonia headless platform with the Skia renderer, serialized with test parallelization disabled; no display or
windowing system is required. No network folder is used: a folder that cannot be reached is a folder that is not there.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- Loading shows the folders in the order of the settings; a folder that exists and was read is reachable, one that does
  not exist is not reachable now, and one that the catalog was not told about has not been read yet; the title of the
  page is `Style Kits`.
- A typed or pasted full path, with white space around it, is added at the end and selected and the text box is cleared;
  the same folder with another case or a closing separator is refused with a message; a relative path, a path that
  climbs out with `..`, and a rooted path with no drive are refused with a message that names the text, the text stays,
  and the page stays valid; the button to add is unusable while the text is blank.
- Browsing adds the folder that the user chooses, starting in the selected folder when it exists; a cancel, and a window
  that has no picker yet, add nothing.
- Remove, Move up, and Move down act on the selected folder, are usable only where they can work, and keep the moved
  folder selected, and removing selects the folder that took its place.
- Applying returns the settings with the folders in their order and the default kit, and every other member unchanged.
- A folder that is not a full path makes the page invalid, its row says so, applying is refused with an exception, and
  removing the folder makes the page valid again; a folder that cannot be reached is kept when applying.
- The choices of the default kit are no style and the kits that are not hidden, with their places; the default of the
  settings is selected; a default that no kit has is shown as not found, is kept when applying, and is not offered again
  after the settings are loaded with another default; choosing another kit or no style changes what is applied.
- While the page is open it follows the catalog (the status of a folder and the kits that can be the default), after
  `OnClosed` it does not, and after another `Load` it does again.
- In the settings dialog, a folder that is typed and a default kit that is chosen are written to the settings file when
  OK is clicked, and the page is listed with the other pages.
- A missing catalog, function for the picker, or settings is refused; the rule for a folder is a full path without a
  character that a path cannot have.
- In the accessibility sweep of the windows of the application, in both theme variants, every button of the page that
  shows no text has a name and a tooltip, every text box, list, and drop-down has a name, and every enabled button can
  take the keyboard focus.

#### Test Scenarios

**StyleSettingsPage_Load_Folders_AreListedInOrderWithTheirStatus**: Three shared folders, of which the first exists and
has been read, the second does not exist, and the third is not known to the catalog, are listed in that order with the
statuses `Reachable.`, `Not reachable now...`, and `Not read yet...`, all valid, and the page is valid. This scenario is
tested by `StyleSettingsPage_Load_Folders_AreListedInOrderWithTheirStatus`.

**StyleSettingsPage_AddTypedFolder_FullPath_IsAddedSelectedAndDuplicatesAreRefused**: A path with white space around it
is added after the first folder and selected, the text box is cleared and there is no message; the first folder spelled
in upper case with a closing separator is refused with `in the list already`. This scenario is tested by
`StyleSettingsPage_AddTypedFolder_FullPath_IsAddedSelectedAndDuplicatesAreRefused`.

**StyleSettingsPage_AddTypedFolder_NotAFullPath_IsRefusedWithAMessage**: `kits\team`, `..\team`, and `\team` are each
refused with a message that names the text, nothing is added, the text stays, and the page is valid. This scenario is
tested by `StyleSettingsPage_AddTypedFolder_NotAFullPath_IsRefusedWithAMessage`.

**StyleSettingsPage_BrowseFolder_PickerChoosesCancelsOrIsMissing_AddsOrDoesNothing**: With the first folder selected,
the user chooses a second folder and it is added, and the picker was asked to start in the first folder; a cancel adds
nothing; a page whose window has no picker adds nothing. This scenario is tested by
`StyleSettingsPage_BrowseFolder_PickerChoosesCancelsOrIsMissing_AddsOrDoesNothing`.

**StyleSettingsPage_RemoveAndMove_SelectedFolder_ChangesTheOrderAndTheCommandsFollow**: With three folders and none
selected no command is usable; the first folder cannot move up and moves down twice and then cannot move down, staying
selected; it moves up once; removing the selected folder leaves the other two and selects the one that took its place.
This scenario is tested by `StyleSettingsPage_RemoveAndMove_SelectedFolder_ChangesTheOrderAndTheCommandsFollow`.

**StyleSettingsPage_Apply_FoldersAndDefaultKit_MergesOnlyTheStyles**: Settings with a recent project and a model name
are loaded into a page, in which two folders are added, one is moved up, and the kit `clean` is chosen as the default;
the applied settings have the two folders in the new order and the default kit, and are equal to the loaded settings in
every other member. This scenario is tested by `StyleSettingsPage_Apply_FoldersAndDefaultKit_MergesOnlyTheStyles`.

**StyleSettingsPage_IsValid_EntryThatIsNotAFullPath_BlocksApplyUntilRemoved**: Settings with a folder that does not
exist and a relative folder make the page invalid, the second row says that it is not a full path, applying throws, and
the change of `IsValid` is announced; removing the relative folder makes the page valid and applying keeps the folder
that does not exist. This scenario is tested by
`StyleSettingsPage_IsValid_EntryThatIsNotAFullPath_BlocksApplyUntilRemoved`.

**StyleSettingsPage_DefaultKit_ChoicesSelectionAndUnknownDefault_AreShownAndApplied**: With settings that name the kit
`team-kit`, which no kit has, the choices are no style, the two kits of the program, and `team-kit (not found)`, which
is selected and is kept when applied; choosing `slides-dark` and then no style changes what is applied; loading settings
that name `clean` selects `Clean (built-in)` and drops the choice that was not found. This scenario is tested by
`StyleSettingsPage_DefaultKit_ChoicesSelectionAndUnknownDefault_AreShownAndApplied`.

**StyleSettingsPage_CatalogChanges_AreFollowedWhileOpenOnly**: A page loaded before the kits were read says that the
folder has not been read and shows `clean (not found)`; after the catalog has read the places it says `Reachable.` and
`Clean (built-in)`; after `OnClosed` (twice) and the loss of the folder the status stays as it was; loading again shows
`Not reachable now`, and when the folder is back and the catalog reads it again the page says `Reachable.`. This
scenario is tested by `StyleSettingsPage_CatalogChanges_AreFollowedWhileOpenOnly`.

**StyleSettingsPage_NullArguments_Throw**: A missing catalog, function for the picker, and missing settings for `Load`
and `Apply` are refused. This scenario is tested by `StyleSettingsPage_NullArguments_Throw`.

**StyleSettingsPage_IsFullPath_Texts_FollowTheRuleOfTheSettings**: A folder of the temporary root and a network path are
full paths; nothing, white space, a bare name, a drive with no root, and a text with a character that a path cannot have
are not. This scenario is tested by `StyleSettingsPage_IsFullPath_Texts_FollowTheRuleOfTheSettings`.

**StyleSettingsPageView_Show_ListsFoldersAndTheDefaultKit**: The real page, with the real kits of the program, lists the
folder with the status `Not read yet`, offers no style and the kits of the program (among them `Clean (built-in)` and
`DEMA (built-in)`), has `slides-dark` selected, and is titled `Style Kits`. This scenario is tested by
`StyleSettingsPageView_Show_ListsFoldersAndTheDefaultKit`.

**StyleSettingsPageView_Buttons_AddMoveAndRemoveFolders**: Typing two full paths and clicking Add lists them; a third
text that is not a full path shows the red message that names it; selecting the second and clicking Move up changes the
order; selecting the second again and clicking Remove leaves one folder. This scenario is tested by
`StyleSettingsPageView_Buttons_AddMoveAndRemoveFolders`.

**SettingsDialogView_StyleKitsPage_OkButtonSavesFoldersAndDefaultKit**: In the real settings dialog over the AI Provider
page and this page, the page is listed second; a typed folder and the kit DEMA as the default are chosen, and clicking
OK writes both to the settings file. This scenario is tested by
`SettingsDialogView_StyleKitsPage_OkButtonSavesFoldersAndDefaultKit`.

**AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages**: The settings dialog of the composed shell
has the page third, after the AI Provider and Speech pages. This scenario is tested by
`AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages`.

**MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses**: The Settings menu item of the real main window
opens a dialog that lists the page with the other two, and Cancel closes it. This scenario is tested by
`MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: The settings dialog of the real application is shown in
both theme variants and each of its pages, this one among them, is chosen and every control of it is looked at; a
button that shows no text has a name and a tooltip, a text box, list, and drop-down has a name, and every enabled button
can take the keyboard focus. This scenario is tested by `Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.

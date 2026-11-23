### StylePicker

#### Verification Approach

`StylePicker` is verified with unit tests in `StylePickerTests.cs` over the real `ProjectService`, the real
`SettingsService`, the real `StyleKitCatalog`, and the real `StyleSelection` on temporary folders. The kits are real
folders (two kits of the program, and kits of the user that a test writes), the project is created with the project
service, and the choice is read back from the text of the project file, so nothing about the choice is substituted. The
tests check what is observable: the labels, tool tips, names, and colors of the list, the selection, the visibility, the
error message, the command behind the footer entry, and the `activeStyleKit` property of the project file. Headless UI
tests in `StylePickerViewTests.cs` (the `UiTests` project, using Avalonia's headless platform) compose the whole
application, show the real main window, find the title bar and the pill by automation identifier, and choose a kit by
index, as a user would. The look of the pill (its colors in the two themes and the width of the list) is checked by eye
before a release.

#### Test Environment

Unit tests use the standard test environment with temporary local folders and real file I/O. The refresh that a project
change starts may still be reading when a test ends, so the removal of the folder tries again for a short time. UI tests
run under the Avalonia headless platform with the Skia renderer, serialized with test parallelization disabled; no
display or windowing system is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The list holds no style first, then a choice for each kit that is not hidden by a kit of a more local place (a kit of
  the user that has the id of a kit of the program replaces it in the list), each with the place it comes from; the tool
  tip of a kit has its description and folder.
- The list is shown only while a project is open, as a pill in the title bar of the window.
- The pill shows the name of the chosen kit without its place and up to three dots in the colors of its primary,
  secondary, and accent roles (then other roles, never the background or the text); no style has no dots; the pill is 26
  pixels high and has the name `Style kit` for assistive technology.
- The list ends with the entry `Manage style kits...` only when the host gave the picker the command; using the entry
  opens the window that manages kits and closes the list.
- Choosing a kit writes its id into the project file and keeps it selected; choosing no style removes the property.
- A kit that the project names and that cannot be found is the selected choice, says that it was not found, is left
  alone when it is chosen again, and is gone from the list when another choice is made.
- A choice that cannot be kept (no project) shows why, shows no style again as what the project uses, and the next
  choice that works clears the message.
- A choice that is made by another part of the application is shown without being written again.
- Asking for the default kit of a new project is passed on when there is a way to give it, and is nothing when there is
  none; a disposed picker no longer follows the kits; a missing collaborator is refused.

#### Test Scenarios

**StylePicker_Items_KitsOfTheCatalog_NoStyleFirstAndVisibleOnlyWithAProject**: With two kits of the program, the list
is `(No style)`, `Clean (built-in)`, and `Slides Dark (built-in)`, hidden with no project and visible with one, with no
style selected, with the description and folder of a kit in its tool tip; closing the project hides it again. This
scenario is tested by `StylePicker_Items_KitsOfTheCatalog_NoStyleFirstAndVisibleOnlyWithAProject`.

**StylePicker_Items_KitPalette_GivesNameAndUpToThreeMainColors**: A kit with seven colors (background, text, highlight,
accent, primary, secondary, and extra), the kit `Clean` with one color, and no style are listed; no style has the name
`(No style)` and no colors, `Clean` has the name `Clean`, the label `Clean (built-in)`, and its one color, and the rich
kit has the name `Vivid` and the colors of its primary, secondary, and accent roles in that order, without the
background or the text. This scenario is tested by `StylePicker_Items_KitPalette_GivesNameAndUpToThreeMainColors`.

**StylePicker_ManageKitsCommand_SetThenCleared_IsAnnouncedAndTold**: A picker has no footer entry; a command is set and
the picker says it has one, running the command through the picker runs it once, and taking the command away says it
has none; each change of the command announces `HasManageKits`. This scenario is tested by
`StylePicker_ManageKitsCommand_SetThenCleared_IsAnnouncedAndTold`.

**StylePicker_Select_KitThenNoStyle_IsWrittenToTheProjectFile**: Choosing a kit writes its id into the project file and
keeps it selected; choosing no style removes it; no error is shown. This scenario is tested by
`StylePicker_Select_KitThenNoStyle_IsWrittenToTheProjectFile`.

**StylePicker_MissingKit_IsShownAsNotFoundAndGoesWhenAnotherIsChosen**: A project that names `team-kit` shows
`team-kit (not found)` as the last choice and selected, with an explanation; choosing it again leaves the file as it is;
choosing `clean` writes it and removes the missing choice. This scenario is tested by
`StylePicker_MissingKit_IsShownAsNotFoundAndGoesWhenAnotherIsChosen`.

**StylePicker_Items_KitHiddenByAMoreLocalKit_IsListedOnceWithTheLocalPlace**: A kit of the user with the id `clean` is
listed once, as `My Clean (my kits)`, and the list has three choices. This scenario is tested by
`StylePicker_Items_KitHiddenByAMoreLocalKit_IsListedOnceWithTheLocalPlace`.

**StylePicker_Select_NoProject_ReportsTheReasonAndShowsNoStyleAgain**: With no project, choosing a kit shows a message
that names the project and puts the selection back on no style; after a project is open, a choice works and the message
is gone. This scenario is tested by `StylePicker_Select_NoProject_ReportsTheReasonAndShowsNoStyleAgain`.

**StylePicker_SelectedItem_ChoiceMadeElsewhere_IsFollowedWithoutWritingAgain**: When `StyleSelection` is asked to use a
kit, the list selects it and no choice of the list was started. This scenario is tested by
`StylePicker_SelectedItem_ChoiceMadeElsewhere_IsFollowedWithoutWritingAgain`.

**StylePicker_ApplyDefaultToNewProjectAsync_PassesOnOrDoesNothing**: A picker that was given the way applies the default
kit of the user to the open project; a picker that was given none leaves the file alone. This scenario is tested by
`StylePicker_ApplyDefaultToNewProjectAsync_PassesOnOrDoesNothing`.

**StylePicker_PlaceText_EveryPlace_IsNamedForTheUser**: The four places are named `project`, `my kits`, `shared`, and
`built-in`. This scenario is tested by `StylePicker_PlaceText_EveryPlace_IsNamedForTheUser`.

**StylePicker_DisposeAndConstructor_StopsFollowingAndRefusesNull**: After disposal (twice) the list is not changed when
the kits are read, and each missing collaborator is refused. This scenario is tested by
`StylePicker_DisposeAndConstructor_StopsFollowingAndRefusesNull`.

**StylePickerView_TitleBar_ShownOnlyWithAProjectAndListsTheKits**: In the real window the pill is hidden with no
project; with a project it is shown inside the title bar with the name `Style kit`, and its list holds no style and the
three kits of the program, with no style selected. This scenario is tested by
`StylePickerView_TitleBar_ShownOnlyWithAProjectAndListsTheKits`.

**StylePickerView_Pill_ShowsNameAndSwatchesOfTheChosenKit**: With a project open the pill shows the words `(No style)`;
after the DEMA kit is chosen the pill is 26 high, shows only the name `DEMA`, and shows between one and three dots, each
with a color; no error text is shown. This scenario is tested by
`StylePickerView_Pill_ShowsNameAndSwatchesOfTheChosenKit`.

**StylePickerView_ManageStyleKitsEntry_OpensTheWindowThatManagesKits**: The list is opened and the entry
`Manage style kits...` is clicked; the Style Kits window is open and the list is closed. This scenario is tested by
`StylePickerView_ManageStyleKitsEntry_OpensTheWindowThatManagesKits`.

**StylePickerView_ChooseKit_WritesTheProjectFile**: Choosing a kit in the combo box by index writes it into the project
file and the active style follows; choosing no style removes it. This scenario is tested by
`StylePickerView_ChooseKit_WritesTheProjectFile`.

**StylePickerView_MissingKit_IsShownAsNotFound**: A project that names an unknown kit shows `team-kit (not found)` as
the selected entry of the combo box. This scenario is tested by `StylePickerView_MissingKit_IsShownAsNotFound`.

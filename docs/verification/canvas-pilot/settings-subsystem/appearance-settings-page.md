### AppearanceSettingsPage

#### Verification Approach

`AppearanceSettingsPage` is verified by unit tests in `AppearanceSettingsPageTests.cs`, which drive the view model with
real `AppSettings` records and need no test doubles, and by headless UI tests in `AppearanceSettingsPageViewTests.cs`,
which show the real `SettingsDialogView` over the page and a real `SettingsService` on a temporary folder, read the
controls by automation identifier, change them, and choose OK.

#### Test Environment

The unit tests use the standard test environment. The UI tests run under the Avalonia headless platform with the
embedded Inter font, serialized with test parallelization disabled; no display is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- A new page lists Follow system, Dark, and Light in that order, shows the default look, and is valid.
- Loading shows the stored theme and options, and applying returns the settings with only the appearance replaced.
- The dialog shows the page with a themed list that has the name `Theme` and two check boxes, each with an automation
  identifier, and choosing OK stores the edited choices.

#### Test Scenarios

**AppearanceSettingsPage_New_ListsThemesInOrderAndShowsTheDefaultLook**: A new page is titled Appearance, lists the
three themes in order, follows the system, shows thumbnails, has compact rows, and is valid. This scenario is tested by
`AppearanceSettingsPage_New_ListsThemesInOrderAndShowsTheDefaultLook`.

**AppearanceSettingsPage_Load_StoredAppearance_IsShown**: Settings with the light theme, no thumbnails, and comfortable
rows are loaded; the page shows them. This scenario is tested by `AppearanceSettingsPage_Load_StoredAppearance_IsShown`.

**AppearanceSettingsPage_Apply_EditedValues_MergesOnlyTheAppearance**: A page loaded with settings that hold a recent
project, a model name, and a window width is edited to the dark theme, no thumbnails, and comfortable rows; the applied
settings differ from the original in the appearance only. This scenario is tested by
`AppearanceSettingsPage_Apply_EditedValues_MergesOnlyTheAppearance`.

**AppearanceSettingsPage_LoadThenApply_NoEdit_KeepsTheAppearance**: Loading and applying without an edit gives back the
same appearance. This scenario is tested by `AppearanceSettingsPage_LoadThenApply_NoEdit_KeepsTheAppearance`.

**AppearanceSettingsPage_LoadAndApply_NullSettings_Throw**: A null argument is refused by both methods. This scenario is
tested by `AppearanceSettingsPage_LoadAndApply_NullSettings_Throw`.

**AppearanceSettingsPageView_Show_ShowsStoredAppearanceWithAutomationIds**: A dialog over the page and stored settings
shows the page with the three themes, the stored theme selected, the list named `Theme`, and both check boxes in their
stored state. This scenario is tested by `AppearanceSettingsPageView_Show_ShowsStoredAppearanceWithAutomationIds`.

**AppearanceSettingsPageView_ChooseThemeAndOptions_Ok_StoresThem**: Dark is chosen, the thumbnails are turned off, the
comfortable rows are turned on, and OK is chosen; a fresh service loads those choices. This scenario is tested by
`AppearanceSettingsPageView_ChooseThemeAndOptions_Ok_StoresThem`.

### AppearanceSettingsPage

![Settings Structure](SettingsSubsystemView.svg)

#### Purpose

`AppearanceSettingsPage` is the "Appearance" page of the settings dialog. It lets the user choose the theme variant of
the application and two options of the project explorer, and merges only those choices into the settings. It knows
nothing of windows or themes: the application watches the settings and shows the theme (see `ThemeResources`).

#### Data Model

**Title**: `string` — `Appearance`. **Description**: `The theme of the window and how the project explorer looks.`
**IconKey**: `CpIconImage`. The settings dialog shows them in its list of pages and above the page.

**Themes**: `IReadOnlyList<SettingsChoice<ThemeChoice>>` — `Follow system`, `Dark`, and `Light`, in that order.

**SelectedTheme**: `SettingsChoice<ThemeChoice>` — the choice that is edited; `Follow system` until settings are loaded.

**ShowThumbnails** and **ComfortableRows**: `bool` — whether the explorer shows a small picture beside each file
(default `true`) and whether its rows are taller (default `false`).

**IsValid**: `bool` — always `true`, because every combination of the choices can be stored.

**View**: `AppearanceSettingsPageView` shows two raised cards, each with a section title. The card `Theme` has a labeled
list of the themes (named `Theme`) and a hint that says what following the system means; the card `Project explorer` has
two check boxes. The view has a parameterless constructor for the XAML loader; the view locator binds it to
the view model, whose type name ends in `ViewModel`. Controls carry automation identifiers (`SettingsAppearancePage` for
the page, `AppearanceThemeComboBox`, `AppearanceShowThumbnailsCheckBox`, and `AppearanceComfortableRowsCheckBox`). The
card look and the hint come from the shared styles of the controls, so no color is written in the view.

#### Key Methods

**Load**: Replaces the working state with the theme and the options of `AppSettings.Appearance`; a theme that is not in
the list (which cannot happen for a normalized file) shows as `Follow system`.

**Apply**: Returns the given settings with `Appearance` replaced by the edited values (`with` expressions), so every
value that another page or subsystem owns is returned unchanged.

**OnClosed**: Does nothing, because the page has no background work.

#### Error Handling

`Load` and `Apply` throw `ArgumentNullException` for a null argument. No entry can be invalid, so `Apply` never throws
`InvalidOperationException`.

#### Dependencies

- **SettingsDialog** — hosts the page through `ISettingsPage` and calls `Load`, `Apply`, and `OnClosed`.
- **SettingsService** — the `AppSettings`, `AppearanceSettings`, and `ThemeChoice` types.
- **CommunityToolkit.Mvvm** — property change notification.
- **Avalonia** — the controls of the view; the colors come from the design tokens of `ThemeResources`.

#### Callers

- `SettingsDialog` — shows and applies the page.
- The application composition root — adds the page as the last page of the dialog.

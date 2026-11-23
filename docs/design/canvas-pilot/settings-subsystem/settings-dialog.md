### SettingsDialog

![Settings Structure](SettingsSubsystemView.svg)

#### Purpose

`SettingsDialog` is the modal settings window and its view model. It hosts the settings pages it is given
behind the `ISettingsPage` contract, so the `Settings` subsystem does not depend on the subsystems that
contribute pages, shows them in a rail with an icon and a title and the chosen page under its title and a sentence, and
it saves the pages' entries only when the user chooses OK or Apply.

#### Data Model

**ISettingsPage**: the page contract — `Title` (`string`), `Description` (`string`, the sentence that says what the
page is for; a default member that returns an empty string, so a page that has none shows no sentence), `IconKey`
(`string`, the resource key of the icon of the page, such as `CpIconSparkle`; a default member that returns
`CpIconSettings`), `IsValid` (`bool`, with change notification), `Load(AppSettings)`, `Apply(AppSettings)` returning the
updated settings, and `OnClosed()`. A page edits its own working state, so the dialog needs no copy of the settings. The
four pages give these values: AI Provider (icon `CpIconSparkle`), Speech (`CpIconMic`), Style Kits (`CpIconBraces`), and
Appearance (`CpIconImage`), each with a sentence.

**Pages**: `IReadOnlyList<ISettingsPage>` — the hosted pages in display order, supplied to the constructor.

**SelectedPage**: `ISettingsPage?` — the page shown on the right; the first page initially.

**ErrorMessage**: `string` — text describing the last failed save, or empty; the banner at the foot of the window shows
while it is not empty.

**AllPagesValid**: `bool` — true when every page's `IsValid` is true.

**Commands**: `OkCommand`, `ApplyCommand` (both enabled only while `AllPagesValid`), and `CancelCommand`.

**CloseRequested**: event with `DialogCloseRequestedEventArgs.Accepted` — raised when the dialog asks its host
to close; `true` for OK and `false` for Cancel.

**SettingsChoice**: `record SettingsChoice<T>(T Value, string Label)` — a value with its display text, used by
pages for combo boxes; `ToString` returns the label.

**IconKeyConverter** (`IconKeyConverter.Instance`): a value converter that turns the resource key of an icon into the
geometry that the application resources hold, so that a page names its icon without knowing how it is drawn. It looks
the key up in the resources of the running application for its theme variant and returns the geometry, or `null` when
there is no application, the key is not text, or the resource is not a geometry; the icon is then empty and the page
stays in the list. It does not convert back.

**View**: `SettingsDialogView` is an Avalonia window (860 by 620, at least 640 by 420, on the pane surface) in two rows.
The upper row has two columns:

- *Rail* (200 wide, on the window surface with a line at its right): a `ListBox` of the rail look (`SettingsPageList`,
  named `Settings pages`) bound to `Pages` and `SelectedPage`. Each entry has an icon (a `PathIcon` whose data is the
  `IconKey` of the page through the converter), the title, and, for the chosen entry, a short accent bar at the left.
- *Page*: a heading with the title of the chosen page (`SettingsPageTitle`, the page title look) and the sentence
  (`SettingsPageDescription`, the hint look, hidden when the sentence is empty), then a `ContentControl`
  (`SettingsPageContent`) that shows the selected page through the view locator.

The lower row is the footer bar (the window surface with a line above it): at the left the banner of a problem, which is
visible only while `ErrorMessage` is not empty, in the danger banner look with an error icon and the text
(`SettingsErrorMessage`, in the primary text color so that it reads on the tint), and at the right the buttons OK (the
primary look, the default button of the window), Cancel (the secondary look, the cancel button), and Apply (the
secondary look), each at least 88 wide (`SettingsOkButton`, `SettingsCancelButton`, `SettingsApplyButton`). The color
of every part comes from the design tokens. The four pages are drawn as raised cards by their own views (see the
design of each page), so a new page only has to put its cards in a scroll viewer.

**AutomationIds**: `SettingsPageTitle` and `SettingsPageDescription` are new; `SettingsPageList`, `SettingsPageContent`,
`SettingsOkButton`, `SettingsCancelButton`, `SettingsApplyButton`, and `SettingsErrorMessage` are unchanged
(`SettingsErrorMessage` is still the text block; the banner around it has no identifier).

#### Key Methods

**Constructor**: Loads every page with `ISettingsService.Current`, subscribes to each page's property changes,
and selects the first page.

**Apply** (command): When `AllPagesValid`, starts from `ISettingsService.Current`, passes it through each
page's `Apply` in order, and calls `Save` with the result. A successful save clears `ErrorMessage`; an
`IOException` sets `ErrorMessage` and keeps the dialog open.

**Ok** (command): Runs the apply step and, only when it succeeds, requests the host to close with acceptance.

**Cancel** (command): Requests the host to close without saving.

**NotifyClosed**: Unsubscribes from the pages and calls `OnClosed` on each, once. Later calls do nothing. The
OK and Cancel commands call it before requesting the close, and the view calls it when the window closes, so
pages are told exactly once however the dialog is closed.

**Page change handling**: When a page reports a change of `IsValid`, the dialog raises a change of
`AllPagesValid` and re-evaluates the OK and Apply commands.

#### Error Handling

The constructor throws `ArgumentNullException` for a null service or page list. A page that is invalid is
never applied: the commands are disabled, and the apply step returns without saving. A save failure
(`IOException`) is shown to the user rather than thrown.

#### Dependencies

- **SettingsService** (through `ISettingsService`) — supplies `Current` and stores the merged result.
- **CommunityToolkit.Mvvm** — property change notification and command source generators.
- **Avalonia** — window, list, and content controls; the design tokens and icons of `ThemeResources` that the view
  takes its colors, sizes, and icons from.

#### Callers

- The application's main window shell, through a dialog service, builds the view model with the pages the
  application provides and shows the window modally.

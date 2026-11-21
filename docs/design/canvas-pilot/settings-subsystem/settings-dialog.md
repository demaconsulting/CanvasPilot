### SettingsDialog

![Settings Structure](SettingsSubsystemView.svg)

#### Purpose

`SettingsDialog` is the modal settings window and its view model. It hosts the settings pages it is given
behind the `ISettingsPage` contract, so the `Settings` subsystem does not depend on the subsystems that
contribute pages, and it saves the pages' entries only when the user chooses OK or Apply.

#### Data Model

**ISettingsPage**: the page contract — `Title` (`string`), `IsValid` (`bool`, with change notification),
`Load(AppSettings)`, `Apply(AppSettings)` returning the updated settings, and `OnClosed()`. A page edits its
own working state, so the dialog needs no copy of the settings.

**Pages**: `IReadOnlyList<ISettingsPage>` — the hosted pages in display order, supplied to the constructor.

**SelectedPage**: `ISettingsPage?` — the page shown on the right; the first page initially.

**ErrorMessage**: `string` — text describing the last failed save, or empty.

**AllPagesValid**: `bool` — true when every page's `IsValid` is true.

**Commands**: `OkCommand`, `ApplyCommand` (both enabled only while `AllPagesValid`), and `CancelCommand`.

**CloseRequested**: event with `DialogCloseRequestedEventArgs.Accepted` — raised when the dialog asks its host
to close; `true` for OK and `false` for Cancel.

**SettingsChoice**: `record SettingsChoice<T>(T Value, string Label)` — a value with its display text, used by
pages for combo boxes; `ToString` returns the label.

**View**: `SettingsDialogView` is an Avalonia window with a page list (`ListBox` bound to `Pages` and
`SelectedPage`), a `ContentControl` showing the selected page through the view locator, an error line, and OK,
Cancel, and Apply buttons. Controls carry automation identifiers (`SettingsPageList`, `SettingsOkButton`,
`SettingsCancelButton`, `SettingsApplyButton`, `SettingsErrorMessage`).

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
- **Avalonia** — window, list, and content controls.

#### Callers

- The application's main window shell, through a dialog service, builds the view model with the pages the
  application provides and shows the window modally.

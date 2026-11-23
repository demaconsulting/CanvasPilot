### StylePicker

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StylePicker` is the pill in the title bar of the main window with which the user chooses the style kit of the open
project. The style belongs to the project, so the choice is one step away while the user works, and the pill always
shows what the project file really says: the kit that it names, also when that kit cannot be found, and not a kit that
was clicked and refused. The unit is a view model (`StylePickerViewModel`) with a small value for each choice
(`StylePickerItem`) and a view (`StylePickerView`) that the main window hosts in its title bar.

#### Data Model

- `StylePickerItem(Id, Label, ToolTip, IsMissing, Name, SwatchList)`: one choice. `Id` is the id of the kit, or `null`
  for no style; `Label` is the text in the list; `ToolTip` explains the choice; `IsMissing` is true for the one choice
  that stands for a kit that the project names and that cannot be found; `Name` is the name of the kit without its place
  (`null` for no style and for a missing kit); `SwatchList` is up to three colors of the palette as `#RRGGBB` separated
  by semicolons. Three derived values serve the view: `DisplayName` (the `Name`, or the `Label` when there is no name,
  which is what the pill shows), `Swatches` (the colors as a list), and `HasNoSwatches`. `ToString` is `DisplayName`,
  which is the text that a screen reader is given as the value of the pill and as the name of a choice, instead of the
  text of the record with all its values.
- *Swatch rule*: the colors are chosen from the palette of the kit in this order: `primary`, `secondary`, `accent`, then
  the other roles in the order of the palette, never `background` or `text`, each color once (ignoring case), at most
  three. A kit that has none of these has no swatches, which the view draws like no style.
- `Items`: the choices, in order: `(No style)`, then every kit of the catalog that is not overridden, in the order of
  the catalog (the places in the order of precedence, then by name), then the missing kit when there is one. The label
  of a kit is its name and the place it comes from in brackets: `project`, `my kits`, `shared`, or `built-in`. The tool
  tip of a kit is its description, when it has one, and the id, the place, and the folder. The label of a missing kit is
  the id and `(not found)`, and its tool tip says that the kit cannot be found and that another one, or no style, can be
  chosen.
- `SelectedItem`: the choice that is selected. It is the kit that the id in the project file names, the missing kit when
  the id names none that exists, and no style when the file names none.
- `IsVisible`: true while a project is open. `ToolTipText`: the tool tip of the selected choice. `ErrorMessage` and
  `HasError`: why the last choice could not be kept.
- `ManageKitsCommand` and `HasManageKits`: what the footer entry of the list does, or `null` for no entry. The picker
  does not know the windows of the application, so `MainWindowShell` sets the command to its Style Kits command; a
  change of the command announces `HasManageKits`.
- `ChangeTask`: the task of the last choice that was written; tests wait for it.

The unit keeps no copy of the choice. The list and the selection are rebuilt from `IActiveStyle` (`ActiveId`,
`IsMissing`) and the catalog (`Entries`) every time one of them, or the project service, announces a change.

#### Key Methods

**Rebuild**: Makes the list of choices and the choice that is selected. The collection is replaced only when the new
list differs from the shown one (records compare by value), so a control that shows it is not reset without need. A flag
marks the time of the rebuild, so that the change of selection that it causes is not taken for the choice of the user.
It also announces `IsVisible`, which depends on the project service.

**Choosing**: When the user selects another choice, the view model calls `IActiveStyle.SetActiveAsync` with its id
(`null` for no style), which writes the project file. Selecting the missing kit changes nothing, because it is what the
file says. When the call fails with an `InvalidOperationException` (no project is open, or the file cannot be written)
the reason is shown in `ErrorMessage` and written to the log and the list is rebuilt, so that it shows the kit that the
project really uses; a choice that works clears the message.

**ApplyDefaultToNewProjectAsync**: Calls the function that the composition gave it (the method of `StyleSelection` that
gives a new project the default kit of the user), or completes at once when there is none. The main window calls it
after it created a project.

**Threads**: The announcements may come from a worker (the catalog is refreshed on workers). The view model captures the
synchronization context of the thread that created it, and rebuilds on that thread.

**PlaceText**: The words for the places, shared with the units of the later steps that list kits.

**View**: `StylePickerView` shows the pill and, beside it, the error message (`StylePickerError`) in the danger color
when there is one. The view is hidden while `IsVisible` is false.

- *Pill*: a combo box (`StylePicker`, automation name `Style kit`) over `Items` with `SelectedItem` bound both ways,
  whose tool tip is `ToolTipText`. It takes its look from the control theme `StylePill`, which lives in the resources of
  the view and is not a shared theme: 26 pixels high, fully rounded, a raised surface with the input outline that turns
  to the accent under the pointer, the ring of the keyboard focus (2 pixels, just outside, as the shared control themes
  draw it), and a chevron. The selection box shows the swatches and `DisplayName` (the name is cut with an ellipsis
  after 160 pixels).
- *Swatches*: up to three overlapping 12 pixel dots, each outlined in the raised surface color so that they read as
  separate, filled with the colors of `Swatches` through a converter that turns the hex text into a brush; for a choice
  with `HasNoSwatches` one empty dashed dot is drawn instead. The dots are decoration: the name next to them carries the
  meaning.
- *List*: opens under the pill, aligned to its right edge, on the overlay surface with a dialog shadow and at least 260
  pixels wide. Each choice shows its swatches and its `Label` (the name with the place) and has the tool tip of the
  choice; the container theme `StylePillItem` is a 28 pixel row that is tinted with the accent under the pointer and
  filled with the accent tint when chosen, with a focus ring for the keyboard.
- *Footer entry*: while `HasManageKits` is true, a border below the list holds the button `Manage style kits...`
  (`StylePickerManageKits`, with the settings icon) that runs `ManageKitsCommand`. The view closes the list when the
  button is used, because the window that it opens needs the room; the button is found once the template is applied.

**AutomationIds**: `StylePicker` (the pill), `StylePickerError` and `StylePickerHost` (the container that the main
window puts the view in, in the title bar) are unchanged by the redesign; `StylePickerManageKits` is new. The visible
`Style:` label is gone: the name of the pill for assistive technology is `Style kit`.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing collaborator. A choice that cannot be kept is not an
exception to the caller: it is shown and logged. Failures to write a log entry are ignored. The list is rebuilt under a
lock, because outside a user interface two reads of the kits can announce a change on different threads at once; inside
the user interface the announcements are posted to its thread and never overlap.

#### Dependencies

- **IActiveStyle** (of `StyleSelection`) - the kit of the project, whether it is missing, the announcement, and the
  choice.
- **IStyleKitCatalog** (of `StyleKitCatalog`) - the kits that can be chosen and the announcement of a change.
- **IProjectService** (of the `Project` subsystem) - whether a project is open and when that changes.
- **CommunityToolkit.Mvvm** - property change notification.
- **Avalonia** - the view.
- **RollingFileLogger** - optional; records a choice that was refused.

#### Callers

- `ApplicationComposition` - builds the picker and gives it to the shell.
- `MainWindowShell` and `MainWindowView` - host it in the title bar, the shell sets its `ManageKitsCommand` to the Style
  Kits command, and the shell asks it to give a new project the default kit.

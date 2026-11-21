### StylePicker

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StylePicker` is the list in the status bar of the main window with which the user chooses the style kit of the open
project. The style belongs to the project, so the choice is one step away while the user works, and the list always
shows what the project file really says: the kit that it names, also when that kit cannot be found, and not a kit that
was clicked and refused. The unit is a view model (`StylePickerViewModel`) with a small value for each choice
(`StylePickerItem`) and a view (`StylePickerView`) that the main window shows through the view locator.

#### Data Model

- `StylePickerItem(Id, Label, ToolTip, IsMissing)`: one choice. `Id` is the id of the kit, or `null` for no style;
  `Label` is the text in the list; `ToolTip` explains the choice; `IsMissing` is true for the one choice that stands for
  a kit that the project names and that cannot be found.
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

**View**: `StylePickerView` shows the label `Style:`, a combo box (`StylePicker`) over `Items` with `SelectedItem` bound
both ways, and the error message (`StylePickerError`) in red when there is one. The view is hidden while `IsVisible` is
false.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing collaborator. A choice that cannot be kept is not an
exception to the caller: it is shown and logged. Failures to write a log entry are ignored.

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
- `MainWindowShell` and `MainWindowView` - host it in the status bar, and the shell asks it to give a new project the
  default kit.

### ExportPresetDialog

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`ExportPresetDialog` is the modal Export PNG window and its view model. It shows the ways that the shown picture can
be exported: the picture as it is shown, the sizes that the style kit of the project offers, and the sizes that the
application offers. It says what the file will be for the selected way, and reports the choice. It exports nothing:
`ImageExporter` draws and writes, and asks for the dialog through `IExportDialogs.PickExportPresetAsync`, which the main
window implements by showing this window.

#### Data Model

**ExportPresetItemViewModel**: one line of the list: the `Preset`, its `Name`, the `SizeLine` (for example
`1920 x 1080 px, on #FFFFFF`, `800 px wide, transparent`, or `As shown`), and the `Group` (`As shown`, `Style kit`, or
`Application`). The text of a line (`ToString`) is its `Name`, the first line it shows, because a screen reader names a
row of two lines by the text of its item, which would otherwise be the type name.

**Items**: the lines, in the order the exporter gave them (the picture as it is shown first, the sizes of the style kit,
then the sizes of the application).

**SelectedItem** / **SelectedPreset**: the selected line and its preset. The dialog starts with the line whose preset
has the same identifier as the choice of the last time, or with the first line when there is no such line.

**ResultText**: a sentence about the file for the selected line: the picture as it is shown with its own size and
background; a box (`1920 x 1080 px`, the picture fitted inside with its proportions kept); one side only (the other side
follows the picture); the natural size; and the background color or that the background stays transparent. Empty when
nothing is selected.

**Accepted**: true after the user chose Export.

**Commands**: `ExportCommand` (enabled while a line is selected) and `CancelCommand`. **CloseRequested**: event with
`ExportPresetCloseRequestedEventArgs.Accepted`, raised when the dialog asks its host to close.

**View**: `ExportPresetDialogView` is an Avalonia window, 520 by 440 with a minimum of 420 by 340, whose body is the
pane surface (`CpSurfacePane`): a section title ("Choose how the picture is exported."), a list of the lines (the name
in semibold and the size line in the hint style), the result sentence in the hint style, and a footer bar
(`CpSurfaceWindow` with a line above it) that holds the primary *Export...* button (the default) and the secondary
*Cancel* button (the cancel key). The list is named "Export sizes" for screen readers. Controls carry automation
identifiers (`ExportPresetList`, `ExportPresetResult`, `ExportPresetOkButton`, `ExportPresetCancelButton`), which the
restyle did not change. The view has a parameterless constructor for the XAML loader and a constructor that takes the
view model, and it closes itself when the view model asks.

#### Key Methods

**Export** (command): Sets `Accepted` and asks the host to close with acceptance. **Cancel** (command): asks the host to
close without acceptance. The main window returns `SelectedPreset` when the dialog was accepted and `null` otherwise.

**ExportPreset.DescribeSize**: the text of a size line: the box, one side, or the natural size, followed by
`transparent` or `on {color}` and the resolution when there are any; the picture as it is shown is `As shown`.

#### Error Handling

The constructors throw `ArgumentNullException` for a missing list of presets or preset. A list with no presets gives a
dialog with nothing selected, whose Export command cannot run. The dialog validates no sizes: a size that cannot be
drawn is reported by the exporter when it draws.

#### Dependencies

- **ImageExporter** — the caller, through `IExportDialogs`, which owns the interface and the `ExportPreset` value.
- Avalonia and CommunityToolkit.Mvvm — the window, the list, and the commands.

#### Callers

- `MainWindowView` (the main window of the `AppShell` subsystem) — implements `IExportDialogs.PickExportPresetAsync` by
  showing the dialog modally over the main window.

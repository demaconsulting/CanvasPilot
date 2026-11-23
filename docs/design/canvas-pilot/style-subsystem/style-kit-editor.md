### StyleKitEditor

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleKitEditor` is the editor of one style kit of the user or of the project. It holds a **draft**, which is a copy of
every part of the kit in a form that a window can edit, checks the draft with the rules of the style file, and writes
the draft as the kit only when the user saves. Nothing on disk changes before that, so canceling is always safe. The
unit is a view model (`StyleKitEditorViewModel`) with a small row type for each kind of list in the draft, and a view
that the view locator finds (`StyleKitEditorView`) with a control that edits one color (`ColorEditorView`).

#### Data Model

- **Fields of the draft**: `Name`, `Description`, the three font lists (`HeadingFont`, `BodyFont`, `MonospaceFont`), the
  three numbers (`StrokeWidth`, `CornerRadius`, `Spacing`, as `decimal` so that a numeric control can edit them),
  whether the kit has a Mermaid theme (`UseMermaid`) and the theme (`MermaidTheme`), the Vega-Lite configuration as text
  (`VegaLiteConfigText`, indented so that a person can read it), and `Notes`. `Id` is the id of the kit; it is the name
  of the folder of the kit and cannot be changed (duplicate the kit to give it another id).
- **Rows**: `PaletteRows` (`StyleColorRowViewModel`: role, color as text, whether the role is required, and the red,
  green, and blue channels as numbers that are read from and written to the text), `MermaidVariables`
  (`StyleNameValueRowViewModel`), `DoItems` and `DontItems` (`StyleTextRowViewModel`), `Presets`
  (`StylePresetRowViewModel`, whose numbers are kept as the text that the user typed so that a half-typed number is not
  lost), and `Examples` (`StyleExampleRowViewModel`: the name of the file in the `examples` folder, the caption, the
  full path of the file as it is now, and whether it is a file that the user chose and that is not in the kit yet).
- **Derived values**: `Errors` and `Warnings` (each a line made as `path: message`, the same text that the reader of the
  style file gives), `HasErrors`, `HasWarnings`, `CanSave` (true while there is no error), `IsDirty` (the draft differs
  from the kit that was opened), `ContrastText` (how readable the text is on the background, by the WCAG ratio, as a
  hint), `SaveError`, and `ExampleMessage` (why a chosen file was not added).
- **Result**: `Saved` and `SavedEntry` (the kit as the catalog found it after the save).

#### Key Methods

**Building the kit**: `BuildKit` makes the kit that the draft describes. Blank lines of the two lists of advice and
blank theme variables are left out, so a line that was added and not filled in is not an error. A number of an export
size that is not a number is left out of the size. The Vega-Lite text is compacted when it is JSON and passed on as it
is when it is not, so that the check says what is wrong. Two colors with one name, a color with no name, and a theme
variable that is named twice cannot be shown in a kit (a kit holds one value for a name), so the builder reports them as
problems of the draft.

**Checking**: after every change of the draft, the unit builds the kit and collects the problems of the draft and the
result of `StyleKitSerializer.Validate`, which is the same check that the reader of the files makes. A number of an
export size that is not a number is said once, where it is, and the message that the size has no side is left out for
that size, because it would only repeat the problem. Errors stop the save; warnings (for example a font that CanvasNet
does not bundle) do not. `IsDirty` compares the canonical text of the kit and the list of examples with those of the
draft when it was opened, so a value that is changed and changed back is not a change.

**Colors**: `AddColorCommand` adds a color that is `#808080` and has the next of the usual roles (`background`, `text`,
`primary`, `secondary`, `accent`, `surface`, `muted`) that the palette does not have, then `color-1`, `color-2`, and so
on. `RemoveColorCommand` removes a color unless its role is one that every kit needs. A color can be set by typing
`#RRGGBB` or by the red, green, and blue values of the row: the text is the truth, the values are read from it, and
writing a value writes the text; text that is not a color keeps what the user typed, reads as 0 for the values, and has
`HexError`.

**Examples**: `AddExampleFiles` adds files that the user chose. A file that is not there, or is larger than the limit of
an example (16 MiB), is not added and `ExampleMessage` says why. A file gets a name that no example has, made from its
own name with characters that a file name cannot have on any platform (the rule of the kit document) replaced and a
number before the extension when needed. A file that is added is only a row until the kit is saved. `AddExamplesCommand`
asks the function `PickExampleFiles`, which the window supplies, and adds what it returns; with no function it does
nothing.

**Save**: `SaveCommand` is usable while `CanSave`. It runs in this order. First the files that the user chose are copied
into the `examples` folder of the kit, under names that do not take the name of an example that stays or of a file that
is in the folder and is not an example that the user removed. Then the catalog writes the kit file, with the captions.
Then the example files that the user removed are deleted. If the copying or the writing fails, the files that this save
copied are taken away again, `SaveError` says why, and the editor stays open; a file that cannot be deleted at the end
is logged and left, which a later read of the folder shows as an example again. On success `Saved` and `SavedEntry` are
set and `CloseRequested` is raised. The kit is saved in the place that it was found in (the user or the project) and in
a folder named as its id. `CancelCommand` raises `CloseRequested` and writes nothing.

**View**: `StyleKitEditorView` is a window (860 by 700, at least 680 by 500, on the pane surface) with a tab for each
part of the kit (the tab headers are 14 pixel semibold, from the shared tab style): General (name, description, the id
and the place), Palette (a `ColorEditorView` for each color with a Remove button that is off for a required color, Add
color, and the contrast hint), Type and shape (the three font lists and numeric boxes for the three numbers), Mermaid
and Vega-Lite (a check box for the theme, the list of themes, the variable rows, and a multi-line box for the
configuration), Guidance (the notes and the two lists), Sizes (a row of boxes for each export size), and Examples (a row
with the name of the file and a caption box for each example, Add files, and the reason a file was not added). Every
field has a label in the label look, hints use the hint look, errors the error look, and warnings the caution look, all
of them from the shared text styles, so that no color is written in the window; every input has an automation name
(for example `Name`, `Description`, `Headings font`, `Line width`). Below the tabs is a footer bar with the errors, the
warnings, the reason a save failed, and the Save and Cancel buttons; Save has the primary look, Cancel the secondary
look, and Save follows `CanSave`. The Remove buttons of the rows are handled in one place in the code of the window,
which passes the row to the command that removes it. The window closes when `CloseRequested` is raised.
`ColorEditorView` is the control of one color: the name of the role (editable only for a color that is not required;
named `Color role`), a swatch painted with the text of the color and outlined with the control outline, the text box
(named after the format of the text), the reason when the text is not a color in the error look, and a button (named
for what it does) that opens a flyout with sliders for red, green, and blue, each with a name, which are bound to the
channels of the row so that they and the text always agree. It uses only the controls that Avalonia has, so the editor
needs no package for a color picker.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing catalog or entry and `InvalidOperationException` for a kit
that is read only (it comes with the program or is shared). A wrong value in the draft is a message in `Errors`, never
an exception. A failure to write is `SaveError`.

#### Dependencies

- **StyleKitDocument** unit - `StyleKitSerializer.Validate`, `Write`, and the limits of the format, and `StyleColors`.
- **StyleKitCatalog** unit (through `IStyleKitCatalog`) - writes the kit.
- **StylePicker** unit - the words for the places of kits (`PlaceText`).
- **Utilities** subsystem - `PathHelpers.SafePathCombine`, so that no file is written outside the folder of the kit.
- **Logging** subsystem - a file that could not be deleted.
- **CommunityToolkit.Mvvm**, **Avalonia** - notification, commands, and the view.

#### Callers

- `StyleKitManagerViewModel` - makes the editor for a kit that the user chose to edit, or has just created or
  duplicated, and shows it through the dialogs.
- `MainWindowView` - shows the editor window modally over the window that manages kits when the dialogs are asked to.

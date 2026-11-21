### StyleKitManager

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleKitManager` is the window of the user for style kits: it shows the kits that the catalog found, grouped by place,
with a preview of the kit that is chosen, and it carries out the actions that the user takes on a kit. It does not
change a kit by itself: it asks the catalog to make, copy, save, and delete kits of the user and of the project, the
archive to write and read zip files, the active style to record the kit that the project uses, and the editor to change
the parts of a kit. The unit is a view model (`StyleKitManagerViewModel`) with a view model for each group
(`StyleKitGroupViewModel`), for each kit (`StyleKitItemViewModel`, which also holds everything of the preview), and for
each small picture (`StyleThumbnailViewModel`), the cache of small pictures (`StyleThumbnailCache`), the interface
through which it asks for windows, pickers, and questions (`IStyleKitDialogs`), and a view that the view locator finds.
The main window implements `IStyleKitDialogs` through the dialog service, so the unit is tested with hand-written
dialogs and no window.

#### Data Model

- `Groups`: one `StyleKitGroupViewModel` for each place, in the order of precedence: the open project (absent while no
  project is open), the user (`My kits`), the shared folders, and the program (`Built-in`). A group has its items in the
  order of the catalog, `Notes` (for each shared folder that cannot be reached, `Not reachable: {path} ({reason})`; for
  each folder that holds a kit with errors, `Cannot be used: {folder} ({first error})`), and `EmptyText` that says what
  to do when the group has no kit.
- `StyleKitItemViewModel`: the entry as found, and for the list a strip of the first six colors, the dimmed card of a
  kit that another kit hides with the sentence that says what hides it, `IsInUse` (the open project uses it), and
  `IsSelected`. For the preview it holds the colors as `StyleSwatch` values (role, color, and a label color that reads
  on it), the font lines, the line of numbers, the Mermaid and Vega-Lite lines, the advice, the lines of the export
  sizes, the warnings of the reader, `GuidanceText` (the text that `StyleGuidance` gives an assistant) with
  `GuidanceSizeText` (`N of 4,000 characters`), the sentence that a kit of the program or of a team cannot be changed,
  and `Thumbnails`.
- `SelectedItem`, `HasSelection`, `IsSelectionReadOnly`, `HasProject`, `StatusMessage` (what the last command did or why
  it could not), `TargetIndex` and `TargetLocation` (where a new kit, a copy, and an imported kit go: the kits of the
  user or the project), and `ThumbnailTask` (which tests wait for).
- `StyleThumbnailCache`: the small pictures, kept by file, size, and time of the last write, at most 64.

#### Key Methods

**Following the catalog**: the view model builds the groups when it is made and again on every change that the catalog,
the project service, or the active style announces, on the thread that owns the user interface. The kit that was chosen
stays chosen when the same kit (the same place and folder) is in the new list.

**Choosing a kit**: choosing a kit marks it, makes the commands follow it, and starts drawing its example images. A
choice that is made while the pictures of another kit are drawn stops waiting for them; the drawing goes on in the cache
for the next time. Each example is drawn by the render service 240 pixels wide, on a worker thread, with the folder of
**its own kit** as the only folder that the file may read other files from, because the files of a shared kit or of a
zip file are not trusted. A picture that cannot be drawn shows `Could not be drawn: {reason}` where the picture would
be.

**Commands**: `UseInProjectCommand` is available while a project is open and the chosen kit is not hidden by another
one, and writes the id into the project file through the active style. `NewCommand` and `DuplicateCommand` ask for a
name through the dialogs, make the kit in the place that `TargetIndex` names, choose it, and open it in the editor;
`DuplicateCommand` is available for every kit, also one that is read only, because a copy is how such a kit is changed.
`EditCommand` and `DeleteCommand` are available only for a kit of the user or of the project; the editor gets the picker
of example files of the window, and a delete asks first. `ExportCommand` asks where to write and writes the zip file on
a worker thread. `ImportCommand` asks for a zip file and imports it on a worker thread into the place that `TargetIndex`
names; when the place has a kit with that id it asks whether to replace it, then whether to keep both, and stops when
the user says no to both. `CopyIntoProjectCommand` is available while a project is open for a kit that is not in the
project already. `RefreshCommand` reads the places again and `CloseCommand` asks the window to close. A problem that a
command meets (a place that cannot be written, an archive that is refused, no project) is said in `StatusMessage` and
logged, not thrown.

**Cache of pictures**: `GetAsync` returns the picture of an example, drawing it when it is not kept or the file has
changed; a file that two callers ask for at once is drawn once; a caller that stops waiting does not cancel the drawing;
a drawing that failed for a reason that may pass (it was cancelled, or the render service threw) is not kept; a file
that is not there is reported without a drawing; when the cache is full the picture that was asked for longest ago goes
first; disposing the cache cancels what is being drawn.

**View**: `StyleKitManagerView` is a window with the list on the left and the preview on the right, split by a bar that
can be dragged. The list is an `ItemsControl` of groups (`StyleKitGroups`); each group has its title, its empty text, a
card for each kit (`StyleKitCard`, a flat button with the name, the mark `in use`, the description, the strip of colors,
the place, and what hides the kit, tinted when it is chosen and dimmed when another kit hides it), and its notes. The
preview (`StyleKitPreview`) shows the name, the place, why the kit cannot be changed, the warnings, the colors as
swatches that carry their role and color in a readable label, the fonts and numbers, the advice, the sizes, the small
pictures of the examples with their captions or the reason that one could not be drawn, and the text for an assistant
with its length behind an expander. Along the bottom are the status line (`StyleKitStatus`), the buttons of the commands
(`StyleKitUseButton`, `StyleKitNewButton`, `StyleKitDuplicateButton`, `StyleKitEditButton`, `StyleKitDeleteButton`,
`StyleKitImportButton`, `StyleKitExportButton`, `StyleKitCopyIntoProjectButton`, `StyleKitRefreshButton`,
`StyleKitCloseButton`), and the list that chooses where new and imported kits go (`StyleKitTargetComboBox`). Choosing a
card is handled in the code of the window, which passes the card's kit to `SelectItemCommand`. The bitmaps of the
pictures are made from the rendered pictures by a converter of the window. The window closes when the view model asks,
and disposes the view model when it is closed. It is shown by the main window, which keeps one such window (see the
design of `MainWindowShell`).

#### Error Handling

The constructors throw `ArgumentNullException` for a missing argument. Everything that can go wrong in a command,
including a failure of the catalog, the archive, the active style, and the file system, is caught and said in
`StatusMessage`. A drawing that fails is a placeholder text, not an exception.

#### Dependencies

- **StyleKitCatalog** unit (through `IStyleKitCatalog`) - the kits, the places, and the changes to kits of the user and
  of the project.
- **StyleKitArchive** unit - zip export and import, the latter as untrusted input.
- **StyleSelection** unit (through `IActiveStyle`) - the kit that the project uses and the means to change it.
- **StyleKitEditor** unit - the editor that a kit is changed in.
- **StyleGuidance** unit - the text that an assistant would be given.
- **StylePicker** unit - the words for the places of kits.
- **Project** subsystem - whether a project is open and when that changes.
- **Preview** subsystem - `IImageRenderService` and `RenderedImage`, for the small pictures.
- **Logging** subsystem - the problems that a command met.
- **CommunityToolkit.Mvvm**, **Avalonia** - notification, commands, and the view.

#### Callers

- `MainWindowShell` - makes the view model when the user chooses Style Kits in the Tools menu and hands it to the
  dialogs, which show it in a window.

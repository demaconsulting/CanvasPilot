## Style

![Style Structure](StyleSubsystemView.svg)

### Overview

The `Style` subsystem holds the look that pictures should have. A **style kit** is a set of colors, fonts, numbers,
advice, export sizes, and example images that a person or a team keeps in a folder: a `style.json` file and an
`examples` folder. Kits come from four places: the kits that come with the program, the kits of the user, the kits in
the `styles` folder of the open project, and shared folders that the user lists; the most local copy of a kit wins. The
project says which kit it uses, the application exports pictures at the sizes of the kit, an assistant that is added
later is given the style as a block of text and two tools, and a picture can be checked against the palette of the kit.
None of it depends on any provider of a language model or on any agent library, so what the subsystem offers can be
given to any assistant that is added later.

The subsystem is built up unit by unit. It now contains ten units, `StyleKitDocument`, `StyleKitCatalog`,
`StyleKitArchive`, `StyleSelection`, `StyleGuidance`, `StyleChecker`, `StyleTools`, `StylePicker`,
`StyleSettingsPage`, and `StyleKitEditor`.
`StyleKitDocument` holds the values of a kit and the format that they are kept in: it reads a kit from a file and from
its folder with a message for each wrong property, checks a kit that is built in memory with the same rules, and writes
a kit as canonical text. `StyleKitCatalog` finds the kits in the four places, chooses the kit of the most local place
for an id, reads shared folders on workers with a time limit, keeps the kits of the program and the shared kits read
only, and creates, saves, duplicates, copies, and deletes the kits of the user and of the project. The kits of the
program are three folders of files, `clean`, `slides-dark`, and `dema`, that are copied beside the executable as loose
files (content files of the library project) and are in the published folder and the installer package; each has three
example files that use only the colors of the kit. `StyleKitArchive` writes a kit as a zip file and reads one from a zip
file as untrusted input: it refuses as a whole, before anything is written, an archive with a name that could write
outside the target, a link, a file that is not part of a kit, or too many or too large files, and it puts a valid kit in
place from a hidden folder, so a refused archive leaves nothing. `StyleSelection` finds the kit that the open project
uses from the id in the project file, keeps a new choice in the project file, starts a new project with the default kit
of the user, and offers the export sizes of the kit and of the application to the export dialog of `Preview`.
`StyleGuidance` writes the style of a kit as a block of text of at most 4,000 characters for an assistant and as single
whole sections, with the full paths of the example images. `StyleChecker` measures the colors of a rendered picture
against the palette of a kit, in a histogram and in CIELAB, and reports what is on the palette, what is a blend at an
edge, and what is off the palette. The units that show and edit kits in windows follow in later steps, and this document
grows with them. `StyleTools` describes the two tools of an assistant, `style_get` and `style_check`, as plain values
(the descriptor record of the drawing knowledge) with a function from text to text that never throws; the check reads
only files inside the folder of the project. `StylePicker` is the list in the status bar of the main window with which
the user chooses the kit of the open project. `StyleSettingsPage` is the page of the settings dialog with which the user
lists the shared folders of kits and chooses the default kit of a new project. `StyleKitEditor` is the editor of one kit
of the user or of the project: a draft of every part that is checked with the rules of the style file and is written,
with the example files that were added and removed, only when the user saves. The subsystem depends on CanvasNet for
the classification of font families, and on `Project`, `Settings`, `Preview`, `Utilities`, `Logging`, and `Knowledge`
(only the descriptor record of a tool).

### Interfaces

**StyleKitDocument**: The values of a kit and the file format.

- *Type*: In-process .NET static classes `StyleKitSerializer` and `StyleColors`, and the immutable values `StyleKit`,
  `StyleFonts`, `StyleMermaid`, `StyleGuidanceText`, `StyleSizePreset`, `StyleExample`, `StyleIssue`, and
  `StyleKitParseResult`, and the enumeration `StyleKitLocation`.
- *Role*: Provider.
- *Contract*: `Parse` reads the text of a style file, `ReadFolder` reads a kit folder, `Validate` checks a kit that is
  built in memory, and `Write` writes a kit as the text of a style file. Reading never throws for a bad file: it returns
  every error and warning, each with the path of the property in the words of the file, and returns no kit when there is
  an error.
- *Constraints*: Stateless and safe for concurrent use. A kit that has an error is not written by the units that save
  kits.

**StyleKitCatalog**: The kits in the places that hold them.

- *Type*: In-process .NET interface `IStyleKitCatalog` and sealed class `StyleKitCatalog`, with the immutable values
  `StyleKitEntry`, `StyleLocationStatus`, and `StyleBrokenKit`.
- *Role*: Provider (the entries, the places, and the changes to kits), and Consumer (the project service for the open
  project, the settings service for the shared folders).
- *Contract*: `Entries` lists every kit with its place, folder, read-only flag, and whether it is overridden;
  `Locations` tells whether each place could be read and which folders are broken; `Resolve` finds the kit of an id by
  the order of precedence (project, user, shared folders in order, program); `RefreshAsync` reads the places, each
  shared folder with a time limit; `Save`, `CreateBlank`, `Duplicate`, `CopyIntoProject`, and `Delete` change the kits
  of the user and of the project; `Changed` is raised after any of these changes the entries.
- *Constraints*: A shared folder that is offline never blocks the caller or the other places. The kits of the program
  and shared kits are never changed. Safe for concurrent use; `Changed` may be raised on any thread.

**StyleKitArchive**: A kit as a zip file.

- *Type*: In-process .NET static class `StyleKitArchive`, the immutable value `StyleImportResult`, and the enumeration
  `StyleImportConflict`.
- *Role*: Provider.
- *Contract*: `Export` writes a kit and its example files as a zip file; `Import` reads a zip file into a folder of kits
  and returns the kit, or the reason it was refused, as `Refuse`, `Replace`, or `KeepBoth` says when the id is taken.
- *Constraints*: Stateless. `Import` never throws for a bad archive, writes only inside the target folder, and leaves
  nothing in it when it refuses.

**StyleSelection**: The kit that the open project uses, and the export sizes.

- *Type*: In-process .NET interface `IActiveStyle` and sealed class `StyleSelection`, which also implements
  `IExportPresetSource` of the `Preview` subsystem.
- *Role*: Provider (the active kit, the choice, the export sizes), and Consumer (the project service, the catalog, the
  settings service).
- *Contract*: `Active` is the kit that the id in the project file resolves to, `ActiveId` is that id, and `IsMissing` is
  true when the id names a kit that cannot be found; `SetActiveAsync` writes the choice into the project file;
  `ApplyDefaultToNewProjectAsync` gives a project with no style the default kit of the user; `GetPresets` gives the
  sizes of the kit and then the sizes of the application that the kit does not rename; `Changed` is raised when any
  answer may have changed.
- *Constraints*: Keeps no copy of the choice, so an answer is never stale. Safe for concurrent use; `Changed` may be
  raised on any thread.

**StyleGuidance**: The style of a kit as text for an assistant.

- *Type*: In-process .NET static class `StyleGuidance`.
- *Role*: Provider.
- *Contract*: `Build` writes the block of a kit, of at most `BudgetCharacters` (4,000) characters, in a fixed order, and
  ends it with a line that says so when parts were left out; `BuildSection` writes one section whole.
- *Constraints*: Stateless and thread-safe. Never fails for a kit that the format allows. The text of a kit is written
  so that it cannot change the structure of the block, and the block says that it is data and not instructions.

**StyleChecker**: A picture measured against a palette.

- *Type*: In-process .NET static class `StyleChecker`, the immutable values `StyleCheckOptions`, `StyleCheckReport`,
  `StylePaletteUse`, and `StyleOffPaletteColor`.
- *Role*: Provider; consumer of the pixels of a rendered picture of `Preview`.
- *Contract*: `Check` takes a rendered picture (or its pixels with a size), a kit, and the limits, and returns the
  report: the share of each palette color, of blended edges, and of colors off the palette (the largest listed with
  their nearest palette color), and whether the picture passes; `ToNotes` and `ToText` write the report for the list of
  notes of a picture and for a reader of text.
- *Constraints*: Stateless and thread-safe. The same pixels, in any order, give the same report. A picture of the
  largest size is checked in well under a second.

**CheckAgainstStyle**: The command of the Image menu.

- *Type*: In-process .NET interface `ICheckAgainstStyle` and sealed class `StyleCheckCommand`.
- *Role*: Provider (the command, to the main window), and Consumer (the shown picture and the notes of the preview of
  `Preview`, and the active style).
- *Contract*: `CanCheck` says whether a picture is shown and `CanCheckChanged` follows it; `RunAsync` checks the shown
  picture against the kit of the project on a worker thread and puts the result among the notes of the picture, and
  answers `null`, or a sentence that says why nothing was checked.
- *Constraints*: Changes nothing but the notes. The result is applied on the context of the caller, and a result about a
  picture that was replaced is dropped.

**StyleTools**: The tools of an assistant.

- *Type*: In-process .NET static class `StyleTools`; the tools are `KnowledgeTool` records of the `Knowledge` subsystem.
- *Role*: Provider; consumer of the active style, the catalog, the render service, and the guidance and the checker.
- *Contract*: `CreateAll` gives `style_get` (the guidance of the kit of the project, of a named kit, or one section) and
  `style_check` (a picture file of the project checked against the palette of the kit); each has a name, a description,
  a JSON Schema, and a function from the arguments as text to an answer as text.
- *Constraints*: A function never throws; a mistake is answered with a message that says what to send. `style_check`
  reads only a file inside the project folder (not in `.canvaspilot`, and not through a link that leads out) and waits
  for the drawing on a thread of the pool, so it must not be called from the thread of the user interface.

**StylePicker**: The choice of the kit in the status bar.

- *Type*: In-process .NET view model `StylePickerViewModel` with the value `StylePickerItem`, and the view
  `StylePickerView`.
- *Role*: Provider (the choices and the selection that the window shows), and Consumer (the active style, the catalog,
  and the project service).
- *Contract*: `Items` is no style, then each kit that is not hidden by a kit of a more local place, then the kit that
  the project names when it cannot be found; `SelectedItem` is the kit that the project uses, and setting it writes the
  choice through `IActiveStyle.SetActiveAsync`; `IsVisible` is true while a project is open; `ErrorMessage` says why a
  choice was refused; `ApplyDefaultToNewProjectAsync` passes on the request that the shell makes after it created a
  project.
- *Constraints*: Keeps no choice of its own: the list is rebuilt from the active style and the catalog on the thread
  that owns the user interface whenever either announces a change.

**StyleSettingsPage**: The settings page for shared folders and the default kit.

- *Type*: In-process .NET view model `StyleSettingsPageViewModel`, which is an `ISettingsPage` of the `Settings`
  subsystem, with the values `StyleFolderItem` and `StyleKitChoice`, and the view `StyleSettingsPageView`.
- *Role*: Provider (a page of the settings dialog), and Consumer (the catalog of kits, and the folder picker of the
  window through a function).
- *Contract*: `Title` is `Style Kits`; `Folders` lists the shared folders in order with the status of each;
  `AddTypedFolderCommand`, `BrowseFolderCommand`, `RemoveFolderCommand`, `MoveFolderUpCommand`, and
  `MoveFolderDownCommand` edit the list; `DefaultKitChoices` and `SelectedDefaultKit` choose the default kit; `Load`
  takes the style settings, and `Apply` returns the settings with only `Styles` replaced; `IsValid` is false while a
  folder is not a full path.
- *Constraints*: Edits a working copy. A folder that cannot be reached is kept, and a default kit that no kit has is
  kept.

**StyleKitEditor**: The editor of one kit of the user or of the project.

- *Type*: In-process .NET view model `StyleKitEditorViewModel` with the row values `StyleColorRowViewModel`,
  `StyleNameValueRowViewModel`, `StyleTextRowViewModel`, `StylePresetRowViewModel`, and
  `StyleExampleRowViewModel`.
- *Role*: Provider (the draft and its commands), and Consumer (the catalog that writes the kit).
- *Contract*: The draft has a field or a list of rows for every part of the kit; `Errors` and `Warnings` are the
  messages of the reader of the style file; `CanSave` is true while there is no error; `BuildKit` makes the kit that
  the draft describes; `AddExampleFiles` and `AddExamplesCommand` add example images; `SaveCommand` copies the chosen
  example files in, writes the kit through the catalog, and deletes the example files that were removed;
  `CancelCommand` writes nothing; `CloseRequested` asks the window to close; `Saved` and `SavedEntry` give the
  result.
- *Constraints*: A kit that comes with the program or is shared is refused (it is read only). Nothing on disk changes
  before the save, and a failed save takes away the files that it copied.

### Design

**The dependency rule.** The subsystems below `AppShell` do not depend on `AppShell` or on each other upwards, and this
subsystem follows it. `Preview`, `Project`, `Settings`, and `Knowledge` never depend on `Style`: the seams that they
need (a source of export sizes, a sink for notes of a render) are declared in `Preview`, the id of the kit of a project
is a plain text in the project file, and the style settings are a plain record in the settings. `Style` depends on those
subsystems, and `AppShell`, which builds every unit in one place, is the only place that knows all of them.

**No provider.** The subsystem refers to no package of a language model, of an agent library, or of an AI extension
library, and adds no package at all. Its guidance and tools are plain values and text. The test
`StyleSubsystem_Assembly_ReferencesNoLanguageModelPackage` checks the referenced assemblies, the packages that the test
project resolves, and the package references of the project file, the same way that the `Knowledge` subsystem does.

**A kit is a folder.** A kit is the folder `{id}` with the file `style.json` and the folder `examples`, so that a kit
can be copied, zipped, committed, and shared as a folder and read by a person. The id is a lower-case kebab text of 1 to
48 characters and names the folder of a kit that the application writes.

**Written by hand, so read by hand.** The file is read property by property over a JSON document and not deserialized,
so that each message can name the property that is wrong in the words a person uses to edit the file, and so that every
error is reported at once. Writing is canonical, so a kit that is read and written again is the same text.

**Everything is bounded.** A kit may come from a shared folder or a zip file, so its size and shape are limited before
anything is made from it. Details are in the design of the `StyleKitDocument` unit.

### Dependencies

- **StyleKitDocument** unit: the values of a kit and the format of its file.
- **StyleKitCatalog** unit: the kits in the places, the choice by precedence, and the changes to the kits of the user
  and of the project.
- **StyleKitArchive** unit: a kit as a zip file, exported and imported as untrusted input.
- **StyleSelection** unit: the kit of the open project, the choice, and the export sizes.
- **StyleGuidance** unit: the style of a kit as text for an assistant.
- **StyleChecker** unit: a picture measured against the palette of a kit.
- **StyleTools** unit: the tools `style_get` and `style_check` of an assistant.
- **StylePicker** unit: the choice of the kit of the open project in the status bar.
- **StyleSettingsPage** unit: the shared folders and the default kit in the settings dialog.
- **StyleKitEditor** unit: the editing of one kit of the user or of the project.
- **Knowledge** subsystem: only the descriptor record of a tool (`KnowledgeTool`).
- **Project** subsystem: which project is open and when it changes, and the id of the kit that the project file keeps
  (the project service).
- **Preview** subsystem: the export size and the source of export sizes that `StyleSelection` implements.
- **Settings** subsystem: the shared kit folders and when they change (the settings service).
- **Utilities** subsystem: the safe combination of folder and file names.
- **Logging** subsystem: the log of kits that cannot be used and folders that cannot be read.
- **CanvasNet** (OTS): the classification of font families, used to warn about a font that not every format draws.

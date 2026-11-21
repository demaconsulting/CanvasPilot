## Style

![Style Structure](StyleSubsystemView.svg)

### Overview

The `Style` subsystem holds the look that pictures should have. A **style kit** is a set of colors, fonts, numbers, advice,
export sizes, and example images that a person or a team keeps in a folder: a `style.json` file and an `examples` folder.
Kits come from four places: the kits that come with the program, the kits of the user, the kits in the `styles` folder of
the open project, and shared folders that the user lists; the most local copy of a kit wins. The project says which kit it
uses, the application exports pictures at the sizes of the kit, an assistant that is added later is given the style as a
block of text and two tools, and a picture can be checked against the palette of the kit. None of it depends on any
provider of a language model or on any agent library, so what the subsystem offers can be given to any assistant that is
added later.

The subsystem is built up unit by unit. It now contains two units, `StyleKitDocument` and `StyleKitCatalog`.
`StyleKitDocument` holds the values of a kit and the format that they are kept in: it reads a kit from a file and from its
folder with a message for each wrong property, checks a kit that is built in memory with the same rules, and writes a kit
as canonical text. `StyleKitCatalog` finds the kits in the four places, chooses the kit of the most local place for an id,
reads shared folders on workers with a time limit, keeps the kits of the program and the shared kits read only, and
creates, saves, duplicates, copies, and deletes the kits of the user and of the project. The units that move kits as zip
files, choose the kit of a project, write the guidance for an assistant, check a picture, describe the tools of an
assistant, and show and edit kits in windows follow in later steps, and this document grows with them. The subsystem
depends on CanvasNet for the classification of font families, and on `Project`, `Settings`, `Utilities`, and `Logging` for
the catalog of kits, and later on `Preview` and `Knowledge` (only the descriptor record of a tool).

### Interfaces

**StyleKitDocument**: The values of a kit and the file format.

- *Type*: In-process .NET static classes `StyleKitSerializer` and `StyleColors`, and the immutable values `StyleKit`,
  `StyleFonts`, `StyleMermaid`, `StyleGuidanceText`, `StyleSizePreset`, `StyleExample`, `StyleIssue`, and
  `StyleKitParseResult`, and the enumeration `StyleKitLocation`.
- *Role*: Provider.
- *Contract*: `Parse` reads the text of a style file, `ReadFolder` reads a kit folder, `Validate` checks a kit that is
  built in memory, and `Write` writes a kit as the text of a style file. Reading never throws for a bad file: it returns
  every error and warning, each with the path of the property in the words of the file, and returns no kit when there is an
  error.
- *Constraints*: Stateless and safe for concurrent use. A kit that has an error is not written by the units that save kits.

**StyleKitCatalog**: The kits in the places that hold them.

- *Type*: In-process .NET interface `IStyleKitCatalog` and sealed class `StyleKitCatalog`, with the immutable values
  `StyleKitEntry`, `StyleLocationStatus`, and `StyleBrokenKit`.
- *Role*: Provider (the entries, the places, and the changes to kits), and Consumer (the project service for the open
  project, the settings service for the shared folders).
- *Contract*: `Entries` lists every kit with its place, folder, read-only flag, and whether it is overridden; `Locations`
  tells whether each place could be read and which folders are broken; `Resolve` finds the kit of an id by the order of
  precedence (project, user, shared folders in order, program); `RefreshAsync` reads the places, each shared folder with a
  time limit; `Save`, `CreateBlank`, `Duplicate`, `CopyIntoProject`, and `Delete` change the kits of the user and of the
  project; `Changed` is raised after any of these changes the entries.
- *Constraints*: A shared folder that is offline never blocks the caller or the other places. The kits of the program and
  shared kits are never changed. Safe for concurrent use; `Changed` may be raised on any thread.

### Design

**The dependency rule.** The subsystems below `AppShell` do not depend on `AppShell` or on each other upwards, and this
subsystem follows it. `Preview`, `Project`, `Settings`, and `Knowledge` never depend on `Style`: the seams that they need
(a source of export sizes, a sink for notes of a render) are declared in `Preview`, the id of the kit of a project is a
plain text in the project file, and the style settings are a plain record in the settings. `Style` depends on those
subsystems, and `AppShell`, which builds every unit in one place, is the only place that knows all of them.

**No provider.** The subsystem refers to no package of a language model, of an agent library, or of an AI extension
library, and adds no package at all. Its guidance and tools are plain values and text. The test
`StyleSubsystem_Assembly_ReferencesNoLanguageModelPackage` checks the referenced assemblies, the packages that the test
project resolves, and the package references of the project file, the same way that the `Knowledge` subsystem does.

**A kit is a folder.** A kit is the folder `{id}` with the file `style.json` and the folder `examples`, so that a kit can be
copied, zipped, committed, and shared as a folder and read by a person. The id is a lower-case kebab text of 1 to 48
characters and names the folder of a kit that the application writes.

**Written by hand, so read by hand.** The file is read property by property over a JSON document and not deserialized, so
that each message can name the property that is wrong in the words a person uses to edit the file, and so that every error
is reported at once. Writing is canonical, so a kit that is read and written again is the same text.

**Everything is bounded.** A kit may come from a shared folder or a zip file, so its size and shape are limited before
anything is made from it. Details are in the design of the `StyleKitDocument` unit.

### Dependencies

- **StyleKitDocument** unit: the values of a kit and the format of its file.
- **StyleKitCatalog** unit: the kits in the places, the choice by precedence, and the changes to the kits of the user and
  of the project.
- **Project** subsystem: which project is open and when it changes (the project service).
- **Settings** subsystem: the shared kit folders and when they change (the settings service).
- **Utilities** subsystem: the safe combination of folder and file names.
- **Logging** subsystem: the log of kits that cannot be used and folders that cannot be read.
- **CanvasNet** (OTS): the classification of font families, used to warn about a font that not every format draws.

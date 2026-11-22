### StyleTools

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleTools` describes the two tools that give an assistant the style of the project: `style_get`, which gives the style
of a kit as text, and `style_check`, which checks a picture file of the project against the palette of the kit of the
project. A tool is a plain value, the descriptor record `KnowledgeTool` that the `Knowledge` subsystem uses for the
drawing knowledge (name, description, JSON Schema text, and a function from the arguments as text to an answer as text),
so one adapter later can give both kinds of tool to any provider of a language model, and the unit depends on no
provider. The record is not renamed here; a neutral name is a change for the increment that adds the assistant. The unit
has no state and no user interface.

#### Data Model

- `GetToolName` is `style_get` and `CheckToolName` is `style_check`.
- `style_get` takes the optional strings `kit` (the id of a kit) and `section` (one of `palette`, `fonts`, `mermaid`,
  `vegaLite`, `notes`, `presets`, `examples`); the schema is an object that allows no other property.
- `style_check` takes the optional string `path` (relative to the project folder, or a full path inside it) and the
  optional number `tolerance` (0 to 100, default 10); the schema is an object that allows no other property.
- The unit is given its collaborators as values and functions: the active style (`IActiveStyle`), the catalog of kits, a
  function that gives the folder of the open project, a function that gives the picture that is shown, and the render
  service (`IImageRenderService`). It holds nothing else.

#### Key Methods

**CreateGetTool / CreateCheckTool / CreateAll**: Make the descriptors. A missing collaborator is an
`ArgumentNullException`; everything after that is answered, not thrown.

**style_get**: With no arguments, in any of the ways that mean none (nothing, blank, `null`, `{}`, null or empty
values), it answers with `StyleGuidance.Build` of the active kit. With `kit` it finds that kit through the catalog (the
kit of the most local place); an id that no kit has is answered with
`No style kit has the id '{id}'. Known kits: {ids}.`, and the ids are the kits that are not overridden, in order. With
no kit chosen, or with a kit that is missing, it says so and how to choose one (the Style Kits window, the Style list,
or `style_get` with `kit`). With `section` it answers with `StyleGuidance.BuildSection`, the whole section, with no cut;
the name is not case sensitive; a name that is not a section is answered with the list of the sections.

**style_check**: The steps, each ending the call with a message when it fails:

1. Read the arguments: not JSON, not an object, `path` that is not a string, and a `tolerance` that is not a number from
   0 to 100 are refused with a message that says what to send.
2. There must be an open project (the folder function), an active kit, and a path (given, or the picture that is shown).
3. **Contain the path.** The path is joined to the project folder with `PathHelpers.SafePathCombine`, which refuses a
   relative path that climbs out, a full path elsewhere, and a network path (the unit has no allowance for them), and a
   character that a path cannot have. The first part of the path below the project folder must not be `.canvaspilot`
   (the data folder that the application keeps to itself; the comparison ignores case). The path must not be a folder
   and must be a file that exists. Then the file and each folder from it up to the project folder are looked at: one
   that is a link or a junction is followed with `ResolveLinkTarget` to the place it finally leads to, which must be
   inside the project folder (or inside the real place of the project folder, when that is reached through a link); a
   link that cannot be followed is refused.
4. **Draw.** The file is drawn with `IImageRenderService.RenderAsync` inside `Task.Run`, and the result is waited for
   with `GetAwaiter().GetResult()`. This makes the call synchronous for the assistant, whose thread it runs on, and the
   work on a thread of the pool means that the wait cannot be blocked by a context that belongs to the caller. It must
   never be called from the thread of the user interface. The render service lets the file read other files only from
   inside the project folder. A failed drawing is answered with its code, message, and hint; a cancelled drawing is
   answered as such.
5. **Check.** `StyleChecker.Check` with the picture, the active kit, and the tolerance, and the answer is
   `StyleCheckReport.ToText` with the name of the file.

**Never throws.** Every call is wrapped so that an exception other than running out of memory is answered with
`Error: the tool could not answer ({type}).`; every other mistake of the caller is answered with a message that starts
with `Error:` (for `style_check`) or says what is missing (for `style_get`). A long argument is cut to 80 characters
when an answer repeats it.

#### Error Handling

The factory methods throw `ArgumentNullException` for a missing collaborator. The functions of the tools throw nothing.

#### Dependencies

- **StyleGuidance** unit — the block and the sections that `style_get` gives.
- **StyleChecker** unit — the check and the text of its report.
- **StyleSelection** unit (through `IActiveStyle`) — the kit of the open project.
- **StyleKitCatalog** unit (through `IStyleKitCatalog`) — the kit of an id and the list of known kits.
- **KnowledgeTool** of the `Knowledge` subsystem — the descriptor record only; the unit uses nothing else of that
  subsystem.
- **ImageRenderService** (through `IImageRenderService`) — draws the file.
- **PathHelpers** of the `Utilities` subsystem — the containment of the path.
- **ProjectDocument** of the `Project` subsystem — the name of the data folder.

#### Callers

- The `AgentToolRegistry` of the `Agent` subsystem, which gives the tools to the copilot, and the composition of the
  application, which gives the functions for the folder of the project and the picture that is shown.

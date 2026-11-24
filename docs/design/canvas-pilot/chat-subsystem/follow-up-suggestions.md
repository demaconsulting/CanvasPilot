### FollowUpSuggestions

#### Purpose

`FollowUpSuggestions` decides which prompts the chat panel offers as buttons for what the user may want to say next,
from what the project has. A project with no drawing is offered ways to get a first one, a project with a drawing is
offered ways to refine it, and nothing is offered while the copilot works on a message. The unit exists so that this
rule is a plain function of three facts that is verified without a window, a copilot, or a project folder, and so that
the panel only has to ask it again whenever one of the facts changes. It has two parts: `FollowUpSuggestions`, the
planner, and `ProjectDrawingProbe`, the small search that tells the panel whether the folder of the open project holds a
drawing.

#### Data Model

- `FollowUpSuggestions`: `public static class` with no state.
  - `StarterPrompts`: the three prompts that a project without a drawing is offered: "Draw a flowchart of our release
    process", "Make a title card for a launch announcement", and "Chart something from a small table of numbers".
  - `RefinementPrompts`: the three prompts that a project with a drawing is offered: "Make the title larger", "Add a
    legend", and "Use my style kit".
  - `ExportPrompt`: the constant "Export as PNG", which is offered after the refinements and only while a picture is
    shown.
  - `Plan(hasDrawing, hasPictureShown, busy)`: the planner, described below.
  - The prompts are plain sentences that the panel writes into the message for the user to change and send; they are not
    commands. Both lists are read-only and are never handed out for change: `Plan` returns a new list each time.
- `ProjectDrawingProbe`: `internal static class` with no state.
  - `HasDrawing(projectFolder, filter)`: tells whether the folder holds a drawing, which is a file that the
    `IImageSourceFilter` that it is given accepts (the panel gives it the `CanvasNetImageSourceFilter` that the project
    explorer uses, so a drawing is a file that CanvasNet can draw, with the compound extensions matched as a whole).
  - `MaximumDepth` (4): how many folders below the project folder the search goes.
  - `SkippedTopFolders` (`attachments`, `styles`): the folders at the top of a project that hold the files of the
    project (the copies of attached files and the style kits) and not its drawings.

#### Key Methods

**Planning.** `Plan` is a pure function of three facts and gives:

1. an empty list when `busy` is true, whatever the other two facts are, because nothing is offered while the copilot
   works on a message;
2. otherwise, when `hasDrawing` is false, a copy of `StarterPrompts`;
3. otherwise a list that starts with the three `RefinementPrompts` and, only when `hasPictureShown` is true, ends with
   `ExportPrompt`.

The export is offered only with a picture because the export acts on the picture that is shown; a project that has a
drawing in its folder but shows none is not offered it. `hasPictureShown` without `hasDrawing` changes nothing in the
plan (the panel never gives that combination, because a picture that is shown is a drawing). The planner knows nothing
of the window, the files, or the copilot: the panel finds out the facts and asks again whenever one changes.

**Searching the project for a drawing.** `HasDrawing` is a depth-first search that stops at the first drawing. For a
folder it looks first at the files of the folder, with `Directory.EnumerateFiles`, which lists them lazily so that the
search stops at the first file that the filter accepts, and then, while the folder is less than `MaximumDepth` folders
below the project folder, into its sub-folders. A sub-folder is left out when its name starts with a dot (the hidden
data folder `.canvaspilot` and every other hidden folder) and, at the top of the project only, when it is `attachments`
or `styles` (compared without regard to case), because a picture that was attached to a message, or a file of a style
kit, is not a drawing that the copilot made and could change. The search never reads a file: it only lists names. It is
bounded in three ways so that it is quick, also for a large folder: the depth, the stop at the first drawing, and the
lists being lazy. It runs on the thread that calls it; the panel calls it on the user interface thread, only when
nothing cheaper answers the question (a picture is shown, or the copilot wrote a drawing in this session, both mean that
the project has a drawing) and never while the copilot works, because nothing is offered then. A panel that was made
without a filter does not search the folder at all; for it a project has a drawing only when a picture is shown or the
copilot wrote one (a written file is judged by a default `CanvasNetImageSourceFilter`).

**How the panel uses the unit.** `ChatPanelViewModel` asks for a new plan in four situations: when it is made, when the
project changes (after the transcript was emptied and the saved conversation was loaded, and with the flag of the
drawing that the copilot wrote cleared, because that drawing belongs to the project it was written in), when the
annotation layer reports that a picture appeared or went away, and when a turn starts or ends. For a turn that has ended
the file that the copilot wrote is the new fact: the panel notes the files that the copilot reports as written
(`FileWritten` of `IAgentService`, which can come from any thread, so only a flag is set) that the filter accepts, and
plans after the turn. The panel offers the plan in the empty conversation as the examples and above the message once a
conversation exists (see the design of the `ChatPanel`); it does not offer a plan that equals the one it already offers.

#### Error Handling

Nothing here throws for what a project or a disk holds. `Plan` has no failure: every combination of its three facts has
an answer. `HasDrawing` handles `IOException` (which includes a folder that is missing or was removed while the search
ran) and `UnauthorizedAccessException` by treating the folder that could not be read as holding no drawing; the search
goes on with the folders that it can read, and a project folder that is missing gives `false`. A drawing that is only
reachable through an unreadable folder is therefore not found, which costs nothing worse than an offer of ways to start
instead of ways to refine. Programming errors are not guarded: a `null` folder or filter is the caller's mistake and
throws the exception of the framework, and the panel gives the probe only a non-empty folder and a filter that it was
given. Nothing is logged: the unit has no logger and the outcome is a list of prompts.

#### Dependencies

- **IImageSourceFilter** (the interface of `ProjectService`, implemented by `CanvasNetImageSourceFilter`): which files
  are drawings. The probe does not know CanvasNet; the panel chooses the filter.
- **System.IO** (part of the .NET runtime): the lists of the files and folders of the project.

The planner has no dependency at all, and the unit depends on no other subsystem than `Project` for the one interface.

#### Callers

- `ChatPanelViewModel` calls `Plan` (and, through its own `RefreshSuggestions`, `ProjectDrawingProbe.HasDrawing`) and
  binds the empty conversation and the prompts above the message to the list that it keeps (`Suggestions`).
- The verification of the unit calls `Plan` and the panel through its public members; `ProjectDrawingProbe` is internal
  and is reached through the panel.

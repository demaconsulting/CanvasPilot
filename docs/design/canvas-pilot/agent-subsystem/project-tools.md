### ProjectTools

![Agent Tools](AgentToolsView.svg)

#### Purpose

`ProjectTools` gives the AI copilot its hands on the project: it lists, reads, and writes the files of the project and
draws pictures of them. It is two tool packs of the AI library, `project` (`project_list`, `project_read`,
`project_write`) and `render` (`render_image`), and the class that makes them for `AgentToolRegistry`. Every tool is
confined to the project folder by the path policy that the registry builds, and the tools refuse what that policy does
not see: links, and names that mean something else than the file they seem to name. The unit also holds its supporting
types: `ProjectFilesToolPack` and `RenderImageToolPack` (the packs), `ProjectPathGuard` (the checks of a path),
`ProjectFileKinds` (which files are text and which are pictures), `ImageScaler` (the shrinking of a picture),
`ToolCallLog` (the log of a call), and `FileWrittenEventArgs` (the event).

#### Decisions

**Overwriting is direct: no backup and no prompt.** This is a decision of the user, and it is final for this version.
Users keep their drawings in source control or copy the project before a session, so `project_write` creates or replaces
a file at once, the application keeps no copy of the old content, and nothing is asked. The application therefore writes
nothing into the project besides the file the model was asked to write: no backup folder, no history, no temporary file
after the call. What replaces the safety net is visibility: the answer of the tool says `created` or `replaced` with the
sizes (and the chat shows that text), the event `FileWritten` lets the shell select the file in the project explorer,
and the role prompt of the copilot (a later step) tells it to read a file before it replaces it and to keep what the
user did not ask it to change. The user guide says plainly that CanvasPilot keeps no copies and does not ask before the
copilot replaces a file.

**The tools refuse links themselves.** The path policy of the AI library judges a path by its spelling and does not
follow links (pinned by
`AgentToolRegistry_CreatePolicy_FolderLinkInsideTheProject_IsPermittedAndMustBeRefusedByTheTools`), so a junction or
symbolic link inside the project that leads outside it is permitted by the policy. `ProjectPathGuard` therefore looks at
every existing part of the path from the project folder down, and refuses the path when a part is a link. The project
folder itself is the user's choice and is not judged.

**Which reparse points are links.** A *folder* that is a reparse point of any kind is refused (that is how junctions and
symbolic links to folders look, and a folder is where a path can be redirected). A *file* is refused when .NET reports a
link target for it (a symbolic link). Other reparse points of files (the placeholders of a cloud drive such as OneDrive,
for example) do not redirect a path and are allowed, so that a project in a synchronized folder keeps working. A reparse
point whose target cannot be read is treated as a link.

#### Data Model

**ProjectTools**: `sealed class`, public. The constructor takes the `IImageRenderService` (required), the `IActiveStyle`
(optional; without it no style kit example is offered), an `IImageSourceFilter` (the filter of the application, which
asks CanvasNet, by default), and a `RollingFileLogger` (optional). `CreatePacks(AgentToolContext)` returns the two packs
and is the supplier of project packs that `AgentToolRegistry` takes in its constructor
(`new AgentToolRegistry(knowledge, style, projectTools.CreatePacks)`); the packs hold no project, because the registry
gives each pack the policy of the project when it builds the tools. The event `FileWritten`
(`EventHandler<FileWrittenEventArgs>`) is raised after a file was created or replaced, on the thread that ran the tool.
The composition of the application (a later step) passes the object to the registry and handles the event on the thread
of the user interface.

**FileWrittenEventArgs**: `sealed class`. `RelativePath` (the path below the project folder with forward slashes) and
`Created` (`true` when the file is new). It never carries content.

**ProjectFilesToolPack**: `internal sealed class`, `IToolPack` with the prefix `project`. The three tools.

**RenderImageToolPack**: `internal sealed class`, `IToolPack` with the prefix `render`. The one tool. It needs no
ability of the host: a model that cannot see still gets the size, the format, and the notes of the renderer.

**ProjectPathGuard**: `internal static class`. `TryResolve(policy, request, forWrite, ...)`,
`TryResolveUnder(root, request, ...)` (for the style kit folder), `IsInside`, `IsLink`, and `FindLink`.

**ProjectFileKinds**: `internal sealed class`. `IsTextFile`, `IsBinaryImage`, `IsImageSource`.

**ImageScaler**: `internal static class`. `FitWithin(image, maxEdge)`.

**ToolCallLog**: `internal sealed class`. `Record(tool, relativePath, bytes, outcome)`.

#### The Tools

**project_list(folder?)**: Lists the files below the project folder, or below a folder of it, as lines
`- path (N bytes[, image source])` with paths relative to the project folder and forward slashes. The order is fixed:
the files of a folder (by name, ignoring case) before its subfolders. Left out: the data folder (`.canvaspilot`), every
`*.canvaspilot` file, folders whose names begin with a dot (`.git`), files and folders with the hidden or system
attribute, and links. The list stops at 200 files, and a visit of more than 2,000 folders, and then says that it is not
complete and that a folder can be given. A folder that is not there, or is outside the project, is a refusal.

**project_read(path)**: Reads a file as UTF-8 text. The file must be a text image source or a `.csv`, `.json`, or `.txt`
file. It is refused, each with its reason, when it is a picture in a binary format (the answer points to
`render_image`), another kind of file, missing or a folder, larger than `ToolLimits.MaxReadBytes` (64 KiB, read with a
bound in case the file grows), not valid UTF-8, or longer than `ToolLimits.MaxResultCharacters` characters (32,000, the
ceiling for one answer; the library would refuse a longer answer, so the tool says so first). A byte order mark is not
part of the text. An empty file is said to be empty.

**project_write(path, content)**: Creates or replaces a file with the complete text. Rules, in order: the content must
be given; the path must pass the guard for writing; the first folder of the path must not be `styles` (any case),
because the style kits of the project are the user's and the copilot may read them but not change them; the file name
must end with the extension of a *text* image source or with `.csv`, `.json`, `.txt` (the binary image sources, such as
`.png` and `.pdf`, are refused, because writing text into them would only make a broken picture); the UTF-8 bytes must
be at most 256 KiB; the target must not be a folder. The file is written to a temporary file in the same folder
(`.{guid}.tmp`, flushed to disk) and moved over the old file, and missing folders are created. A failure part way
removes the temporary file and leaves the old file as it was. Moving over the old file replaces the name and not the
content of a hard link, so a second name of the file (possibly outside the project) is never written through. The answer
is `File 'x' created (N bytes).` or `File 'x' replaced (old bytes -> new bytes).` followed by a hint to call
`render_image`. `FileWritten` is raised after the write; a listener that throws is logged and ignored.

**render_image(path)**: Draws an image source or a picture file and returns the picture. See below.

#### Paths: the Guard

`ProjectPathGuard.TryResolve` asks the policy first (`TryResolveRead` or `TryResolveWrite`; the policy refuses `..` that
leaves the project, full paths and network paths outside it, paths relative to a drive, the data folder, and the project
file) and then checks, in order:

1. the resolved path is inside the project folder, by whole folder names (a folder beside the project whose name starts
   like it is outside);
2. every name in the path: the data folder and any `*.canvaspilot` name (again, the policy denies them by their long
   names); a name with a colon (a hidden stream of a file, `name:stream`); a name that ends with a dot or a space
   (Windows trims them, so the name means another one; Windows already trims them in the full path, and the check covers
   other platforms); a short 8.3 name (a tilde followed by a digit, `CANVAS~1`, which could stand for a long name that
   the policy denies); and a device name (`NUL`, `CON`, `COM1`, `LPT1`, with or without an extension);
3. no existing part of the path, from the first folder below the project down to the file itself, is a link (see *Which
   reparse points are links*). A link is judged before its existence, so a junction whose target is gone is found.

A refusal is a sentence for the model that names the reason and not an absolute path of the user's computer; it never
throws. The check cannot close the gap between the check and the use (a link made in between).

#### render_image in Detail

**How the picture is made (verified).** `IImageRenderService.RenderAsync(file, root, token)` draws in memory and returns
a `RenderedImage` (RGBA pixels, the format, the notes); `EncodePng(image)` encodes it into a byte array.
`ImageRenderService` writes no file (it reads the source file and, to fingerprint it, hashes it), so no route to save a
picture is needed and the tool cannot write into the project folder.

**Which file.** First as a path of the project (guard, read). Only when that is refused, and the request is a full path
inside the folder of the active style kit (`IActiveStyle.Active.FolderPath`, read at the time of the call), as a file of
the kit: guard `TryResolveUnder` (inside the kit folder, names, no link), and the kit folder is the root that the viewer
may read from. The kit grant is therefore the kit folder only, and a kit inside the project is just a path of the
project. In both cases the file must be an image source (`IImageSourceFilter`) and must exist.

**What comes back.** The PNG scaled so that the longer side is at most 1,280 pixels (`ImageScaler.FitWithin`, an average
of the block of pixels with the colors weighted by opacity; a smaller picture is not enlarged, and the element positions
are not carried over), as `ToolResult.Image(png, "image/png", text)`. The text is `Drawn 'x.svg' (svg), W x H pixels.`,
a sentence that says the picture was scaled when it was, and the notes of the renderer (`Severity: code: message`, at
most 50 and then a count) or that it reported none. When the PNG is larger than `ToolLimits.MaxBinaryBytes` (8 MiB) the
picture is made smaller in steps (1,280, 960, 640, 320) and, when it never fits, the answer is a refusal.

**Failure.** A render that fails is a normal answer: `'x.mmd' could not be drawn. Error {code}: {message} Hint: {hint}`
with the three parts of the renderer's `RenderError`. A renderer that throws is an answer that names only the type of
the exception. A canceled turn throws `OperationCanceledException` and nothing is drawn; a render that reports
cancellation while the turn goes on is an answer.

#### Key Methods

**CreatePacks(context)**: See *Data Model*. Throws `ArgumentNullException` for a missing context.

**ProjectFilesToolPack.CreateTools(policy)** and **RenderImageToolPack.CreateTools(policy)**: Make the functions with
`GuardedToolFactory.Create` from the delegate of each tool (the parameters and their words become the schema) and wrap
them with `AsGuarded(policy.Limits)`, so a result over the ceilings is refused by the library.

#### Error Handling

Building the packs can fail only for a missing argument. A *call* of a tool never throws: a refusal is a
`ToolResult.Denied(reason, sentence)` (path not permitted, target not found, unsupported media type, resource too large,
invalid request), a failure of the file system is a sentence with the type of the exception, and only the cancellation
of a stopped turn is let through. The text of an exception is never repeated, because it can hold a path of the user's
computer.

#### Security

The unit is the place where the copilot can hurt the user, so it is built to refuse first. The decisions that matter:
the policy decides the grant, and the guard checks the result again; a path through a link is refused wherever the link
is; the data folder, the project file, and the style kits are never written; only text sources and small data files are
written, at most 256 KiB each; a hard link is not written through; the folders that are created are below the project
folder because the path was checked and no part of it is a link; the picture tool writes nothing; the log holds metadata
only (the name of the tool, the path relative to the project, a size, the outcome; never content, never a message, never
the path of a refused request). Known limits: a link made between the check and the use cannot be prevented by a check;
a file with a second name outside the project can be *read* through its name inside the project (only writing is made
safe); and a project in a folder that the user shares can be changed by others while the copilot works.

#### Dependencies

`IImageRenderService` (Preview subsystem) and `RenderedImage`/`RenderResult`; `IActiveStyle` and `StyleKitCatalog`
(Style subsystem: the active kit and the name of the styles folder); `ProjectDocument` and `IImageSourceFilter` (Project
subsystem: the names of the data folder and project file, and the filter for image sources); `RollingFileLogger`
(Logging subsystem); the AgentKit library (`PathPolicy`, `IToolPack`, `GuardedToolFactory`, `AsGuarded`, `ToolResult`,
`DenialReason`, `ToolLimits`); and Microsoft.Extensions.AI (`AIFunction`, `DataContent`).

#### Callers

`AgentToolRegistry` takes `CreatePacks` as its supplier of project packs, and the application composition (a later step)
builds the `ProjectTools`, passes it to the registry, and handles `FileWritten` to refresh and select the file in the
project explorer.

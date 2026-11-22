### AgentToolRegistry

![Agent Tools](AgentToolsView.svg)

#### Purpose

`AgentToolRegistry` builds the tools that the AI copilot may use for one project. It confines every tool to the project
folder with one path policy, offers the drawing knowledge and the style tools (plain tool descriptions that know nothing
of the AI library) as functions of the library, adds the packs that the application supplies for the project and the
optional packs that the user chose, and refuses a set of tools that a model or its provider could not be given. The unit
also holds its supporting types: `DescriptorToolPack` and the `DescriptorFunction` that it makes, `AgentToolContext`,
and `OptionalPackEntry`.

#### Data Model

**AgentToolRegistry**: `sealed class`. The constructor takes the descriptions of the drawing knowledge tools and of the
style tools (`IEnumerable<KnowledgeTool>`, in this step given by the caller; the composition of the application passes
`KnowledgeTools.CreateAll` and `ApplicationComposition.StyleToolDescriptors` in a later step), an optional supplier of
project packs (`Func<AgentToolContext, IEnumerable<IToolPack>>`), an optional catalog of optional packs
(`IReadOnlyDictionary<string, OptionalPackEntry>`, empty by default), the `ToolLimits` (the defaults of the AI library
when not given), and the `HostCapabilities` of the model and provider (none by default). Both description lists are
copied and checked when the registry is made. The registry is immutable and safe for concurrent use.

**AgentToolContext**: `sealed record(ProjectFolder)`. What the tools are built for. It holds only the folder; a pack
that needs more (a renderer, the project file) gets it from the code that supplies the pack.

**OptionalPackEntry**: `sealed record(DisplayName, Factory, RequiredCapabilities)`. One entry of the catalog: the name a
settings page shows, a factory that makes the pack for a project, and the abilities the model or provider must have.

**DescriptorToolPack**: `sealed class`, implements the `IToolPack` of the AI library. Created with a family prefix and
the descriptions. It needs no capability.

**DescriptorFunction**: `internal sealed class`, derives from `AIFunction`. Makes a function of one `KnowledgeTool`.

#### The Rules

**Path policy.** `CreatePolicy(projectFolder, limits)` makes `new PathPolicy(folder, [PathRule.ReadWrite(folder, deny)],
limits)` where `folder` is the full path without a trailing separator and `deny` is `.canvaspilot` (the data folder,
`ProjectDocument.DataFolderName`) and `*.canvaspilot` (every project file, `ProjectDocument.FileExtension`). The library
matches a deny pattern against the file name and against every folder name on the way, so the data folder and a project
file are out of reach for reading and for writing at the top of the project and in any folder below it. A folder that is
empty, white space, or not a full path is refused, because a relative folder would anchor the tools to the current
directory. The API of the library (verified in the restored package, version 0.1.0-beta.3) is `PathPolicy(string
workingDirectory, IEnumerable<PathRule> grants, ToolLimits limits)` and `PathRule.ReadWrite(string root,
IEnumerable<string> denyPatterns)`; `TryResolveRead` and `TryResolveWrite` give a refusal as a message and not an
exception.

**Links are not followed (the finding of the AI library item).** The policy judges a path by its normalized spelling. A
folder link (a junction or a symbolic link) inside the project that leads outside it is therefore *permitted* by the
policy, and the resolved path keeps its spelling inside the project.
`AgentToolRegistry_CreatePolicy_FolderLinkInsideTheProject_IsPermittedAndMustBeRefusedByTheTools` pins this. The
registry does not hide it: the tools that read or write files (the project tools of a later step) must refuse a path
that passes through a reparse point themselves, and the verification of those tools covers it. A descriptor pack reads
no file, so it is not affected.

**Packs and order.** The tools come from these packs in this order: `canvasnet` (the knowledge descriptions,
`canvasnet_help`), `style` (`style_get`, `style_check`), the packs of the project supplier (the project tools, a later
step), then the optional packs that were chosen, ordered by identifier. All packs are composed by the library's
`ToolPackBuilder` under the one policy and the host capabilities. The builder refuses two packs with the same prefix and
a tool whose name does not start with the prefix of its pack; the registry checks the prefixes first and reports them in
its own sentence. A pack that needs an ability the host lacks is not offered by the builder.

**Optional packs.** The catalog is empty in this version. `CreateTools(context, optionalPackIds)` takes the identifiers
that the user chose (a set; later a list in the settings), refuses an identifier that is not in the catalog (naming it
and the known ones), and makes only the chosen packs. A later pack, for example one of the text file packs of the AI
library, is one catalog entry and one identifier; nothing else is restructured.

**The set check.** After the library has built the tools, `CreateTools` refuses the set with an
`InvalidOperationException` whose message names every problem found: it has no tool; two tools have the same name; a
name is one of the built-in tools of the Copilot CLI. `CopilotBuiltInToolNames` is the list of the library
(`CopilotAgentFactory.ReservedBuiltInToolNames`, which is not public, so a test reads it with reflection and fails when
an upgrade adds a name that ours lacks).

**The function of a description.** `DescriptorFunction` keeps the name and the words of the description; its
`JsonSchema` is the schema text of the description parsed once (an `ArgumentException` when it is not a JSON object); a
call writes the arguments with `AIJsonUtilities.DefaultOptions` as one JSON object (`{}` for none), calls the function
of the description on a pool thread, and returns the answer as `ToolResult.Text`. Each function is wrapped with
`AsGuarded(policy.Limits)`, so an answer over `MaxResultCharacters` is refused by the library with a message that names
`ResourceTooLarge`.

#### Key Methods

**CreatePolicy(projectFolder, limits)**: Static. Makes the path policy of a project, as described above.

**CreateTools(context, optionalPackIds)**: Makes the policy, the packs, and the tools of one project and checks the set.
Throws `ArgumentNullException`, `ArgumentException` (folder not a full path, identifier not in the catalog), or
`InvalidOperationException` (the set check, or two packs with one prefix). The agent service of a later step catches
these and reports them.

**OptionalPackIds / OptionalPacks**: The catalog, for a settings page.

**DescriptorToolPack(prefix, descriptions)**: Checks at construction that every description can be used, that every name
follows the naming rule of the library (`ToolName.Validate`), starts with `{prefix}_`, and is used once.

#### Error Handling

Building the tools can fail, and the failure is an exception with a message that names the cause, because a wrong set of
tools is a mistake of the code that builds it and not of the user. A *call* of a tool never fails with an exception: a
description that throws, a null answer, or arguments that cannot be written as JSON are an answer (a sentence that names
the tool and the type of the exception, or empty text). Only the cancellation of the turn is let through, and the
description is not called once the token is canceled.

#### Security

The path policy is the one reliable decision about which paths a tool may touch, so it is built in one place from the
project folder. The data folder and the project files are denied to every tool. The text of an exception is never put
into an answer (it can hold a path of the user's computer); only its type name is. The unit reads no file and writes no
file itself, and it does not log. Links are the one gap of the library and are the duty of the project tools (see
above).

#### Dependencies

`KnowledgeTool` of the Knowledge subsystem (the plain description), the project document of the Project subsystem (the
names of the data folder and project file), the AgentKit library (`PathPolicy`, `PathRule`, `IToolPack`,
`ToolPackBuilder`, `ToolLimits`, `ToolName`, `ToolResult`, `AsGuarded`), and Microsoft.Extensions.AI (`AIFunction`,
`AIJsonUtilities`). The unit does not use the Copilot SDK and does not name the style tools: the style descriptions are
given by the caller.

#### Callers

The agent service (a later step) builds the tools of the open project with it, and the application composition passes
the descriptions. The project tools (a later step) are supplied through the project pack supplier.

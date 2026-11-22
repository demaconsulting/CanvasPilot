### AgentInstructions

![Agent Tools](AgentToolsView.svg)

#### Purpose

`AgentInstructions` writes the instructions that the AI copilot is given for one project. They are made of four parts,
in this order: the role prompt, the index of the drawing knowledge, the facts of the project, and the style of the
project. The role prompt is an embedded Markdown resource (`AgentSubsystem/Instructions/RolePrompt.md`) that tells the
copilot how to work. The index, the project facts, and the style tell it what exists. The unit writes text and nothing
else: it reads no file, calls no tool, logs nothing, and holds no state. It also holds its supporting types:
`AgentProjectFacts` (what is known about the open project) and `InstructionTier` (how much to write).

#### Decisions

**Order of the parts.** The role prompt and the index do not change from project to project, so they come first and the
facts and the style, which change with the project, come last. A provider that caches the start of a request can then
reuse the start for every project. The same input always gives the same text, with line feeds as line breaks and no
dependence on the culture, so the text can be tested and compared.

**Two tiers, chosen by the context window.** `ChooseTier(provider, contextWindowTokens)` gives the full tier for every
GitHub Copilot model (its window is large and is not set by the user) and for an Ollama window of at least 16,384
tokens, which is `AiProviderSettings.RecommendedMinimumContextTokens`; a smaller window gets the compact tier. The user
is already warned about a window below that value in the settings, and the compact tier is what lets such a window work
at all.

| Part | Full tier | Compact tier |
| --- | --- | --- |
| Role prompt | the whole prompt | the whole prompt |
| Index of the knowledge | every card (`BuildIndex()`) | the cards at the top only (`BuildIndex(0)`) |
| List of files | at most 40 files and 1,600 characters of paths | at most 15 files and 500 characters of paths |
| Style | `StyleGuidance.Build(kit)` | `StyleGuidance.Build(kit, includeExamples: false)` |

**Why the compact index is the top level only.** The index of the cards below the top is almost the whole index: with
the cards of the application, depth 0 (the top cards) is 1,859 characters, depth 1 is 10,527, and every card is 11,717.
An index that stops one level deeper would leave no room in a compact budget of 11,000 characters, so the compact tier
lists the top cards and the copilot asks for a card by its identifier with `canvasnet_help`, which answers with the
cards below it. The example images of the style are left out of the compact tier because they are only paths, which the
copilot can still ask for with `style_get` and the section `examples`.

**Sizes are measured limits.** The budgets are constants of the unit and each is checked by a test that builds the text
from the real parts: the instructions of the full tier are at most 20,000 characters (about 5,000 tokens) and those of
the compact tier at most 11,000 (about 3,000 tokens), for every style kit that comes with the program and for the
longest list of the longest names; the words and schemas of all seven tools of the copilot together (the name, the
description, and the JSON Schema of each, as the registry builds them) are at most 7,000 characters. The role prompt
itself is at most 3,000 characters, which the constructor checks. When the unit was made, the full tier was 18,025 to
18,180 characters, the compact tier 6,380 to 6,538, the role prompt 1,738, and the tools 4,527. The remaining room is
meant for a card that is added later, for which the index has its own limit of 12,000 characters
(`KnowledgePromptBuilder.IndexBudgetCharacters`).

**The role prompt.** The prompt (about 1,700 characters) says, in this order of ideas: the copilot draws with CanvasNet
in the open project and uses only the tools it has, and names them; it asks for a card before it uses a format it is
unsure of; it calls `style_get` before it draws and `style_check` after; it writes a drawing with `project_write` and
looks at the picture with `render_image` after every change, and never calls a drawing done before it looked; the
example images that `style_get` names are outside the project and can be looked at with `render_image` only; paths are
relative to the project folder with forward slashes, and a file that is not known to exist is looked up with
`project_list` and never invented; before it replaces a file, it reads the file with `project_read` and keeps what the
user did not ask it to change, because `project_write` overwrites at once with no backup and no question and the user
relies on source control; a mark of a message is the place that the user means; and it explains briefly what it did. The
subsystem test checks that the tool names in the prompt are exactly the names of the tools that the registry offers, so
a renamed tool cannot leave the prompt behind.

#### Data Model

**AgentProjectFacts**: `sealed record(Name, Folder, Files, ActiveStyle, ActiveStyleId)`. `Files` are paths relative to
the project folder, in the order in which they should be listed (the caller sorts them); `ActiveStyle` is the entry of
the kit that applies (`StyleKitEntry`, or `null`), and `ActiveStyleId` is the id that the project file names (or
`null`), which is told to the copilot when the kit cannot be found.

**InstructionTier**: `enum` with `Full` and `Compact`.

**AgentInstructions**: `sealed class`. The constructor takes the writer of the index (`KnowledgePromptBuilder`) and
optionally a role prompt, which is by default the embedded resource (read once, line breaks made `\n`, trimmed). A role
prompt that is empty or longer than 3,000 characters is an `ArgumentException`. The constants are
`FullTierMinimumContextTokens` (16,384), `RolePromptBudgetCharacters` (3,000), `FullBudgetCharacters` (20,000),
`CompactBudgetCharacters` (11,000), `ToolSchemaBudgetCharacters` (7,000), the number and character limits of the file
list, and `MaxLineCharacters` (120).

#### Key Methods

**ChooseTier(provider, contextWindowTokens)**: Static. `Full` when the provider is GitHub Copilot or the window is at
least 16,384 tokens, otherwise `Compact`.

**Build(facts, tier)**: Writes the text. The role prompt, then `## Drawing knowledge` and the index, then
`## This project` with `- Name:`, `- Folder:`, and `- Files (relative to the folder...):` followed by one `  - path`
line for each file listed, then `## Style`. The files that are blank are left out; backslashes become forward slashes;
the head of the list is taken while both the number limit and the character limit allow it, and at least one file is
always listed, so a list that was cut says `the first N of M; call project_list for the rest`. With no file the line
says `none yet; create the first one with project_write`. The style part is the line
`Active style kit: {name} (id {id}).` and the block of `StyleGuidance`; when no kit applies it says that the project has
no style kit (and to draw plain and to tell the user a kit can be chosen), or, when the project file names a kit that
cannot be found, that the kit is missing and to draw without a style and say so.

#### Error Handling

`Build` throws `ArgumentNullException` for missing facts and `ArgumentOutOfRangeException` for a tier that is not
defined; the constructor throws for a missing writer or a role prompt out of range. A missing embedded resource is an
`InvalidOperationException`, which is a mistake of the build and is found by the first test. Nothing else can fail:
every value of the facts is made one line, a kit never makes the style longer than its own budget (4,000 characters),
and an odd project (no files, no kit, a kit that is missing) has its own sentence.

#### Security

The text of the instructions is sent to a provider, so it holds only what the copilot needs: the name and folder of the
project, the relative names of some of its files, and the style of the kit. It holds no setting, no token, no message of
the user, and no content of any file. A name or path can hold anything the user typed, so every value is turned into one
line (a run of white space or control characters becomes one space) and cut at 120 characters, so a file name with a
line break cannot start a new instruction. The style block comes from a kit that may be shared by a team, so it is the
block of `StyleGuidance`, which says that it is data and not instructions and makes the free text of a kit safe.

#### Dependencies

`KnowledgePromptBuilder` of the Knowledge subsystem (the index); `StyleGuidance` and `StyleKitEntry` of the Style
subsystem (the style, with and without the example images); `AiProviderSettings` and `AiProviderKind` of the Settings
subsystem (the recommended minimum window and the provider). The unit uses no AI library.

#### Callers

The agent service (a later step) writes the instructions when it starts a provider session for a project: it chooses the
tier from the provider and the context window of the settings, takes the facts from the open project and the active
style, and gives the text to the agent of the provider.

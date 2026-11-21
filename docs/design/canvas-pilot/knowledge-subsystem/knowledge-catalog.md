### KnowledgeCatalog

![Knowledge Structure](KnowledgeSubsystemView.svg)

#### Purpose

`KnowledgeCatalog` builds the tree of knowledge cards of the application and answers lookups in it: the help cards of
CanvasNet, extended with the cards of CanvasPilot that are embedded in the application, without the few cards that are
only about the program that hosts CanvasNet, copied into values of its own.

#### Data Model

- `KnowledgeCard`: `Id` (the path, for example `svg/framed-figure`), `Name`, `ParentId` (the nearest card above that is
  offered, none for a root), `Title`, `Summary`, `Tags`, `Aliases`, `Body`, `ChildIds`, `SeeAlsoIds`, and `Examples`.
- `KnowledgeExample`: `Name`, `Kind` (`source`, `snippet`, or `command`), `Language`, `FileName`, `Description`, and
  `Content`. `KnowledgeExampleReference` pairs an example with the identifier of the card that holds it.
- The cards of CanvasPilot are Markdown files with front matter under `KnowledgeSubsystem/Cards`, and their examples
  are files under `KnowledgeSubsystem/Examples`, named `root-file`. The project file embeds them with the logical names
  that CanvasNet reads: `DemaConsulting.CanvasPilot.Knowledge/{path}.md` and
  `DemaConsulting.CanvasPilot.Knowledge.Examples.{root}-{file}`.
- The six cards of CanvasPilot are the root card `canvaspilot` and five cards below it: `project-layout`,
  `deliverables`, `marks`, `preview`, and `styles`, which tells the assistant how to draw in the style of the project
  and when to call `style_get` and `style_check`. The names of the tools, of the folder of the kits of a project, of the
  style file, and of the sections of the style that the card quotes are checked against the constants of the code.
- `ExcludedCardIds` is the list of identifiers of the cards that are left out, each only about the program that hosts
  CanvasNet:

  | Card | Why it is left out |
  | --- | --- |
  | `errors` | How a host program receives a failure (exceptions, error constants) and sets strict mode. |
  | `mermaid/options` | The render options of a diagram that the host program sets in code. |
  | `plantuml/options` | The same for a PlantUML diagram. |
  | `scad/options` | The same for a model: variables, time, and triangle budget set by the host program. |
  | `svg/options` | The same for a drawing: size, scale, background, folder, and file-access policy. |
  | `vega-lite/options` | The same for a chart; the params of a link are in `vega-lite/params`. |
  | `svg/flatten` | The host program's operation that writes a drawing copy for browsers and other SVG readers. |

  The other cards that carry the CanvasNet tag `hosting` are offered: `files`, `pdf`, `pptx`, `vsdx`, `raster`,
  `scad/views`, `vega-lite/params`, `positions`, and `positions/selectors` tell how a drawing embeds a page, a picture,
  or a file, how a link sets a view or a param, and what a callout binds to. The children of `errors`, the codes and the
  diagnostics, are offered and move to the top level.

#### Key Methods

**Default**: builds the catalog of the application on first use, once and thread-safe, from the CanvasNet help catalog,
the embedded cards, and `ExcludedCardIds`.

**Create(library, assembly, prefix, excludedCardIds)**: the internal builder, used by `Default` and by tests. It extends
the library catalog with the cards of the assembly, refuses an identifier that is not a card, and copies each card that
is not left out. The parent of a card is the nearest card above it that is offered, so the children of a card that is
left out become children of the nearest offered card above it or, with none, roots; the children of a card are the
offered cards below it, and the related cards are those that are offered.

**Find(path)**: finds the card of a path whose segments are separated by a slash or white space, each the name or
another name of a card, ignoring case. A path that names no card, or a card that is left out, gives nothing.

**GetChildren(card) and GetSeeAlso(card)**: the cards below a card and the cards related to it, in order.

**FindExamples(name, scopeCardId)**: the examples of a name, in order of the cards, in all cards, in the card of the
scope and below it, or in the card that a name with a card in front of it (`card-id#name`) selects. The match of the
name ignores case. Where several cards hold an example of the name, as for the sequence diagram of two formats, all are
given.

**Link writing**: the body of a card is written by CanvasNet with each link written by the catalog: a card that is
offered as its identifier in code quotes, an example as the name of the example and the identifier of its card. A link
to a card that is left out is written as the title of that card, and a link to an example of such a card as "an
example".

#### Error Handling

A malformed embedded card, or one that does not fit the tree, is an `InvalidOperationException` that CanvasNet raises
with the name of the file and the line; because the default catalog is lazy, the exception is raised at first use, and
the tests load it, so a malformed card fails the build and not the user. A missing argument is an
`ArgumentNullException`, and an identifier of a card that does not exist an `ArgumentException`. The unit performs no
I/O except reading embedded resources and logs nothing.

#### Dependencies

- **CanvasNet** (OTS): `HelpCatalog` for the cards, `Extend` for the cards of the application, and `FormatMarkdown`,
  `GetChildren`, and `GetSeeAlso` for the parts of a card.

#### Callers

- The search, the builder of the prompt, the tools and skills for an assistant, and the browser of the cards of later
  steps read the catalog through `IKnowledgeCatalog`.

### KnowledgeCatalog

![Knowledge Structure](KnowledgeSubsystemView.svg)

#### Purpose

`KnowledgeCatalog` builds the tree of knowledge cards of the application and answers lookups in it: the help cards of
CanvasNet, extended with the cards of CanvasPilot that are embedded in the application, without the cards that carry the
tag `hosting`, copied into values of its own.

#### Data Model

- `KnowledgeCard`: `Id` (the path, for example `svg/framed-figure`), `Name`, `ParentId` (the nearest card above that is
  offered, none for a root), `Title`, `Summary`, `Tags`, `Aliases`, `Body`, `ChildIds`, `SeeAlsoIds`, and `Examples`.
- `KnowledgeExample`: `Name`, `Kind` (`source`, `snippet`, or `command`), `Language`, `FileName`, `Description`, and
  `Content`. `KnowledgeExampleReference` pairs an example with the identifier of the card that holds it.
- The cards of CanvasPilot are Markdown files with front matter under `KnowledgeSubsystem/Cards`, and their examples
  are files under `KnowledgeSubsystem/Examples`, named `root-file`. The project file embeds them with the logical names
  that CanvasNet reads: `DemaConsulting.CanvasPilot.Knowledge/{path}.md` and
  `DemaConsulting.CanvasPilot.Knowledge.Examples.{root}-{file}`.
- `ExcludedTags` is the list of tags that are left out: `hosting`.

#### Key Methods

**Default**: builds the catalog of the application on first use, once and thread-safe, from the CanvasNet help catalog,
the embedded cards, and `ExcludedTags`.

**Create(library, assembly, prefix, excludedTags)**: the internal builder, used by `Default` and by tests. It extends
the library catalog with the cards of the assembly and then filters the result, and copies each visible card. The parent
of a card is the nearest card above it that is offered, found from the children of the filtered catalog, so the children
of a card that is left out become roots.

**Find(path)**: finds the card of a path whose segments are separated by a slash or white space, each the name or
another name of a card, ignoring case. A path that names no card, or a card that is left out, gives nothing.

**GetChildren(card) and GetSeeAlso(card)**: the cards below a card and the cards related to it, in order.

**FindExamples(name, scopeCardId)**: the examples of a name, in order of the cards, in all cards, in the card of the
scope and below it, or in the card that a name with a card in front of it (`card-id#name`) selects. The match of the
name ignores case. Where several cards hold an example of the name, as for the sequence diagram of two formats, all are
given.

**Link writing**: the body of a card is written by CanvasNet with each link to a card that is offered written by the
catalog: a card as its identifier in code quotes, an example as the name of the example and the identifier of its card.
CanvasNet writes a link to a card that is left out as the title of that card.

#### Error Handling

A malformed embedded card, or one that does not fit the tree, is an `InvalidOperationException` that CanvasNet raises
with the name of the file and the line; because the default catalog is lazy, the exception is raised at first use, and
the tests load it, so a malformed card fails the build and not the user. A missing argument is an
`ArgumentNullException`, and an unknown tag of the filter an `ArgumentException`. The unit performs no I/O except
reading embedded resources and logs nothing.

#### Dependencies

- **CanvasNet** (OTS): `HelpCatalog` for the cards, `Extend` for the cards of the application, and `Filter` for the
  tags.

#### Callers

- The search, the builder of the prompt, the tools and skills for an assistant, and the browser of the cards of later
  steps read the catalog through `IKnowledgeCatalog`.

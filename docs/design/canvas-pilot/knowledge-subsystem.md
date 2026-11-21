## Knowledge

![Knowledge Structure](KnowledgeSubsystemView.svg)

### Overview

The `Knowledge` subsystem holds what the drawing assistant of a later increment, and the user, need to know to draw with
the formats CanvasNet draws and to work in a CanvasPilot project. The knowledge is a tree of cards. Each card has a
title, a one-line summary, tags, other names, a body in Markdown that links to other cards and examples, related cards,
and examples with their text. The cards come from two places: the help of CanvasNet, which tells how to write each
format and is as current as the library that draws the files, and cards of CanvasPilot that are embedded in the
application and tell what the library cannot know: where files go in a project, how to make the picture the user uses,
how to read the marks and attachments of a message, and what the user sees. The subsystem has no dependency on the user
interface and none on any provider of a language model or on any agent library, so the knowledge can be given to any
assistant that is added later.

The subsystem contains three units: `KnowledgeCatalog`, which builds the tree and answers lookups, `KnowledgeSearch`,
which finds the cards that fit some words, and `KnowledgePromptBuilder`, which writes the cards as text for the prompt
of an assistant. It depends on CanvasNet for the help cards and on `Project`, `Composer`, and `Annotation` only in its
tests, which check that what the cards of CanvasPilot say about the application is what the code says.

### Interfaces

**IKnowledgeCatalog and KnowledgeCatalog**: The tree of cards.

- *Type*: In-process .NET interface and sealed class, with the immutable values `KnowledgeCard`, `KnowledgeExample`, and
  `KnowledgeExampleReference`.
- *Role*: Provider.
- *Contract*: `Cards` lists every card depth first, `Roots` the cards with no card above them. `Find` takes a path and
  gives a card or nothing, `GetChildren` and `GetSeeAlso` give the cards below a card and the cards related to it, and
  `FindExamples` gives the examples of a name. `KnowledgeCatalog.Default` is the catalog of the application, built on
  first use, once.
- *Constraints*: Immutable after it is built and safe for concurrent use. Reading an embedded card that is malformed is
  an `InvalidOperationException` that names the card file.

**KnowledgeSearch**: The search of the cards.

- *Type*: In-process .NET class, with the immutable value `KnowledgeSearchResult`.
- *Role*: Provider.
- *Contract*: `Search` takes the words of a query and a number of results and returns the cards that fit, best first,
  each with its identifier, title, summary, and a positive score. It is made from an `IKnowledgeCatalog`.
- *Constraints*: Deterministic: the same query on the same cards gives the same list. Safe for concurrent use.

**KnowledgePromptBuilder**: The cards as text for a prompt.

- *Type*: In-process .NET class.
- *Role*: Provider.
- *Contract*: `BuildIndex` writes the index of every card (or of the top levels of the tree), `BuildCard` a card with
  its related cards and, when asked, the text of its examples, `BuildRelated` the cards below a card and the cards
  related to it, `BuildExample` one example in a fenced block, and `BuildSearchResults` the cards that a search found.
  The index of the catalog of the application is limited to `IndexBudgetCharacters`, 12,000 characters.
- *Constraints*: Plain text with line feeds, the same for the same cards, and no state. Safe for concurrent use.

### Design

**Extend first, filter second.** The catalog is made from the help catalog of CanvasNet by adding the cards that are
embedded in the application and then hiding the cards that carry the tag `hosting`. The order matters, because CanvasNet
refuses to extend a catalog that is already filtered. The cards of CanvasPilot are one new tree, `canvaspilot`, which
follows the trees of CanvasNet; they carry scope tags that are never hidden.

**Why `hosting` is left out.** CanvasNet puts the tag `hosting` on the cards about what the program that hosts it
controls: the options of a render, the folders it may read, the flattening of an SVG file, and the command-line tool.
CanvasPilot is that program and sets those options itself, and the assistant writes files, so those cards would only
take room in a prompt. The tag filter only excludes, so every other tag stays and a card that a later version of
CanvasNet adds is offered without a change. The cost is accepted and recorded: the cards for the binary formats, the
card of errors (its children stay and move to the top level), the cards of positions, and a few cards of options are not
offered.

**Own values.** Every card is copied into a `KnowledgeCard`, so that what searches the cards, writes them for a prompt,
and shows them does not depend on the help types of CanvasNet and can be tested with small catalogs. The links of a body
are written once, here, as the identifier of a card in code quotes or as the name of an example and the card that holds
it; a link to a card that is left out is plain text.

**Deterministic and shared.** The default catalog is built once, thread-safe, from embedded resources only, so no file
or network is touched and every caller sees the same tree.

**Search by weighted words.** The search scores the words of a query against the identifier, other names, title, example
names, tags, and summary of each card with fixed weights, and does not read the body, so the card that is about a thing
comes before the cards that only mention it and a list is short enough for a prompt. Ties are broken by depth and
identifier, so the answer never depends on the order of a dictionary, the culture, or chance.

**A compact index and cards on demand.** The whole text of the cards is many times what a prompt can hold, so an
assistant is given an index with one line for each card and asks for the cards it needs. The size of the index has a
limit that the tests check on the real catalog, so a card that makes it too large fails the build.

### Dependencies

- **KnowledgeCatalog** unit: the tree of cards.
- **KnowledgeSearch** unit: the search of the cards, which reads the catalog through its interface.
- **KnowledgePromptBuilder** unit: the text for a prompt, which reads the catalog through its interface and writes the
  results of the search.
- **CanvasNet** (OTS): the help cards, their tag filter, and their extension by another assembly.

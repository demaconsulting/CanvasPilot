## Knowledge

![Knowledge Structure](KnowledgeSubsystemView.svg)

### Overview

The `Knowledge` subsystem holds what the AI copilot needs to know to draw with
the formats CanvasNet draws and to work in a CanvasPilot project. The knowledge is a tree of cards. Each card has a
title, a one-line summary, tags, other names, a body in Markdown that links to other cards and examples, related cards,
and examples with their text. The cards come from two places: the help of CanvasNet, which tells how to write each
format and is as current as the library that draws the files, and cards of CanvasPilot that are embedded in the
application and tell what the library cannot know: where files go in a project, how to make the picture the user uses,
how to read the marks and attachments of a message, and what the user sees. None of it depends on the user interface,
on any provider of a language model, or on any agent library, so the knowledge can be given to any provider of the
copilot.

The subsystem contains five units: `KnowledgeCatalog`, which builds the tree and answers lookups, `KnowledgeSearch`,
which finds the cards that fit some words, `KnowledgePromptBuilder`, which writes the cards as text for the prompt of an
assistant, `KnowledgeTools`, which describes the tool the assistant calls to get them, and `KnowledgeSkills`, which
describes each card as a skill. It depends on CanvasNet for the help
cards, and on `Project`, `Composer`, and `Annotation` only in its tests, which check that what the cards of CanvasPilot
say about the application is what the code says.

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

**KnowledgeTools**: The tool that an assistant calls to get the knowledge.

- *Type*: In-process static class, with the immutable value `KnowledgeTool`.
- *Role*: Provider.
- *Contract*: `CreateHelpTool` and `CreateAll` give the tool `canvasnet_help`: a name, a description, the JSON Schema of
  its arguments as a JSON text, and a function that takes the arguments as a JSON text and returns text. With no
  arguments it answers the index; with `topic` a card; with `example` the text of an example, or the choices written as
  `card-id#name`; with `query` the cards that fit some words. A topic or example that nothing has is answered with the
  closest cards.
- *Constraints*: The function never throws; arguments that cannot be used are answered with a message that names the
  arguments. The same arguments on the same cards give the same answer. Safe for concurrent use.

**KnowledgeSkills**: The cards as skills for an assistant.

- *Type*: In-process static class, with the immutable value `KnowledgeSkill`.
- *Role*: Provider.
- *Contract*: `Create` gives one skill for each card of a catalog, in order: a name that is `canvasnet-` and the
  identifier of the card with each slash written as a dash, a description that is the title and summary of the card, and
  a content that is the card with the text of its examples. `NameFor` gives the name of a card.
- *Constraints*: The skills are made as they are read. The names of the skills of the catalog of the application are all
  different, which the tests check. Safe for concurrent use.

### Design

**Extend first, then leave cards out.** The catalog is made from the help catalog of CanvasNet by adding the cards that
are embedded in the application and then leaving out the cards of `ExcludedCardIds`. The order matters, because
CanvasNet refuses to extend a catalog that is already filtered, and its filter works by tags only, so the exclusion by
identifier is done here, on the extended catalog. The cards of CanvasPilot are one new tree, `canvaspilot`, which
follows the trees of CanvasNet; none of them is left out.

**Which cards are left out, and why by identifier.** CanvasNet puts the tag `hosting` on sixteen cards, but the tag
means "something a host program decides", and for more than half of them a drawing needs it: the formats that are
embedded as pages (`pdf`, `pptx`, `vsdx`), the raster pictures (`raster`), the files a document may reference (`files`),
the fragments that set a view or a param of a link (`scad/views`, `vega-lite/params`), and the positions that a callout
binds to (`positions`, `positions/selectors`). A rule such as "hosting and not tagged with a format" does not separate
them, because the cards of the options of a format carry the tag of that format. So the exclusion is a list of
identifiers, which is exact, is reviewed card by card, and fails loudly: `Create` refuses an identifier that is not a
card, so a card that CanvasNet renames or removes fails the build instead of being offered silently. A card that a later
version of CanvasNet adds is offered until it is reviewed. The cards that are left out, and why, are listed in the
design of the `KnowledgeCatalog` unit: the `errors` card of the host's exceptions and strict mode, the five cards of
render options of a format that the host program sets in code, and `svg/flatten`, an operation of the host program for
other SVG readers.

The children of `errors`, the codes and the diagnostics, stay: they explain what the preview shows, and they move to the
top level. A related card that is left out is dropped from the list of related cards, and a link to it in a body is
written as its title, so no identifier, list, index line, skill, or search result names a card that is not offered. The
cards that are offered still contain some wording for the host program (a command-line tool, Pandoc, the classes of
CanvasNet); the root card of CanvasPilot tells the assistant that it cannot run those and which tools to use instead
(`canvasnet_help` and the file tools).

**Own values.** Every card is copied into a `KnowledgeCard`, so that what searches the cards, writes them for a prompt,
makes skills of them, and answers the help tool does not depend on the help types of CanvasNet and can be tested with
small catalogs. The links of a body are written once, here, as the identifier of a card in code quotes or as the name of
an example and the card that holds it; a link to a card that is left out is plain text.

**Deterministic and shared.** The default catalog is built once, thread-safe, from embedded resources only, so no file
or network is touched and every caller sees the same tree.

**Search by weighted words.** The search scores the words of a query against the identifier, other names, title, example
names, tags, and summary of each card with fixed weights, and does not read the body, so the card that is about a thing
comes before the cards that only mention it and a list is short enough for a prompt. Ties are broken by depth and
identifier, so the answer never depends on the order of a dictionary, the culture, or chance.

**A compact index and cards on demand.** The whole text of the cards is many times what a prompt can hold, so an
assistant is given an index with one line for each card and asks for the cards it needs. The size of the index has a
limit that the tests check on the real catalog, so a card that makes it too large fails the build.

**A tool that answers in messages.** The assistant reaches the cards through one tool whose description is plain values
and whose function takes and returns text, so that it can be handed to the tool mechanism of any provider that is
chosen later. The function never throws, because an assistant corrects itself from a message that says what to send
and cannot from an exception; a name that does not exist is answered with the cards that are closest to it.

**Skills made as they are read.** A provider that works with skills can be given each card as one, with the identifier
of the card in its name, the title and summary as its description, and the card with its examples as its content. The
text of all the cards is many times what should be built up front, so the skills are made one at a time as they are
read.

### Dependencies

- **KnowledgeCatalog** unit: the tree of cards.
- **KnowledgeSearch** unit: the search of the cards, which reads the catalog through its interface.
- **KnowledgePromptBuilder** unit: the text for a prompt, which reads the catalog through its interface and writes the
  results of the search.
- **KnowledgeTools** unit: the tool for an assistant, which answers from the catalog, the search, and the text writer.
- **KnowledgeSkills** unit: the cards as skills, which reads the catalog and writes each card with the text writer.
- **CanvasNet** (OTS): the help cards, their tag filter, and their extension by another assembly.

The view below shows the dependencies between the units of this subsystem and on CanvasNet, from the SysML2 model.

![Knowledge dependencies](KnowledgeSubsystemDependencyView.svg)

### KnowledgePromptBuilder

![Knowledge Structure](KnowledgeSubsystemView.svg)

#### Purpose

`KnowledgePromptBuilder` writes the knowledge cards as plain text for the prompt of an assistant. The whole text of the
cards is many times what a prompt can hold, so an assistant is given a compact index of every card and then asks for the
cards, examples, and searches it needs; this unit writes each of those texts, the same for the same cards.

#### Data Model

- The unit holds only the `IKnowledgeCatalog` that it writes from. It has no other state.
- `IndexBudgetCharacters` is 12,000: the most characters that the index of the catalog of the application may have. The
  tests of the unit check it on the real catalog, so a new card of CanvasPilot, or a new version of CanvasNet that adds
  many cards or long summaries, fails the build instead of making every prompt larger. The cards at the top of the tree
  have their own, smaller limit of 2,500 characters, for a prompt that cannot hold the whole index.
- The text of a line of the index and of every list of cards is `- id: title - summary`, with no indentation, because
  the identifier already shows where the card is in the tree.

#### Key Methods

**BuildIndex(maxDepth)**: the first line says what the cards are and how to ask for one (a card by identifier, an
example by name or by `id#name`, a search by words, with the tool `canvasnet_help`), and then one line for each card in
the order of the tree. A depth of zero writes only the cards at the top, one writes the cards below them as well, and so
on; the default writes every card. A depth below zero is refused. The text has line feeds and no final line feed.

**BuildCard(card, includeExamples)**: the heading with the title, the line `id:`, the summary, `Tags:` and
`Also known as:` when the card has them, a blank line, the body of the card as the catalog wrote it, the related cards
as written by `BuildRelated`, and a section `## Examples` with a line for each example: its name, kind, language, file,
and description. When examples are asked for, the text of each example follows, written by `BuildExample`.

**BuildExample(cardId, example)**: a line that names the example, its card, and its kind, language, and file (a command
has no file), then the text of the example in a fenced block that has the language of the example. The fence is longer
than the longest run of backticks in the text, so that the text cannot end the block. The method is static, because it
needs no catalog.

**BuildRelated(card)**: the sections `## Cards` (the cards below the card) and `## See also` (the related cards), each
only when it has a card, written as lines of the form of the index and separated by a blank line; empty when the card
has neither.

**BuildSearchResults(results)**: a line that says what follows and a line for each card, best first, without the score,
or the single line `No card matches.` when the search found no card. The method is static, because it needs no catalog.

#### Error Handling

A missing argument is an `ArgumentNullException` and a depth below zero an `ArgumentOutOfRangeException`. The unit
performs no I/O and logs nothing, holds no state that changes, and is safe for concurrent use.

#### Dependencies

- **KnowledgeCatalog** unit: the cards, the cards below them, and the cards related to them, through
  `IKnowledgeCatalog`.
- **KnowledgeSearch** unit: the type of the results of a search that `BuildSearchResults` writes.

#### Callers

- The tool and the skills for an assistant of later steps give the index and the texts of cards, examples, and searches
  to the assistant.

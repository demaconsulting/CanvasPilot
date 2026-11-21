### KnowledgePromptBuilder

#### Verification Approach

`KnowledgePromptBuilder` is verified with unit tests in `KnowledgePromptBuilderTests.cs`. The exact text of every form
is checked on a small catalog that the test makes by hand with a fake of `IKnowledgeCatalog`, so that the whole text can
be compared. The size of the index is checked on the catalog of the application, whose cards are the real content that
the limit protects. Nothing is substituted in the unit itself.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The index of the small catalog is exactly the header and one line for each card in tree order, and a depth limits the
  levels; a depth below zero and a missing catalog are refused; an empty catalog gives only the header.
- The index of the catalog of the application has at most 12,000 characters, the index of the cards at the top has at
  most 2,500, every card has its line, and the text has line feeds only.
- A card is written exactly with its heading, identifier, summary, tags, other names, body, children, related cards, and
  list of examples; with examples asked for, each example follows in a fenced block; a card without optional parts has
  only the parts it has.
- An example is written exactly, with and without a file name, and its fence is longer than any run of backticks in its
  text.
- The cards below a card and the cards related to it are written in their sections, and nothing for a card with neither.
- The result of a search is written with a line for each card, best first, or one line when there is none.
- A card of the application is written the same twice and keeps the links of its body as identifiers and example names.

#### Test Scenarios

**KnowledgePromptBuilder_BuildIndex_SmallCatalog_IsHeaderAndOneLinePerCardInTreeOrder**: The index of a catalog of four
cards is the header line and four lines in tree order, with no final line feed. This scenario is tested by
`KnowledgePromptBuilder_BuildIndex_SmallCatalog_IsHeaderAndOneLinePerCardInTreeOrder`.

**KnowledgePromptBuilder_BuildIndex_MaxDepth_LimitsTheLevels**: Depth zero lists the two cards at the top, depth one
adds the card below the first, and a depth below zero is refused. This scenario is tested by
`KnowledgePromptBuilder_BuildIndex_MaxDepth_LimitsTheLevels`.

**KnowledgePromptBuilder_BuildIndex_EmptyCatalogAndNullCatalog_AreHandled**: A catalog without cards gives the header
only, and a missing catalog is refused. This scenario is tested by
`KnowledgePromptBuilder_BuildIndex_EmptyCatalogAndNullCatalog_AreHandled`.

**KnowledgePromptBuilder_BuildIndex_RealCatalog_FitsBudget**: The index of the catalog of the application has at most
12,000 characters, the index of its top cards at most 2,500, and one line for each card. The measured size after the
exclusion of the cards that are only about the host program was 11,556 characters for the whole index, 1,859 for the top
cards, and 10,366 for the top cards and the level below them, for 91 cards in 17 trees; the budgets were not changed
(the whole index was 10,584, 1,680, and 9,394 for 82 cards before the cards for embedding pages, pictures, and files
were offered). This scenario is tested by
`KnowledgePromptBuilder_BuildIndex_RealCatalog_FitsBudget`.

**KnowledgePromptBuilder_BuildCard_WithoutExamples_IsTheCardWithListsOfRelatedCardsAndExamples**: The text of a card
with tags, other names, a child, a related card, and two examples is the exact expected text with the list of examples
but not their text. This scenario is tested by
`KnowledgePromptBuilder_BuildCard_WithoutExamples_IsTheCardWithListsOfRelatedCardsAndExamples`.

**KnowledgePromptBuilder_BuildCard_WithExamples_AddsEachExampleInAFencedBlock**: With examples asked for, the text
starts as the text without them and ends with both examples in fenced blocks. This scenario is tested by
`KnowledgePromptBuilder_BuildCard_WithExamples_AddsEachExampleInAFencedBlock`.

**KnowledgePromptBuilder_BuildCard_BareCard_HasOnlyTheParts**: A card with no tags, other names, children, related
cards, or examples has its heading, identifier, summary, and body; a missing card is refused. This scenario is tested by
`KnowledgePromptBuilder_BuildCard_BareCard_HasOnlyTheParts`.

**KnowledgePromptBuilder_BuildExample_FileCommandAndTextWithFence_AreWrittenInAFencedBlock**: An example with a file, a
command without a file, and an example whose text has a fence are written exactly, the last with a longer fence; missing
arguments are refused. This scenario is tested by
`KnowledgePromptBuilder_BuildExample_FileCommandAndTextWithFence_AreWrittenInAFencedBlock`.

**KnowledgePromptBuilder_BuildRelated_ChildrenAndSeeAlso_AreListedInSections**: A card with a child and a related card
has both sections, a card with a child only has one, and a card with neither gives an empty text. This scenario is
tested by `KnowledgePromptBuilder_BuildRelated_ChildrenAndSeeAlso_AreListedInSections`.

**KnowledgePromptBuilder_BuildSearchResults_WithAndWithoutCards_AreListedInOrder**: Two results are written as two lines
with their line of introduction, no result as `No card matches.`, and a missing list is refused. This scenario is tested
by `KnowledgePromptBuilder_BuildSearchResults_WithAndWithoutCards_AreListedInOrder`.

**KnowledgePromptBuilder_BuildCard_RealCatalog_KeepsLinkTextAndIsRepeatable**: The card of the marks is written the same
twice, with its example in a fenced block and the identifier of a linked card, and the card of framed figures keeps the
link to its example as the name of the example and its card. This scenario is tested by
`KnowledgePromptBuilder_BuildCard_RealCatalog_KeepsLinkTextAndIsRepeatable`.

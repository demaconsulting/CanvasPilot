### KnowledgeSearch

#### Verification Approach

`KnowledgeSearch` is verified with unit tests in `KnowledgeSearchTests.cs`. The weights are checked on small catalogs
that the test makes by hand with a fake of `IKnowledgeCatalog`, so that each score can be worked out exactly. The
questions that an assistant asks are checked on the catalog of the application, so that the right cards are found.
Nothing is substituted in the unit itself.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A card that has the query as another name scores higher than a card that has it only in its summary; the scores are
  the sums of the weights.
- A plural word matches the word and the word matches the plural; a word of three letters keeps its `s`.
- A card that every word matches gets the bonus; a repeated word counts once.
- Example names, titles, tags, summaries, and identifiers score by their weights, a tag has no prefix match, and two
  letters are not a prefix.
- A query that is the identifier of a card scores that card highest, however the words are separated.
- Cards of the same score are ordered by the depth of their identifier and then by identifier; the same query gives the
  same list twice and in two searches.
- A blank query finds nothing, a missing query and a number of results below one are refused, and the number of results
  is respected.
- In the catalog of the application, each card is the first result for its own identifier, and the questions about
  gradients, class diagrams, framed figures, Gantt charts, marks, sequence diagrams, and where files go find the cards
  that answer them.

#### Test Scenarios

**KnowledgeSearch_Search_ExactAliasOutranksSummaryHit**: A card with the other name `foo` scores 190, and a card that
mentions `foo` in its summary scores 60. This scenario is tested by
`KnowledgeSearch_Search_ExactAliasOutranksSummaryHit`.

**KnowledgeSearch_Search_PluralAndSingularWords_MatchEachOther**: The queries `diagrams` and `diagram` find cards whose
title has the other form, with a score of 80 each. This scenario is tested by
`KnowledgeSearch_Search_PluralAndSingularWords_MatchEachOther`.

**KnowledgeSearch_Search_ShortTokensKeepTheirS**: The query `svg` finds the card that has `svg` and not the card that
has `sv`. This scenario is tested by `KnowledgeSearch_Search_ShortTokensKeepTheirS`.

**KnowledgeSearch_Search_CardThatEveryTokenMatchesGetsTheBonus**: The query `red blue` scores 110 for a card whose title
has both words and 40 for a card that has the other name `red`. This scenario is tested by
`KnowledgeSearch_Search_CardThatEveryTokenMatchesGetsTheBonus`.

**KnowledgeSearch_Search_RepeatedWordsInTheQuery_CountOnce**: A query that repeats a word in other cases scores the same
as the word once. This scenario is tested by `KnowledgeSearch_Search_RepeatedWordsInTheQuery_CountOnce`.

**KnowledgeSearch_Search_ExampleNameHit_FindsTheCard**: The word of the name of an example scores 75, its start 62, and
two letters nothing. This scenario is tested by `KnowledgeSearch_Search_ExampleNameHit_FindsTheCard`.

**KnowledgeSearch_Search_FieldWeights_AreAsSpecified**: A title prefix scores 65, a tag 70, a tag prefix nothing, a
summary prefix 55, and a name 190. This scenario is tested by `KnowledgeSearch_Search_FieldWeights_AreAsSpecified`.

**KnowledgeSearch_Search_WholeIdentifierAsQuery_GetsTheWholeQueryScore**: The identifier of a card, written with a slash
or with spaces and in another case, scores 270 for that card and 40 for its parent. This scenario is tested by
`KnowledgeSearch_Search_WholeIdentifierAsQuery_GetsTheWholeQueryScore`.

**KnowledgeSearch_Search_Ties_AreOrderedByDepthThenIdentifier**: Four cards of the same score come as the two top cards
in identifier order and then the two cards one level down in identifier order. This scenario is tested by
`KnowledgeSearch_Search_Ties_AreOrderedByDepthThenIdentifier`.

**KnowledgeSearch_Search_BlankNullAndBadMaxResults_AreHandled**: Blank queries give an empty list, and a missing query,
a number of results of zero or less, and a missing catalog are refused. This scenario is tested by
`KnowledgeSearch_Search_BlankNullAndBadMaxResults_AreHandled`.

**KnowledgeSearch_Search_MaxResults_LimitsTheListToTheBestCards**: Of ten cards that match, two are returned with the
best first, eight by default, and ten when many are asked for. This scenario is tested by
`KnowledgeSearch_Search_MaxResults_LimitsTheListToTheBestCards`.

**KnowledgeSearch_Search_SameQueryTwice_GivesTheSameResults**: One query on the catalog of the application gives equal
lists twice in one search and in another search. This scenario is tested by
`KnowledgeSearch_Search_SameQueryTwice_GivesTheSameResults`.

**KnowledgeSearch_Search_IdentifierOfEveryCard_FindsThatCardFirst**: In the catalog of the application, the identifier
of each card gives that card as the first result. This scenario is tested by
`KnowledgeSearch_Search_IdentifierOfEveryCard_FindsThatCardFirst`.

**KnowledgeSearch_Search_RealCatalog_FindsTheCardsForCommonQuestions**: The questions about gradients, class diagrams,
framed callouts, Gantt charts, marks, sequences, and where files go find the cards of the style, the two class diagrams,
the framed figure, the Gantt chart, the marks, the two sequence diagrams, and the layout of a project. This scenario is
tested by `KnowledgeSearch_Search_RealCatalog_FindsTheCardsForCommonQuestions`.

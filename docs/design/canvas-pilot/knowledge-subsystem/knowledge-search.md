### KnowledgeSearch

![Knowledge Structure](KnowledgeSubsystemView.svg)

#### Purpose

`KnowledgeSearch` finds the cards of a knowledge catalog that fit some words, so that a user or an assistant can ask for
what they want to do without knowing the name of the card. The answer is the same for the same words and the same cards.

#### Data Model

- `KnowledgeSearchResult`: `Id`, `Title`, `Summary`, and `Score` (always positive).
- The search holds an index that it builds from `IKnowledgeCatalog.Cards` when it is created: for each card the tokens
  of its other names, identifier, example names, title, tags, and summary, the number of slashes in the identifier, and
  the forms of its name, other names, and identifier that a whole query can equal. The index does not change.
- A token is a run of letters and digits in lower case, with one trailing `s` removed from a token of more than three
  characters, so that the plural of a word matches the word. The same rule cuts the query and the cards.
- The weights, which are constants of the unit, are fixed:

  | Field | Exact | Prefix |
  | --- | --- | --- |
  | Other name | 40 | 20 |
  | Identifier | 40 | 20 |
  | Title | 30 | 15 |
  | Example name | 25 | 12 |
  | Tag | 20 | not matched |
  | Summary | 10 | 5 |

  A token of the query scores the exact weight when it is a token of the field. It scores the prefix weight when it
  starts a longer token of the field and has three or more characters.

  A card gets 50 more when every token of the query matches it, and 100 more, once, when its name, one of its other
  names, or its identifier is the whole query, written as typed, with the words joined by dashes, or with the words
  joined by slashes.

#### Key Methods

**Search(query, maxResults)**: cuts the query into tokens without repeats, scores every card, and returns the cards with
a score above zero ordered by score (highest first), then by the number of slashes in the identifier (fewest first),
then by identifier in ordinal order, limited to `maxResults` (default 8). A token is scored once, by the field with the
best score, so one field cannot count twice and a word that is in the title and in the summary counts as a title word. A
query without a token gives an empty list.

#### Error Handling

A missing query is an `ArgumentNullException` and a number of results below one an `ArgumentOutOfRangeException`. A
missing catalog is an `ArgumentNullException` at creation. The unit performs no I/O and logs nothing, and it is safe for
concurrent use, because the index is not changed after it is built.

#### Dependencies

- **KnowledgeCatalog** unit: the cards, through `IKnowledgeCatalog`.

#### Callers

- The tool for an assistant uses the search.

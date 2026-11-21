### KnowledgeTools

![Knowledge Structure](KnowledgeSubsystemView.svg)

#### Purpose

`KnowledgeTools` describes the tool that gives an assistant the drawing knowledge. The assistant is given the compact
index of the cards in its prompt and calls the tool for the cards, examples, and searches it needs; this unit says what
the tool is called, how to call it, and what it answers. The description is plain values, so it can be handed to the
tool mechanism of any provider of a language model that is chosen later.

#### Data Model

- `KnowledgeTool` is a record of the name, the description, the JSON Schema of the arguments as a JSON text, and a
  function `Invoke` that takes the arguments as a JSON text and returns the answer as text.
- The tool is `canvasnet_help` (`HelpToolName`). Its arguments are three optional strings: `topic` (a card by
  identifier, path, or other name), `example` (the name of an example, or `card-id#name`), and `query` (words to search
  for). The schema allows no other argument; an argument that is not in the schema is ignored when the tool is called,
  so a provider that adds one does not cause an error.
- The unit holds no state of its own: the function keeps the catalog, the search, and the writer of texts that it was
  made with, none of which changes.

#### Key Methods

**CreateHelpTool(catalog, search, prompts)**: makes the tool from the three collaborators that it answers with. A
missing collaborator is refused.

**CreateAll(catalog)**: makes the search and the writer of texts from the catalog and returns every tool; today only
`canvasnet_help`. A missing catalog is refused.

**Invoke(argumentsJson)**: reads the arguments and answers, in this order of precedence:

1. No arguments (nothing, white space, `{}`, `null`, or only empty values): the index of the cards.
2. `example`, with the card that `topic` names when there is one: the example, written by
   `KnowledgePromptBuilder.BuildExample`, when exactly one card in the scope has an example of that name; when several
   do, the line `Several cards have an example named 'x'. Ask again with one of:` and a line
   `- card-id#name: description` for each, which the assistant repeats as `example`; when none does, the message for an
   unknown example.
3. `topic`: the card, written by `BuildCard` with the list of its examples, and when the card has examples a last line
   that says how to read the text of one; when no card has that identifier, path, or name, the message for an unknown
   topic.
4. `query`: the cards that the search finds, written by `BuildSearchResults`, or a message that none matches.

The message for an unknown topic or example says what was not found (`No card matches 'x'.` or `No example is named
'x'.`), then `Closest cards:` and the five cards that the search finds for its words, and ends with
`Call with no arguments for the full index.`; when the search finds nothing the message is the first sentence and the
last. What is repeated from the arguments is cut at 80 characters.

#### Error Handling

The function does not throw. Text that is not JSON, JSON that is not an object, and a value that is not a string are
answered with a message that starts with `Error:`, says what is wrong, and names the three arguments, so the assistant
can send a correct call. A failure of the catalog while answering is caught and answered with a message that names the
kind of the exception. Only the call that makes the tool refuses a missing collaborator, with an
`ArgumentNullException`. The unit performs no I/O and logs nothing, and is safe for concurrent use.

#### Dependencies

- **KnowledgeCatalog** unit: the cards, their paths, and their examples, through `IKnowledgeCatalog`.
- **KnowledgeSearch** unit: the cards that fit the words of a query, and the suggestions for an unknown topic.
- **KnowledgePromptBuilder** unit: the text of the index, a card, an example, and the results of a search.

#### Callers

- The assistant of a later step hands the tool to its provider and calls `Invoke` with the arguments that the model
  sends.

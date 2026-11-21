### KnowledgeTools

#### Verification Approach

`KnowledgeTools` is verified with unit tests in `KnowledgeToolsTests.cs`. The exact answers of each mode are checked on
a small catalog that the tests make by hand with a fake of `IKnowledgeCatalog`, so that the whole text can be compared
with what `KnowledgePromptBuilder` writes for the same cards. One test calls the tool made by `CreateAll` over the
catalog of the application. Nothing is substituted in the unit itself.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The tool is named `canvasnet_help`, its description says how to call it, and its schema is JSON that has the three
  string arguments `topic`, `example`, and `query` and allows no other.
- A call with nothing, white space, `{}`, `null`, empty values, or an unknown argument is answered with the index.
- A topic is answered with the card by identifier, by a path with a slash or with a space, by an other name, and
  ignoring case, with a line about the examples only when the card has examples.
- An example that one card has is answered with its fenced text; an example that several cards have is answered with the
  choices as `card-id#name`, and each choice, the scope of a topic, and a card above the example give that example.
- A query is answered with the cards that match, best first, or with a message that none matches.
- A topic or an example that nothing has is answered with the closest cards and the hint for the index, or with the hint
  only when there are no close cards; a long argument is repeated cut.
- Bad arguments and a failing catalog are answered with a message, and nothing is thrown.

#### Test Scenarios

**KnowledgeTools_CreateHelpTool_Descriptor_NamesTheToolAndItsThreeArguments**: The name, the description, and the schema
of the tool are as described, and every missing collaborator is refused. This scenario is tested by
`KnowledgeTools_CreateHelpTool_Descriptor_NamesTheToolAndItsThreeArguments`.

**KnowledgeTools_Invoke_NoArguments_AnswersTheIndex**: Seven ways of calling without arguments are each answered with
the index. This scenario is tested by `KnowledgeTools_Invoke_NoArguments_AnswersTheIndex`.

**KnowledgeTools_Invoke_Topic_AnswersTheCardWithAHintForItsExamples**: A card is found by identifier, upper case, other
name, and with white space around it, and its text is the card as written for a prompt followed by the hint. This
scenario is tested by `KnowledgeTools_Invoke_Topic_AnswersTheCardWithAHintForItsExamples`.

**KnowledgeTools_Invoke_Topic_PathWithSpaceAndCardWithoutExamples_AreFound**: A card below another is found with a slash
and with a space, and a card without examples has no hint. This scenario is tested by
`KnowledgeTools_Invoke_Topic_PathWithSpaceAndCardWithoutExamples_AreFound`.

**KnowledgeTools_Invoke_Example_Single_AnswersTheFencedText**: An example of one card is answered with its text in a
fenced block. This scenario is tested by `KnowledgeTools_Invoke_Example_Single_AnswersTheFencedText`.

**KnowledgeTools_Invoke_Example_Ambiguous_ListsChoicesAndCanBeScopedByTopicOrId**: An example that two cards have is
answered with both choices; the topic of a card, `card-id#name`, and the topic of a card above give that example. This
scenario is tested by `KnowledgeTools_Invoke_Example_Ambiguous_ListsChoicesAndCanBeScopedByTopicOrId`.

**KnowledgeTools_Invoke_Example_Unknown_SuggestsCards**: An example name that no card has is answered with the closest
cards, with the hint only when none is close, and with the message for an unknown topic when the card does not exist.
This scenario is tested by `KnowledgeTools_Invoke_Example_Unknown_SuggestsCards`.

**KnowledgeTools_Invoke_Query_AnswersTheSearchResults**: A query is answered with the result list, a query that matches
nothing with the message, and a null topic does not hide the query. This scenario is tested by
`KnowledgeTools_Invoke_Query_AnswersTheSearchResults`.

**KnowledgeTools_Invoke_UnknownTopic_SuggestsTheClosestCards**: A topic that is a part of an identifier gives the cards
that fit it; a word that no card has, and a topic with no word in it, give the hint only. This scenario is tested by
`KnowledgeTools_Invoke_UnknownTopic_SuggestsTheClosestCards`.

**KnowledgeTools_Invoke_SeveralArguments_ExampleThenTopicThenQuery**: With an example, a topic, and a query together the
example is answered; with a topic and a query the card is. This scenario is tested by
`KnowledgeTools_Invoke_SeveralArguments_ExampleThenTopicThenQuery`.

**KnowledgeTools_Invoke_BadArguments_AnswerAnErrorThatNamesTheArguments**: Eleven inputs that are not JSON, not an
object, or have a value of the wrong type are each answered with a message that starts with `Error:` and names the three
arguments. This scenario is tested by `KnowledgeTools_Invoke_BadArguments_AnswerAnErrorThatNamesTheArguments`.

**KnowledgeTools_Invoke_UnusualArguments_AreAnsweredAndNeverThrow**: An argument of 5,000 characters is repeated cut to
80 characters, a nest of 5,000 arrays is an error, and an argument with control characters and a surrogate pair is
answered. This scenario is tested by `KnowledgeTools_Invoke_UnusualArguments_AreAnsweredAndNeverThrow`.

**KnowledgeTools_Invoke_CatalogThatFails_AnswersAnError**: A catalog whose lookups throw is answered with a message that
names the kind of exception. This scenario is tested by `KnowledgeTools_Invoke_CatalogThatFails_AnswersAnError`.

**KnowledgeTools_CreateAll_RealCatalog_AnswersIndexTopicExampleAndQuery**: The tool made over the catalog of the
application answers the index, a card found by a mixed-case path with a space, an example named as `card-id#name`, the
choices for an example that several cards have, a card of CanvasPilot, and a search for a Gantt chart that finds the
card of Gantt charts first. This scenario is tested by
`KnowledgeTools_CreateAll_RealCatalog_AnswersIndexTopicExampleAndQuery`.

### KnowledgeSkills

#### Verification Approach

`KnowledgeSkills` is verified with unit tests in `KnowledgeSkillsTests.cs`. The exact skills are checked on a small
catalog that the tests make by hand with a fake of `IKnowledgeCatalog`, with a wrapper that counts reads to prove that
the skills are made as they are read, and on the catalog of the application to prove that there is one skill for each
card and that no two names are the same. Nothing is substituted in the unit itself.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The name of a skill is `canvasnet-` and the identifier with slashes written as dashes, and a missing identifier is
  refused.
- A catalog of three cards has three skills in the order of the cards with the title and summary as the description and
  the card with its examples as the content, which holds the body and each example in a fenced block.
- Nothing of the cards is built when the skills are asked for, reading one skill builds one card, and a missing catalog
  is refused at the call.
- A catalog without cards has no skill.
- The catalog of the application has as many skills as cards, all names are different and valid, and each skill has the
  description and the content of its card with the text of every example.

#### Test Scenarios

**KnowledgeSkills_NameFor_CardId_IsPrefixAndIdWithDashes**: The names for a card at the top, a card below another, a
card of CanvasPilot, and a card with a dash in its name are as described, and a missing identifier is refused. This
scenario is tested by `KnowledgeSkills_NameFor_CardId_IsPrefixAndIdWithDashes`.

**KnowledgeSkills_Create_SmallCatalog_IsOneSkillPerCardWithNameDescriptionAndContent**: Three cards give three skills in
order, with the names, the descriptions, and the content that `KnowledgePromptBuilder` writes for each card with its
examples. This scenario is tested by
`KnowledgeSkills_Create_SmallCatalog_IsOneSkillPerCardWithNameDescriptionAndContent`.

**KnowledgeSkills_Create_SmallCatalog_ContentHasBodyAndFencedExamples**: The content of the first skill starts with the
heading, has the body, the list of examples, and the text of a snippet and of a command in fenced blocks of their
languages. This scenario is tested by `KnowledgeSkills_Create_SmallCatalog_ContentHasBodyAndFencedExamples`.

**KnowledgeSkills_Create_Enumeration_IsDeferredAndANullCatalogIsRefusedAtTheCall**: With a catalog that counts reads,
asking for the skills reads nothing, taking the first skill reads the cards below one card, reading all of them reads
those of every card again, and a missing catalog is refused without reading the result. This scenario is tested by
`KnowledgeSkills_Create_Enumeration_IsDeferredAndANullCatalogIsRefusedAtTheCall`.

**KnowledgeSkills_Create_EmptyCatalog_IsEmpty**: A catalog without cards has no skill. This scenario is tested by
`KnowledgeSkills_Create_EmptyCatalog_IsEmpty`.

**KnowledgeSkills_Create_RealCatalog_HasOneUniqueSkillForEveryCard**: The catalog of the application gives one skill for
each card, in order, with unique names made of lower case letters, digits, and single dashes, the names of the cards of
framed figures and of the marks, the title and summary as each description, and as each content the heading and
identifier of the card and the text of every example. This scenario is tested by
`KnowledgeSkills_Create_RealCatalog_HasOneUniqueSkillForEveryCard`.

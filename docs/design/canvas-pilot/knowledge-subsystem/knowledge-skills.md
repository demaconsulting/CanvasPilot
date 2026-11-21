### KnowledgeSkills

![Knowledge Structure](KnowledgeSubsystemView.svg)

#### Purpose

`KnowledgeSkills` describes the cards of a catalog as skills for an assistant: one value for each card, with a name, a
short description that says what it is about, and the full text. An assistant or a provider that works with skills, or
with one tool for each topic, can be given the knowledge card by card. The values are plain, so they can be handed to
any provider of a language model that is chosen later.

#### Data Model

- `KnowledgeSkill` is a record of the `Name`, the `Description`, and the `Content`.
- The name is `canvasnet-` (`NamePrefix`) followed by the identifier of the card with each `/` written as `-`, for the
  cards of CanvasPilot as well as for those of CanvasNet: `svg/framed-figure` is `canvasnet-svg-framed-figure` and
  `canvaspilot/marks` is `canvasnet-canvaspilot-marks`.
- The description is the title of the card, `-`, and its summary.
- The content is the text that `KnowledgePromptBuilder.BuildCard` writes for the card with its examples asked for: the
  card as the assistant reads it, followed by the text of each example in a fenced block.
- The unit holds no state.

#### Key Methods

**NameFor(cardId)**: the name of the skill of a card. A missing identifier is refused.

**Create(catalog)**: one skill for each card of the catalog, in the order of its cards. The text of all the cards is
many times what should be built up front, so the result is made as it is read: asking for the skills reads nothing,
reading one skill builds the text of one card, and every read of the result starts again. A missing catalog is refused
when `Create` is called, not when the result is read, which is why `Create` hands the work to a second method that makes
the skills.

Two identifiers could in theory give the same name, such as `a/b-c` and `a-b/c`. The unit does not rename them; the
tests check that the cards of the catalog of the application give different names, so a new card that would collide
fails the build and is renamed in its source.

#### Error Handling

A missing identifier or catalog is an `ArgumentNullException`. The unit performs no I/O and logs nothing, and is safe
for concurrent use.

#### Dependencies

- **KnowledgeCatalog** unit: the cards, through `IKnowledgeCatalog`.
- **KnowledgePromptBuilder** unit: the text of a card with its examples.

#### Callers

- The assistant of a later step hands the skills to its provider, or turns each into a tool.

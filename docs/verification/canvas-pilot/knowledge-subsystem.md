## Knowledge

### Verification Approach

The `Knowledge` subsystem is verified by integration tests defined in `KnowledgeSubsystemTests.cs`. The tests use the
real catalog of the application, with the help cards of CanvasNet, the cards of CanvasPilot that are embedded in the
application, and the filter that leaves out the cards about what the host program controls, the way the later units and
the assistant will use it. They check the observable result: which cards are offered, that the cards of CanvasPilot are
last, that no card of the left-out tag is present, that the catalog is one value that many readers share, and that the
application library refers to no package of a language model or agent library. The subsystem has no dependency on the
user interface, so no substitute is needed.

### Test Environment

N/A - standard test environment. The tests read embedded resources and the dependency file of the test project, and
the project file of the application library from the repository; no network, display, or special permission is needed.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- The catalog has a card for every drawing format of CanvasNet and the cards of CanvasPilot as its last tree, and no
  card of it carries the tag that is left out.
- Many threads that ask for the default catalog get the same one.
- The search of the catalog of the application finds the card of a format, the cards of CanvasPilot, and nothing for a
  word that no card has.
- The index of the catalog of the application is within its budget, lists the cards of both sources, and the text of the
  card that a search found holds its example.
- The tool of the application answers the index, a card, an example, and a search from the catalog of the
  application, and answers bad arguments with a message.
- The skills of the application have one skill for each card, with different names that start with `canvasnet-`,
  including the cards of CanvasPilot and none for a card that is left out.
- The cards of the application are read into blocks that a window can draw, with headings, list items, and code in
  them.
- No assembly that the application library refers to, no package that the test project resolves, and no package that
  the project file of the application library declares is a language model or agent library.

### Test Scenarios

**KnowledgeSubsystem_Default_OffersDrawingCardsOfCanvasNetAndCanvasPilotWithoutHostingCards**: The roots of the catalog
hold the cards for SVG, Mermaid, PlantUML, OpenSCAD, Vega-Lite, and composition, and the tree of CanvasPilot as the last
root; the cards that carry the left-out tag, and the cards of files and of SVG options that carry it, are absent. This
scenario is tested by `KnowledgeSubsystem_Default_OffersDrawingCardsOfCanvasNetAndCanvasPilotWithoutHostingCards`.

**KnowledgeSubsystem_Default_IsOneSharedCatalogReadableFromManyThreads**: Eight tasks ask for the default catalog at the
same time and all get the same object that a later request gets. This scenario is tested by
`KnowledgeSubsystem_Default_IsOneSharedCatalogReadableFromManyThreads`.

**KnowledgeSubsystem_Search_QuestionsAboutDrawing_FindTheCardsThatAnswerThem**: The search of the catalog of the
application answers `gantt chart` with the card of the Gantt chart first, a question about reading the marks of a
message with the card of the marks, and `project folder layout` with the card of the layout of a project first, and a
nonsense word with nothing. This scenario is tested by
`KnowledgeSubsystem_Search_QuestionsAboutDrawing_FindTheCardsThatAnswerThem`.

**KnowledgeSubsystem_PromptBuilder_IndexSearchAndCard_AreReadyForAPrompt**: The index of the catalog of the application
is within its budget and has the lines of a card of CanvasNet and a card of CanvasPilot, the list of the results of a
search for a framed figure with a callout starts with the card of framed figures, and the text of that card with its
examples holds the text of its first example in a fenced block of its language. This scenario is tested by
`KnowledgeSubsystem_PromptBuilder_IndexSearchAndCard_AreReadyForAPrompt`.

**KnowledgeSubsystem_Tools_HelpTool_AnswersFromTheApplicationCatalog**: The tool made for the catalog of the
application, called by the JSON text that an assistant sends, answers the index, the card of the layout of a project,
the text of an example of the card of framed figures, and a search for a Gantt chart, and answers a topic that does not
exist with the closest cards and text that is not JSON with an error message. This scenario is tested by
`KnowledgeSubsystem_Tools_HelpTool_AnswersFromTheApplicationCatalog`.

**KnowledgeSubsystem_Skills_ApplicationCatalog_HasOneSkillPerCardWithoutHostingCards**: The skills of the catalog of the
application are as many as its cards, have different names that start with `canvasnet-`, include the skills of the
cards of CanvasPilot, have none for a card of the left-out tag, and have the text of the card of the marks of a message
with its example as the content of its skill. This scenario is tested by
`KnowledgeSubsystem_Skills_ApplicationCatalog_HasOneSkillPerCardWithoutHostingCards`.

**KnowledgeSubsystem_Markdown_CardOfTheApplication_IsReadIntoBlocksForAWindow**: The body of the card of the marks of a
message, which is a card of CanvasPilot, and the body of the card of framed figures, which is a card of CanvasNet, are
each read into blocks with a heading, list items or paragraphs, and runs of code text, and no block is empty. This
scenario is tested by `KnowledgeSubsystem_Markdown_CardOfTheApplication_IsReadIntoBlocksForAWindow`.

**KnowledgeSubsystem_Assembly_ReferencesNoLanguageModelPackage**: The assemblies that the application library refers
to, the packages in the dependency file of the test project, and the package references in the project file of the
application library do not include the SDKs of GitHub Copilot, OllamaSharp, the AI extensions of Microsoft, AgentKit, or
the other libraries of language models that are listed. This scenario is tested by
`KnowledgeSubsystem_Assembly_ReferencesNoLanguageModelPackage`.

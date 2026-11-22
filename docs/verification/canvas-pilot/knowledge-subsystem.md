## Knowledge

### Verification Approach

The `Knowledge` subsystem is verified by integration tests defined in `KnowledgeSubsystemTests.cs`. The tests use the
real catalog of the application, with the help cards of CanvasNet, the cards of CanvasPilot that are embedded in the
application, and the list that leaves out the cards that are only about the host program, the way the later units and
the assistant will use it. They check the observable result: which cards are offered, that the cards of CanvasPilot are
last, that no card that is only about the host program is present and the cards for embedding pages and files are, that
the catalog is one value that many readers share, and that the application library refers to no package of a language
model or agent library. The Knowledge subsystem has one unit with a window, the browser, whose view model is exercised
here without a window and with a clipboard that records; the window itself is verified by the headless tests of that
unit.

### Test Environment

N/A - standard test environment. The tests read embedded resources and the dependency file of the test project, and
the project file of the application library from the repository; no network, display, or special permission is needed.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- The catalog has a card for every drawing format of CanvasNet and the cards of CanvasPilot as its last tree, has the
  cards for embedding pages, pictures, and files, and has none of the cards that are only about the host program.
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
- The browser over the catalog of the application shows the tree of its roots, finds a card by search, shows a card with
  its text and related cards, and copies an example to the clipboard that it was given.
- No type of the subsystem uses a type of a language model or agent library in its signatures, and no source file of
  the subsystem has a using directive for such a library.

### Test Scenarios

**KnowledgeSubsystem_Default_OffersDrawingCardsOfCanvasNetAndCanvasPilotWithoutHostOnlyCards**: The roots of the catalog
hold the cards for SVG, Mermaid, PlantUML, OpenSCAD, Vega-Lite, and composition, and the tree of CanvasPilot as the last
root; the cards for files, PDF, PowerPoint, Visio, raster pictures, views of a model, and params of a chart are offered;
the cards of errors, SVG options, and flattening, which are only about the host program, are absent. This
scenario is tested by `KnowledgeSubsystem_Default_OffersDrawingCardsOfCanvasNetAndCanvasPilotWithoutHostOnlyCards`.

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

**KnowledgeSubsystem_Skills_ApplicationCatalog_HasOneSkillPerCardWithoutHostOnlyCards**: The skills of the catalog of
the application are as many as its cards, have different names that start with `canvasnet-`, include the skills of the
cards of CanvasPilot and of the cards for files and PDF pages, have none for a card that is left out, and have the text
of the card of the marks of a message with its example as the content of its skill. This scenario is tested by
`KnowledgeSubsystem_Skills_ApplicationCatalog_HasOneSkillPerCardWithoutHostOnlyCards`.

**KnowledgeSubsystem_Markdown_CardOfTheApplication_IsReadIntoBlocksForAWindow**: The body of the card of the marks of a
message, which is a card of CanvasPilot, and the body of the card of framed figures, which is a card of CanvasNet, are
each read into blocks with a heading, list items or paragraphs, and runs of code text, and no block is empty. This
scenario is tested by `KnowledgeSubsystem_Markdown_CardOfTheApplication_IsReadIntoBlocksForAWindow`.

**KnowledgeSubsystem_Browser_ApplicationCatalog_ShowsTreeSearchCardsAndCopiesAnExample**: The browser over the catalog
of the application has a node for each root, finds the card of the marks of a message and a card for gradients by
search, shows the card of the marks with its text and its related cards as the catalog gives them, and puts the text of
its first example on a clipboard that records, with a status that names the example. This scenario is tested by
`KnowledgeSubsystem_Browser_ApplicationCatalog_ShowsTreeSearchCardsAndCopiesAnExample`.

**KnowledgeSubsystem_Types_ReferenceNoLanguageModelPackage**: The types of the subsystem, in their base types,
interfaces, fields, properties, events, constructors, and methods, use no type of the SDKs of GitHub Copilot,
OllamaSharp, the AI extensions of Microsoft, the Microsoft Agent Framework, AgentKit, or the other libraries of language
models that are listed, and no source file in the folder of the subsystem has a using directive for them. The
application library as a whole refers to the libraries of the AI copilot, so the rule is checked on the subsystem and
not on the assembly. This scenario is tested by `KnowledgeSubsystem_Types_ReferenceNoLanguageModelPackage`.

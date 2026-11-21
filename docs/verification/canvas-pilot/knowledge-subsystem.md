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

**KnowledgeSubsystem_Assembly_ReferencesNoLanguageModelPackage**: The assemblies that the application library refers
to, the packages in the dependency file of the test project, and the package references in the project file of the
application library do not include the SDKs of GitHub Copilot, OllamaSharp, the AI extensions of Microsoft, AgentKit, or
the other libraries of language models that are listed. This scenario is tested by
`KnowledgeSubsystem_Assembly_ReferencesNoLanguageModelPackage`.

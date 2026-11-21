### KnowledgeCatalog

#### Verification Approach

`KnowledgeCatalog` is verified with unit tests in `KnowledgeCatalogTests.cs` on the catalog of the application, which is
built from the real help cards of CanvasNet and the real cards of CanvasPilot, because the cards are the content that is
verified. Small catalogs for the units that use the catalog are made with a fake of `IKnowledgeCatalog`, not with this
unit. The tests compare the examples with the library's own, compare the statements of the cards of CanvasPilot with the
constants of the application, and list the embedded card files to prove that each is a card.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The seven cards that are only about the host program are the left-out ones, they are absent and cannot be found, every
  other card of CanvasNet is offered (including the cards that carry the tag `hosting` and that a drawing needs), and
  the cards below a left-out card are roots.
- No child, related card, parent, or code-quoted link of any card names a card that is left out, and every card can be
  reached from the roots once.
- The root card of CanvasPilot says that command-line tools and the host program cannot be run and names the tools to
  use.
- The six cards of CanvasPilot are exactly the embedded card files, they are the last tree, and every card is complete.
- The card about style kits quotes the names that the code uses for the tools, the folder, the style file, and the
  sections.
- A card is found by path, by space-separated path, by another name, and in any case; a missing, unknown, or left-out
  path finds nothing.
- No body holds a link of the CanvasNet syntax outside code; a link to a left-out card is the title of the card.
- Examples are found by name, by scope, and by card prefix; the examples of the cards of CanvasNet are the library's,
  and those of the cards of CanvasPilot are the embedded files.
- The statements of the cards of CanvasPilot agree with the constants of the application.
- The types that the unit offers mention no type of CanvasNet, and its lists cannot be changed.

#### Test Scenarios

**KnowledgeCatalog_Default_HostOnlyCardsAreAbsentAndAllOtherCardsRemain**: The left-out identifiers are the seven of the
design, they are not in the catalog and cannot be found, every other card of the library is in the catalog, and the
number of cards is the number of the library minus the left-out ones plus the cards of CanvasPilot. This scenario is
tested by `KnowledgeCatalog_Default_HostOnlyCardsAreAbsentAndAllOtherCardsRemain`.

**KnowledgeCatalog_Default_DrawingCardsThatCarryTheHostingTagRemain**: The cards for files, PDF, PowerPoint, Visio,
raster pictures, views of a model, params of a chart, and positions are offered and are found by identifier and by
their other names. This scenario is tested by `KnowledgeCatalog_Default_DrawingCardsThatCarryTheHostingTagRemain`.

**KnowledgeCatalog_Default_NoReferenceNamesACardThatIsLeftOut**: Every child, related card, and parent of every card is
a card of the catalog, no body quotes a left-out card as an identifier, every child has its card as parent, and a walk
from the roots reaches each card once. This scenario is tested by
`KnowledgeCatalog_Default_NoReferenceNamesACardThatIsLeftOut`.

**KnowledgeCatalog_Default_RelatedCardThatIsLeftOutIsDropped**: Where the library lists the card of flattening as
related to the card of position bindings, and the card of errors as related to the card of files, the catalog drops
them and keeps the cards that are offered. This scenario is tested by
`KnowledgeCatalog_Default_RelatedCardThatIsLeftOutIsDropped`.

**KnowledgeCatalog_Default_RootCardOfCanvasPilotSaysWhatTheAssistantCannotRun**: The body of the root card names the
command-line tools, says that they cannot be run, and names the `canvasnet_help` tool and the file tools. This scenario
is tested by `KnowledgeCatalog_Default_RootCardOfCanvasPilotSaysWhatTheAssistantCannotRun`.

**KnowledgeCatalog_Default_VisibleCardsBelowAHiddenCardArePromotedToRoots**: The card of the codes of errors, whose
parent is left out, has no parent and is a root; a card below a card that is offered keeps its parent. This scenario is
tested by `KnowledgeCatalog_Default_VisibleCardsBelowAHiddenCardArePromotedToRoots`.

**KnowledgeCatalog_Create_UnknownExcludedCardId_ThrowsArgumentException**: An identifier that is not the identifier of a
card, or is missing, is refused. This scenario is tested by
`KnowledgeCatalog_Create_UnknownExcludedCardId_ThrowsArgumentException`.

**KnowledgeCatalog_Create_NullArguments_ThrowArgumentNullException**: Each argument of the builder is required. This
scenario is tested by `KnowledgeCatalog_Create_NullArguments_ThrowArgumentNullException`.

**KnowledgeCatalog_Default_HasTheCanvasPilotCardsAsOneTreeAfterTheLibraryRoots**: The identifiers of the cards of the
tree of CanvasPilot are the names of the embedded card files, its children are the files of its folder, it is the last
root, and its cards are the last cards. This scenario is tested by
`KnowledgeCatalog_Default_HasTheCanvasPilotCardsAsOneTreeAfterTheLibraryRoots`.

**KnowledgeCatalog_Default_EveryCardIsComplete**: Every card has a title, a summary, tags, and an identifier that ends
with its name, its children and related cards exist, and each card of CanvasPilot has a body and a scope tag. This
scenario is tested by `KnowledgeCatalog_Default_EveryCardIsComplete`.

**KnowledgeCatalog_Find_PathNameAliasAndCase_FindTheCard**: The card of framed figures is found by its path, by the path
with a space, by another name, and in upper case, and the project card by its other name. This scenario is tested by
`KnowledgeCatalog_Find_PathNameAliasAndCase_FindTheCard`.

**KnowledgeCatalog_Find_UnknownHiddenOrBlankPath_FindsNothing**: Unknown, left-out, and blank paths find nothing, and a
missing path is refused. This scenario is tested by `KnowledgeCatalog_Find_UnknownHiddenOrBlankPath_FindsNothing`.

**KnowledgeCatalog_GetChildrenAndGetSeeAlso_ReturnTheNamedCardsInOrder**: The children and related cards of the card of
the project are the cards of its identifiers in order, a leaf has no children, and a missing card is refused. This
scenario is tested by `KnowledgeCatalog_GetChildrenAndGetSeeAlso_ReturnTheNamedCardsInOrder`.

**KnowledgeCatalog_Default_BodiesHaveLinksWrittenAsCardIds**: The body of the card of the project links to a card as its
identifier in code quotes, the body of the card of the picture links to an example as its name and card, and no body
holds the bracket syntax outside code. This scenario is tested by
`KnowledgeCatalog_Default_BodiesHaveLinksWrittenAsCardIds`.

**KnowledgeCatalog_Default_LinksToHiddenCardsAreNotCardIds**: Where the library writes a link to the left-out card of
flattening, the catalog writes the title of the card, and the links to cards that are offered are identifiers. This
scenario is tested by `KnowledgeCatalog_Default_LinksToHiddenCardsAreNotCardIds`.

**KnowledgeCatalog_FindExamples_SharedNameCardPrefixAndScope_SelectTheExamples**: The name of the sequence diagram is
the name of an example of Mermaid and of PlantUML; both are found, a card prefix or a scope selects one, and the case
does not matter. This scenario is tested by
`KnowledgeCatalog_FindExamples_SharedNameCardPrefixAndScope_SelectTheExamples`.

**KnowledgeCatalog_FindExamples_UnknownNameOrCard_FindsNothing**: An unknown example, card prefix, or scope finds
nothing, an example outside the scope is not found, and a missing name is refused. This scenario is tested by
`KnowledgeCatalog_FindExamples_UnknownNameOrCard_FindsNothing`.

**KnowledgeCatalog_FindExamples_CanvasPilotExamples_AreTheEmbeddedFiles**: The two examples of CanvasPilot are found in
their cards, with their kind, language, and file name, and their content is that of the embedded files. This scenario is
tested by `KnowledgeCatalog_FindExamples_CanvasPilotExamples_AreTheEmbeddedFiles`.

**KnowledgeCatalog_Default_LibraryExamples_EqualTheLibraryFiles**: The examples of every card of CanvasNet that is
offered equal those of the library in name, kind, language, file, description, and content. This scenario is tested by
`KnowledgeCatalog_Default_LibraryExamples_EqualTheLibraryFiles`.

**KnowledgeCatalog_Default_CanvasPilotCards_AgreeWithTheApplication**: The cards of the project say the extension of the
project file, the name of the data folder, the name of the folder of attachments, every kind of mark, and the references
of a message as the constants and names of the application have them. This scenario is tested by
`KnowledgeCatalog_Default_CanvasPilotCards_AgreeWithTheApplication`.

**KnowledgeCatalog_Default_StylesCardQuotesTheNamesThatTheCodeUses**: The card `canvaspilot/styles` is a child of the
root card of CanvasPilot, the root card and the card about the layout point to it, and the card quotes, between back
ticks, the names of the two tools of the style, the name of the folder of the kits of a project and the name of the
style file and of the folder of examples, every section of the guidance, and the three cards of the formats that it is
related to, as the constants and lists of the code have them. This scenario is tested by
`KnowledgeCatalog_Default_StylesCardQuotesTheNamesThatTheCodeUses`.

**KnowledgeCatalog_Default_PublicTypesReferToNoHelpTypeOfCanvasNetAndCannotBeChanged**: No public member of the
interface, the catalog, the card, and the examples mentions a type of CanvasNet, and an add to the lists of cards,
roots, tags, children, and examples is refused. This scenario is tested by
`KnowledgeCatalog_Default_PublicTypesReferToNoHelpTypeOfCanvasNetAndCannotBeChanged`.

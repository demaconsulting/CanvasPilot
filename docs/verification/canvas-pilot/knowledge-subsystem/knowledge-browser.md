### KnowledgeBrowser

#### Verification Approach

`KnowledgeBrowser` is verified with unit tests in
`test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeBrowserTests.cs` for the view model and its nodes,
links, and examples, and with headless Avalonia UI tests in
`test/DemaConsulting.CanvasPilot.UiTests/KnowledgeBrowserViewTests.cs` for the window. The unit tests use a small
catalog that they make by hand (`FakeKnowledgeCatalog`) so that the tree, the links, and the search can be compared
exactly, a clipboard that records (`RecordingKnowledgeClipboard`) in place of the system clipboard, and, in one test,
the catalog of the application to show that every card can be shown. The UI tests show the real `KnowledgeBrowserView`
over the catalog of the application, choose nodes as a user does, press the real buttons with the mouse, and compare
what the window shows with what the view model and the catalog hold. Nothing is substituted in the unit itself.

#### Test Environment

The standard test runner, and for the window the headless Avalonia platform with the Skia back end and the application's
own `App`. No window is shown on a screen. The look of the window on a real screen (the proportions of the tree and the
card, and the selection of text with the mouse) and the system clipboard itself are outside what the headless tests can
see; they check what the window is given and what it shows.

#### Acceptance Criteria

- All unit tests and UI tests of the unit pass with zero failures.
- The tree has a node for each root and, below it, a node for each card below that card, in order, and a catalog that
  loops gives a tree that ends; nothing is shown of a card until one is chosen.
- A chosen card is shown with its title, summary, tags, other names, text drawn from its Markdown, the cards below it,
  its related cards, and its examples, however it was chosen: in the tree, from a hit, or from a link; the cards above
  it in the tree are open and its node is selected.
- A card that the catalog does not have changes nothing but the status.
- Two or more characters in the search box show the hits in place of the tree and fewer show the tree again; a search
  that finds nothing says so, and the card that is shown stays.
- Copy puts the text of the example on the clipboard and names it in the status; a missing clipboard, one that cannot be
  reached, and one that throws all give the status that the clipboard cannot be reached and no failure.
- Close closes the window; the window gives the view model the clipboard of the window when it has none.

#### Test Scenarios

**KnowledgeBrowserViewModel_Constructor_NullArguments_Throw**: A missing catalog, search, identifier, card, or node list
is refused. This scenario is tested by `KnowledgeBrowserViewModel_Constructor_NullArguments_Throw`.

**KnowledgeBrowserViewModel_Constructor_BuildsTreeFromRootsAndChildren**: The tree of the small catalog has its two
roots, their children in order, no node that is open, no card shown, and the tree and not hits. This scenario is tested
by `KnowledgeBrowserViewModel_Constructor_BuildsTreeFromRootsAndChildren`.

**KnowledgeBrowserViewModel_Constructor_CatalogThatLoops_BuildsATreeThatEnds**: Two cards that are the children of each
other make a tree of two nodes. This scenario is tested by
`KnowledgeBrowserViewModel_Constructor_CatalogThatLoops_BuildsATreeThatEnds`.

**KnowledgeBrowserViewModel_Select_Card_ShowsEverythingTheCardHolds**: Choosing the first card shows its title, summary,
tags, other names, blocks, cards below, related cards, and examples with their headers and descriptions, and tells the
window what changed. This scenario is tested by `KnowledgeBrowserViewModel_Select_Card_ShowsEverythingTheCardHolds`.

**KnowledgeBrowserViewModel_Select_PlainLeafCard_ShowsNoRelatedPartsAndHeaderWithoutFile**: A card with no tags, other
names, cards below, related cards, or examples shows none of them, and an example without a file has a header without
one. This scenario is tested by
`KnowledgeBrowserViewModel_Select_PlainLeafCard_ShowsNoRelatedPartsAndHeaderWithoutFile`.

**KnowledgeBrowserViewModel_Select_ChildCard_OpensAncestorsAndSelectsItsNode**: Choosing a card below a root opens the
root and selects the node of the card, and leaves the other root closed. This scenario is tested by
`KnowledgeBrowserViewModel_Select_ChildCard_OpensAncestorsAndSelectsItsNode`.

**KnowledgeBrowserViewModel_Select_AliasAndUnknownCard_FindsOrKeepsTheShownCard**: A card is found by an alias in
another case, and a card that is not there leaves the card that is shown and says so. This scenario is tested by
`KnowledgeBrowserViewModel_Select_AliasAndUnknownCard_FindsOrKeepsTheShownCard`.

**KnowledgeBrowserViewModel_SelectedNode_SetByTheTree_ShowsItsCardAndIgnoresNull**: A node that the tree selects shows
its card, and a cleared selection leaves it. This scenario is tested by
`KnowledgeBrowserViewModel_SelectedNode_SetByTheTree_ShowsItsCardAndIgnoresNull`.

**KnowledgeBrowserViewModel_Select_CardThatIsShown_ReadsItAgainAndClearsStatus**: Choosing the card that is shown again
clears the status of a failed copy. This scenario is tested by
`KnowledgeBrowserViewModel_Select_CardThatIsShown_ReadsItAgainAndClearsStatus`.

**KnowledgeBrowserViewModel_NavigateCommand_LinkOfTheCard_ShowsThatCard**: A related card and a card below are followed
with their links, which also open the tree, and the command with no identifier does nothing. This scenario is tested by
`KnowledgeBrowserViewModel_NavigateCommand_LinkOfTheCard_ShowsThatCard`.

**KnowledgeBrowserViewModel_SearchText_TwoOrMoreCharacters_ShowsHitsInPlaceOfTree**: A word with white space around it
shows the hits, best first, in place of the tree and tells the window. This scenario is tested by
`KnowledgeBrowserViewModel_SearchText_TwoOrMoreCharacters_ShowsHitsInPlaceOfTree`.

**KnowledgeBrowserViewModel_SearchText_ShortOrEmpty_RestoresTreeAndNoHitIsReported**: One character, white space, and an
empty or missing text show the tree again, and a word that no card has is a search with no hit. This scenario is tested
by `KnowledgeBrowserViewModel_SearchText_ShortOrEmpty_RestoresTreeAndNoHitIsReported`.

**KnowledgeBrowserViewModel_SearchText_HitChosen_ShowsCardAndKeepsHits**: Typing leaves the card that is shown, and
going to a hit shows its card and keeps the hits. This scenario is tested by
`KnowledgeBrowserViewModel_SearchText_HitChosen_ShowsCardAndKeepsHits`.

**KnowledgeBrowserViewModel_CopyExampleCommand_Success_PutsContentOnClipboardAndSaysSo**: The second example is put on
the recording clipboard, and the status names it. This scenario is tested by
`KnowledgeBrowserViewModel_CopyExampleCommand_Success_PutsContentOnClipboardAndSaysSo`.

**KnowledgeBrowserViewModel_CopyExampleCommand_ClipboardOutOfReach_SaysSoAndDoesNotThrow**: With no clipboard, with one
that fails, and with one that throws, the status says that the clipboard cannot be reached. This scenario is tested by
`KnowledgeBrowserViewModel_CopyExampleCommand_ClipboardOutOfReach_SaysSoAndDoesNotThrow`.

**KnowledgeBrowserViewModel_Commands_NoExampleAndClose_DoNothingAndRaiseCloseRequested**: Copying no example copies
nothing, and Close raises one request. This scenario is tested by
`KnowledgeBrowserViewModel_Commands_NoExampleAndClose_DoNothingAndRaiseCloseRequested`.

**KnowledgeBrowserViewModel_Select_EveryCardOfTheApplication_ShowsItsTextAndSearchFindsCards**: Every card of the
catalog of the application can be shown, the roots are the nodes, a search finds the card of the marks of a message, and
showing that card opens its root and shows text and examples. This scenario is tested by
`KnowledgeBrowserViewModel_Select_EveryCardOfTheApplication_ShowsItsTextAndSearchFindsCards`.

**KnowledgeBrowserView_Shown_ShowsRootsInTreeAndNoCard**: The window shows a node for each root, the note that asks the
user to choose a card, and no card. This scenario is tested by `KnowledgeBrowserView_Shown_ShowsRootsInTreeAndNoCard`.

**KnowledgeBrowserView_NodeChosen_ShowsTitleSummaryTagsAndBodyDrawnFromMarkdown**: Choosing a node in the tree shows the
title, summary, tags, other names, and a body that has a control for each block, with runs of code text and headings.
This scenario is tested by `KnowledgeBrowserView_NodeChosen_ShowsTitleSummaryTagsAndBodyDrawnFromMarkdown`.

**KnowledgeBrowserView_MarkdownBlocksPanel_DrawsEveryKindOfBlock**: A heading, a paragraph with bold, italic, and code
runs, a bullet, a number, and a block of code are each drawn as their kind needs, and the panel follows a change of the
blocks and an empty list. This scenario is tested by `KnowledgeBrowserView_MarkdownBlocksPanel_DrawsEveryKindOfBlock`.

**KnowledgeBrowserView_SeeAlsoAndChildButtons_GoToTheirCardsAndSelectTheNode**: The buttons for the related cards and
the cards below are as many as the card has; pressing them shows their cards and the tree selects the node. This
scenario is tested by `KnowledgeBrowserView_SeeAlsoAndChildButtons_GoToTheirCardsAndSelectTheNode`.

**KnowledgeBrowserView_SearchBox_TextTyped_ShowsHitsInPlaceOfTreeAndHitOpensCard**: Typing a word shows a button for
each hit in place of the tree, pressing the first shows its card, and clearing the box shows the tree. This scenario is
tested by `KnowledgeBrowserView_SearchBox_TextTyped_ShowsHitsInPlaceOfTreeAndHitOpensCard`.

**KnowledgeBrowserView_SearchBox_NoCardFits_SaysSo**: Words that no card has show the note that no card matches. This
scenario is tested by `KnowledgeBrowserView_SearchBox_NoCardFits_SaysSo`.

**KnowledgeBrowserView_CopyButton_PutsTheExampleOnTheClipboardAndSaysSo**: A card with an example shows its header and
text, and pressing Copy puts the text on the clipboard and shows the status. This scenario is tested by
`KnowledgeBrowserView_CopyButton_PutsTheExampleOnTheClipboardAndSaysSo`.

**KnowledgeBrowserView_Shown_WithoutClipboard_UsesTheClipboardOfTheWindow**: A view model with no clipboard gets one
from the window, and copying through it ends in a status that says whether it was reached. This scenario is tested by
`KnowledgeBrowserView_Shown_WithoutClipboard_UsesTheClipboardOfTheWindow`.

**KnowledgeBrowserView_CloseButton_ClosesTheWindow**: Pressing Close closes the window. This scenario is tested by
`KnowledgeBrowserView_CloseButton_ClosesTheWindow`.

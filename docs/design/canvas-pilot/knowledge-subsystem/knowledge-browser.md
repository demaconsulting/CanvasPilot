### KnowledgeBrowser

![Knowledge Structure](KnowledgeSubsystemView.svg)

#### Purpose

`KnowledgeBrowser` is the window in which the user reads the drawing knowledge: a search box, the cards as a tree or the
hits of the search, and the card that is chosen with its text, the cards below it, its related cards, and its examples.
The cards are written for the AI copilot, which is given their index and fetches a card with a tool, but they also
explain how each format works, and a user who can read them can tell the assistant exactly what is wanted. The unit
exists as its own item because it is the only part of the Knowledge subsystem that has a window, and so the only one
that depends on the user interface; every other unit of the subsystem can be used without it. The main window shows it
through a seam, as a window of its own that does not block the application.

#### Data Model

- `KnowledgeBrowserViewModel`: holds the `Nodes` of the tree, the `SearchText`, the `Results` of the search with
  `IsSearching` and `HasNoResults`, the node and card that are shown (`SelectedNode`, `SelectedCard`), what is read from
  the card (`CardTitle`, `CardSummary`, `TagsText`, `AliasesText`, `Blocks`, `Children`, `SeeAlso`, and `Examples`, each
  with a flag that says whether it has anything), the `Status` sentence, the `Clipboard`, and the commands
  `NavigateCommand`, `CopyExampleCommand`, and `CloseCommand`, which raises `CloseRequested`.
- `KnowledgeNodeViewModel`: a node of the tree, with its card, its child nodes, and whether it is open.
- `KnowledgeLinkViewModel`: a card to go to, with its identifier, title, summary, and the navigate command. The cards
  below a card, its related cards, and the hits of a search are all links.
- `KnowledgeExampleViewModel`: an example, with a header line, its description, its text, and the copy command.
- `IKnowledgeClipboard`: the seam by which the view model puts text on the clipboard; `WindowKnowledgeClipboard` is the
  one that uses the clipboard of the window.
- `IKnowledgeBrowserLauncher`: the seam by which the main window is asked to show the browser.
- `KnowledgeBrowserView` (a `Window`): the search box above a tree or the list of hits, a splitter, and the card, then a
  line of status and a Close button. `MarkdownBlocksPanel` is the control that draws the blocks that `KnowledgeMarkdown`
  read: a text block for each block, with runs for bold, italic, and code text, a dot for a bullet, and a scrolling,
  fixed-width block for code. All text can be selected.

#### Key Methods

**Select(id)**: shows a card. It finds the node of the card, by identifier or else by the path or alias that the catalog
understands, opens the nodes above it so that it is in view, makes it the selected node, and reads from the catalog what
the window shows. The text of the card is read once into blocks. A card that the catalog does not have leaves everything
as it is and says so in the status. Choosing a node in the tree, a hit of the search, and a link all end here.

**SearchText**: two or more characters, not counting the white space around them, run the search of the knowledge and
show its hits in place of the tree; fewer show the tree and clear the hits. The search is the one that the assistant
uses, so the hits are the same. The card that is shown is not changed by typing.

**CopyExampleCommand**: puts the text of an example on the clipboard and says `Copied example 'name'.`; with no
clipboard, with one that cannot be reached, or with one that throws, it says `Could not reach the clipboard.` and writes
the failure to the log.

**The window**: the view sets the clipboard of the view model to the clipboard of the window when it has none, closes
when the view model asks, and draws the card with the controls above.

#### Error Handling

Programming errors throw: a missing catalog, search, node, or identifier (`ArgumentNullException`). Everything that can
go wrong while the window is in use is a sentence in the status and not an exception: an unknown card and a clipboard
that cannot be reached. A catalog whose cards refer to one another in a loop makes a tree that ends. The view model
changes nothing in the catalog and is owned by the user interface thread.

#### Dependencies

- **KnowledgeCatalog** unit (through `IKnowledgeCatalog`): the cards, their children, related cards, and examples.
- **KnowledgeSearch** unit: the hits of the search.
- **KnowledgeMarkdown** unit: the blocks that the text of a card is drawn from.
- **Logging** subsystem: an optional logger for a clipboard failure.
- **Avalonia** and **CommunityToolkit.Mvvm** (OTS): the window, controls, clipboard, and commands.

#### Callers

- `MainWindowShell` of the AppShell subsystem makes a view model for the command of the Help menu and gives it to the
  launcher.
- The main window shows the view through `IKnowledgeBrowserLauncher.ShowKnowledgeBrowserAsync` as a window that it owns
  and that does not block it.

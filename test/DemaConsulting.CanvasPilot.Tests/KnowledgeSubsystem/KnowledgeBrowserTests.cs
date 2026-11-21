// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="KnowledgeBrowserViewModel" />, with its nodes, links, and examples: the tree of cards, the
///     card that is shown, the cards that the user goes to from it, the search, and the copying of an example. A small
///     catalog that the tests make by hand stands in for the cards of CanvasNet, and a recording clipboard for the system
///     clipboard; one test reads the real catalog.
/// </summary>
public sealed class KnowledgeBrowserTests
{
    /// <summary>
    ///     Makes a catalog of two trees: <c>svg</c> with two cards below it, a related card, and two examples, and
    ///     <c>mermaid</c> with one card below it.
    /// </summary>
    /// <returns>The catalog.</returns>
    private static FakeKnowledgeCatalog MakeCatalog()
    {
        return new FakeKnowledgeCatalog(
            FakeKnowledgeCatalog.Card(
                "svg",
                "SVG",
                "Draw with SVG.",
                tags: ["svg", "authoring"],
                aliases: ["vector"],
                body: "## Shapes\n\nUse `rect` and **bold** text.\n\n- one\n- two\n\n```xml\n<svg/>\n```",
                children: ["svg/shapes", "svg/gradients"],
                seeAlso: ["mermaid"],
                examples: [FakeKnowledgeCatalog.Example("box", "<svg>box</svg>"), FakeKnowledgeCatalog.Example("star", "<svg>star</svg>")]),
            FakeKnowledgeCatalog.Card("svg/shapes", "Shapes", "Basic shapes.", body: "Shapes body.", seeAlso: ["svg/gradients"]),
            FakeKnowledgeCatalog.Card(
                "svg/gradients",
                "Gradients",
                "Color blends.",
                body: "Gradients body.",
                examples: [FakeKnowledgeCatalog.Example("gradient", "<linearGradient/>", fileName: null)]),
            FakeKnowledgeCatalog.Card("mermaid", "Mermaid", "Flow charts.", tags: ["mermaid"], body: "Mermaid body.", children: ["mermaid/flow"]),
            FakeKnowledgeCatalog.Card("mermaid/flow", "Flowcharts", "Boxes and arrows.", body: "Flow body."));
    }

    /// <summary>
    ///     Makes a view model over a catalog.
    /// </summary>
    /// <param name="catalog">The catalog, or <see langword="null" /> for the one of <see cref="MakeCatalog" />.</param>
    /// <returns>The view model.</returns>
    private static KnowledgeBrowserViewModel MakeViewModel(IKnowledgeCatalog? catalog = null)
    {
        catalog ??= MakeCatalog();
        return new KnowledgeBrowserViewModel(catalog, new KnowledgeSearch(catalog));
    }

    /// <summary>
    ///     Test that the view model refuses a missing catalog or search.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_Constructor_NullArguments_Throw()
    {
        // Arrange: a catalog and a search
        var catalog = MakeCatalog();

        // Act / Assert: each missing argument is refused, and so is a missing identifier or example
        Assert.Throws<ArgumentNullException>(() => new KnowledgeBrowserViewModel(null!, new KnowledgeSearch(catalog)));
        Assert.Throws<ArgumentNullException>(() => new KnowledgeBrowserViewModel(catalog, null!));
        Assert.Throws<ArgumentNullException>(() => MakeViewModel(catalog).Select(null!));
        Assert.Throws<ArgumentNullException>(() => new KnowledgeNodeViewModel(null!, []));
        Assert.Throws<ArgumentNullException>(() => new KnowledgeNodeViewModel(catalog.Cards[0], null!));
    }

    /// <summary>
    ///     Test that the tree has a node for each root and, below it, a node for each card below that card, in the order of
    ///     the catalog, and that nothing is shown before the user chooses a card.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_Constructor_BuildsTreeFromRootsAndChildren()
    {
        // Arrange / Act: a view model over the small catalog
        var viewModel = MakeViewModel();

        // Assert: two roots with their children, no card shown, and the tree shown rather than hits
        Assert.Equal(["svg", "mermaid"], viewModel.Nodes.Select(n => n.Id));
        Assert.Equal(["svg/shapes", "svg/gradients"], viewModel.Nodes[0].Children.Select(n => n.Id));
        Assert.Equal(["mermaid/flow"], viewModel.Nodes[1].Children.Select(n => n.Id));
        Assert.Empty(viewModel.Nodes[0].Children[0].Children);
        Assert.Equal("SVG", viewModel.Nodes[0].Title);
        Assert.Equal("Draw with SVG.", viewModel.Nodes[0].Summary);
        Assert.False(viewModel.Nodes[0].IsExpanded);
        Assert.Null(viewModel.SelectedCard);
        Assert.Null(viewModel.SelectedNode);
        Assert.False(viewModel.HasSelection);
        Assert.False(viewModel.IsSearching);
        Assert.Equal("Drawing Knowledge", viewModel.Title);
        Assert.Equal(string.Empty, viewModel.Status);
        Assert.False(viewModel.HasStatus);
    }

    /// <summary>
    ///     Test that choosing a card shows its title, summary, tags, other names, text, cards below it, related cards, and
    ///     examples.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_Select_Card_ShowsEverythingTheCardHolds()
    {
        // Arrange: a view model that reports what changed
        var viewModel = MakeViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: choose the first card
        var shown = viewModel.Select("svg");

        // Assert: the card and everything read from it is shown, and the window was told
        Assert.True(shown);
        Assert.Equal("svg", viewModel.SelectedCard!.Id);
        Assert.Same(viewModel.Nodes[0], viewModel.SelectedNode);
        Assert.True(viewModel.HasSelection);
        Assert.Equal("SVG", viewModel.CardTitle);
        Assert.Equal("Draw with SVG.", viewModel.CardSummary);
        Assert.Equal("Tags: svg, authoring", viewModel.TagsText);
        Assert.Equal("Also known as: vector", viewModel.AliasesText);
        Assert.True(viewModel.HasTags);
        Assert.True(viewModel.HasAliases);
        Assert.Equal(
            KnowledgeMarkdown.Parse(viewModel.SelectedCard.Body).Select(b => b.GetType()),
            viewModel.Blocks.Select(b => b.GetType()));
        Assert.Contains(viewModel.Blocks, b => b is HeadingBlock { Level: 2 });
        Assert.Contains(viewModel.Blocks, b => b is CodeBlock { Language: "xml" });
        Assert.Equal(["svg/shapes", "svg/gradients"], viewModel.Children.Select(l => l.Id));
        Assert.Equal(["Shapes", "Gradients"], viewModel.Children.Select(l => l.Title));
        Assert.Equal(["Basic shapes.", "Color blends."], viewModel.Children.Select(l => l.Summary));
        Assert.Equal(["mermaid"], viewModel.SeeAlso.Select(l => l.Id));
        Assert.True(viewModel.HasChildren);
        Assert.True(viewModel.HasSeeAlso);
        Assert.Equal(["box", "star"], viewModel.Examples.Select(e => e.Name));
        Assert.Equal(["<svg>box</svg>", "<svg>star</svg>"], viewModel.Examples.Select(e => e.Content));
        Assert.Equal("box (source, xml, a.svg)", viewModel.Examples[0].Header);
        Assert.Equal("Shows box.", viewModel.Examples[0].Description);
        Assert.True(viewModel.HasExamples);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.SelectedNode), changed);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.CardTitle), changed);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.Blocks), changed);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.Children), changed);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.SeeAlso), changed);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.Examples), changed);
    }

    /// <summary>
    ///     Test that a card with nothing below it, no related card, no example, no tag, and no other name shows none of
    ///     them, and that an example without a file has a header without one.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_Select_PlainLeafCard_ShowsNoRelatedPartsAndHeaderWithoutFile()
    {
        // Arrange: a view model over a catalog of a card with no tags and one of an example without a file
        var catalog = new FakeKnowledgeCatalog(
            FakeKnowledgeCatalog.Card("plain", "Plain", tags: [], body: "Text."),
            FakeKnowledgeCatalog.Card("other", "Other", examples: [FakeKnowledgeCatalog.Example("x", fileName: null)]));
        var viewModel = MakeViewModel(catalog);

        // Act: choose the plain card, then the other
        viewModel.Select("plain");
        var plain = (viewModel.HasTags, viewModel.HasAliases, viewModel.HasChildren, viewModel.HasSeeAlso, viewModel.HasExamples);
        viewModel.Select("other");

        // Assert: nothing of the extra parts is shown, and the header names no file
        Assert.Equal((false, false, false, false, false), plain);
        Assert.Equal("x (source, xml)", viewModel.Examples[0].Header);
    }

    /// <summary>
    ///     Test that choosing a card below another opens the cards above it in the tree, selects it, and leaves the other
    ///     cards closed.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_Select_ChildCard_OpensAncestorsAndSelectsItsNode()
    {
        // Arrange: a view model
        var viewModel = MakeViewModel();

        // Act: choose a card below the first root
        viewModel.Select("svg/gradients");

        // Assert: the root is open and the node of the card is the one selected; the other root stays closed
        Assert.True(viewModel.Nodes[0].IsExpanded);
        Assert.False(viewModel.Nodes[1].IsExpanded);
        Assert.Same(viewModel.Nodes[0].Children[1], viewModel.SelectedNode);
        Assert.Equal("Gradients", viewModel.CardTitle);
        Assert.Equal(["gradient"], viewModel.Examples.Select(e => e.Name));
    }

    /// <summary>
    ///     Test that a card can be chosen by an alias or a path with spaces, and that an unknown card leaves the card that
    ///     is shown as it is and says so.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_Select_AliasAndUnknownCard_FindsOrKeepsTheShownCard()
    {
        // Arrange: a view model that shows a card
        var viewModel = MakeViewModel();
        viewModel.Select("mermaid");

        // Act: choose by an alias, then an unknown card
        var byAlias = viewModel.Select("Vector");
        var shownByAlias = viewModel.SelectedCard!.Id;
        var unknown = viewModel.Select("no/such/card");

        // Assert: the alias found the card, and the unknown card changed nothing but the status
        Assert.True(byAlias);
        Assert.Equal("svg", shownByAlias);
        Assert.False(unknown);
        Assert.Equal("svg", viewModel.SelectedCard.Id);
        Assert.Equal("There is no card 'no/such/card'.", viewModel.Status);
        Assert.True(viewModel.HasStatus);
    }

    /// <summary>
    ///     Test that the node that the tree selects shows the card of the node, and that clearing the selection, which a
    ///     tree does when its items change, leaves what is shown.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_SelectedNode_SetByTheTree_ShowsItsCardAndIgnoresNull()
    {
        // Arrange: a view model
        var viewModel = MakeViewModel();

        // Act: the tree selects a node, and then clears the selection
        viewModel.SelectedNode = viewModel.Nodes[1];
        viewModel.SelectedNode = null;

        // Assert: the card of the node is still shown
        Assert.Equal("mermaid", viewModel.SelectedCard!.Id);
        Assert.Same(viewModel.Nodes[1], viewModel.SelectedNode);
        Assert.Equal(["mermaid/flow"], viewModel.Children.Select(l => l.Id));
    }

    /// <summary>
    ///     Test that choosing the card that is shown again reads it again, which clears the status.
    /// </summary>
    [Fact]
    public async Task KnowledgeBrowserViewModel_Select_CardThatIsShown_ReadsItAgainAndClearsStatus()
    {
        // Arrange: a view model that shows a card and has a status from a failed copy
        var viewModel = MakeViewModel();
        viewModel.Select("svg");
        await viewModel.CopyExampleCommand.ExecuteAsync(viewModel.Examples[0]);
        Assert.Equal("Could not reach the clipboard.", viewModel.Status);

        // Act: choose the same card
        viewModel.Select("svg");

        // Assert: the status is gone
        Assert.Equal(string.Empty, viewModel.Status);
    }

    /// <summary>
    ///     Test that a related card and a card below can be followed with the navigate command, and that a command without
    ///     an identifier does nothing.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_NavigateCommand_LinkOfTheCard_ShowsThatCard()
    {
        // Arrange: a view model that shows the first card
        var viewModel = MakeViewModel();
        viewModel.Select("svg");

        // Act: follow the related card, then a card below the card of the tree, then the command with no parameter
        var seeAlso = viewModel.SeeAlso[0];
        seeAlso.NavigateCommand.Execute(seeAlso.Id);
        var afterSeeAlso = viewModel.SelectedCard!.Id;
        var child = viewModel.Children.Single();
        child.NavigateCommand.Execute(child.Id);
        var afterChild = viewModel.SelectedCard.Id;
        viewModel.NavigateCommand.Execute(null);

        // Assert: each link went to its card, and the empty command changed nothing
        Assert.Equal("mermaid", afterSeeAlso);
        Assert.Equal("mermaid/flow", afterChild);
        Assert.Equal("mermaid/flow", viewModel.SelectedCard.Id);
        Assert.Same(viewModel.Nodes[1].Children[0], viewModel.SelectedNode);
        Assert.True(viewModel.Nodes[1].IsExpanded);
    }

    /// <summary>
    ///     Test that two or more characters in the search box show the hits of the search in place of the tree, best first,
    ///     with the text trimmed.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_SearchText_TwoOrMoreCharacters_ShowsHitsInPlaceOfTree()
    {
        // Arrange: a view model
        var viewModel = MakeViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: type a word with spaces around it
        viewModel.SearchText = "  gradients ";

        // Assert: the hit is the card, the tree gives way, and the window was told
        Assert.True(viewModel.IsSearching);
        Assert.False(viewModel.HasNoResults);
        Assert.Equal("svg/gradients", viewModel.Results[0].Id);
        Assert.Equal("Gradients", viewModel.Results[0].Title);
        Assert.Equal("Color blends.", viewModel.Results[0].Summary);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.Results), changed);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.IsSearching), changed);
        Assert.Contains(nameof(KnowledgeBrowserViewModel.SearchText), changed);
    }

    /// <summary>
    ///     Test that one character, only white space, and an empty text show the tree again, and that a word with no hit is
    ///     a search that found nothing.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_SearchText_ShortOrEmpty_RestoresTreeAndNoHitIsReported()
    {
        // Arrange: a view model that is searching
        var viewModel = MakeViewModel();
        viewModel.SearchText = "shapes";
        Assert.True(viewModel.IsSearching);

        // Act: shorten the text step by step, then search for a word that is nowhere
        var states = new List<(bool Searching, int Hits)>();
        foreach (var text in new[] { "s", "  ", string.Empty })
        {
            viewModel.SearchText = "shapes";
            viewModel.SearchText = text;
            states.Add((viewModel.IsSearching, viewModel.Results.Count));
        }

        viewModel.SearchText = "zzzzzz";

        // Assert: the tree came back each time, and the last search found nothing and says so
        Assert.All(states, state => Assert.Equal((false, 0), state));
        Assert.True(viewModel.IsSearching);
        Assert.True(viewModel.HasNoResults);
        Assert.Empty(viewModel.Results);
        viewModel.SearchText = null!;
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.False(viewModel.IsSearching);
    }

    /// <summary>
    ///     Test that going to a hit shows its card and keeps the hits, and that the card that is shown stays while the user
    ///     types.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_SearchText_HitChosen_ShowsCardAndKeepsHits()
    {
        // Arrange: a view model that shows a card
        var viewModel = MakeViewModel();
        viewModel.Select("svg");

        // Act: search, check that the card stays, and go to the second hit
        viewModel.SearchText = "mermaid";
        var staysWhileTyping = viewModel.SelectedCard!.Id;
        var hit = viewModel.Results[0];
        hit.NavigateCommand.Execute(hit.Id);

        // Assert: the card of the hit is shown and the hits are still there
        Assert.Equal("svg", staysWhileTyping);
        Assert.Equal("mermaid", viewModel.SelectedCard.Id);
        Assert.True(viewModel.IsSearching);
        Assert.Same(viewModel.Nodes[1], viewModel.SelectedNode);
    }

    /// <summary>
    ///     Test that copying an example puts its text on the clipboard and says which example was copied.
    /// </summary>
    [Fact]
    public async Task KnowledgeBrowserViewModel_CopyExampleCommand_Success_PutsContentOnClipboardAndSaysSo()
    {
        // Arrange: a view model with a recording clipboard that shows a card with examples
        var clipboard = new RecordingKnowledgeClipboard();
        var viewModel = MakeViewModel();
        viewModel.Clipboard = clipboard;
        viewModel.Select("svg");

        // Act: copy the second example
        await viewModel.Examples[1].CopyCommand.ExecuteAsync(viewModel.Examples[1]);

        // Assert: its text is on the clipboard and the status names it
        Assert.Equal(["<svg>star</svg>"], clipboard.Texts);
        Assert.Equal("Copied example 'star'.", viewModel.Status);
        Assert.True(viewModel.HasStatus);
    }

    /// <summary>
    ///     Test that a missing clipboard, a clipboard that cannot be reached, and a clipboard that throws all leave the
    ///     status that says the clipboard cannot be reached, and that nothing is thrown at the user.
    /// </summary>
    [Fact]
    public async Task KnowledgeBrowserViewModel_CopyExampleCommand_ClipboardOutOfReach_SaysSoAndDoesNotThrow()
    {
        // Arrange: a view model without a clipboard that shows a card with examples
        var viewModel = MakeViewModel();
        viewModel.Select("svg");
        var statuses = new List<string>();

        // Act: copy with no clipboard, with one that fails, and with one that throws
        await viewModel.CopyExampleCommand.ExecuteAsync(viewModel.Examples[0]);
        statuses.Add(viewModel.Status);
        viewModel.Clipboard = new RecordingKnowledgeClipboard { Succeeds = false };
        await viewModel.CopyExampleCommand.ExecuteAsync(viewModel.Examples[0]);
        statuses.Add(viewModel.Status);
        viewModel.Clipboard = new RecordingKnowledgeClipboard { Throws = new InvalidOperationException("locked") };
        await viewModel.CopyExampleCommand.ExecuteAsync(viewModel.Examples[0]);
        statuses.Add(viewModel.Status);

        // Assert: the same sentence each time
        Assert.All(statuses, status => Assert.Equal("Could not reach the clipboard.", status));
    }

    /// <summary>
    ///     Test that the copy command with no example does nothing, and that the close command asks the window to close.
    /// </summary>
    [Fact]
    public async Task KnowledgeBrowserViewModel_Commands_NoExampleAndClose_DoNothingAndRaiseCloseRequested()
    {
        // Arrange: a view model with a clipboard that counts the copies
        var clipboard = new RecordingKnowledgeClipboard();
        var viewModel = MakeViewModel();
        viewModel.Clipboard = clipboard;
        var closed = 0;
        viewModel.CloseRequested += (_, _) => closed++;

        // Act: copy nothing and close
        await viewModel.CopyExampleCommand.ExecuteAsync(null);
        viewModel.CloseCommand.Execute(null);

        // Assert: nothing was copied and the close was requested once
        Assert.Empty(clipboard.Texts);
        Assert.Equal(string.Empty, viewModel.Status);
        Assert.Equal(1, closed);
    }

    /// <summary>
    ///     Test that a catalog whose cards loop does not make the tree loop.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_Constructor_CatalogThatLoops_BuildsATreeThatEnds()
    {
        // Arrange: two cards that are each the child of the other, and a root that holds the first
        var catalog = new FakeKnowledgeCatalog(
            FakeKnowledgeCatalog.Card("a", "A", children: ["a/b"]),
            FakeKnowledgeCatalog.Card("a/b", "B", children: ["a"]));

        // Act: build the tree
        var viewModel = MakeViewModel(catalog);

        // Assert: the tree ends after the second card
        Assert.Equal(["a"], viewModel.Nodes.Select(n => n.Id));
        Assert.Equal(["a/b"], viewModel.Nodes[0].Children.Select(n => n.Id));
        Assert.Empty(viewModel.Nodes[0].Children[0].Children);
    }

    /// <summary>
    ///     Test that the browser over the catalog of the application has a node for each of its roots, shows every card of it
    ///     with its text, and finds the cards that fit the words of a search.
    /// </summary>
    [Fact]
    public void KnowledgeBrowserViewModel_Select_EveryCardOfTheApplication_ShowsItsTextAndSearchFindsCards()
    {
        // Arrange: a view model over the catalog of the application
        var catalog = KnowledgeCatalog.Default;
        var viewModel = new KnowledgeBrowserViewModel(catalog, new KnowledgeSearch(catalog));

        // Act: show every card, then search for a word of a card of CanvasPilot
        var notShown = catalog.Cards.Where(card => !viewModel.Select(card.Id)).Select(card => card.Id).ToList();
        var lastExamplesOk = viewModel.Examples.All(e => e.Content.Length > 0);
        viewModel.SearchText = "marks";

        // Assert: the roots are the nodes, every card was shown with text, and the search found the card of the marks
        Assert.Equal(catalog.Roots.Select(r => r.Id), viewModel.Nodes.Select(n => n.Id));
        Assert.Empty(notShown);
        Assert.True(lastExamplesOk);
        Assert.Contains(viewModel.Results, hit => hit.Id == "canvaspilot/marks");
        viewModel.Select("canvaspilot/marks");
        Assert.NotEmpty(viewModel.Blocks);
        Assert.NotEmpty(viewModel.Examples);
        Assert.True(viewModel.Nodes.Single(n => n.Id == "canvaspilot").IsExpanded);
    }
}

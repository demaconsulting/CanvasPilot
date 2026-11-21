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

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     A clipboard for the headless tests of the browser of the drawing knowledge: it records the text that is put on it.
/// </summary>
internal sealed class RecordingKnowledgeClipboard : IKnowledgeClipboard
{
    /// <summary>
    ///     Gets the texts that were put on the clipboard, in order.
    /// </summary>
    public List<string> Texts { get; } = [];

    /// <inheritdoc />
    public Task<bool> SetTextAsync(string text)
    {
        Texts.Add(text);
        return Task.FromResult(true);
    }
}

/// <summary>
///     Headless UI tests for <see cref="KnowledgeBrowserView" />: the window that shows the cards of the drawing knowledge
///     as a tree with a search box, the card that is chosen with its text drawn from Markdown, the cards below it and its
///     related cards as buttons that go to them, and its examples with a Copy button. The cards are those of the catalog
///     of the application, so what the window shows can be compared with what the catalog holds.
/// </summary>
public sealed class KnowledgeBrowserViewTests
{
    /// <summary>
    ///     Makes a browser over the catalog of the application.
    /// </summary>
    /// <param name="clipboard">The clipboard of the browser, or <see langword="null" /> to leave it to the window.</param>
    /// <returns>The view model.</returns>
    private static KnowledgeBrowserViewModel MakeViewModel(IKnowledgeClipboard? clipboard = null)
    {
        var catalog = KnowledgeCatalog.Default;
        return new KnowledgeBrowserViewModel(catalog, new KnowledgeSearch(catalog)) { Clipboard = clipboard };
    }

    /// <summary>
    ///     Shows a window for a view model and lays it out.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    /// <returns>The window.</returns>
    private static KnowledgeBrowserView Show(KnowledgeBrowserViewModel viewModel)
    {
        var window = new KnowledgeBrowserView(viewModel);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    /// <summary>
    ///     Finds a control in a window by its automation identifier.
    /// </summary>
    /// <typeparam name="T">The type of the control.</typeparam>
    /// <param name="window">The window.</param>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The control.</returns>
    private static T Find<T>(Window window, string automationId)
        where T : Control => SettingsUiHelpers.Find<T>(window, automationId);

    /// <summary>
    ///     Finds every control with an automation identifier, in the order of the window.
    /// </summary>
    /// <typeparam name="T">The type of the control.</typeparam>
    /// <param name="window">The window.</param>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The controls.</returns>
    private static List<T> FindAll<T>(Window window, string automationId)
        where T : Control
    {
        return window.GetVisualDescendants().OfType<T>()
            .Where(control => AutomationProperties.GetAutomationId(control) == automationId)
            .ToList();
    }

    /// <summary>
    ///     Presses a button as a user would: scrolled into view and clicked with the mouse.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="button">The button.</param>
    private static void Press(Window window, Button button)
    {
        button.BringIntoView();
        window.UpdateLayout();
        SettingsUiHelpers.Click(window, button);
        window.UpdateLayout();
    }

    /// <summary>
    ///     Reads the text of a text block, which is the text of its runs when it has runs.
    /// </summary>
    /// <param name="block">The text block.</param>
    /// <returns>The text.</returns>
    private static string TextOf(TextBlock block)
    {
        return block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;
    }

    /// <summary>
    ///     Test that the window starts with the roots of the catalog in the tree, no card chosen, and the hits hidden.
    /// </summary>
    [AvaloniaFact]
    public void KnowledgeBrowserView_Shown_ShowsRootsInTreeAndNoCard()
    {
        // Arrange / Act: show the window over the catalog of the application
        var viewModel = MakeViewModel();
        var window = Show(viewModel);

        // Assert: the tree has a node for each root, the placeholder is shown and the card is not, and the hits are not
        var tree = Find<TreeView>(window, "KnowledgeCardTree");
        Assert.Equal(viewModel.Nodes.Count, tree.ItemCount);
        Assert.Equal(KnowledgeCatalog.Default.Roots.Count, tree.ItemCount);
        Assert.True(tree.IsVisible);
        Assert.True(Find<TextBlock>(window, "KnowledgePlaceholder").IsVisible);
        Assert.False(Find<SelectableTextBlock>(window, "KnowledgeCardTitle").IsEffectivelyVisible);
        Assert.False(Find<TextBox>(window, "KnowledgeSearchBox").IsReadOnly);
        Assert.Equal("Drawing Knowledge", window.Title);
        window.Close();
    }

    /// <summary>
    ///     Test that choosing a node of the tree shows the title, summary, tags, and the text of its card, with the text
    ///     drawn from its Markdown: headings, list items, code, and bold and code runs.
    /// </summary>
    [AvaloniaFact]
    public void KnowledgeBrowserView_NodeChosen_ShowsTitleSummaryTagsAndBodyDrawnFromMarkdown()
    {
        // Arrange: a shown window
        var viewModel = MakeViewModel();
        var window = Show(viewModel);
        var tree = Find<TreeView>(window, "KnowledgeCardTree");
        var node = viewModel.Nodes.Single(n => n.Id == "canvaspilot");

        // Act: the user chooses the node of the card of CanvasPilot
        tree.SelectedItem = node;
        window.UpdateLayout();

        // Assert: the card is shown in place of the placeholder, with its parts and its text as controls
        var card = node.Card;
        Assert.Same(node, viewModel.SelectedNode);
        Assert.False(Find<TextBlock>(window, "KnowledgePlaceholder").IsVisible);
        Assert.Equal(card.Title, Find<SelectableTextBlock>(window, "KnowledgeCardTitle").Text);
        Assert.Equal(card.Summary, Find<SelectableTextBlock>(window, "KnowledgeCardSummary").Text);
        Assert.Contains("Tags: ", Find<TextBlock>(window, "KnowledgeCardTags").Text);
        Assert.Contains("project", Find<TextBlock>(window, "KnowledgeCardAliases").Text);
        var body = Find<MarkdownBlocksPanel>(window, "KnowledgeCardBody");
        Assert.Equal(viewModel.Blocks.Count, body.Children.Count);
        var runs = body.GetVisualDescendants().OfType<SelectableTextBlock>()
            .SelectMany(text => text.Inlines ?? []).OfType<Run>().ToList();
        Assert.Contains(runs, run => run.Background is not null);
        Assert.Contains(body.GetVisualDescendants().OfType<SelectableTextBlock>(), text => AutomationProperties.GetAutomationId(text) == "KnowledgeBodyHeading");
        window.Close();
    }

    /// <summary>
    ///     Test that every kind of block of a card is drawn: a heading in bold type, a bullet with a dot, a number as
    ///     written, text with bold and code runs, and a block of code with its lines.
    /// </summary>
    [AvaloniaFact]
    public void KnowledgeBrowserView_MarkdownBlocksPanel_DrawsEveryKindOfBlock()
    {
        // Arrange: a panel over the blocks that the reader makes of a small text
        var blocks = KnowledgeMarkdown.Parse(
            "## Heading\n\nSome **bold**, *italic*, and `code` text.\n\n- a bullet\n3. a number\n\n```xml\n<svg/>\n<g/>\n```");
        var panel = new MarkdownBlocksPanel { Blocks = blocks };
        var window = new Window { Content = panel, Width = 500, Height = 400 };

        // Act: show it
        window.Show();
        window.UpdateLayout();

        // Assert: five blocks are drawn, each as its kind needs
        Assert.Equal(5, panel.Children.Count);
        var heading = Assert.IsType<SelectableTextBlock>(panel.Children[0]);
        Assert.Equal(FontWeight.SemiBold, heading.FontWeight);
        Assert.Equal("Heading", TextOf(heading));
        var paragraph = Assert.IsType<SelectableTextBlock>(panel.Children[1]);
        var paragraphRuns = paragraph.Inlines!.OfType<Run>().ToList();
        Assert.Equal("Some bold, italic, and code text.", TextOf(paragraph));
        Assert.Contains(paragraphRuns, run => run.Text == "bold" && run.FontWeight == FontWeight.Bold);
        Assert.Contains(paragraphRuns, run => run.Text == "italic" && run.FontStyle == FontStyle.Italic);
        Assert.Contains(paragraphRuns, run => run.Text == "code" && run.Background is not null);
        var markers = panel.Children.OfType<Grid>().Select(grid => grid.Children.OfType<TextBlock>().First(t => t is not SelectableTextBlock).Text).ToList();
        Assert.Equal(["\u2022", "3."], markers);
        var code = panel.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => AutomationProperties.GetAutomationId(t) == "KnowledgeBodyCode");
        Assert.Equal("<svg/>\n<g/>", code.Text?.Replace("\r\n", "\n", StringComparison.Ordinal));

        // Act: draw different blocks, then none
        panel.Blocks = KnowledgeMarkdown.Parse("Only text.");
        var afterChange = panel.Children.Count;
        panel.Blocks = null;

        // Assert: the panel follows the blocks
        Assert.Equal(1, afterChange);
        Assert.Empty(panel.Children);
        window.Close();
    }

    /// <summary>
    ///     Test that the cards below a card and its related cards are buttons that go to their cards, and that going to a
    ///     card selects its node in the tree and opens the cards above it.
    /// </summary>
    [AvaloniaFact]
    public void KnowledgeBrowserView_SeeAlsoAndChildButtons_GoToTheirCardsAndSelectTheNode()
    {
        // Arrange: a window that shows the card of CanvasPilot, which has cards below it and related cards
        var viewModel = MakeViewModel();
        var window = Show(viewModel);
        viewModel.Select("canvaspilot");
        window.UpdateLayout();
        var catalog = KnowledgeCatalog.Default;
        var card = catalog.Find("canvaspilot")!;
        var seeAlsoButtons = FindAll<Button>(window, "KnowledgeSeeAlsoButton");
        var childButtons = FindAll<Button>(window, "KnowledgeChildButton");
        Assert.Equal(card.SeeAlsoIds.Count, seeAlsoButtons.Count);
        Assert.Equal(card.ChildIds.Count, childButtons.Count);

        // Act: press the first related card, and then the first card below the card that is shown
        Press(window, seeAlsoButtons[0]);
        var afterSeeAlso = viewModel.SelectedCard!.Id;
        var seeAlsoTitle = Find<SelectableTextBlock>(window, "KnowledgeCardTitle").Text;
        viewModel.Select("canvaspilot");
        window.UpdateLayout();
        Press(window, FindAll<Button>(window, "KnowledgeChildButton")[0]);

        // Assert: each press showed its card, and the tree selects the node of the card below
        Assert.Equal(card.SeeAlsoIds[0], afterSeeAlso);
        Assert.Equal(catalog.Find(card.SeeAlsoIds[0])!.Title, seeAlsoTitle);
        Assert.Equal(card.ChildIds[0], viewModel.SelectedCard.Id);
        var tree = Find<TreeView>(window, "KnowledgeCardTree");
        Assert.Equal(card.ChildIds[0], ((KnowledgeNodeViewModel)tree.SelectedItem!).Id);
        Assert.True(viewModel.Nodes.Single(n => n.Id == "canvaspilot").IsExpanded);
        window.Close();
    }

    /// <summary>
    ///     Test that typing in the search box shows the hits in place of the tree, that a hit can be chosen, and that
    ///     clearing the box shows the tree again.
    /// </summary>
    [AvaloniaFact]
    public void KnowledgeBrowserView_SearchBox_TextTyped_ShowsHitsInPlaceOfTreeAndHitOpensCard()
    {
        // Arrange: a shown window
        var viewModel = MakeViewModel();
        var window = Show(viewModel);
        var searchBox = Find<TextBox>(window, "KnowledgeSearchBox");

        // Act: type a word
        searchBox.Text = "gradient";
        window.UpdateLayout();

        // Assert: the hits are shown, with the best first, and the tree is hidden
        var tree = Find<TreeView>(window, "KnowledgeCardTree");
        var results = FindAll<Button>(window, "KnowledgeResultButton");
        Assert.False(tree.IsVisible);
        Assert.True(viewModel.IsSearching);
        Assert.Equal(viewModel.Results.Count, results.Count);
        Assert.NotEmpty(results);
        Assert.False(Find<TextBlock>(window, "KnowledgeNoResults").IsVisible);

        // Act: choose the first hit
        var firstHit = viewModel.Results[0];
        Press(window, results[0]);

        // Assert: its card is shown
        Assert.Equal(firstHit.Id, viewModel.SelectedCard!.Id);
        Assert.Equal(firstHit.Title, Find<SelectableTextBlock>(window, "KnowledgeCardTitle").Text);

        // Act: clear the box
        searchBox.Text = string.Empty;
        window.UpdateLayout();

        // Assert: the tree is back
        Assert.True(tree.IsVisible);
        Assert.Empty(FindAll<Button>(window, "KnowledgeResultButton"));
        window.Close();
    }

    /// <summary>
    ///     Test that a search that finds nothing says so.
    /// </summary>
    [AvaloniaFact]
    public void KnowledgeBrowserView_SearchBox_NoCardFits_SaysSo()
    {
        // Arrange: a shown window
        var window = Show(MakeViewModel());

        // Act: type words that are in no card
        Find<TextBox>(window, "KnowledgeSearchBox").Text = "xylophone";
        window.UpdateLayout();

        // Assert: the note is shown and no hit is
        Assert.True(Find<TextBlock>(window, "KnowledgeNoResults").IsVisible);
        Assert.Empty(FindAll<Button>(window, "KnowledgeResultButton"));
        window.Close();
    }

    /// <summary>
    ///     Test that each example of a card is shown with its header and text, and that its Copy button puts the text on the
    ///     clipboard and says so.
    /// </summary>
    [AvaloniaFact]
    public void KnowledgeBrowserView_CopyButton_PutsTheExampleOnTheClipboardAndSaysSo()
    {
        // Arrange: a window over a view model with a recording clipboard, that shows a card with an example
        var clipboard = new RecordingKnowledgeClipboard();
        var viewModel = MakeViewModel(clipboard);
        var window = Show(viewModel);
        viewModel.Select("canvaspilot/deliverables");
        window.UpdateLayout();
        var example = viewModel.Examples[0];

        // Act: press Copy
        var copyButtons = FindAll<Button>(window, "KnowledgeCopyExampleButton");
        Press(window, copyButtons[0]);

        // Assert: the header and the text are shown, the text was copied, and the status says so
        Assert.Equal(viewModel.Examples.Count, copyButtons.Count);
        Assert.Equal(example.Header, FindAll<TextBlock>(window, "KnowledgeExampleHeader")[0].Text);
        Assert.Equal(example.Content, FindAll<SelectableTextBlock>(window, "KnowledgeExampleContent")[0].Text);
        Assert.Equal([example.Content], clipboard.Texts);
        Assert.Equal($"Copied example '{example.Name}'.", Find<TextBlock>(window, "KnowledgeStatus").Text);
        window.Close();
    }

    /// <summary>
    ///     Test that the window gives the view model the clipboard of the system when it has none, and that copying
    ///     through it ends in a status that says whether the clipboard could be reached.
    /// </summary>
    [AvaloniaFact]
    public async Task KnowledgeBrowserView_Shown_WithoutClipboard_UsesTheClipboardOfTheWindow()
    {
        // Arrange: a window over a view model that has no clipboard, showing a card with an example
        var viewModel = MakeViewModel();
        Assert.Null(viewModel.Clipboard);
        var window = Show(viewModel);
        viewModel.Select("canvaspilot/deliverables");

        // Act: copy the example through the clipboard that the window supplied
        await viewModel.CopyExampleCommand.ExecuteAsync(viewModel.Examples[0]);

        // Assert: the view model has a clipboard, and it answered either way with a sentence
        Assert.NotNull(viewModel.Clipboard);
        Assert.StartsWith(
            viewModel.Status.StartsWith("Copied", StringComparison.Ordinal) ? "Copied example" : "Could not reach",
            viewModel.Status,
            StringComparison.Ordinal);
        window.Close();
    }

    /// <summary>
    ///     Test that the Close button closes the window.
    /// </summary>
    [AvaloniaFact]
    public void KnowledgeBrowserView_CloseButton_ClosesTheWindow()
    {
        // Arrange: a shown window
        var window = Show(MakeViewModel());

        // Act: press Close
        SettingsUiHelpers.Click(window, Find<Button>(window, "KnowledgeCloseButton"));

        // Assert: the window is closed
        Assert.False(window.IsVisible);
    }
}

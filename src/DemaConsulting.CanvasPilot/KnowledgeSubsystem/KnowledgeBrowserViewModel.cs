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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     The view model of the browser of the drawing knowledge: the tree of cards, the search box and its hits, and the
///     card that is shown with its text, the cards below it, its related cards, and its examples, each of which can be
///     copied.
/// </summary>
/// <remarks>
///     <para>
///         <b>One card at a time, reached three ways.</b> The user chooses a card in the tree (the window sets
///         <see cref="SelectedNode" />), chooses a hit of the search, or follows a card below or a related card. All three
///         end in <see cref="Select" />, which opens the cards above the card in the tree, selects it there, and reads
///         what the window shows (the title, the summary, the tags, the text as blocks of Markdown, the cards below, the
///         related cards, and the examples) from the catalog. A card that the catalog does not know leaves what is shown
///         as it is and says so in <see cref="Status" />.
///     </para>
///     <para>
///         <b>Search replaces the tree, it does not filter it.</b> Two or more characters in <see cref="SearchText" />
///         run the same search that the assistant uses and show its hits in the place of the tree; fewer show the tree
///         again. The card that is shown stays as it is while the user types.
///     </para>
///     <para>
///         <b>The clipboard belongs to the window.</b> <see cref="Clipboard" /> is set by the window, which owns the
///         system clipboard; without it, or when it cannot be reached, copying says so in <see cref="Status" /> and does
///         nothing else. The view model is owned by the user interface thread and makes no change to the catalog.
///     </para>
/// </remarks>
public sealed class KnowledgeBrowserViewModel : ObservableObject
{
    /// <summary>
    ///     The fewest characters of the search box that start a search.
    /// </summary>
    public const int MinimumSearchLength = 2;

    /// <summary>
    ///     The catalog of cards.
    /// </summary>
    private readonly IKnowledgeCatalog _catalog;

    /// <summary>
    ///     The search over the catalog.
    /// </summary>
    private readonly KnowledgeSearch _search;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The nodes of the tree by the identifier of their card.
    /// </summary>
    private readonly Dictionary<string, KnowledgeNodeViewModel> _nodesById = new(StringComparer.Ordinal);

    /// <summary>
    ///     The node of the card that is shown, or <see langword="null" /> before a card was chosen.
    /// </summary>
    private KnowledgeNodeViewModel? _selectedNode;

    /// <summary>
    ///     The card that is shown, or <see langword="null" /> before a card was chosen.
    /// </summary>
    private KnowledgeCard? _selectedCard;

    /// <summary>
    ///     The text of the card that is shown, as blocks.
    /// </summary>
    private IReadOnlyList<MarkdownBlock> _blocks = [];

    /// <summary>
    ///     The cards below the card that is shown.
    /// </summary>
    private IReadOnlyList<KnowledgeLinkViewModel> _children = [];

    /// <summary>
    ///     The related cards of the card that is shown.
    /// </summary>
    private IReadOnlyList<KnowledgeLinkViewModel> _seeAlso = [];

    /// <summary>
    ///     The examples of the card that is shown.
    /// </summary>
    private IReadOnlyList<KnowledgeExampleViewModel> _examples = [];

    /// <summary>
    ///     The hits of the search.
    /// </summary>
    private IReadOnlyList<KnowledgeLinkViewModel> _results = [];

    /// <summary>
    ///     Whether the hits of a search are shown in place of the tree.
    /// </summary>
    private bool _isSearching;

    /// <summary>
    ///     The sentence that says what the last copy did.
    /// </summary>
    private string _status = string.Empty;

    /// <summary>
    ///     The text of the search box.
    /// </summary>
    private string _searchText = string.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KnowledgeBrowserViewModel" /> class.
    /// </summary>
    /// <param name="catalog">The cards to show.</param>
    /// <param name="search">The search over the same cards.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog" /> or <paramref name="search" /> is <see langword="null" />.</exception>
    public KnowledgeBrowserViewModel(IKnowledgeCatalog catalog, KnowledgeSearch search, RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(search);

        _catalog = catalog;
        _search = search;
        _logger = logger;
        NavigateCommand = new RelayCommand<string>(NavigateTo);
        CopyExampleCommand = new AsyncRelayCommand<KnowledgeExampleViewModel>(CopyExampleAsync);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        Nodes = BuildNodes(catalog.Roots, []);
    }

    /// <summary>
    ///     Raised when the user asks to close the browser, so that the window can close.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    ///     Gets the text of the window title.
    /// </summary>
    public string Title { get; } = "Drawing Knowledge";

    /// <summary>
    ///     Gets or sets what copies text to the clipboard. The window sets it; <see langword="null" /> means that the
    ///     clipboard cannot be reached.
    /// </summary>
    public IKnowledgeClipboard? Clipboard { get; set; }

    /// <summary>
    ///     Gets the tree of cards: a node for each root of the catalog, with the nodes of the cards below it.
    /// </summary>
    public IReadOnlyList<KnowledgeNodeViewModel> Nodes { get; }

    /// <summary>
    ///     Gets or sets the text of the search box. Two or more characters, not counting the white space around them,
    ///     show the hits in <see cref="Results" /> in place of the tree; fewer show the tree.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                RunSearch();
            }
        }
    }

    /// <summary>
    ///     Gets the hits of the search, best first; empty when the tree is shown.
    /// </summary>
    public IReadOnlyList<KnowledgeLinkViewModel> Results
    {
        get => _results;
        private set
        {
            if (SetProperty(ref _results, value))
            {
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the hits of a search are shown in place of the tree.
    /// </summary>
    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (SetProperty(ref _isSearching, value))
            {
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether a search was made and found no card.
    /// </summary>
    public bool HasNoResults => IsSearching && Results.Count == 0;

    /// <summary>
    ///     Gets or sets the node of the card that is shown, which is what the tree selects. Setting a node shows its
    ///     card; setting <see langword="null" />, which a tree does when its items change, leaves what is shown.
    /// </summary>
    public KnowledgeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (value is not null && SetProperty(ref _selectedNode, value))
            {
                ShowNode(value);
            }
        }
    }

    /// <summary>
    ///     Gets the card that is shown, or <see langword="null" /> before a card was chosen.
    /// </summary>
    public KnowledgeCard? SelectedCard => _selectedCard;

    /// <summary>
    ///     Gets a value indicating whether a card is shown.
    /// </summary>
    public bool HasSelection => _selectedCard is not null;

    /// <summary>
    ///     Gets the title of the card that is shown, or an empty text.
    /// </summary>
    public string CardTitle => _selectedCard?.Title ?? string.Empty;

    /// <summary>
    ///     Gets the summary of the card that is shown, or an empty text.
    /// </summary>
    public string CardSummary => _selectedCard?.Summary ?? string.Empty;

    /// <summary>
    ///     Gets the tags of the card that is shown as a line of text, or an empty text.
    /// </summary>
    public string TagsText => _selectedCard is { Tags.Count: > 0 } card ? "Tags: " + string.Join(", ", card.Tags) : string.Empty;

    /// <summary>
    ///     Gets the other names of the card that is shown as a line of text, or an empty text.
    /// </summary>
    public string AliasesText => _selectedCard is { Aliases.Count: > 0 } card ? "Also known as: " + string.Join(", ", card.Aliases) : string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the card that is shown has tags.
    /// </summary>
    public bool HasTags => TagsText.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether the card that is shown has other names.
    /// </summary>
    public bool HasAliases => AliasesText.Length > 0;

    /// <summary>
    ///     Gets the text of the card that is shown as blocks of Markdown, which the window draws.
    /// </summary>
    public IReadOnlyList<MarkdownBlock> Blocks => _blocks;

    /// <summary>
    ///     Gets the cards below the card that is shown, as links.
    /// </summary>
    public IReadOnlyList<KnowledgeLinkViewModel> Children => _children;

    /// <summary>
    ///     Gets a value indicating whether the card that is shown has cards below it.
    /// </summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>
    ///     Gets the related cards of the card that is shown, as links.
    /// </summary>
    public IReadOnlyList<KnowledgeLinkViewModel> SeeAlso => _seeAlso;

    /// <summary>
    ///     Gets a value indicating whether the card that is shown has related cards.
    /// </summary>
    public bool HasSeeAlso => _seeAlso.Count > 0;

    /// <summary>
    ///     Gets the examples of the card that is shown.
    /// </summary>
    public IReadOnlyList<KnowledgeExampleViewModel> Examples => _examples;

    /// <summary>
    ///     Gets a value indicating whether the card that is shown has examples.
    /// </summary>
    public bool HasExamples => _examples.Count > 0;

    /// <summary>
    ///     Gets the sentence that says what the last copy or the last unknown card did; empty before one.
    /// </summary>
    public string Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Status" /> has a sentence.
    /// </summary>
    public bool HasStatus => _status.Length > 0;

    /// <summary>
    ///     Gets the command that shows a card; its parameter is the identifier of the card.
    /// </summary>
    public IRelayCommand<string> NavigateCommand { get; }

    /// <summary>
    ///     Gets the command that copies the text of an example to the clipboard; its parameter is the example.
    /// </summary>
    public IAsyncRelayCommand<KnowledgeExampleViewModel> CopyExampleCommand { get; }

    /// <summary>
    ///     Gets the command that closes the browser.
    /// </summary>
    public IRelayCommand CloseCommand { get; }

    /// <summary>
    ///     Shows a card: opens the cards above it in the tree, selects it there, and reads what the window shows from the
    ///     catalog.
    /// </summary>
    /// <param name="id">The identifier or path of the card.</param>
    /// <returns><see langword="true" /> when the card was shown; <see langword="false" /> when the catalog has no such card.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id" /> is <see langword="null" />.</exception>
    public bool Select(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!_nodesById.TryGetValue(id, out var node))
        {
            var card = _catalog.Find(id);
            if (card is null || !_nodesById.TryGetValue(card.Id, out node))
            {
                Status = $"There is no card '{id}'.";
                return false;
            }
        }

        // Going to the card that is shown again reads it again, which clears the status
        if (ReferenceEquals(node, _selectedNode))
        {
            ShowNode(node);
        }
        else
        {
            SelectedNode = node;
        }

        return true;
    }

    /// <summary>
    ///     Makes the nodes of a list of cards and of the cards below them.
    /// </summary>
    /// <param name="cards">The cards, in order.</param>
    /// <param name="path">The identifiers of the cards above, which keep a catalog that loops from looping the tree.</param>
    /// <returns>The nodes, in order.</returns>
    private List<KnowledgeNodeViewModel> BuildNodes(IReadOnlyList<KnowledgeCard> cards, HashSet<string> path)
    {
        var nodes = new List<KnowledgeNodeViewModel>(cards.Count);
        foreach (var card in cards)
        {
            if (!path.Add(card.Id))
            {
                continue;
            }

            var node = new KnowledgeNodeViewModel(card, BuildNodes(_catalog.GetChildren(card), path));
            _nodesById.TryAdd(card.Id, node);
            nodes.Add(node);
            path.Remove(card.Id);
        }

        return nodes;
    }

    /// <summary>
    ///     Shows the card of a node: opens the nodes above it and reads what the window shows.
    /// </summary>
    /// <param name="node">The node.</param>
    private void ShowNode(KnowledgeNodeViewModel node)
    {
        // The cards above are opened so that the node is in view in the tree
        var parentId = node.Card.ParentId;
        while (parentId is not null && _nodesById.TryGetValue(parentId, out var parent))
        {
            parent.IsExpanded = true;
            parentId = parent.Card.ParentId;
        }

        var card = node.Card;
        _selectedCard = card;
        _blocks = KnowledgeMarkdown.Parse(card.Body);
        _children = _catalog.GetChildren(card).Select(ToLink).ToList();
        _seeAlso = _catalog.GetSeeAlso(card).Select(ToLink).ToList();
        _examples = card.Examples.Select(example => new KnowledgeExampleViewModel(example, CopyExampleCommand)).ToList();
        Status = string.Empty;

        OnPropertyChanged(nameof(SelectedCard));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CardTitle));
        OnPropertyChanged(nameof(CardSummary));
        OnPropertyChanged(nameof(TagsText));
        OnPropertyChanged(nameof(AliasesText));
        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(HasAliases));
        OnPropertyChanged(nameof(Blocks));
        OnPropertyChanged(nameof(Children));
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(SeeAlso));
        OnPropertyChanged(nameof(HasSeeAlso));
        OnPropertyChanged(nameof(Examples));
        OnPropertyChanged(nameof(HasExamples));
    }

    /// <summary>
    ///     Makes the link to a card.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The link.</returns>
    private KnowledgeLinkViewModel ToLink(KnowledgeCard card)
    {
        return new KnowledgeLinkViewModel(card.Id, card.Title, card.Summary, NavigateCommand);
    }

    /// <summary>
    ///     Runs the search for the text of the search box, or shows the tree when the text is too short.
    /// </summary>
    private void RunSearch()
    {
        var text = _searchText.Trim();
        if (text.Length < MinimumSearchLength)
        {
            IsSearching = false;
            Results = [];
            return;
        }

        Results = _search.Search(text)
            .Select(hit => new KnowledgeLinkViewModel(hit.Id, hit.Title, hit.Summary, NavigateCommand))
            .ToList();
        IsSearching = true;
    }

    /// <summary>
    ///     Shows the card of the command parameter.
    /// </summary>
    /// <param name="id">The identifier of the card.</param>
    private void NavigateTo(string? id)
    {
        if (id is not null)
        {
            Select(id);
        }
    }

    /// <summary>
    ///     Copies the text of an example to the clipboard and says what happened.
    /// </summary>
    /// <param name="example">The example.</param>
    /// <returns>A task that completes when the text was copied or the clipboard was found out of reach.</returns>
    private async Task CopyExampleAsync(KnowledgeExampleViewModel? example)
    {
        if (example is null)
        {
            return;
        }

        var copied = false;
        var clipboard = Clipboard;
        if (clipboard is not null)
        {
            try
            {
                copied = await clipboard.SetTextAsync(example.Content);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The system clipboard is shared with other programs and can fail in ways that differ by platform
                _logger?.Log(LogLevel.Warning, $"Copying the example '{example.Name}' failed: {ex.Message}");
            }
        }

        Status = copied ? $"Copied example '{example.Name}'." : "Could not reach the clipboard.";
    }
}

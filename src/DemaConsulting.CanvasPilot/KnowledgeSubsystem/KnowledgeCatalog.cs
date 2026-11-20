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

using System.Reflection;
using DemaConsulting.CanvasNet.Viewer.Help;

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     The drawing knowledge of CanvasPilot: the help cards of CanvasNet that tell how to write each drawing format,
///     without the cards about what the program that hosts CanvasNet controls, together with the cards of CanvasPilot
///     itself about working in a project.
/// </summary>
/// <remarks>
///     <para>
///         The catalog is built by extending the CanvasNet catalog with the cards that are embedded in this assembly and
///         then hiding the cards that carry a tag of <see cref="ExcludedTags" />; a catalog that is already filtered
///         cannot be extended, so the order matters. The CanvasNet types are not part of the interface of the unit: every
///         card is copied into a <see cref="KnowledgeCard" />, with each link of its body written as the identifier of a
///         card, so that what uses the knowledge does not depend on CanvasNet's help types. The catalog is immutable
///         once built and safe for concurrent use.
///     </para>
/// </remarks>
public sealed class KnowledgeCatalog : IKnowledgeCatalog
{
    /// <summary>
    ///     The prefix of the names of the embedded resources that hold the cards and examples of CanvasPilot.
    /// </summary>
    public const string ResourcePrefix = "DemaConsulting.CanvasPilot.Knowledge";

    /// <summary>
    ///     The lazily built catalog of the application.
    /// </summary>
    private static readonly Lazy<KnowledgeCatalog> DefaultCatalog = new(CreateDefault);

    /// <summary>
    ///     The cards by identifier.
    /// </summary>
    private readonly Dictionary<string, KnowledgeCard> _byId;

    /// <summary>
    ///     The catalog that finds cards by path.
    /// </summary>
    private readonly HelpCatalog _help;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KnowledgeCatalog" /> class.
    /// </summary>
    /// <param name="help">The filtered help catalog to take the cards from.</param>
    private KnowledgeCatalog(HelpCatalog help)
    {
        _help = help;

        // The nearest visible card above each card: a card whose parent is hidden is a root of the filtered catalog
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var card in help.Cards)
        {
            foreach (var child in help.GetChildren(card))
            {
                parents[child.Id] = card.Id;
            }
        }

        var cards = help.Cards
            .Select(card => CopyCard(help, card, parents.GetValueOrDefault(card.Id)))
            .ToList();
        Cards = cards.AsReadOnly();
        _byId = cards.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Roots = cards.Where(c => c.ParentId is null).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Gets the tags of the cards that are left out: <c>hosting</c>, which CanvasNet puts on the cards about what the
    ///     program that hosts it controls, such as the options of a render, file access, and the command-line tool.
    /// </summary>
    /// <remarks>
    ///     CanvasPilot is that program and sets those options itself, and the assistant writes files, so the cards would
    ///     only take room in the prompt.
    /// </remarks>
    public static IReadOnlyList<string> ExcludedTags { get; } = Array.AsReadOnly([HelpTags.Hosting]);

    /// <summary>
    ///     Gets the catalog of the application: the cards of CanvasNet and of CanvasPilot, without the excluded ones. It is
    ///     built on first use, once, and shared.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an embedded card is malformed; the message names the file.</exception>
    public static KnowledgeCatalog Default => DefaultCatalog.Value;

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeCard> Cards { get; }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeCard> Roots { get; }

    /// <inheritdoc />
    public KnowledgeCard? Find(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _help.Find(path) is { } card ? _byId[card.Id] : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeCard> GetChildren(KnowledgeCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.ChildIds.Select(id => _byId[id]).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeCard> GetSeeAlso(KnowledgeCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.SeeAlsoIds.Select(id => _byId[id]).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeExampleReference> FindExamples(string name, string? scopeCardId = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        // A name with a card in front of it selects that card's tree
        var exampleName = name.Trim();
        var scopes = new List<string>();
        var hash = exampleName.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            var card = Find(exampleName[..hash]);
            if (card is null)
            {
                return [];
            }

            scopes.Add(card.Id);
            exampleName = exampleName[(hash + 1)..].Trim();
        }

        if (scopeCardId is not null)
        {
            var card = Find(scopeCardId);
            if (card is null)
            {
                return [];
            }

            scopes.Add(card.Id);
        }

        var found = new List<KnowledgeExampleReference>();
        foreach (var card in Cards.Where(c => scopes.All(s => IsInTree(c.Id, s))))
        {
            found.AddRange(card.Examples
                .Where(e => string.Equals(e.Name, exampleName, StringComparison.OrdinalIgnoreCase))
                .Select(e => new KnowledgeExampleReference(card.Id, e)));
        }

        return found.AsReadOnly();
    }

    /// <summary>
    ///     Creates a catalog from a help catalog and the cards of an assembly.
    /// </summary>
    /// <param name="library">The unfiltered help catalog to start from.</param>
    /// <param name="extensionAssembly">The assembly that holds the embedded cards to add.</param>
    /// <param name="resourcePrefix">The prefix of the names of those resources.</param>
    /// <param name="excludedTags">The tags of the cards to leave out.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when a tag is not a tag of the help cards.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a card is malformed or does not fit the tree.</exception>
    internal static KnowledgeCatalog Create(
        HelpCatalog library,
        Assembly extensionAssembly,
        string resourcePrefix,
        IEnumerable<string> excludedTags)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(extensionAssembly);
        ArgumentNullException.ThrowIfNull(resourcePrefix);
        ArgumentNullException.ThrowIfNull(excludedTags);

        // Extend first and filter second, because a filtered catalog cannot be extended
        return new KnowledgeCatalog(library
            .Extend(extensionAssembly, resourcePrefix)
            .Filter(null, excludedTags));
    }

    /// <summary>
    ///     Creates the catalog of the application.
    /// </summary>
    /// <returns>The catalog.</returns>
    private static KnowledgeCatalog CreateDefault()
    {
        return Create(HelpCatalog.Default, typeof(KnowledgeCatalog).Assembly, ResourcePrefix, ExcludedTags);
    }

    /// <summary>
    ///     Gets whether a card is a card, or below the card, of a tree.
    /// </summary>
    /// <param name="cardId">The identifier of the card.</param>
    /// <param name="treeId">The identifier of the card at the top of the tree.</param>
    /// <returns><see langword="true" /> when the card is in the tree.</returns>
    private static bool IsInTree(string cardId, string treeId)
    {
        return cardId.Equals(treeId, StringComparison.Ordinal)
               || cardId.StartsWith(treeId + "/", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Copies one help card, with the links of its body written as identifiers.
    /// </summary>
    /// <param name="help">The help catalog the card is from.</param>
    /// <param name="card">The help card.</param>
    /// <param name="parentId">The identifier of the nearest visible card above it, or <see langword="null" />.</param>
    /// <returns>The card.</returns>
    private static KnowledgeCard CopyCard(HelpCatalog help, HelpCard card, string? parentId)
    {
        var body = help.FormatMarkdown(card, WriteLink);
        var examples = card.Examples
            .Select(e => new KnowledgeExample(e.Name, e.Kind, e.Language, e.FileName, e.Description, e.Content))
            .ToList();
        return new KnowledgeCard(
            card.Id,
            card.Name,
            parentId,
            card.Title,
            card.Summary,
            card.Tags.ToList().AsReadOnly(),
            card.Aliases.ToList().AsReadOnly(),
            body,
            help.GetChildren(card).Select(c => c.Id).ToList().AsReadOnly(),
            help.GetSeeAlso(card).Select(c => c.Id).ToList().AsReadOnly(),
            examples.AsReadOnly());
    }

    /// <summary>
    ///     Writes a link of a card body: a card as its identifier in code quotes, and an example as its name and the card
    ///     that holds it.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>The text.</returns>
    private static string WriteLink(HelpLink link)
    {
        return link.ExampleName is { } example
            ? $"example `{example}` of `{link.ExampleCardId}`"
            : $"`{link.CardId}`";
    }
}

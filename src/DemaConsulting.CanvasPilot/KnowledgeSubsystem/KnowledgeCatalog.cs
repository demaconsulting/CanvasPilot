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
///     The drawing knowledge of CanvasPilot: the help cards of CanvasNet that tell how to write each drawing format and
///     how to compose pictures, without the few cards that are only about the program that hosts CanvasNet, together
///     with the cards of CanvasPilot itself about working in a project.
/// </summary>
/// <remarks>
///     <para>
///         The catalog is built by extending the CanvasNet catalog with the cards that are embedded in this assembly and
///         then leaving out the cards of <see cref="ExcludedCardIds" />. A card is left out by its identifier and not by
///         a tag, because the tag <c>hosting</c> of CanvasNet is also on cards that a drawing needs (the formats that are
///         embedded as pages, the file references, and the fragments that set a view or a param). What is left out is
///         kept consistent: a card below a card that is left out moves up to the nearest card that is offered, a link
///         to a card that is left out is written as its title, and the see-also list names offered cards only. The
///         CanvasNet types are not part of the interface of the unit: every card is copied into a
///         <see cref="KnowledgeCard" />, with each link of its body written as the identifier of a card, so that what
///         uses the knowledge does not depend on CanvasNet's help types. The catalog is immutable once built and safe
///         for concurrent use.
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
    ///     The catalog that finds cards by path, with every card, also those that are left out.
    /// </summary>
    private readonly HelpCatalog _help;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KnowledgeCatalog" /> class.
    /// </summary>
    /// <param name="help">The help catalog, extended and not filtered, to take the cards from.</param>
    /// <param name="excluded">The identifiers of the cards to leave out.</param>
    private KnowledgeCatalog(HelpCatalog help, HashSet<string> excluded)
    {
        _help = help;

        // The offered cards below a card: a card that is left out is replaced by the offered cards below it
        IEnumerable<HelpCard> OfferedBelow(IEnumerable<HelpCard> cards)
        {
            foreach (var card in cards)
            {
                if (excluded.Contains(card.Id))
                {
                    foreach (var below in OfferedBelow(help.GetChildren(card)))
                    {
                        yield return below;
                    }
                }
                else
                {
                    yield return card;
                }
            }
        }

        // The nearest offered card above each card: a card whose parent is left out moves up to the next one
        var offered = help.Cards.Where(c => !excluded.Contains(c.Id)).ToList();
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var card in offered)
        {
            foreach (var child in OfferedBelow(help.GetChildren(card)))
            {
                parents[child.Id] = card.Id;
            }
        }

        var titles = help.Cards.ToDictionary(c => c.Id, c => c.Title, StringComparer.Ordinal);
        var cards = offered
            .Select(card => CopyCard(
                help,
                card,
                parents.GetValueOrDefault(card.Id),
                OfferedBelow(help.GetChildren(card)).Select(c => c.Id).ToList(),
                help.GetSeeAlso(card).Where(c => !excluded.Contains(c.Id)).Select(c => c.Id).ToList(),
                link => WriteLink(link, excluded, titles)))
            .ToList();
        Cards = cards.AsReadOnly();
        _byId = cards.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Roots = cards.Where(c => c.ParentId is null).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Gets the identifiers of the CanvasNet cards that are left out, which are only about what the program that hosts
    ///     CanvasNet controls: the options of a render in the host's code, strict mode and the exceptions of a failure,
    ///     and flattening a drawing for other SVG readers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         CanvasPilot is that program: it renders the drawings itself, and the assistant writes files and cannot call
    ///         those operations, so the cards would only take room in the prompt. The other cards of CanvasNet that carry
    ///         the tag <c>hosting</c> are offered, because they tell how a drawing embeds a page of a document, a raster
    ///         picture, or a file, and how a link sets a view or a param. The rationale of each identifier is in the design
    ///         of this unit.
    ///     </para>
    /// </remarks>
    public static IReadOnlyList<string> ExcludedCardIds { get; } = Array.AsReadOnly(
    [
        "errors",
        "mermaid/options",
        "plantuml/options",
        "scad/options",
        "svg/flatten",
        "svg/options",
        "vega-lite/options"
    ]);

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
        return _help.Find(path) is { } card && _byId.TryGetValue(card.Id, out var offered) ? offered : null;
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
    /// <param name="excludedCardIds">The identifiers of the cards to leave out.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when an identifier is not the identifier of a card.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a card is malformed or does not fit the tree.</exception>
    internal static KnowledgeCatalog Create(
        HelpCatalog library,
        Assembly extensionAssembly,
        string resourcePrefix,
        IEnumerable<string> excludedCardIds)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(extensionAssembly);
        ArgumentNullException.ThrowIfNull(resourcePrefix);
        ArgumentNullException.ThrowIfNull(excludedCardIds);

        // The catalog is extended and not filtered, because a filtered catalog cannot be extended and a filter works by
        // tags only; the cards are left out here, by identifier
        var extended = library.Extend(extensionAssembly, resourcePrefix);
        var excluded = new HashSet<string>(excludedCardIds, StringComparer.Ordinal);
        var known = extended.Cards.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var unknown = excluded.Where(id => id is null || !known.Contains(id)).Select(id => id ?? "null").FirstOrDefault();
        if (unknown is not null)
        {
            throw new ArgumentException(
                $"The identifier '{unknown}' is not the identifier of a card.",
                nameof(excludedCardIds));
        }

        return new KnowledgeCatalog(extended, excluded);
    }

    /// <summary>
    ///     Creates the catalog of the application.
    /// </summary>
    /// <returns>The catalog.</returns>
    private static KnowledgeCatalog CreateDefault()
    {
        return Create(HelpCatalog.Default, typeof(KnowledgeCatalog).Assembly, ResourcePrefix, ExcludedCardIds);
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
    /// <param name="parentId">The identifier of the nearest offered card above it, or <see langword="null" />.</param>
    /// <param name="childIds">The identifiers of the offered cards below it.</param>
    /// <param name="seeAlsoIds">The identifiers of the offered cards related to it.</param>
    /// <param name="writeLink">The writer of a link of the body.</param>
    /// <returns>The card.</returns>
    private static KnowledgeCard CopyCard(
        HelpCatalog help,
        HelpCard card,
        string? parentId,
        List<string> childIds,
        List<string> seeAlsoIds,
        Func<HelpLink, string> writeLink)
    {
        var body = help.FormatMarkdown(card, writeLink);
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
            childIds.AsReadOnly(),
            seeAlsoIds.AsReadOnly(),
            examples.AsReadOnly());
    }

    /// <summary>
    ///     Writes a link of a card body: a card as its identifier in code quotes, and an example as its name and the card
    ///     that holds it. A card that is left out is written as its title, and an example of such a card as "an example".
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="excluded">The identifiers of the cards that are left out.</param>
    /// <param name="titles">The title of every card by identifier.</param>
    /// <returns>The text.</returns>
    private static string WriteLink(HelpLink link, HashSet<string> excluded, Dictionary<string, string> titles)
    {
        if (link.ExampleName is { } example)
        {
            return link.ExampleCardId is { } owner && !excluded.Contains(owner)
                ? $"example `{example}` of `{owner}`"
                : "an example";
        }

        return link.CardId is { } card && !excluded.Contains(card) ? $"`{card}`" : titles[link.CardId!];
    }
}

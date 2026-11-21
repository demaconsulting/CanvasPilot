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
///     A small catalog of cards that tests make by hand, so that the units that use a knowledge catalog are tested
///     without the cards of CanvasNet.
/// </summary>
internal sealed class FakeKnowledgeCatalog : IKnowledgeCatalog
{
    /// <summary>
    ///     The cards by identifier.
    /// </summary>
    private readonly Dictionary<string, KnowledgeCard> _byId;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeKnowledgeCatalog" /> class.
    /// </summary>
    /// <param name="cards">The cards, depth first; the parents and children are as the cards say.</param>
    public FakeKnowledgeCatalog(params KnowledgeCard[] cards)
    {
        Cards = cards;
        _byId = cards.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Roots = cards.Where(c => c.ParentId is null).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeCard> Cards { get; }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeCard> Roots { get; }

    /// <summary>
    ///     Makes a card.
    /// </summary>
    /// <param name="id">The identifier; the card is below the card of the identifier without its last segment.</param>
    /// <param name="title">The title.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="tags">The tags.</param>
    /// <param name="aliases">The other names.</param>
    /// <param name="body">The body.</param>
    /// <param name="children">The identifiers of the children.</param>
    /// <param name="seeAlso">The identifiers of the related cards.</param>
    /// <param name="examples">The examples.</param>
    /// <returns>The card.</returns>
    public static KnowledgeCard Card(
        string id,
        string title,
        string summary = "A summary.",
        string[]? tags = null,
        string[]? aliases = null,
        string body = "A body.",
        string[]? children = null,
        string[]? seeAlso = null,
        KnowledgeExample[]? examples = null)
    {
        var slash = id.LastIndexOf('/');
        return new KnowledgeCard(
            id,
            id[(slash + 1)..],
            slash < 0 ? null : id[..slash],
            title,
            summary,
            tags ?? ["authoring"],
            aliases ?? [],
            body,
            children ?? [],
            seeAlso ?? [],
            examples ?? []);
    }

    /// <summary>
    ///     Makes an example.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="content">The text.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="language">The language.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>The example.</returns>
    public static KnowledgeExample Example(
        string name,
        string content = "<svg/>",
        string kind = "source",
        string language = "xml",
        string? fileName = "a.svg")
    {
        return new KnowledgeExample(name, kind, language, fileName, "Shows " + name + ".", content);
    }

    /// <inheritdoc />
    public KnowledgeCard? Find(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split(['/', ' '], StringSplitOptions.RemoveEmptyEntries);
        var id = string.Join('/', segments).ToLowerInvariant();
        if (_byId.TryGetValue(id, out var byId))
        {
            return byId;
        }

        return segments.Length == 1
            ? Cards.FirstOrDefault(c => c.ParentId is null && c.Aliases.Contains(id, StringComparer.Ordinal))
            : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeCard> GetChildren(KnowledgeCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.ChildIds.Select(id => _byId[id]).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeCard> GetSeeAlso(KnowledgeCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.SeeAlsoIds.Select(id => _byId[id]).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<KnowledgeExampleReference> FindExamples(string name, string? scopeCardId = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        var parts = name.Split('#', 2);
        var exampleName = parts[^1];
        var cardId = parts.Length == 2 ? parts[0] : scopeCardId;
        return Cards
            .Where(c => cardId is null || c.Id == cardId || c.Id.StartsWith(cardId + "/", StringComparison.Ordinal))
            .SelectMany(c => c.Examples.Where(e => e.Name == exampleName).Select(e => new KnowledgeExampleReference(c.Id, e)))
            .ToList();
    }
}

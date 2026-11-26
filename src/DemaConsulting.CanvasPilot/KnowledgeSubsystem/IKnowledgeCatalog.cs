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

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     The drawing knowledge that the drawing assistant can look things up in: a tree of
///     cards, each with a body, related cards, and examples. It is a seam, so that what searches the cards and builds
///     prompts from them can be tested on small catalogs.
/// </summary>
public interface IKnowledgeCatalog
{
    /// <summary>
    ///     Gets every card, depth first from the roots.
    /// </summary>
    IReadOnlyList<KnowledgeCard> Cards { get; }

    /// <summary>
    ///     Gets the cards that have no card above them, in order.
    /// </summary>
    IReadOnlyList<KnowledgeCard> Roots { get; }

    /// <summary>
    ///     Finds a card by its path.
    /// </summary>
    /// <param name="path">
    ///     The path: segments separated by <c>/</c> or white space, each the name or an alias of a card below the
    ///     previous one, ignoring case.
    /// </param>
    /// <returns>The card, or <see langword="null" /> when the path names no card.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path" /> is <see langword="null" />.</exception>
    KnowledgeCard? Find(string path);

    /// <summary>
    ///     Gets the cards below a card.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The cards, in order; empty for a leaf.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card" /> is <see langword="null" />.</exception>
    IReadOnlyList<KnowledgeCard> GetChildren(KnowledgeCard card);

    /// <summary>
    ///     Gets the related cards of a card.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The cards, in order; empty when there are none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card" /> is <see langword="null" />.</exception>
    IReadOnlyList<KnowledgeCard> GetSeeAlso(KnowledgeCard card);

    /// <summary>
    ///     Finds the examples of a name.
    /// </summary>
    /// <param name="name">
    ///     The name of the example, or <c>card-id#name</c> to name the card as well; the card is found by path, so its
    ///     aliases work.
    /// </param>
    /// <param name="scopeCardId">
    ///     The card whose tree to search (the card and the cards below it), or <see langword="null" /> to search every card.
    /// </param>
    /// <returns>Every example of that name in the scope, in the order of the cards; empty when there is none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <see langword="null" />.</exception>
    IReadOnlyList<KnowledgeExampleReference> FindExamples(string name, string? scopeCardId = null);
}

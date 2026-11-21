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

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     One card in the tree of the browser, with the cards below it. The tree shows the title; whether the node is
///     open is kept here so that going to a card from a link can open the cards above it.
/// </summary>
public sealed partial class KnowledgeNodeViewModel : ObservableObject
{
    /// <summary>
    ///     Whether the cards below the node are shown.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KnowledgeNodeViewModel" /> class.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="children">The nodes of the cards below the card, in order.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public KnowledgeNodeViewModel(KnowledgeCard card, IReadOnlyList<KnowledgeNodeViewModel> children)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(children);

        Card = card;
        Children = children;
    }

    /// <summary>
    ///     Gets the card.
    /// </summary>
    public KnowledgeCard Card { get; }

    /// <summary>
    ///     Gets the nodes of the cards below the card, in order; empty for a leaf.
    /// </summary>
    public IReadOnlyList<KnowledgeNodeViewModel> Children { get; }

    /// <summary>
    ///     Gets the identifier of the card.
    /// </summary>
    public string Id => Card.Id;

    /// <summary>
    ///     Gets the title of the card, which is what the tree shows.
    /// </summary>
    public string Title => Card.Title;

    /// <summary>
    ///     Gets the one-line summary of the card, which the tree shows as a tip.
    /// </summary>
    public string Summary => Card.Summary;
}

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

using System.Text;

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     Writes the knowledge cards as plain text for the prompt of an assistant: a compact index of every card, one card
///     with its related cards and examples, one example, and the result of a search. The text is the same for the same
///     cards.
/// </summary>
/// <remarks>
///     <para>
///         The index is the part that is given to the assistant in every prompt, so its size is limited:
///         <see cref="IndexBudgetCharacters" /> characters for the catalog of the application, which the tests of the unit
///         check, so that a new card of CanvasNet or of CanvasPilot that makes the index too large fails the build. The
///         assistant asks for the cards it needs, one at a time. The text has no markup other than the Markdown of the
///         cards themselves, line feeds as line breaks, and no dependency on the culture. The unit holds no state and
///         is safe for concurrent use.
///     </para>
/// </remarks>
public sealed class KnowledgePromptBuilder
{
    /// <summary>
    ///     The most characters that the index of the catalog of the application may have.
    /// </summary>
    public const int IndexBudgetCharacters = 12_000;

    /// <summary>
    ///     The first line of the index: what the cards are and how to ask for one.
    /// </summary>
    internal const string IndexHeader =
        "CanvasNet drawing knowledge. Ask for a card with the canvasnet_help tool (topic = id), an example (example = name or id#name), or a search (query = words).";

    /// <summary>
    ///     The line that is written when a search finds no card.
    /// </summary>
    internal const string NoCardsFound = "No card matches.";

    /// <summary>
    ///     The catalog whose cards are written.
    /// </summary>
    private readonly IKnowledgeCatalog _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KnowledgePromptBuilder" /> class.
    /// </summary>
    /// <param name="catalog">The catalog whose cards are written.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog" /> is <see langword="null" />.</exception>
    public KnowledgePromptBuilder(IKnowledgeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>
    ///     Writes the index of the cards: a line that says how to ask for a card and then one line for each card, in the
    ///     order of the tree, as <c>- id: title - summary</c>.
    /// </summary>
    /// <param name="maxDepth">
    ///     The deepest level of cards to list: zero lists only the cards at the top, one the cards below them as well, and so
    ///     on. The default lists every card.
    /// </param>
    /// <returns>The index, without a final line feed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDepth" /> is negative.</exception>
    public string BuildIndex(int maxDepth = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var builder = new StringBuilder(IndexHeader);
        foreach (var root in _catalog.Roots)
        {
            AppendIndexLines(builder, root, 0, maxDepth);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Writes one card: its title, identifier, summary, tags, other names, body, the cards below it and the cards related
    ///     to it, and its examples.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="includeExamples">
    ///     <see langword="true" /> to write the text of each example after the list of the examples; <see langword="false" />
    ///     to write only the list, with the name, kind, language, file, and description of each.
    /// </param>
    /// <returns>The text of the card, without a final line feed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card" /> is <see langword="null" />.</exception>
    public string BuildCard(KnowledgeCard card, bool includeExamples = false)
    {
        ArgumentNullException.ThrowIfNull(card);

        var builder = new StringBuilder();
        builder.Append("# ").Append(card.Title).Append('\n');
        builder.Append("id: ").Append(card.Id).Append('\n');
        builder.Append(card.Summary).Append('\n');
        if (card.Tags.Count > 0)
        {
            builder.Append("Tags: ").Append(string.Join(", ", card.Tags)).Append('\n');
        }

        if (card.Aliases.Count > 0)
        {
            builder.Append("Also known as: ").Append(string.Join(", ", card.Aliases)).Append('\n');
        }

        builder.Append('\n').Append(card.Body.TrimEnd('\n'));
        var related = BuildRelated(card);
        if (related.Length > 0)
        {
            builder.Append("\n\n").Append(related);
        }

        if (card.Examples.Count > 0)
        {
            builder.Append("\n\n## Examples\n");
            builder.Append(string.Join('\n', card.Examples.Select(DescribeExample)));
            if (includeExamples)
            {
                foreach (var example in card.Examples)
                {
                    builder.Append("\n\n").Append(BuildExample(card.Id, example));
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Writes one example: a line that names it and the card it is from, and its text in a fenced block of its language.
    /// </summary>
    /// <param name="cardId">The identifier of the card that holds the example.</param>
    /// <param name="example">The example.</param>
    /// <returns>The text, without a final line feed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public static string BuildExample(string cardId, KnowledgeExample example)
    {
        ArgumentNullException.ThrowIfNull(cardId);
        ArgumentNullException.ThrowIfNull(example);

        // The fence is longer than any run of backticks in the text, so that the text cannot end the block
        var fence = new string('`', Math.Max(3, LongestRunOfBackticks(example.Content) + 1));
        var details = example.FileName is null
            ? $"{example.Kind}, {example.Language}"
            : $"{example.Kind}, {example.Language}, {example.FileName}";
        var builder = new StringBuilder();
        builder.Append("Example ").Append(example.Name).Append(" of card ").Append(cardId)
            .Append(" (").Append(details).Append("): ").Append(example.Description).Append('\n');
        builder.Append(fence).Append(example.Language).Append('\n');
        builder.Append(example.Content.TrimEnd('\n')).Append('\n');
        builder.Append(fence);
        return builder.ToString();
    }

    /// <summary>
    ///     Writes the cards below a card and the cards related to it, each as <c>- id: title - summary</c>.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>
    ///     The sections <c>## Cards</c> and <c>## See also</c>, each only when it has a card, separated by a blank line;
    ///     empty when the card has neither.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card" /> is <see langword="null" />.</exception>
    public string BuildRelated(KnowledgeCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var sections = new List<string>();
        var children = _catalog.GetChildren(card);
        if (children.Count > 0)
        {
            sections.Add("## Cards\n" + string.Join('\n', children.Select(DescribeCard)));
        }

        var related = _catalog.GetSeeAlso(card);
        if (related.Count > 0)
        {
            sections.Add("## See also\n" + string.Join('\n', related.Select(DescribeCard)));
        }

        return string.Join("\n\n", sections);
    }

    /// <summary>
    ///     Writes the result of a search: a line that says what follows and then one line for each card, best first, as
    ///     <c>- id: title - summary</c>.
    /// </summary>
    /// <param name="results">The cards that a search found.</param>
    /// <returns>The text, without a final line feed; a single line when there is no card.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="results" /> is <see langword="null" />.</exception>
    public static string BuildSearchResults(IReadOnlyList<KnowledgeSearchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return results.Count == 0
            ? NoCardsFound
            : "Cards found, best first:\n" + string.Join('\n', results.Select(r => $"- {r.Id}: {r.Title} - {r.Summary}"));
    }

    /// <summary>
    ///     Writes the line of a card for the index and for the lists of related cards.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The line: <c>- id: title - summary</c>.</returns>
    private static string DescribeCard(KnowledgeCard card)
    {
        return $"- {card.Id}: {card.Title} - {card.Summary}";
    }

    /// <summary>
    ///     Writes the line of an example for the list of the examples of a card.
    /// </summary>
    /// <param name="example">The example.</param>
    /// <returns>The line: <c>- name [kind, language] file: description</c>.</returns>
    private static string DescribeExample(KnowledgeExample example)
    {
        var file = example.FileName is null ? string.Empty : " " + example.FileName;
        return $"- {example.Name} [{example.Kind}, {example.Language}]{file}: {example.Description}";
    }

    /// <summary>
    ///     Gets the length of the longest run of backticks in a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The length; zero when there is none.</returns>
    private static int LongestRunOfBackticks(string text)
    {
        var longest = 0;
        var run = 0;
        foreach (var c in text)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    /// <summary>
    ///     Appends the line of a card and, depth first, the lines of the cards below it, down to a depth.
    /// </summary>
    /// <param name="builder">The text that is built.</param>
    /// <param name="card">The card.</param>
    /// <param name="depth">The depth of the card; zero for a card at the top.</param>
    /// <param name="maxDepth">The deepest level to write.</param>
    private void AppendIndexLines(StringBuilder builder, KnowledgeCard card, int depth, int maxDepth)
    {
        builder.Append('\n').Append(DescribeCard(card));
        if (depth >= maxDepth)
        {
            return;
        }

        foreach (var child in _catalog.GetChildren(card))
        {
            AppendIndexLines(builder, child, depth + 1, maxDepth);
        }
    }
}

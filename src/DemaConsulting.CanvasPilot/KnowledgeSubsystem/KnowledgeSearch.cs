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
///     Searches the cards of a knowledge catalog by words, with a score that is the same for the same input.
/// </summary>
/// <remarks>
///     <para>
///         The query and the text of each card are cut into tokens: runs of letters and digits, in lower case, with one
///         trailing <c>s</c> removed from a token of more than three characters, so that the plural of a word matches
///         the word. Each token of the query is scored once against every card, by the best field it matches; a card
///         that is matched by every token of the query gets a bonus, and a card whose name, other name, or identifier
///         is the whole query gets a larger one. The weights are fixed (see the constants), nothing depends on the
///         culture or on the order of a dictionary, and the cards that tie are ordered by how deep they are and then by
///         identifier, so the same query gives the same list every time. The body of a card is not searched, because it
///         would make the result noisy; the words of the title, summary, tags, other names, identifier, and example
///         names carry what a card is about. The search holds an index that it builds from the catalog when it is
///         created and is safe for concurrent use.
///     </para>
/// </remarks>
public sealed class KnowledgeSearch
{
    /// <summary>
    ///     The number of results of a search when the caller does not say.
    /// </summary>
    public const int DefaultMaxResults = 8;

    /// <summary>
    ///     The score of a card whose name, other name, or identifier is the whole query, given once.
    /// </summary>
    private const int WholeQueryScore = 100;

    /// <summary>
    ///     The bonus of a card that every token of the query matches.
    /// </summary>
    private const int AllTokensBonus = 50;

    /// <summary>
    ///     The score of a token of the query that is a token of an other name of the card.
    /// </summary>
    private const int AliasExactScore = 40;

    /// <summary>
    ///     The score of a token of the query that starts a token of an other name of the card.
    /// </summary>
    private const int AliasPrefixScore = 20;

    /// <summary>
    ///     The score of a token of the query that is a token of the identifier of the card.
    /// </summary>
    private const int IdExactScore = 40;

    /// <summary>
    ///     The score of a token of the query that starts a token of the identifier of the card.
    /// </summary>
    private const int IdPrefixScore = 20;

    /// <summary>
    ///     The score of a token of the query that is a token of the name of an example of the card.
    /// </summary>
    private const int ExampleExactScore = 25;

    /// <summary>
    ///     The score of a token of the query that starts a token of the name of an example of the card.
    /// </summary>
    private const int ExamplePrefixScore = 12;

    /// <summary>
    ///     The score of a token of the query that is a token of the title of the card.
    /// </summary>
    private const int TitleExactScore = 30;

    /// <summary>
    ///     The score of a token of the query that starts a token of the title of the card.
    /// </summary>
    private const int TitlePrefixScore = 15;

    /// <summary>
    ///     The score of a token of the query that is a token of a tag of the card.
    /// </summary>
    private const int TagExactScore = 20;

    /// <summary>
    ///     The score of a token of the query that is a token of the summary of the card.
    /// </summary>
    private const int SummaryExactScore = 10;

    /// <summary>
    ///     The score of a token of the query that starts a token of the summary of the card.
    /// </summary>
    private const int SummaryPrefixScore = 5;

    /// <summary>
    ///     The shortest token of the query that can match the start of a longer token.
    /// </summary>
    private const int MinimumPrefixLength = 3;

    /// <summary>
    ///     The longest token that keeps a trailing <c>s</c>; a longer token loses one.
    /// </summary>
    private const int StemLength = 3;

    /// <summary>
    ///     The indexed cards, in the order of the catalog.
    /// </summary>
    private readonly IReadOnlyList<IndexedCard> _cards;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KnowledgeSearch" /> class.
    /// </summary>
    /// <param name="catalog">The catalog to search; its cards are read once, now.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog" /> is <see langword="null" />.</exception>
    public KnowledgeSearch(IKnowledgeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _cards = catalog.Cards.Select(Index).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Searches the cards.
    /// </summary>
    /// <param name="query">The words to search for.</param>
    /// <param name="maxResults">The most cards to return.</param>
    /// <returns>
    ///     The cards that match, best first, at most <paramref name="maxResults" />; empty when the query holds no word or
    ///     no card matches.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxResults" /> is less than one.</exception>
    public IReadOnlyList<KnowledgeSearchResult> Search(string query, int maxResults = DefaultMaxResults)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxResults, 1);

        var tokens = Tokenize(query).Distinct(StringComparer.Ordinal).ToList();
        if (tokens.Count == 0)
        {
            return [];
        }

        var wholeKeys = WholeQueryKeys(query);
        return _cards
            .Select(card => (card, score: Score(card, tokens, wholeKeys)))
            .Where(r => r.score > 0)
            .OrderByDescending(r => r.score)
            .ThenBy(r => r.card.Depth)
            .ThenBy(r => r.card.Card.Id, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(r => new KnowledgeSearchResult(r.card.Card.Id, r.card.Card.Title, r.card.Card.Summary, r.score))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    ///     Cuts a text into tokens: runs of letters and digits in lower case, with one trailing <c>s</c> removed from a
    ///     token of more than three characters.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The tokens, in order.</returns>
    internal static IEnumerable<string> Tokenize(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                continue;
            }

            if (builder.Length > 0)
            {
                yield return Stem(builder.ToString());
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            yield return Stem(builder.ToString());
        }
    }

    /// <summary>
    ///     Removes one trailing <c>s</c> from a token of more than three characters.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The token for matching.</returns>
    private static string Stem(string token)
    {
        return token.Length > StemLength && token[^1] == 's' ? token[..^1] : token;
    }

    /// <summary>
    ///     Gets the forms of a query that a name, other name, or identifier is compared with: the query in lower case, with
    ///     white space kept, with each run of it written as a dash, and with each run of it written as a slash.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The forms; empty when the query is blank.</returns>
    private static HashSet<string> WholeQueryKeys(string query)
    {
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [];
        }

        var lower = words.Select(w => w.ToLowerInvariant()).ToArray();
        return new HashSet<string>(StringComparer.Ordinal)
        {
            string.Join(' ', lower),
            string.Join('-', lower),
            string.Join('/', lower)
        };
    }

    /// <summary>
    ///     Builds the index entry of a card.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The entry.</returns>
    private static IndexedCard Index(KnowledgeCard card)
    {
        return new IndexedCard(
            card,
            card.Id.Count(c => c == '/'),
            [.. card.Aliases.SelectMany(Tokenize)],
            [.. Tokenize(card.Id)],
            [.. card.Examples.SelectMany(e => Tokenize(e.Name))],
            [.. Tokenize(card.Title)],
            [.. card.Tags.SelectMany(Tokenize)],
            [.. Tokenize(card.Summary)],
            new HashSet<string>(card.Aliases.Select(a => a.ToLowerInvariant()).Append(card.Id.ToLowerInvariant()).Append(card.Name.ToLowerInvariant()), StringComparer.Ordinal));
    }

    /// <summary>
    ///     Scores a card against the tokens of a query.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="tokens">The tokens of the query, without repeats.</param>
    /// <param name="wholeKeys">The forms of the whole query.</param>
    /// <returns>The score; zero when no token matches.</returns>
    private static int Score(IndexedCard card, IReadOnlyList<string> tokens, HashSet<string> wholeKeys)
    {
        var total = 0;
        var matched = 0;
        foreach (var token in tokens)
        {
            var best = Math.Max(
                Math.Max(
                    Math.Max(Match(card.AliasTokens, token, AliasExactScore, AliasPrefixScore), Match(card.IdTokens, token, IdExactScore, IdPrefixScore)),
                    Math.Max(Match(card.ExampleTokens, token, ExampleExactScore, ExamplePrefixScore), Match(card.TitleTokens, token, TitleExactScore, TitlePrefixScore))),
                Math.Max(Match(card.TagTokens, token, TagExactScore, 0), Match(card.SummaryTokens, token, SummaryExactScore, SummaryPrefixScore)));
            if (best > 0)
            {
                total += best;
                matched++;
            }
        }

        if (matched == 0)
        {
            return 0;
        }

        if (matched == tokens.Count)
        {
            total += AllTokensBonus;
        }

        if (wholeKeys.Overlaps(card.WholeKeys))
        {
            total += WholeQueryScore;
        }

        return total;
    }

    /// <summary>
    ///     Scores a token of the query against the tokens of one field.
    /// </summary>
    /// <param name="fieldTokens">The tokens of the field.</param>
    /// <param name="token">The token of the query.</param>
    /// <param name="exactScore">The score when the token is a token of the field.</param>
    /// <param name="prefixScore">The score when the token starts a token of the field, or zero for no prefix match.</param>
    /// <returns>The score, zero when the token does not match.</returns>
    private static int Match(string[] fieldTokens, string token, int exactScore, int prefixScore)
    {
        if (Array.IndexOf(fieldTokens, token) >= 0)
        {
            return exactScore;
        }

        return prefixScore > 0
               && token.Length >= MinimumPrefixLength
               && fieldTokens.Any(f => f.StartsWith(token, StringComparison.Ordinal))
            ? prefixScore
            : 0;
    }

    /// <summary>
    ///     A card with its tokens, ready to be scored.
    /// </summary>
    /// <param name="Card">The card.</param>
    /// <param name="Depth">The number of slashes in the identifier.</param>
    /// <param name="AliasTokens">The tokens of the other names.</param>
    /// <param name="IdTokens">The tokens of the identifier.</param>
    /// <param name="ExampleTokens">The tokens of the names of the examples.</param>
    /// <param name="TitleTokens">The tokens of the title.</param>
    /// <param name="TagTokens">The tokens of the tags.</param>
    /// <param name="SummaryTokens">The tokens of the summary.</param>
    /// <param name="WholeKeys">The identifier, name, and other names, in lower case.</param>
    private sealed record IndexedCard(
        KnowledgeCard Card,
        int Depth,
        string[] AliasTokens,
        string[] IdTokens,
        string[] ExampleTokens,
        string[] TitleTokens,
        string[] TagTokens,
        string[] SummaryTokens,
        HashSet<string> WholeKeys);
}

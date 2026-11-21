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
///     Unit tests for <see cref="KnowledgeSearch" />, on small catalogs of cards made by hand, so that each weight can
///     be checked, and on the catalog of the application, so that the questions a user asks find the right cards.
/// </summary>
public sealed class KnowledgeSearchTests
{
    /// <summary>
    ///     Makes a search over cards.
    /// </summary>
    /// <param name="cards">The cards.</param>
    /// <returns>The search.</returns>
    private static KnowledgeSearch SearchOver(params KnowledgeCard[] cards)
    {
        return new KnowledgeSearch(new FakeKnowledgeCatalog(cards));
    }

    /// <summary>
    ///     Validates that a card that has the query as another name outranks a card that has it in its summary, and that
    ///     the scores are those of the weights: an other name, the bonus for all tokens, and the whole query.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_ExactAliasOutranksSummaryHit()
    {
        // Arrange: one card that is called foo, and one that mentions foo in its summary
        var search = SearchOver(
            FakeKnowledgeCatalog.Card("beta", "Beta card", "Mentions foo in the summary."),
            FakeKnowledgeCatalog.Card("alpha", "Alpha", "Something else.", aliases: ["foo"]));

        // Act
        var results = search.Search("foo");

        // Assert: alias 40 + all tokens 50 + whole query 100 for the first; summary 10 + all tokens 50 for the second
        Assert.Equal(["alpha", "beta"], results.Select(r => r.Id));
        Assert.Equal(190, results[0].Score);
        Assert.Equal(60, results[1].Score);
        Assert.Equal("Alpha", results[0].Title);
        Assert.Equal("Something else.", results[0].Summary);
    }

    /// <summary>
    ///     Validates that a token of the query that ends in <c>s</c> matches the same word without it, in both directions.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_PluralAndSingularWords_MatchEachOther()
    {
        // Arrange: a card of one diagram and a card of many
        var search = SearchOver(
            FakeKnowledgeCatalog.Card("one", "A flow diagram"),
            FakeKnowledgeCatalog.Card("many", "Many flow diagrams"));

        // Act
        var plural = search.Search("diagrams");
        var singular = search.Search("diagram");

        // Assert: both cards match both queries with the same score: title 30 + all tokens 50
        Assert.Equal(["many", "one"], plural.Select(r => r.Id));
        Assert.All(plural, r => Assert.Equal(80, r.Score));
        Assert.Equal(["many", "one"], singular.Select(r => r.Id));
        Assert.All(singular, r => Assert.Equal(80, r.Score));
    }

    /// <summary>
    ///     Validates that a token of three characters or fewer keeps its <c>s</c>.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_ShortTokensKeepTheirS()
    {
        // Arrange: a card about the format svg and a card about the word sv
        var search = SearchOver(
            FakeKnowledgeCatalog.Card("a", "The svg format"),
            FakeKnowledgeCatalog.Card("b", "The sv format"));

        // Act
        var results = search.Search("svg");

        // Assert: the token svg is not cut to sv, and sv is not a prefix match because it is shorter than the query
        Assert.Equal(["a"], results.Select(r => r.Id));
    }

    /// <summary>
    ///     Validates that a card that every token of the query matches gets the bonus, and one that only some tokens match
    ///     does not.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_CardThatEveryTokenMatchesGetsTheBonus()
    {
        // Arrange: a card that holds both words and a card with a heavier match of one word
        var search = SearchOver(
            FakeKnowledgeCatalog.Card("both", "Red and blue"),
            FakeKnowledgeCatalog.Card("red", "Colors", aliases: ["red"]));

        // Act
        var results = search.Search("red blue");

        // Assert: title 30 + title 30 + bonus 50 against an other name 40 without the bonus
        Assert.Equal(["both", "red"], results.Select(r => r.Id));
        Assert.Equal(110, results[0].Score);
        Assert.Equal(40, results[1].Score);
    }

    /// <summary>
    ///     Validates that a word that is repeated in the query counts once.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_RepeatedWordsInTheQuery_CountOnce()
    {
        // Arrange: a card with the word in its title
        var search = SearchOver(FakeKnowledgeCatalog.Card("x", "Gradient fills"));

        // Act
        var once = search.Search("gradient");
        var thrice = search.Search("Gradient gradient GRADIENT");

        // Assert: the same score: title 30 + all tokens 50
        Assert.Equal(80, once[0].Score);
        Assert.Equal(80, thrice[0].Score);
    }

    /// <summary>
    ///     Validates that the name of an example of a card finds the card, by the word and by the start of the word.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_ExampleNameHit_FindsTheCard()
    {
        // Arrange: a card with an example named for a gradient band
        var search = SearchOver(
            FakeKnowledgeCatalog.Card("svg/style", "Styles", examples: [FakeKnowledgeCatalog.Example("gradient-band")]));

        // Act
        var exact = search.Search("gradient");
        var prefix = search.Search("grad");
        var tooShort = search.Search("gr");

        // Assert: example 25 + all tokens 50; prefix 12 + all tokens 50; two characters are not a prefix
        Assert.Equal(75, exact[0].Score);
        Assert.Equal(62, prefix[0].Score);
        Assert.Empty(tooShort);
    }

    /// <summary>
    ///     Validates the weights of the title, tag, summary, and identifier, and that a tag has no prefix match.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_FieldWeights_AreAsSpecified()
    {
        // Arrange: one card for each field, with words that no other field has
        var search = SearchOver(
            FakeKnowledgeCatalog.Card("a", "Flowchart", "Plain.", ["authoring"]),
            FakeKnowledgeCatalog.Card("b", "Plain", "Plain.", ["design"]),
            FakeKnowledgeCatalog.Card("c", "Plain", "A swimlane summary."),
            FakeKnowledgeCatalog.Card("d/zebra", "Plain", "Plain."));

        // Act
        var titlePrefix = search.Search("flow");
        var tag = search.Search("design");
        var tagPrefix = search.Search("author");
        var summaryPrefix = search.Search("swim");
        var id = search.Search("zebra");

        // Assert: title prefix 15, tag 20, no tag prefix, summary prefix 5, identifier 40; each with the bonus of 50, and
        // the identifier also has the whole-query score of 100, because the query is the name of the card
        Assert.Equal(65, Assert.Single(titlePrefix).Score);
        Assert.Equal(70, Assert.Single(tag).Score);
        Assert.Empty(tagPrefix);
        Assert.Equal(55, Assert.Single(summaryPrefix).Score);
        Assert.Equal(190, Assert.Single(id).Score);
    }

    /// <summary>
    ///     Validates that a query that is the identifier of a card, written with slashes, spaces, or dashes, gets the
    ///     whole-query score once.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_WholeIdentifierAsQuery_GetsTheWholeQueryScore()
    {
        // Arrange: a card and its parent
        var search = SearchOver(
            FakeKnowledgeCatalog.Card("svg", "SVG", children: ["svg/framed-figure"]),
            FakeKnowledgeCatalog.Card("svg/framed-figure", "Framed figures"));

        // Act
        var bySlash = search.Search("svg/framed-figure");
        var bySpace = search.Search("SVG Framed-Figure");

        // Assert: three identifier tokens 3 x 40 + all tokens 50 + whole query 100; the parent matches one token of three
        Assert.Equal("svg/framed-figure", bySlash[0].Id);
        Assert.Equal(270, bySlash[0].Score);
        Assert.Equal(40, bySlash[1].Score);
        Assert.Equal(270, bySpace[0].Score);
    }

    /// <summary>
    ///     Validates that cards with the same score are ordered by the depth of their identifier and then by identifier.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_Ties_AreOrderedByDepthThenIdentifier()
    {
        // Arrange: four cards with the same title, given in an order that is neither by depth nor by identifier
        var search = SearchOver(
            FakeKnowledgeCatalog.Card("zeta", "Common", children: ["zeta/deep"]),
            FakeKnowledgeCatalog.Card("zeta/deep", "Common"),
            FakeKnowledgeCatalog.Card("alpha", "Common", children: ["alpha/child"]),
            FakeKnowledgeCatalog.Card("alpha/child", "Common"));

        // Act
        var results = search.Search("common");

        // Assert: the same score, shallow cards first, then identifiers in ordinal order
        Assert.All(results, r => Assert.Equal(80, r.Score));
        Assert.Equal(["alpha", "zeta", "alpha/child", "zeta/deep"], results.Select(r => r.Id));
    }

    /// <summary>
    ///     Validates that a blank query finds nothing, and that a missing query and a number of results below one are
    ///     refused.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_BlankNullAndBadMaxResults_AreHandled()
    {
        // Arrange: a search over one card
        var search = SearchOver(FakeKnowledgeCatalog.Card("a", "Alpha"));

        // Act / Assert: blank and punctuation-only queries are empty, null and a zero count are refused
        Assert.Empty(search.Search(string.Empty));
        Assert.Empty(search.Search("   "));
        Assert.Empty(search.Search("/-- !!"));
        Assert.Throws<ArgumentNullException>(() => search.Search(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => search.Search("alpha", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => search.Search("alpha", -1));
        Assert.Throws<ArgumentNullException>(() => new KnowledgeSearch(null!));
    }

    /// <summary>
    ///     Validates that the number of results is limited, with the best first, and that the default is eight.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_MaxResults_LimitsTheListToTheBestCards()
    {
        // Arrange: ten cards that match, the third with a better match
        var cards = Enumerable.Range(0, 10)
            .Select(i => FakeKnowledgeCatalog.Card($"card{i}", "Common thing", aliases: i == 3 ? ["common"] : null))
            .ToArray();
        var search = SearchOver(cards);

        // Act
        var two = search.Search("common", 2);
        var normal = search.Search("common");
        var all = search.Search("common", 100);

        // Assert: the best card first, the limit is respected, and the default is eight
        Assert.Equal(["card3", "card0"], two.Select(r => r.Id));
        Assert.Equal(KnowledgeSearch.DefaultMaxResults, normal.Count);
        Assert.Equal(8, KnowledgeSearch.DefaultMaxResults);
        Assert.Equal(10, all.Count);
    }

    /// <summary>
    ///     Validates that the same query gives the same list every time, and that two searches over the same cards agree.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_SameQueryTwice_GivesTheSameResults()
    {
        // Arrange: two searches over the catalog of the application
        var first = new KnowledgeSearch(KnowledgeCatalog.Default);
        var second = new KnowledgeSearch(KnowledgeCatalog.Default);

        // Act
        var a = first.Search("class diagram colors", 20);
        var b = first.Search("class diagram colors", 20);
        var c = second.Search("class diagram colors", 20);

        // Assert: the lists are equal value by value
        Assert.NotEmpty(a);
        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    /// <summary>
    ///     Validates that a card of the catalog of the application is the best answer to its own identifier.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_IdentifierOfEveryCard_FindsThatCardFirst()
    {
        // Arrange: the catalog of the application
        var search = new KnowledgeSearch(KnowledgeCatalog.Default);

        // Act / Assert: each card is the first result for its own identifier
        Assert.All(KnowledgeCatalog.Default.Cards, card => Assert.Equal(card.Id, search.Search(card.Id)[0].Id));
    }

    /// <summary>
    ///     Validates that the questions that a user or an assistant asks find the right cards of the application.
    /// </summary>
    [Fact]
    public void KnowledgeSearch_Search_RealCatalog_FindsTheCardsForCommonQuestions()
    {
        // Arrange: the catalog of the application
        var search = new KnowledgeSearch(KnowledgeCatalog.Default);

        // Act
        var gradient = search.Search("gradient", 3);
        var classDiagram = search.Search("class diagram");
        var framed = search.Search("framed callout");
        var gantt = search.Search("gantt");
        var marks = search.Search("marks");
        var sequence = search.Search("sequence");
        var files = search.Search("where do files go", 3);

        // Assert: the card that the question is about is first, or among the first for a shared name
        Assert.Contains(gradient, r => r.Id == "svg/style");
        Assert.Equal("svg/framed-figure", framed[0].Id);
        Assert.Equal(["mermaid/class", "plantuml/class"], classDiagram.Take(2).Select(r => r.Id));
        Assert.Equal("mermaid/gantt", gantt[0].Id);
        Assert.Equal("canvaspilot/marks", marks[0].Id);
        Assert.Equal(["mermaid/sequence", "plantuml/sequence"], sequence.Take(2).Select(r => r.Id));
        Assert.Equal("canvaspilot/project-layout", files[0].Id);
        Assert.All(new[] { gradient, classDiagram, framed, gantt, marks, sequence, files }.SelectMany(r => r), r => Assert.True(r.Score > 0));
    }
}

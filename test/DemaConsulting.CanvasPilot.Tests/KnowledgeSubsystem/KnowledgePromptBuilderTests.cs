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
///     Unit tests for <see cref="KnowledgePromptBuilder" />, on a small catalog made by hand so that the exact text can be
///     checked, and on the catalog of the application, so that the size of the index is kept within its budget.
/// </summary>
public sealed class KnowledgePromptBuilderTests
{
    /// <summary>
    ///     The card at the top of the small catalog: it has children, related cards, tags, other names, and examples.
    /// </summary>
    private static readonly KnowledgeCard Top = FakeKnowledgeCatalog.Card(
        "top",
        "The top",
        "Top summary.",
        ["authoring", "design"],
        ["first", "start"],
        "Body of top.\nSee `top/child`.\n",
        ["top/child"],
        ["other"],
        [
            FakeKnowledgeCatalog.Example("sketch", "<svg/>\n", "snippet", "xml", "sketch.svg"),
            FakeKnowledgeCatalog.Example("run", "tool --go", "command", "shell", null)
        ]);

    /// <summary>
    ///     The card below the top card.
    /// </summary>
    private static readonly KnowledgeCard Child = FakeKnowledgeCatalog.Card("top/child", "The child", "Child summary.", children: ["top/child/leaf"]);

    /// <summary>
    ///     The card below the child.
    /// </summary>
    private static readonly KnowledgeCard Leaf = FakeKnowledgeCatalog.Card("top/child/leaf", "The leaf", "Leaf summary.");

    /// <summary>
    ///     A second card at the top, related to the first.
    /// </summary>
    private static readonly KnowledgeCard Other = FakeKnowledgeCatalog.Card("other", "Another one", "Other summary.");

    /// <summary>
    ///     Makes a builder over the small catalog.
    /// </summary>
    /// <returns>The builder.</returns>
    private static KnowledgePromptBuilder MakeBuilder()
    {
        return new KnowledgePromptBuilder(new FakeKnowledgeCatalog(Top, Child, Leaf, Other));
    }

    /// <summary>
    ///     Validates that the index is the header and one line for each card in tree order.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildIndex_SmallCatalog_IsHeaderAndOneLinePerCardInTreeOrder()
    {
        // Arrange: the small catalog
        var builder = MakeBuilder();

        // Act
        var index = builder.BuildIndex();

        // Assert: exactly the header and the four lines, with line feeds and no final line feed
        Assert.Equal(
            KnowledgePromptBuilder.IndexHeader + "\n"
            + "- top: The top - Top summary.\n"
            + "- top/child: The child - Child summary.\n"
            + "- top/child/leaf: The leaf - Leaf summary.\n"
            + "- other: Another one - Other summary.",
            index);
        Assert.StartsWith("CanvasNet drawing knowledge.", index, StringComparison.Ordinal);
        Assert.Contains("canvasnet_help", index, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a depth limits the index to the cards down to that level.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildIndex_MaxDepth_LimitsTheLevels()
    {
        // Arrange: the small catalog
        var builder = MakeBuilder();

        // Act
        var rootsOnly = builder.BuildIndex(0).Split('\n').Skip(1).ToList();
        var oneLevel = builder.BuildIndex(1).Split('\n').Skip(1).ToList();

        // Assert: depth zero lists the two cards at the top, depth one adds the child
        Assert.Equal(["- top: The top - Top summary.", "- other: Another one - Other summary."], rootsOnly);
        Assert.Equal(
            ["- top: The top - Top summary.", "- top/child: The child - Child summary.", "- other: Another one - Other summary."],
            oneLevel);
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.BuildIndex(-1));
    }

    /// <summary>
    ///     Validates that an empty catalog has an index that is only the header, and that a missing catalog is refused.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildIndex_EmptyCatalogAndNullCatalog_AreHandled()
    {
        // Arrange: a builder over no cards
        var builder = new KnowledgePromptBuilder(new FakeKnowledgeCatalog());

        // Act / Assert: the index is the header; a missing catalog is refused
        Assert.Equal(KnowledgePromptBuilder.IndexHeader, builder.BuildIndex());
        Assert.Throws<ArgumentNullException>(() => new KnowledgePromptBuilder(null!));
    }

    /// <summary>
    ///     Validates that the index of the catalog of the application is within the budget that keeps it affordable in
    ///     every prompt, and that the overview of the top cards is within its smaller budget.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildIndex_RealCatalog_FitsBudget()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;
        var builder = new KnowledgePromptBuilder(catalog);

        // Act
        var index = builder.BuildIndex();
        var overview = builder.BuildIndex(0);

        // Assert: the whole index fits the budget, the overview of the top cards is small, and every card has a line
        Assert.Equal(12_000, KnowledgePromptBuilder.IndexBudgetCharacters);
        Assert.True(
            index.Length <= KnowledgePromptBuilder.IndexBudgetCharacters,
            $"The index has {index.Length} characters; the budget is {KnowledgePromptBuilder.IndexBudgetCharacters}.");
        Assert.True(overview.Length <= 2_500, $"The overview has {overview.Length} characters; the budget is 2500.");
        Assert.Equal(catalog.Cards.Count + 1, index.Split('\n').Length);
        Assert.Equal(catalog.Roots.Count + 1, overview.Split('\n').Length);
        Assert.All(catalog.Cards, card => Assert.Contains($"\n- {card.Id}: {card.Title} - {card.Summary}", index, StringComparison.Ordinal));
        Assert.DoesNotContain("\r", index, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the exact text of a card without the text of its examples.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildCard_WithoutExamples_IsTheCardWithListsOfRelatedCardsAndExamples()
    {
        // Arrange: the small catalog
        var builder = MakeBuilder();

        // Act
        var text = builder.BuildCard(Top);

        // Assert: heading, identifier, summary, tags, other names, body, children, related cards, and the list of examples
        Assert.Equal(
            "# The top\n"
            + "id: top\n"
            + "Top summary.\n"
            + "Tags: authoring, design\n"
            + "Also known as: first, start\n"
            + "\n"
            + "Body of top.\nSee `top/child`.\n"
            + "\n"
            + "## Cards\n"
            + "- top/child: The child - Child summary.\n"
            + "\n"
            + "## See also\n"
            + "- other: Another one - Other summary.\n"
            + "\n"
            + "## Examples\n"
            + "- sketch [snippet, xml] sketch.svg: Shows sketch.\n"
            + "- run [command, shell]: Shows run.",
            text);
    }

    /// <summary>
    ///     Validates that the text of the examples is written after the list when it is asked for.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildCard_WithExamples_AddsEachExampleInAFencedBlock()
    {
        // Arrange: the small catalog
        var builder = MakeBuilder();

        // Act
        var without = builder.BuildCard(Top);
        var with = builder.BuildCard(Top, includeExamples: true);

        // Assert: the text with examples starts with the text without them and then holds both examples
        Assert.StartsWith(without, with, StringComparison.Ordinal);
        Assert.EndsWith(
            "\n\nExample sketch of card top (snippet, xml, sketch.svg): Shows sketch.\n```xml\n<svg/>\n```"
            + "\n\nExample run of card top (command, shell): Shows run.\n```shell\ntool --go\n```",
            with,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a card with no tags, other names, children, related cards, or examples has only its heading,
    ///     identifier, summary, and body.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildCard_BareCard_HasOnlyTheParts()
    {
        // Arrange: the small catalog and a card without optional parts
        var builder = MakeBuilder();
        var bare = new KnowledgeCard("bare", "bare", null, "Bare", "Bare summary.", [], [], "Just a body.", [], [], []);

        // Act
        var text = builder.BuildCard(bare, includeExamples: true);

        // Assert: nothing but the parts it has
        Assert.Equal("# Bare\nid: bare\nBare summary.\n\nJust a body.", text);
        Assert.Throws<ArgumentNullException>(() => builder.BuildCard(null!));
    }

    /// <summary>
    ///     Validates the exact text of an example, with and without a file name, and that a text that holds a fence gets a
    ///     longer fence.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildExample_FileCommandAndTextWithFence_AreWrittenInAFencedBlock()
    {
        // Arrange: an example whose text has a run of three backticks
        var tricky = FakeKnowledgeCatalog.Example("tricky", "before\n```\ncode\n```\nafter", "snippet", "markdown", "t.md");

        // Act
        var file = KnowledgePromptBuilder.BuildExample("top", Top.Examples[0]);
        var command = KnowledgePromptBuilder.BuildExample("top", Top.Examples[1]);
        var fenced = KnowledgePromptBuilder.BuildExample("top", tricky);

        // Assert: the header line names the example, its card, and its details; the block uses the language of the example
        Assert.Equal("Example sketch of card top (snippet, xml, sketch.svg): Shows sketch.\n```xml\n<svg/>\n```", file);
        Assert.Equal("Example run of card top (command, shell): Shows run.\n```shell\ntool --go\n```", command);
        Assert.Equal(
            "Example tricky of card top (snippet, markdown, t.md): Shows tricky.\n````markdown\nbefore\n```\ncode\n```\nafter\n````",
            fenced);
        Assert.Throws<ArgumentNullException>(() => KnowledgePromptBuilder.BuildExample(null!, Top.Examples[0]));
        Assert.Throws<ArgumentNullException>(() => KnowledgePromptBuilder.BuildExample("top", null!));
    }

    /// <summary>
    ///     Validates the cards below a card and the cards related to it, and that nothing is written for a card that has
    ///     neither.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildRelated_ChildrenAndSeeAlso_AreListedInSections()
    {
        // Arrange: the small catalog
        var builder = MakeBuilder();

        // Act
        var both = builder.BuildRelated(Top);
        var childrenOnly = builder.BuildRelated(Child);
        var none = builder.BuildRelated(Leaf);

        // Assert: the sections that exist, separated by a blank line; empty when there are none
        Assert.Equal(
            "## Cards\n- top/child: The child - Child summary.\n\n## See also\n- other: Another one - Other summary.",
            both);
        Assert.Equal("## Cards\n- top/child/leaf: The leaf - Leaf summary.", childrenOnly);
        Assert.Equal(string.Empty, none);
        Assert.Throws<ArgumentNullException>(() => builder.BuildRelated(null!));
    }

    /// <summary>
    ///     Validates the text of the result of a search, with cards and with none.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildSearchResults_WithAndWithoutCards_AreListedInOrder()
    {
        // Arrange: results of a search
        var results = new[]
        {
            new KnowledgeSearchResult("top/child", "The child", "Child summary.", 90),
            new KnowledgeSearchResult("other", "Another one", "Other summary.", 40)
        };

        // Act
        var found = KnowledgePromptBuilder.BuildSearchResults(results);
        var none = KnowledgePromptBuilder.BuildSearchResults([]);

        // Assert: one line for each card, best first, the score is not shown, and a line says when there is none
        Assert.Equal(
            "Cards found, best first:\n- top/child: The child - Child summary.\n- other: Another one - Other summary.",
            found);
        Assert.Equal("No card matches.", none);
        Assert.Throws<ArgumentNullException>(() => KnowledgePromptBuilder.BuildSearchResults(null!));
    }

    /// <summary>
    ///     Validates that in the text of a card of the application the links are the identifiers of cards and the names of
    ///     examples, as the catalog wrote them, and that the same card gives the same text twice.
    /// </summary>
    [Fact]
    public void KnowledgePromptBuilder_BuildCard_RealCatalog_KeepsLinkTextAndIsRepeatable()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;
        var builder = new KnowledgePromptBuilder(catalog);
        var marks = catalog.Find("canvaspilot/marks");
        var framed = catalog.Find("svg/framed-figure");
        Assert.NotNull(marks);
        Assert.NotNull(framed);

        // Act
        var first = builder.BuildCard(marks, includeExamples: true);
        var second = builder.BuildCard(marks, includeExamples: true);
        var framedText = builder.BuildCard(framed);

        // Assert: the card is the same twice, names the example, writes it in a block, and keeps the links of the body
        Assert.Equal(first, second);
        Assert.Contains("# Reading marks and attachments in a message", first, StringComparison.Ordinal);
        Assert.Contains("Example mark-payload of card canvaspilot/marks (snippet, json, mark-payload.json):", first, StringComparison.Ordinal);
        Assert.Contains("```json\n{", first, StringComparison.Ordinal);
        Assert.Contains("`positions/geometry`", first, StringComparison.Ordinal);
        Assert.Contains("example `framed` of `svg/framed-figure`", framedText, StringComparison.Ordinal);
        Assert.Contains("## Examples\n- framed [snippet, xml] framed.svg:", framedText, StringComparison.Ordinal);
        Assert.DoesNotContain("[[", framedText.Replace("[[fork]]", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }
}

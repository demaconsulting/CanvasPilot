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

using System.Text.RegularExpressions;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="KnowledgeSkills" />, on a small catalog made by hand so that the exact skills can be
///     checked, and on the catalog of the application, so that the names are proved to be unique.
/// </summary>
public sealed partial class KnowledgeSkillsTests
{
    /// <summary>
    ///     The card at the top of the small catalog: it has a body, a child, and two examples.
    /// </summary>
    private static readonly KnowledgeCard Top = FakeKnowledgeCatalog.Card(
        "top",
        "The top",
        "Top summary.",
        body: "Body of top.\nSee `top/child`.\n",
        children: ["top/child"],
        examples:
        [
            FakeKnowledgeCatalog.Example("sketch", "<svg/>\n", "snippet", "xml", "sketch.svg"),
            FakeKnowledgeCatalog.Example("run", "tool --go", "command", "shell", null)
        ]);

    /// <summary>
    ///     The card below the top card; it has no example.
    /// </summary>
    private static readonly KnowledgeCard Child = FakeKnowledgeCatalog.Card("top/child", "The child", "Child summary.");

    /// <summary>
    ///     A second card at the top.
    /// </summary>
    private static readonly KnowledgeCard Other = FakeKnowledgeCatalog.Card("other-card", "Another one", "Other summary.");

    /// <summary>
    ///     Gets the pattern of a name that a provider can use: lower case letters, digits, and single dashes.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("^canvasnet-[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidName();

    /// <summary>
    ///     Validates that the name of a skill is made from the identifier of the card, for a card at the top, a card below
    ///     another, a card of CanvasPilot, and a card with a dash in its name, and that a missing identifier is refused.
    /// </summary>
    [Fact]
    public void KnowledgeSkills_NameFor_CardId_IsPrefixAndIdWithDashes()
    {
        // Arrange / Act / Assert: the prefix, then the identifier with each slash written as a dash
        Assert.Equal("canvasnet-", KnowledgeSkills.NamePrefix);
        Assert.Equal("canvasnet-svg", KnowledgeSkills.NameFor("svg"));
        Assert.Equal("canvasnet-svg-framed-figure", KnowledgeSkills.NameFor("svg/framed-figure"));
        Assert.Equal("canvasnet-canvaspilot-marks", KnowledgeSkills.NameFor("canvaspilot/marks"));
        Assert.Equal("canvasnet-vega-lite-params", KnowledgeSkills.NameFor("vega-lite/params"));
        Assert.Throws<ArgumentNullException>(() => KnowledgeSkills.NameFor(null!));
    }

    /// <summary>
    ///     Validates that there is one skill for each card, in the order of the cards, with the name from the identifier,
    ///     the title and summary as the description, and the card with its examples as the content.
    /// </summary>
    [Fact]
    public void KnowledgeSkills_Create_SmallCatalog_IsOneSkillPerCardWithNameDescriptionAndContent()
    {
        // Arrange
        var catalog = new FakeKnowledgeCatalog(Top, Child, Other);
        var prompts = new KnowledgePromptBuilder(catalog);

        // Act
        var skills = KnowledgeSkills.Create(catalog).ToList();

        // Assert: three skills, exactly as described
        Assert.Equal(["canvasnet-top", "canvasnet-top-child", "canvasnet-other-card"], skills.Select(s => s.Name));
        Assert.Equal(["The top - Top summary.", "The child - Child summary.", "Another one - Other summary."], skills.Select(s => s.Description));
        Assert.Equal(prompts.BuildCard(Top, includeExamples: true), skills[0].Content);
        Assert.Equal(prompts.BuildCard(Child, includeExamples: true), skills[1].Content);
        Assert.Equal(prompts.BuildCard(Other, includeExamples: true), skills[2].Content);
    }

    /// <summary>
    ///     Validates that the content of a skill holds the body of the card and the text of each example in a fenced
    ///     block.
    /// </summary>
    [Fact]
    public void KnowledgeSkills_Create_SmallCatalog_ContentHasBodyAndFencedExamples()
    {
        // Arrange
        var catalog = new FakeKnowledgeCatalog(Top, Child, Other);

        // Act
        var content = KnowledgeSkills.Create(catalog).First().Content;

        // Assert: the heading, the body, and both examples as fenced blocks
        Assert.StartsWith("# The top\nid: top\nTop summary.\n", content, StringComparison.Ordinal);
        Assert.Contains("Body of top.\nSee `top/child`.", content, StringComparison.Ordinal);
        Assert.Contains("## Examples\n- sketch [snippet, xml] sketch.svg: Shows sketch.", content, StringComparison.Ordinal);
        Assert.Contains("Example sketch of card top (snippet, xml, sketch.svg): Shows sketch.\n```xml\n<svg/>\n```", content, StringComparison.Ordinal);
        Assert.Contains("Example run of card top (command, shell): Shows run.\n```shell\ntool --go\n```", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the skills are made as they are read: nothing is built when the skills are asked for, and
    ///     reading the first skill builds only that card; and that a missing catalog is refused at the call.
    /// </summary>
    [Fact]
    public void KnowledgeSkills_Create_Enumeration_IsDeferredAndANullCatalogIsRefusedAtTheCall()
    {
        // Arrange: a catalog that counts how often the cards are read
        var catalog = new CountingCatalog(new FakeKnowledgeCatalog(Top, Child, Other));

        // Act
        var skills = KnowledgeSkills.Create(catalog);
        var untouched = catalog.Reads;
        var first = skills.First();
        var afterFirst = catalog.ChildrenReads;
        var all = skills.ToList();

        // Assert: no read at the call, one card's worth after the first skill, every card after all, and a fresh result each time
        Assert.Equal(0, untouched);
        Assert.Equal("canvasnet-top", first.Name);
        Assert.Equal(1, afterFirst);
        Assert.Equal(3, all.Count);
        Assert.Equal(1 + 3, catalog.ChildrenReads);
        Assert.Throws<ArgumentNullException>(() => KnowledgeSkills.Create(null!));
    }

    /// <summary>
    ///     Validates that a catalog without cards has no skill.
    /// </summary>
    [Fact]
    public void KnowledgeSkills_Create_EmptyCatalog_IsEmpty()
    {
        // Arrange / Act / Assert
        Assert.Empty(KnowledgeSkills.Create(new FakeKnowledgeCatalog()));
    }

    /// <summary>
    ///     Validates that the catalog of the application has one skill for each card, with names that a provider can use,
    ///     that no two are the same, and that each has the description and the content of its card.
    /// </summary>
    [Fact]
    public void KnowledgeSkills_Create_RealCatalog_HasOneUniqueSkillForEveryCard()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;

        // Act
        var skills = KnowledgeSkills.Create(catalog).ToList();

        // Assert: as many as the cards, unique valid names, and the card's own text
        Assert.Equal(catalog.Cards.Count, skills.Count);
        Assert.Equal(skills.Count, skills.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(skills, s => Assert.Matches(ValidName(), s.Name));
        Assert.Contains(skills, s => s.Name == "canvasnet-svg-framed-figure");
        Assert.Contains(skills, s => s.Name == "canvasnet-canvaspilot-marks");
        for (var i = 0; i < skills.Count; i++)
        {
            var card = catalog.Cards[i];
            Assert.Equal(KnowledgeSkills.NameFor(card.Id), skills[i].Name);
            Assert.Equal($"{card.Title} - {card.Summary}", skills[i].Description);
            Assert.StartsWith($"# {card.Title}\nid: {card.Id}\n", skills[i].Content, StringComparison.Ordinal);
            Assert.All(card.Examples, e => Assert.Contains(e.Content.TrimEnd('\n'), skills[i].Content, StringComparison.Ordinal));
        }
    }

    /// <summary>
    ///     A catalog that counts how often the cards are read, to prove that the skills are made as they are read.
    /// </summary>
    private sealed class CountingCatalog(IKnowledgeCatalog inner) : IKnowledgeCatalog
    {
        /// <summary>
        ///     Gets how many times the list of cards or of roots was read.
        /// </summary>
        public int Reads { get; private set; }

        /// <summary>
        ///     Gets how many times the cards below a card were read, which is once for each card that is written.
        /// </summary>
        public int ChildrenReads { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeCard> Cards
        {
            get
            {
                Reads++;
                return inner.Cards;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeCard> Roots
        {
            get
            {
                Reads++;
                return inner.Roots;
            }
        }

        /// <inheritdoc />
        public KnowledgeCard? Find(string path) => inner.Find(path);

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeCard> GetChildren(KnowledgeCard card)
        {
            ChildrenReads++;
            return inner.GetChildren(card);
        }

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeCard> GetSeeAlso(KnowledgeCard card) => inner.GetSeeAlso(card);

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeExampleReference> FindExamples(string name, string? scopeCardId = null) =>
            inner.FindExamples(name, scopeCardId);
    }
}

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

using System.Text.Json;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="KnowledgeTools" />, on a small catalog made by hand so that the exact answers can be
///     checked, and on the catalog of the application.
/// </summary>
public sealed class KnowledgeToolsTests
{
    /// <summary>
    ///     The card at the top of the small catalog: it has an alias, a child, and an example whose name only it has.
    /// </summary>
    private static readonly KnowledgeCard Svg = FakeKnowledgeCatalog.Card(
        "svg",
        "SVG drawing",
        "Draw pictures with SVG.",
        aliases: ["vector"],
        children: ["svg/framed-figure"],
        examples: [FakeKnowledgeCatalog.Example("sketch", "<svg/>\n", "source", "xml", "sketch.svg")]);

    /// <summary>
    ///     The card below the first: it has an example that another card also has.
    /// </summary>
    private static readonly KnowledgeCard Framed = FakeKnowledgeCatalog.Card(
        "svg/framed-figure",
        "Framed figure",
        "A figure with a frame and callouts.",
        examples: [FakeKnowledgeCatalog.Example("shared", "<svg>frame</svg>")]);

    /// <summary>
    ///     A second card at the top, with an example of the same name as the card below the first.
    /// </summary>
    private static readonly KnowledgeCard Mermaid = FakeKnowledgeCatalog.Card(
        "mermaid",
        "Mermaid diagrams",
        "Draw flowcharts and sequence diagrams.",
        examples: [FakeKnowledgeCatalog.Example("shared", "graph TD\n", "source", "mermaid", "shared.mmd")]);

    /// <summary>
    ///     Makes the tool over the small catalog, with the writer of texts that the answers are compared with.
    /// </summary>
    /// <param name="prompts">The writer of texts over the same catalog.</param>
    /// <returns>The tool.</returns>
    private static KnowledgeTool MakeTool(out KnowledgePromptBuilder prompts)
    {
        var catalog = new FakeKnowledgeCatalog(Svg, Framed, Mermaid);
        prompts = new KnowledgePromptBuilder(catalog);
        return KnowledgeTools.CreateHelpTool(catalog, new KnowledgeSearch(catalog), prompts);
    }

    /// <summary>
    ///     Validates the fields of the tool: its name, a description that says how to call it, and a schema that is JSON
    ///     with the three arguments; missing collaborators are refused.
    /// </summary>
    [Fact]
    public void KnowledgeTools_CreateHelpTool_Descriptor_NamesTheToolAndItsThreeArguments()
    {
        // Arrange / Act
        var tool = MakeTool(out _);
        using var schema = JsonDocument.Parse(tool.ParametersSchema);
        var properties = schema.RootElement.GetProperty("properties");

        // Assert: the name, the words of the description, and a schema of three strings that allows nothing else
        Assert.Equal("canvasnet_help", KnowledgeTools.HelpToolName);
        Assert.Equal(KnowledgeTools.HelpToolName, tool.Name);
        Assert.Contains("no arguments", tool.Description, StringComparison.Ordinal);
        Assert.All(new[] { "topic", "example", "query" }, name =>
        {
            Assert.Contains(name, tool.Description, StringComparison.Ordinal);
            Assert.Equal("string", properties.GetProperty(name).GetProperty("type").GetString());
        });
        Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        Assert.Equal(3, properties.EnumerateObject().Count());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());

        var catalog = new FakeKnowledgeCatalog(Svg);
        var search = new KnowledgeSearch(catalog);
        var prompts = new KnowledgePromptBuilder(catalog);
        Assert.Throws<ArgumentNullException>(() => KnowledgeTools.CreateHelpTool(null!, search, prompts));
        Assert.Throws<ArgumentNullException>(() => KnowledgeTools.CreateHelpTool(catalog, null!, prompts));
        Assert.Throws<ArgumentNullException>(() => KnowledgeTools.CreateHelpTool(catalog, search, null!));
        Assert.Throws<ArgumentNullException>(() => KnowledgeTools.CreateAll(null!));
    }

    /// <summary>
    ///     Validates that a call without arguments, in every way of saying so, is answered with the index.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{"topic":null,"example":"","query":"  "}""")]
    [InlineData("""{"unknown":"ignored"}""")]
    public void KnowledgeTools_Invoke_NoArguments_AnswersTheIndex(string? arguments)
    {
        // Arrange
        var tool = MakeTool(out var prompts);

        // Act
        var answer = tool.Invoke(arguments!);

        // Assert: the index of the cards, the same as the writer gives
        Assert.Equal(prompts.BuildIndex(), answer);
        Assert.Contains("\n- svg/framed-figure: Framed figure - ", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a topic is answered with the card, found by identifier, by a path with spaces, by an alias, and
    ///     ignoring case, with a line that says how to get its examples.
    /// </summary>
    [Theory]
    [InlineData("svg")]
    [InlineData("SVG")]
    [InlineData("vector")]
    [InlineData("  svg  ")]
    public void KnowledgeTools_Invoke_Topic_AnswersTheCardWithAHintForItsExamples(string topic)
    {
        // Arrange
        var tool = MakeTool(out var prompts);

        // Act
        var answer = tool.Invoke(JsonSerializer.Serialize(new { topic }));

        // Assert: the card as the writer gives it, then the hint for the examples
        Assert.Equal(
            prompts.BuildCard(Svg) + "\n\nTo read the text of an example, call canvasnet_help with example set to its name, or to svg#name.",
            answer);
        Assert.Contains("- sketch [source, xml] sketch.svg: Shows sketch.", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a path of a card below another is found whether it is written with a slash or with a space, and
    ///     that a card without examples has no hint.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_Topic_PathWithSpaceAndCardWithoutExamples_AreFound()
    {
        // Arrange: a catalog with a card that has no example
        var plain = FakeKnowledgeCatalog.Card("plain", "Plain card", "No examples.", children: ["plain/leaf"]);
        var leaf = FakeKnowledgeCatalog.Card("plain/leaf", "The leaf", "A leaf.");
        var catalog = new FakeKnowledgeCatalog(plain, leaf);
        var prompts = new KnowledgePromptBuilder(catalog);
        var tool = KnowledgeTools.CreateHelpTool(catalog, new KnowledgeSearch(catalog), prompts);

        // Act
        var bySlash = tool.Invoke("""{"topic":"plain/leaf"}""");
        var bySpace = tool.Invoke("""{"topic":"Plain Leaf"}""");
        var withoutExamples = tool.Invoke("""{"topic":"plain"}""");

        // Assert: both ways find the leaf, and a card without examples is the card alone
        Assert.Equal(prompts.BuildCard(leaf), bySlash);
        Assert.Equal(bySlash, bySpace);
        Assert.Equal(prompts.BuildCard(plain), withoutExamples);
        Assert.DoesNotContain("To read the text of an example", withoutExamples, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an example that one card has is answered with its text in a fenced block.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_Example_Single_AnswersTheFencedText()
    {
        // Arrange
        var tool = MakeTool(out _);

        // Act
        var answer = tool.Invoke("""{"example":"sketch"}""");

        // Assert: the text of the example
        Assert.Equal(KnowledgePromptBuilder.BuildExample("svg", Svg.Examples[0]), answer);
        Assert.Contains("```xml\n<svg/>\n```", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an example that several cards have is answered with the choices, and that the choices can be
    ///     followed with <c>card-id#name</c> or by naming the card as the topic.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_Example_Ambiguous_ListsChoicesAndCanBeScopedByTopicOrId()
    {
        // Arrange
        var tool = MakeTool(out _);

        // Act
        var ambiguous = tool.Invoke("""{"example":"shared"}""");
        var scoped = tool.Invoke("""{"example":"shared","topic":"mermaid"}""");
        var byId = tool.Invoke("""{"example":"svg/framed-figure#shared"}""");
        var scopedByTree = tool.Invoke("""{"example":"shared","topic":"svg"}""");

        // Assert: both choices are named, and each way of choosing gives the example of that card
        Assert.StartsWith("Several cards have an example named 'shared'. Ask again with one of:", ambiguous, StringComparison.Ordinal);
        Assert.Contains("\n- svg/framed-figure#shared: Shows shared.", ambiguous, StringComparison.Ordinal);
        Assert.Contains("\n- mermaid#shared: Shows shared.", ambiguous, StringComparison.Ordinal);
        Assert.Equal(KnowledgePromptBuilder.BuildExample("mermaid", Mermaid.Examples[0]), scoped);
        Assert.Equal(KnowledgePromptBuilder.BuildExample("svg/framed-figure", Framed.Examples[0]), byId);
        Assert.Equal(byId, scopedByTree);
    }

    /// <summary>
    ///     Validates that an example that no card has is answered with the cards that are closest to its name, and that
    ///     an example of a card that does not exist is answered as an unknown topic.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_Example_Unknown_SuggestsCards()
    {
        // Arrange
        var tool = MakeTool(out _);

        // Act
        var unknownWithHits = tool.Invoke("""{"example":"framed"}""");
        var unknownWithoutHits = tool.Invoke("""{"example":"xylophone"}""");
        var unknownCard = tool.Invoke("""{"example":"sketch","topic":"nothing-here"}""");
        var wrongCard = tool.Invoke("""{"example":"sketch","topic":"mermaid"}""");

        // Assert: the cards that fit the words come first, and the hint for the index closes every answer
        Assert.StartsWith("No example is named 'framed'. Closest cards:\n- svg/framed-figure: Framed figure - ", unknownWithHits, StringComparison.Ordinal);
        Assert.EndsWith("\nCall with no arguments for the full index.", unknownWithHits, StringComparison.Ordinal);
        Assert.Equal("No example is named 'xylophone'. Call with no arguments for the full index.", unknownWithoutHits);
        Assert.Equal("No card matches 'nothing-here'. Call with no arguments for the full index.", unknownCard);
        Assert.StartsWith("No example is named 'sketch'.", wrongCard, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a query is answered with the cards that match, best first, and with a message when none does.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_Query_AnswersTheSearchResults()
    {
        // Arrange
        var tool = MakeTool(out _);

        // Act
        var hit = tool.Invoke("""{"query":"flowcharts"}""");
        var miss = tool.Invoke("""{"query":"xylophone"}""");
        var wins = tool.Invoke("""{"query":"framed","topic":null}""");

        // Assert: the result list of the search, and the message of a miss
        Assert.Equal("Cards found, best first:\n- mermaid: Mermaid diagrams - Draw flowcharts and sequence diagrams.", hit);
        Assert.Equal("No card matches 'xylophone'. Call with no arguments for the full index.", miss);
        Assert.StartsWith("Cards found, best first:\n- svg/framed-figure: ", wins, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a topic that no card has is answered with the closest cards and a hint for the index, and that
    ///     a topic with no word to search for has only the hint.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_UnknownTopic_SuggestsTheClosestCards()
    {
        // Arrange
        var tool = MakeTool(out _);

        // Act
        var near = tool.Invoke("""{"topic":"svg/framed-fig"}""");
        var far = tool.Invoke("""{"topic":"xylophone"}""");
        var punctuation = tool.Invoke("""{"topic":"???"}""");

        // Assert: the cards that fit are listed; nothing found gives the hint alone
        Assert.StartsWith("No card matches 'svg/framed-fig'. Closest cards:\n- svg/framed-figure: Framed figure - ", near, StringComparison.Ordinal);
        Assert.EndsWith("\nCall with no arguments for the full index.", near, StringComparison.Ordinal);
        Assert.Equal("No card matches 'xylophone'. Call with no arguments for the full index.", far);
        Assert.Equal("No card matches '???'. Call with no arguments for the full index.", punctuation);
    }

    /// <summary>
    ///     Validates the order of the arguments: an example comes before a topic, and a topic before a query.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_SeveralArguments_ExampleThenTopicThenQuery()
    {
        // Arrange
        var tool = MakeTool(out var prompts);

        // Act
        var exampleWins = tool.Invoke("""{"example":"sketch","topic":"svg","query":"x"}""");
        var topicWins = tool.Invoke("""{"topic":"mermaid","query":"framed"}""");

        // Assert
        Assert.Equal(KnowledgePromptBuilder.BuildExample("svg", Svg.Examples[0]), exampleWins);
        Assert.StartsWith(prompts.BuildCard(Mermaid), topicWins, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that arguments that cannot be used are answered with a message that names the three arguments, and
    ///     that nothing is thrown.
    /// </summary>
    [Theory]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("[\"svg\"]")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("\"svg\"")]
    [InlineData("""{"topic":1}""")]
    [InlineData("""{"topic":["svg"]}""")]
    [InlineData("""{"example":{"name":"x"}}""")]
    [InlineData("""{"query":true}""")]
    [InlineData("""{"topic":"svg",""")]
    public void KnowledgeTools_Invoke_BadArguments_AnswerAnErrorThatNamesTheArguments(string arguments)
    {
        // Arrange
        var tool = MakeTool(out _);

        // Act
        var answer = tool.Invoke(arguments);

        // Assert: an error that says what to send, with the three arguments named
        Assert.StartsWith("Error: ", answer, StringComparison.Ordinal);
        Assert.Contains("topic", answer, StringComparison.Ordinal);
        Assert.Contains("example", answer, StringComparison.Ordinal);
        Assert.Contains("query", answer, StringComparison.Ordinal);
        Assert.Contains("no arguments", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a very long argument is not copied whole into the answer, and that text that is deeply nested
    ///     or has odd characters is answered and not thrown at.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_UnusualArguments_AreAnsweredAndNeverThrow()
    {
        // Arrange
        var tool = MakeTool(out _);
        var longTopic = new string('a', 5_000);
        var deep = new string('[', 5_000) + new string(']', 5_000);

        // Act
        var longAnswer = tool.Invoke(JsonSerializer.Serialize(new { topic = longTopic }));
        var deepAnswer = tool.Invoke(deep);
        var odd = tool.Invoke(JsonSerializer.Serialize(new { query = "\u0000\uD83D\uDE00 \r\n\t", topic = "\\", example = "#" }));

        // Assert: the long text is cut, the deep text is an error, and the odd text is some answer
        Assert.True(longAnswer.Length < 200, "The answer is " + longAnswer.Length + " characters.");
        Assert.Contains(new string('a', 80) + "...", longAnswer, StringComparison.Ordinal);
        Assert.StartsWith("Error: ", deepAnswer, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(odd));
    }

    /// <summary>
    ///     Validates that a failure inside the catalog is answered with an error message, not thrown.
    /// </summary>
    [Fact]
    public void KnowledgeTools_Invoke_CatalogThatFails_AnswersAnError()
    {
        // Arrange: a catalog that fails every lookup
        var catalog = new FailingCatalog();
        var tool = KnowledgeTools.CreateHelpTool(catalog, new KnowledgeSearch(new FakeKnowledgeCatalog(Svg)), new KnowledgePromptBuilder(catalog));

        // Act
        var answer = tool.Invoke("""{"topic":"svg"}""");

        // Assert: an error that names the kind of failure
        Assert.StartsWith("Error: the tool could not answer (InvalidOperationException).", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the tool of the application: it is made from the catalog of the application and answers the index,
    ///     a card, an example, an ambiguous example, and a search with the cards of CanvasNet and of CanvasPilot.
    /// </summary>
    [Fact]
    public void KnowledgeTools_CreateAll_RealCatalog_AnswersIndexTopicExampleAndQuery()
    {
        // Arrange: the tools of the catalog of the application
        var catalog = KnowledgeCatalog.Default;
        var tools = KnowledgeTools.CreateAll(catalog);
        var tool = Assert.Single(tools);
        var prompts = new KnowledgePromptBuilder(catalog);
        var framed = catalog.Find("svg/framed-figure")!;
        var duplicate = catalog.Cards
            .SelectMany(c => c.Examples.Select(e => (c.Id, e.Name)))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .First(g => g.Count() > 1)
            .ToList();

        // Act
        var index = tool.Invoke("{}");
        var card = tool.Invoke("""{"topic":"SVG Framed-Figure"}""");
        var example = tool.Invoke(JsonSerializer.Serialize(new { example = $"{framed.Id}#{framed.Examples[0].Name}" }));
        var ambiguous = tool.Invoke(JsonSerializer.Serialize(new { example = duplicate[0].Name }));
        var project = tool.Invoke("""{"topic":"canvaspilot/marks"}""");
        var found = tool.Invoke("""{"query":"gantt chart"}""");

        // Assert: each mode gives the text the writer gives for the same cards
        Assert.Equal("canvasnet_help", tool.Name);
        Assert.Equal(prompts.BuildIndex(), index);
        Assert.StartsWith(prompts.BuildCard(framed), card, StringComparison.Ordinal);
        Assert.Equal(KnowledgePromptBuilder.BuildExample(framed.Id, framed.Examples[0]), example);
        Assert.StartsWith("Several cards have an example named", ambiguous, StringComparison.Ordinal);
        Assert.All(duplicate, x => Assert.Contains($"\n- {x.Id}#{x.Name}: ", ambiguous, StringComparison.Ordinal));
        Assert.StartsWith("# ", project, StringComparison.Ordinal);
        Assert.StartsWith("Cards found, best first:\n- mermaid/gantt: ", found, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A catalog whose every lookup fails, to prove that the tool does not let the failure out.
    /// </summary>
    private sealed class FailingCatalog : IKnowledgeCatalog
    {
        /// <inheritdoc />
        public IReadOnlyList<KnowledgeCard> Cards => [];

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeCard> Roots => [];

        /// <inheritdoc />
        public KnowledgeCard? Find(string path) => throw new InvalidOperationException("Find failed.");

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeCard> GetChildren(KnowledgeCard card) => throw new InvalidOperationException("Children failed.");

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeCard> GetSeeAlso(KnowledgeCard card) => throw new InvalidOperationException("See also failed.");

        /// <inheritdoc />
        public IReadOnlyList<KnowledgeExampleReference> FindExamples(string name, string? scopeCardId = null) =>
            throw new InvalidOperationException("Examples failed.");
    }
}

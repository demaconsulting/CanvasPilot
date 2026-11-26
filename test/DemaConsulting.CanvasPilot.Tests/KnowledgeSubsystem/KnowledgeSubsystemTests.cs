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
///     Subsystem tests for the Knowledge subsystem: the catalog of the application, with the cards of CanvasNet and of
///     CanvasPilot, as the units that are built on it see it.
/// </summary>
public sealed class KnowledgeSubsystemTests
{
    /// <summary>
    ///     Validates that the catalog of the application offers the cards of CanvasNet for drawing, the cards of
    ///     CanvasPilot for working in a project, and nothing that is only about the host program.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_Default_OffersDrawingCardsOfCanvasNetAndCanvasPilotWithoutHostOnlyCards()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;
        var ids = catalog.Cards.Select(c => c.Id).ToList();

        // Act
        var roots = catalog.Roots.Select(c => c.Id).ToList();

        // Assert: a card for every drawing format, the cards of CanvasPilot last, the cards for embedding pages, rasters,
        // files, views, and params, and no card that is only about the host program
        Assert.All(new[] { "svg", "mermaid", "plantuml", "scad", "vega-lite", "composition", "canvaspilot" }, id => Assert.Contains(id, roots));
        Assert.Equal("canvaspilot", roots[^1]);
        Assert.Contains("canvaspilot/marks", ids);
        Assert.All(
            new[] { "files", "pdf", "pptx", "vsdx", "raster", "scad/views", "vega-lite/params" },
            id => Assert.Contains(id, ids));
        Assert.DoesNotContain("svg/options", ids);
        Assert.DoesNotContain("svg/flatten", ids);
        Assert.DoesNotContain("errors", ids);
    }

    /// <summary>
    ///     Validates that the catalog and its cards can be read from several threads at the same time, and that the
    ///     catalog of the application is one shared value.
    /// </summary>
    [Fact]
    public async Task KnowledgeSubsystem_Default_IsOneSharedCatalogReadableFromManyThreads()
    {
        // Arrange: many readers
        var readers = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => KnowledgeCatalog.Default))
            .ToArray();

        // Act
        var catalogs = await Task.WhenAll(readers);

        // Assert: every reader got the same catalog
        Assert.All(catalogs, c => Assert.Same(KnowledgeCatalog.Default, c));
    }

    /// <summary>
    ///     Validates that the search finds, in the catalog of the application, the cards that answer the questions of a
    ///     user and of an assistant, with the cards of CanvasPilot among them, and finds nothing for words that no card
    ///     has.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_Search_QuestionsAboutDrawing_FindTheCardsThatAnswerThem()
    {
        // Arrange: the search over the catalog of the application
        var search = new KnowledgeSearch(KnowledgeCatalog.Default);

        // Act
        var gantt = search.Search("gantt chart");
        var marks = search.Search("how do I read the marks of a message");
        var layout = search.Search("project folder layout");
        var nothing = search.Search("xylophone");

        // Assert: the card of the format, the cards of CanvasPilot, and an empty answer
        Assert.Equal("mermaid/gantt", gantt[0].Id);
        Assert.Contains(marks, r => r.Id == "canvaspilot/marks");
        Assert.Equal("canvaspilot/project-layout", layout[0].Id);
        Assert.Empty(nothing);
    }

    /// <summary>
    ///     Validates that the text for a prompt is built from the catalog of the application: an index that is within its
    ///     budget and lists the cards of CanvasNet and of CanvasPilot, the text of the card that a search found, and the
    ///     text of an example of it.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_PromptBuilder_IndexSearchAndCard_AreReadyForAPrompt()
    {
        // Arrange: the catalog, the search, and the builder of the application
        var catalog = KnowledgeCatalog.Default;
        var search = new KnowledgeSearch(catalog);
        var builder = new KnowledgePromptBuilder(catalog);

        // Act
        var index = builder.BuildIndex();
        var found = search.Search("framed figure callout", 3);
        var listing = KnowledgePromptBuilder.BuildSearchResults(found);
        var card = catalog.Find(found[0].Id);
        var text = builder.BuildCard(card!, includeExamples: true);
        var example = KnowledgePromptBuilder.BuildExample(card!.Id, card.Examples[0]);

        // Assert: the index is within budget and lists both sources of cards, the listing names the best card first, the
        // card text holds its example, and the example text is a fenced block of the same file
        Assert.True(index.Length <= KnowledgePromptBuilder.IndexBudgetCharacters);
        Assert.Contains("\n- svg/framed-figure: ", index, StringComparison.Ordinal);
        Assert.Contains("\n- canvaspilot/marks: ", index, StringComparison.Ordinal);
        Assert.StartsWith("Cards found, best first:\n- svg/framed-figure: ", listing, StringComparison.Ordinal);
        Assert.Equal("svg/framed-figure", card.Id);
        Assert.Contains(example, text, StringComparison.Ordinal);
        Assert.Contains("```xml\n<svg", example, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the tool of the application answers from the catalog of the application, called as an assistant
    ///     calls it with a JSON text: the index, a card of CanvasPilot, an example, and a search; and that it answers a
    ///     topic that does not exist with the closest cards and text that is not JSON with an error message.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_Tools_HelpTool_AnswersFromTheApplicationCatalog()
    {
        // Arrange: the tool of the application, and the example that the card of framed figures starts with
        var catalog = KnowledgeCatalog.Default;
        var tool = Assert.Single(KnowledgeTools.CreateAll(catalog));
        var framed = catalog.Find("svg/framed-figure")!;

        // Act
        var index = tool.Invoke("{}");
        var layout = tool.Invoke("""{"topic":"canvaspilot project-layout"}""");
        var example = tool.Invoke(JsonSerializer.Serialize(new { example = $"{framed.Id}#{framed.Examples[0].Name}" }));
        var search = tool.Invoke("""{"query":"gantt chart"}""");
        var unknown = tool.Invoke("""{"topic":"gantt"}""");
        var broken = tool.Invoke("this is not JSON");

        // Assert: each answer is the text of the cards it is about, and mistakes are answered, not thrown
        Assert.Equal("canvasnet_help", tool.Name);
        Assert.StartsWith(new KnowledgePromptBuilder(catalog).BuildIndex(), index, StringComparison.Ordinal);
        Assert.StartsWith("# ", layout, StringComparison.Ordinal);
        Assert.Contains("id: canvaspilot/project-layout\n", layout, StringComparison.Ordinal);
        Assert.StartsWith("Example ", example, StringComparison.Ordinal);
        Assert.Contains("```xml\n<svg", example, StringComparison.Ordinal);
        Assert.StartsWith("Cards found, best first:\n- mermaid/gantt: ", search, StringComparison.Ordinal);
        Assert.StartsWith("No card matches 'gantt'. Closest cards:\n- mermaid/gantt: ", unknown, StringComparison.Ordinal);
        Assert.StartsWith("Error: the arguments are not valid JSON.", broken, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the skills of the application are made from the catalog of the application: one for each card,
    ///     with different names, the cards of CanvasPilot among them, and none for a card that is left out.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_Skills_ApplicationCatalog_HasOneSkillPerCardWithoutHostOnlyCards()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;

        // Act
        var skills = KnowledgeSkills.Create(catalog).ToList();
        var names = skills.Select(s => s.Name).ToList();
        var marks = skills.Single(s => s.Name == "canvasnet-canvaspilot-marks");
        var marksCard = catalog.Find("canvaspilot/marks")!;

        // Assert: as many skills as cards with different names, a skill for a drawing card and none for a card that is left
        // out, and the card as the content
        Assert.Equal(catalog.Cards.Count, skills.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, n => Assert.StartsWith("canvasnet-", n, StringComparison.Ordinal));
        Assert.Contains("canvasnet-svg-framed-figure", names);
        Assert.Contains("canvasnet-files", names);
        Assert.Contains("canvasnet-pdf", names);
        Assert.DoesNotContain("canvasnet-svg-options", names);
        Assert.DoesNotContain("canvasnet-errors", names);
        Assert.Equal($"{marksCard.Title} - {marksCard.Summary}", marks.Description);
        Assert.Contains(KnowledgePromptBuilder.BuildExample(marksCard.Id, marksCard.Examples[0]), marks.Content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the body of a card of CanvasPilot and the body of a card of CanvasNet are read into blocks that
    ///     a window can draw, with headings, items or paragraphs, runs of code text, and nothing empty.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_Markdown_CardOfTheApplication_IsReadIntoBlocksForAWindow()
    {
        // Arrange: a card of each source
        var catalog = KnowledgeCatalog.Default;
        var cards = new[] { catalog.Find("canvaspilot/marks")!, catalog.Find("svg/framed-figure")! };

        // Act
        var read = cards.Select(c => KnowledgeMarkdown.Parse(c.Body)).ToList();

        // Assert: each card has a heading, text blocks, and runs of code, and no block has an empty run
        Assert.All(read, blocks =>
        {
            Assert.NotEmpty(blocks);
            Assert.Contains(blocks, b => b is HeadingBlock);
            Assert.Contains(blocks, b => b is ListItemBlock or ParagraphBlock);
            var spans = blocks.SelectMany(b => b switch
            {
                HeadingBlock h => h.Spans,
                ParagraphBlock p => p.Spans,
                ListItemBlock l => l.Spans,
                _ => []
            }).ToList();
            Assert.Contains(spans, s => s.Style == MarkdownSpanStyle.Code);
            Assert.All(spans, s => Assert.NotEqual(string.Empty, s.Text));
        });
    }
}

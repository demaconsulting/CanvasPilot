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
using DemaConsulting.CanvasPilot.ChatSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ReplyMarkdown" />: every construct that the unit reads, the cases at the edges of each,
///     and a test over a reply full of constructs that the unit does not read that no letter or digit of text is lost.
/// </summary>
public sealed class ReplyMarkdownTests
{
    /// <summary>
    ///     Writes the blocks of a text as short lines that tests compare: <c>H2:</c>, <c>P:</c>, <c>L-:</c>, or
    ///     <c>C[language]</c>, then the text, with the runs written as <c>&lt;b&gt;</c>, <c>&lt;i&gt;</c>, and
    ///     <c>&lt;c&gt;</c>.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <returns>One line for each block.</returns>
    private static string[] Describe(string markdown)
    {
        return ReplyMarkdown.Parse(markdown).Select(Describe).ToArray();
    }

    /// <summary>
    ///     Writes a block as a short line.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The line.</returns>
    private static string Describe(MarkdownBlock block)
    {
        return block switch
        {
            HeadingBlock h => $"H{h.Level}:{Describe(h.Spans)}",
            ParagraphBlock p => $"P:{Describe(p.Spans)}",
            ListItemBlock l => $"L{l.Marker}:{Describe(l.Spans)}",
            CodeBlock c => $"C[{c.Language}]{c.Text}",
            _ => throw new InvalidOperationException("Unknown block.")
        };
    }

    /// <summary>
    ///     Writes runs of text with their styles.
    /// </summary>
    /// <param name="spans">The runs.</param>
    /// <returns>The text, with bold in <c>&lt;b&gt;</c>, italic in <c>&lt;i&gt;</c>, and code in <c>&lt;c&gt;</c>.</returns>
    private static string Describe(IReadOnlyList<MarkdownSpan> spans)
    {
        var builder = new StringBuilder();
        foreach (var span in spans)
        {
            builder.Append(span.Style switch
            {
                MarkdownSpanStyle.Bold => $"<b>{span.Text}</b>",
                MarkdownSpanStyle.Italic => $"<i>{span.Text}</i>",
                MarkdownSpanStyle.Code => $"<c>{span.Text}</c>",
                _ => span.Text
            });
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets the letters and digits of a text, in order, which is what must survive reading.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The letters and digits.</returns>
    private static string LettersAndDigits(string text)
    {
        return new string(text.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>
    ///     Gets the letters and digits of the text that a block shows, including the marker of an item and the language of
    ///     a block of code.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The letters and digits.</returns>
    private static string ShownLettersAndDigits(MarkdownBlock block)
    {
        return LettersAndDigits(block switch
        {
            HeadingBlock h => Text(h.Spans),
            ParagraphBlock p => Text(p.Spans),
            ListItemBlock l => l.Marker + Text(l.Spans),
            CodeBlock c => c.Language + c.Text,
            _ => throw new InvalidOperationException("Unknown block.")
        });
    }

    /// <summary>
    ///     Joins the text of runs.
    /// </summary>
    /// <param name="spans">The runs.</param>
    /// <returns>The text without the markers that gave the runs their styles.</returns>
    private static string Text(IReadOnlyList<MarkdownSpan> spans)
    {
        return string.Concat(spans.Select(s => s.Text));
    }

    /// <summary>
    ///     Validates that headings of the six levels are read with their text and runs, that more than six marks, no space
    ///     after the marks, and a deep indent are not headings, and that a mark alone is an empty heading.
    /// </summary>
    [Fact]
    public void ReplyMarkdown_Parse_Headings_AreReadByLevel()
    {
        // Arrange / Act / Assert: the six levels, a heading with a run of code, and the lines that only look like headings
        Assert.Equal(
            ["H1:One", "H2:Two", "H3:Three", "H4:Four", "H5:Five", "H6:Six"],
            Describe("# One\n## Two\n### Three\n#### Four\n##### Five\n###### Six"));
        Assert.Equal(["H2:The <c>id</c> of a <b>card</b>"], Describe("## The `id` of a **card**"));
        Assert.Equal(["P:####### seven", "P:#nospace", "P:# indented"], Describe("####### seven\n\n#nospace\n\n    # indented"));
        Assert.Equal(["H3:Indented"], Describe("   ### Indented"));
        Assert.Equal(["H1:"], Describe("#"));
    }

    /// <summary>
    ///     Validates that the lines of a paragraph are joined with a space, that a blank line ends a paragraph, and that
    ///     every kind of line break and the white space around the text are handled.
    /// </summary>
    [Fact]
    public void ReplyMarkdown_Parse_Paragraphs_JoinLinesAndSplitAtBlankLines()
    {
        // Arrange / Act / Assert
        Assert.Equal(["P:one two three"], Describe("one\ntwo\n  three  "));
        Assert.Equal(["P:one two", "P:three"], Describe("one\r\ntwo\r\n\r\nthree"));
        Assert.Equal(["P:one", "P:two"], Describe("one\r\r  \rtwo"));
        Assert.Equal(["H2:Title", "P:text"], Describe("## Title\ntext"));
        Assert.Empty(ReplyMarkdown.Parse(string.Empty));
        Assert.Empty(ReplyMarkdown.Parse("  \n\n \t \r\n"));
        Assert.Throws<ArgumentNullException>(() => ReplyMarkdown.Parse(null!));
    }

    /// <summary>
    ///     Validates the items of bulleted and numbered lists with their markers, the lines that continue an item, and the
    ///     lines that start a list inside a paragraph or end an item.
    /// </summary>
    [Fact]
    public void ReplyMarkdown_Parse_Lists_ReadMarkersAndContinuations()
    {
        // Arrange / Act / Assert: the three bullets and two kinds of number
        Assert.Equal(["L-:one", "L*:two", "L+:three"], Describe("- one\n* two\n+ three"));
        Assert.Equal(["L1.:first", "L2.:second", "L10):tenth"], Describe("1. first\n2. second\n10) tenth"));

        // The lines after an item continue it, with or without an indent, until a blank line
        Assert.Equal(
            ["L-:an item that goes on and on", "P:A paragraph."],
            Describe("- an item\n  that goes on\nand on\n\nA paragraph."));
        Assert.Equal(["L1.:wrapped <c>code</c> item"], Describe("1. wrapped\n   `code` item"));

        // A list starts inside a paragraph, and a heading or a fence ends an item
        Assert.Equal(["P:Steps:", "L-:one", "L-:two"], Describe("Steps:\n- one\n- two"));
        Assert.Equal(["L-:one", "H2:Next", "L-:two"], Describe("- one\n## Next\n- two"));

        // A nested item is an item of its own
        Assert.Equal(["L-:outer", "L-:inner"], Describe("- outer\n  - inner"));
    }

    /// <summary>
    ///     Validates that lines that look like a marker but are not one are text: a marker with nothing after it, a number
    ///     with no dot, bold at the start of a line, and a star next to text.
    /// </summary>
    [Fact]
    public void ReplyMarkdown_Parse_LinesThatOnlyLookLikeListItems_AreParagraphs()
    {
        // Arrange / Act / Assert
        Assert.Equal(["P:-"], Describe("-"));
        Assert.Equal(["P:-not an item"], Describe("-not an item"));
        Assert.Equal(["P:2024 was a year"], Describe("2024 was a year"));
        Assert.Equal(["P:<b>Bold</b> start of a line"], Describe("**Bold** start of a line"));
        Assert.Equal(["P:<i>Italic</i> start"], Describe("*Italic* start"));
    }

    /// <summary>
    ///     Validates fenced blocks: the language, no language, a fence that is not closed, a longer fence that holds a
    ///     shorter one, an indented fence, and lines that look like other blocks but are code.
    /// </summary>
    [Fact]
    public void ReplyMarkdown_Parse_FencedCode_IsKeptVerbatim()
    {
        // Arrange / Act / Assert: language and text, blank lines kept, markers inside the block not read
        Assert.Equal(["C[xml]<svg>\n\n  <g/>\n</svg>"], Describe("```xml\n<svg>\n\n  <g/>\n</svg>\n```"));
        Assert.Equal(["C[]plain"], Describe("```\nplain\n```"));
        Assert.Equal(["C[md]# not a heading\n- not an item\n**not bold**"], Describe("```md\n# not a heading\n- not an item\n**not bold**\n```"));
        Assert.Equal(["C[]"], Describe("```\n```"));

        // A fence that is not closed runs to the end, and the text after the language on the fence line is not part of it
        Assert.Equal(["C[json]{\"a\": 1}"], Describe("```json title\n{\"a\": 1}"));

        // A longer fence holds a shorter one; the indent of the fence is taken off the lines
        Assert.Equal(["C[md]```xml\n<svg/>\n```"], Describe("````md\n```xml\n<svg/>\n```\n````"));
        Assert.Equal(["L-:item", "C[xml]<a>\n  <b/>\n</a>"], Describe("- item\n  ```xml\n  <a>\n    <b/>\n  </a>\n  ```"));

        // Text before and after a block, and backticks on the fence line that make it text
        Assert.Equal(["P:Before", "C[sh]run", "P:After"], Describe("Before\n```sh\nrun\n```\nAfter"));
        Assert.Equal(["P:<c>inline</c> code"], Describe("```inline``` code"));
    }

    /// <summary>
    ///     Validates the runs of text: bold, italic, code, a mix, code that holds markers, and markers that are kept
    ///     because they have no end or are next to white space.
    /// </summary>
    [Fact]
    public void ReplyMarkdown_Parse_Spans_ReadBoldItalicAndCode()
    {
        // Arrange / Act / Assert: the three styles and a mix
        Assert.Equal(["P:a <b>bold</b> b"], Describe("a **bold** b"));
        Assert.Equal(["P:an <i>italic</i> b"], Describe("an *italic* b"));
        Assert.Equal(["P:the <c>id</c> of a card"], Describe("the `id` of a card"));
        Assert.Equal(["P:<b>b</b><i>i</i><c>c</c>end"], Describe("**b***i*`c`end"));

        // Code inside bold, stars inside code, and a code span with a backtick in it
        Assert.Equal(["P:<b>see </b><c>id</c>"], Describe("**see `id`**"));
        Assert.Equal(["P:<c>a*b</c> and <c>**</c>"], Describe("`a*b` and `**`"));
        Assert.Equal(["P:<c>`</c> and <c>a`b</c>"], Describe("`` ` `` and ``a`b``"));
        Assert.Equal(["P:<b>bold with </b><c>x**y</c><b> inside</b>"], Describe("**bold with `x**y` inside**"));

        // Markers with no end, or next to white space, are text
        Assert.Equal(["P:a * b * c and 2*3"], Describe("a * b * c and 2*3"));
        Assert.Equal(["P:**unclosed and *also"], Describe("**unclosed and *also"));
        Assert.Equal(["P:a `tick and **"], Describe("a `tick and **"));
        Assert.Equal(["P:snake_case_name and a_b"], Describe("snake_case_name and a_b"));
    }

    /// <summary>
    ///     Validates that neighbors that end up in the same style are one run, that no run is empty, and that the order of
    ///     the blocks of a whole reply is the order of its text.
    /// </summary>
    [Fact]
    public void ReplyMarkdown_Parse_WholeReply_KeepsOrderAndMergesRuns()
    {
        // Arrange: a reply that uses every construct that the unit reads
        const string markdown = "Intro with `a` and **b**.\n\n## Steps\n\n1. Do *this*\n   first.\n2. Then that.\n\n```xml\n<svg/>\n```\n\n- end";

        // Act
        var blocks = ReplyMarkdown.Parse(markdown);

        // Assert: every block in order, and unmatched markers merged into the text around them
        Assert.Equal(
            ["P:Intro with <c>a</c> and <b>b</b>.", "H2:Steps", "L1.:Do <i>this</i> first.", "L2.:Then that.", "C[xml]<svg/>", "L-:end"],
            blocks.Select(Describe).ToArray());
        var merged = Assert.IsType<ParagraphBlock>(ReplyMarkdown.Parse("a * b ** c").Single());
        var span = Assert.Single(merged.Spans);
        Assert.Equal(new MarkdownSpan("a * b ** c", MarkdownSpanStyle.Normal), span);
        Assert.All(blocks.OfType<ParagraphBlock>(), p => Assert.All(p.Spans, s => Assert.NotEqual(string.Empty, s.Text)));
    }

    /// <summary>
    ///     Validates, on a reply full of constructs that the unit does not read (links, images, tables, quotes, setext
    ///     headings, rules, underscore emphasis, HTML, strike-through, a nested item, and a fence that is not closed),
    ///     that reading does not fail, that no letter or digit of the text is lost or added, and that a link is kept as
    ///     the text that was written.
    /// </summary>
    [Fact]
    public void ReplyMarkdown_Parse_ConstructsItDoesNotRead_KeepEveryLetterAndDigit()
    {
        // Arrange: a reply that uses the constructs the unit keeps as text; the fence line has only a language
        const string link = "See [docs](https://example.com/a1) and ![alt](p.png) for more.";
        var markdown = string.Join(
            "\n",
            link,
            string.Empty,
            "| a | b |",
            "|---|---|",
            "| 1 | 2 |",
            string.Empty,
            "> quote of 3 words",
            string.Empty,
            "Title",
            "=====",
            string.Empty,
            "***",
            string.Empty,
            "Some _under_ and __double__ text, <b>html</b>, and ~~strike~~.",
            string.Empty,
            "- outer",
            "  - nested 4",
            string.Empty,
            "```svg",
            "<rect width=\"5\"/>");

        // Act
        var blocks = ReplyMarkdown.Parse(markdown);

        // Assert: the letters and digits of the source are the ones shown, and the link is one plain run as written
        Assert.Equal(LettersAndDigits(markdown), string.Concat(blocks.Select(ShownLettersAndDigits)));
        var first = Assert.IsType<ParagraphBlock>(blocks[0]);
        Assert.Equal(new MarkdownSpan(link, MarkdownSpanStyle.Normal), Assert.Single(first.Spans));
        Assert.Equal(new CodeBlock("svg", "<rect width=\"5\"/>"), blocks[^1]);
    }
}

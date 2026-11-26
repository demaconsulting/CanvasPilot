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

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.ChatSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for <see cref="MarkdownBlocksPanel" />: the control that draws the blocks that
///     <see cref="ReplyMarkdown" /> reads from Markdown, which the chat panel uses for the replies of the copilot.
/// </summary>
public sealed class ReplyMarkdownViewTests
{
    /// <summary>
    ///     Reads the text of a text block, which is the text of its runs when it has runs.
    /// </summary>
    /// <param name="block">The text block.</param>
    /// <returns>The text.</returns>
    private static string TextOf(TextBlock block)
    {
        return block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;
    }

    /// <summary>
    ///     Test that every kind of block is drawn: a heading in bold type, a bullet with a dot, a number as written, text
    ///     with bold, italic, and code runs, and a block of code with its lines; and that the panel follows its blocks.
    /// </summary>
    [AvaloniaFact]
    public void MarkdownBlocksPanel_Blocks_DrawsEveryKindOfBlock()
    {
        // Arrange: a panel over the blocks that the reader makes of a small text
        var blocks = ReplyMarkdown.Parse(
            "## Heading\n\nSome **bold**, *italic*, and `code` text.\n\n- a bullet\n3. a number\n\n```xml\n<svg/>\n<g/>\n```");
        var panel = new MarkdownBlocksPanel { Blocks = blocks };
        var window = new Window { Content = panel, Width = 500, Height = 400 };

        // Act: show it
        window.Show();
        window.UpdateLayout();

        // Assert: five blocks are drawn, each as its kind needs
        Assert.Equal(5, panel.Children.Count);
        var heading = Assert.IsType<SelectableTextBlock>(panel.Children[0]);
        Assert.Equal(FontWeight.SemiBold, heading.FontWeight);
        Assert.Equal("Heading", TextOf(heading));
        var paragraph = Assert.IsType<SelectableTextBlock>(panel.Children[1]);
        var paragraphRuns = paragraph.Inlines!.OfType<Run>().ToList();
        Assert.Equal("Some bold, italic, and code text.", TextOf(paragraph));
        Assert.Contains(paragraphRuns, run => run.Text == "bold" && run.FontWeight == FontWeight.Bold);
        Assert.Contains(paragraphRuns, run => run.Text == "italic" && run.FontStyle == FontStyle.Italic);
        Assert.Contains(paragraphRuns, run => run.Text == "code" && run.Background is not null);
        var markers = panel.Children.OfType<Grid>().Select(grid => grid.Children.OfType<TextBlock>().First(t => t is not SelectableTextBlock).Text).ToList();
        Assert.Equal(["\u2022", "3."], markers);
        var code = panel.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => AutomationProperties.GetAutomationId(t) == "ReplyMarkdownCode");
        Assert.Equal("<svg/>\n<g/>", code.Text?.Replace("\r\n", "\n", StringComparison.Ordinal));

        // Act: draw different blocks, then none
        panel.Blocks = ReplyMarkdown.Parse("Only text.");
        var afterChange = panel.Children.Count;
        panel.Blocks = null;

        // Assert: the panel follows the blocks
        Assert.Equal(1, afterChange);
        Assert.Empty(panel.Children);
        window.Close();
    }
}

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
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests of the Chat subsystem with its views: the chat panel, the composer, the views of the rows of the
///     answer, and the reader and control that draw a completed reply, together in a window, with only the copilot
///     replaced by a scripted fake that answers when the test says so.
/// </summary>
public sealed class ChatSubsystemUiTests
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
    ///     Test that a completed reply of the copilot is shown with its formatting drawn instead of its markers: a heading
    ///     in semibold type, bold, italic, and code text, a dot and a number for its list items, and a block of code, all
    ///     as text that can be selected, with a link that is not read kept as the text that was written.
    /// </summary>
    [AvaloniaFact]
    public void ChatSubsystem_ShowRepliesFormatted_CompletedReply_DrawsHeadingsListsCodeAndStyledText()
    {
        // Arrange: a message that was sent to the copilot
        var agent = new ScriptedAgentService();
        using var harness = new ChatHarness(agent: agent);
        harness.Type("Draw a box");
        harness.ClickSend();

        // Act: the copilot answers with a reply that uses the formatting, and the turn completes
        const string link = "Read [the docs](https://example.com) for more.";
        agent.Emit(new AgentEvent.TextDelta(
            "## Plan\n\nA **bold** frame, an *italic* label, and `box.svg`.\n\n- a frame\n2. a label\n\n```svg\n<rect/>\n```\n\n" + link));
        agent.Emit(new AgentEvent.Completed());
        Assert.True(harness.WaitFor(() => !harness.Chat.IsCopilotBusy));

        // Assert: the formatted reply is shown in place of the plain text
        var reply = Assert.Single(harness.Replies);
        var markdown = reply.FindControl<MarkdownBlocksPanel>("ReplyMarkdown")!;
        Assert.True(markdown.IsEffectivelyVisible);
        Assert.False(reply.FindControl<SelectableTextBlock>("ReplyText")!.IsEffectivelyVisible);
        Assert.Equal(6, markdown.Children.Count);

        // The heading is semibold, without its marks
        var heading = Assert.IsType<SelectableTextBlock>(markdown.Children[0]);
        Assert.Equal(FontWeight.SemiBold, heading.FontWeight);
        Assert.Equal("Plan", TextOf(heading));

        // The paragraph has bold, italic, and code runs, without asterisks or backticks
        var paragraph = Assert.IsType<SelectableTextBlock>(markdown.Children[1]);
        var runs = paragraph.Inlines!.OfType<Run>().ToList();
        Assert.Equal("A bold frame, an italic label, and box.svg.", TextOf(paragraph));
        Assert.Contains(runs, run => run.Text == "bold" && run.FontWeight == FontWeight.Bold);
        Assert.Contains(runs, run => run.Text == "italic" && run.FontStyle == FontStyle.Italic);
        Assert.Contains(runs, run => run.Text == "box.svg" && run.Background is not null);

        // The items have a dot for a bullet and the number as written
        var markers = markdown.Children.OfType<Grid>()
            .Select(grid => grid.Children.OfType<TextBlock>().First(t => t is not SelectableTextBlock).Text)
            .ToList();
        Assert.Equal(["\u2022", "2."], markers);

        // The block of code holds its line, and the link is kept as its text
        var texts = markdown.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => AutomationProperties.GetAutomationId(t)?.StartsWith("ReplyMarkdown", StringComparison.Ordinal) == true)
            .ToList();
        var code = texts.Single(t => AutomationProperties.GetAutomationId(t) == "ReplyMarkdownCode");
        Assert.Equal("<rect/>", code.Text);
        Assert.Equal(link, TextOf(Assert.IsType<SelectableTextBlock>(markdown.Children[5])));

        // Every text of the reply can be selected
        Assert.Equal(6, texts.Count);
        Assert.All(texts, t => Assert.IsType<SelectableTextBlock>(t));
    }
}

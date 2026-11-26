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

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Reads the Markdown text of a reply of the copilot into blocks and runs of text that
///     <see cref="MarkdownBlocksPanel" /> draws.
/// </summary>
/// <remarks>
///     <para>
///         The unit reads headings (<c>#</c> to <c>######</c>), paragraphs, bulleted and numbered lists, fenced blocks
///         of code, and, inside the text, <c>**bold**</c>, <c>*italic*</c>, and <c>`code`</c>. Links, tables, quotes,
///         and any other construct are kept as the text that was written, so a Markdown library would add a dependency
///         for no gain. A line that starts a heading, a list item, or a fence ends the paragraph or item before it; the
///         lines that follow a list item without a blank line continue the item; a blank line ends a paragraph or an
///         item; a fence that is not closed runs to the end of the text. Lines of a paragraph or an item are joined with
///         a space.
///     </para>
///     <para>
///         Reading never fails: a marker that has nothing to close it, such as a lone <c>*</c>, is kept as text. Only
///         the first word after an opening fence is kept, as the language of the block. The unit holds no state and is
///         safe for concurrent use.
///     </para>
/// </remarks>
public static class ReplyMarkdown
{
    /// <summary>
    ///     The fewest backticks that open or close a fenced block.
    /// </summary>
    private const int MinimumFence = 3;

    /// <summary>
    ///     The deepest level of a heading.
    /// </summary>
    private const int MaximumHeadingLevel = 6;

    /// <summary>
    ///     The most spaces that may come before a heading.
    /// </summary>
    private const int MaximumHeadingIndent = 3;

    /// <summary>
    ///     Reads Markdown text into blocks.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <returns>The blocks in order; empty for text that is empty or only blank lines.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var lines = markdown.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        var blocks = new List<MarkdownBlock>();
        var pending = new List<string>();
        string? marker = null;
        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            var text = line.Trim();
            if (text.Length == 0)
            {
                Flush(blocks, pending, ref marker);
            }
            else if (TryReadFence(line, out var fence, out var language, out var indent))
            {
                Flush(blocks, pending, ref marker);
                index = ReadCode(lines, index + 1, fence, language, indent, blocks);
            }
            else if (TryReadHeading(line, out var level, out var headingText))
            {
                Flush(blocks, pending, ref marker);
                blocks.Add(new HeadingBlock(level, ParseSpans(headingText)));
            }
            else if (TryReadListItem(text, out var itemMarker, out var itemText))
            {
                Flush(blocks, pending, ref marker);
                marker = itemMarker;
                pending.Add(itemText);
            }
            else
            {
                // A line that starts nothing continues the item or the paragraph before it
                pending.Add(text);
            }

            index++;
        }

        Flush(blocks, pending, ref marker);
        return blocks.AsReadOnly();
    }

    /// <summary>
    ///     Writes the paragraph or the list item that is being read as a block and starts again.
    /// </summary>
    /// <param name="blocks">The blocks that have been read.</param>
    /// <param name="pending">The lines of the paragraph or item that is being read.</param>
    /// <param name="marker">The marker of the item that is being read, or <see langword="null" /> for a paragraph.</param>
    private static void Flush(List<MarkdownBlock> blocks, List<string> pending, ref string? marker)
    {
        if (pending.Count > 0)
        {
            var spans = ParseSpans(string.Join(' ', pending));
            blocks.Add(marker is null ? new ParagraphBlock(spans) : new ListItemBlock(marker, spans));
        }

        pending.Clear();
        marker = null;
    }

    /// <summary>
    ///     Reads the opening line of a fenced block.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="fence">The number of backticks of the fence.</param>
    /// <param name="language">The language after the fence, or <see langword="null" /> when there is none.</param>
    /// <param name="indent">The number of spaces before the fence.</param>
    /// <returns><see langword="true" /> when the line opens a fenced block.</returns>
    private static bool TryReadFence(string line, out int fence, out string? language, out int indent)
    {
        var trimmed = line.TrimStart();
        indent = line.Length - trimmed.Length;
        fence = CountLeading(trimmed, '`');
        language = null;
        if (fence < MinimumFence)
        {
            return false;
        }

        // A line with a backtick after the fence is code in a paragraph, not a fence
        var info = trimmed[fence..].Trim();
        if (info.Contains('`', StringComparison.Ordinal))
        {
            return false;
        }

        if (info.Length > 0)
        {
            language = info.Split(' ', 2)[0];
        }

        return true;
    }

    /// <summary>
    ///     Reads the lines of a fenced block up to its closing fence and adds the block.
    /// </summary>
    /// <param name="lines">All the lines of the text.</param>
    /// <param name="start">The index of the first line after the opening fence.</param>
    /// <param name="fence">The number of backticks of the opening fence.</param>
    /// <param name="language">The language of the block.</param>
    /// <param name="indent">The number of spaces before the opening fence, which are taken off each line of the block.</param>
    /// <param name="blocks">The blocks, to which the block is added.</param>
    /// <returns>The index of the closing fence, or of the last line when the fence is not closed.</returns>
    private static int ReadCode(string[] lines, int start, int fence, string? language, int indent, List<MarkdownBlock> blocks)
    {
        var code = new List<string>();
        var index = start;
        while (index < lines.Length && !IsClosingFence(lines[index], fence))
        {
            code.Add(RemoveIndent(lines[index], indent));
            index++;
        }

        blocks.Add(new CodeBlock(language, string.Join('\n', code)));
        return index;
    }

    /// <summary>
    ///     Tells whether a line closes a fenced block: only backticks, at least as many as the opening fence.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="fence">The number of backticks of the opening fence.</param>
    /// <returns><see langword="true" /> when the line closes the block.</returns>
    private static bool IsClosingFence(string line, int fence)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= fence && trimmed.All(c => c == '`');
    }

    /// <summary>
    ///     Takes up to a number of spaces off the start of a line.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="indent">The most spaces to take off.</param>
    /// <returns>The line without those spaces.</returns>
    private static string RemoveIndent(string line, int indent)
    {
        var count = 0;
        while (count < indent && count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return line[count..];
    }

    /// <summary>
    ///     Reads a heading.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="level">The level of the heading.</param>
    /// <param name="text">The text of the heading.</param>
    /// <returns><see langword="true" /> when the line is a heading: one to six <c>#</c> and then a space or the end.</returns>
    private static bool TryReadHeading(string line, out int level, out string text)
    {
        text = string.Empty;
        var trimmed = line.TrimStart();
        level = CountLeading(trimmed, '#');
        if (line.Length - trimmed.Length > MaximumHeadingIndent
            || level is < 1 or > MaximumHeadingLevel
            || (trimmed.Length > level && !char.IsWhiteSpace(trimmed[level])))
        {
            return false;
        }

        text = trimmed[level..].Trim();
        return true;
    }

    /// <summary>
    ///     Reads the start of a list item.
    /// </summary>
    /// <param name="text">The line without the white space around it.</param>
    /// <param name="marker">The marker: <c>-</c>, <c>*</c>, <c>+</c>, or a number with <c>.</c> or <c>)</c>.</param>
    /// <param name="itemText">The text after the marker.</param>
    /// <returns><see langword="true" /> when the line starts an item: a marker, a space, and some text.</returns>
    private static bool TryReadListItem(string text, out string marker, out string itemText)
    {
        marker = string.Empty;
        itemText = string.Empty;
        var length = MarkerLength(text);
        if (length == 0 || length >= text.Length || !char.IsWhiteSpace(text[length]))
        {
            return false;
        }

        marker = text[..length];
        itemText = text[length..].Trim();
        return itemText.Length > 0;
    }

    /// <summary>
    ///     Gets the length of the list marker at the start of a text.
    /// </summary>
    /// <param name="text">The text, which is not empty.</param>
    /// <returns>The length of <c>-</c>, <c>*</c>, <c>+</c>, or a number followed by <c>.</c> or <c>)</c>; zero when the text starts with none.</returns>
    private static int MarkerLength(string text)
    {
        if (text[0] is '-' or '*' or '+')
        {
            return 1;
        }

        var digits = CountLeadingDigits(text);
        return digits > 0 && digits < text.Length && text[digits] is '.' or ')' ? digits + 1 : 0;
    }

    /// <summary>
    ///     Counts the same character at the start of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="c">The character.</param>
    /// <returns>The number of times it repeats at the start.</returns>
    private static int CountLeading(string text, char c)
    {
        var count = 0;
        while (count < text.Length && text[count] == c)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    ///     Counts the digits at the start of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The number of digits at the start.</returns>
    private static int CountLeadingDigits(string text)
    {
        var count = 0;
        while (count < text.Length && char.IsAsciiDigit(text[count]))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    ///     Reads the text of a block into runs of one style each.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The runs, with no empty run and no two neighbors of the same style.</returns>
    private static List<MarkdownSpan> ParseSpans(string text)
    {
        var spans = new List<MarkdownSpan>();
        AppendSpans(text, MarkdownSpanStyle.Normal, spans);

        // Join neighbors of the same style, which the markers that are kept as text leave behind
        var merged = new List<MarkdownSpan>();
        foreach (var span in spans.Where(s => s.Text.Length > 0))
        {
            if (merged.Count > 0 && merged[^1].Style == span.Style)
            {
                merged[^1] = merged[^1] with { Text = merged[^1].Text + span.Text };
            }
            else
            {
                merged.Add(span);
            }
        }

        return merged;
    }

    /// <summary>
    ///     Reads a text into runs, in a style, and adds them. Code, bold, and italic are read from their markers; a marker
    ///     that has no end is kept as text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="style">The style of the text that is not between markers.</param>
    /// <param name="spans">The runs, to which the new runs are added.</param>
    private static void AppendSpans(string text, MarkdownSpanStyle style, List<MarkdownSpan> spans)
    {
        var plain = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            if (TryReadSpan(text, i, out var inner, out var innerStyle, out var next))
            {
                if (plain.Length > 0)
                {
                    spans.Add(new MarkdownSpan(plain.ToString(), style));
                    plain.Clear();
                }

                if (innerStyle == MarkdownSpanStyle.Code)
                {
                    spans.Add(new MarkdownSpan(inner, MarkdownSpanStyle.Code));
                }
                else
                {
                    AppendSpans(inner, innerStyle, spans);
                }

                i = next;
            }
            else
            {
                plain.Append(text[i]);
                i++;
            }
        }

        if (plain.Length > 0)
        {
            spans.Add(new MarkdownSpan(plain.ToString(), style));
        }
    }

    /// <summary>
    ///     Reads a span of code, bold, or italic text that starts at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position of the first marker.</param>
    /// <param name="inner">The text between the markers.</param>
    /// <param name="style">The style of the span.</param>
    /// <param name="next">The position after the last marker.</param>
    /// <returns><see langword="true" /> when a span that has both its markers starts at the position.</returns>
    private static bool TryReadSpan(string text, int start, out string inner, out MarkdownSpanStyle style, out int next)
    {
        inner = string.Empty;
        style = MarkdownSpanStyle.Normal;
        next = start;
        switch (text[start])
        {
            case '`':
                return TryReadCode(text, start, out inner, out style, out next);

            case '*':
                var bold = start + 1 < text.Length && text[start + 1] == '*';
                var marker = bold ? 2 : 1;
                var open = start + marker;
                var close = open < text.Length && !char.IsWhiteSpace(text[open]) ? FindClosingStar(text, open, marker) : -1;
                if (close < 0)
                {
                    return false;
                }

                inner = text[open..close];
                style = bold ? MarkdownSpanStyle.Bold : MarkdownSpanStyle.Italic;
                next = close + marker;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    ///     Reads a span of code between runs of backticks of the same length.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position of the first backtick.</param>
    /// <param name="inner">The code.</param>
    /// <param name="style">Set to code.</param>
    /// <param name="next">The position after the closing backticks.</param>
    /// <returns><see langword="true" /> when the run of backticks at the position is closed by another of the same length.</returns>
    private static bool TryReadCode(string text, int start, out string inner, out MarkdownSpanStyle style, out int next)
    {
        inner = string.Empty;
        style = MarkdownSpanStyle.Code;
        next = start;
        var length = CountLeading(text[start..], '`');
        var close = FindBacktickRun(text, start + length, length);
        if (close < 0)
        {
            return false;
        }

        inner = text[(start + length)..close];

        // One space on both sides is the way to write code that starts or ends with a backtick; it is not part of the code
        if (inner.Length > 2 && inner[0] == ' ' && inner[^1] == ' ' && inner.Any(c => c != ' '))
        {
            inner = inner[1..^1];
        }

        next = close + length;
        return true;
    }

    /// <summary>
    ///     Finds the next run of exactly a number of backticks.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="from">The position to start at.</param>
    /// <param name="length">The length of the run to find.</param>
    /// <returns>The position of the run, or -1 when there is none.</returns>
    private static int FindBacktickRun(string text, int from, int length)
    {
        var i = from;
        while (i < text.Length)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }

            var run = CountLeading(text[i..], '`');
            if (run == length)
            {
                return i;
            }

            i += run;
        }

        return -1;
    }

    /// <summary>
    ///     Finds the star or stars that close a bold or italic span, skipping code.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="from">The position after the opening marker.</param>
    /// <param name="marker">The number of stars of the marker: one for italic, two for bold.</param>
    /// <returns>The position of the closing marker, or -1 when there is none.</returns>
    private static int FindClosingStar(string text, int from, int marker)
    {
        var i = from;
        while (i < text.Length)
        {
            if (text[i] == '`')
            {
                // Stars inside code do not close the span; code that is not closed is plain text
                var run = CountLeading(text[i..], '`');
                var close = FindBacktickRun(text, i + run, run);
                i = close < 0 ? i + run : close + run;
                continue;
            }

            if (text[i] == '*' && !char.IsWhiteSpace(text[i - 1]) && CountLeading(text[i..], '*') >= marker
                && (marker == 2 || CountLeading(text[i..], '*') == 1))
            {
                return i;
            }

            i++;
        }

        return -1;
    }
}

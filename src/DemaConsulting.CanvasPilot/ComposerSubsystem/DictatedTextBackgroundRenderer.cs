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

using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     A stretch of the message that was dictated, as offsets of the message.
/// </summary>
/// <param name="Offset">The offset of the first dictated character.</param>
/// <param name="Length">The number of dictated characters, one or more.</param>
public readonly record struct DictatedRange(int Offset, int Length);

/// <summary>
///     Draws a soft background behind the words that were dictated, so that the user can see which words the speech
///     recognizer wrote and check them, as the payload also tells the assistant which words were dictated.
/// </summary>
/// <remarks>
///     The renderer reads the message, not the editing control, as the source of truth about where dictated text is; the
///     control only provides the geometry of the lines. The ranges are cut to the text that the control holds at the
///     moment of drawing, because the control and the message agree again only after a change is complete.
/// </remarks>
public sealed class DictatedTextBackgroundRenderer : IBackgroundRenderer
{
    /// <summary>
    ///     The color of the background, a transparent blue that keeps the text readable on a light and a dark theme.
    /// </summary>
    private static readonly IBrush DictatedBrush = new ImmutableSolidColorBrush(Color.Parse("#300078D7"));

    /// <summary>
    ///     Gives the message whose dictated words are drawn, or <see langword="null" /> for none.
    /// </summary>
    private readonly Func<ComposerDocument?> _document;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DictatedTextBackgroundRenderer" /> class.
    /// </summary>
    /// <param name="document">Gives the message whose dictated words are drawn; it is asked at every drawing.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> is <see langword="null" />.</exception>
    public DictatedTextBackgroundRenderer(Func<ComposerDocument?> document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
    }

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Background;

    /// <summary>
    ///     Finds the dictated stretches of a message.
    /// </summary>
    /// <param name="document">The message.</param>
    /// <returns>The ranges of the dictated text, in order, without the chips between them.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<DictatedRange> GetRanges(ComposerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Walk the segments once, keeping the offset, and report the dictated text
        var ranges = new List<DictatedRange>();
        var offset = 0;
        foreach (var segment in document.Segments)
        {
            if (segment is TextSegment { Origin: TextOrigin.Dictated, Length: > 0 })
            {
                ranges.Add(new DictatedRange(offset, segment.Length));
            }

            offset += segment.Length;
        }

        return ranges;
    }

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);

        // Nothing to draw before the lines are known, or without a message
        var document = _document();
        if (document is null || textView.Document is null || !textView.VisualLinesValid)
        {
            return;
        }

        // Draw each stretch as one geometry, so that stretches that wrap over lines look like one block
        var limit = textView.Document.TextLength;
        foreach (var range in GetRanges(document))
        {
            var length = Math.Min(range.Length, limit - range.Offset);
            if (length <= 0)
            {
                continue;
            }

            var builder = new BackgroundGeometryBuilder { CornerRadius = 3 };
            builder.AddSegment(textView, new SimpleSegment(range.Offset, length));
            var geometry = builder.CreateGeometry();
            if (geometry is not null)
            {
                drawingContext.DrawGeometry(DictatedBrush, null, geometry);
            }
        }
    }
}

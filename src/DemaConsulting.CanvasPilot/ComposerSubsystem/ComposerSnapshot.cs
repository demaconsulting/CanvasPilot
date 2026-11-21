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

using System.Collections.Immutable;
using System.Text;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     An unchangeable copy of what the composer held at one moment: the ordered segments of the message. It is what
///     later steps read to build the message that is sent, so that nothing the user does after taking it can change
///     it.
/// </summary>
public sealed class ComposerSnapshot
{
    /// <summary>
    ///     The character that stands for a chip in <see cref="FlatText" />.
    /// </summary>
    public const char ChipPlaceholder = '\uFFFC';

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerSnapshot" /> class.
    /// </summary>
    /// <param name="segments">The segments, in order; they are copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments" /> is <see langword="null" />.</exception>
    internal ComposerSnapshot(IEnumerable<ComposerSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        Segments = [.. segments];
        Length = Segments.Sum(segment => segment.Length);
    }

    /// <summary>
    ///     Gets the segments, in order. Adjacent pieces of text of the same origin are one segment, and no text segment
    ///     is empty.
    /// </summary>
    public ImmutableArray<ComposerSegment> Segments { get; }

    /// <summary>
    ///     Gets the number of positions: one for each character of text and one for each chip.
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///     Gets the chips, in order.
    /// </summary>
    public IEnumerable<ChipSegment> Chips => Segments.OfType<ChipSegment>();

    /// <summary>
    ///     Gets the text as the editing control holds it: the text of every segment, with one
    ///     <see cref="ChipPlaceholder" /> for each chip.
    /// </summary>
    public string FlatText
    {
        get
        {
            var builder = new StringBuilder(Length);
            foreach (var segment in Segments)
            {
                builder.Append(segment is TextSegment text ? text.Text : ChipPlaceholder);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    ///     Determines whether another snapshot holds exactly the same segments in the same order.
    /// </summary>
    /// <param name="other">The other snapshot.</param>
    /// <returns><see langword="true" /> when both hold the same content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other" /> is <see langword="null" />.</exception>
    public bool HasSameContentAs(ComposerSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Segments.SequenceEqual(other.Segments);
    }
}

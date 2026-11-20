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

using System.Collections.ObjectModel;
using System.Text;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     The message the user is composing, as an ordered sequence of segments: typed text, dictated text, and chips for
///     attached files, folders, and images and for marks on the shown picture. It is the single source of truth about
///     the message; the editing control only shows it, and the control and the model are kept the same by turning each
///     change of the control into one edit of the model.
/// </summary>
/// <remarks>
///     <para>
///         Positions, called offsets, count one for each UTF-16 code unit of text and one for each chip, so an offset of
///         the model is an offset of the editing control that shows each chip as one placeholder character. An offset
///         lies between two positions: zero is before the first, and <see cref="Length" /> is after the last. Offsets
///         inside a pair of surrogate characters are the caller's responsibility; the editing control never produces
///         them.
///     </para>
///     <para>
///         The sequence is kept in one canonical form: adjacent text of the same origin is one segment, text of
///         different origins is separate segments, and no text segment is empty. Because the form is canonical,
///         putting back what a change removed gives exactly the sequence from before the change, which is what undo
///         relies on. All text that enters the document is cleaned first (see <see cref="SanitizeText" />), because
///         dropped and pasted text is data from outside. The type is not safe for concurrent use; the user interface
///         thread owns it.
///     </para>
/// </remarks>
public sealed class ComposerDocument
{
    /// <summary>
    ///     The segments in canonical form.
    /// </summary>
    private readonly List<ComposerSegment> _segments = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerDocument" /> class, empty.
    /// </summary>
    public ComposerDocument()
    {
        Segments = new ReadOnlyCollection<ComposerSegment>(_segments);
    }

    /// <summary>
    ///     Gets the segments in order. The list is a live view that changes with the document; take a
    ///     <see cref="Snapshot" /> to keep what the document holds now.
    /// </summary>
    public IReadOnlyList<ComposerSegment> Segments { get; }

    /// <summary>
    ///     Gets the number of positions: one for each character of text and one for each chip.
    /// </summary>
    public int Length => _segments.Sum(segment => segment.Length);

    /// <summary>
    ///     Gets the chips in order.
    /// </summary>
    public IEnumerable<ChipSegment> Chips => _segments.OfType<ChipSegment>();

    /// <summary>
    ///     Gets the text as the editing control holds it: the text of every segment with one placeholder character for
    ///     each chip.
    /// </summary>
    public string FlatText
    {
        get
        {
            var builder = new StringBuilder();
            foreach (var segment in _segments)
            {
                builder.Append(segment is TextSegment text ? text.Text : ComposerSnapshot.ChipPlaceholder);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    ///     Cleans text that comes from outside the application before it can enter the message.
    /// </summary>
    /// <remarks>
    ///     Line breaks of every kind become one line feed, tab and line feed stay, the chip placeholder and every other
    ///     control character are removed. The chip placeholder is removed because a placeholder inside text would be
    ///     taken for a chip and shift every chip after it; other control characters are removed because they have no
    ///     meaning in a message and some confuse editing controls and tools that read the message later. The method
    ///     only removes and replaces characters; it never interprets the text.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <returns>The cleaned text, which is the same string when nothing had to change.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public static string SanitizeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Look first, so that clean text - nearly all text - is returned without being copied
        if (!NeedsCleaning(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            switch (character)
            {
                case '\r':
                    // A carriage return followed by a line feed is one line break, so the pair keeps its line feed only
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        break;
                    }

                    builder.Append('\n');
                    break;
                case '\u2028':
                case '\u2029':
                    builder.Append('\n');
                    break;
                case '\n':
                case '\t':
                    builder.Append(character);
                    break;
                default:
                    if (character != ComposerSnapshot.ChipPlaceholder && !char.IsControl(character))
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Inserts text at an offset.
    /// </summary>
    /// <param name="offset">The offset, from zero to <see cref="Length" />.</param>
    /// <param name="text">The text; it is cleaned first.</param>
    /// <param name="origin">Where the text came from.</param>
    /// <returns>The text that was inserted after cleaning, which can be empty, in which case nothing changed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset" /> is not inside the document.</exception>
    public string InsertText(int offset, string text, TextOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(text);
        var cleaned = SanitizeText(text);
        Insert(offset, [new TextSegment(cleaned, origin)]);
        return cleaned;
    }

    /// <summary>
    ///     Inserts a chip at an offset, between the text before it and after it.
    /// </summary>
    /// <param name="offset">The offset, from zero to <see cref="Length" />.</param>
    /// <param name="chip">The chip.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chip" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset" /> is not inside the document.</exception>
    public void InsertChip(int offset, ChipSegment chip)
    {
        ArgumentNullException.ThrowIfNull(chip);
        Insert(offset, [chip]);
    }

    /// <summary>
    ///     Inserts segments at an offset, the way one drop, one paste, or the undo of one deletion does.
    /// </summary>
    /// <param name="offset">The offset, from zero to <see cref="Length" />.</param>
    /// <param name="segments">The segments; text segments that are empty are ignored and neighbors that belong together are joined.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="segments" /> holds <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset" /> is not inside the document.</exception>
    public void Insert(int offset, IReadOnlyList<ComposerSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Any(segment => segment is null))
        {
            throw new ArgumentException("A segment must not be null.", nameof(segments));
        }

        // Check the offset, then rebuild the sequence from what is before it, the new segments, and what is after it
        var length = Length;
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, length);
        var before = Slice(0, offset);
        var after = Slice(offset, length - offset);
        Replace(Normalize(before.Concat(segments).Concat(after)));
    }

    /// <summary>
    ///     Deletes a range of positions, text and chips alike.
    /// </summary>
    /// <param name="offset">The offset of the first position to delete.</param>
    /// <param name="length">The number of positions to delete; zero deletes nothing.</param>
    /// <returns>The segments that were removed, in order, so that they can be put back.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the document.</exception>
    public IReadOnlyList<ComposerSegment> Delete(int offset, int length)
    {
        var removed = GetSlice(offset, length);
        if (length == 0)
        {
            return removed;
        }

        // Rebuild the sequence from what is before the range and what is after it
        var total = Length;
        var before = Slice(0, offset);
        var after = Slice(offset + length, total - offset - length);
        Replace(Normalize(before.Concat(after)));
        return removed;
    }

    /// <summary>
    ///     Gets the segments that cover a range of positions without changing the document. A text segment that the
    ///     range cuts through is cut.
    /// </summary>
    /// <param name="offset">The offset of the first position.</param>
    /// <param name="length">The number of positions; zero gives an empty list.</param>
    /// <returns>The segments, in order and in canonical form.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the document.</exception>
    public IReadOnlyList<ComposerSegment> GetSlice(int offset, int length)
    {
        var total = Length;
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, total);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, total - offset);
        return Slice(offset, length);
    }

    /// <summary>
    ///     Takes an unchangeable copy of the document.
    /// </summary>
    /// <returns>The snapshot, which later changes of the document do not affect.</returns>
    public ComposerSnapshot Snapshot() => new(_segments);

    /// <summary>
    ///     Removes everything from the document, as sending the message does.
    /// </summary>
    public void Clear() => Replace([]);

    /// <summary>
    ///     Puts a sequence of segments into canonical form: text of the same origin that is next to each other is
    ///     joined, and empty text is dropped.
    /// </summary>
    /// <param name="segments">The segments in order.</param>
    /// <returns>The canonical sequence.</returns>
    internal static List<ComposerSegment> Normalize(IEnumerable<ComposerSegment> segments)
    {
        var result = new List<ComposerSegment>();
        foreach (var segment in segments)
        {
            if (segment is TextSegment { Text.Length: 0 })
            {
                continue;
            }

            // Join text to the text before it when both have the same origin
            if (segment is TextSegment text && result.Count > 0 && result[^1] is TextSegment previous &&
                previous.Origin == text.Origin)
            {
                result[^1] = new TextSegment(previous.Text + text.Text, text.Origin);
                continue;
            }

            result.Add(segment);
        }

        return result;
    }

    /// <summary>
    ///     Determines whether text holds anything that <see cref="SanitizeText" /> changes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true" /> when the text has to be cleaned.</returns>
    private static bool NeedsCleaning(string text)
    {
        foreach (var character in text)
        {
            if (character is '\r' or '\u2028' or '\u2029' or ComposerSnapshot.ChipPlaceholder ||
                (char.IsControl(character) && character is not '\n' and not '\t'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Replaces the stored sequence in place, so the live view of <see cref="Segments" /> shows the new one.
    /// </summary>
    /// <param name="segments">The canonical sequence.</param>
    private void Replace(List<ComposerSegment> segments)
    {
        _segments.Clear();
        _segments.AddRange(segments);
    }

    /// <summary>
    ///     Cuts a range out of the stored sequence; the range has been checked.
    /// </summary>
    /// <param name="offset">The offset of the first position.</param>
    /// <param name="length">The number of positions.</param>
    /// <returns>The segments that cover the range.</returns>
    private List<ComposerSegment> Slice(int offset, int length)
    {
        var result = new List<ComposerSegment>();
        var end = offset + length;
        var position = 0;
        foreach (var segment in _segments)
        {
            var segmentEnd = position + segment.Length;

            // Keep the part of this segment that lies inside the range; text can be cut, a chip is whole or absent
            if (segmentEnd > offset && position < end && length > 0)
            {
                if (segment is TextSegment text)
                {
                    var from = Math.Max(offset, position) - position;
                    var to = Math.Min(end, segmentEnd) - position;
                    result.Add(from == 0 && to == text.Length ? text : new TextSegment(text.Text[from..to], text.Origin));
                }
                else
                {
                    result.Add(segment);
                }
            }

            position = segmentEnd;
            if (position >= end)
            {
                break;
            }
        }

        return result;
    }
}

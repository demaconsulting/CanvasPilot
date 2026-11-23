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

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     One change of the composer that the user can undo and redo as a single step: some positions are taken out at an
///     offset and some segments are put in their place. Every user action is exactly one edit, whatever it contains,
///     which is what gives undo and redo their granularity: one typed or deleted character, one dictated block, one
///     drop or paste of any number of items, one added mark, and one deleted chip are each one step.
/// </summary>
/// <remarks>
///     <para>
///         An edit is made from the document it is going to change, because it remembers what it removes, which is how
///         it can put it back. Each user action has a factory method here, so that the rule for the action lives in one
///         place and is tested once: the method decides what is removed, what is inserted, and where the caret is
///         before and after. The edit is immutable and can be applied and reverted any number of times, which is how
///         redo gives the same step again. An edit refuses to change a document that is not in the state it expects, so
///         a change that bypassed the history is found at once instead of corrupting the message.
///     </para>
/// </remarks>
public sealed class ComposerEdit
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerEdit" /> class.
    /// </summary>
    /// <param name="offset">The offset at which positions are removed and segments inserted.</param>
    /// <param name="removed">The segments that are removed, as they are in the document.</param>
    /// <param name="inserted">The segments that are inserted; they are put into canonical form.</param>
    /// <param name="caretBefore">The caret offset before the edit.</param>
    /// <param name="caretAfter">The caret offset after the edit.</param>
    private ComposerEdit(
        int offset,
        IEnumerable<ComposerSegment> removed,
        IEnumerable<ComposerSegment> inserted,
        int caretBefore,
        int caretAfter)
    {
        Offset = offset;
        Removed = [.. removed];
        Inserted = [.. ComposerDocument.Normalize(inserted)];
        RemovedLength = Removed.Sum(segment => segment.Length);
        InsertedLength = Inserted.Sum(segment => segment.Length);
        CaretBefore = caretBefore;
        CaretAfter = caretAfter;
    }

    /// <summary>
    ///     Gets the offset at which the edit removes and inserts.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    ///     Gets the segments the edit removes.
    /// </summary>
    public ImmutableArray<ComposerSegment> Removed { get; }

    /// <summary>
    ///     Gets the segments the edit inserts.
    /// </summary>
    public ImmutableArray<ComposerSegment> Inserted { get; }

    /// <summary>
    ///     Gets the number of positions the edit removes.
    /// </summary>
    public int RemovedLength { get; }

    /// <summary>
    ///     Gets the number of positions the edit inserts.
    /// </summary>
    public int InsertedLength { get; }

    /// <summary>
    ///     Gets the caret offset to restore when the edit is undone.
    /// </summary>
    public int CaretBefore { get; }

    /// <summary>
    ///     Gets the caret offset to restore when the edit is applied or redone.
    /// </summary>
    public int CaretAfter { get; }

    /// <summary>
    ///     Gets a value indicating whether the edit changes nothing, which is the case for an empty text or no items.
    ///     The history does not record such an edit, so it never makes an undo step that does nothing.
    /// </summary>
    public bool IsEmpty => RemovedLength == 0 && InsertedLength == 0;

    /// <summary>
    ///     Makes the edit for typing: text the user typed, which replaces the selection when there is one. Typing one
    ///     character is one edit, and typing over a selection is one edit.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset at which the text is typed, which is the start of the selection.</param>
    /// <param name="text">The typed text; it is cleaned first.</param>
    /// <param name="replaceLength">The length of the selection that the text replaces; zero when there is none.</param>
    /// <param name="caretBefore">The caret offset before the edit; by default the end of the selection.</param>
    /// <returns>The edit, which is empty when there is neither text nor a selection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> or <paramref name="text" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the document.</exception>
    public static ComposerEdit TypeText(
        ComposerDocument document,
        int offset,
        string text,
        int replaceLength = 0,
        int? caretBefore = null) =>
        InsertText(document, offset, text, TextOrigin.Typed, replaceLength, caretBefore);

    /// <summary>
    ///     Makes the edit for a block of dictated text, which is inserted at the caret, or over the selection, as one
    ///     piece however many words it holds.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The recognized text; it is cleaned first.</param>
    /// <param name="replaceLength">The length of the selection that the text replaces; zero when there is none.</param>
    /// <param name="caretBefore">The caret offset before the edit; by default the end of the selection.</param>
    /// <returns>The edit, which is empty for empty text so that nothing is recorded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> or <paramref name="text" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the document.</exception>
    public static ComposerEdit Dictate(
        ComposerDocument document,
        int offset,
        string text,
        int replaceLength = 0,
        int? caretBefore = null) =>
        InsertText(document, offset, text, TextOrigin.Dictated, replaceLength, caretBefore);

    /// <summary>
    ///     Makes the edit for text that is pasted or dropped as text, which is one piece however long it is.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The pasted text; it is cleaned first.</param>
    /// <param name="replaceLength">The length of the selection that the text replaces; zero when there is none.</param>
    /// <param name="caretBefore">The caret offset before the edit; by default the end of the selection.</param>
    /// <returns>The edit, which is empty for empty text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> or <paramref name="text" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the document.</exception>
    public static ComposerEdit Paste(
        ComposerDocument document,
        int offset,
        string text,
        int replaceLength = 0,
        int? caretBefore = null) =>
        InsertText(document, offset, text, TextOrigin.Typed, replaceLength, caretBefore);

    /// <summary>
    ///     Makes the edit for dropping or pasting items: all the chips and text of one operation are one edit.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset at which the items are inserted.</param>
    /// <param name="items">The segments of the operation, in order.</param>
    /// <param name="replaceLength">The length of the selection that the items replace; zero when there is none.</param>
    /// <param name="caretBefore">The caret offset before the edit; by default the end of the selection.</param>
    /// <returns>The edit, which is empty when there are no items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> or <paramref name="items" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="items" /> holds <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the document.</exception>
    public static ComposerEdit Drop(
        ComposerDocument document,
        int offset,
        IReadOnlyList<ComposerSegment> items,
        int replaceLength = 0,
        int? caretBefore = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Any(item => item is null))
        {
            throw new ArgumentException("An item must not be null.", nameof(items));
        }

        return Create(document, offset, replaceLength, items, caretBefore);
    }

    /// <summary>
    ///     Makes the edit for adding a mark: the chip of the new mark is inserted, which is one edit.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset at which the chip is inserted, which is the caret.</param>
    /// <param name="chip">The chip of the mark.</param>
    /// <returns>The edit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> or <paramref name="chip" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is not inside the document.</exception>
    public static ComposerEdit AddMark(ComposerDocument document, int offset, MarkChip chip)
    {
        ArgumentNullException.ThrowIfNull(chip);
        return Create(document, offset, 0, [chip], null);
    }

    /// <summary>
    ///     Makes the edit for adding chips that the user did not place in the text, such as the chip of a new mark or an
    ///     attached file: the chips go to the end of the row of chips at the start of the message, and a line feed after
    ///     the row, when the message does not have one there already, puts the words on the line below. The caret keeps
    ///     its place in the words, so that typing goes on where it was; when it was in or before the row it goes to the
    ///     start of the words.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="chips">The chips to add, in order.</param>
    /// <param name="caretOffset">The caret offset before the edit.</param>
    /// <returns>The edit, which is empty when there are no chips.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="chips" /> holds <see langword="null" />.</exception>
    public static ComposerEdit AddLeadingChips(ComposerDocument document, IReadOnlyList<ChipSegment> chips, int caretOffset)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(chips);
        if (chips.Any(chip => chip is null))
        {
            throw new ArgumentException("A chip must not be null.", nameof(chips));
        }

        if (chips.Count == 0)
        {
            return Create(document, 0, 0, [], caretOffset);
        }

        // Find the end of the row of chips at the start, and whether a line feed follows it
        var rowLength = 0;
        var hasBreak = false;
        foreach (var segment in document.Segments)
        {
            if (segment is not ChipSegment)
            {
                hasBreak = segment is TextSegment { Text: ['\n', ..] };
                break;
            }

            rowLength += segment.Length;
        }

        List<ComposerSegment> inserted = [.. chips];
        if (!hasBreak)
        {
            inserted.Add(new TextSegment("\n", TextOrigin.Typed));
        }

        // The words move down by what is inserted; a caret in or before the row goes to the start of the words
        var insertedLength = ComposerDocument.Normalize(inserted).Sum(segment => segment.Length);
        var caretAfter = caretOffset <= rowLength ? rowLength + chips.Count + 1 : caretOffset + insertedLength;
        return new ComposerEdit(rowLength, [], inserted, caretOffset, caretAfter);
    }

    /// <summary>
    ///     Makes the edit for deleting a range of text or chips with the Backspace or Delete key, or by deleting the
    ///     selection. Deleting one character is one edit and deleting a selection is one edit.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset of the first position to delete.</param>
    /// <param name="length">The number of positions to delete.</param>
    /// <param name="caretBefore">
    ///     The caret offset before the edit; by default the end of the range, as it is after Backspace or after deleting
    ///     a selection. Use the start of the range for the Delete key.
    /// </param>
    /// <returns>The edit, which is empty when <paramref name="length" /> is zero.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the document.</exception>
    public static ComposerEdit Delete(ComposerDocument document, int offset, int length, int? caretBefore = null) =>
        Create(document, offset, length, [], caretBefore);

    /// <summary>
    ///     Makes the edit for deleting one chip with its delete button, which is one edit.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset of the chip.</param>
    /// <returns>The edit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is not inside the document.</exception>
    /// <exception cref="ArgumentException">Thrown when the position at <paramref name="offset" /> is text and not a chip.</exception>
    public static ComposerEdit DeleteChip(ComposerDocument document, int offset)
    {
        ArgumentNullException.ThrowIfNull(document);
        var slice = document.GetSlice(offset, Math.Min(1, document.Length - offset));
        if (slice.Count != 1 || slice[0] is not ChipSegment)
        {
            throw new ArgumentException("There is no chip at the offset.", nameof(offset));
        }

        return Create(document, offset, 1, [], offset + 1);
    }

    /// <summary>
    ///     Changes the document the way the edit says: removes the positions and inserts the segments.
    /// </summary>
    /// <param name="document">The document, in the state the edit was made from or has been reverted to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown, without changing the document, when the document does not hold what the edit expects to remove.
    /// </exception>
    public void Apply(ComposerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Exchange(document, RemovedLength, Removed, Inserted);
    }

    /// <summary>
    ///     Undoes the edit: removes what it inserted and puts back what it removed.
    /// </summary>
    /// <param name="document">The document, in the state the edit left it in.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown, without changing the document, when the document does not hold what the edit inserted.
    /// </exception>
    public void Revert(ComposerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Exchange(document, InsertedLength, Inserted, Removed);
    }

    /// <summary>
    ///     Makes an edit that inserts text of one origin.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The text.</param>
    /// <param name="origin">The origin of the text.</param>
    /// <param name="replaceLength">The length of the selection that the text replaces.</param>
    /// <param name="caretBefore">The caret offset before the edit, or <see langword="null" /> for the end of the selection.</param>
    /// <returns>The edit.</returns>
    private static ComposerEdit InsertText(
        ComposerDocument document,
        int offset,
        string text,
        TextOrigin origin,
        int replaceLength,
        int? caretBefore)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Create(document, offset, replaceLength, [new TextSegment(text, origin)], caretBefore);
    }

    /// <summary>
    ///     Makes an edit from the document's current content.
    /// </summary>
    /// <param name="document">The document the edit is going to change.</param>
    /// <param name="offset">The offset at which positions are removed and segments inserted.</param>
    /// <param name="removeLength">The number of positions to remove.</param>
    /// <param name="inserted">The segments to insert.</param>
    /// <param name="caretBefore">The caret offset before the edit, or <see langword="null" /> for the end of the removed range.</param>
    /// <returns>The edit.</returns>
    private static ComposerEdit Create(
        ComposerDocument document,
        int offset,
        int removeLength,
        IReadOnlyList<ComposerSegment> inserted,
        int? caretBefore)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Read what is removed now, because it is what undo has to put back; this also checks the range
        var removed = document.GetSlice(offset, removeLength);
        var insertedLength = ComposerDocument.Normalize(inserted).Sum(segment => segment.Length);
        return new ComposerEdit(offset, removed, inserted, caretBefore ?? offset + removeLength, offset + insertedLength);
    }

    /// <summary>
    ///     Replaces what the document holds at the edit's offset: checks that it holds what is expected, removes it, and
    ///     inserts the replacement.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="expectedLength">The number of positions to remove.</param>
    /// <param name="expected">The segments the document must hold there.</param>
    /// <param name="replacement">The segments to put there.</param>
    private void Exchange(
        ComposerDocument document,
        int expectedLength,
        ImmutableArray<ComposerSegment> expected,
        ImmutableArray<ComposerSegment> replacement)
    {
        // Check before changing anything, so a document that was changed behind the history is never made worse
        if (Offset + expectedLength > document.Length ||
            !document.GetSlice(Offset, expectedLength).SequenceEqual(expected))
        {
            throw new InvalidOperationException("The document does not hold what the edit expects.");
        }

        document.Delete(Offset, expectedLength);
        document.Insert(Offset, replacement);
    }
}

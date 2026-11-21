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
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     Holds the marks that the user has drawn for the message being composed, each pinned to the exact picture it was
///     drawn on, numbered in the order they were made, with its geometry in the pixels of that picture and an optional
///     short note.
/// </summary>
/// <remarks>
///     <para>
///         The registry does not decide which marks belong to the message: a mark is part of the message exactly while
///         its chip is in the composer's text, so the caller passes the identities of the mark chips it holds (see
///         <see cref="ActiveMarks" />). This keeps one undo system for text, chips, and marks: undoing the addition of
///         a mark removes its chip, which hides the mark, and redoing brings both back, with no separate undo of marks
///         here.
///     </para>
///     <para>
///         A mark's number is the largest number in the registry plus one and is never changed afterwards, so a mark
///         that the user undid and drew again does not take over the number of the one that was undone. The registry
///         is emptied when the message is sent. It is not safe for concurrent use: the user interface thread owns it;
///         <see cref="Marks" /> gives an immutable copy that other threads may read.
///     </para>
/// </remarks>
public sealed class MarkRegistry
{
    /// <summary>
    ///     The longest note a mark keeps, in characters. A longer note is cut, so that a mark's note stays a short
    ///     remark and not a second message.
    /// </summary>
    public const int MaximumNoteLength = 200;

    /// <summary>
    ///     The marks, in the order they were made.
    /// </summary>
    private readonly List<Mark> _marks = [];

    /// <summary>
    ///     The source of the time of creation; the system clock by default, a fixed clock in tests.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MarkRegistry" /> class.
    /// </summary>
    /// <param name="timeProvider">The source of the time at which marks are made; the system clock when <see langword="null" />.</param>
    public MarkRegistry(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    ///     Gets an unchangeable copy of all marks that were made since the registry was last cleared, in the order they
    ///     were made, whether or not their chips are still in the message.
    /// </summary>
    public ImmutableArray<Mark> Marks => [.. _marks];

    /// <summary>
    ///     Makes a mark on a picture and adds it to the registry.
    /// </summary>
    /// <param name="snapshot">The exact picture the user drew on; the mark is pinned to it for good.</param>
    /// <param name="kind">The shape of the mark.</param>
    /// <param name="points">
    ///     The points the pointer visited, in the pixels of <paramref name="snapshot" />; see
    ///     <see cref="MarkGeometry.Create" /> for how they are put into normal form.
    /// </param>
    /// <param name="note">The short note, or <see langword="null" /> for none; see <see cref="CleanNote" />.</param>
    /// <returns>The new mark, with the next number.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the snapshot or the points are <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the points do not make a mark, for example because the shape is a click; see
    ///     <see cref="MarkGeometry.Create" />. The registry does not change in that case.
    /// </exception>
    public Mark Add(RenderedImage snapshot, MarkKind kind, IReadOnlyList<RenderPoint> points, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Make the geometry first: it throws for a shape that is no mark, and then nothing has been added
        var geometry = MarkGeometry.Create(kind, points, snapshot.Width, snapshot.Height);

        // The number is one more than the largest number so far, so a number is never used twice in a draft
        var number = (_marks.Count == 0 ? 0 : _marks.Max(mark => mark.Number)) + 1;
        var mark = new Mark(Guid.NewGuid(), number, geometry, CleanNote(note), snapshot, _timeProvider.GetUtcNow());
        _marks.Add(mark);
        return mark;
    }

    /// <summary>
    ///     Finds a mark by its identity.
    /// </summary>
    /// <param name="markId">The identity, as carried by the mark's chip.</param>
    /// <returns>The mark, or <see langword="null" /> when the registry holds no such mark.</returns>
    public Mark? Find(Guid markId)
    {
        return _marks.FirstOrDefault(mark => mark.Id == markId);
    }

    /// <summary>
    ///     Gets the marks that are part of the message: those whose chips are in it.
    /// </summary>
    /// <param name="markIds">The identities of the mark chips that are in the message; repeated or unknown ones are ignored.</param>
    /// <returns>The marks, in the order of their numbers, whatever picture they are on.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markIds" /> is <see langword="null" />.</exception>
    public IReadOnlyList<Mark> ActiveMarks(IEnumerable<Guid> markIds)
    {
        ArgumentNullException.ThrowIfNull(markIds);

        var wanted = markIds.ToHashSet();
        return [.. _marks.Where(mark => wanted.Contains(mark.Id)).OrderBy(mark => mark.Number)];
    }

    /// <summary>
    ///     Gets the marks to show on a picture: those that are part of the message and were drawn on exactly this
    ///     picture. Marks that were drawn on an earlier version of the file stay in the message but are not shown on the
    ///     new picture, because their geometry means the pixels of the picture they were drawn on.
    /// </summary>
    /// <param name="markIds">The identities of the mark chips that are in the message.</param>
    /// <param name="snapshotKey">The key of the picture that is shown (<see cref="RenderedImage.SnapshotKey" />).</param>
    /// <returns>The marks, in the order of their numbers.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public IReadOnlyList<Mark> VisibleMarks(IEnumerable<Guid> markIds, string snapshotKey)
    {
        ArgumentNullException.ThrowIfNull(snapshotKey);

        return [.. ActiveMarks(markIds).Where(mark => string.Equals(mark.Snapshot.SnapshotKey, snapshotKey, StringComparison.Ordinal))];
    }

    /// <summary>
    ///     Removes every mark, which is what happens when the message is sent; the next mark is number one again.
    /// </summary>
    public void Clear()
    {
        _marks.Clear();
    }

    /// <summary>
    ///     Makes a note fit to keep with a mark: one line without control characters and at most
    ///     <see cref="MaximumNoteLength" /> characters, never cut in the middle of a character that needs two UTF-16
    ///     code units.
    /// </summary>
    /// <param name="note">The note as typed, or <see langword="null" />.</param>
    /// <returns>The cleaned note, or <see langword="null" /> when nothing is left.</returns>
    public static string? CleanNote(string? note)
    {
        if (note is null)
        {
            return null;
        }

        // Replace every control character, line breaks and tabs included, with a space, so the note is one line
        var builder = new StringBuilder(note.Length);
        foreach (var character in note)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        // Trim, then cut to the limit without leaving half of a surrogate pair
        var text = builder.ToString().Trim();
        if (text.Length > MaximumNoteLength)
        {
            var length = char.IsHighSurrogate(text[MaximumNoteLength - 1]) ? MaximumNoteLength - 1 : MaximumNoteLength;
            text = text[..length].TrimEnd();
        }

        return text.Length == 0 ? null : text;
    }
}

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

using System.Globalization;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     Where a piece of text in the message came from, which the message keeps so that the agent can tell what the
///     user typed from what the user said.
/// </summary>
public enum TextOrigin
{
    /// <summary>
    ///     The text was typed, pasted, or dropped as text.
    /// </summary>
    Typed,

    /// <summary>
    ///     The text was recognized from speech.
    /// </summary>
    Dictated
}

/// <summary>
///     One piece of the message in the composer: a run of text or a chip. The composer's text is an ordered sequence
///     of segments, and every position in it, an offset, counts one for each UTF-16 code unit of text and one for
///     each chip, which is the same counting as the editing control uses when it shows a chip as one placeholder
///     character.
/// </summary>
/// <remarks>
///     Segments are immutable values, so a snapshot of the composer, an edit, and the history can share them without
///     copying and without one of them changing what another holds. The set of segment types is closed: only this
///     file derives from the type.
/// </remarks>
public abstract record ComposerSegment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerSegment" /> class. Not accessible outside the
    ///     assembly's composer types, which keeps the set of segment types closed.
    /// </summary>
    private protected ComposerSegment()
    {
    }

    /// <summary>
    ///     Gets the number of positions the segment occupies: its length in characters for text, and one for a chip.
    /// </summary>
    public abstract int Length { get; }
}

/// <summary>
///     A run of text of one origin. The text is cleaned when the segment is made, so no segment can hold the
///     placeholder character that stands for a chip or a control character that could confuse the editing control
///     or the agent.
/// </summary>
public sealed record TextSegment : ComposerSegment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TextSegment" /> class.
    /// </summary>
    /// <param name="text">
    ///     The text. Line breaks become a line feed, other control characters and the chip placeholder are removed (see
    ///     <see cref="ComposerDocument.SanitizeText" />).
    /// </param>
    /// <param name="origin">Where the text came from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public TextSegment(string text, TextOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = ComposerDocument.SanitizeText(text);
        Origin = origin;
    }

    /// <summary>
    ///     Gets the cleaned text, which can be empty only for a segment that is not part of a document.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets where the text came from.
    /// </summary>
    public TextOrigin Origin { get; }

    /// <inheritdoc />
    public override int Length => Text.Length;
}

/// <summary>
///     A chip: one object in the message that stands for something other than text, and occupies one position.
/// </summary>
public abstract record ChipSegment : ComposerSegment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChipSegment" /> class.
    /// </summary>
    private protected ChipSegment()
    {
    }

    /// <inheritdoc />
    public sealed override int Length => 1;
}

/// <summary>
///     A chip for something on the user's computer that is attached to the message: a file, a folder, or an image.
///     The chip holds only where the thing is. It never holds or opens the content, which is treated as data until it
///     is copied into the project when the message is sent.
/// </summary>
public abstract record AttachmentChip : ChipSegment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AttachmentChip" /> class.
    /// </summary>
    /// <param name="fullPath">The full path of the thing that is attached.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fullPath" /> is null, empty, or blank.</exception>
    private protected AttachmentChip(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        FullPath = fullPath;
    }

    /// <summary>
    ///     Gets the full path of the thing that is attached, as it was when it was dropped or pasted.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    ///     Gets the name to show on the chip: the last part of the path, or the whole path when it has no last part
    ///     (the root of a drive).
    /// </summary>
    public string Name
    {
        get
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(FullPath));
            return name.Length > 0 ? name : FullPath;
        }
    }
}

/// <summary>
///     A chip for a file that is attached to the message.
/// </summary>
public sealed record FileChip : AttachmentChip
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileChip" /> class.
    /// </summary>
    /// <param name="fullPath">The full path of the file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fullPath" /> is null, empty, or blank.</exception>
    public FileChip(string fullPath)
        : base(fullPath)
    {
    }
}

/// <summary>
///     A chip for a folder that is attached to the message, with everything in it.
/// </summary>
public sealed record FolderChip : AttachmentChip
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FolderChip" /> class.
    /// </summary>
    /// <param name="fullPath">The full path of the folder.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fullPath" /> is null, empty, or blank.</exception>
    public FolderChip(string fullPath)
        : base(fullPath)
    {
    }
}

/// <summary>
///     A chip for a picture that is attached to the message, either a picture file that was dropped, or a picture that
///     was pasted from the clipboard and saved in the project's private data folder.
/// </summary>
public sealed record ImageChip : AttachmentChip
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageChip" /> class.
    /// </summary>
    /// <param name="fullPath">The full path of the picture file.</param>
    /// <param name="isPasted">
    ///     <see langword="true" /> when the application saved the picture from the clipboard, so that it belongs to
    ///     the draft and is moved into the project, or removed, by the application; <see langword="false" /> for a
    ///     file of the user, which is only copied.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fullPath" /> is null, empty, or blank.</exception>
    public ImageChip(string fullPath, bool isPasted = false)
        : base(fullPath)
    {
        IsPasted = isPasted;
    }

    /// <summary>
    ///     Gets a value indicating whether the application saved the picture from the clipboard.
    /// </summary>
    public bool IsPasted { get; }
}

/// <summary>
///     A chip for a mark that the user drew on the shown picture. The composer knows a mark only by its identity and
///     its number; the marks themselves are kept elsewhere, and a mark is part of the message only while its chip is
///     in the text.
/// </summary>
public sealed record MarkChip : ChipSegment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MarkChip" /> class.
    /// </summary>
    /// <param name="markId">The identity of the mark.</param>
    /// <param name="number">The number the mark shows, one or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="number" /> is less than one.</exception>
    public MarkChip(Guid markId, int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        MarkId = markId;
        Number = number;
    }

    /// <summary>
    ///     Gets the identity of the mark.
    /// </summary>
    public Guid MarkId { get; }

    /// <summary>
    ///     Gets the number the mark shows.
    /// </summary>
    public int Number { get; }

    /// <summary>
    ///     Gets the label of the chip, for example <c>Mark 2</c>.
    /// </summary>
    public string Label => string.Create(CultureInfo.InvariantCulture, $"Mark {Number}");
}

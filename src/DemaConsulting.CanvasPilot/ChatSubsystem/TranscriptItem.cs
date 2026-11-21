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

using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     One piece of a sent message as the transcript shows it: a run of typed or dictated text, or a chip for an
///     attachment or a mark.
/// </summary>
public sealed class TranscriptItem
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TranscriptItem" /> class for a run of text.
    /// </summary>
    /// <param name="text">The text, which is not empty.</param>
    /// <param name="origin">Whether the text was typed or dictated.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public TranscriptItem(string text, TextOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        Origin = origin;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="TranscriptItem" /> class for a chip.
    /// </summary>
    /// <param name="chip">The chip as it was in the message.</param>
    /// <param name="thumbnailChip">
    ///     The chip to ask the provider for a picture of: the chip itself, or, for an attached picture, a chip for the copy
    ///     of the file in the project, because the original may have been moved when the message was sent.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public TranscriptItem(ChipSegment chip, ChipSegment thumbnailChip)
    {
        ArgumentNullException.ThrowIfNull(chip);
        ArgumentNullException.ThrowIfNull(thumbnailChip);

        Chip = chip;
        ThumbnailChip = thumbnailChip;
    }

    /// <summary>
    ///     Gets the text of a run of text, or <see langword="null" /> for a chip.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    ///     Gets whether a run of text was typed or dictated; <see cref="TextOrigin.Typed" /> for a chip.
    /// </summary>
    public TextOrigin Origin { get; }

    /// <summary>
    ///     Gets the chip, or <see langword="null" /> for a run of text.
    /// </summary>
    public ChipSegment? Chip { get; }

    /// <summary>
    ///     Gets the chip that the picture of this item is asked for, or <see langword="null" /> for a run of text.
    /// </summary>
    public ChipSegment? ThumbnailChip { get; }

    /// <summary>
    ///     Gets a value indicating whether the item is a chip.
    /// </summary>
    public bool IsChip => Chip is not null;
}

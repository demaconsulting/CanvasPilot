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

using Avalonia.Controls;
using AvaloniaEdit.Rendering;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     Turns each chip placeholder character of the editing control into an inline control, so that the chip is drawn
///     in the line of text, where it can be selected, deleted with the keys, and moved past with the caret like one
///     character.
/// </summary>
/// <remarks>
///     <para>
///         The generator relies on the positional mapping of the message: the n-th chip placeholder in the text of the
///         control is the n-th chip of the message. It therefore counts the placeholders before an offset to know which
///         chip to ask for, and holds no chip of its own, so that nothing can get out of step with the message.
///     </para>
///     <para>
///         The control calls the generator whenever a line is drawn again, not only when text is typed, so a new control
///         is made each time and the work is kept small. When there is no chip for a placeholder, which can be the case
///         for a moment while the control and the message are brought together, no element is made and the
///         placeholder is drawn as a character.
///     </para>
/// </remarks>
public sealed class ChipElementGenerator : VisualLineElementGenerator
{
    /// <summary>
    ///     Makes the control that shows the chip with a number, or <see langword="null" /> when there is none.
    /// </summary>
    private readonly Func<int, Control?> _createChip;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChipElementGenerator" /> class.
    /// </summary>
    /// <param name="createChip">
    ///     Makes the control that shows the chip with a number counted from zero in the order of the message, or gives
    ///     <see langword="null" /> when the message has no such chip.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="createChip" /> is <see langword="null" />.</exception>
    public ChipElementGenerator(Func<int, Control?> createChip)
    {
        ArgumentNullException.ThrowIfNull(createChip);
        _createChip = createChip;
    }

    /// <inheritdoc />
    public override int GetFirstInterestedOffset(int startOffset)
    {
        // Look for the next placeholder in the rest of the line
        var end = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
        for (var offset = startOffset; offset < end; offset++)
        {
            if (CurrentContext.Document.GetCharAt(offset) == ComposerSnapshot.ChipPlaceholder)
            {
                return offset;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public override VisualLineElement? ConstructElement(int offset)
    {
        if (CurrentContext.Document.GetCharAt(offset) != ComposerSnapshot.ChipPlaceholder)
        {
            return null;
        }

        // The number of placeholders before this one is the number of its chip
        var index = 0;
        for (var position = 0; position < offset; position++)
        {
            if (CurrentContext.Document.GetCharAt(position) == ComposerSnapshot.ChipPlaceholder)
            {
                index++;
            }
        }

        var control = _createChip(index);
        return control is null ? null : new InlineObjectElement(1, control);
    }
}

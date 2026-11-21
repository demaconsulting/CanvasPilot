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

namespace DemaConsulting.CanvasPilot.PayloadSubsystem;

/// <summary>
///     One piece of the message in the order the user placed it: a run of text, a reference to an attachment, or a
///     reference to a mark. The sequence of segments is the message with its structure; <see cref="MessagePayload.Text" />
///     is the same message as one string.
/// </summary>
/// <remarks>
///     The set of segment types is closed: only this file derives from the type. Segments are immutable values.
/// </remarks>
public abstract record PayloadSegment
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadSegment" /> class. Not accessible outside the payload
    ///     types, which keeps the set of segment types closed.
    /// </summary>
    private protected PayloadSegment()
    {
    }
}

/// <summary>
///     A run of text of one origin, so that the reader can tell what the user typed from what the user said.
/// </summary>
/// <param name="Text">The text, as it was in the message.</param>
/// <param name="Origin">Whether the text was typed or dictated.</param>
public sealed record PayloadTextPart(string Text, TextOrigin Origin) : PayloadSegment;

/// <summary>
///     A reference to an attachment at this place in the message.
/// </summary>
/// <param name="Index">The one-based index of the attachment in <see cref="MessagePayload.Attachments" />.</param>
public sealed record PayloadAttachmentPart(int Index) : PayloadSegment;

/// <summary>
///     A reference to a mark at this place in the message.
/// </summary>
/// <param name="Number">The number of the mark, which is the number it shows on the picture.</param>
public sealed record PayloadMarkPart(int Number) : PayloadSegment;

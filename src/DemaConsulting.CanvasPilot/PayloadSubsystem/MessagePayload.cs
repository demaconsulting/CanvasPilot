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

namespace DemaConsulting.CanvasPilot.PayloadSubsystem;

/// <summary>
///     The message that the user sent, in the form in which the AI copilot receives it: the text with its references in
///     order, the attachments with their paths, and the pictures with their annotated images, mark crops, mark geometry
///     in picture pixels, and the parts of the source file under each mark. It is the contract between the chat and the
///     copilot.
/// </summary>
/// <remarks>
///     <para>
///         The object is immutable: it copies the lists it is given and its parts copy their bytes, so a draft that the
///         user goes on editing, a registry that is cleared, or a buffer that is reused cannot change a message that has
///         been built. It holds no native or disposable resources and may be shared between threads.
///     </para>
///     <para>
///         The contract is versioned: <see cref="Version" /> is <see cref="ContractVersion" />, and a reader that does
///         not know a version must not guess at its meaning.
///     </para>
/// </remarks>
public sealed class MessagePayload
{
    /// <summary>
    ///     The version of the contract that this class and its parts describe.
    /// </summary>
    public const int ContractVersion = 1;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessagePayload" /> class.
    /// </summary>
    /// <param name="text">
    ///     The message as one string: the text of the message with <c>[Mark n]</c> and <c>[Attachment n: name]</c> at the
    ///     places of the marks and attachments.
    /// </param>
    /// <param name="segments">The message as segments in the order the user placed them; copied.</param>
    /// <param name="attachments">The attachments, in order; copied.</param>
    /// <param name="images">The pictures that marks were drawn on; copied.</param>
    /// <param name="createdUtc">When the payload was built.</param>
    /// <exception cref="ArgumentNullException">Thrown when the text or a list is <see langword="null" />.</exception>
    public MessagePayload(
        string text,
        IEnumerable<PayloadSegment> segments,
        IEnumerable<PayloadAttachment> attachments,
        IEnumerable<PayloadImage> images,
        DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(images);

        Text = text;
        Segments = [.. segments];
        Attachments = [.. attachments];
        Images = [.. images];
        CreatedUtc = createdUtc;
    }

    /// <summary>
    ///     Gets the version of the contract, which is <see cref="ContractVersion" />.
    /// </summary>
    public int Version { get; } = ContractVersion;

    /// <summary>
    ///     Gets the message as one string, with <c>[Mark n]</c> and <c>[Attachment n: name]</c> at the places of the marks
    ///     and attachments, in the order the user placed them.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the message as segments, in the order the user placed them, so that a reader can tell typed text from
    ///     dictated text and find the references without parsing <see cref="Text" />.
    /// </summary>
    public ImmutableArray<PayloadSegment> Segments { get; }

    /// <summary>
    ///     Gets the attachments, in the order their chips were in the message.
    /// </summary>
    public ImmutableArray<PayloadAttachment> Attachments { get; }

    /// <summary>
    ///     Gets the pictures that marks were drawn on, one for each distinct picture, in the order of the first mark that
    ///     refers to it; empty when the message has no marks.
    /// </summary>
    public ImmutableArray<PayloadImage> Images { get; }

    /// <summary>
    ///     Gets when the payload was built.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }
}

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
using DemaConsulting.CanvasPilot.AnnotationSubsystem;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     A message that was sent, exactly as it was when the user sent it: what the composer held, the marks that were
///     part of it, the attachments as they are in the project, and what was built from them for the assistant. Nothing in
///     it can change, so the transcript and the payload inspector can keep it as long as they like.
/// </summary>
public sealed class SentMessage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SentMessage" /> class.
    /// </summary>
    /// <param name="message">What the composer held when the message was sent.</param>
    /// <param name="marks">The marks whose chips were in the message, in the order of their numbers.</param>
    /// <param name="attachments">The attachments as they are in the project, in the order of the chips.</param>
    /// <param name="payload">What was built from the message for the assistant; the payload of the payload subsystem.</param>
    /// <param name="sentUtc">The time at which the message was sent.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than the time is <see langword="null" />.</exception>
    public SentMessage(
        ComposerSnapshot message,
        IEnumerable<Mark> marks,
        IEnumerable<StagedAttachment> attachments,
        object payload,
        DateTimeOffset sentUtc)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(payload);

        Message = message;
        Marks = [.. marks];
        Attachments = [.. attachments];
        Payload = payload;
        SentUtc = sentUtc;
    }

    /// <summary>
    ///     Gets what the composer held when the message was sent: its text with the origin of each part, and its chips
    ///     in order.
    /// </summary>
    public ComposerSnapshot Message { get; }

    /// <summary>
    ///     Gets the marks whose chips were in the message, in the order of their numbers.
    /// </summary>
    public ImmutableArray<Mark> Marks { get; }

    /// <summary>
    ///     Gets the attachments as they are in the project, in the order of the chips of the message.
    /// </summary>
    public ImmutableArray<StagedAttachment> Attachments { get; }

    /// <summary>
    ///     Gets what was built from the message for the assistant. It is the immutable payload of the payload subsystem
    ///     (<c>MessagePayload</c>); the composer holds it as an object because the payload subsystem uses the composer
    ///     and not the other way round.
    /// </summary>
    public object Payload { get; }

    /// <summary>
    ///     Gets the time at which the message was sent.
    /// </summary>
    public DateTimeOffset SentUtc { get; }
}

/// <summary>
///     The event data of <see cref="ComposerViewModel.MessageSent" />.
/// </summary>
public sealed class MessageSentEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MessageSentEventArgs" /> class.
    /// </summary>
    /// <param name="message">The message that was sent.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message" /> is <see langword="null" />.</exception>
    public MessageSentEventArgs(SentMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Message = message;
    }

    /// <summary>
    ///     Gets the message that was sent.
    /// </summary>
    public SentMessage Message { get; }
}

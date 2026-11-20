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

using DemaConsulting.CanvasPilot.AnnotationSubsystem;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     What the composer needs to turn the message that the user sends into what is kept and given to the assistant. The
///     interface is owned by the composer, and the payload subsystem implements it with its <c>PayloadBuilder</c>, so
///     that the composer can send a message without depending on the subsystem that depends on it.
/// </summary>
public interface IMessageBuilder
{
    /// <summary>
    ///     Builds the sent message from what the composer held and the attachments as they are in the project. It may run
    ///     on a thread-pool thread and only reads the values it is given, which cannot change.
    /// </summary>
    /// <param name="message">What the composer held when the message was sent.</param>
    /// <param name="marks">The marks that were made for the draft; the builder uses those whose chips are in the message.</param>
    /// <param name="attachments">The attachments as they are in the project, in the order of the chips of the message.</param>
    /// <param name="cancellationToken">Cancels the build before it starts.</param>
    /// <returns>A task with the sent message; it fails when the message cannot be built.</returns>
    Task<SentMessage> BuildAsync(
        ComposerSnapshot message,
        IReadOnlyList<Mark> marks,
        IReadOnlyList<StagedAttachment> attachments,
        CancellationToken cancellationToken = default);
}

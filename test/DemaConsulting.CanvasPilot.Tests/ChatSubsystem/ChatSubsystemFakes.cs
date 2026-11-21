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
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A builder of sent messages for the chat tests that gives every message the same ready-made payload, so that the
///     transcript and the payload inspector can be tested without drawing pictures.
/// </summary>
internal sealed class PayloadMessageBuilder : IMessageBuilder
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadMessageBuilder" /> class.
    /// </summary>
    /// <param name="payload">The payload that every sent message gets.</param>
    public PayloadMessageBuilder(MessagePayload payload)
    {
        Payload = payload;
    }

    /// <summary>
    ///     Gets the payload that every sent message gets.
    /// </summary>
    public MessagePayload Payload { get; }

    /// <inheritdoc />
    public Task<SentMessage> BuildAsync(
        ComposerSnapshot message,
        IReadOnlyList<Mark> marks,
        IReadOnlyList<StagedAttachment> attachments,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SentMessage(message, marks, attachments, Payload, FakeMessageBuilder.Now));
}

/// <summary>
///     A launcher of the payload inspector for the chat tests that records what it was asked to show.
/// </summary>
internal sealed class RecordingInspectorLauncher : IPayloadInspectorLauncher
{
    /// <summary>
    ///     Gets what the launcher was asked to show, in order.
    /// </summary>
    public List<PayloadInspectorViewModel> Shown { get; } = [];

    /// <inheritdoc />
    public Task ShowPayloadInspectorAsync(PayloadInspectorViewModel viewModel)
    {
        Shown.Add(viewModel);
        return Task.CompletedTask;
    }
}

/// <summary>
///     Makes payloads for the chat tests.
/// </summary>
internal static class ChatTestPayloads
{
    /// <summary>
    ///     The first bytes of a PNG file, which is enough for the tests that do not decode the picture.
    /// </summary>
    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    /// <summary>
    ///     Makes a payload with one marked picture that holds the given marks, and a text that refers to them.
    /// </summary>
    /// <param name="markNumbers">The numbers of the marks of the picture.</param>
    /// <returns>The payload.</returns>
    public static MessagePayload WithMarks(params int[] markNumbers)
    {
        var marks = markNumbers.Select(number => new PayloadMark(
            number,
            MarkKind.Rectangle,
            [new RenderPoint(10, 10), new RenderPoint(40, 30)],
            new RenderBox(10, 10, 30, 20),
            "check this",
            PngBytes,
            new RenderBox(0, 0, 64, 64),
            [],
            sourceElementsAvailable: false,
            sourceElementsUnavailableReason: "svg provides no element positions",
            sourceElementsTruncated: false));
        var image = new PayloadImage(TestPlatformPaths.FullPath("project", "flow.svg"), "file-hash", "pixel-hash", "svg", 200, 100, PngBytes, marks);
        return new MessagePayload(
            string.Join(' ', markNumbers.Select(number => $"[Mark {number}]")),
            markNumbers.Select(number => (PayloadSegment)new PayloadMarkPart(number)),
            [],
            [image],
            FakeMessageBuilder.Now);
    }

    /// <summary>
    ///     Makes a payload of only text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The payload.</returns>
    public static MessagePayload OfText(string text) =>
        new(text, [new PayloadTextPart(text, TextOrigin.Typed)], [], [], FakeMessageBuilder.Now);
}

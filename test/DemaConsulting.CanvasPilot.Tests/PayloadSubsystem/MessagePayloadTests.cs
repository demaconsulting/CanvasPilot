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
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="MessagePayload" /> and the value types it is made of.
/// </summary>
public sealed class MessagePayloadTests
{
    /// <summary>
    ///     The points of a mark that is a rectangle.
    /// </summary>
    private static readonly RenderPoint[] Corners = [new(10, 20), new(60, 50)];

    /// <summary>
    ///     Makes a mark with the given bytes for its area.
    /// </summary>
    /// <param name="crop">The bytes of the picture of the area.</param>
    /// <param name="points">The points of the mark.</param>
    /// <param name="elements">The parts of the source under the mark.</param>
    /// <returns>The mark.</returns>
    private static PayloadMark MakeMark(byte[] crop, IEnumerable<RenderPoint> points, IEnumerable<SourceElementReference>? elements = null)
    {
        return new PayloadMark(
            2,
            MarkKind.Rectangle,
            points,
            new RenderBox(10, 20, 50, 30),
            "note",
            crop,
            new RenderBox(0, 0, 80, 64),
            elements ?? [],
            true,
            null,
            false);
    }

    /// <summary>
    ///     Test that the payload carries the version of the contract.
    /// </summary>
    [Fact]
    public void MessagePayload_New_CarriesContractVersionOne()
    {
        // Arrange / Act: make an empty payload
        var payload = new MessagePayload(string.Empty, [], [], [], DateTimeOffset.UnixEpoch);

        // Assert: the version is the contract version, which is one
        Assert.Equal(1, MessagePayload.ContractVersion);
        Assert.Equal(1, payload.Version);
        Assert.Equal(DateTimeOffset.UnixEpoch, payload.CreatedUtc);
        Assert.Empty(payload.Segments);
        Assert.Empty(payload.Attachments);
        Assert.Empty(payload.Images);
    }

    /// <summary>
    ///     Test that changing the lists and the byte buffers that were given to the payload afterwards changes nothing in
    ///     it, at every level.
    /// </summary>
    [Fact]
    public void MessagePayload_New_CopiesListsAndBytes_SoLaterChangesToTheSourcesChangeNothing()
    {
        // Arrange: lists and buffers that the caller keeps
        var crop = new byte[] { 1, 2, 3 };
        var picture = new byte[] { 9, 8, 7, 6 };
        var points = new List<RenderPoint>(Corners);
        var elements = new List<SourceElementReference> { new("node", "A", "Start", 2, new RenderBox(1, 2, 3, 4), null) };
        var marks = new List<PayloadMark> { MakeMark(crop, points, elements) };
        var image = new PayloadImage(@"C:\project\flow.mmd", "file-hash", "pixel-hash", "mermaid", 200, 100, picture, marks);
        var images = new List<PayloadImage> { image };
        var segments = new List<PayloadSegment> { new PayloadTextPart("Hello ", TextOrigin.Typed), new PayloadMarkPart(2) };
        var attachments = new List<PayloadAttachment> { new(1, AttachmentKind.File, "a.txt", @"C:\x\a.txt", "attachments/a.txt", 3, 1) };
        var payload = new MessagePayload("Hello [Mark 2]", segments, attachments, images, DateTimeOffset.UnixEpoch);

        // Act: the caller changes and clears everything it kept
        crop[0] = 99;
        picture[0] = 99;
        points.Clear();
        elements.Clear();
        marks.Clear();
        images.Clear();
        segments.Clear();
        attachments.Clear();

        // Assert: the payload still holds the original content
        var held = Assert.Single(payload.Images);
        Assert.Equal<byte>([9, 8, 7, 6], held.AnnotatedPng.ToArray());
        var mark = Assert.Single(held.Marks);
        Assert.Equal<byte>([1, 2, 3], mark.CropPng.ToArray());
        Assert.Equal(Corners, mark.Points);
        Assert.Single(mark.SourceElements);
        Assert.Equal(2, payload.Segments.Length);
        Assert.Single(payload.Attachments);
    }

    /// <summary>
    ///     Test that the segments are values: equal parts are equal and the parts keep what they were made with.
    /// </summary>
    [Fact]
    public void PayloadSegment_Parts_AreValuesThatKeepOriginAndReferences()
    {
        // Arrange / Act: make parts
        var typed = new PayloadTextPart("a", TextOrigin.Typed);
        var dictated = new PayloadTextPart("a", TextOrigin.Dictated);

        // Assert: origin and references are part of the value
        Assert.Equal(new PayloadTextPart("a", TextOrigin.Typed), typed);
        Assert.NotEqual(typed, dictated);
        Assert.Equal(new PayloadAttachmentPart(2), new PayloadAttachmentPart(2));
        Assert.NotEqual<PayloadSegment>(new PayloadMarkPart(2), new PayloadAttachmentPart(2));
    }

    /// <summary>
    ///     Test that the parts of a payload refuse content that cannot be a part of one.
    /// </summary>
    [Fact]
    public void MessagePayload_New_InvalidParts_Throw()
    {
        // Arrange: valid parts to build from
        var mark = MakeMark([1], Corners);

        // Act / Assert: each invalid argument is refused
        Assert.Throws<ArgumentNullException>(() => new MessagePayload(null!, [], [], [], DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentNullException>(() => new MessagePayload(string.Empty, null!, [], [], DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentNullException>(() => new MessagePayload(string.Empty, [], null!, [], DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentNullException>(() => new MessagePayload(string.Empty, [], [], null!, DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => new PayloadImage(" ", null, "h", "svg", 10, 10, new byte[] { 1 }, [mark]));
        Assert.Throws<ArgumentException>(() => new PayloadImage("p", null, " ", "svg", 10, 10, new byte[] { 1 }, [mark]));
        Assert.Throws<ArgumentNullException>(() => new PayloadImage("p", null, "h", null!, 10, 10, new byte[] { 1 }, [mark]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadImage("p", null, "h", "svg", 0, 10, new byte[] { 1 }, [mark]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadImage("p", null, "h", "svg", 10, 0, new byte[] { 1 }, [mark]));
        Assert.Throws<ArgumentException>(() => new PayloadImage("p", null, "h", "svg", 10, 10, ReadOnlyMemory<byte>.Empty, [mark]));
        Assert.Throws<ArgumentNullException>(() => new PayloadImage("p", null, "h", "svg", 10, 10, new byte[] { 1 }, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadMark(0, MarkKind.Rectangle, Corners, default, null, new byte[] { 1 }, default, [], true, null, false));
        Assert.Throws<ArgumentException>(() => new PayloadMark(1, MarkKind.Rectangle, [new RenderPoint(1, 1)], default, null, new byte[] { 1 }, default, [], true, null, false));
        Assert.Throws<ArgumentException>(() => new PayloadMark(1, MarkKind.Rectangle, Corners, default, null, ReadOnlyMemory<byte>.Empty, default, [], true, null, false));
        Assert.Throws<ArgumentNullException>(() => new PayloadMark(1, MarkKind.Rectangle, null!, default, null, new byte[] { 1 }, default, [], true, null, false));
        Assert.Throws<ArgumentNullException>(() => new PayloadMark(1, MarkKind.Rectangle, Corners, default, null, new byte[] { 1 }, default, null!, true, null, false));
    }
}

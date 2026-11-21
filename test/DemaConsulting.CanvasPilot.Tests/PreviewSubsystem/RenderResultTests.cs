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

using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the result types of the <see cref="ImageRenderService" /> unit: <see cref="RenderedImage" />,
///     <see cref="RenderDiagnostic" />, <see cref="RenderError" />, and <see cref="RenderResult" />.
/// </summary>
public sealed class RenderResultTests
{
    /// <summary>
    ///     Test that a rendered image keeps what it was created with and defaults to no notes.
    /// </summary>
    [Fact]
    public void RenderedImage_New_ValidArguments_KeepsValues()
    {
        // Arrange / Act: create a 2 x 1 picture without notes
        var image = new RenderedImage("c:\\p\\a.svg", "svg", 2, 1, new byte[8]);

        // Assert: the values are kept and the notes are empty
        Assert.Equal("c:\\p\\a.svg", image.SourcePath);
        Assert.Equal("svg", image.Format);
        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(8, image.RgbaPixels.Length);
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that a rendered image refuses sizes and pixel data that do not fit together.
    /// </summary>
    [Fact]
    public void RenderedImage_New_InvalidArguments_Throw()
    {
        // Act / Assert: blank paths, empty sizes, and pixel data of the wrong length are refused
        Assert.Throws<ArgumentException>(() => new RenderedImage(" ", "svg", 1, 1, new byte[4]));
        Assert.Throws<ArgumentNullException>(() => new RenderedImage("a.svg", null!, 1, 1, new byte[4]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderedImage("a.svg", "svg", 0, 1, new byte[0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderedImage("a.svg", "svg", 1, 0, new byte[0]));
        Assert.Throws<ArgumentException>(() => new RenderedImage("a.svg", "svg", 2, 2, new byte[15]));
    }

    /// <summary>
    ///     Test that the three kinds of result report exactly one outcome each.
    /// </summary>
    [Fact]
    public void RenderResult_Factories_ReportExactlyOneOutcome()
    {
        // Arrange: one result of each kind
        var image = TestImages.Create("a.svg", 1, 1);
        var success = RenderResult.Success(image);
        var failure = RenderResult.Failure(new RenderError("code", "message"));
        var cancelled = RenderResult.Cancelled();

        // Act / Assert: each has its own outcome and nothing else
        Assert.True(success.Succeeded);
        Assert.Same(image, success.Image);
        Assert.Null(success.Error);
        Assert.False(success.IsCancelled);
        Assert.False(failure.Succeeded);
        Assert.Equal("code", failure.Error?.Code);
        Assert.Null(failure.Error?.Hint);
        Assert.False(failure.IsCancelled);
        Assert.False(cancelled.Succeeded);
        Assert.Null(cancelled.Error);
        Assert.True(cancelled.IsCancelled);
        Assert.Throws<ArgumentNullException>(() => RenderResult.Success(null!));
        Assert.Throws<ArgumentNullException>(() => RenderResult.Failure(null!));
    }

    /// <summary>
    ///     Test that a note is displayed as severity, code, and message on one line.
    /// </summary>
    [Fact]
    public void RenderDiagnostic_DisplayText_ShowsSeverityCodeAndMessage()
    {
        // Arrange / Act: a warning note
        var note = new RenderDiagnostic(RenderDiagnosticSeverity.Warning, "image-access-denied", "image was not drawn");

        // Assert: the display text carries all three
        Assert.Equal("Warning: image-access-denied: image was not drawn", note.DisplayText);
    }

    /// <summary>
    ///     Test that the snapshot key combines the path of the file with the fingerprint of the pixels, so it
    ///     changes when either changes.
    /// </summary>
    [Fact]
    public void RenderedImage_SnapshotKey_CombinesPathAndContentHash()
    {
        // Arrange: pictures that differ only in path, only in pixels, and not at all
        var pixels = new byte[] { 1, 2, 3, 4 };
        var original = new RenderedImage("c:\\p\\a.svg", "svg", 1, 1, pixels);
        var same = new RenderedImage("c:\\p\\a.svg", "svg", 1, 1, pixels);
        var otherPath = new RenderedImage("c:\\p\\b.svg", "svg", 1, 1, pixels);
        var otherPixels = new RenderedImage("c:\\p\\a.svg", "svg", 1, 1, new byte[] { 1, 2, 3, 5 });

        // Act / Assert: the key contains path and hash; equal pictures share it and the others differ
        Assert.Contains("c:\\p\\a.svg", original.SnapshotKey, StringComparison.Ordinal);
        Assert.Contains(original.ContentHash, original.SnapshotKey, StringComparison.Ordinal);
        Assert.Equal(original.SnapshotKey, same.SnapshotKey);
        Assert.NotEqual(original.SnapshotKey, otherPath.SnapshotKey);
        Assert.NotEqual(original.SnapshotKey, otherPixels.SnapshotKey);
    }

    /// <summary>
    ///     Test that the pixel fingerprint is a SHA-256 hash in lower case hexadecimal that depends on the pixels and
    ///     on the size, not on the notes or the path.
    /// </summary>
    [Fact]
    public void RenderedImage_ContentHash_DependsOnPixelsAndSizeOnly()
    {
        // Arrange: the same eight bytes as 2 x 1 and as 1 x 2, and the 2 x 1 picture with notes and another path
        var pixels = new byte[8];
        var wide = new RenderedImage("a.svg", "svg", 2, 1, pixels);
        var tall = new RenderedImage("a.svg", "svg", 1, 2, pixels);
        var withNote = new RenderedImage(
            "b.svg",
            "svg",
            2,
            1,
            pixels,
            [new RenderDiagnostic(RenderDiagnosticSeverity.Info, "code", "message")]);

        // Act / Assert: 64 lower case hexadecimal characters; the size matters; notes and path do not
        Assert.Matches("^[0-9a-f]{64}$", wide.ContentHash);
        Assert.NotEqual(wide.ContentHash, tall.ContentHash);
        Assert.Equal(wide.ContentHash, withNote.ContentHash);
    }

    /// <summary>
    ///     Test that a picture without element data has empty lists, and that the lists it was given are copied so
    ///     later changes to the caller's lists do not change the picture.
    /// </summary>
    [Fact]
    public void RenderedImage_New_PositionsAndAnchors_AreCopiedAndDefaultToEmpty()
    {
        // Arrange: a picture without element data and one built from lists that are changed afterwards
        var plain = new RenderedImage("a.mmd", "mermaid", 1, 1, new byte[4]);
        var positions = new List<RenderPosition> { new("node", "A", "Start", 2, null, null, new RenderBox(1, 2, 3, 4), new RenderPoint(2.5, 4)) };
        var anchors = new List<RenderAnchor> { new("top", 10, 20) };
        var image = new RenderedImage("a.mmd", "mermaid", 1, 1, new byte[4], null, "abc", positions, true, anchors);

        // Act: change the lists the picture was built from
        positions.Clear();
        anchors.Clear();

        // Assert: the plain picture has nothing; the other still holds its copies and the facts it was given
        Assert.Empty(plain.Positions);
        Assert.Empty(plain.Anchors);
        Assert.False(plain.PositionsTruncated);
        Assert.Null(plain.SourceFileHash);
        var position = Assert.Single(image.Positions);
        Assert.Equal(("node", "A", "Start", 2), (position.Kind, position.Id, position.Text, position.Line));
        Assert.Equal(new RenderBox(1, 2, 3, 4), position.Box);
        Assert.Equal(new RenderPoint(2.5, 4), position.Pixel);
        Assert.Equal(new RenderAnchor("top", 10, 20), Assert.Single(image.Anchors));
        Assert.True(image.PositionsTruncated);
        Assert.Equal("abc", image.SourceFileHash);
    }
}

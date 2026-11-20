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
}

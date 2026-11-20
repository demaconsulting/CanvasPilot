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

using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the <see cref="ImageRenderService" /> unit. Most tests render real files with the real CanvasNet
///     viewer in temporary folders; the ones that need a failure CanvasNet cannot be made to produce on demand
///     use a substitute drawing function.
/// </summary>
public sealed class ImageRenderServiceTests : IDisposable
{
    /// <summary>
    ///     A small SVG with a half-transparent red rectangle, 40 x 20 pixels.
    /// </summary>
    private const string HalfRedSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'>" +
        "<rect width='40' height='20' fill='#ff0000' fill-opacity='0.5'/></svg>";

    /// <summary>
    ///     Temporary folder that holds the project folder, removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-render-tests-").FullName;

    /// <summary>
    ///     Gets the project folder.
    /// </summary>
    private string Project => Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that rendering a real SVG file returns its size, format, and straight-alpha RGBA pixels, and that
    ///     the result has no error.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_SvgFile_ReturnsPixelsSizeAndFormat()
    {
        // Arrange: a project with a half-transparent red SVG
        var file = Write("half.svg", HalfRedSvg);
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: the picture has the SVG's size, format, and a straight-alpha RGBA first pixel
        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.False(result.IsCancelled);
        var image = result.Image!;
        Assert.Equal(file, image.SourcePath);
        Assert.Equal("svg", image.Format);
        Assert.Equal(40, image.Width);
        Assert.Equal(20, image.Height);
        Assert.Equal(40 * 20 * 4, image.RgbaPixels.Length);
        Assert.Equal([255, 0, 0, 128], image.RgbaPixels.Span[..4].ToArray().Select(b => (int)b));
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that a picture the file embeds from outside the project folder is not drawn and is reported as a
    ///     note, because file access is confined to the project folder, while the render still succeeds.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_EmbeddedPictureOutsideProject_ReportsAccessDeniedNote()
    {
        // Arrange: an SVG that embeds a file that lies beside the project folder
        await File.WriteAllTextAsync(Path.Combine(_root, "outside.png"), "not really a picture", TestContext.Current.CancellationToken);
        var file = Write(
            "linked.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='10' height='10'>" +
            "<image href='../outside.png' width='10' height='10'/></svg>");
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: the picture exists and a warning explains that the embedded file was denied
        Assert.True(result.Succeeded);
        var note = Assert.Single(result.Image!.Diagnostics);
        Assert.Equal(RenderDiagnosticSeverity.Warning, note.Severity);
        Assert.Equal("image-access-denied", note.Code);
        Assert.Contains("outside.png", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a picture the file embeds from a sub-folder of the project is drawn without notes.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_EmbeddedPictureInProjectSubFolder_IsDrawnWithoutNotes()
    {
        // Arrange: an SVG that embeds another SVG from a sub-folder of the project
        Directory.CreateDirectory(Path.Combine(Project, "assets"));
        Write(Path.Combine("assets", "inner.svg"), HalfRedSvg);
        var file = Write(
            "outer.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='40' height='20'>" +
            "<image href='assets/inner.svg' width='40' height='20'/></svg>");
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: the embedded picture was drawn (a red pixel appears) and nothing was reported
        Assert.True(result.Succeeded);
        Assert.Empty(result.Image!.Diagnostics);
        Assert.True(result.Image.RgbaPixels.Span[0] > 200);
    }

    /// <summary>
    ///     Test that a file CanvasNet cannot draw is returned as an error with a message, not thrown.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_BrokenSvg_ReturnsCannotDrawErrorWithoutThrowing()
    {
        // Arrange: an SVG that is not well-formed XML
        var file = Write("broken.svg", "<svg><not closed");
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: an error with the cannot-draw code, a message naming the problem, and a hint
        Assert.False(result.Succeeded);
        Assert.False(result.IsCancelled);
        var error = Assert.IsType<RenderError>(result.Error);
        Assert.Equal(RenderErrorCodes.CannotDraw, error.Code);
        Assert.Contains("well-formed", error.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(error.Hint));
    }

    /// <summary>
    ///     Test that a missing file is an input-not-found error.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_MissingFile_ReturnsInputNotFoundError()
    {
        // Arrange: a path in the project that does not exist
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(Path.Combine(Project, "missing.svg"), Project, TestContext.Current.CancellationToken);

        // Assert: the error names the problem
        Assert.Equal(RenderErrorCodes.InputNotFound, result.Error?.Code);
    }

    /// <summary>
    ///     Test that a file type no image source draws is an unsupported-format error.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_UnsupportedExtension_ReturnsUnsupportedFormatError()
    {
        // Arrange: a text file
        var file = Write("notes.txt", "hello");
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: the error names the problem
        Assert.Equal(RenderErrorCodes.UnsupportedFormat, result.Error?.Code);
    }

    /// <summary>
    ///     Test that the code and hint of a CanvasNet viewer error are passed on unchanged.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_ViewerException_ReturnsItsCodeAndHint()
    {
        // Arrange: a drawing function that fails the way the viewer does for a page that does not exist
        var service = new ImageRenderService((_, _) => throw new ViewerException(ErrorCodes.PageOutOfRange, "The file has no page 2.", "Use page 1."));

        // Act: render any file
        var result = await service.RenderAsync(Path.Combine(Project, "any.svg"), Project, TestContext.Current.CancellationToken);

        // Assert: code, message, and hint are the viewer's
        Assert.Equal(new RenderError("page-out-of-range", "The file has no page 2.", "Use page 1."), result.Error);
    }

    /// <summary>
    ///     Test that every other failure, including one nobody expected, becomes an error result with a sensible code.
    /// </summary>
    [Theory]
    [InlineData(typeof(IOException), RenderErrorCodes.InputUnreadable)]
    [InlineData(typeof(UnauthorizedAccessException), RenderErrorCodes.InputUnreadable)]
    [InlineData(typeof(NotSupportedException), RenderErrorCodes.UnsupportedFormat)]
    [InlineData(typeof(FileNotFoundException), RenderErrorCodes.InputNotFound)]
    [InlineData(typeof(InvalidCastException), RenderErrorCodes.OperationFailed)]
    public async Task ImageRenderService_RenderAsync_OtherFailures_AreReturnedAsErrors(Type exceptionType, string expectedCode)
    {
        // Arrange: a drawing function that throws the exception
        var service = new ImageRenderService((_, _) => throw (Exception)Activator.CreateInstance(exceptionType, "boom")!);

        // Act: render any file
        var result = await service.RenderAsync(Path.Combine(Project, "any.svg"), Project, TestContext.Current.CancellationToken);

        // Assert: no exception reached the caller and the code matches the kind of failure
        Assert.Equal(expectedCode, result.Error?.Code);
        Assert.Equal("boom", result.Error?.Message);
    }

    /// <summary>
    ///     Test that a token that is already cancelled gives a cancelled result without drawing.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_AlreadyCancelledToken_ReturnsCancelledWithoutDrawing()
    {
        // Arrange: a drawing function that records that it ran, and a cancelled token
        var drew = false;
        var service = new ImageRenderService((_, _) =>
        {
            drew = true;
            throw new InvalidOperationException("must not be called");
        });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act: render with the cancelled token
        var result = await service.RenderAsync(Path.Combine(Project, "any.svg"), Project, cancellation.Token);

        // Assert: cancelled, and nothing was drawn
        Assert.True(result.IsCancelled);
        Assert.Null(result.Image);
        Assert.Null(result.Error);
        Assert.False(drew);
    }

    /// <summary>
    ///     Test that canceling while a render runs passes the token to the viewer and gives a cancelled result.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_CancelledWhileRendering_ReturnsCancelled()
    {
        // Arrange: a drawing function that waits for its token, as a long render would observe it
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new ImageRenderService((_, options) =>
        {
            started.SetResult();
            options.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            options.CancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("the token was not cancelled");
        });
        using var cancellation = new CancellationTokenSource();

        // Act: start a render, cancel it once it runs, and wait for the result
        var render = service.RenderAsync(Path.Combine(Project, "any.svg"), Project, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        var result = await render.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert: the result is cancelled, not an error
        Assert.True(result.IsCancelled);
        Assert.Null(result.Error);
    }

    /// <summary>
    ///     Test that failures are written to the log when a logger is supplied.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_Failure_IsLogged()
    {
        // Arrange: a service with a logger and a file that cannot be drawn
        var logger = new RollingFileLogger(Path.Combine(_root, "logs"));
        var service = new ImageRenderService(logger);
        var file = Write("broken.svg", "<svg><not closed");

        // Act: render it
        await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: the log names the file and the code
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("broken.svg", log, StringComparison.Ordinal);
        Assert.Contains(RenderErrorCodes.CannotDraw, log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that encoding a picture produces a valid PNG file whose pixels match the picture, including its
    ///     transparency.
    /// </summary>
    [Fact]
    public void ImageRenderService_EncodePng_Image_ProducesValidPngWithSamePixels()
    {
        // Arrange: a 3 x 2 picture with an opaque and a half-transparent pixel
        var pixels = new byte[3 * 2 * 4];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(index * 10);
        }

        pixels[3] = 255;
        pixels[7] = 128;
        var image = new RenderedImage(Path.Combine(Project, "x.svg"), "svg", 3, 2, pixels);
        var service = new ImageRenderService();

        // Act: encode it and decode the result
        var png = service.EncodePng(image);
        using var stream = new MemoryStream(png);
        using var decoded = PngCodec.Load(stream);

        // Assert: PNG signature, same size, and the same straight-alpha pixels
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.Equal(3, decoded.Width);
        Assert.Equal(2, decoded.Height);
        var decodedPixels = new byte[pixels.Length];
        for (var y = 0; y < decoded.Height; y++)
        {
            decoded.GetRowSpanBytes(y).CopyTo(decodedPixels.AsSpan(y * 12, 12));
        }

        Assert.Equal(pixels, decodedPixels);
    }

    /// <summary>
    ///     Test that the constructor and methods reject unusable arguments.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_InvalidArguments_Throw()
    {
        // Arrange: a service
        var service = new ImageRenderService();

        // Act / Assert: nulls and blanks are refused, whether they are given to the constructor or the methods
        Assert.Throws<ArgumentNullException>(() => new ImageRenderService((Func<string, ViewerRenderOptions, ViewerImage>)null!));
        Assert.Throws<ArgumentNullException>(() => service.EncodePng(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RenderAsync(" ", Project, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RenderAsync("a.svg", "", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Writes a file in the project folder.
    /// </summary>
    /// <param name="relativePath">The path relative to the project folder.</param>
    /// <param name="content">The content.</param>
    /// <returns>The full path.</returns>
    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(Project, relativePath);
        File.WriteAllText(path, content);
        return path;
    }
}

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

using System.Security.Cryptography;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Positions;
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
    ///     Test that a Mermaid diagram reports its nodes and edges with their source lines, text, and boxes in the
    ///     pixels of the picture, and that the request for positions does not add notes.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_MermaidFile_ReportsNodePositionsInPixels()
    {
        // Arrange: a diagram with three nodes and two edges
        var file = Write("flow.mmd", "flowchart LR\n  A[Start] --> B[Middle]\n  B --> C[End]\n");
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: the three nodes carry id, text, source line, and a box that lies inside the picture
        Assert.True(result.Succeeded);
        var image = result.Image!;
        Assert.Equal("mermaid", image.Format);
        Assert.Empty(image.Diagnostics);
        Assert.False(image.PositionsTruncated);
        Assert.Empty(image.Anchors);
        var nodes = image.Positions.Where(position => position.Kind == "node").ToList();
        Assert.Equal(["A", "B", "C"], nodes.Select(node => node.Id));
        Assert.Equal(["Start", "Middle", "End"], nodes.Select(node => node.Text));
        Assert.Equal(2, nodes[0].Line);
        Assert.All(nodes, node =>
        {
            var box = Assert.IsType<RenderBox>(node.Box);
            Assert.True(box.X >= 0 && box.Y >= 0 && box.Width > 0 && box.Height > 0);
            Assert.True(box.X + box.Width <= image.Width && box.Y + box.Height <= image.Height);
            Assert.NotNull(node.Pixel);
        });

        // Assert: the nodes are drawn from left to right and the edges name the nodes they join
        Assert.True(nodes[0].Box!.Value.X < nodes[1].Box!.Value.X);
        Assert.True(nodes[1].Box!.Value.X < nodes[2].Box!.Value.X);
        var edges = image.Positions.Where(position => position.Kind == "edge").ToList();
        Assert.Equal([("A", "B"), ("B", "C")], edges.Select(edge => (edge.From, edge.To)));
    }

    /// <summary>
    ///     Test that a Vega-Lite chart reports its marks and axes.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_VegaLiteChart_ReportsMarkPositions()
    {
        // Arrange: a bar chart with two bars
        var file = Write(
            "bars.vl.json",
            "{\"$schema\":\"https://vega.github.io/schema/vega-lite/v5.json\"," +
            "\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"bar\"," +
            "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}");
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: two bars and the axes are reported, each with a box
        Assert.True(result.Succeeded);
        var image = result.Image!;
        Assert.Equal("vega-lite", image.Format);
        Assert.Equal(2, image.Positions.Count(position => position.Kind == "mark"));
        Assert.Equal(2, image.Positions.Count(position => position.Kind == "axis"));
        Assert.All(image.Positions, position => Assert.NotNull(position.Box));
    }

    /// <summary>
    ///     Test that an OpenSCAD model reports the anchors it declares with their pixel positions.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_ScadWithAnchor_ReportsAnchor()
    {
        // Arrange: a model that declares one anchor
        var file = Write("part.scad", "cube([10, 10, 10]);\necho(\"anchor\", \"top\", [5, 5, 10]);\n");
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: one anchor with a pixel inside the picture, and no element positions
        Assert.True(result.Succeeded);
        var image = result.Image!;
        Assert.Equal("scad", image.Format);
        var anchor = Assert.Single(image.Anchors);
        Assert.Equal("top", anchor.Name);
        Assert.InRange(anchor.PixelX!.Value, 0, image.Width);
        Assert.InRange(anchor.PixelY!.Value, 0, image.Height);
        Assert.Empty(image.Positions);
    }

    /// <summary>
    ///     Test that a kind of file without element data reports no positions and no anchors, and no notes about it.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_SvgFile_ReportsNoPositions()
    {
        // Arrange: an SVG
        var file = Write("half.svg", HalfRedSvg);
        var service = new ImageRenderService();

        // Act: render it
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: no positions, no anchors, no truncation, and no notes
        var image = result.Image!;
        Assert.Empty(image.Positions);
        Assert.Empty(image.Anchors);
        Assert.False(image.PositionsTruncated);
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that rendering the same unchanged file twice gives the same fingerprints, and that the file
    ///     fingerprint is the SHA-256 hash of the file's bytes.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_SameFileTwice_SameContentHash()
    {
        // Arrange: a file and a service
        var file = Write("half.svg", HalfRedSvg);
        var service = new ImageRenderService();

        // Act: render it twice
        var first = (await service.RenderAsync(file, Project, TestContext.Current.CancellationToken)).Image!;
        var second = (await service.RenderAsync(file, Project, TestContext.Current.CancellationToken)).Image!;

        // Assert: equal fingerprints, equal keys, and the file hash is the hash of the file's bytes
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(first.SourceFileHash, second.SourceFileHash);
        Assert.Equal(first.SnapshotKey, second.SnapshotKey);
        var expected = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken)));
        Assert.Equal(expected, first.SourceFileHash);
    }

    /// <summary>
    ///     Test that changing the file changes both fingerprints and the snapshot key.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_ChangedFile_DifferentSourceAndContentHash()
    {
        // Arrange: a file rendered once, then changed to another color
        var file = Write("half.svg", HalfRedSvg);
        var service = new ImageRenderService();
        var before = (await service.RenderAsync(file, Project, TestContext.Current.CancellationToken)).Image!;
        Write("half.svg", HalfRedSvg.Replace("#ff0000", "#00ff00", StringComparison.Ordinal));

        // Act: render it again
        var after = (await service.RenderAsync(file, Project, TestContext.Current.CancellationToken)).Image!;

        // Assert: the file, the pixels, and so the key differ
        Assert.NotEqual(before.SourceFileHash, after.SourceFileHash);
        Assert.NotEqual(before.ContentHash, after.ContentHash);
        Assert.NotEqual(before.SnapshotKey, after.SnapshotKey);
    }

    /// <summary>
    ///     Test that a file that disappears after it was drawn still gives a picture, without a file fingerprint.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_FileGoneAfterDrawing_HasNoSourceFileHash()
    {
        // Arrange: a drawing function that draws the real file and then deletes it
        var file = Write("half.svg", HalfRedSvg);
        var service = new ImageRenderService((path, options) =>
        {
            var image = CanvasNetViewer.Render(path, options);
            File.Delete(path);
            return image;
        });

        // Act: render the file
        var result = await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);

        // Assert: the picture exists, has pixel fingerprint, and has no file fingerprint
        Assert.True(result.Succeeded);
        Assert.Null(result.Image!.SourceFileHash);
        Assert.Equal(64, result.Image.ContentHash.Length);
    }

    /// <summary>
    ///     Test that the request to the viewer asks for the position of every element.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_Request_AsksForAllPositions()
    {
        // Arrange: a drawing function that records the options it receives
        ViewerRenderOptions? received = null;
        var service = new ImageRenderService((_, options) =>
        {
            received = options;
            throw new InvalidOperationException("stop");
        });

        // Act: render any file
        await service.RenderAsync(Path.Combine(Project, "any.mmd"), Project, TestContext.Current.CancellationToken);

        // Assert: every position was requested
        Assert.NotNull(received);
        Assert.NotNull(received.Positions);
        Assert.Same(PositionSelection.All, received.Positions);
    }

    /// <summary>
    ///     Test that a vector file drawn with a width and a height is exactly that size, fitted with its ratio kept and
    ///     not stretched, with a transparent margin when the settings ask for transparency and with the color of the
    ///     background when they give one.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_SvgIntoBox_IsExactlyThatSizeAndLetterboxed()
    {
        // Arrange: a 1600 x 640 SVG that is one opaque violet rectangle
        var file = Write(
            "banner.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='1600' height='640'><rect width='1600' height='640' fill='#7C3AED'/></svg>");
        var service = new ImageRenderService();

        // Act: draw it into a 1920 x 1080 box over transparency, over a color, and with only a width
        var transparent = await service.RenderAsync(file, Project, new RenderSettings(1920, 1080, Transparent: true), TestContext.Current.CancellationToken);
        var colored = await service.RenderAsync(file, Project, new RenderSettings(1920, 1080, BackgroundHex: "#0A1022"), TestContext.Current.CancellationToken);
        var widthOnly = await service.RenderAsync(file, Project, new RenderSettings(Width: 800), TestContext.Current.CancellationToken);
        var natural = await service.RenderAsync(file, Project, RenderSettings.AsRendered, TestContext.Current.CancellationToken);

        // Assert: exact sizes, the margin is transparent or the color, the picture is in the middle, and the width scales
        Assert.Equal((1600, 640), (natural.Image!.Width, natural.Image.Height));
        var box = transparent.Image!;
        Assert.Equal((1920, 1080), (box.Width, box.Height));
        Assert.Equal([0, 0, 0, 0], Pixel(box, 0, 0));
        Assert.Equal([124, 58, 237, 255], Pixel(box, 960, 540));
        var flattened = colored.Image!;
        Assert.Equal((1920, 1080), (flattened.Width, flattened.Height));
        Assert.Equal([10, 16, 34, 255], Pixel(flattened, 0, 0));
        Assert.Equal([124, 58, 237, 255], Pixel(flattened, 960, 540));
        Assert.Equal((800, 320), (widthOnly.Image!.Width, widthOnly.Image.Height));
    }

    /// <summary>
    ///     Test that a Mermaid diagram, a Vega-Lite chart, and a raster picture are each drawn at a box size exactly and
    ///     with a transparent margin, so that every family of format can be exported at a size.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_MermaidVegaLiteAndRasterIntoBox_AreExactlyThatSize()
    {
        // Arrange: a diagram, a chart, and a 10 x 10 red PNG
        var diagram = Write("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var chart = Write(
            "bars.vl.json",
            "{\"$schema\":\"https://vega.github.io/schema/vega-lite/v5.json\"," +
            "\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"bar\"," +
            "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}");
        var raster = Path.Combine(Project, "red.png");
        using (var surface = new DemaConsulting.CanvasNet.Canvas.Surface(10, 10))
        {
            for (var y = 0; y < 10; y++)
            {
                for (var x = 0; x < 10; x++)
                {
                    surface[x, y] = new DemaConsulting.CanvasNet.Canvas.Rgba32(255, 0, 0, 255);
                }
            }

            PngCodec.Save(surface, raster);
        }

        var service = new ImageRenderService();
        var settings = new RenderSettings(800, 400, Transparent: true);

        // Act
        var diagramResult = await service.RenderAsync(diagram, Project, settings, TestContext.Current.CancellationToken);
        var chartResult = await service.RenderAsync(chart, Project, settings, TestContext.Current.CancellationToken);
        var rasterResult = await service.RenderAsync(raster, Project, settings, TestContext.Current.CancellationToken);

        // Assert: each is exactly 800 x 400, the margin of each is transparent, and the raster is fitted in the middle
        foreach (var result in new[] { diagramResult, chartResult, rasterResult })
        {
            Assert.True(result.Succeeded, result.Error?.Message);
            Assert.Equal((800, 400), (result.Image!.Width, result.Image.Height));
        }

        Assert.Equal(0, Pixel(diagramResult.Image!, 0, 0)[3]);
        Assert.Equal(0, Pixel(rasterResult.Image!, 0, 0)[3]);
        Assert.Equal([255, 0, 0, 255], Pixel(rasterResult.Image!, 400, 200));
    }

    /// <summary>
    ///     Test that settings that CanvasNet does not draw are a failed result with the code of invalid settings and a
    ///     message that says what is allowed, and not an exception.
    /// </summary>
    /// <param name="width">The width of the settings.</param>
    /// <param name="height">The height of the settings.</param>
    /// <param name="background">The background of the settings.</param>
    /// <param name="dpi">The resolution of the settings.</param>
    /// <param name="expected">A part of the expected message.</param>
    [Theory]
    [InlineData(0, null, null, null, "width must be from 1 to 8192")]
    [InlineData(8193, null, null, null, "width must be from 1 to 8192")]
    [InlineData(null, -5, null, null, "height must be from 1 to 8192")]
    [InlineData(100, 9000, null, null, "height must be from 1 to 8192")]
    [InlineData(null, null, "red", null, "background must be a color of the form #RRGGBB")]
    [InlineData(null, null, null, 0.0, "resolution must be greater than 0")]
    [InlineData(null, null, null, 2400.5, "resolution must be greater than 0")]
    public async Task ImageRenderService_RenderAsync_InvalidSettings_ReturnsInvalidSettingsError(int? width, int? height, string? background, double? dpi, string expected)
    {
        // Arrange: a good file and settings that are not drawn
        var file = Write("half.svg", HalfRedSvg);
        var service = new ImageRenderService();

        // Act
        var result = await service.RenderAsync(file, Project, new RenderSettings(width, height, BackgroundHex: background, Dpi: dpi), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(RenderErrorCodes.InvalidSettings, result.Error!.Code);
        Assert.Contains(expected, result.Error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Error.Hint);
    }

    /// <summary>
    ///     Test that a render without settings, with settings that change nothing, and by the older overload is the
    ///     same render, and that each setting reaches the viewer as the option of that name.
    /// </summary>
    [Fact]
    public async Task ImageRenderService_RenderAsync_Settings_ReachTheViewerAsOptions()
    {
        // Arrange: a drawing function that records the options it receives and then stops
        var received = new List<ViewerRenderOptions>();
        var service = new ImageRenderService((_, options) =>
        {
            received.Add(options);
            throw new InvalidOperationException("stop");
        });
        var file = Path.Combine(Project, "any.svg");

        // Act: render with no settings, with the older overload, as rendered, transparent, with a color, and with both
        await service.RenderAsync(file, Project, null, TestContext.Current.CancellationToken);
        await service.RenderAsync(file, Project, TestContext.Current.CancellationToken);
        await service.RenderAsync(file, Project, RenderSettings.AsRendered, TestContext.Current.CancellationToken);
        await service.RenderAsync(file, Project, new RenderSettings(100, 50, Transparent: true, Dpi: 150), TestContext.Current.CancellationToken);
        await service.RenderAsync(file, Project, new RenderSettings(BackgroundHex: "#0A1022"), TestContext.Current.CancellationToken);
        await service.RenderAsync(file, Project, new RenderSettings(Transparent: true, BackgroundHex: "#0A1022"), TestContext.Current.CancellationToken);

        // Assert: the first three change nothing, and the others set width, height, background, and resolution
        Assert.Equal(6, received.Count);
        Assert.All(received.Take(3), o =>
        {
            Assert.Null(o.Width);
            Assert.Null(o.Height);
            Assert.Null(o.Background);
            Assert.Null(o.Dpi);
        });
        Assert.Equal((100, 50, 150.0), (received[3].Width, received[3].Height, received[3].Dpi));
        Assert.Equal(0, received[3].Background!.Value.A);
        Assert.Equal((10, 16, 34, 255), ((int)received[4].Background!.Value.R, (int)received[4].Background!.Value.G, (int)received[4].Background!.Value.B, (int)received[4].Background!.Value.A));
        Assert.Equal(0, received[5].Background!.Value.A);
    }

    /// <summary>
    ///     Test that encoding with a resolution writes the physical size chunk into the file, that the file still decodes
    ///     to the same pixels, and that encoding without a resolution writes no such chunk.
    /// </summary>
    [Fact]
    public void ImageRenderService_EncodePng_WithDpi_WritesPhysicalSizeAndKeepsPixels()
    {
        // Arrange: a 3 x 2 picture
        var pixels = new byte[3 * 2 * 4];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(index * 10);
        }

        var image = new RenderedImage(Path.Combine(Project, "x.svg"), "svg", 3, 2, pixels);
        var service = new ImageRenderService();

        // Act
        var plain = service.EncodePng(image);
        var withNull = service.EncodePng(image, null);
        var withDpi = service.EncodePng(image, 150);
        using var stream = new MemoryStream(withDpi);
        using var decoded = PngCodec.Load(stream);

        // Assert: no chunk without a resolution, 150 dots per inch are 5906 pixels per meter, and the pixels are the same
        Assert.Equal(plain, withNull);
        Assert.DoesNotContain("pHYs", System.Text.Encoding.ASCII.GetString(plain), StringComparison.Ordinal);
        Assert.Contains("pHYs", System.Text.Encoding.ASCII.GetString(withDpi), StringComparison.Ordinal);
        Assert.Equal(plain.Length + 21, withDpi.Length);
        Assert.Equal((3, 2), (decoded.Width, decoded.Height));
        var decodedPixels = new byte[pixels.Length];
        for (var y = 0; y < decoded.Height; y++)
        {
            decoded.GetRowSpanBytes(y).CopyTo(decodedPixels.AsSpan(y * 12, 12));
        }

        Assert.Equal(pixels, decodedPixels);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.EncodePng(image, 0));
    }

    /// <summary>
    ///     Reads one pixel of a picture.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The red, green, blue, and alpha bytes.</returns>
    private static int[] Pixel(RenderedImage image, int x, int y)
    {
        var offset = ((y * image.Width) + x) * 4;
        return image.RgbaPixels.Span.Slice(offset, 4).ToArray().Select(b => (int)b).ToArray();
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

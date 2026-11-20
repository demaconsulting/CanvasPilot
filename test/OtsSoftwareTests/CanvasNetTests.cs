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

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the DemaConsulting.CanvasNet off-the-shelf software item: the viewer library that
///     draws image source files, and the core library's PNG codec. They exercise the library directly on temporary
///     folders, with the same calls and options the application uses.
/// </summary>
public sealed class CanvasNetTests : IDisposable
{
    /// <summary>
    ///     An SVG that is 40 x 20 pixels and holds a half-transparent red rectangle.
    /// </summary>
    private const string HalfRedSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'>" +
        "<rect width='40' height='20' fill='#ff0000' fill-opacity='0.5'/></svg>";

    /// <summary>
    ///     Temporary folder removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ots-canvasnet-").FullName;

    /// <summary>
    ///     Gets the project folder inside the temporary root.
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
    ///     Test that CanvasNet renders an SVG file to a surface of the file's size whose pixels are red, green, blue,
    ///     alpha bytes with straight alpha, and reports the format that was drawn.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_SvgFile_ProducesStraightAlphaPixels()
    {
        // Arrange: an SVG with a half-transparent red rectangle
        var file = Write("half.svg", HalfRedSvg);

        // Act: render it as the application does
        using var image = CanvasNetViewer.Render(file, new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(Project) });

        // Assert: size, format, tightly packed rows, and straight-alpha RGBA in the first pixel
        Assert.Equal(40, image.Surface.Width);
        Assert.Equal(20, image.Surface.Height);
        Assert.Equal("svg", image.Format);
        var row = image.Surface.GetRowSpanBytes(0);
        Assert.Equal(40 * 4, row.Length);
        Assert.Equal([255, 0, 0, 128], row[..4].ToArray().Select(b => (int)b));
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that CanvasNet lists the file name extensions it can draw, lower case with the dot and including
    ///     compound extensions, so the application can offer exactly those files.
    /// </summary>
    [Fact]
    public void CanvasNet_Extensions_ListsSupportedImageSourceTypes()
    {
        // Act: read the list of extensions
        var extensions = CanvasNetViewer.Extensions;

        // Assert: the common image source types are present, including the compound Vega-Lite extension
        foreach (var expected in new[] { ".svg", ".png", ".jpg", ".pdf", ".mmd", ".scad", ".puml", ".vl.json" })
        {
            Assert.Contains(expected, extensions);
        }

        Assert.All(extensions, extension => Assert.Equal(extension.ToLowerInvariant(), extension));
        Assert.All(extensions, extension => Assert.StartsWith(".", extension, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a file confined to the project folder cannot embed a file outside it: the file is not drawn
    ///     and CanvasNet reports a warning note instead.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_UnderRootsPolicy_DeniesEmbeddedFileOutsideRoots()
    {
        // Arrange: an SVG in the project that embeds a file beside the project folder
        File.WriteAllText(Path.Combine(_root, "outside.png"), "not really a picture");
        var file = Write("outside.svg", EmbeddingSvg("../outside.png"));

        // Act: render with file access confined to the project folder
        using var image = CanvasNetViewer.Render(file, new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(Project) });

        // Assert: the render succeeded and one warning says that access was denied
        var note = Assert.Single(image.Diagnostics);
        Assert.Equal(ImageDiagnosticSeverity.Warning, note.Severity);
        Assert.Equal("image-access-denied", note.Code);
    }

    /// <summary>
    ///     Test that a file inside the project folder can be embedded under the same confinement.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_UnderRootsPolicy_AllowsEmbeddedFileInsideRoots()
    {
        // Arrange: an SVG that embeds another SVG from a sub-folder of the project
        Directory.CreateDirectory(Path.Combine(Project, "assets"));
        Write(Path.Combine("assets", "inner.svg"), HalfRedSvg);
        var file = Write("outer.svg", EmbeddingSvg("assets/inner.svg"));

        // Act: render with file access confined to the project folder
        using var image = CanvasNetViewer.Render(file, new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(Project) });

        // Assert: nothing was denied and the embedded picture was drawn
        Assert.Empty(image.Diagnostics);
        Assert.True(image.Surface.GetRowSpanBytes(0)[0] > 200);
    }

    /// <summary>
    ///     Test that CanvasNet reports a mistake of the caller as a viewer error that carries a stable code and a
    ///     hint, here a page that the file does not have.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_PageBeyondLastPage_ThrowsViewerExceptionWithCodeAndHint()
    {
        // Arrange: an SVG, which has only page 1
        var file = Write("one.svg", HalfRedSvg);

        // Act: ask for page 2
        var exception = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(file, new ViewerRenderOptions { Page = 2 }));

        // Assert: the stable code and a hint are available to show the user
        Assert.Equal(ErrorCodes.PageOutOfRange, exception.Code);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.False(string.IsNullOrWhiteSpace(exception.Hint));
    }

    /// <summary>
    ///     Test that CanvasNet reports files it cannot draw as exceptions that describe the problem: a malformed file,
    ///     a missing file, and a type nobody draws.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_UnusableFiles_ThrowDescriptiveExceptions()
    {
        // Arrange: a malformed SVG and a text file
        var malformed = Write("broken.svg", "<svg><not closed");
        var text = Write("notes.txt", "hello");

        // Act / Assert: each failure has its own exception type and a message about the problem
        var cannotDraw = Assert.Throws<ImageSourceException>(() => CanvasNetViewer.Render(malformed));
        Assert.Contains("well-formed", cannotDraw.Message, StringComparison.Ordinal);
        Assert.Throws<FileNotFoundException>(() => CanvasNetViewer.Render(Path.Combine(Project, "missing.svg")));
        var unsupported = Assert.Throws<NotSupportedException>(() => CanvasNetViewer.Render(text));
        Assert.Contains(".svg", unsupported.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that CanvasNet stops a render whose cancellation token is cancelled.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange: a valid SVG and a token that is already cancelled
        var file = Write("half.svg", HalfRedSvg);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert: the render is cancelled instead of completing
        Assert.Throws<OperationCanceledException>(() => CanvasNetViewer.Render(file, new ViewerRenderOptions { CancellationToken = cancellation.Token }));
    }

    /// <summary>
    ///     Test that CanvasNet's PNG codec encodes a surface to a PNG stream that decodes back to the same pixels,
    ///     transparency included.
    /// </summary>
    [Fact]
    public void CanvasNet_PngCodec_SaveSurface_WritesValidPngThatRoundTrips()
    {
        // Arrange: a 3 x 2 surface with a half-transparent color
        using var surface = new Surface(3, 2);
        surface.Clear(new Rgba32(10, 20, 30, 128));

        // Act: encode to memory and decode again
        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream);
        var bytes = stream.ToArray();
        stream.Position = 0;
        using var decoded = PngCodec.Load(stream);

        // Assert: PNG signature, same size, and the same straight-alpha pixel
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], bytes[..8]);
        Assert.Equal(3, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal(new Rgba32(10, 20, 30, 128), decoded[2, 1]);
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

    /// <summary>
    ///     Builds an SVG that embeds one picture by link.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <returns>The SVG text.</returns>
    private static string EmbeddingSvg(string href)
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='40' height='20'>" +
               $"<image href='{href}' width='40' height='20'/></svg>";
    }
}

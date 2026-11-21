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

using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Help;

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
        foreach (var expected in new[] { ".svg", ".png", ".jpg", ".pdf", ".mmd", ".scad", ".puml", ".plantuml", ".pu", ".vl.json" })
        {
            Assert.Contains(expected, extensions);
        }

        Assert.All(extensions, extension => Assert.Equal(extension.ToLowerInvariant(), extension));
        Assert.All(extensions, extension => Assert.StartsWith(".", extension, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that CanvasNet renders a PlantUML file to a picture, reporting the format it drew, so PlantUML files
    ///     in a project can be shown like the other image source types.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_PlantUmlFile_ProducesPicture()
    {
        // Arrange: a small sequence diagram
        var file = Write("hello.puml", "@startuml\nAlice -> Bob: Hello\nBob --> Alice: Hi\n@enduml\n");

        // Act: render it as the application does
        using var image = CanvasNetViewer.Render(file, new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(Project) });

        // Assert: a non-empty picture, tightly packed rows, drawn as PlantUML, and something other than a blank page
        Assert.Equal("plantuml", image.Format);
        Assert.True(image.Surface.Width > 0 && image.Surface.Height > 0);
        Assert.Equal(image.Surface.Width * 4, image.Surface.GetRowSpanBytes(0).Length);
        Assert.True(
            HasPixelOtherThan(image.Surface, image.Surface[0, 0], 0, 0, image.Surface.Width, image.Surface.Height),
            "The diagram was not drawn.");
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
    ///     Test that a Mermaid diagram rendered with every position requested reports its nodes and edges with their
    ///     identifiers, text, source lines, and boxes in the pixels of the picture, and that it reports none when no
    ///     positions were requested.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_MermaidWithPositionsSelection_ReportsNodesWithBoxes()
    {
        // Arrange: a diagram with three nodes and two edges
        var file = Write("flow.mmd", "flowchart LR\n  A[Start] --> B[Middle]\n  B --> C[End]\n");

        // Act: render it once asking for every position and once asking for none
        using var image = CanvasNetViewer.Render(file, new ViewerRenderOptions { Positions = PositionSelection.All });
        using var plain = CanvasNetViewer.Render(file);

        // Assert: five positions, the nodes with text, line, and a box inside the picture; none when not asked for
        Assert.Equal("mermaid", image.Format);
        Assert.False(image.PositionsTruncated);
        Assert.Equal(5, image.Positions.Count);
        var nodes = image.Positions.Where(position => position.Kind == "node").ToList();
        Assert.Equal(["A", "B", "C"], nodes.Select(node => node.Id));
        Assert.Equal(["Start", "Middle", "End"], nodes.Select(node => node.Text));
        Assert.Equal(2, nodes[0].Line);
        Assert.All(nodes, node =>
        {
            var box = Assert.IsType<PositionBox>(node.Box);
            Assert.True(box.Width > 0 && box.Height > 0);
            Assert.True(box.X >= 0 && box.Y >= 0);
            Assert.True(box.X + box.Width <= image.Surface.Width && box.Y + box.Height <= image.Surface.Height);
            Assert.NotNull(node.Pixel);
        });
        var edges = image.Positions.Where(position => position.Kind == "edge").ToList();
        Assert.Equal([("A", "B"), ("B", "C")], edges.Select(edge => (edge.From, edge.To)));
        Assert.Empty(plain.Positions);
    }

    /// <summary>
    ///     Test that a Vega-Lite chart rendered with every position requested reports its marks and axes with boxes.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_VegaLiteWithPositionsSelection_ReportsMarksAndAxes()
    {
        // Arrange: a bar chart with two bars
        var file = Write(
            "bars.vl.json",
            "{\"$schema\":\"https://vega.github.io/schema/vega-lite/v5.json\"," +
            "\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"bar\"," +
            "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}");

        // Act: render it asking for every position
        using var image = CanvasNetViewer.Render(file, new ViewerRenderOptions { Positions = PositionSelection.All });

        // Assert: two bar marks and two axes, each with a box
        Assert.Equal("vega-lite", image.Format);
        Assert.Equal(2, image.Positions.Count(position => position.Kind == "mark"));
        Assert.Equal(2, image.Positions.Count(position => position.Kind == "axis"));
        Assert.All(image.Positions, position => Assert.NotNull(position.Box));
    }

    /// <summary>
    ///     Test that an OpenSCAD model reports the anchors it declares with <c>echo("anchor", ...)</c> with their pixel
    ///     positions in the picture, and that the request for positions does not change that.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_ScadAnchors_ReportsPixelPositions()
    {
        // Arrange: a model that declares one anchor on the top of a cube
        var file = Write("part.scad", "cube([10, 10, 10]);\necho(\"anchor\", \"top\", [5, 5, 10]);\n");

        // Act: render it asking for every position, as the application does
        using var image = CanvasNetViewer.Render(file, new ViewerRenderOptions { Positions = PositionSelection.All });

        // Assert: one anchor with its world position and a pixel inside the picture; no element positions
        Assert.Equal("scad", image.Format);
        var anchor = Assert.Single(image.Anchors);
        Assert.Equal("top", anchor.Name);
        Assert.Equal((5, 5, 10), (anchor.X, anchor.Y, anchor.Z));
        Assert.InRange(anchor.PixelX!.Value, 0, image.Surface.Width);
        Assert.InRange(anchor.PixelY!.Value, 0, image.Surface.Height);
        Assert.Empty(image.Positions);
    }

    /// <summary>
    ///     Test that an SVG document given as a stream, that embeds a PNG picture as a data link and draws shapes
    ///     and text over it, is drawn with the picture and with all of the shapes. This is how the application draws
    ///     the marks over a picture it has already shown.
    /// </summary>
    [Fact]
    public void CanvasNet_Render_SvgStream_WithDataUriImageAndShapes_DrawsAll()
    {
        // Arrange: a 100 x 50 solid red PNG, embedded as a data link in an SVG that draws a blue rectangle outline,
        // a green ellipse outline, a yellow polyline, and white text over it
        using var picture = new Surface(100, 50);
        picture.Clear(new Rgba32(255, 0, 0, 255));
        using var png = new MemoryStream();
        PngCodec.Save(picture, png);
        var svg =
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='100' height='50'>" +
            $"<image href='data:image/png;base64,{Convert.ToBase64String(png.ToArray())}' x='0' y='0' width='100' height='50'/>" +
            "<rect x='10' y='10' width='40' height='30' fill='none' stroke='#0000ff' stroke-width='4'/>" +
            "<ellipse cx='75' cy='25' rx='15' ry='10' fill='none' stroke='#00ff00' stroke-width='4'/>" +
            "<circle cx='30' cy='25' r='6' fill='#ff00ff'/>" +
            "<polyline points='55,45 95,45' fill='none' stroke='#ffff00' stroke-width='4'/>" +
            "<text x='12' y='9' font-size='8' fill='#ffffff'>1</text></svg>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));

        // Act: render the stream
        using var image = CanvasNetViewer.Render(stream, "marks.svg", new ViewerRenderOptions());

        // Assert: the size of the SVG, the embedded picture, and each stroke at a pixel on its line
        Assert.Equal("svg", image.Format);
        Assert.Equal(100, image.Surface.Width);
        Assert.Equal(50, image.Surface.Height);
        Assert.Equal(new Rgba32(255, 0, 0, 255), image.Surface[2, 2]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), image.Surface[10, 25]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), image.Surface[90, 25]);
        Assert.Equal(new Rgba32(255, 255, 0, 255), image.Surface[75, 45]);
        Assert.Equal(new Rgba32(255, 0, 255, 255), image.Surface[30, 25]);
        Assert.True(HasPixelOtherThan(image.Surface, new Rgba32(255, 0, 0, 255), 11, 1, 20, 9), "The text was not drawn.");
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that CanvasNet copies a rectangular region of a surface into a new, independent surface, which is how
    ///     the application cuts the area around a mark from the picture with the marks on it.
    /// </summary>
    [Fact]
    public void CanvasNet_Surface_Crop_CopiesTheRegionIntoAnIndependentSurface()
    {
        // Arrange: a 20 x 10 surface with one distinct pixel at (7, 4)
        using var surface = new Surface(20, 10);
        surface.Clear(new Rgba32(10, 20, 30, 255));
        surface[7, 4] = new Rgba32(200, 100, 50, 255);

        // Act: cut a 6 x 5 region that starts at (5, 2), then change the original
        using var region = surface.Crop(5, 2, 6, 5);
        surface[7, 4] = new Rgba32(0, 0, 0, 255);

        // Assert: the size of the region, the distinct pixel at its place, the other pixels, and independence from the
        // original
        Assert.Equal(6, region.Width);
        Assert.Equal(5, region.Height);
        Assert.Equal(new Rgba32(200, 100, 50, 255), region[2, 2]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), region[0, 0]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), region[5, 4]);
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
    }

    /// <summary>
    ///     Test that CanvasNet provides its help cards: a tree of cards that are found by path and alias, each with a
    ///     title, summary, tags, a body, related cards, and examples with their content.
    /// </summary>
    [Fact]
    public void CanvasNet_HelpCatalog_Default_ProvidesCardsWithBodiesRelatedCardsAndExamples()
    {
        // Arrange: the help catalog of the library
        var catalog = HelpCatalog.Default;

        // Act: find a card by path and by alias, and read its parts
        var card = catalog.Find("svg/framed-figure");
        var byAlias = catalog.Find("SVG framed");

        // Assert: the card is complete, and its parents, children, related cards, and examples are reachable
        Assert.NotNull(card);
        Assert.Same(card, byAlias);
        Assert.False(string.IsNullOrWhiteSpace(card.Title));
        Assert.False(string.IsNullOrWhiteSpace(card.Summary));
        Assert.Contains("svg", card.Tags);
        Assert.False(string.IsNullOrWhiteSpace(card.Markdown));
        Assert.NotEmpty(catalog.GetSeeAlso(card));
        Assert.Contains(card.Examples, e => e.Name == "framed" && e.Content.Contains("<svg", StringComparison.Ordinal));
        var svg = catalog.Find("svg");
        Assert.NotNull(svg);
        Assert.Contains(svg, catalog.Roots);
        Assert.Contains(card, catalog.GetChildren(svg));
        Assert.Equal("svg", card.ParentId);
        Assert.Contains(catalog.Cards, c => c.Id == "mermaid/flowchart");
    }

    /// <summary>
    ///     Test that CanvasNet extends its help cards with the cards of another assembly: the card is a new root after the
    ///     library roots, its links resolve, and the library catalog is unchanged.
    /// </summary>
    [Fact]
    public void CanvasNet_HelpCatalog_Extend_EmbeddedCard_AddsANewRootAndLeavesTheLibraryUnchanged()
    {
        // Arrange: the help catalog of the library
        var library = HelpCatalog.Default;

        // Act: add the card that is embedded in the tests, and write its body with the links as card names
        var extended = library.Extend(typeof(CanvasNetTests).Assembly, "OtsSoftwareTests.HelpCards");
        var card = extended.Find("sample");
        var body = card is null ? string.Empty : extended.FormatMarkdown(card, l => l.ExampleName is null ? $"<{l.CardId}>" : $"<{l.ExampleName}@{l.ExampleCardId}>");

        // Assert: the card is the last root, found by its alias, with its related card and links resolved, and the
        // library catalog does not hold it
        Assert.NotNull(card);
        Assert.Equal("ots-sample", card.Id);
        Assert.Equal("ots-sample", extended.Roots[^1].Id);
        Assert.Equal(library.Roots.Count + 1, extended.Roots.Count);
        Assert.Equal("svg", Assert.Single(extended.GetSeeAlso(card)).Id);
        Assert.Contains("<svg>", body, StringComparison.Ordinal);
        Assert.Contains("<framed@svg/framed-figure>", body, StringComparison.Ordinal);
        Assert.Null(library.Find("ots-sample"));
        Assert.DoesNotContain(library.Cards, c => c.Id == "ots-sample");
    }

    /// <summary>
    ///     Test that CanvasNet classifies the generic CSS font families, the common aliases of them, and the bundled
    ///     Liberation families, and does not recognize an arbitrary family, and splits a font-family list, so the
    ///     application can tell which fonts of a style are drawn the same on every machine.
    /// </summary>
    [Fact]
    public void CanvasNet_GenericFontFamilies_ClassifiesBundledAliasesAndUnknownFamilies()
    {
        // Act: classify each family the way the application does, with the common aliases included
        bool Classify(string name, out bool serif, out bool fixedPitch) =>
            GenericFontFamilies.TryClassify(name, includeCommonAliases: true, out serif, out fixedPitch);
        var sans = Classify("sans-serif", out var sansSerif, out var sansFixed);
        var segoe = Classify("Segoe UI", out var segoeSerif, out var segoeFixed);
        var serifKeyword = Classify("serif", out var serifSerif, out var serifFixed);
        var mono = Classify("monospace", out var monoSerif, out var monoFixed);
        var consolas = Classify("Consolas", out _, out var consolasFixed);
        var liberationMono = Classify("liberation mono", out _, out var liberationFixed);
        var inter = Classify("Inter", out _, out _);
        var comic = Classify("Comic Sans MS", out _, out _);
        var withoutAliases = GenericFontFamilies.TryClassify("Arial", includeCommonAliases: false, out _, out _);
        var families = GenericFontFamilies.SplitFamilyList("\"Segoe UI\", Arial, sans-serif");

        // Assert: the keywords and the aliases are recognized with their design, other families are not, and an alias is
        // recognized only when the aliases are asked for
        Assert.True(sans);
        Assert.False(sansSerif || sansFixed);
        Assert.True(segoe);
        Assert.False(segoeSerif || segoeFixed);
        Assert.True(serifKeyword);
        Assert.True(serifSerif);
        Assert.False(serifFixed);
        Assert.True(mono);
        Assert.True(monoFixed);
        Assert.False(monoSerif);
        Assert.True(consolas);
        Assert.True(consolasFixed);
        Assert.True(liberationMono);
        Assert.True(liberationFixed);
        Assert.False(inter);
        Assert.False(comic);
        Assert.False(withoutAliases);
        Assert.Equal(["Segoe UI", "Arial", "sans-serif"], families);
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
    ///     Tells whether any pixel in a region of a surface differs from a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="color">The color.</param>
    /// <param name="left">The left edge of the region.</param>
    /// <param name="top">The top edge of the region.</param>
    /// <param name="right">The right edge of the region, not included.</param>
    /// <param name="bottom">The bottom edge of the region, not included.</param>
    /// <returns><see langword="true" /> when at least one pixel of the region is not the color.</returns>
    private static bool HasPixelOtherThan(Surface surface, Rgba32 color, int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (surface[x, y] != color)
                {
                    return true;
                }
            }
        }

        return false;
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

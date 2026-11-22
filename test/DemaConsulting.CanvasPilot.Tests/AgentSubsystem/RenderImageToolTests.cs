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

using System.Buffers.Binary;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the picture tool of <see cref="ProjectTools" />: <c>render_image</c> draws an image source or picture
///     of the project, or an example image of the active style kit, through the render service, and returns the PNG scaled
///     to at most 1280 pixels with a text part; a render that fails is an answer; and no file is written anywhere.
/// </summary>
public sealed class RenderImageToolTests : ProjectToolsTestBase
{
    /// <summary>
    ///     Test that a picture of the project is drawn with the project folder as the only place the viewer may read from,
    ///     and comes back as a PNG part with a text part that has the size, the format, and every note; and that nothing is
    ///     written to the project folder or anywhere else.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_ProjectFile_ReturnsThePngAndItsDescriptionAndWritesNoFile()
    {
        // Arrange: a file, notes of the renderer, and the state of everything before
        Put(Path.Combine(Project, "cover.svg"), "<svg/>");
        Renderer.Answer = file => RenderResult.Success(TestImages.Create(
            file,
            800,
            600,
            [
                new RenderDiagnostic(RenderDiagnosticSeverity.Warning, "image-access-denied", "an embedded picture was not drawn"),
                new RenderDiagnostic(RenderDiagnosticSeverity.Unsupported, "filter-unsupported", "a filter is not drawn"),
            ]));
        var before = Snapshot(Root);

        // Act
        var answer = await Call("render_image", ("path", "cover.svg"));

        // Assert: one picture of type PNG, the text with size, format, and both notes, the folder, and no new file
        var picture = ToolAnswers.Picture(answer);
        Assert.NotNull(picture);
        Assert.Equal("image/png", picture.MediaType);
        Assert.True(picture.Data.Span.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        var text = ToolAnswers.Text(answer);
        Assert.Contains("'cover.svg' (svg), 800 x 600 pixels", text, StringComparison.Ordinal);
        Assert.Contains("image-access-denied", text, StringComparison.Ordinal);
        Assert.Contains("filter-unsupported", text, StringComparison.Ordinal);
        Assert.DoesNotContain("scaled", text, StringComparison.Ordinal);
        var request = Assert.Single(Renderer.Requests);
        Assert.Equal(Path.Combine(Project, "cover.svg"), request.File);
        Assert.Equal(Project, request.Folder);
        Assert.Equal(before, Snapshot(Root));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that a picture that is larger than 1280 pixels on its longer side is scaled down to it with its proportions
    ///     kept (a wide one and a tall one), that a picture that is smaller is not enlarged, and that the text says it was
    ///     scaled.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_LargePicture_IsScaledToAtMost1280PixelsAndSmallOnesAreNotEnlarged()
    {
        // Arrange
        Put(Path.Combine(Project, "wide.svg"), "<svg/>");
        Put(Path.Combine(Project, "tall.svg"), "<svg/>");
        Put(Path.Combine(Project, "small.svg"), "<svg/>");
        Renderer.Answer = file => RenderResult.Success(Path.GetFileName(file) switch
        {
            "wide.svg" => TestImages.Create(file, 3000, 1500),
            "tall.svg" => TestImages.Create(file, 900, 4000),
            _ => TestImages.Create(file, 100, 50),
        });

        // Act
        var wide = ToolAnswers.Text(await Call("render_image", ("path", "wide.svg")));
        var tall = ToolAnswers.Text(await Call("render_image", ("path", "tall.svg")));
        var small = ToolAnswers.Text(await Call("render_image", ("path", "small.svg")));

        // Assert: the sizes that were encoded, and the words
        Assert.Equal([(1280, 640), (288, 1280), (100, 50)], Renderer.Encoded);
        Assert.Contains("3000 x 1500 pixels. The picture is scaled down to 1280 x 640", wide, StringComparison.Ordinal);
        Assert.Contains("scaled down to 288 x 1280", tall, StringComparison.Ordinal);
        Assert.DoesNotContain("scaled", small, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a picture whose PNG is too large for an answer is made smaller in steps until it fits, and that one that
    ///     never fits is refused.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_PngOverTheCeiling_IsMadeSmallerInStepsOrRefused()
    {
        // Arrange: a large picture whose PNG shrinks with the size, and one that stays too large
        Put(Path.Combine(Project, "heavy.svg"), "<svg/>");
        Put(Path.Combine(Project, "huge.svg"), "<svg/>");
        Renderer.Answer = file => RenderResult.Success(TestImages.Create(file, 2000, 2000));
        Renderer.PaddedLength = image => image.Width > 700 ? 9 * 1024 * 1024 : 1024;

        // Act
        var heavy = await Call("render_image", ("path", "heavy.svg"));
        var steps = Renderer.Encoded.ToList();
        Renderer.PaddedLength = _ => 9 * 1024 * 1024;
        var huge = ToolAnswers.Text(await Call("render_image", ("path", "huge.svg")));

        // Assert: 1280 and 960 are too large, 640 fits; the other never fits
        Assert.NotNull(ToolAnswers.Picture(heavy));
        Assert.Equal([(1280, 1280), (960, 960), (640, 640)], steps);
        Assert.Contains("too large", huge, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a real SVG is drawn by the real render service into a real PNG that is scaled to 1280 pixels, with no
    ///     file written.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_RealSvgWithTheRealRenderService_ReturnsARealScaledPng()
    {
        // Arrange: tools over the real renderer, and a drawing that is larger than the limit
        Put(
            Path.Combine(Project, "big.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"3000\" height=\"1500\"><rect width=\"3000\" height=\"1500\" fill=\"#2563EB\"/></svg>");
        var tools = new ProjectTools(new ImageRenderService(), Style);
        var before = Snapshot(Root);

        // Act
        var answer = await Call(tools, "render_image", ("path", "big.svg"));

        // Assert: a PNG with the signature and the size 1280 x 640 in its header, the text, and no new file
        var picture = ToolAnswers.Picture(answer);
        Assert.NotNull(picture);
        var bytes = picture.Data.ToArray();
        Assert.True(bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        Assert.Equal(1280, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(640, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        Assert.Contains("3000 x 1500", ToolAnswers.Text(answer), StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(Root));
    }

    /// <summary>
    ///     Test that a file that cannot be drawn is an ordinary answer with the code, the message, and the hint of the
    ///     renderer, that a renderer that throws is an answer too (naming only the type of the exception), and that neither
    ///     is an exception.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_FailureOfTheRender_IsAnAnswerWithCodeMessageAndHint()
    {
        // Arrange
        Put(Path.Combine(Project, "broken.mmd"), "graph");
        Put(Path.Combine(Project, "crash.mmd"), "graph");
        Renderer.Answer = file => Path.GetFileName(file) == "crash.mmd"
            ? throw new InvalidOperationException("secret detail " + file)
            : RenderResult.Failure(new RenderError("cannot-draw", "line 2 has a syntax error", "Check the arrow syntax."));

        // Act
        var broken = await Call("render_image", ("path", "broken.mmd"));
        var crash = await Call("render_image", ("path", "crash.mmd"));

        // Assert: the three parts of the error, no picture, and only the type of the exception
        var text = ToolAnswers.Text(broken);
        Assert.Null(ToolAnswers.Picture(broken));
        Assert.Contains("cannot-draw", text, StringComparison.Ordinal);
        Assert.Contains("line 2 has a syntax error", text, StringComparison.Ordinal);
        Assert.Contains("Hint: Check the arrow syntax.", text, StringComparison.Ordinal);
        var crashText = ToolAnswers.Text(crash);
        Assert.Contains("InvalidOperationException", crashText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret detail", crashText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a stopped turn stops the render by cancellation and does not draw, and that a render that reports
    ///     cancellation without a stopped turn is an answer.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_CanceledTurn_ThrowsCancellationAndDoesNotDraw()
    {
        // Arrange
        Put(Path.Combine(Project, "cover.svg"), "<svg/>");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: a canceled token throws before anything is drawn
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Tool("render_image").InvokeAsync(
            new AIFunctionArguments { ["path"] = "cover.svg" },
            source.Token));
        Assert.Empty(Renderer.Requests);

        // Arrange / Act / Assert: a render that says it was canceled while the turn goes on is an answer
        Renderer.Answer = _ => RenderResult.Cancelled();
        var answer = ToolAnswers.Text(await Call("render_image", ("path", "cover.svg")));
        Assert.Contains("canceled", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a file that is not an image source, a missing file, and every path that leaves the project, reaches the
    ///     data folder, or passes through a link are not drawn, and that the renderer is never asked.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_RefusedFilesAndEscapingPaths_AreNeverDrawn()
    {
        // Arrange: a text file, a picture outside, and a junction to a folder with a picture
        Put(Path.Combine(Project, "notes.txt"), "x");
        Put(Path.Combine(Root, "outside.svg"), "<svg/>");
        var secret = Path.Combine(Root, "secret");
        Directory.CreateDirectory(secret);
        Put(Path.Combine(secret, "key.svg"), "<svg/>");
        Links.CreateJunction(Path.Combine(Project, "link"), secret);
        string[] paths =
        [
            "notes.txt",
            "missing.svg",
            "../outside.svg",
            Path.Combine(Root, "outside.svg"),
            "Brochure.canvaspilot",
            ".canvaspilot/draft.svg",
            "link/key.svg",
            Path.Combine(Project, "link", "key.svg"),
            "",
        ];

        // Act
        var answers = new List<string>();
        foreach (var path in paths)
        {
            answers.Add(ToolAnswers.Text(await Call("render_image", ("path", path))));
        }

        // Assert: the renderer was never asked, and each answer is a sentence
        Assert.Empty(Renderer.Requests);
        Assert.All(answers, text => Assert.False(string.IsNullOrWhiteSpace(text)));
        Assert.Contains("not an image source", answers[0], StringComparison.Ordinal);
        Assert.Contains("no file", answers[1], StringComparison.Ordinal);
        Assert.Contains("link", answers[6], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an example image of the active style kit is drawn by its full path with the kit folder as the only
    ///     place the viewer may read from; that the grant follows the active kit at the time of the call; and that it is
    ///     images only, the kit folder only, and not through links or <c>..</c>.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_StyleKitExample_IsAllowedFromTheActiveKitFolderOnly()
    {
        // Arrange: a kit outside the project with an example, a style file, a link to a secret, and a picture beside it
        var kit = Path.Combine(Root, "kit");
        Directory.CreateDirectory(Path.Combine(kit, "examples"));
        Put(Path.Combine(kit, "examples", "title.svg"), "<svg/>");
        Put(Path.Combine(kit, "style.json"), "{}");
        var secret = Path.Combine(Root, "secret");
        Directory.CreateDirectory(secret);
        Put(Path.Combine(secret, "key.svg"), "<svg/>");
        Links.CreateJunction(Path.Combine(kit, "examples", "link"), secret);
        Put(Path.Combine(Root, "beside.svg"), "<svg/>");
        var entry = new StyleKitEntry(StyleKitPlaces.Kit("brand"), StyleKitLocation.User, kit, null, false, false, null, []);

        // Act: no kit is active, then the kit is
        var withoutKit = ToolAnswers.Text(await Call("render_image", ("path", Path.Combine(kit, "examples", "title.svg"))));
        Style.Use(entry);
        var example = await Call("render_image", ("path", Path.Combine(kit, "examples", "title.svg")));
        var styleFile = ToolAnswers.Text(await Call("render_image", ("path", Path.Combine(kit, "style.json"))));
        var beside = ToolAnswers.Text(await Call("render_image", ("path", Path.Combine(Root, "beside.svg"))));
        var climb = ToolAnswers.Text(await Call("render_image", ("path", Path.Combine(kit, "examples", "..", "..", "beside.svg"))));
        var link = ToolAnswers.Text(await Call("render_image", ("path", Path.Combine(kit, "examples", "link", "key.svg"))));
        var relative = ToolAnswers.Text(await Call("render_image", ("path", "examples/title.svg")));
        Style.Use(null);
        var afterChange = ToolAnswers.Text(await Call("render_image", ("path", Path.Combine(kit, "examples", "title.svg"))));

        // Assert: only the example with the kit active was drawn, and with the kit folder as the root
        Assert.DoesNotContain("Drawn", withoutKit, StringComparison.Ordinal);
        Assert.NotNull(ToolAnswers.Picture(example));
        Assert.Contains("style kit example 'examples/title.svg'", ToolAnswers.Text(example), StringComparison.Ordinal);
        var request = Assert.Single(Renderer.Requests);
        Assert.Equal(Path.Combine(kit, "examples", "title.svg"), request.File);
        Assert.Equal(kit, request.Folder);
        Assert.DoesNotContain("Drawn", styleFile + beside + climb + link + relative + afterChange, StringComparison.Ordinal);
        Assert.Contains("not an image source", styleFile, StringComparison.Ordinal);
        Assert.Contains("link", link, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a kit that lies inside the project is drawn as a file of the project (the folder of the project is the
    ///     root), so the grant of the kit does not widen or narrow what the project allows.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_StyleKitInsideTheProject_IsDrawnAsAProjectFile()
    {
        // Arrange: a kit in the styles folder of the project
        var kit = Path.Combine(Project, "styles", "brand");
        Directory.CreateDirectory(Path.Combine(kit, "examples"));
        Put(Path.Combine(kit, "examples", "title.svg"), "<svg/>");
        Style.Use(new StyleKitEntry(StyleKitPlaces.Kit("brand"), StyleKitLocation.Project, kit, null, false, false, null, []));

        // Act: by the full path and by the path relative to the project
        await Call("render_image", ("path", Path.Combine(kit, "examples", "title.svg")));
        await Call("render_image", ("path", "styles/brand/examples/title.svg"));

        // Assert: both are drawn with the project folder as the root
        Assert.Equal(2, Renderer.Requests.Count);
        Assert.All(Renderer.Requests, request => Assert.Equal(Project, request.Folder));
    }

    /// <summary>
    ///     Test that a missing path argument is an answer and not an exception.
    /// </summary>
    [Fact]
    public async Task RenderImageTool_Render_MissingPath_IsAnAnswer()
    {
        // Act
        var answer = ToolAnswers.Text(await Call("render_image", ("path", null)));

        // Assert
        Assert.Contains("empty", answer, StringComparison.Ordinal);
        Assert.Empty(Renderer.Requests);
    }
}

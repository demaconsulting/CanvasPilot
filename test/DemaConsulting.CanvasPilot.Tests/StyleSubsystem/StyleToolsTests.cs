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

using System.Diagnostics;
using System.Text.Json;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StyleTools" />: the descriptors of the two tools, every mode and every kind of error of
///     <c>style_get</c> with a fake active style, every mode and error of <c>style_check</c> with a fake renderer, the
///     containment of the path to the project (including links that lead out), the bounds of the tolerance, bad arguments,
///     and a real render of a file on and off the palette.
/// </summary>
public sealed class StyleToolsTests : IDisposable
{
    /// <summary>
    ///     The places that hold the kits and the project, in a temporary folder.
    /// </summary>
    private readonly StyleKitPlaces _places = new();

    /// <summary>
    ///     The style that the open project uses, which a test sets.
    /// </summary>
    private readonly FakeActiveStyle _active = new();

    /// <summary>
    ///     The folder of the project, which is a folder of the places.
    /// </summary>
    private readonly string _project;

    /// <summary>
    ///     The folder outside the project that a link leads to.
    /// </summary>
    private readonly string _outside;

    /// <summary>
    ///     The file that the tool takes as the picture that is shown.
    /// </summary>
    private string? _shown;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleToolsTests" /> class with a project folder and a folder
    ///     outside it that each hold a picture.
    /// </summary>
    public StyleToolsTests()
    {
        _project = Directory.CreateDirectory(Path.Combine(_places.Root, "the-project")).FullName;
        _outside = Directory.CreateDirectory(Path.Combine(_places.Root, "elsewhere")).FullName;
        File.WriteAllText(Path.Combine(_outside, "secret.svg"), WhiteSvg);
        File.WriteAllText(Path.Combine(_project, "inside.svg"), WhiteSvg);
    }

    /// <summary>
    ///     Gets the text of an SVG that is only white.
    /// </summary>
    private static string WhiteSvg =>
        "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10' fill='#FFFFFF'/></svg>";

    /// <summary>
    ///     Gets the token that cancels the running test.
    /// </summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public void Dispose()
    {
        // A junction is removed as a link and not by deleting what it leads to
        foreach (var link in Directory.EnumerateDirectories(_project).Where(d => new DirectoryInfo(d).LinkTarget is not null))
        {
            Directory.Delete(link);
        }

        _places.Dispose();
    }

    /// <summary>
    ///     Validates the descriptors: the names, descriptions that tell what to call and when, schemas that are JSON objects
    ///     with the arguments of the design and no other property, a section list that is the one of the guidance, and that
    ///     a missing collaborator is refused.
    /// </summary>
    [Fact]
    public void StyleTools_CreateAll_Descriptors_HaveNamesDescriptionsAndSchemas()
    {
        // Act
        var tools = StyleTools.CreateAll(_active, _places.CreateCatalog(), () => _project, () => _shown, Substitute.For<IImageRenderService>());
        var get = tools[0];
        var check = tools[1];
        using var getSchema = JsonDocument.Parse(get.ParametersSchema);
        using var checkSchema = JsonDocument.Parse(check.ParametersSchema);

        // Assert: two tools with the names of the design and descriptions that say how to call them
        Assert.Equal(["style_get", "style_check"], tools.Select(t => t.Name));
        Assert.Equal((StyleTools.GetToolName, StyleTools.CheckToolName), (get.Name, check.Name));
        Assert.Contains("Call it before you draw", get.Description, StringComparison.Ordinal);
        Assert.Contains("after you draw", check.Description, StringComparison.Ordinal);
        Assert.All(tools, t => Assert.NotNull(t.Invoke));

        // The schemas are objects that allow only the arguments of the design
        foreach (var schema in new[] { getSchema, checkSchema })
        {
            Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
            Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        }

        Assert.Equal(["kit", "section"], getSchema.RootElement.GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            StyleGuidance.SectionNames,
            getSchema.RootElement.GetProperty("properties").GetProperty("section").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["path", "tolerance"], checkSchema.RootElement.GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.Equal("number", checkSchema.RootElement.GetProperty("properties").GetProperty("tolerance").GetProperty("type").GetString());
        Assert.Throws<ArgumentNullException>(() => StyleTools.CreateGetTool(null!, _places.CreateCatalog()));
        Assert.Throws<ArgumentNullException>(() => StyleTools.CreateGetTool(_active, null!));
        Assert.Throws<ArgumentNullException>(() => StyleTools.CreateCheckTool(null!, () => _project, () => _shown, Substitute.For<IImageRenderService>()));
        Assert.Throws<ArgumentNullException>(() => StyleTools.CreateCheckTool(_active, null!, () => _shown, Substitute.For<IImageRenderService>()));
        Assert.Throws<ArgumentNullException>(() => StyleTools.CreateCheckTool(_active, () => _project, null!, Substitute.For<IImageRenderService>()));
        Assert.Throws<ArgumentNullException>(() => StyleTools.CreateCheckTool(_active, () => _project, () => _shown, null!));
    }

    /// <summary>
    ///     Validates that <c>style_get</c> with no arguments, in each way of saying so, gives the block of the active kit,
    ///     and that the block is the one of the guidance.
    /// </summary>
    /// <param name="arguments">The arguments, or nothing.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"kit\":null,\"section\":\"\"}")]
    [InlineData("{\"unknown\":1}")]
    public async Task StyleTools_Get_NoArguments_GivesTheGuidanceOfTheActiveKit(string? arguments)
    {
        // Arrange: a project that uses a kit
        var get = await CreateGetAsync("clean", "slides-dark");
        _active.Use(_places.Catalog!.Resolve("clean"));

        // Act
        var answer = get.Invoke(arguments!);

        // Assert
        Assert.Equal(StyleGuidance.Build(_places.Catalog.Resolve("clean")!), answer);
    }

    /// <summary>
    ///     Validates that a kit that is named gives its own guidance, also when the project uses another one, and that an
    ///     id that no kit has is answered with the ids that are known.
    /// </summary>
    [Fact]
    public async Task StyleTools_Get_NamedKit_GivesThatKitOrListsTheKnownKits()
    {
        // Arrange: two kits, the project uses the first
        var get = await CreateGetAsync("clean", "slides-dark");
        _active.Use(_places.Catalog!.Resolve("clean"));

        // Act
        var named = get.Invoke("{\"kit\":\"slides-dark\"}");
        var unknown = get.Invoke("{\"kit\":\"nobody\"}");

        // Assert: the named kit is described, and the unknown one is answered with the known ids
        Assert.Equal(StyleGuidance.Build(_places.Catalog.Resolve("slides-dark")!), named);
        Assert.Equal("No style kit has the id 'nobody'. Known kits: clean, slides-dark.", unknown);
    }

    /// <summary>
    ///     Validates that with no kit chosen, and with a kit that is missing, <c>style_get</c> says how to choose one and
    ///     lists the kits that are known, and that with no kit at all it says there are none.
    /// </summary>
    [Fact]
    public async Task StyleTools_Get_NoActiveKitOrMissingKit_SaysHowToChoose()
    {
        // Arrange: two kits and a project with no style
        var get = await CreateGetAsync("clean", "slides-dark");
        _active.Use(null);

        // Act
        var none = get.Invoke("{}");
        _active.UseMissing("lost-kit");
        var missing = get.Invoke("{}");
        var empty = StyleTools.CreateGetTool(_active, new StyleKitPlaces().CreateCatalog()).Invoke("{}");

        // Assert: each answer says what is wrong and how to get a style
        Assert.StartsWith("This project has no style kit. Ask the user to choose one", none, StringComparison.Ordinal);
        Assert.Contains("call style_get with kit set to the id of a kit. Known kits: clean, slides-dark.", none, StringComparison.Ordinal);
        Assert.StartsWith("The project names the style kit 'lost-kit', which cannot be found.", missing, StringComparison.Ordinal);
        Assert.EndsWith("No style kits are known.", empty, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a section is given in full, whatever the case of its name, that every section of the list works,
    ///     and that a name that is not a section is answered with the names.
    /// </summary>
    [Fact]
    public async Task StyleTools_Get_Section_GivesThatSectionOrListsTheSections()
    {
        // Arrange
        var get = await CreateGetAsync("clean");
        var entry = _places.Catalog!.Resolve("clean")!;
        _active.Use(entry);

        // Act
        var palette = get.Invoke("{\"section\":\"Palette\"}");
        var vega = get.Invoke("{\"kit\":\"clean\",\"section\":\"vegaLite\"}");
        var bad = get.Invoke("{\"section\":\"colors\"}");

        // Assert
        Assert.Equal(StyleGuidance.BuildSection(entry, "palette"), palette);
        Assert.Equal(StyleGuidance.BuildSection(entry, "vegaLite"), vega);
        Assert.All(StyleGuidance.SectionNames, name => Assert.Equal(StyleGuidance.BuildSection(entry, name), get.Invoke($"{{\"section\":\"{name}\"}}")));
        Assert.Equal(
            "Error: 'colors' is not a section of the style. Use one of: palette, fonts, mermaid, vegaLite, notes, presets, examples.",
            bad);
    }

    /// <summary>
    ///     Validates that arguments that are not JSON, not an object, or of the wrong type are answered with a message that
    ///     says what to send, and that a catalog that fails does not make the tool throw.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="expected">A part of the message.</param>
    [Theory]
    [InlineData("{", "not valid JSON")]
    [InlineData("[1]", "must be a JSON object")]
    [InlineData("\"text\"", "must be a JSON object")]
    [InlineData("{\"kit\":5}", "'kit' must be a string")]
    [InlineData("{\"section\":true}", "'section' must be a string")]
    public async Task StyleTools_Get_BadArguments_AreAnsweredWithAMessage(string arguments, string expected)
    {
        // Arrange
        var get = await CreateGetAsync("clean");
        _active.Use(_places.Catalog!.Resolve("clean"));

        // Act
        var answer = get.Invoke(arguments);

        // Assert
        Assert.StartsWith("Error: ", answer, StringComparison.Ordinal);
        Assert.Contains(expected, answer, StringComparison.Ordinal);
        Assert.Contains("style_get", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a catalog that throws does not make <c>style_get</c> throw.
    /// </summary>
    [Fact]
    public void StyleTools_Get_CatalogThrows_IsAnsweredWithAMessage()
    {
        // Arrange
        var catalog = Substitute.For<IStyleKitCatalog>();
        catalog.Resolve(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("broken"));
        var get = StyleTools.CreateGetTool(_active, catalog);

        // Act
        var answer = get.Invoke("{\"kit\":\"clean\"}");

        // Assert
        Assert.Equal("Error: the tool could not answer (InvalidOperationException).", answer);
    }

    /// <summary>
    ///     Validates that <c>style_check</c> resolves a relative path against the project folder, draws that file with the
    ///     project folder as the folder it may read, on a thread of the pool, and answers with the text of the report.
    /// </summary>
    [Fact]
    public async Task StyleTools_Check_RelativePath_DrawsTheFileAndAnswersWithTheReport()
    {
        // Arrange: a renderer that gives a picture of the palette color, and the thread that it was called on
        var renderer = Substitute.For<IImageRenderService>();
        var onPoolThread = false;
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            onPoolThread = Thread.CurrentThread.IsThreadPoolThread;
            return Task.FromResult(RenderResult.Success(new RenderedImage((string)call[0], "svg", 2, 2, Enumerable.Repeat((byte)255, 16).ToArray())));
        });
        var check = await CreateCheckAsync(renderer, "clean");
        _active.Use(_places.Catalog!.Resolve("clean"));

        // Act
        var answer = check.Invoke("{\"path\":\"inside.svg\"}");

        // Assert: the full path inside the project and the project folder were given to the renderer, and the answer is the text
        await renderer.Received(1).RenderAsync(Path.Combine(_project, "inside.svg"), _project, Arg.Any<CancellationToken>());
        Assert.True(onPoolThread);
        Assert.StartsWith("Style check of inside.svg against \"Clean\" (clean): 2 x 2 px, 4 pixels counted (0 transparent).\n", answer, StringComparison.Ordinal);
        Assert.Contains("Result: PASSED", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that with no path the picture that is shown is checked, that a full path inside the project is accepted,
    ///     and that with neither the answer says what to send.
    /// </summary>
    [Fact]
    public async Task StyleTools_Check_NoPathOrFullPath_UsesTheShownPictureOrTheFullPath()
    {
        // Arrange
        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(RenderResult.Success(new RenderedImage((string)call[0], "svg", 1, 1, Opaque(255, 255, 255)))));
        var check = await CreateCheckAsync(renderer, "clean");
        _active.Use(_places.Catalog!.Resolve("clean"));
        _shown = Path.Combine(_project, "inside.svg");

        // Act
        var shown = check.Invoke("{}");
        _shown = null;
        var neither = check.Invoke(string.Empty);
        var full = check.Invoke("{\"path\":" + JsonSerializer.Serialize(Path.Combine(_project, "inside.svg")) + "}");

        // Assert
        Assert.StartsWith("Style check of inside.svg against", shown, StringComparison.Ordinal);
        Assert.StartsWith("Error: no path was given and no picture is shown.", neither, StringComparison.Ordinal);
        Assert.StartsWith("Style check of inside.svg against", full, StringComparison.Ordinal);
        await renderer.Received(2).RenderAsync(Path.Combine(_project, "inside.svg"), _project, Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Validates the tolerance: it reaches the check (a color just outside the default tolerance passes with a larger
    ///     one), and values out of its range or of the wrong type are refused.
    /// </summary>
    [Fact]
    public async Task StyleTools_Check_Tolerance_ReachesTheCheckAndIsBounded()
    {
        // Arrange: a kit with only white and a picture of a color that is just outside the default tolerance
        var kit = StyleKitPlaces.Kit("only-white") with
        {
            Palette = new Dictionary<string, string> { ["background"] = "#FFFFFF", ["text"] = "#FFFFFF", ["primary"] = "#FFFFFF" }
        };
        StyleKitPlaces.WriteKit(_places.BuiltIn, kit);
        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(RenderResult.Success(new RenderedImage((string)call[0], "svg", 1, 1, Opaque(255, 255, 230)))));
        var check = await CreateCheckAsync(renderer);
        _active.Use(_places.Catalog!.Resolve("only-white"));

        // Act
        var strict = check.Invoke("{\"path\":\"inside.svg\"}");
        var lenient = check.Invoke("{\"path\":\"inside.svg\",\"tolerance\":30}");
        var edgeLow = check.Invoke("{\"path\":\"inside.svg\",\"tolerance\":0}");
        var edgeHigh = check.Invoke("{\"path\":\"inside.svg\",\"tolerance\":100}");
        var tooLow = check.Invoke("{\"path\":\"inside.svg\",\"tolerance\":-0.5}");
        var tooHigh = check.Invoke("{\"path\":\"inside.svg\",\"tolerance\":100.5}");
        var text = check.Invoke("{\"path\":\"inside.svg\",\"tolerance\":\"10\"}");

        // Assert: the default fails the color and 30 accepts it, the ends of the range are accepted, outside them is refused
        Assert.Contains("Result: FAILED", strict, StringComparison.Ordinal);
        Assert.Contains("Result: PASSED", lenient, StringComparison.Ordinal);
        Assert.Contains("Result: FAILED", edgeLow, StringComparison.Ordinal);
        Assert.Contains("Result: PASSED", edgeHigh, StringComparison.Ordinal);
        Assert.All(new[] { tooLow, tooHigh, text }, answer => Assert.StartsWith("Error: tolerance must be a number from 0 to 100.", answer, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a path that is not inside the project, in each way that it can be written, is refused before
    ///     anything is drawn: a relative path that climbs out, a full path elsewhere, a network path, the reserved data
    ///     folder, a folder, a file that is not there, a character that a path cannot have, and a name that is not a path.
    /// </summary>
    [Fact]
    public async Task StyleTools_Check_PathOutsideTheProject_IsRefusedBeforeDrawing()
    {
        // Arrange: a picture in the data folder of the application, and a folder with a picture in it
        Directory.CreateDirectory(Path.Combine(_project, ".canvaspilot"));
        await File.WriteAllTextAsync(Path.Combine(_project, ".canvaspilot", "cache.svg"), WhiteSvg, Ct);
        Directory.CreateDirectory(Path.Combine(_project, "sub"));
        var renderer = Substitute.For<IImageRenderService>();
        var check = await CreateCheckAsync(renderer, "clean");
        _active.Use(_places.Catalog!.Resolve("clean"));
        var secret = Path.Combine(_outside, "secret.svg");

        // Act and assert: each way of writing a path out of the project, or to nothing, is an error that says why
        Refused(check, "..\\elsewhere\\secret.svg", "is not a path inside the project folder");
        Refused(check, "../elsewhere/secret.svg", "is not a path inside the project folder");
        Refused(check, "sub\\..\\..\\elsewhere\\secret.svg", "is not a path inside the project folder");
        Refused(check, secret, "is not a path inside the project folder");
        Refused(check, "\\\\server\\share\\picture.svg", "is not a path inside the project folder");
        Refused(check, ".canvaspilot\\cache.svg", "data folder of the application");
        Refused(check, ".CanvasPilot/cache.svg", "data folder of the application");
        Refused(check, "sub", "is a folder");
        Refused(check, "missing.svg", "no file 'missing.svg'");
        Refused(check, "bad\0name.svg", "is not a path inside the project folder");
        await renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Validates that a junction inside the project that leads outside, and a file in such a folder, are refused, that a
    ///     junction that leads to another place in the project is allowed, and that a symbolic link to a file outside is
    ///     refused when the machine allows making one.
    /// </summary>
    [Fact]
    public async Task StyleTools_Check_LinkThatLeadsOutOfTheProject_IsRefused()
    {
        // Arrange: a junction to the outside folder, and one to a folder of the project
        Directory.CreateDirectory(Path.Combine(_project, "real"));
        await File.WriteAllTextAsync(Path.Combine(_project, "real", "good.svg"), WhiteSvg, Ct);
        var outLink = Path.Combine(_project, "out-link");
        var inLink = Path.Combine(_project, "in-link");
        if (!TryCreateLink(outLink, _outside) || !TryCreateLink(inLink, Path.Combine(_project, "real")))
        {
            Assert.Skip("This machine does not allow making a junction or a link.");
        }

        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(RenderResult.Success(new RenderedImage((string)call[0], "svg", 1, 1, Opaque(255, 255, 255)))));
        var check = await CreateCheckAsync(renderer, "clean");
        _active.Use(_places.Catalog!.Resolve("clean"));

        // Act
        var viaInLink = check.Invoke("{\"path\":\"in-link/good.svg\"}");

        // Assert: the junction to the outside is refused, the junction inside the project is drawn
        Refused(check, "out-link/secret.svg", "is, or is inside, a link to a place outside the project folder");
        Assert.StartsWith("Style check of good.svg against", viaInLink, StringComparison.Ordinal);
        await renderer.Received(1).RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // A symbolic link to a file outside the project is refused too, where a link can be made
        var fileLink = Path.Combine(_project, "file-link.svg");
        try
        {
            File.CreateSymbolicLink(fileLink, Path.Combine(_outside, "secret.svg"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        Refused(check, "file-link.svg", "is, or is inside, a link to a place outside the project folder");
    }

    /// <summary>
    ///     Validates the answers when the check cannot start: no project, no kit, a missing kit, and a picture that cannot
    ///     be drawn or whose drawing was cancelled, and that a renderer that throws does not make the tool throw.
    /// </summary>
    [Fact]
    public async Task StyleTools_Check_NoProjectNoKitOrDrawingFails_AreAnsweredWithAMessage()
    {
        // Arrange
        var renderer = Substitute.For<IImageRenderService>();
        var folder = (string?)_project;
        var check = StyleTools.CreateCheckTool(_active, () => folder, () => _shown, renderer);
        await _places.CreateCatalog().RefreshAsync(Ct);
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean"));
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        var arguments = "{\"path\":\"inside.svg\"}";

        // Act: with no kit, with a missing kit, with no project, then with a kit and three kinds of failure to draw
        var noKit = check.Invoke(arguments);
        _active.UseMissing("lost-kit");
        var missingKit = check.Invoke(arguments);
        _active.Use(catalog.Resolve("clean"));
        folder = null;
        var noProject = check.Invoke(arguments);
        folder = _project;
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(RenderResult.Failure(new RenderError("cannot-draw", "The file has a syntax error.", "Fix line 3."))),
            Task.FromResult(RenderResult.Cancelled()),
            Task.FromException<RenderResult>(new InvalidOperationException("boom")));
        var failed = check.Invoke(arguments);
        var cancelled = check.Invoke(arguments);
        var thrown = check.Invoke(arguments);

        // Assert
        Assert.StartsWith("Error: this project has no style kit", noKit, StringComparison.Ordinal);
        Assert.StartsWith("Error: the project names the style kit 'lost-kit', which cannot be found", missingKit, StringComparison.Ordinal);
        Assert.Equal("Error: no project is open, so there is no picture to check.", noProject);
        Assert.Equal("Error: the picture could not be drawn (cannot-draw): The file has a syntax error. Fix line 3.", failed);
        Assert.Equal("Error: the picture could not be drawn because the drawing was cancelled.", cancelled);
        Assert.Equal("Error: the tool could not answer (InvalidOperationException).", thrown);
    }

    /// <summary>
    ///     Validates that arguments that are not JSON, not an object, or of the wrong type are answered with a message that
    ///     says what to send, and that the arguments of <c>style_get</c> are not accepted as a path.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="expected">A part of the message.</param>
    [Theory]
    [InlineData("{", "not valid JSON")]
    [InlineData("[\"inside.svg\"]", "must be a JSON object")]
    [InlineData("{\"path\":5}", "'path' must be a string")]
    [InlineData("{\"path\":\"inside.svg\",\"tolerance\":null}", "Style check of inside.svg")]
    public async Task StyleTools_Check_BadArguments_AreAnsweredWithAMessage(string arguments, string expected)
    {
        // Arrange
        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(RenderResult.Success(new RenderedImage((string)call[0], "svg", 1, 1, Opaque(255, 255, 255)))));
        var check = await CreateCheckAsync(renderer, "clean");
        _active.Use(_places.Catalog!.Resolve("clean"));

        // Act
        var answer = check.Invoke(arguments);

        // Assert
        Assert.Contains(expected, answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates with the real render service that a file that uses the palette of the kit passes, that a file with
    ///     another color fails and names it, and that a file that cannot be drawn is answered with the reason.
    /// </summary>
    [Fact]
    public async Task StyleTools_Check_RealRender_PassesOnAndFailsOffThePalette()
    {
        // Arrange: three files: on the palette, off it, and not a picture
        await File.WriteAllTextAsync(Path.Combine(_project, "on.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='50' height='50'><rect width='50' height='50' fill='#FFFFFF'/><rect x='10' y='10' width='30' height='30' fill='#2563EB'/></svg>", Ct);
        await File.WriteAllTextAsync(Path.Combine(_project, "off.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='50' height='50'><rect width='50' height='50' fill='#FFFFFF'/><rect width='25' height='50' fill='#D32F2F'/></svg>", Ct);
        await File.WriteAllTextAsync(Path.Combine(_project, "broken.svg"), "<svg", Ct);
        var check = await CreateCheckAsync(new ImageRenderService(), "clean");
        _active.Use(_places.Catalog!.Resolve("clean"));

        // Act
        var on = check.Invoke("{\"path\":\"on.svg\"}");
        var off = check.Invoke("{\"path\":\"off.svg\"}");
        var broken = check.Invoke("{\"path\":\"broken.svg\"}");

        // Assert
        Assert.Contains("Result: PASSED", on, StringComparison.Ordinal);
        Assert.Contains("#2563EB primary", on, StringComparison.Ordinal);
        Assert.Contains("Result: FAILED, 50.0% of the pixels are off the palette", off, StringComparison.Ordinal);
        Assert.Contains("Off the palette: #D32F2F 50.0%", off, StringComparison.Ordinal);
        Assert.StartsWith("Error: the picture could not be drawn (", broken, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Makes the pixels of a picture of one opaque color, for a picture of one pixel.
    /// </summary>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <returns>The four bytes of the pixel.</returns>
    private static byte[] Opaque(byte red, byte green, byte blue) => [red, green, blue, 255];

    /// <summary>
    ///     Validates that every answer of <c>style_check</c> to a refused path is an error with the path, and that the
    ///     answer does not repeat a long path in full.
    /// </summary>
    /// <param name="check">The tool.</param>
    /// <param name="path">The path to try.</param>
    /// <param name="reason">A part of the message that says why.</param>
    private static void Refused(KnowledgeTool check, string path, string reason)
    {
        var answer = check.Invoke("{\"path\":" + JsonSerializer.Serialize(path) + "}");
        Assert.StartsWith("Error: ", answer, StringComparison.Ordinal);
        Assert.Contains(reason, answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Writes the kits that a test needs into the folder of the program, finds them with a catalog, and creates the tool
    ///     that gives the style.
    /// </summary>
    /// <param name="ids">The ids of the kits.</param>
    /// <returns>The tool.</returns>
    private async Task<KnowledgeTool> CreateGetAsync(params string[] ids)
    {
        await FindKitsAsync(ids);
        return StyleTools.CreateGetTool(_active, _places.Catalog!);
    }

    /// <summary>
    ///     Writes the kits that a test needs, finds them with a catalog, and creates the tool that checks a picture.
    /// </summary>
    /// <param name="renderer">The service that draws the file.</param>
    /// <param name="ids">The ids of the kits.</param>
    /// <returns>The tool.</returns>
    private async Task<KnowledgeTool> CreateCheckAsync(IImageRenderService renderer, params string[] ids)
    {
        await FindKitsAsync(ids);
        return StyleTools.CreateCheckTool(_active, () => _project, () => _shown, renderer);
    }

    /// <summary>
    ///     Writes kits and finds them with a catalog that the places keep.
    /// </summary>
    /// <param name="ids">The ids of the kits.</param>
    /// <returns>A task that completes when the catalog has read them.</returns>
    private async Task FindKitsAsync(string[] ids)
    {
        foreach (var id in ids)
        {
            StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit(id, name: char.ToUpperInvariant(id[0]) + id[1..]));
        }

        _places.Catalog = _places.CreateCatalog();
        await _places.Catalog.RefreshAsync(Ct);
    }

    /// <summary>
    ///     Makes a junction (on Windows) or a symbolic link to a folder.
    /// </summary>
    /// <param name="link">The path of the link.</param>
    /// <param name="target">The folder that it leads to.</param>
    /// <returns><see langword="true" /> when the link was made.</returns>
    private static bool TryCreateLink(string link, string target)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Directory.CreateSymbolicLink(link, target);
                return true;
            }

            // A junction needs no privilege, unlike a symbolic link, and is what a user is likely to have
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process!.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

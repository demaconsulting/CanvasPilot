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

using System.Text.Json;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of the style kits that come with the program, as they are copied beside the test assembly the way they are
///     copied beside the executable: that each is a kit without error or warning, that its example files exist and are
///     drawn without a note, that the text is readable on the background, that its colors are the ones of the design, and
///     that the catalog finds them in its default place.
/// </summary>
public sealed class BuiltInStyleKitTests : IDisposable
{
    /// <summary>
    ///     The ids of the kits that come with the program.
    /// </summary>
    private static readonly string[] KitIds = ["clean", "slides-dark", "dema"];

    /// <summary>
    ///     The names of the example files that every built-in kit has.
    /// </summary>
    private static readonly string[] ExampleFiles = ["title-card.svg", "flow.mmd", "chart.vl.json"];

    /// <summary>
    ///     Temporary folder removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-builtin-kits-").FullName;

    /// <summary>
    ///     Gets the folder of the kits that come with the program, beside the test assembly.
    /// </summary>
    private static string BuiltInFolder => Path.Combine(AppContext.BaseDirectory, StyleKitCatalog.BuiltInFolderName);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that each kit that comes with the program loads with no error and no warning, with the id, the name,
    ///     and the Mermaid theme of the design, and that its three examples are found with captions.
    /// </summary>
    /// <param name="id">The id of the kit.</param>
    /// <param name="name">The name that the kit has.</param>
    [Theory]
    [InlineData("clean", "Clean")]
    [InlineData("slides-dark", "Slides Dark")]
    [InlineData("dema", "DEMA")]
    public void BuiltInStyleKits_ReadFolder_EachKit_LoadsWithNoErrorAndNoWarning(string id, string name)
    {
        // Act
        var result = StyleKitSerializer.ReadFolder(Path.Combine(BuiltInFolder, id));

        // Assert
        Assert.True(result.Succeeded, string.Join("; ", result.Issues));
        Assert.Empty(result.Issues);
        var kit = result.Kit!;
        Assert.Equal(id, kit.Id);
        Assert.Equal(name, kit.Name);
        Assert.Equal("base", kit.Mermaid!.Theme);
        Assert.NotNull(kit.VegaLiteConfigJson);
        Assert.Equal(ExampleFiles.Order(), kit.Examples.Select(e => Path.GetFileName(e.RelativePath)).Order());
        Assert.All(kit.Examples, e => Assert.False(string.IsNullOrWhiteSpace(e.Caption)));
        Assert.All(kit.Examples, e => Assert.True(File.Exists(e.FullPath), e.FullPath));
        Assert.DoesNotContain(kit.Guidance.Do, string.IsNullOrWhiteSpace);
        Assert.NotEmpty(kit.Guidance.Notes);
    }

    /// <summary>
    ///     Validates that every example of every kit is drawn by the render service, confined to the folder of its kit,
    ///     without an error and without a note, so that the examples are real pictures.
    /// </summary>
    [Fact]
    public async Task BuiltInStyleKits_ExampleFiles_AreDrawnWithoutErrorOrNote()
    {
        // Arrange
        var renderer = new ImageRenderService();

        // Act / Assert: each example of each kit is drawn, with the folder of the kit as the only folder it may read
        foreach (var id in KitIds)
        {
            var folder = Path.Combine(BuiltInFolder, id);
            foreach (var file in ExampleFiles)
            {
                var result = await renderer.RenderAsync(Path.Combine(folder, "examples", file), folder, TestContext.Current.CancellationToken);
                Assert.True(result.Succeeded, $"{id}/{file}: {result.Error?.Message}");
                Assert.Empty(result.Image!.Diagnostics);
                Assert.True(result.Image.Width > 100 && result.Image.Height > 50, $"{id}/{file} is {result.Image.Width} x {result.Image.Height}.");
            }
        }
    }

    /// <summary>
    ///     Validates that the text of each kit can be read on its background and on its surface, and that the muted text can
    ///     be read on the background, with the contrast of at least 4.5 to 1 that the guidelines of the web ask for.
    /// </summary>
    /// <param name="id">The id of the kit.</param>
    [Theory]
    [InlineData("clean")]
    [InlineData("slides-dark")]
    [InlineData("dema")]
    public void BuiltInStyleKits_Palette_TextIsReadableOnBackgroundAndSurface(string id)
    {
        // Arrange
        var kit = StyleKitSerializer.ReadFolder(Path.Combine(BuiltInFolder, id)).Kit!;
        var palette = kit.Palette;

        // Act / Assert
        Assert.True(StyleColors.Contrast(palette["text"], palette["background"]) >= 4.5, "text on background");
        Assert.True(StyleColors.Contrast(palette["text"], palette["surface"]) >= 4.5, "text on surface");
        Assert.True(StyleColors.Contrast(palette["muted"], palette["background"]) >= 4.5, "muted text on background");
    }

    /// <summary>
    ///     Validates that the colors, fonts, numbers, and sizes of each kit are the ones of the design.
    /// </summary>
    [Fact]
    public void BuiltInStyleKits_Values_AreTheOnesOfTheDesign()
    {
        // Act
        var clean = StyleKitSerializer.ReadFolder(Path.Combine(BuiltInFolder, "clean")).Kit!;
        var dark = StyleKitSerializer.ReadFolder(Path.Combine(BuiltInFolder, "slides-dark")).Kit!;
        var dema = StyleKitSerializer.ReadFolder(Path.Combine(BuiltInFolder, "dema")).Kit!;

        // Assert
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["background"] = "#FFFFFF",
                ["surface"] = "#F1F5F9",
                ["text"] = "#0F172A",
                ["muted"] = "#475569",
                ["primary"] = "#2563EB",
                ["secondary"] = "#64748B",
                ["accent"] = "#F59E0B",
                ["success"] = "#16A34A",
                ["danger"] = "#C62828"
            },
            clean.Palette);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["background"] = "#0F172A",
                ["surface"] = "#1E293B",
                ["text"] = "#F1F5F9",
                ["muted"] = "#94A3B8",
                ["primary"] = "#60A5FA",
                ["secondary"] = "#94A3B8",
                ["accent"] = "#FBBF24"
            },
            dark.Palette);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["background"] = "#070B1F",
                ["surface"] = "#130F3B",
                ["text"] = "#F8FAFC",
                ["muted"] = "#94A3B8",
                ["primary"] = "#7C3AED",
                ["secondary"] = "#06B6D4",
                ["accent"] = "#FBBF24",
                ["pink"] = "#DB2777",
                ["highlight"] = "#F472B6",
                ["indigo"] = "#A5B4FC",
                ["cyan-bright"] = "#22D3EE"
            },
            dema.Palette);
        Assert.All(new[] { clean, dark, dema }, k => Assert.Equal(new StyleFonts("sans-serif", "sans-serif", "monospace"), k.Fonts));
        Assert.Equal((2.0, 12.0, 16.0), (clean.StrokeWidth, clean.CornerRadius, clean.Spacing));
        Assert.Equal((3.0, 16.0, 24.0), (dark.StrokeWidth, dark.CornerRadius, dark.Spacing));
        Assert.Equal((3.0, 16.0, 24.0), (dema.StrokeWidth, dema.CornerRadius, dema.Spacing));
        Assert.Equal(
            [new StyleSizePreset("Slide 16:9", 1920, 1080, null, false), new StyleSizePreset("Slide 720p", 1280, 720, null, false)],
            dark.SizePresets);
        Assert.Equal(
            [new StyleSizePreset("Banner", 1600, 640, null, false), new StyleSizePreset("Slide 16:9", 1920, 1080, null, false)],
            dema.SizePresets);
        Assert.Equal([new StyleSizePreset("Document (1200 wide)", 1200, null, null, false)], clean.SizePresets);
    }

    /// <summary>
    ///     Validates that the Mermaid example of each kit carries the theme variables of the kit in its front matter, and
    ///     that the chart example carries the palette of the chart of the kit, so that the examples show how to use the
    ///     kit and not another look.
    /// </summary>
    [Fact]
    public void BuiltInStyleKits_Examples_CarryTheThemeAndChartConfigurationOfTheirKit()
    {
        foreach (var id in KitIds)
        {
            // Arrange
            var folder = Path.Combine(BuiltInFolder, id);
            var kit = StyleKitSerializer.ReadFolder(folder).Kit!;
            var diagram = File.ReadAllText(Path.Combine(folder, "examples", "flow.mmd"));
            using var chart = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "examples", "chart.vl.json")));
            using var config = JsonDocument.Parse(kit.VegaLiteConfigJson!);

            // Assert: every theme variable of the kit is in the front matter with its value, and the chart repeats the
            // configuration of the kit
            Assert.Contains("theme: base", diagram, StringComparison.Ordinal);
            Assert.All(kit.Mermaid!.ThemeVariables, v => Assert.Contains($"{v.Key}: \"{v.Value}\"", diagram, StringComparison.Ordinal));
            Assert.Equal(
                config.RootElement.GetProperty("range").GetProperty("category").GetRawText(),
                chart.RootElement.GetProperty("config").GetProperty("range").GetProperty("category").GetRawText().Replace(" ", string.Empty, StringComparison.Ordinal));
            Assert.Equal(
                config.RootElement.GetProperty("background").GetString(),
                chart.RootElement.GetProperty("config").GetProperty("background").GetString());
        }
    }

    /// <summary>
    ///     Validates that the folder beside the program holds the three style files and the nine example files, so that the
    ///     files that are marked as content reach the output folder the way they reach the published folder.
    /// </summary>
    [Fact]
    public void BuiltInStyleKits_OutputFolder_HoldsTheStyleFilesAndAllNineExamples()
    {
        // Act
        var styleFiles = Directory.GetFiles(BuiltInFolder, "style.json", SearchOption.AllDirectories);
        var examples = Directory.GetFiles(BuiltInFolder, "*", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "examples")
            .Select(f => Path.GetRelativePath(BuiltInFolder, f).Replace('\\', '/'))
            .Order()
            .ToList();

        // Assert
        Assert.Equal(3, styleFiles.Length);
        Assert.Equal(
            KitIds.SelectMany(id => ExampleFiles.Select(file => $"{id}/examples/{file}")).Order(),
            examples);
    }

    /// <summary>
    ///     Validates that the catalog finds the three kits in its default place for the kits that come with the program,
    ///     as read only kits of that place, and that a kit of the user with the same id hides them.
    /// </summary>
    [Fact]
    public async Task BuiltInStyleKits_Catalog_DefaultPlace_FindsTheThreeKitsAsReadOnlyAndTheUserCanOverrideThem()
    {
        // Arrange: a catalog that uses the default place for the kits of the program and a folder of its own for the user
        var places = new StyleKitPlaces();
        try
        {
            using var catalog = new StyleKitCatalog(places.Projects, places.Settings, userFolder: places.User);

            // Act
            await catalog.RefreshAsync(TestContext.Current.CancellationToken);
            var found = catalog.Entries.ToList();
            StyleKitPlaces.WriteKit(places.User, StyleKitPlaces.Kit("clean", "mine"));
            await catalog.RefreshAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(KitIds.Order(), found.Select(e => e.Kit.Id).Order());
            Assert.All(found, e => Assert.Equal(StyleKitLocation.BuiltIn, e.Location));
            Assert.All(found, e => Assert.True(e.IsReadOnly));
            Assert.Equal("mine", catalog.Resolve("clean")!.Kit.Description);
            Assert.Equal(StyleKitLocation.User, catalog.Resolve("clean")!.Location);
        }
        finally
        {
            places.Dispose();
        }
    }
}

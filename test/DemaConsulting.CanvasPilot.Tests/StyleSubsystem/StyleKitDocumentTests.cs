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

using System.Text.Json.Nodes;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StyleKitSerializer" />, <see cref="StyleColors" />, and the values of the
///     StyleKitDocument unit: reading a kit file with a message for each wrong property, writing it as canonical text,
///     the warnings about fonts, and the limits that bound the size of a kit.
/// </summary>
public sealed class StyleKitDocumentTests : IDisposable
{
    /// <summary>
    ///     A kit file that uses every property, written with white space that is not the canonical one.
    /// </summary>
    private const string FullKitJson = """
        {
          "formatVersion": 1, "id": "demo-kit", "name": "Demo Kit", "description": "A kit for tests.",
          "palette": { "background": "#ffffff", "text": "#0f172a", "primary": "#2563eb", "secondary": "#64748b",
                       "accent": "#f59e0b", "surface": "#f1f5f9", "muted": "#475569", "success": "#16a34a" },
          "fonts": { "heading": "Segoe UI, sans-serif", "body": "sans-serif", "monospace": "Consolas, monospace" },
          "stroke": { "width": 2 }, "cornerRadius": 12, "spacing": 16,
          "mermaid": { "theme": "base", "themeVariables": { "primaryColor": "#2563EB", "lineColor": "#475569" } },
          "vegaLite": { "config": { "range": { "category": ["#2563EB", "#F59E0B"] }, "axis": { "labelFontSize": 12 } } },
          "guidance": { "notes": "Keep it calm.", "do": ["Use the palette."], "dont": ["Use gradients."] },
          "sizePresets": [
            { "name": "Slide", "width": 1920, "height": 1080, "transparent": false },
            { "name": "Email", "width": 800, "dpi": 150, "transparent": true }
          ],
          "examples": { "examples/card.svg": "A title card." }
        }
        """;

    /// <summary>
    ///     The smallest kit file: only what is required.
    /// </summary>
    private const string MinimalKitJson = """
        { "formatVersion": 1, "id": "tiny", "name": "Tiny",
          "palette": { "background": "#FFFFFF", "text": "#000000", "primary": "#FF0000" } }
        """;

    /// <summary>
    ///     Temporary folder removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-style-kit-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a kit that uses every property is read into every field and written again as the same text,
    ///     byte for byte, which is canonical: line feeds only, normalized colors, and fixed property order.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Write_FullKit_RoundTripsEveryFieldAndIsByteStable()
    {
        // Arrange: the folder of the kit holds one example with a caption and one without
        var folder = MakeKitFolder("demo-kit", FullKitJson, "card.svg", "plain.png");

        // Act: read the folder, write the kit, read the text again, and write again
        var first = StyleKitSerializer.ReadFolder(folder);
        var text = StyleKitSerializer.Write(first.Kit!);
        var second = StyleKitSerializer.Parse(text, folder);
        var again = StyleKitSerializer.Write(second.Kit!);

        // Assert: no problem, every field read, and the second write equal to the first
        Assert.True(first.Succeeded, string.Join("; ", first.Issues));
        Assert.Empty(first.Issues);
        var kit = second.Kit!;
        Assert.Equal("demo-kit", kit.Id);
        Assert.Equal("Demo Kit", kit.Name);
        Assert.Equal("A kit for tests.", kit.Description);
        Assert.Equal(
            ["background", "text", "primary", "secondary", "accent", "surface", "muted", "success"],
            kit.Palette.Keys);
        Assert.Equal("#2563EB", kit.Palette["primary"]);
        Assert.Equal(new StyleFonts("Segoe UI, sans-serif", "sans-serif", "Consolas, monospace"), kit.Fonts);
        Assert.Equal(2, kit.StrokeWidth);
        Assert.Equal(12, kit.CornerRadius);
        Assert.Equal(16, kit.Spacing);
        Assert.Equal("base", kit.Mermaid!.Theme);
        Assert.Equal(["primaryColor", "lineColor"], kit.Mermaid.ThemeVariables.Keys);
        Assert.Equal("""{"range":{"category":["#2563EB","#F59E0B"]},"axis":{"labelFontSize":12}}""", kit.VegaLiteConfigJson);
        Assert.Equal("Keep it calm.", kit.Guidance.Notes);
        Assert.Equal(["Use the palette."], kit.Guidance.Do);
        Assert.Equal(["Use gradients."], kit.Guidance.Dont);
        Assert.Equal(
            [new StyleSizePreset("Slide", 1920, 1080, null, false), new StyleSizePreset("Email", 800, null, 150, true)],
            kit.SizePresets);
        Assert.Equal(["examples/card.svg", "examples/plain.png"], kit.Examples.Select(e => e.RelativePath));
        Assert.Equal(["A title card.", null], kit.Examples.Select(e => e.Caption));
        Assert.Equal(Path.Combine(folder, "examples", "card.svg"), kit.Examples[0].FullPath);
        Assert.Equal(text, again);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.True(text.IndexOf("\"formatVersion\"", StringComparison.Ordinal) < text.IndexOf("\"palette\"", StringComparison.Ordinal));
        Assert.True(text.IndexOf("\"palette\"", StringComparison.Ordinal) < text.IndexOf("\"examples\"", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a kit with only the required properties is a complete kit: the other properties have the plain
    ///     defaults, and the optional Mermaid and Vega-Lite parts are absent and are not written.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_MinimalKit_UsesDefaults()
    {
        // Act
        var result = StyleKitSerializer.Parse(MinimalKitJson, string.Empty);

        // Assert
        Assert.True(result.Succeeded, string.Join("; ", result.Issues));
        var kit = result.Kit!;
        Assert.Equal(string.Empty, kit.Description);
        Assert.Equal(new StyleFonts("sans-serif", "sans-serif", "monospace"), kit.Fonts);
        Assert.Equal(1, kit.StrokeWidth);
        Assert.Equal(0, kit.CornerRadius);
        Assert.Equal(8, kit.Spacing);
        Assert.Null(kit.Mermaid);
        Assert.Null(kit.VegaLiteConfigJson);
        Assert.Empty(kit.SizePresets);
        Assert.Empty(kit.Examples);
        var text = StyleKitSerializer.Write(kit);
        Assert.DoesNotContain("mermaid", text, StringComparison.Ordinal);
        Assert.DoesNotContain("vegaLite", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that colors are accepted in any case and are held in upper case.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_LowerCaseColors_AreNormalizedToUpperCase()
    {
        // Arrange
        var json = Edit(MinimalKitJson, ("palette>primary", "\"#abcdef\""));

        // Act
        var result = StyleKitSerializer.Parse(json, string.Empty);

        // Assert
        Assert.Equal("#ABCDEF", result.Kit!.Palette["primary"]);
        Assert.Contains("\"primary\": \"#ABCDEF\"", StyleKitSerializer.Write(result.Kit), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the folder reader lists the files of the examples folder at the top level only, sorted by name,
    ///     without hidden files and without a folder, and gives each its caption when the kit file has one.
    /// </summary>
    [Fact]
    public void StyleKitDocument_ReadFolder_ExamplesFolder_ListsTopLevelFilesSortedWithCaptions()
    {
        // Arrange: files in a deliberately unsorted creation order, a hidden file, and a sub-folder with a file
        var folder = MakeKitFolder("demo-kit", FullKitJson, "z-last.png", "card.svg", "b-middle.mmd");
        File.WriteAllText(Path.Combine(folder, "examples", ".hidden"), "x");
        Directory.CreateDirectory(Path.Combine(folder, "examples", "sub"));
        File.WriteAllText(Path.Combine(folder, "examples", "sub", "deep.svg"), "x");

        // Act
        var result = StyleKitSerializer.ReadFolder(folder);

        // Assert
        Assert.Equal(
            ["examples/b-middle.mmd", "examples/card.svg", "examples/z-last.png"],
            result.Kit!.Examples.Select(e => e.RelativePath));
        Assert.Equal("A title card.", result.Kit.Examples.Single(e => e.RelativePath == "examples/card.svg").Caption);
        Assert.All(result.Kit.Examples.Where(e => e.RelativePath != "examples/card.svg"), e => Assert.Null(e.Caption));
    }

    /// <summary>
    ///     Validates that a folder that does not exist, a folder with no kit file, and a kit file over the size limit are
    ///     each an error of the whole file and not an exception.
    /// </summary>
    [Fact]
    public void StyleKitDocument_ReadFolder_MissingFolderMissingFileOrOversizeFile_ReportsErrors()
    {
        // Arrange
        var missing = Path.Combine(_root, "nothing");
        var empty = Directory.CreateDirectory(Path.Combine(_root, "empty")).FullName;
        var big = Directory.CreateDirectory(Path.Combine(_root, "big")).FullName;
        File.WriteAllText(Path.Combine(big, "style.json"), new string(' ', StyleKitSerializer.MaxFileBytes + 1));

        // Act
        var results = new[] { missing, empty, big }.Select(StyleKitSerializer.ReadFolder).ToList();

        // Assert
        Assert.All(results, r => Assert.Null(r.Kit));
        Assert.Contains("does not exist", results[0].Issues.Single().Message, StringComparison.Ordinal);
        Assert.Contains("has no style.json file", results[1].Issues.Single().Message, StringComparison.Ordinal);
        Assert.Contains("larger than 256 KiB", results[2].Issues.Single().Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that each wrong value is reported as an error that names the property and says what to do, and that
    ///     no kit is returned.
    /// </summary>
    /// <param name="path">The property to change, with <c>&gt;</c> between the levels.</param>
    /// <param name="value">The JSON text to put there, or <see langword="null" /> to remove the property.</param>
    /// <param name="expected">The text of the issue: the path, a colon, and the message or the start of it.</param>
    [Theory]
    [InlineData("id", "\"Bad Id\"", "id: 'Bad Id' is not a valid id; use 1 to 48 lower-case letters, digits, and dashes")]
    [InlineData("id", "\"-start\"", "id: '-start' is not a valid id")]
    [InlineData("id", null, "id: is missing.")]
    [InlineData("id", "5", "id: must be text.")]
    [InlineData("name", "\"\"", "name: must not be empty.")]
    [InlineData("name", "\"123456789012345678901234567890123456789012345678901234567890X\"", "name: is 61 characters long; the limit is 60.")]
    [InlineData("palette>primary", "\"#12\"", "palette.primary: '#12' is not a color; use #RRGGBB.")]
    [InlineData("palette>primary", "\"red\"", "palette.primary: 'red' is not a color; use #RRGGBB.")]
    [InlineData("palette>primary", "\"#80FF0000\"", "palette.primary: '#80FF0000' is not a color; use #RRGGBB (colors are opaque, so #AARRGGBB is not accepted).")]
    [InlineData("palette>primary", "7", "palette.primary: must be a color text such as #RRGGBB.")]
    [InlineData("palette>text", null, "palette.text: is required; every kit needs a background, a text, and a primary color.")]
    [InlineData("palette>Bad Role", "\"#112233\"", "palette.Bad Role: is not a usable name")]
    [InlineData("palette", null, "palette: is missing; every kit needs a palette")]
    [InlineData("palette", "[]", "palette: must be an object of color names to #RRGGBB colors.")]
    [InlineData("mermaid", "{\"theme\":\"neon\"}", "mermaid.theme: 'neon' is not a Mermaid theme; use default, base, dark, forest, neutral.")]
    [InlineData("mermaid", "{}", "mermaid.theme: is missing.")]
    [InlineData("mermaid", "{\"theme\":\"base\",\"themeVariables\":{\"bad name\":\"x\"}}", "mermaid.themeVariables.bad name: is not a usable variable name")]
    [InlineData("vegaLite", "{\"config\":[1]}", "vegaLite.config: must be a JSON object")]
    [InlineData("vegaLite", "{}", "vegaLite.config: is missing")]
    [InlineData("stroke", "{\"width\":-1}", "stroke.width: is -1; use a number from 0 to 32.")]
    [InlineData("stroke", "{\"width\":33}", "stroke.width: is 33; use a number from 0 to 32.")]
    [InlineData("cornerRadius", "65", "cornerRadius: is 65; use a number from 0 to 64.")]
    [InlineData("spacing", "129", "spacing: is 129; use a number from 0 to 128.")]
    [InlineData("spacing", "\"wide\"", "spacing: must be a number.")]
    [InlineData("fonts", "{\"body\":\"\\\"\\\"\"}", "fonts.body: '\"\"' is not a valid font-family list")]
    [InlineData("guidance", "{\"do\":[\"\"]}", "guidance.do[0]: must not be empty.")]
    [InlineData("guidance", "{\"dont\":[3]}", "guidance.dont[0]: must be text.")]
    [InlineData("sizePresets>[0]>width", "0", "sizePresets[0].width: is 0; use a whole number of pixels from 1 to 8192.")]
    [InlineData("sizePresets>[0]>width", "8193", "sizePresets[0].width: is 8193; use a whole number of pixels from 1 to 8192.")]
    [InlineData("sizePresets>[0]>width", "12.5", "sizePresets[0].width: must be a whole number.")]
    [InlineData("sizePresets>[0]>dpi", "10", "sizePresets[0].dpi: is 10; use a number from 24 to 1200.")]
    [InlineData("sizePresets>[0]>dpi", "1201", "sizePresets[0].dpi: is 1201; use a number from 24 to 1200.")]
    [InlineData("sizePresets>[0]>name", "\"\"", "sizePresets[0].name: must not be empty.")]
    [InlineData("sizePresets>[0]>transparent", "\"yes\"", "sizePresets[0].transparent: must be true or false.")]
    [InlineData("sizePresets>[1]", "5", "sizePresets[1]: must be an object")]
    [InlineData("examples>examples/card.svg", "5", "examples[\"examples/card.svg\"]: must be a caption text.")]
    public void StyleKitDocument_Parse_InvalidValue_ReportsErrorWithPathAndText(string path, string? value, string expected)
    {
        // Arrange: the full kit with one value changed
        var json = Edit(FullKitJson, (path, value));

        // Act
        var result = StyleKitSerializer.Parse(json, string.Empty);

        // Assert: no kit, and one error that starts with the expected text
        Assert.Null(result.Kit);
        Assert.Contains(result.Errors.Select(i => i.ToString()), e => e.StartsWith(expected, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a preset with neither a width nor a height is an error of the preset, that one of the two is
    ///     enough, and that the same name twice, ignoring case, is an error of the second preset.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_PresetSizesAndNames_AreChecked()
    {
        // Arrange
        var neither = Edit(FullKitJson, ("sizePresets>[0]>width", null), ("sizePresets>[0]>height", null));
        var onlyHeight = Edit(FullKitJson, ("sizePresets>[0]>width", null));
        var duplicate = Edit(FullKitJson, ("sizePresets>[1]>name", "\"SLIDE\""));

        // Act
        var neitherResult = StyleKitSerializer.Parse(neither, string.Empty);
        var onlyHeightResult = StyleKitSerializer.Parse(onlyHeight, string.Empty);
        var duplicateResult = StyleKitSerializer.Parse(duplicate, string.Empty);

        // Assert
        Assert.Contains("sizePresets[0]: needs a width, a height, or both.", neitherResult.Errors.Select(i => i.ToString()));
        Assert.True(onlyHeightResult.Succeeded);
        Assert.Null(onlyHeightResult.Kit!.SizePresets[0].Width);
        Assert.Contains(
            "sizePresets[1].name: 'SLIDE' is used by another size preset; names must differ, ignoring case.",
            duplicateResult.Errors.Select(i => i.ToString()));
    }

    /// <summary>
    ///     Validates that every error of a file is reported, not only the first.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_SeveralErrors_AreAllReported()
    {
        // Arrange: three unrelated mistakes
        var json = Edit(FullKitJson, ("id", "\"X\""), ("palette>primary", "\"red\""), ("spacing", "500"));

        // Act
        var result = StyleKitSerializer.Parse(json, string.Empty);

        // Assert
        Assert.Equal(["id", "palette.primary", "spacing"], result.Errors.Select(i => i.Path));
    }

    /// <summary>
    ///     Validates the format version: missing, not a number, below 1, and above the version that is read are each an
    ///     error, and a newer version says it was made by a newer version.
    /// </summary>
    /// <param name="value">The JSON text of the version, or <see langword="null" /> to remove it.</param>
    /// <param name="expected">The start of the message.</param>
    [Theory]
    [InlineData(null, "formatVersion: is missing")]
    [InlineData("0", "formatVersion: is 0; the first format is 1")]
    [InlineData("2", "formatVersion: is 2: the kit was made by a newer version of CanvasPilot")]
    [InlineData("\"1\"", "formatVersion: must be the number 1.")]
    [InlineData("1.5", "formatVersion: must be the number 1.")]
    public void StyleKitDocument_Parse_FormatVersion_IsCheckedFirst(string? value, string expected)
    {
        // Arrange: a version that is wrong, and another mistake that must not be reported because the file is refused
        var json = Edit(FullKitJson, ("formatVersion", value), ("spacing", "500"));

        // Act
        var result = StyleKitSerializer.Parse(json, string.Empty);

        // Assert
        Assert.Null(result.Kit);
        var issue = Assert.Single(result.Issues);
        Assert.StartsWith(expected, issue.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that text that is not a JSON object, is not JSON at all, or nests too deep is one error of the whole
    ///     file, and that a file over the size limit is refused before it is parsed.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_NotAnObjectNotJsonTooDeepOrTooLarge_IsOneErrorOfTheFile()
    {
        // Arrange
        var deep = new string('[', 40) + new string(']', 40);
        var large = "{\"x\":\"" + new string('a', StyleKitSerializer.MaxFileBytes) + "\"}";

        // Act
        var array = StyleKitSerializer.Parse("[]", string.Empty);
        var notJson = StyleKitSerializer.Parse("{ not json", string.Empty);
        var empty = StyleKitSerializer.Parse(string.Empty, string.Empty);
        var tooDeep = StyleKitSerializer.Parse(deep, string.Empty);
        var tooLarge = StyleKitSerializer.Parse(large, string.Empty);

        // Assert
        Assert.All(new[] { array, notJson, empty, tooDeep, tooLarge }, r =>
        {
            Assert.Null(r.Kit);
            var issue = Assert.Single(r.Issues);
            Assert.Equal(string.Empty, issue.Path);
            Assert.True(issue.IsError);
        });
        Assert.Equal("The file must hold one JSON object with the properties of a kit.", array.Issues[0].Message);
        Assert.StartsWith("The file is not valid JSON", notJson.Issues[0].Message, StringComparison.Ordinal);
        Assert.Contains("nested deeper than 16 levels", tooDeep.Issues[0].Message, StringComparison.Ordinal);
        Assert.StartsWith("The file is larger than 256 KiB", tooLarge.Issues[0].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an example caption whose path could name a file outside the examples folder (a parent folder, a
    ///     root, a sub-folder, a backslash, or a character a file name cannot have) is an error.
    /// </summary>
    /// <param name="key">The path used as the key of the caption.</param>
    /// <param name="expectedPart">Part of the expected message.</param>
    [Theory]
    [InlineData("examples/../evil.png", "must not contain '.' or '..' segments")]
    [InlineData("../evil.png", "must not contain '.' or '..' segments")]
    [InlineData("/etc/evil.png", "must be relative to the kit folder")]
    [InlineData("C:\\evil.png", "must be relative to the kit folder")]
    [InlineData("examples\\evil.png", "must use forward slashes")]
    [InlineData("examples/sub/evil.png", "must name one file directly in the examples folder")]
    [InlineData("evil.png", "must name one file directly in the examples folder")]
    [InlineData("examples/", "must name one file directly in the examples folder")]
    [InlineData("examples/a|b.png", "holds a character that a file name cannot have")]
    public void StyleKitDocument_Parse_ExamplePathOutsideTheExamplesFolder_IsAnError(string key, string expectedPart)
    {
        // Arrange
        var json = Edit(FullKitJson, ("examples>" + key, "\"caption\""));

        // Act
        var result = StyleKitSerializer.Parse(json, string.Empty);

        // Assert
        Assert.Null(result.Kit);
        var issue = Assert.Single(result.Errors);
        Assert.Equal($"examples[\"{key}\"]", issue.Path);
        Assert.Contains(expectedPart, issue.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an unknown top-level property is a warning, that the kit is still read, and that the next write
    ///     drops the property.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_UnknownProperty_IsAWarningAndIsDroppedOnWrite()
    {
        // Arrange
        var json = Edit(MinimalKitJson, ("futureThing", "{\"a\":1}"));

        // Act
        var result = StyleKitSerializer.Parse(json, string.Empty);

        // Assert
        Assert.True(result.Succeeded);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("futureThing", warning.Path);
        Assert.False(warning.IsError);
        Assert.DoesNotContain("futureThing", StyleKitSerializer.Write(result.Kit!), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a font list whose first family is a generic keyword, a common alias, or a bundled Liberation
    ///     family is accepted without a message.
    /// </summary>
    /// <param name="fonts">The font list.</param>
    [Theory]
    [InlineData("sans-serif")]
    [InlineData("monospace")]
    [InlineData("Segoe UI, sans-serif")]
    [InlineData("\"Segoe UI\", Arial, sans-serif")]
    [InlineData("Consolas")]
    [InlineData("Arial")]
    [InlineData("Liberation Mono")]
    [InlineData("")]
    public void StyleKitDocument_Parse_BundledOrCommonFonts_HaveNoMessage(string fonts)
    {
        // Arrange
        var json = Edit(FullKitJson, ("fonts>body", JsonValue.Create(fonts).ToJsonString()));

        // Act
        var result = StyleKitSerializer.Parse(json, string.Empty);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(result.Issues);
    }

    /// <summary>
    ///     Validates that a font list whose first family is not bundled is a warning that says which formats use the
    ///     installed font and which always use the bundled fonts, and that the kit is still read.
    /// </summary>
    /// <param name="fonts">The font list.</param>
    /// <param name="family">The family that the warning names.</param>
    [Theory]
    [InlineData("Inter, sans-serif", "Inter")]
    [InlineData("\"Comic Sans MS\"", "Comic Sans MS")]
    public void StyleKitDocument_Parse_FontThatIsNotBundled_IsAWarning(string fonts, string family)
    {
        // Arrange
        var json = Edit(FullKitJson, ("fonts>heading", JsonValue.Create(fonts).ToJsonString()));

        // Act
        var result = StyleKitSerializer.Parse(json, string.Empty);

        // Assert
        Assert.True(result.Succeeded);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("fonts.heading", warning.Path);
        Assert.Equal(
            $"'{family}' is drawn with the installed font only by SVG and OpenSCAD on machines that have it; " +
            "Mermaid, PlantUML, and Vega-Lite text always uses the bundled Liberation fonts.",
            warning.Message);
    }

    /// <summary>
    ///     Validates the warnings about the folder: a caption for a file that is not in the examples folder, and a folder
    ///     whose name differs from the id.
    /// </summary>
    [Fact]
    public void StyleKitDocument_ReadFolder_CaptionWithoutFileAndFolderNameDifferentFromId_AreWarnings()
    {
        // Arrange: no example file, and a folder with another name than the id
        var folder = MakeKitFolder("other-name", FullKitJson);

        // Act
        var result = StyleKitSerializer.ReadFolder(folder);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains(result.Warnings, w => w.Path == "examples[\"examples/card.svg\"]" && w.Message.StartsWith("has no file in the examples folder", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Path == "id" && w.Message.Contains("'other-name'", StringComparison.Ordinal));
        Assert.Empty(result.Kit!.Examples);
        Assert.DoesNotContain("card.svg", StyleKitSerializer.Write(result.Kit), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the limits on the size of a kit: the number of palette entries, theme variables, presets, guidance
    ///     entries, and captions, and the size of the Vega-Lite configuration, are each an error when exceeded and are
    ///     accepted at the limit.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_Limits_AreErrorsWhenExceededAndAcceptedAtTheLimit()
    {
        // Arrange: files with the largest allowed number of each list, and with one more
        var atLimit = Edit(
            MinimalKitJson,
            ("palette", Object(24, i => (i < 3 ? new[] { "background", "text", "primary" }[i] : $"extra{i}", "\"#112233\""))),
            ("mermaid", "{\"theme\":\"base\",\"themeVariables\":" + Object(40, i => ($"v{i}", "\"x\"")) + "}"),
            ("sizePresets", Array(16, i => $"{{\"name\":\"P{i}\",\"width\":10}}")),
            ("guidance", "{\"do\":" + Array(10, _ => "\"a\"") + ",\"dont\":" + Array(10, _ => "\"b\"") + "}"),
            ("examples", Object(24, i => ($"examples/f{i}.svg", "\"c\""))));
        var overLimit = Edit(
            MinimalKitJson,
            ("palette", Object(25, i => (i < 3 ? new[] { "background", "text", "primary" }[i] : $"extra{i}", "\"#112233\""))),
            ("mermaid", "{\"theme\":\"base\",\"themeVariables\":" + Object(41, i => ($"v{i}", "\"x\"")) + "}"),
            ("sizePresets", Array(17, i => $"{{\"name\":\"P{i}\",\"width\":10}}")),
            ("guidance", "{\"do\":" + Array(11, _ => "\"a\"") + ",\"dont\":" + Array(11, _ => "\"b\"") + "}"),
            ("examples", Object(25, i => ($"examples/f{i}.svg", "\"c\""))));

        // Act
        var accepted = StyleKitSerializer.Parse(atLimit, string.Empty);
        var refused = StyleKitSerializer.Parse(overLimit, string.Empty);

        // Assert
        Assert.True(accepted.Succeeded, string.Join("; ", accepted.Issues));
        Assert.Equal(
            [
                "examples: has 25 entries; the limit is 24.",
                "palette: has 25 entries; the limit is 24.",
                "mermaid.themeVariables: has 41 entries; the limit is 40.",
                "guidance.do: has 11 entries; the limit is 10.",
                "guidance.dont: has 11 entries; the limit is 10.",
                "sizePresets: has 17 entries; the limit is 16."
            ],
            refused.Errors.Select(i => i.ToString()));
    }

    /// <summary>
    ///     Validates the limits of text lengths: the description, the notes, a guidance entry, a preset name, a caption,
    ///     and a theme variable are each an error one character over the limit and accepted at the limit.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_TextLengths_AreErrorsOneCharacterOverTheLimit()
    {
        // Arrange
        string Text(int length) => "\"" + new string('a', length) + "\"";
        var atLimit = Edit(
            FullKitJson,
            ("description", Text(300)),
            ("guidance", "{\"notes\":" + Text(1500) + ",\"do\":[" + Text(160) + "]}"),
            ("sizePresets>[0]>name", Text(40)),
            ("examples>examples/card.svg", Text(160)),
            ("mermaid>themeVariables>primaryColor", Text(60)),
            ("fonts>body", Text(120)));
        var over = Edit(
            FullKitJson,
            ("description", Text(301)),
            ("guidance", "{\"notes\":" + Text(1501) + ",\"do\":[" + Text(161) + "]}"),
            ("sizePresets>[0]>name", Text(41)),
            ("examples>examples/card.svg", Text(161)),
            ("mermaid>themeVariables>primaryColor", Text(61)),
            ("fonts>body", Text(121)));
        var folder = MakeKitFolder("demo-kit", over, "card.svg");

        // Act
        var accepted = StyleKitSerializer.Parse(atLimit, string.Empty);
        var refused = StyleKitSerializer.ReadFolder(folder);

        // Assert: the font at the limit has a warning (the family 'aaa...' is not bundled) and no error
        Assert.True(accepted.Succeeded, string.Join("; ", accepted.Errors));
        Assert.Equal(
            [
                "description: is 301 characters long; the limit is 300.",
                "fonts.body: is 121 characters long; the limit is 120.",
                "mermaid.themeVariables.primaryColor: is 61 characters long; the limit is 60.",
                "guidance.notes: is 1501 characters long; the limit is 1500.",
                "guidance.do[0]: is 161 characters long; the limit is 160.",
                "sizePresets[0].name: is 41 characters long; the limit is 40.",
                "examples[\"examples/card.svg\"]: is 161 characters long; the limit is 160."
            ],
            refused.Errors.Select(i => i.ToString()));
    }

    /// <summary>
    ///     Validates the limits on the Vega-Lite configuration: a configuration over 8 KiB of compact JSON is an error,
    ///     white space of the file does not count, and a configuration that is not an object is an error of the file.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Parse_VegaLiteConfigSize_IsMeasuredOnCompactJson()
    {
        // Arrange: a compact configuration of exactly the limit, one byte more, and the same as indented text
        string Config(int bytes) => "{\"k\":\"" + new string('a', bytes - 8) + "\"}";
        var atLimit = Edit(MinimalKitJson, ("vegaLite", "{\"config\":" + Config(StyleKitSerializer.MaxVegaLiteConfigBytes) + "}"));
        var over = Edit(MinimalKitJson, ("vegaLite", "{\"config\":" + Config(StyleKitSerializer.MaxVegaLiteConfigBytes + 1) + "}"));
        var spaced = Edit(MinimalKitJson, ("vegaLite", "{\"config\": {\n  \"k\" :  \"v\"\n }}"));

        // Act
        var acceptedResult = StyleKitSerializer.Parse(atLimit, string.Empty);
        var overResult = StyleKitSerializer.Parse(over, string.Empty);
        var spacedResult = StyleKitSerializer.Parse(spaced, string.Empty);

        // Assert
        Assert.True(acceptedResult.Succeeded, string.Join("; ", acceptedResult.Issues));
        Assert.Contains("vegaLite.config: is larger than 8 KiB as compact JSON, which is the limit.", overResult.Errors.Select(i => i.ToString()));
        Assert.Equal("""{"k":"v"}""", spacedResult.Kit!.VegaLiteConfigJson);
    }

    /// <summary>
    ///     Validates the limits of the examples folder: more files than a kit may have are ignored with a warning, and a
    ///     file over the size limit is ignored with a warning.
    /// </summary>
    [Fact]
    public void StyleKitDocument_ReadFolder_TooManyOrTooLargeExampleFiles_AreIgnoredWithWarnings()
    {
        // Arrange: 26 small files and one file of one byte over the limit, which is a sparse file
        var names = Enumerable.Range(0, 26).Select(i => $"f{i:00}.svg").ToArray();
        var folder = MakeKitFolder("demo-kit", MinimalKitJson.Replace("tiny", "demo-kit", StringComparison.Ordinal).Replace("Tiny", "Demo", StringComparison.Ordinal), names);
        using (var big = new FileStream(Path.Combine(folder, "examples", "a-big.png"), FileMode.Create))
        {
            big.SetLength(StyleKitSerializer.MaxExampleBytes + 1);
        }

        // Act
        var result = StyleKitSerializer.ReadFolder(folder);

        // Assert: the first 24 by name that are small enough are examples, the rest are warnings
        Assert.True(result.Succeeded);
        Assert.Equal(24, result.Kit!.Examples.Count);
        Assert.Equal("examples/f00.svg", result.Kit.Examples[0].RelativePath);
        Assert.DoesNotContain(result.Kit.Examples, e => e.RelativePath == "examples/a-big.png");
        Assert.Contains(result.Warnings, w => w.Path == "examples[\"examples/a-big.png\"]" && w.Message.StartsWith("is larger than 16 MiB", StringComparison.Ordinal));
        Assert.Equal(2, result.Warnings.Count(w => w.Message.Contains("beyond the 24 examples", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Validates that a kit built in memory is checked with the same rules as a file: a good kit has no issue, and a
    ///     bad one has the same messages that the reader gives.
    /// </summary>
    [Fact]
    public void StyleKitDocument_Validate_KitInMemory_GivesTheSameMessagesAsTheReader()
    {
        // Arrange
        var good = StyleKitSerializer.Parse(FullKitJson, string.Empty).Kit!;
        var bad = good with
        {
            Id = "Bad",
            Palette = new Dictionary<string, string> { ["background"] = "white", ["text"] = "#000000" },
            CornerRadius = 100,
            VegaLiteConfigJson = "[1]",
            SizePresets = [new StyleSizePreset("Empty", null, null, null, false)]
        };

        // Act
        var goodIssues = StyleKitSerializer.Validate(good);
        var badIssues = StyleKitSerializer.Validate(bad).Select(i => i.ToString()).ToList();

        // Assert
        Assert.Empty(goodIssues);
        Assert.Contains(badIssues, i => i.StartsWith("id: 'Bad' is not a valid id", StringComparison.Ordinal));
        Assert.Contains("palette.background: 'white' is not a color; use #RRGGBB.", badIssues);
        Assert.Contains("palette.primary: is required; every kit needs a background, a text, and a primary color.", badIssues);
        Assert.Contains("cornerRadius: is 100; use a number from 0 to 64.", badIssues);
        Assert.Contains("vegaLite.config: must be a JSON object, like the config of a Vega-Lite specification.", badIssues);
        Assert.Contains("sizePresets[0]: needs a width, a height, or both.", badIssues);
    }

    /// <summary>
    ///     Validates that the identifier rule accepts lower-case kebab ids of 1 to 48 characters and refuses everything
    ///     else, including a trailing line feed.
    /// </summary>
    /// <param name="id">The text to test.</param>
    /// <param name="expected">Whether it is an id.</param>
    [Theory]
    [InlineData("clean", true)]
    [InlineData("slides-dark", true)]
    [InlineData("9lives", true)]
    [InlineData("a", true)]
    [InlineData("-lead", false)]
    [InlineData("Upper", false)]
    [InlineData("under_score", false)]
    [InlineData("with space", false)]
    [InlineData("trailing\n", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void StyleKitDocument_IsValidId_ChecksThePattern(string? id, bool expected)
    {
        // Act / Assert
        Assert.Equal(expected, StyleKitSerializer.IsValidId(id));
    }

    /// <summary>
    ///     Validates that an id of 48 characters is accepted and one of 49 is refused.
    /// </summary>
    [Fact]
    public void StyleKitDocument_IsValidId_LengthLimit_Is48Characters()
    {
        // Act / Assert
        Assert.True(StyleKitSerializer.IsValidId(new string('a', 48)));
        Assert.False(StyleKitSerializer.IsValidId(new string('a', 49)));
    }

    /// <summary>
    ///     Validates that the readers and the writer refuse a missing argument.
    /// </summary>
    [Fact]
    public void StyleKitDocument_PublicMembers_NullArguments_ThrowArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => StyleKitSerializer.Parse(null!, string.Empty));
        Assert.Throws<ArgumentNullException>(() => StyleKitSerializer.Parse("{}", null!));
        Assert.Throws<ArgumentNullException>(() => StyleKitSerializer.ReadFolder(null!));
        Assert.Throws<ArgumentNullException>(() => StyleKitSerializer.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => StyleKitSerializer.Write(null!));
        Assert.Throws<ArgumentNullException>(() => StyleColors.Normalize(null!));
    }

    /// <summary>
    ///     Validates that an issue is written as its path, a colon, and its message, and as the message alone for the
    ///     whole file.
    /// </summary>
    [Fact]
    public void StyleKitDocument_StyleIssue_ToString_WritesPathAndMessage()
    {
        // Act / Assert
        Assert.Equal("palette.primary: is bad", new StyleIssue("palette.primary", "is bad", true).ToString());
        Assert.Equal("The file is bad.", new StyleIssue(string.Empty, "The file is bad.", true).ToString());
    }

    /// <summary>
    ///     Validates the color helpers: reading, normalizing, refusing other forms, and the contrast of the WCAG formula.
    /// </summary>
    [Fact]
    public void StyleKitDocument_StyleColors_ParseNormalizeAndContrast_FollowTheDefinitions()
    {
        // Act
        var parsed = StyleColors.TryParse("#0a1B2c", out var red, out var green, out var blue);
        var normalized = StyleColors.Normalize("#0a1B2c");

        // Assert: reading and normalizing
        Assert.True(parsed);
        Assert.Equal((10, 27, 44), (red, green, blue));
        Assert.Equal("#0A1B2C", normalized);
        foreach (var bad in new[] { null, string.Empty, "#12", "123456", "#12345G", "#1234567", "#80112233", "red", " #112233", "#+12233" })
        {
            Assert.False(StyleColors.TryParse(bad, out _, out _, out _), bad);
            Assert.False(StyleColors.TryNormalize(bad, out var text), bad);
            Assert.Equal(string.Empty, text);
        }

        Assert.Throws<FormatException>(() => StyleColors.Normalize("red"));

        // Assert: contrast is 21 for black and white, 1 for equal colors, symmetric, and a known mid value
        Assert.Equal(21.0, StyleColors.Contrast("#000000", "#FFFFFF"), 6);
        Assert.Equal(1.0, StyleColors.Contrast("#2563EB", "#2563EB"), 9);
        Assert.Equal(StyleColors.Contrast("#0F172A", "#FFFFFF"), StyleColors.Contrast("#FFFFFF", "#0F172A"), 12);
        Assert.Equal(4.48, StyleColors.Contrast("#777777", "#FFFFFF"), 2);
        Assert.Throws<FormatException>(() => StyleColors.Contrast("#000000", "nope"));
    }

    /// <summary>
    ///     Makes a kit folder with a style file and empty example files.
    /// </summary>
    /// <param name="folderName">The name of the folder.</param>
    /// <param name="json">The text of the style file.</param>
    /// <param name="examples">The names of the example files to create.</param>
    /// <returns>The full path of the kit folder.</returns>
    private string MakeKitFolder(string folderName, string json, params string[] examples)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, folderName)).FullName;
        File.WriteAllText(Path.Combine(folder, "style.json"), json);
        var examplesFolder = Directory.CreateDirectory(Path.Combine(folder, "examples")).FullName;
        foreach (var example in examples)
        {
            File.WriteAllText(Path.Combine(examplesFolder, example), "x");
        }

        return folder;
    }

    /// <summary>
    ///     Changes a JSON text: each edit sets the value at a path, or removes it when the value is
    ///     <see langword="null" />. The levels of the path are separated by <c>&gt;</c>, and a level may be a property or
    ///     <c>[n]</c> for an element of a list.
    /// </summary>
    /// <param name="json">The text to change.</param>
    /// <param name="edits">The edits, in order.</param>
    /// <returns>The changed text.</returns>
    private static string Edit(string json, params (string Path, string? Value)[] edits)
    {
        var root = JsonNode.Parse(json)!;
        foreach (var (path, value) in edits)
        {
            var levels = path.Split('>');
            var node = root;
            foreach (var level in levels[..^1])
            {
                node = Step(node, level)!;
            }

            var last = levels[^1];
            var newValue = value is null ? null : JsonNode.Parse(value);
            if (last.StartsWith('['))
            {
                var array = node.AsArray();
                var index = int.Parse(last.Trim('[', ']'), System.Globalization.CultureInfo.InvariantCulture);
                if (value is null)
                {
                    array.RemoveAt(index);
                }
                else
                {
                    array[index] = newValue;
                }
            }
            else if (value is null)
            {
                node.AsObject().Remove(last);
            }
            else
            {
                node.AsObject()[last] = newValue;
            }
        }

        return root.ToJsonString();
    }

    /// <summary>
    ///     Steps into a property or a list element.
    /// </summary>
    /// <param name="node">The node to step from.</param>
    /// <param name="level">A property name or <c>[n]</c>.</param>
    /// <returns>The child node.</returns>
    private static JsonNode? Step(JsonNode node, string level) =>
        level.StartsWith('[')
            ? node.AsArray()[int.Parse(level.Trim('[', ']'), System.Globalization.CultureInfo.InvariantCulture)]
            : node.AsObject()[level];

    /// <summary>
    ///     Writes a JSON object text with a given number of properties.
    /// </summary>
    /// <param name="count">The number of properties.</param>
    /// <param name="property">Makes the name and the JSON value of the property at an index.</param>
    /// <returns>The JSON text.</returns>
    private static string Object(int count, Func<int, (string Name, string Value)> property) =>
        "{" + string.Join(",", Enumerable.Range(0, count).Select(i =>
        {
            var (name, value) = property(i);
            return $"\"{name}\":{value}";
        })) + "}";

    /// <summary>
    ///     Writes a JSON array text with a given number of elements.
    /// </summary>
    /// <param name="count">The number of elements.</param>
    /// <param name="element">Makes the JSON text of the element at an index.</param>
    /// <returns>The JSON text.</returns>
    private static string Array(int count, Func<int, string> element) =>
        "[" + string.Join(",", Enumerable.Range(0, count).Select(element)) + "]";
}

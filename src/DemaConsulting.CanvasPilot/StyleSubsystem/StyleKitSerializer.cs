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
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Reads, checks, and writes the <c>style.json</c> file of a style kit, and reads the folder of a kit.
/// </summary>
/// <remarks>
///     <para>
///         The file is read by hand over a JSON document, not deserialized, so that every message can name the property
///         that is wrong in the words a person uses to edit the file (<c>palette.primary</c>, <c>sizePresets[1].width</c>),
///         and so that every error is reported, not only the first. The file is also read from places that are not
///         trusted (a shared folder, a zip file), so its size and nesting are limited before anything is built from it.
///     </para>
///     <para>
///         Writing is canonical: fixed property order, two-space indentation, line feeds, and normalized colors, so a kit
///         that is read and written again is byte for byte the same file and a change of a kit is a small difference in a
///         review. Unknown top-level properties are warnings and are dropped by the next write. A
///         <c>formatVersion</c> above the one this version reads is an error, because the file may mean something that
///         this version cannot keep. Stateless and safe to call from any thread.
///     </para>
/// </remarks>
public static partial class StyleKitSerializer
{
    /// <summary>
    ///     The format version that this version of the application reads and writes.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    ///     The name of the file in a kit folder that holds the kit.
    /// </summary>
    public const string FileName = "style.json";

    /// <summary>
    ///     The name of the folder in a kit folder that holds the example images.
    /// </summary>
    public const string ExamplesFolderName = "examples";

    /// <summary>
    ///     The largest <c>style.json</c> that is read, in bytes.
    /// </summary>
    public const int MaxFileBytes = 256 * 1024;

    /// <summary>
    ///     The deepest nesting of JSON that is read.
    /// </summary>
    public const int MaxJsonDepth = 16;

    /// <summary>
    ///     The most entries a palette may have, the standard roles and the extra colors together.
    /// </summary>
    public const int MaxPaletteEntries = 24;

    /// <summary>
    ///     The most theme variables the Mermaid part may have.
    /// </summary>
    public const int MaxThemeVariables = 40;

    /// <summary>
    ///     The most size presets a kit may have.
    /// </summary>
    public const int MaxSizePresets = 16;

    /// <summary>
    ///     The most example images a kit may have.
    /// </summary>
    public const int MaxExamples = 24;

    /// <summary>
    ///     The most entries each of the lists of things to do and to avoid may have.
    /// </summary>
    public const int MaxGuidanceItems = 10;

    /// <summary>
    ///     The largest example file that is accepted, in bytes.
    /// </summary>
    public const long MaxExampleBytes = 16L * 1024 * 1024;

    /// <summary>
    ///     The largest size of the Vega-Lite configuration of a kit as compact JSON, in bytes.
    /// </summary>
    public const int MaxVegaLiteConfigBytes = 8 * 1024;

    /// <summary>
    ///     The longest name of a kit, in characters.
    /// </summary>
    public const int MaxNameLength = 60;

    /// <summary>
    ///     The longest description of a kit, in characters.
    /// </summary>
    public const int MaxDescriptionLength = 300;

    /// <summary>
    ///     The longest font list, in characters.
    /// </summary>
    public const int MaxFontLength = 120;

    /// <summary>
    ///     The longest notes text of the guidance, in characters.
    /// </summary>
    public const int MaxNotesLength = 1500;

    /// <summary>
    ///     The longest entry of a list of things to do or to avoid, in characters.
    /// </summary>
    public const int MaxGuidanceItemLength = 160;

    /// <summary>
    ///     The longest name of a size preset, in characters.
    /// </summary>
    public const int MaxPresetNameLength = 40;

    /// <summary>
    ///     The longest caption of an example, in characters.
    /// </summary>
    public const int MaxCaptionLength = 160;

    /// <summary>
    ///     The longest value of a Mermaid theme variable, in characters.
    /// </summary>
    public const int MaxThemeVariableLength = 60;

    /// <summary>
    ///     The largest width or height of a size preset, in pixels; it is the largest surface that CanvasNet draws.
    /// </summary>
    public const int MaxPresetPixels = 8192;

    /// <summary>
    ///     The characters, besides the control characters and the slashes, that no file name of a kit may hold: those that
    ///     a Windows file name cannot have, so a kit can be used on every platform.
    /// </summary>
    internal const string NonPortableNameCharacters = ":*?\"<>|";

    /// <summary>
    ///     The palette roles that every kit must have.
    /// </summary>
    public static IReadOnlyList<string> RequiredRoles { get; } = ["background", "text", "primary"];

    /// <summary>
    ///     The Mermaid themes that a kit may name.
    /// </summary>
    public static IReadOnlyList<string> MermaidThemes { get; } = ["default", "base", "dark", "forest", "neutral"];

    /// <summary>
    ///     The options of the writer: indented, line feeds, and characters that need no escape left as they are.
    /// </summary>
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///     Tells whether a text can be the identifier of a kit.
    /// </summary>
    /// <param name="id">The text to test; may be <see langword="null" />.</param>
    /// <returns><see langword="true" /> for 1 to 48 lower-case letters, digits, and dashes that start with a letter or a digit.</returns>
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// <summary>
    ///     Reads a kit from the text of a <c>style.json</c> file.
    /// </summary>
    /// <remarks>
    ///     Never throws for a bad file; every problem is an issue in the result. The folder is used to find the example
    ///     files and to warn when its name differs from the identifier of the kit; with an empty folder no file is looked
    ///     up and each caption of the file becomes an example without a full path.
    /// </remarks>
    /// <param name="json">The text of the file. Must not be <see langword="null" />; at most <see cref="MaxFileBytes" /> bytes.</param>
    /// <param name="kitFolder">The folder of the kit, or an empty string when the kit has no folder.</param>
    /// <returns>The kit, or no kit and the errors, with the warnings in both cases.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public static StyleKitParseResult Parse(string json, string kitFolder)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(kitFolder);

        // Refuse a text that is over the limit before it is parsed, so that a huge file costs nothing
        var reader = new StyleKitReader();
        if (Encoding.UTF8.GetByteCount(json) > MaxFileBytes)
        {
            reader.Fail(string.Empty, $"The file is larger than {MaxFileBytes / 1024} KiB, which is the limit of a kit file.");
            return new StyleKitParseResult(null, reader.Issues);
        }

        // Parse with a depth limit; a document that is not JSON or nests too deep is one error for the whole file
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
        }
        catch (JsonException exception)
        {
            reader.Fail(string.Empty, $"The file is not valid JSON, or is nested deeper than {MaxJsonDepth} levels: {exception.Message}");
            return new StyleKitParseResult(null, reader.Issues);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                reader.Fail(string.Empty, "The file must hold one JSON object with the properties of a kit.");
                return new StyleKitParseResult(null, reader.Issues);
            }

            // The version comes first: a file of another version may mean something else, so nothing else is read
            if (!reader.CheckVersion(root))
            {
                return new StyleKitParseResult(null, reader.Issues);
            }

            reader.WarnAboutUnknownProperties(root);
            var kit = reader.ReadKit(root, kitFolder);

            // Check the values of what was read, and drop a message about a value that could not be read at all
            reader.Issues.AddRange(ValidateKit(kit).Where(issue => !reader.Covers(issue.Path)));

            if (kitFolder.Length > 0)
            {
                reader.WarnWhenFolderDiffersFromId(kit.Id, kitFolder);
            }

            var failed = reader.Issues.Any(i => i.IsError);
            return new StyleKitParseResult(failed ? null : kit, reader.Issues);
        }
    }

    /// <summary>
    ///     Reads a kit from its folder: the <c>style.json</c> file and the files of the <c>examples</c> folder.
    /// </summary>
    /// <remarks>
    ///     Never throws for a folder that is missing, a file that is missing or too large, or a file that cannot be read;
    ///     each is an error in the result. The size of the file is checked before it is read.
    /// </remarks>
    /// <param name="kitFolder">The folder of the kit. Must not be <see langword="null" />.</param>
    /// <returns>The kit, or no kit and the errors, with the warnings in both cases.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kitFolder" /> is <see langword="null" />.</exception>
    public static StyleKitParseResult ReadFolder(string kitFolder)
    {
        ArgumentNullException.ThrowIfNull(kitFolder);

        // A kit is a folder with a style.json; say which of the two is missing
        var folder = Path.GetFullPath(kitFolder);
        if (!Directory.Exists(folder))
        {
            return Failure($"The kit folder '{folder}' does not exist.");
        }

        var file = Path.Combine(folder, FileName);
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                return Failure($"The kit folder '{folder}' has no {FileName} file.");
            }

            if (info.Length > MaxFileBytes)
            {
                return Failure($"The file {FileName} is larger than {MaxFileBytes / 1024} KiB, which is the limit of a kit file.");
            }

            return Parse(File.ReadAllText(file, Encoding.UTF8), folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure($"The file {FileName} could not be read: {exception.Message}");
        }
    }

    /// <summary>
    ///     Checks a kit that was built in memory with the same rules that apply to a file.
    /// </summary>
    /// <remarks>
    ///     The editor uses this to show the same messages as the reader and to refuse to save an invalid kit. A kit that
    ///     has an error must not be written. The warnings tell what will not work as expected but do not stop a kit.
    /// </remarks>
    /// <param name="kit">The kit to check. Must not be <see langword="null" />.</param>
    /// <returns>Every error and warning, in the order the properties are written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kit" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<StyleIssue> Validate(StyleKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        return ValidateKit(kit);
    }

    /// <summary>
    ///     Writes a kit as the canonical text of a <c>style.json</c> file.
    /// </summary>
    /// <remarks>
    ///     The kit is not checked here; call <see cref="Validate" /> first and do not write a kit that has an error. The
    ///     text ends with a line feed. Writing the kit that was read from a file gives the same text again, so long as the
    ///     file was canonical.
    /// </remarks>
    /// <param name="kit">The kit to write. Must not be <see langword="null" />.</param>
    /// <returns>The text of the file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kit" /> is <see langword="null" />.</exception>
    public static string Write(StyleKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", CurrentFormatVersion);
            writer.WriteString("id", kit.Id);
            writer.WriteString("name", kit.Name.Trim());
            writer.WriteString("description", kit.Description.Trim());

            // Palette in the order of the kit, with every color in its normal form
            writer.WriteStartObject("palette");
            foreach (var (role, color) in kit.Palette)
            {
                writer.WriteString(role, StyleColors.TryNormalize(color, out var normalized) ? normalized : color);
            }

            writer.WriteEndObject();

            writer.WriteStartObject("fonts");
            writer.WriteString("heading", kit.Fonts.Heading.Trim());
            writer.WriteString("body", kit.Fonts.Body.Trim());
            writer.WriteString("monospace", kit.Fonts.Monospace.Trim());
            writer.WriteEndObject();

            writer.WriteStartObject("stroke");
            writer.WriteNumber("width", kit.StrokeWidth);
            writer.WriteEndObject();

            writer.WriteNumber("cornerRadius", kit.CornerRadius);
            writer.WriteNumber("spacing", kit.Spacing);

            // The parts that a kit may leave out are left out
            if (kit.Mermaid is not null)
            {
                writer.WriteStartObject("mermaid");
                writer.WriteString("theme", kit.Mermaid.Theme);
                writer.WriteStartObject("themeVariables");
                foreach (var (name, value) in kit.Mermaid.ThemeVariables)
                {
                    writer.WriteString(name, value);
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            if (kit.VegaLiteConfigJson is not null)
            {
                using var config = JsonDocument.Parse(kit.VegaLiteConfigJson);
                writer.WriteStartObject("vegaLite");
                writer.WritePropertyName("config");
                config.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }

            writer.WriteStartObject("guidance");
            writer.WriteString("notes", kit.Guidance.Notes.Trim());
            WriteTextArray(writer, "do", kit.Guidance.Do);
            WriteTextArray(writer, "dont", kit.Guidance.Dont);
            writer.WriteEndObject();

            writer.WriteStartArray("sizePresets");
            foreach (var preset in kit.SizePresets)
            {
                writer.WriteStartObject();
                writer.WriteString("name", preset.Name.Trim());
                if (preset.Width is { } width)
                {
                    writer.WriteNumber("width", width);
                }

                if (preset.Height is { } height)
                {
                    writer.WriteNumber("height", height);
                }

                if (preset.Dpi is { } dpi)
                {
                    writer.WriteNumber("dpi", dpi);
                }

                writer.WriteBoolean("transparent", preset.Transparent);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            // Only captions are stored; the files of the examples folder are the examples
            writer.WriteStartObject("examples");
            foreach (var example in kit.Examples.Where(e => !string.IsNullOrWhiteSpace(e.Caption)))
            {
                writer.WriteString(example.RelativePath, example.Caption!.Trim());
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>
    ///     Writes a named array of texts.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="name">The property name.</param>
    /// <param name="values">The texts, in order.</param>
    private static void WriteTextArray(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value.Trim());
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Makes the result of a read that failed for a reason that is not in the file text.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    /// <returns>A result with no kit and that one error.</returns>
    private static StyleKitParseResult Failure(string message) =>
        new(null, [new StyleIssue(string.Empty, message, true)]);

    /// <summary>
    ///     Writes the compact JSON text of an element, with the same escaping as the files of a kit.
    /// </summary>
    /// <param name="element">The element to write.</param>
    /// <returns>The JSON text without white space.</returns>
    internal static string Compact(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    ///     Checks every value of a kit, whether it came from a file or from the editor.
    /// </summary>
    /// <param name="kit">The kit to check.</param>
    /// <returns>Every error and warning, in the order the properties are written.</returns>
    private static List<StyleIssue> ValidateKit(StyleKit kit)
    {
        var issues = new List<StyleIssue>();

        // Identity
        if (!IsValidId(kit.Id))
        {
            issues.Add(Error("id", $"'{kit.Id}' is not a valid id; use 1 to 48 lower-case letters, digits, and dashes, starting with a letter or a digit."));
        }

        CheckLength(issues, "name", kit.Name.Trim(), 1, MaxNameLength);
        CheckLength(issues, "description", kit.Description.Trim(), 0, MaxDescriptionLength);

        // The colors, then the fonts, numbers, and the optional parts
        ValidatePalette(kit.Palette, issues);
        CheckFont(issues, "fonts.heading", kit.Fonts.Heading);
        CheckFont(issues, "fonts.body", kit.Fonts.Body);
        CheckFont(issues, "fonts.monospace", kit.Fonts.Monospace);
        CheckRange(issues, "stroke.width", kit.StrokeWidth, 0, 32);
        CheckRange(issues, "cornerRadius", kit.CornerRadius, 0, 64);
        CheckRange(issues, "spacing", kit.Spacing, 0, 128);
        ValidateMermaid(kit.Mermaid, issues);
        ValidateVegaLite(kit.VegaLiteConfigJson, issues);
        ValidateGuidance(kit.Guidance, issues);
        ValidatePresets(kit.SizePresets, issues);
        ValidateExamples(kit.Examples, issues);
        return issues;
    }

    /// <summary>
    ///     Checks the palette: the number of entries, the names, the colors, and the required roles.
    /// </summary>
    /// <param name="palette">The palette to check.</param>
    /// <param name="issues">The list that the problems are added to.</param>
    private static void ValidatePalette(IReadOnlyDictionary<string, string> palette, List<StyleIssue> issues)
    {
        if (palette.Count > MaxPaletteEntries)
        {
            issues.Add(Error("palette", $"has {palette.Count} entries; the limit is {MaxPaletteEntries}."));
        }

        foreach (var (role, color) in palette)
        {
            if (!RolePattern().IsMatch(role))
            {
                issues.Add(Error($"palette.{role}", "is not a usable name; start with a lower-case letter and use at most 32 letters, digits, and dashes."));
            }

            if (!StyleColors.TryParse(color, out _, out _, out _))
            {
                // A color with an alpha channel gets the reason, because that is the usual mistake
                var reason = color is { Length: 9 } && color[0] == '#'
                    ? " (colors are opaque, so #AARRGGBB is not accepted)"
                    : string.Empty;
                issues.Add(Error($"palette.{role}", $"'{color}' is not a color; use #RRGGBB{reason}."));
            }
        }

        foreach (var required in RequiredRoles.Where(role => !palette.ContainsKey(role)))
        {
            issues.Add(Error($"palette.{required}", "is required; every kit needs a background, a text, and a primary color."));
        }
    }

    /// <summary>
    ///     Checks one font list and warns when its first family is not one that CanvasNet bundles or stands in for.
    /// </summary>
    /// <param name="issues">The list that the problems are added to.</param>
    /// <param name="path">The path of the property.</param>
    /// <param name="value">The font list.</param>
    private static void CheckFont(List<StyleIssue> issues, string path, string value)
    {
        if (value.Length > MaxFontLength)
        {
            issues.Add(Error(path, $"is {value.Length} characters long; the limit is {MaxFontLength}."));
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        // The first family decides: a generic keyword, a common alias such as Arial or Segoe UI, or a Liberation family
        // is drawn the same on every machine; any other name is drawn with the installed font by SVG and OpenSCAD only
        var families = GenericFontFamilies.SplitFamilyList(value);
        if (families.Count == 0)
        {
            issues.Add(Error(path, $"'{value}' is not a valid font-family list; write family names separated by commas."));
        }
        else if (!GenericFontFamilies.TryClassify(families[0], includeCommonAliases: true, out _, out _))
        {
            issues.Add(Warning(
                path,
                $"'{families[0]}' is drawn with the installed font only by SVG and OpenSCAD on machines that have it; " +
                "Mermaid, PlantUML, and Vega-Lite text always uses the bundled Liberation fonts."));
        }
    }

    /// <summary>
    ///     Checks the Mermaid part: the theme and the theme variables.
    /// </summary>
    /// <param name="mermaid">The part to check, or <see langword="null" /> when the kit has none.</param>
    /// <param name="issues">The list that the problems are added to.</param>
    private static void ValidateMermaid(StyleMermaid? mermaid, List<StyleIssue> issues)
    {
        if (mermaid is null)
        {
            return;
        }

        if (!MermaidThemes.Contains(mermaid.Theme, StringComparer.Ordinal))
        {
            issues.Add(Error("mermaid.theme", $"'{mermaid.Theme}' is not a Mermaid theme; use {string.Join(", ", MermaidThemes)}."));
        }

        if (mermaid.ThemeVariables.Count > MaxThemeVariables)
        {
            issues.Add(Error("mermaid.themeVariables", $"has {mermaid.ThemeVariables.Count} entries; the limit is {MaxThemeVariables}."));
        }

        foreach (var (name, value) in mermaid.ThemeVariables)
        {
            if (!VariablePattern().IsMatch(name))
            {
                issues.Add(Error($"mermaid.themeVariables.{name}", "is not a usable variable name; start with a letter and use at most 40 letters and digits."));
            }

            if (value.Length > MaxThemeVariableLength)
            {
                issues.Add(Error($"mermaid.themeVariables.{name}", $"is {value.Length} characters long; the limit is {MaxThemeVariableLength}."));
            }
        }
    }

    /// <summary>
    ///     Checks the Vega-Lite configuration: it must be a JSON object that is not too large.
    /// </summary>
    /// <param name="configJson">The JSON text, or <see langword="null" /> when the kit has none.</param>
    /// <param name="issues">The list that the problems are added to.</param>
    private static void ValidateVegaLite(string? configJson, List<StyleIssue> issues)
    {
        if (configJson is null)
        {
            return;
        }

        // The size is measured on the compact text, so that the white space of an indented file does not count
        if (Encoding.UTF8.GetByteCount(configJson) > MaxVegaLiteConfigBytes)
        {
            issues.Add(Error("vegaLite.config", $"is larger than {MaxVegaLiteConfigBytes / 1024} KiB as compact JSON, which is the limit."));
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(configJson, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("vegaLite.config", "must be a JSON object, like the config of a Vega-Lite specification."));
            }
        }
        catch (JsonException exception)
        {
            issues.Add(Error("vegaLite.config", $"is not valid JSON: {exception.Message}"));
        }
    }

    /// <summary>
    ///     Checks the guidance: the length of the notes and the number and length of the entries of the two lists.
    /// </summary>
    /// <param name="guidance">The guidance to check.</param>
    /// <param name="issues">The list that the problems are added to.</param>
    private static void ValidateGuidance(StyleGuidanceText guidance, List<StyleIssue> issues)
    {
        CheckLength(issues, "guidance.notes", guidance.Notes.Trim(), 0, MaxNotesLength);
        CheckList(issues, "guidance.do", guidance.Do);
        CheckList(issues, "guidance.dont", guidance.Dont);
    }

    /// <summary>
    ///     Checks one list of short texts.
    /// </summary>
    /// <param name="issues">The list that the problems are added to.</param>
    /// <param name="path">The path of the list.</param>
    /// <param name="values">The entries.</param>
    private static void CheckList(List<StyleIssue> issues, string path, IReadOnlyList<string> values)
    {
        if (values.Count > MaxGuidanceItems)
        {
            issues.Add(Error(path, $"has {values.Count} entries; the limit is {MaxGuidanceItems}."));
        }

        for (var i = 0; i < values.Count; i++)
        {
            CheckLength(issues, $"{path}[{i}]", values[i].Trim(), 1, MaxGuidanceItemLength);
        }
    }

    /// <summary>
    ///     Checks the size presets: the number, the names, the sizes, and the resolution.
    /// </summary>
    /// <param name="presets">The presets to check.</param>
    /// <param name="issues">The list that the problems are added to.</param>
    private static void ValidatePresets(IReadOnlyList<StyleSizePreset> presets, List<StyleIssue> issues)
    {
        if (presets.Count > MaxSizePresets)
        {
            issues.Add(Error("sizePresets", $"has {presets.Count} entries; the limit is {MaxSizePresets}."));
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var path = $"sizePresets[{i}]";

            // The name is what the user picks in the export dialog, so it must be there and must not repeat
            var name = preset.Name.Trim();
            CheckLength(issues, $"{path}.name", name, 1, MaxPresetNameLength);
            if (name.Length > 0 && !names.Add(name))
            {
                issues.Add(Error($"{path}.name", $"'{name}' is used by another size preset; names must differ, ignoring case."));
            }

            // At least one side is given; a side that is not given is scaled from the other
            if (preset.Width is null && preset.Height is null)
            {
                issues.Add(Error(path, "needs a width, a height, or both."));
            }

            CheckPixels(issues, $"{path}.width", preset.Width);
            CheckPixels(issues, $"{path}.height", preset.Height);
            if (preset.Dpi is { } dpi)
            {
                CheckRange(issues, $"{path}.dpi", dpi, 24, 1200);
            }
        }
    }

    /// <summary>
    ///     Checks the example list: the number, the paths, and the captions.
    /// </summary>
    /// <param name="examples">The examples to check.</param>
    /// <param name="issues">The list that the problems are added to.</param>
    private static void ValidateExamples(IReadOnlyList<StyleExample> examples, List<StyleIssue> issues)
    {
        if (examples.Count > MaxExamples)
        {
            issues.Add(Error("examples", $"has {examples.Count} entries; the limit is {MaxExamples}."));
        }

        foreach (var example in examples)
        {
            var path = $"examples[\"{example.RelativePath}\"]";
            var problem = ExamplePathProblem(example.RelativePath);
            if (problem is not null)
            {
                issues.Add(Error(path, $"the path {problem}."));
            }

            if (example.Caption is { } caption)
            {
                CheckLength(issues, path, caption.Trim(), 0, MaxCaptionLength);
            }
        }
    }

    /// <summary>
    ///     Finds what is wrong with the path of an example, which must name one file directly in the examples folder.
    /// </summary>
    /// <remarks>
    ///     The path is used to build a file name below the kit folder, so a path that is rooted, climbs out with two dots,
    ///     goes into a folder, or holds a character that a file name cannot have is refused: a kit from a shared folder or
    ///     a zip file must not name a file outside itself.
    /// </remarks>
    /// <param name="path">The path to check.</param>
    /// <returns>A phrase that completes the sentence "the path ...", or <see langword="null" /> when the path is usable.</returns>
    internal static string? ExamplePathProblem(string path)
    {
        const string prefix = ExamplesFolderName + "/";
        if (path.Length == 0)
        {
            return "is empty";
        }

        if (Path.IsPathRooted(path) || path.StartsWith('/') || (path.Length >= 2 && path[1] == ':'))
        {
            return "must be relative to the kit folder";
        }

        if (path.Contains('\\'))
        {
            return "must use forward slashes";
        }

        if (path.Split('/').Any(segment => segment is ".." or "."))
        {
            return "must not contain '.' or '..' segments";
        }

        if (!path.StartsWith(prefix, StringComparison.Ordinal) || path.Length == prefix.Length || path.IndexOf('/', prefix.Length) >= 0)
        {
            return "must name one file directly in the examples folder, such as examples/title-card.svg";
        }

        var name = path[prefix.Length..];
        return name.Length > 100 || HasNonPortableNameCharacter(name)
            ? "has a file name that is too long or holds a character that a file name cannot have"
            : null;
    }

    /// <summary>
    ///     Tells whether a file name holds a character that cannot be in a file name on every platform.
    /// </summary>
    /// <remarks>
    ///     The rule is the same on every platform, whatever the platform that checks it accepts: a kit is shared between
    ///     computers (a zip file, a shared folder), so a name that Linux accepts but Windows does not, such as
    ///     <c>a|b.png</c>, must not be written on one and fail to be read on the other. The characters are the control
    ///     characters, the delete character, the two slashes, and <c>: * ? " &lt; &gt; |</c>.
    /// </remarks>
    /// <param name="name">The file name, without a folder.</param>
    /// <returns><see langword="true" /> when the name holds such a character.</returns>
    internal static bool HasNonPortableNameCharacter(string name)
    {
        return name.Any(c => c < ' ' || c == '\u007f' || c is '/' or '\\' || NonPortableNameCharacters.Contains(c, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Checks that a text has a length within limits.
    /// </summary>
    /// <param name="issues">The list that the problems are added to.</param>
    /// <param name="path">The path of the property.</param>
    /// <param name="value">The text, already trimmed.</param>
    /// <param name="min">The smallest length.</param>
    /// <param name="max">The largest length.</param>
    private static void CheckLength(List<StyleIssue> issues, string path, string value, int min, int max)
    {
        if (value.Length < min)
        {
            issues.Add(Error(path, min == 1 ? "must not be empty." : $"must have at least {min} characters."));
        }
        else if (value.Length > max)
        {
            issues.Add(Error(path, $"is {value.Length} characters long; the limit is {max}."));
        }
    }

    /// <summary>
    ///     Checks that a number is finite and within limits.
    /// </summary>
    /// <param name="issues">The list that the problems are added to.</param>
    /// <param name="path">The path of the property.</param>
    /// <param name="value">The number.</param>
    /// <param name="min">The smallest value.</param>
    /// <param name="max">The largest value.</param>
    private static void CheckRange(List<StyleIssue> issues, string path, double value, double min, double max)
    {
        if (!double.IsFinite(value) || value < min || value > max)
        {
            issues.Add(Error(path, $"is {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}; use a number from {min} to {max}."));
        }
    }

    /// <summary>
    ///     Checks that a size in pixels, when it is given, is within the sizes that CanvasNet draws.
    /// </summary>
    /// <param name="issues">The list that the problems are added to.</param>
    /// <param name="path">The path of the property.</param>
    /// <param name="value">The size, or <see langword="null" /> when it is not given.</param>
    private static void CheckPixels(List<StyleIssue> issues, string path, int? value)
    {
        if (value is { } pixels && (pixels < 1 || pixels > MaxPresetPixels))
        {
            issues.Add(Error(path, $"is {pixels}; use a whole number of pixels from 1 to {MaxPresetPixels}."));
        }
    }

    /// <summary>
    ///     Makes an error.
    /// </summary>
    /// <param name="path">The path of the property.</param>
    /// <param name="message">The message.</param>
    /// <returns>The issue.</returns>
    private static StyleIssue Error(string path, string message) => new(path, message, true);

    /// <summary>
    ///     Makes a warning.
    /// </summary>
    /// <param name="path">The path of the property.</param>
    /// <param name="message">The message.</param>
    /// <returns>The issue.</returns>
    private static StyleIssue Warning(string path, string message) => new(path, message, false);

    /// <summary>
    ///     The pattern of the identifier of a kit.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[a-z0-9][a-z0-9-]{0,47}\z")]
    private static partial Regex IdPattern();

    /// <summary>
    ///     The pattern of the name of a color of the palette.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[a-z][a-zA-Z0-9-]{0,31}\z")]
    private static partial Regex RolePattern();

    /// <summary>
    ///     The pattern of the name of a Mermaid theme variable.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9]{0,39}\z")]
    private static partial Regex VariablePattern();
}

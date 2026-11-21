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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Reads the JSON document of a kit property by property and collects a message for each property that cannot be read.
/// </summary>
/// <remarks>
///     This is the reading half of <see cref="StyleKitSerializer" />: it checks the shape (is the property text, a number,
///     an object, or a list) and builds the kit with a harmless default for what could not be read, while the value rules
///     (ranges, patterns, lengths) are applied afterwards to the whole kit by the serializer. A message about a value that
///     could not be read at all is not repeated by that second pass, which is what <see cref="Covers" /> is for. One
///     instance reads one document and is not safe for concurrent use.
/// </remarks>
internal sealed class StyleKitReader
{
    /// <summary>
    ///     The top-level properties of a kit file; any other property is a warning.
    /// </summary>
    private static readonly HashSet<string> KnownProperties = new(StringComparer.Ordinal)
    {
        "formatVersion", "id", "name", "description", "palette", "fonts", "stroke", "cornerRadius", "spacing",
        "mermaid", "vegaLite", "guidance", "sizePresets", "examples"
    };

    /// <summary>
    ///     The paths of the properties that could not be read.
    /// </summary>
    private readonly List<string> _failed = [];

    /// <summary>
    ///     Gets every error and warning found so far, in the order they were found.
    /// </summary>
    public List<StyleIssue> Issues { get; } = [];

    /// <summary>
    ///     Records an error for a property that cannot be used.
    /// </summary>
    /// <param name="path">The path of the property, or an empty string for the whole file.</param>
    /// <param name="message">What is wrong, without the path.</param>
    public void Fail(string path, string message)
    {
        Issues.Add(new StyleIssue(path, message, true));
        _failed.Add(path);
    }

    /// <summary>
    ///     Records a warning for a property that is used but may not do what the author expects.
    /// </summary>
    /// <param name="path">The path of the property.</param>
    /// <param name="message">What is the matter, without the path.</param>
    public void Warn(string path, string message) => Issues.Add(new StyleIssue(path, message, false));

    /// <summary>
    ///     Tells whether a property, or the property that holds it, has already been reported as unreadable.
    /// </summary>
    /// <param name="path">The path of a property.</param>
    /// <returns><see langword="true" /> when a message about the property or its parent has been recorded.</returns>
    public bool Covers(string path) =>
        _failed.Any(f => path == f
                         || path.StartsWith(f + ".", StringComparison.Ordinal)
                         || path.StartsWith(f + "[", StringComparison.Ordinal));

    /// <summary>
    ///     Checks the <c>formatVersion</c> of the document.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <returns><see langword="true" /> when the version is the one this version reads; otherwise an error is recorded.</returns>
    public bool CheckVersion(JsonElement root)
    {
        if (!root.TryGetProperty("formatVersion", out var value))
        {
            Fail("formatVersion", $"is missing; write \"formatVersion\": {StyleKitSerializer.CurrentFormatVersion}.");
            return false;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var version))
        {
            Fail("formatVersion", $"must be the number {StyleKitSerializer.CurrentFormatVersion}.");
            return false;
        }

        // A kit of a newer format may mean something this version cannot keep, so it is refused and not half read
        if (version > StyleKitSerializer.CurrentFormatVersion)
        {
            Fail("formatVersion", $"is {version}: the kit was made by a newer version of CanvasPilot, and this version reads format {StyleKitSerializer.CurrentFormatVersion}.");
            return false;
        }

        if (version < 1)
        {
            Fail("formatVersion", $"is {version}; the first format is 1 and this version reads format {StyleKitSerializer.CurrentFormatVersion}.");
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Records a warning for each top-level property that this version does not know.
    /// </summary>
    /// <param name="root">The root object.</param>
    public void WarnAboutUnknownProperties(JsonElement root)
    {
        foreach (var property in root.EnumerateObject().Where(p => !KnownProperties.Contains(p.Name)))
        {
            Warn(property.Name, "is not a property of this version of the format; it is ignored and is dropped when the kit is saved.");
        }
    }

    /// <summary>
    ///     Records a warning when the name of the folder of a kit differs from the identifier in the file.
    /// </summary>
    /// <param name="id">The identifier of the kit.</param>
    /// <param name="kitFolder">The folder of the kit.</param>
    public void WarnWhenFolderDiffersFromId(string id, string kitFolder)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(kitFolder));
        if (name.Length > 0 && !string.Equals(name, id, StringComparison.OrdinalIgnoreCase))
        {
            Warn("id", $"the folder is named '{name}' but the id of the kit is '{id}'; the id of the file is used.");
        }
    }

    /// <summary>
    ///     Reads every property of the kit, with a default for each one that cannot be read.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <param name="kitFolder">The folder of the kit, or an empty string when it has none.</param>
    /// <returns>The kit, which has not yet been checked for the rules of the values.</returns>
    public StyleKit ReadKit(JsonElement root, string kitFolder)
    {
        var id = ReadString(root, "id", "id", required: true) ?? string.Empty;
        var name = ReadString(root, "name", "name", required: true) ?? string.Empty;
        var description = ReadString(root, "description", "description", required: false) ?? string.Empty;
        var palette = ReadPalette(root);
        var fonts = ReadFonts(root);

        // The numbers have the defaults of a plain kit, so that a small file is a complete kit
        var stroke = ReadObject(root, "stroke", "stroke");
        var strokeWidth = (stroke is { } s ? ReadNumber(s, "width", "stroke.width") : null) ?? 1;
        var cornerRadius = ReadNumber(root, "cornerRadius", "cornerRadius") ?? 0;
        var spacing = ReadNumber(root, "spacing", "spacing") ?? 8;

        return new StyleKit(
            id,
            name,
            description,
            palette,
            fonts,
            strokeWidth,
            cornerRadius,
            spacing,
            ReadMermaid(root),
            ReadVegaLite(root),
            ReadGuidance(root),
            ReadPresets(root),
            ReadExamples(root, kitFolder));
    }

    /// <summary>
    ///     Reads the palette object into an ordered dictionary of normalized colors.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <returns>The palette; empty when it is missing or not an object.</returns>
    private Dictionary<string, string> ReadPalette(JsonElement root)
    {
        var palette = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!HasValue(root, "palette", out var element))
        {
            Fail("palette", "is missing; every kit needs a palette with at least a background, a text, and a primary color.");
            return palette;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            Fail("palette", "must be an object of color names to #RRGGBB colors.");
            return palette;
        }

        foreach (var property in element.EnumerateObject())
        {
            var path = $"palette.{property.Name}";
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                Fail(path, "must be a color text such as #RRGGBB.");
                continue;
            }

            // A valid color is kept in its one normal form; an invalid one is kept as written for the message
            var text = property.Value.GetString()!.Trim();
            if (!palette.TryAdd(property.Name, StyleColors.TryNormalize(text, out var normalized) ? normalized : text))
            {
                Fail(path, "is listed more than once.");
            }
        }

        return palette;
    }

    /// <summary>
    ///     Reads the three font lists, with the plain defaults for the ones that are not given.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <returns>The fonts.</returns>
    private StyleFonts ReadFonts(JsonElement root)
    {
        var fonts = ReadObject(root, "fonts", "fonts");
        if (fonts is not { } element)
        {
            return new StyleFonts("sans-serif", "sans-serif", "monospace");
        }

        return new StyleFonts(
            ReadString(element, "heading", "fonts.heading", required: false) ?? "sans-serif",
            ReadString(element, "body", "fonts.body", required: false) ?? "sans-serif",
            ReadString(element, "monospace", "fonts.monospace", required: false) ?? "monospace");
    }

    /// <summary>
    ///     Reads the Mermaid part, which a kit may leave out.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <returns>The part, or <see langword="null" /> when it is not given or is not an object.</returns>
    private StyleMermaid? ReadMermaid(JsonElement root)
    {
        if (ReadObject(root, "mermaid", "mermaid") is not { } mermaid)
        {
            return null;
        }

        var theme = ReadString(mermaid, "theme", "mermaid.theme", required: true) ?? string.Empty;
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ReadObject(mermaid, "themeVariables", "mermaid.themeVariables") is { } element)
        {
            foreach (var property in element.EnumerateObject())
            {
                var path = $"mermaid.themeVariables.{property.Name}";
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    Fail(path, "must be text.");
                }
                else if (!variables.TryAdd(property.Name, property.Value.GetString()!.Trim()))
                {
                    Fail(path, "is listed more than once.");
                }
            }
        }

        return new StyleMermaid(theme, variables);
    }

    /// <summary>
    ///     Reads the Vega-Lite part, which a kit may leave out, as the compact text of its configuration.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <returns>The compact JSON text of the configuration, or <see langword="null" />.</returns>
    private string? ReadVegaLite(JsonElement root)
    {
        if (ReadObject(root, "vegaLite", "vegaLite") is not { } vega)
        {
            return null;
        }

        if (!HasValue(vega, "config", out var config))
        {
            Fail("vegaLite.config", "is missing; give the config object of a Vega-Lite specification.");
            return null;
        }

        if (config.ValueKind != JsonValueKind.Object)
        {
            Fail("vegaLite.config", "must be a JSON object, like the config of a Vega-Lite specification.");
            return null;
        }

        return StyleKitSerializer.Compact(config);
    }

    /// <summary>
    ///     Reads the guidance, with empty notes and lists for what is not given.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <returns>The guidance.</returns>
    private StyleGuidanceText ReadGuidance(JsonElement root)
    {
        if (ReadObject(root, "guidance", "guidance") is not { } guidance)
        {
            return new StyleGuidanceText(string.Empty, [], []);
        }

        return new StyleGuidanceText(
            ReadString(guidance, "notes", "guidance.notes", required: false) ?? string.Empty,
            ReadStringList(guidance, "do", "guidance.do"),
            ReadStringList(guidance, "dont", "guidance.dont"));
    }

    /// <summary>
    ///     Reads a list of texts.
    /// </summary>
    /// <param name="parent">The object that holds the list.</param>
    /// <param name="name">The property name.</param>
    /// <param name="path">The path of the property.</param>
    /// <returns>The texts, trimmed; empty when the list is not given.</returns>
    private List<string> ReadStringList(JsonElement parent, string name, string path)
    {
        var values = new List<string>();
        if (!HasValue(parent, name, out var element))
        {
            return values;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            Fail(path, "must be a list of texts.");
            return values;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                values.Add(item.GetString()!.Trim());
            }
            else
            {
                Fail($"{path}[{index}]", "must be text.");
            }

            index++;
        }

        return values;
    }

    /// <summary>
    ///     Reads the size presets.
    /// </summary>
    /// <remarks>
    ///     An entry that is not an object is replaced by an empty preset so that the index of every later entry stays the
    ///     index in the file, which is what the messages name.
    /// </remarks>
    /// <param name="root">The root object.</param>
    /// <returns>The presets in file order.</returns>
    private List<StyleSizePreset> ReadPresets(JsonElement root)
    {
        var presets = new List<StyleSizePreset>();
        if (!HasValue(root, "sizePresets", out var element))
        {
            return presets;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            Fail("sizePresets", "must be a list of size presets.");
            return presets;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var path = $"sizePresets[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                Fail(path, "must be an object with a name and a width, a height, or both.");
                presets.Add(new StyleSizePreset(string.Empty, null, null, null, false));
            }
            else
            {
                presets.Add(new StyleSizePreset(
                    ReadString(item, "name", $"{path}.name", required: true) ?? string.Empty,
                    ReadInt(item, "width", $"{path}.width"),
                    ReadInt(item, "height", $"{path}.height"),
                    ReadNumber(item, "dpi", $"{path}.dpi"),
                    ReadBool(item, "transparent", $"{path}.transparent") ?? false));
            }

            index++;
        }

        return presets;
    }

    /// <summary>
    ///     Reads the captions of the examples and joins them with the files of the examples folder.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <param name="kitFolder">The folder of the kit, or an empty string when it has none.</param>
    /// <returns>The examples, sorted by path.</returns>
    private List<StyleExample> ReadExamples(JsonElement root, string kitFolder)
    {
        // The captions, keyed by path; a path that could never name a file in the folder is an error
        var captions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (ReadObject(root, "examples", "examples") is { } element)
        {
            foreach (var property in element.EnumerateObject())
            {
                var path = $"examples[\"{property.Name}\"]";
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    Fail(path, "must be a caption text.");
                }
                else if (StyleKitSerializer.ExamplePathProblem(property.Name) is { } problem)
                {
                    Fail(path, $"the path {problem}.");
                }
                else if (!captions.TryAdd(property.Name, property.Value.GetString()!.Trim()))
                {
                    Fail(path, "is listed more than once.");
                }
            }

            if (captions.Count > StyleKitSerializer.MaxExamples)
            {
                Fail("examples", $"has {captions.Count} entries; the limit is {StyleKitSerializer.MaxExamples}.");
            }
        }

        // Without a folder the captions are all there is; with one the files are the truth
        if (kitFolder.Length == 0)
        {
            return captions
                .OrderBy(c => c.Key, StringComparer.Ordinal)
                .Select(c => new StyleExample(c.Key, string.Empty, c.Value))
                .ToList();
        }

        var examples = DiscoverExamples(kitFolder)
            .Select(file => new StyleExample(file.RelativePath, file.FullPath, captions.GetValueOrDefault(file.RelativePath)))
            .ToList();
        foreach (var orphan in captions.Keys.Where(k => !examples.Any(e => string.Equals(e.RelativePath, k, StringComparison.OrdinalIgnoreCase))))
        {
            Warn($"examples[\"{orphan}\"]", "has no file in the examples folder; the caption is ignored and is dropped when the kit is saved.");
        }

        return examples;
    }

    /// <summary>
    ///     Lists the usable files of the examples folder of a kit: files at the top level, sorted by name, not hidden, not
    ///     links, and not larger than the limit, up to the most that a kit may have.
    /// </summary>
    /// <param name="kitFolder">The folder of the kit.</param>
    /// <returns>The relative path and full path of each example, sorted by name.</returns>
    private List<(string RelativePath, string FullPath)> DiscoverExamples(string kitFolder)
    {
        var found = new List<(string, string)>();
        var folder = Path.Combine(kitFolder, StyleKitSerializer.ExamplesFolderName);
        if (!Directory.Exists(folder))
        {
            return found;
        }

        try
        {
            // The enumeration is bounded so that a folder with a very large number of files costs a fixed amount
            var files = new DirectoryInfo(folder).EnumerateFiles()
                .Where(f => !f.Name.StartsWith('.'))
                .Take(StyleKitSerializer.MaxExamples * 20)
                .OrderBy(f => f.Name, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var relative = $"{StyleKitSerializer.ExamplesFolderName}/{file.Name}";
                var path = $"examples[\"{relative}\"]";
                if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    Warn(path, "is a link and is ignored; an example must be a file in the examples folder.");
                }
                else if (file.Length > StyleKitSerializer.MaxExampleBytes)
                {
                    Warn(path, $"is larger than {StyleKitSerializer.MaxExampleBytes / (1024 * 1024)} MiB and is ignored.");
                }
                else if (found.Count >= StyleKitSerializer.MaxExamples)
                {
                    Warn(path, $"is beyond the {StyleKitSerializer.MaxExamples} examples that a kit may have and is ignored.");
                }
                else
                {
                    found.Add((relative, file.FullName));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Warn(StyleKitSerializer.ExamplesFolderName, $"the examples folder could not be read: {exception.Message}");
        }

        return found;
    }

    /// <summary>
    ///     Finds a property that has a value; a property that is missing or <c>null</c> has none.
    /// </summary>
    /// <param name="parent">The object to look in.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true" /> when the property exists and is not <c>null</c>.</returns>
    private static bool HasValue(JsonElement parent, string name, out JsonElement value) =>
        parent.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;

    /// <summary>
    ///     Reads a property that must be text if it is given.
    /// </summary>
    /// <param name="parent">The object to look in.</param>
    /// <param name="name">The property name.</param>
    /// <param name="path">The path for the message.</param>
    /// <param name="required">Whether a missing property is an error.</param>
    /// <returns>The trimmed text, or <see langword="null" /> when it is not given or is not text.</returns>
    private string? ReadString(JsonElement parent, string name, string path, bool required)
    {
        if (!HasValue(parent, name, out var value))
        {
            if (required)
            {
                Fail(path, "is missing.");
            }

            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()!.Trim();
        }

        Fail(path, "must be text.");
        return null;
    }

    /// <summary>
    ///     Reads a property that must be a number if it is given.
    /// </summary>
    /// <param name="parent">The object to look in.</param>
    /// <param name="name">The property name.</param>
    /// <param name="path">The path for the message.</param>
    /// <returns>The number, or <see langword="null" /> when it is not given or is not a usable number.</returns>
    private double? ReadNumber(JsonElement parent, string name, string path)
    {
        if (!HasValue(parent, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number))
        {
            return number;
        }

        Fail(path, "must be a number.");
        return null;
    }

    /// <summary>
    ///     Reads a property that must be a whole number if it is given.
    /// </summary>
    /// <param name="parent">The object to look in.</param>
    /// <param name="name">The property name.</param>
    /// <param name="path">The path for the message.</param>
    /// <returns>The number, or <see langword="null" /> when it is not given or is not a whole number.</returns>
    private int? ReadInt(JsonElement parent, string name, string path)
    {
        if (!HasValue(parent, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        Fail(path, "must be a whole number.");
        return null;
    }

    /// <summary>
    ///     Reads a property that must be <c>true</c> or <c>false</c> if it is given.
    /// </summary>
    /// <param name="parent">The object to look in.</param>
    /// <param name="name">The property name.</param>
    /// <param name="path">The path for the message.</param>
    /// <returns>The value, or <see langword="null" /> when it is not given or is not a boolean.</returns>
    private bool? ReadBool(JsonElement parent, string name, string path)
    {
        if (!HasValue(parent, name, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        Fail(path, "must be true or false.");
        return null;
    }

    /// <summary>
    ///     Reads a property that must be an object if it is given.
    /// </summary>
    /// <param name="parent">The object to look in.</param>
    /// <param name="name">The property name.</param>
    /// <param name="path">The path for the message.</param>
    /// <returns>The object, or <see langword="null" /> when it is not given or is not an object.</returns>
    private JsonElement? ReadObject(JsonElement parent, string name, string path)
    {
        if (!HasValue(parent, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        Fail(path, "must be an object.");
        return null;
    }
}

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

using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Describes the two tools that give an assistant the style of the project: <c>style_get</c>, which gives the style of
///     the kit of the project as text, and <c>style_check</c>, which checks a picture file of the project against the
///     palette of that kit.
/// </summary>
/// <remarks>
///     <para>
///         The tools are plain descriptions (<see cref="KnowledgeTool" />, the descriptor that the drawing knowledge uses)
///         with a function that takes the arguments as a JSON text and gives the answer as text, so they can be handed to the
///         tool mechanism of any provider of a language model without the unit depending on one. A function never throws:
///         an argument that cannot be used, a kit or a file that does not exist, and a picture that cannot be drawn are
///         answered with a message that says what to send instead, because an assistant corrects itself from a message and
///         cannot from an exception.
///     </para>
///     <para>
///         <c>style_check</c> reads a file, so it is confined to the folder of the open project: the path is combined with
///         the folder by <see cref="PathHelpers.SafePathCombine" />, and a path that leaves the folder, a path inside the
///         reserved folder <c>.canvaspilot</c>, a folder, a file that is not there, and a file or a folder on the way that is
///         a link or a junction to somewhere outside the project are all refused. The file is drawn with the render service,
///         which lets the file read other files only from inside the project folder as well.
///     </para>
///     <para>
///         The function of <c>style_check</c> waits for the picture to be drawn on a thread of the pool, so it must be
///         called from the thread of the assistant and never from the thread of the user interface. The functions hold no
///         state of their own and are safe for concurrent use.
///     </para>
/// </remarks>
public static class StyleTools
{
    /// <summary>
    ///     The name of the tool that gives the style.
    /// </summary>
    public const string GetToolName = "style_get";

    /// <summary>
    ///     The name of the tool that checks a picture against the style.
    /// </summary>
    public const string CheckToolName = "style_check";

    /// <summary>
    ///     What <c>style_get</c> is for and how to call it.
    /// </summary>
    internal const string GetToolDescription =
        "Gets the style of the style kit of this project: its colors with their roles, fonts, shape numbers, the frontmatter for "
        + "Mermaid files, the config for Vega-Lite files, advice, export sizes, and the paths of example images that you can open. "
        + "Call it before you draw. With no arguments it gives the style of the kit of the project; kit names another kit by id; "
        + "section gives one part in full (palette, fonts, mermaid, vegaLite, notes, presets, examples).";

    /// <summary>
    ///     What <c>style_check</c> is for and how to call it.
    /// </summary>
    internal const string CheckToolDescription =
        "Checks a picture file of this project against the palette of the style kit of the project, and says how much of it is "
        + "off the palette, with the colors to change. Call it after you draw a picture and fix what it reports. path is the file "
        + "relative to the project folder (default: the picture that is shown); tolerance is the color difference, 0 to 100, "
        + "within which a color counts as a palette color (default 10).";

    /// <summary>
    ///     The JSON Schema of the arguments of <c>style_get</c>.
    /// </summary>
    internal const string GetParametersSchema =
        """
        {"type":"object","properties":{"kit":{"type":"string","description":"Id of a style kit, to get its style instead of the style of the kit of the project."},"section":{"type":"string","enum":["palette","fonts","mermaid","vegaLite","notes","presets","examples"],"description":"One part of the style, in full."}},"additionalProperties":false}
        """;

    /// <summary>
    ///     The JSON Schema of the arguments of <c>style_check</c>.
    /// </summary>
    internal const string CheckParametersSchema =
        """
        {"type":"object","properties":{"path":{"type":"string","description":"The picture file, relative to the project folder, or a full path inside it. Default: the picture that is shown."},"tolerance":{"type":"number","minimum":0,"maximum":100,"description":"Color difference within which a color counts as a palette color. Default 10."}},"additionalProperties":false}
        """;

    /// <summary>
    ///     The most characters of an argument that an answer repeats.
    /// </summary>
    private const int EchoLimit = 80;

    /// <summary>
    ///     The most kit ids that an answer lists.
    /// </summary>
    private const int ListedKitLimit = 40;

    /// <summary>
    ///     Creates the tool that gives the style.
    /// </summary>
    /// <param name="active">Says which kit the open project uses.</param>
    /// <param name="catalog">Finds a kit by its id and lists the kits that are known.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public static KnowledgeTool CreateGetTool(IActiveStyle active, IStyleKitCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(catalog);

        return new KnowledgeTool(GetToolName, GetToolDescription, GetParametersSchema, argumentsJson => AnswerGet(argumentsJson, active, catalog));
    }

    /// <summary>
    ///     Creates the tool that checks a picture against the style.
    /// </summary>
    /// <param name="active">Says which kit the open project uses.</param>
    /// <param name="projectFolder">Gives the folder of the open project, or <see langword="null" /> when none is open.</param>
    /// <param name="currentFile">Gives the full path of the picture that is shown, or <see langword="null" /> when none is.</param>
    /// <param name="renderer">Draws the file as a picture.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public static KnowledgeTool CreateCheckTool(IActiveStyle active, Func<string?> projectFolder, Func<string?> currentFile, IImageRenderService renderer)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(projectFolder);
        ArgumentNullException.ThrowIfNull(currentFile);
        ArgumentNullException.ThrowIfNull(renderer);

        return new KnowledgeTool(
            CheckToolName,
            CheckToolDescription,
            CheckParametersSchema,
            argumentsJson => AnswerCheck(argumentsJson, active, projectFolder, currentFile, renderer));
    }

    /// <summary>
    ///     Creates both tools.
    /// </summary>
    /// <param name="active">Says which kit the open project uses.</param>
    /// <param name="catalog">Finds a kit by its id and lists the kits that are known.</param>
    /// <param name="projectFolder">Gives the folder of the open project, or <see langword="null" /> when none is open.</param>
    /// <param name="currentFile">Gives the full path of the picture that is shown, or <see langword="null" /> when none is.</param>
    /// <param name="renderer">Draws the file as a picture.</param>
    /// <returns>The tools: <c>style_get</c> and <c>style_check</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public static IReadOnlyList<KnowledgeTool> CreateAll(
        IActiveStyle active,
        IStyleKitCatalog catalog,
        Func<string?> projectFolder,
        Func<string?> currentFile,
        IImageRenderService renderer)
    {
        return [CreateGetTool(active, catalog), CreateCheckTool(active, projectFolder, currentFile, renderer)];
    }

    /// <summary>
    ///     Answers a call of <c>style_get</c>; never throws.
    /// </summary>
    /// <param name="argumentsJson">The arguments as a JSON text, or nothing.</param>
    /// <param name="active">Says which kit the open project uses.</param>
    /// <param name="catalog">Finds a kit by its id.</param>
    /// <returns>The answer.</returns>
    private static string AnswerGet(string? argumentsJson, IActiveStyle active, IStyleKitCatalog catalog)
    {
        try
        {
            if (!TryReadObject(argumentsJson, GetToolName, "kit (a style kit id) or section", out var root, out var error))
            {
                return error;
            }

            var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root is { } properties && !TryReadStrings(properties, ["kit", "section"], GetToolName, arguments, out error))
            {
                return error;
            }

            // Choose the kit: the one that was named, else the one that the project uses
            StyleKitEntry? entry;
            if (arguments.TryGetValue("kit", out var kitId))
            {
                entry = catalog.Resolve(kitId);
                if (entry is null)
                {
                    return $"No style kit has the id '{Echo(kitId)}'. {KnownKits(catalog)}";
                }
            }
            else
            {
                entry = active.Active;
                if (entry is null)
                {
                    return NoActiveKit(active, catalog);
                }
            }

            if (!arguments.TryGetValue("section", out var section))
            {
                return StyleGuidance.Build(entry);
            }

            var known = StyleGuidance.SectionNames.FirstOrDefault(name => string.Equals(name, section, StringComparison.OrdinalIgnoreCase));
            return known is null
                ? $"Error: '{Echo(section)}' is not a section of the style. Use one of: {string.Join(", ", StyleGuidance.SectionNames)}."
                : StyleGuidance.BuildSection(entry, known);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The tool answers an assistant, which can recover from a message but not from an exception
            return $"Error: the tool could not answer ({ex.GetType().Name}).";
        }
    }

    /// <summary>
    ///     Answers a call of <c>style_check</c>; never throws.
    /// </summary>
    /// <param name="argumentsJson">The arguments as a JSON text, or nothing.</param>
    /// <param name="active">Says which kit the open project uses.</param>
    /// <param name="projectFolder">Gives the folder of the open project.</param>
    /// <param name="currentFile">Gives the picture that is shown.</param>
    /// <param name="renderer">Draws the file.</param>
    /// <returns>The answer.</returns>
    private static string AnswerCheck(
        string? argumentsJson,
        IActiveStyle active,
        Func<string?> projectFolder,
        Func<string?> currentFile,
        IImageRenderService renderer)
    {
        try
        {
            // Read the arguments: an optional path and an optional tolerance
            if (!TryReadObject(argumentsJson, CheckToolName, "path (a picture file) or tolerance (a number)", out var root, out var error))
            {
                return error;
            }

            string? path = null;
            double? tolerance = null;
            if (root is { } properties)
            {
                var strings = new Dictionary<string, string>(StringComparer.Ordinal);
                if (!TryReadStrings(properties, ["path"], CheckToolName, strings, out error))
                {
                    return error;
                }

                path = strings.GetValueOrDefault("path");
                if (properties.TryGetProperty("tolerance", out var value) && value.ValueKind != JsonValueKind.Null)
                {
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || number < 0 || number > 100)
                    {
                        return $"Error: tolerance must be a number from 0 to 100. Call {CheckToolName} with path (a picture file) and, if you want, tolerance.";
                    }

                    tolerance = number;
                }
            }

            // The check needs a project, a kit, and a file that is inside the project
            var folder = projectFolder();
            if (string.IsNullOrWhiteSpace(folder))
            {
                return "Error: no project is open, so there is no picture to check.";
            }

            if (active.Active is not { } entry)
            {
                return active.IsMissing
                    ? $"Error: the project names the style kit '{Echo(active.ActiveId ?? string.Empty)}', which cannot be found, so there is no palette to check against. Ask the user to choose a style kit."
                    : "Error: this project has no style kit, so there is no palette to check against. Ask the user to choose a style kit.";
            }

            path ??= currentFile();
            if (string.IsNullOrWhiteSpace(path))
            {
                return $"Error: no path was given and no picture is shown. Call {CheckToolName} with path set to a picture file of the project.";
            }

            var file = ResolveInsideProject(folder, path, out error);
            if (file is null)
            {
                return error;
            }

            // Draw the file on a thread of the pool, so the wait cannot be blocked by the context of the caller, and check it
            var result = Task.Run(() => renderer.RenderAsync(file, folder, CancellationToken.None)).GetAwaiter().GetResult();
            if (result.Image is not { } image)
            {
                return DrawFailure(result);
            }

            var options = tolerance is { } tolerated ? new StyleCheckOptions { Tolerance = tolerated } : new StyleCheckOptions();
            return StyleChecker.Check(image, entry.Kit, options).ToText(Path.GetFileName(file));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The tool answers an assistant, which can recover from a message but not from an exception
            return $"Error: the tool could not answer ({ex.GetType().Name}).";
        }
    }

    /// <summary>
    ///     Writes the answer for a picture that could not be drawn.
    /// </summary>
    /// <param name="result">The result of the drawing, which has no picture.</param>
    /// <returns>The message, with the code of the error and its message and hint.</returns>
    private static string DrawFailure(RenderResult result)
    {
        if (result.Error is not { } error)
        {
            return "Error: the picture could not be drawn because the drawing was cancelled.";
        }

        var hint = string.IsNullOrWhiteSpace(error.Hint) ? string.Empty : $" {error.Hint}";
        return $"Error: the picture could not be drawn ({error.Code}): {error.Message}{hint}";
    }

    /// <summary>
    ///     Resolves a path against the project folder and refuses everything that is not a file inside the project.
    /// </summary>
    /// <param name="projectFolder">The folder of the project.</param>
    /// <param name="path">The path as the caller gave it: relative to the project, or a full path.</param>
    /// <param name="error">The message to answer when the path is refused.</param>
    /// <returns>The full path of the file, or <see langword="null" /> when it is refused.</returns>
    private static string? ResolveInsideProject(string projectFolder, string path, out string error)
    {
        error = string.Empty;
        string full;
        try
        {
            // The combination refuses a path that leaves the folder, as a relative path with .. or a full path elsewhere
            full = Path.GetFullPath(PathHelpers.SafePathCombine(projectFolder, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Error: '{Echo(path)}' is not a path inside the project folder. Use a path relative to the project folder.";
            return null;
        }

        // The folder that the application keeps its own data in is not for the tools
        var root = Path.GetFullPath(projectFolder);
        var relative = Path.GetRelativePath(root, full);
        var first = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.None)[0];
        if (string.Equals(first, ProjectDocument.DataFolderName, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Error: '{Echo(path)}' is in the data folder of the application, which is not for pictures. Use a picture file of the project.";
            return null;
        }

        if (Directory.Exists(full))
        {
            error = $"Error: '{Echo(path)}' is a folder. Use a picture file of the project.";
            return null;
        }

        if (!File.Exists(full))
        {
            error = $"Error: no file '{Echo(path)}' in the project folder. Use a path relative to the project folder.";
            return null;
        }

        if (LeavesTheProject(root, full))
        {
            error = $"Error: '{Echo(path)}' is, or is inside, a link to a place outside the project folder. Use a file that is in the project.";
            return null;
        }

        return full;
    }

    /// <summary>
    ///     Tells whether the file, or a folder between it and the project folder, is a link or a junction that leads out of
    ///     the project folder or cannot be followed.
    /// </summary>
    /// <param name="root">The full path of the project folder.</param>
    /// <param name="file">The full path of the file, which is inside the folder as a path.</param>
    /// <returns><see langword="true" /> when the real place of the file is outside the project folder.</returns>
    private static bool LeavesTheProject(string root, string file)
    {
        // The project folder may itself be reached through a link, so its real place is also a place inside it
        var realRoot = FinalTarget(new DirectoryInfo(root))?.FullName ?? root;
        FileSystemInfo current = new FileInfo(file);
        while (true)
        {
            if (current.LinkTarget is not null || current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                var target = FinalTarget(current);
                if (target is null || (!IsInside(root, target.FullName) && !IsInside(realRoot, target.FullName)))
                {
                    return true;
                }
            }

            var parent = current is FileInfo fileInfo ? fileInfo.Directory : ((DirectoryInfo)current).Parent;
            if (parent is null || string.Equals(Path.TrimEndingDirectorySeparator(parent.FullName), Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            current = parent;
        }
    }

    /// <summary>
    ///     Follows a link to the place it finally leads to.
    /// </summary>
    /// <param name="info">The file or folder.</param>
    /// <returns>The place, or <see langword="null" /> when it is not a link or the link cannot be followed.</returns>
    private static FileSystemInfo? FinalTarget(FileSystemInfo info)
    {
        try
        {
            return info.ResolveLinkTarget(returnFinalTarget: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Tells whether a path is inside a folder.
    /// </summary>
    /// <param name="folder">The full path of the folder.</param>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true" /> when the path is the folder or below it.</returns>
    private static bool IsInside(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        return !(relative == ".."
                 || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                 || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                 || Path.IsPathRooted(relative));
    }

    /// <summary>
    ///     Reads the arguments of a call as a JSON object.
    /// </summary>
    /// <param name="argumentsJson">The arguments as a JSON text, or nothing.</param>
    /// <param name="tool">The name of the tool, for the message.</param>
    /// <param name="arguments">What the tool takes, for the message.</param>
    /// <param name="root">The object, or <see langword="null" /> when there are no arguments.</param>
    /// <param name="error">The message to answer when the arguments cannot be used.</param>
    /// <returns><see langword="true" /> when the arguments can be used; otherwise <see langword="false" />.</returns>
    private static bool TryReadObject(string? argumentsJson, string tool, string arguments, out JsonElement? root, out string error)
    {
        root = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return true;
        }

        try
        {
            // The document is cloned so the element can be used after the document is disposed
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = $"Error: the arguments must be a JSON object. Call {tool} with no arguments, or with {arguments}.";
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            error = $"Error: the arguments are not valid JSON. Call {tool} with no arguments, or with a JSON object that has {arguments}.";
            return false;
        }
    }

    /// <summary>
    ///     Reads the string arguments of a call; a property that is absent, null, or empty is the same as none.
    /// </summary>
    /// <param name="properties">The object.</param>
    /// <param name="names">The names of the string arguments.</param>
    /// <param name="tool">The name of the tool, for the message.</param>
    /// <param name="values">Receives the values that were given.</param>
    /// <param name="error">The message to answer when an argument cannot be used.</param>
    /// <returns><see langword="true" /> when every argument can be used; otherwise <see langword="false" />.</returns>
    private static bool TryReadStrings(JsonElement properties, string[] names, string tool, Dictionary<string, string> values, out string error)
    {
        error = string.Empty;
        foreach (var name in names)
        {
            if (!properties.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (property.ValueKind != JsonValueKind.String)
            {
                error = $"Error: '{name}' must be a string. Call {tool} with string arguments as its description says.";
                return false;
            }

            var text = property.GetString()?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                values[name] = text;
            }
        }

        return true;
    }

    /// <summary>
    ///     Writes the answer for a call that needs the kit of the project when the project has none.
    /// </summary>
    /// <param name="active">Says which kit the open project uses.</param>
    /// <param name="catalog">Lists the kits that are known.</param>
    /// <returns>The message, which says how to get a style.</returns>
    private static string NoActiveKit(IActiveStyle active, IStyleKitCatalog catalog)
    {
        var reason = active.IsMissing
            ? $"The project names the style kit '{Echo(active.ActiveId ?? string.Empty)}', which cannot be found."
            : "This project has no style kit.";
        return $"{reason} Ask the user to choose one (Tools > Style Kits, or the style pill in the title bar), "
               + $"or call {GetToolName} with kit set to the id of a kit. {KnownKits(catalog)}";
    }

    /// <summary>
    ///     Writes the ids of the kits that are known.
    /// </summary>
    /// <param name="catalog">Lists the kits.</param>
    /// <returns>A sentence that lists the ids, or says that there are none.</returns>
    private static string KnownKits(IStyleKitCatalog catalog)
    {
        var ids = catalog.Entries
            .Where(entry => !entry.IsOverridden)
            .Select(entry => entry.Kit.Id)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
        {
            return "No style kits are known.";
        }

        var listed = string.Join(", ", ids.Take(ListedKitLimit));
        return string.Create(CultureInfo.InvariantCulture, $"Known kits: {listed}{(ids.Count > ListedKitLimit ? $" and {ids.Count - ListedKitLimit} more" : string.Empty)}.");
    }

    /// <summary>
    ///     Shortens a text that an answer repeats, so that a long argument is not copied into the answer.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, cut to <see cref="EchoLimit" /> characters with an ellipsis when it is longer.</returns>
    private static string Echo(string text)
    {
        return text.Length <= EchoLimit ? text : text[..EchoLimit] + "...";
    }
}

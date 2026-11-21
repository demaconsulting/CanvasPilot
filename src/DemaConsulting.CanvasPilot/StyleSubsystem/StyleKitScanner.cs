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

using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     A kit that was read from a folder, with the warnings that reading it gave.
/// </summary>
/// <param name="Kit">The kit.</param>
/// <param name="FolderPath">The full path of its folder.</param>
/// <param name="Warnings">The warnings.</param>
internal sealed record ScannedKit(StyleKit Kit, string FolderPath, IReadOnlyList<StyleIssue> Warnings);

/// <summary>
///     The result of reading one place that holds kits.
/// </summary>
/// <param name="Location">The kind of place.</param>
/// <param name="Path">The full path of the place.</param>
/// <param name="SharedRoot">The shared folder, for a shared place, or <see langword="null" />.</param>
/// <param name="IsAvailable">Whether the place could be read.</param>
/// <param name="Problem">Why it could not be read, a note about it, or <see langword="null" />.</param>
/// <param name="Kits">The kits found, in folder order.</param>
/// <param name="Broken">The folders with a style file that cannot be used.</param>
internal sealed record RootScan(
    StyleKitLocation Location,
    string Path,
    string? SharedRoot,
    bool IsAvailable,
    string? Problem,
    IReadOnlyList<ScannedKit> Kits,
    IReadOnlyList<StyleBrokenKit> Broken)
{
    /// <summary>
    ///     Makes the result for a place that could not be read.
    /// </summary>
    /// <param name="location">The kind of place.</param>
    /// <param name="path">The full path of the place.</param>
    /// <param name="sharedRoot">The shared folder, or <see langword="null" />.</param>
    /// <param name="problem">Why it could not be read.</param>
    /// <returns>A result with no kits.</returns>
    public static RootScan Unavailable(StyleKitLocation location, string path, string? sharedRoot, string problem) =>
        new(location, path, sharedRoot, false, problem, [], []);
}

/// <summary>
///     Reads the kits of one place: every sub-folder that has a style file is a kit, and a sub-folder whose style file
///     cannot be used is listed as broken.
/// </summary>
/// <remarks>
///     The scanner does blocking file access, so callers run it on a worker thread, and a place that may not answer (a
///     network folder) with a time limit. Stateless and safe to call from several threads for different places.
/// </remarks>
internal static class StyleKitScanner
{
    /// <summary>
    ///     The most kit folders that are read from one place; a folder with more is read in name order up to this many, so
    ///     that a place with a very large number of folders costs a fixed amount.
    /// </summary>
    public const int MaxKitsPerRoot = 200;

    /// <summary>
    ///     Lists the sub-folders of a folder in name order, leaving out hidden ones whose name starts with a period (which
    ///     is how a folder that is being written is named).
    /// </summary>
    /// <param name="root">The folder to list.</param>
    /// <returns>The full paths of the sub-folders.</returns>
    public static IReadOnlyList<string> ListSubFolders(string root)
    {
        return Directory.EnumerateDirectories(root)
            .Where(folder => !Path.GetFileName(folder).StartsWith('.'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    ///     Reads one place.
    /// </summary>
    /// <param name="location">The kind of place.</param>
    /// <param name="root">The full path of the folder of the place.</param>
    /// <param name="sharedRoot">The shared folder, for a shared place, or <see langword="null" />.</param>
    /// <param name="missingIsProblem">
    ///     <see langword="true" /> when a folder that does not exist is a problem (a shared folder and the kits that come
    ///     with the program); <see langword="false" /> when it is normal (the folders of the user and of a project, which
    ///     are made when the first kit is saved).
    /// </param>
    /// <param name="listSubFolders">Lists the kit folders of a folder; <see cref="ListSubFolders" /> except in tests.</param>
    /// <param name="logger">Optional logger for what is found.</param>
    /// <returns>The kits found and the folders that are broken, or the reason the place could not be read.</returns>
    public static RootScan Scan(
        StyleKitLocation location,
        string root,
        string? sharedRoot,
        bool missingIsProblem,
        Func<string, IReadOnlyList<string>> listSubFolders,
        RollingFileLogger? logger)
    {
        try
        {
            // A place that is not there is empty, or a problem, depending on the place
            if (!Directory.Exists(root))
            {
                return missingIsProblem
                    ? RootScan.Unavailable(location, root, sharedRoot, MissingMessage(location))
                    : new RootScan(location, root, sharedRoot, true, null, [], []);
            }

            var folders = listSubFolders(root);
            var kits = new List<ScannedKit>();
            var broken = new List<StyleBrokenKit>();
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);
            var examined = 0;
            string? note = null;
            foreach (var folder in folders)
            {
                // A folder without a style file is not a kit, whatever else is in it
                if (!File.Exists(Path.Combine(folder, StyleKitSerializer.FileName)))
                {
                    continue;
                }

                if (++examined > MaxKitsPerRoot)
                {
                    note = $"Only the first {MaxKitsPerRoot} kit folders are read.";
                    break;
                }

                var result = StyleKitSerializer.ReadFolder(folder);
                if (!result.Succeeded)
                {
                    broken.Add(new StyleBrokenKit(folder, result.Issues));
                    Log(logger, LogLevel.Warning, $"The style kit folder '{folder}' cannot be used: {string.Join("; ", result.Errors)}");
                    continue;
                }

                // Two folders of one place with the same id cannot both be the kit of that id; the first in name order is
                if (ids.TryGetValue(result.Kit!.Id, out var first))
                {
                    var issue = new StyleIssue("id", $"the id '{result.Kit.Id}' is also used by the kit in '{first}'; only that kit is used.", true);
                    broken.Add(new StyleBrokenKit(folder, [.. result.Issues, issue]));
                    Log(logger, LogLevel.Warning, $"The style kit folder '{folder}' repeats the id of '{first}'.");
                    continue;
                }

                ids[result.Kit.Id] = folder;
                kits.Add(new ScannedKit(result.Kit, folder, [.. result.Warnings]));
            }

            return new RootScan(location, root, sharedRoot, true, note, kits, broken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            Log(logger, LogLevel.Warning, $"The style kit folder '{root}' could not be read: {exception.Message}", exception);
            return RootScan.Unavailable(location, root, sharedRoot, $"The folder could not be read: {exception.Message}");
        }
    }

    /// <summary>
    ///     Gives the sentence about a place that does not exist, for the places where that is a problem.
    /// </summary>
    /// <param name="location">The kind of place.</param>
    /// <returns>The sentence.</returns>
    private static string MissingMessage(StyleKitLocation location) =>
        location == StyleKitLocation.BuiltIn
            ? "The kits that come with the program were not found beside the program."
            : "The folder was not found or cannot be reached.";

    /// <summary>
    ///     Writes a log entry when there is a logger; failing to log never fails a scan.
    /// </summary>
    /// <param name="logger">The logger, or <see langword="null" />.</param>
    /// <param name="level">The severity.</param>
    /// <param name="message">The text.</param>
    /// <param name="exception">The failure, or <see langword="null" />.</param>
    private static void Log(RollingFileLogger? logger, LogLevel level, string message, Exception? exception = null)
    {
        try
        {
            logger?.Log(level, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a problem with a folder worse.
        }
    }
}

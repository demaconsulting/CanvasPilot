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
using System.Text.Json.Serialization;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Keeps the text of the conversation of each project on the computer of the user, in a folder below a root that the
///     caller gives, and never in the project: a project folder may be copied or put under source control.
/// </summary>
/// <remarks>
///     <para>
///         <b>Layout.</b> The history of a project is <c>{root}\{key}\history.json</c>, and next to it
///         <c>project.json</c> names the project and its file for the person who finds the folder (the key is a hash).
///         The key is <see cref="ProjectHistoryKey" />. Nothing is created when a project is opened; the folder appears
///         when the first history is saved.
///     </para>
///     <para>
///         <b>File.</b> <c>history.json</c> is JSON of the form <c>{ "version": 1, "rows": [ ... ] }</c> with rows of
///         <see cref="ChatHistoryRow" />: words only, no pictures, no payloads, and no tool results. Only the newest
///         <see cref="MaxRows" /> rows are kept, and no more than <see cref="MaxBytes" /> bytes of them; the oldest rows
///         are dropped first. A text longer than <see cref="MaxTextCharacters" /> is cut.
///     </para>
///     <para>
///         <b>Writes.</b> A file is written to a temporary file in its folder and then moved over the old one, so a file
///         is the old one or the new one, never half of one; temporary files that a crash left are removed by the next
///         save. A failure to write is a <see cref="ChatHistoryResult" />, never an exception.
///     </para>
///     <para>
///         <b>Bad files.</b> A <c>history.json</c> that is not JSON of this form, or that has a newer version than
///         <see cref="CurrentVersion" />, is not used: it is renamed <c>history.json.bad</c> in the same folder (so that
///         nothing of the user is lost and a newer version of the application can still read it), and the history is
///         empty. The log gets the path and the reason, never a word of a row.
///     </para>
///     <para>
///         <b>Refusal.</b> A root that lies inside the folder of the project is refused: nothing is read, written, or
///         deleted, and the result says so. A configuration mistake must never write into the project.
///     </para>
///     <para>
///         The class is thread-safe; its operations are serialized.
///     </para>
/// </remarks>
public sealed class ChatHistoryStore : IChatHistoryStore
{
    /// <summary>
    ///     The version of the file that this build reads and writes.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    ///     The most rows that the history keeps.
    /// </summary>
    public const int MaxRows = 200;

    /// <summary>
    ///     The most bytes of the history file.
    /// </summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    ///     The most characters of the text of one row; a longer text is cut.
    /// </summary>
    public const int MaxTextCharacters = 100_000;

    /// <summary>
    ///     The name of the file that holds the rows.
    /// </summary>
    public const string HistoryFileName = "history.json";

    /// <summary>
    ///     The name of the file that names the project, for the person who finds the folder.
    /// </summary>
    public const string ProjectFileName = "project.json";

    /// <summary>
    ///     The extension that a bad history file gets.
    /// </summary>
    public const string BadExtension = ".bad";

    /// <summary>
    ///     The sentence for a history folder that lies inside the project; <c>{0}</c> is the folder.
    /// </summary>
    public const string RootInsideProjectMessage =
        "The chat history would be kept in '{0}', which lies inside the project folder, so it was not saved. "
        + "Chat history is kept on this computer outside of every project.";

    /// <summary>
    ///     The largest history file that is read; anything bigger is not a history that this application wrote.
    /// </summary>
    private const long MaxReadBytes = 4L * MaxBytes;

    /// <summary>
    ///     The bytes of the file around the rows, which the limit counts too.
    /// </summary>
    private const int FileOverheadBytes = 64;

    /// <summary>
    ///     The extension of the temporary files.
    /// </summary>
    private const string TemporaryPattern = "*.tmp";

    /// <summary>
    ///     The names and the form of the JSON of the files.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    ///     Serializes the operations.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    ///     Optional logger for the files that cannot be used and the writes that fail.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The clock that dates the sidecar file.
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatHistoryStore" /> class.
    /// </summary>
    /// <param name="historyRoot">The folder below which the history of every project is kept. It is created when the first history is saved.</param>
    /// <param name="logger">Optional logger for the files that cannot be used and the writes that fail.</param>
    /// <param name="clock">The clock, or <see langword="null" /> for the system's.</param>
    /// <exception cref="ArgumentException">Thrown when the root is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the root is <see langword="null" />.</exception>
    public ChatHistoryStore(string historyRoot, RollingFileLogger? logger = null, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyRoot);

        HistoryRoot = Path.GetFullPath(historyRoot);
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    ///     Gets the folder that the history of the user is kept in by default: <c>CanvasPilot\history</c> below the local
    ///     application data of the user (the user profile when there is none).
    /// </summary>
    public static string DefaultHistoryRoot
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                root = ".";
            }

            return PathHelpers.SafePathCombine(root, Path.Combine("CanvasPilot", "history"));
        }
    }

    /// <summary>
    ///     Gets the folder below which the history of every project is kept.
    /// </summary>
    public string HistoryRoot { get; }

    /// <summary>
    ///     Keeps the newest rows that fit the limits: at most <see cref="MaxRows" /> rows and at most
    ///     <see cref="MaxBytes" /> bytes of file, dropping the oldest rows first, and cuts a text that is too long.
    /// </summary>
    /// <param name="rows">The rows, oldest first.</param>
    /// <returns>The rows that are kept, oldest first.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the rows are <see langword="null" />.</exception>
    public static IReadOnlyList<ChatHistoryRow> Cap(IReadOnlyList<ChatHistoryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var start = Math.Max(0, rows.Count - MaxRows);
        var kept = new List<ChatHistoryRow>(rows.Count - start);
        var sizes = new List<int>(rows.Count - start);
        long total = FileOverheadBytes;
        for (var i = start; i < rows.Count; i++)
        {
            var row = CutText(rows[i]);
            var size = JsonSerializer.SerializeToUtf8Bytes(row, JsonOptions).Length + 1;
            kept.Add(row);
            sizes.Add(size);
            total += size;
        }

        // The oldest rows go first until the file fits
        var drop = 0;
        while (drop < kept.Count && total > MaxBytes)
        {
            total -= sizes[drop];
            drop++;
        }

        return drop == 0 ? kept : kept.GetRange(drop, kept.Count - drop);
    }

    /// <inheritdoc />
    public IReadOnlyList<ChatHistoryRow> Load(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        lock (_gate)
        {
            if (IsRefused(project, out _))
            {
                return [];
            }

            var path = HistoryFilePath(project);
            if (!File.Exists(path))
            {
                return [];
            }

            string text;
            try
            {
                if (new FileInfo(path).Length > MaxReadBytes)
                {
                    return Quarantine(path, "is larger than any history that was written");
                }

                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log(LogLevel.Warning, $"The chat history file '{path}' could not be read ({ex.GetType().Name}); it is not used.");
                return [];
            }

            return Parse(path, text);
        }
    }

    /// <inheritdoc />
    public ChatHistoryResult Save(ProjectInfo project, IReadOnlyList<ChatHistoryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rows);

        lock (_gate)
        {
            if (IsRefused(project, out var refusal))
            {
                return ChatHistoryResult.Failed(refusal);
            }

            var folder = FolderOf(project);
            var file = new HistoryFile { Version = CurrentVersion, Rows = [.. Cap(rows)] };
            var sidecar = new ProjectSidecar(project.Name, project.FilePath, _clock.GetUtcNow());
            try
            {
                Directory.CreateDirectory(folder);
                RemoveTemporaryFiles(folder);
                WriteAtomically(folder, HistoryFileName, JsonSerializer.SerializeToUtf8Bytes(file, JsonOptions));
                WriteAtomically(folder, ProjectFileName, JsonSerializer.SerializeToUtf8Bytes(sidecar, JsonOptions));
                return ChatHistoryResult.Ok;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log(LogLevel.Warning, $"The chat history in '{folder}' could not be written ({ex.GetType().Name}).");
                return ChatHistoryResult.Failed($"The chat history could not be saved on this computer: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public ChatHistoryResult Clear(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        lock (_gate)
        {
            if (IsRefused(project, out var refusal))
            {
                return ChatHistoryResult.Failed(refusal);
            }

            var folder = FolderOf(project);
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }

                return ChatHistoryResult.Ok;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log(LogLevel.Warning, $"The chat history in '{folder}' could not be deleted ({ex.GetType().Name}).");
                return ChatHistoryResult.Failed($"The chat history could not be deleted: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Gets the folder that holds the history of a project.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <returns>The full path of the folder, which may not exist.</returns>
    public string FolderOf(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return PathHelpers.SafePathCombine(HistoryRoot, ProjectHistoryKey.Compute(project.FilePath));
    }

    /// <summary>
    ///     Gets the path of the history file of a project.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <returns>The full path of the file, which may not exist.</returns>
    public string HistoryFilePath(ProjectInfo project) => PathHelpers.SafePathCombine(FolderOf(project), HistoryFileName);

    /// <summary>
    ///     Determines whether a root lies inside a folder, or is the folder, by the spelling of their full paths without
    ///     regard to case.
    /// </summary>
    /// <param name="root">The folder that is tested.</param>
    /// <param name="container">The folder that may contain it.</param>
    /// <returns><see langword="true" /> when <paramref name="root" /> is <paramref name="container" /> or below it.</returns>
    internal static bool IsInside(string root, string container)
    {
        var inner = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var outer = Path.TrimEndingDirectorySeparator(Path.GetFullPath(container));
        if (string.Equals(inner, outer, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The separator makes 'proj2' not inside 'proj'
        return inner.StartsWith(outer + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || inner.StartsWith(outer + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Cuts the text of a row that is too long.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The row, or a copy with the text cut.</returns>
    private static ChatHistoryRow CutText(ChatHistoryRow row)
    {
        var text = row.Text ?? string.Empty;
        if (text.Length <= MaxTextCharacters)
        {
            return text == row.Text ? row : row with { Text = text };
        }

        return row with { Text = string.Concat(text.AsSpan(0, MaxTextCharacters - 3), "...") };
    }

    /// <summary>
    ///     Determines whether a row can be used: it has the words or the tool name that its kind needs.
    /// </summary>
    /// <param name="row">The row, as it was read.</param>
    /// <returns><see langword="true" /> when the row is kept.</returns>
    private static bool IsUsable(ChatHistoryRow? row)
    {
        return row is not null
               && Enum.IsDefined(row.Kind)
               && (row.Kind != ChatHistoryRowKind.Tool || !string.IsNullOrWhiteSpace(row.Tool))
               && (row.Kind == ChatHistoryRowKind.Tool || !string.IsNullOrEmpty(row.Text));
    }

    /// <summary>
    ///     Writes a file through a temporary file in the same folder, which is then moved over the old file.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="name">The name of the file.</param>
    /// <param name="content">The bytes.</param>
    private static void WriteAtomically(string folder, string name, byte[] content)
    {
        var temporary = PathHelpers.SafePathCombine(folder, $"{name}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, PathHelpers.SafePathCombine(folder, name), overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>
    ///     Removes the temporary files that a write left when the application ended in the middle of it.
    /// </summary>
    /// <param name="folder">The folder.</param>
    private static void RemoveTemporaryFiles(string folder)
    {
        foreach (var leftover in Directory.EnumerateFiles(folder, TemporaryPattern))
        {
            TryDelete(leftover);
        }
    }

    /// <summary>
    ///     Deletes a file and ignores a failure, because a file that stays is harmless.
    /// </summary>
    /// <param name="path">The file.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temporary file that cannot be deleted now is removed by the next save
        }
    }

    /// <summary>
    ///     Tells whether the history may be kept for a project at all.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="refusal">The sentence that says why not, when it may not.</param>
    /// <returns><see langword="true" /> when the history root lies inside the project folder.</returns>
    private bool IsRefused(ProjectInfo project, out string refusal)
    {
        if (!string.IsNullOrWhiteSpace(project.FolderPath) && IsInside(HistoryRoot, project.FolderPath))
        {
            refusal = string.Format(System.Globalization.CultureInfo.CurrentCulture, RootInsideProjectMessage, HistoryRoot);
            Log(LogLevel.Warning, $"The chat history root '{HistoryRoot}' lies inside a project folder; nothing is read, written, or deleted.");
            return true;
        }

        refusal = string.Empty;
        return false;
    }

    /// <summary>
    ///     Reads the rows from the text of a history file, or sets the file aside when it cannot be used.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="text">What the file holds.</param>
    /// <returns>The rows that the file holds, oldest first.</returns>
    private IReadOnlyList<ChatHistoryRow> Parse(string path, string text)
    {
        HistoryFile? file;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var number)
                || number < 1)
            {
                return Quarantine(path, "is not a chat history");
            }

            if (number > CurrentVersion)
            {
                return Quarantine(path, $"has the newer version {number}");
            }

            file = root.Deserialize<HistoryFile>(JsonOptions);
        }
        catch (JsonException ex)
        {
            return Quarantine(path, $"is not valid ({ex.GetType().Name})");
        }

        if (file?.Rows is null)
        {
            return Quarantine(path, "has no rows");
        }

        return Cap([.. file.Rows.Where(IsUsable).Select(row => row!)]);
    }

    /// <summary>
    ///     Renames a history file that cannot be used, so that it is kept but no longer read.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="reason">What is wrong with it, without any of its words.</param>
    /// <returns>No rows.</returns>
    private IReadOnlyList<ChatHistoryRow> Quarantine(string path, string reason)
    {
        var bad = path + BadExtension;
        try
        {
            File.Move(path, bad, overwrite: true);
            Log(LogLevel.Warning, $"The chat history file '{path}' {reason}; it was renamed to '{bad}' and the history is empty.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Warning, $"The chat history file '{path}' {reason} and could not be renamed ({ex.GetType().Name}); it is not used.");
        }

        return [];
    }

    /// <summary>
    ///     Writes to the log, if there is one.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <param name="message">The message, which holds no words of a row.</param>
    private void Log(LogLevel level, string message) => _logger?.Log(level, message);

    /// <summary>
    ///     The form of <c>history.json</c>.
    /// </summary>
    private sealed class HistoryFile
    {
        /// <summary>
        ///     Gets or sets the version of the file.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        ///     Gets or sets the rows, oldest first.
        /// </summary>
        public List<ChatHistoryRow?>? Rows { get; set; }
    }

    /// <summary>
    ///     The form of <c>project.json</c>, which tells a person to which project a history folder belongs.
    /// </summary>
    /// <param name="ProjectName">The name of the project.</param>
    /// <param name="ProjectFile">The full path of the project file that the key was made from.</param>
    /// <param name="LastUsedUtc">When the history was last saved.</param>
    private sealed record ProjectSidecar(string ProjectName, string ProjectFile, DateTimeOffset LastUsedUtc);
}

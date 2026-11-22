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

using System.ComponentModel;
using System.Globalization;
using System.Text;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The tool pack with the tools that list, read, and write the files of the project: <c>project_list</c>,
///     <c>project_read</c>, and <c>project_write</c>.
/// </summary>
/// <remarks>
///     <para>
///         Every path goes through <see cref="ProjectPathGuard" /> with the path policy that the pack is given, so no tool
///         can leave the project folder, touch the data folder or the project file, or pass through a link.
///         <c>project_write</c> adds its own rules: only text image sources and small data files, at most
///         <see cref="MaxWriteBytes" /> bytes, and never a file below the <c>styles</c> folder of the project, where the
///         style kits are, because a kit is the user's and not the copilot's.
///     </para>
///     <para>
///         <b>No backup and no prompt.</b> <c>project_write</c> creates or replaces a file directly. That is a decision of
///         the user, who keeps drawings in source control or copies them; the application keeps no backup folder and asks
///         nothing. The answer says whether the file was <c>created</c> or <c>replaced</c>, with the sizes, and the event
///         <see cref="ProjectTools.FileWritten" /> lets the shell show the file, so the change is visible and not hidden. The file is
///         written to a temporary file in the same folder and moved over the old one, so a failure never leaves half a
///         file, and a hard link to the old file is replaced and not written through.
///     </para>
///     <para>
///         A tool never throws: a refusal or a failure is an answer that the model can read and correct itself from. The
///         text of an exception is not repeated, only its type, because it can hold a path of the user's computer. The
///         pack is safe for concurrent use; two writes of one file at once are two writes, and the last one stays.
///     </para>
/// </remarks>
internal sealed class ProjectFilesToolPack : IToolPack
{
    /// <summary>
    ///     The most bytes that <c>project_write</c> writes into one file: 256 KiB.
    /// </summary>
    internal const int MaxWriteBytes = 256 * 1024;

    /// <summary>
    ///     The most files that <c>project_list</c> lists in one answer.
    /// </summary>
    internal const int MaxListedFiles = 200;

    /// <summary>
    ///     The most folders that <c>project_list</c> looks into in one answer, so a tree of empty folders cannot keep it busy.
    /// </summary>
    private const int MaxVisitedFolders = 2000;

    /// <summary>
    ///     The name of the tool that lists files.
    /// </summary>
    internal const string ListToolName = "project_list";

    /// <summary>
    ///     The name of the tool that reads a file.
    /// </summary>
    internal const string ReadToolName = "project_read";

    /// <summary>
    ///     The name of the tool that writes a file.
    /// </summary>
    internal const string WriteToolName = "project_write";

    /// <summary>
    ///     The encoding of the files: UTF-8 without a byte order mark, and an error for bytes that are not UTF-8.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    ///     The three bytes that some programs put at the start of a UTF-8 file; they are not part of its text.
    /// </summary>
    private static readonly byte[] Utf8ByteOrderMark = [0xEF, 0xBB, 0xBF];

    /// <summary>
    ///     Tells which files are text files and which are pictures.
    /// </summary>
    private readonly ProjectFileKinds _kinds;

    /// <summary>
    ///     Receives the call lines of the log.
    /// </summary>
    private readonly ToolCallLog _log;

    /// <summary>
    ///     Called after a file was written.
    /// </summary>
    private readonly Action<FileWrittenEventArgs> _fileWritten;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectFilesToolPack" /> class.
    /// </summary>
    /// <param name="kinds">Tells which files are text files and which are pictures.</param>
    /// <param name="log">Receives the metadata of every call.</param>
    /// <param name="fileWritten">Called after a file was created or replaced.</param>
    internal ProjectFilesToolPack(ProjectFileKinds kinds, ToolCallLog log, Action<FileWrittenEventArgs> fileWritten)
    {
        _kinds = kinds;
        _log = log;
        _fileWritten = fileWritten;
    }

    /// <inheritdoc />
    public string FamilyPrefix => ProjectTools.ProjectPrefix;

    /// <inheritdoc />
    public HostCapabilities RequiredCapabilities => HostCapabilities.None;

    /// <inheritdoc />
    public IEnumerable<AIFunction> CreateTools(PathPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        // The tools close over the policy of the project, so one pack object serves any number of projects
        return
        [
            GuardedToolFactory.Create(
                ([Description("A folder of the project, relative to the project folder, such as 'drawings'. Leave out for the whole project.")] string? folder = null)
                    => List(policy, folder),
                ListToolName,
                "Lists the files of the project, with their relative paths, sizes, and whether CanvasNet can draw them (image source). Lists at most "
                + MaxListedFiles.ToString(CultureInfo.InvariantCulture) + " files; give a folder to see more.").AsGuarded(policy.Limits),
            GuardedToolFactory.Create(
                ([Description("The path of the file, relative to the project folder, such as 'cover.svg'.")] string path)
                    => Read(policy, path),
                ReadToolName,
                "Reads a text file of the project as UTF-8 text: an image source such as SVG, Mermaid, or Vega-Lite, or a .csv, .json, or .txt file. Files over "
                + (policy.Limits.MaxReadBytes / 1024).ToString(CultureInfo.InvariantCulture)
                + " KiB are refused. To see a picture, use render_image.").AsGuarded(policy.Limits),
            GuardedToolFactory.Create(
                ([Description("The path of the file, relative to the project folder, such as 'cover.svg' or 'data/sales.csv'. Folders are created as needed.")] string path,
                    [Description("The complete new text of the file, which replaces the old text if the file exists.")] string content)
                    => Write(policy, path, content),
                WriteToolName,
                "Creates a text file of the project, or replaces it with the complete new text. The old text is not kept, so read the file first and keep what the user did not ask you to change. Allowed: "
                + ProjectFileKinds.DescribeTextFiles() + ", at most "
                + (MaxWriteBytes / 1024).ToString(CultureInfo.InvariantCulture) + " KiB. Files in the styles folder (style kits) cannot be written.").AsGuarded(policy.Limits),
        ];
    }

    /// <summary>
    ///     Lists the files of the project, or of one folder of it.
    /// </summary>
    /// <param name="policy">The path policy of the project.</param>
    /// <param name="folder">The folder to list, relative to the project folder; <see langword="null" /> or empty for the project.</param>
    /// <returns>The answer for the model.</returns>
    private object List(PathPolicy policy, string? folder)
    {
        try
        {
            // The start is the project folder itself, or a folder of it that passes the same checks as any path
            var root = Path.TrimEndingDirectorySeparator(policy.WorkingDirectory);
            var start = root;
            var prefix = string.Empty;
            if (!string.IsNullOrWhiteSpace(folder) && folder.Trim() != ".")
            {
                if (!ProjectPathGuard.TryResolve(policy, folder, false, out start, out var relative, out var refusal))
                {
                    _log.Record(ListToolName, null, null, ToolCallLog.Refused);
                    return ToolResult.Denied(DenialReason.PathNotPermitted, refusal);
                }

                prefix = relative + "/";
            }

            if (!Directory.Exists(start))
            {
                _log.Record(ListToolName, prefix.TrimEnd('/'), null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.TargetNotFound, "There is no such folder in the project.");
            }

            // Collect the files in a fixed order, and stop at the ceiling so that a large project cannot flood the answer
            var found = new List<(string Path, long Size, bool IsImage)>();
            var visited = 0;
            var truncated = Collect(new DirectoryInfo(start), prefix, found, ref visited);

            _log.Record(ListToolName, prefix.TrimEnd('/'), found.Count, ToolCallLog.Done);
            return ToolResult.Text(Describe(prefix, found, truncated));
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            _log.Record(ListToolName, null, null, ToolCallLog.Failed);
            return ToolResult.Text($"The files could not be listed ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    ///     Reads a text file of the project.
    /// </summary>
    /// <param name="policy">The path policy of the project.</param>
    /// <param name="path">The path of the file, relative to the project folder.</param>
    /// <returns>The text of the file, or the reason it was not read.</returns>
    private object Read(PathPolicy policy, string? path)
    {
        try
        {
            if (!ProjectPathGuard.TryResolve(policy, path, false, out var full, out var relative, out var refusal))
            {
                _log.Record(ReadToolName, null, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.PathNotPermitted, refusal);
            }

            // Only the files that a drawing is made of are read: text sources and small data files
            if (!File.Exists(full))
            {
                _log.Record(ReadToolName, relative, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.TargetNotFound, $"There is no file '{relative}' in the project. Use project_list to see the files.");
            }

            if (_kinds.IsBinaryImage(full))
            {
                _log.Record(ReadToolName, relative, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.UnsupportedMediaType, $"'{relative}' is a picture, not text. Use render_image to see it.");
            }

            if (!_kinds.IsTextFile(full))
            {
                _log.Record(ReadToolName, relative, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.UnsupportedMediaType, $"Only {ProjectFileKinds.DescribeTextFiles()} can be read.");
            }

            // The size is checked before the bytes are read, and the read itself is bounded in case the file grows
            var limit = policy.Limits.MaxReadBytes;
            var length = new FileInfo(full).Length;
            if (length > limit)
            {
                _log.Record(ReadToolName, relative, length, ToolCallLog.Refused);
                return ToolResult.Denied(
                    DenialReason.ResourceTooLarge,
                    $"'{relative}' is {Kib(length)} KiB; files over {Kib(limit)} KiB cannot be read.");
            }

            var bytes = ReadBounded(full, limit);
            if (bytes is null)
            {
                _log.Record(ReadToolName, relative, length, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.ResourceTooLarge, $"'{relative}' is larger than {Kib(limit)} KiB and cannot be read.");
            }

            string text;
            try
            {
                // A byte order mark is not part of the text, so it is left out
                var start = bytes.AsSpan().StartsWith(Utf8ByteOrderMark) ? Utf8ByteOrderMark.Length : 0;
                text = StrictUtf8.GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                _log.Record(ReadToolName, relative, bytes.Length, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.UnsupportedMediaType, $"'{relative}' is not UTF-8 text.");
            }

            // The text may need more characters than bytes allow for in the answer; the library would refuse it, so say so first
            if (text.Length > policy.Limits.MaxResultCharacters)
            {
                _log.Record(ReadToolName, relative, bytes.Length, ToolCallLog.Refused);
                return ToolResult.Denied(
                    DenialReason.ResourceTooLarge,
                    $"'{relative}' has {text.Length} characters; an answer can hold at most {policy.Limits.MaxResultCharacters}.");
            }

            _log.Record(ReadToolName, relative, bytes.Length, ToolCallLog.Done);
            return ToolResult.Text(text.Length == 0 ? "(The file is empty.)" : text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            _log.Record(ReadToolName, null, null, ToolCallLog.Failed);
            return ToolResult.Text($"The file could not be read ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    ///     Creates or replaces a text file of the project, directly: there is no backup and no question.
    /// </summary>
    /// <param name="policy">The path policy of the project.</param>
    /// <param name="path">The path of the file, relative to the project folder.</param>
    /// <param name="content">The complete text of the file.</param>
    /// <returns>The answer for the model: that the file was created or replaced, or the reason it was not written.</returns>
    private object Write(PathPolicy policy, string? path, string? content)
    {
        try
        {
            // A call without a text is a mistake of the model, and the old file must survive it
            if (content is null)
            {
                _log.Record(WriteToolName, null, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.InvalidRequest, "The content is missing. Send the complete text of the file.");
            }

            if (!ProjectPathGuard.TryResolve(policy, path, true, out var full, out var relative, out var refusal))
            {
                _log.Record(WriteToolName, null, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.PathNotPermitted, refusal);
            }

            // The style kits of the project are the user's; the copilot may read them and may not change them
            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0 && string.Equals(segments[0], StyleKitCatalog.ProjectFolderName, StringComparison.OrdinalIgnoreCase))
            {
                _log.Record(WriteToolName, relative, null, ToolCallLog.Refused);
                return ToolResult.Denied(
                    DenialReason.PathNotPermitted,
                    $"'{relative}' is in the '{StyleKitCatalog.ProjectFolderName}' folder, which holds the style kits of the user. Style kit files cannot be written.");
            }

            if (!_kinds.IsTextFile(full))
            {
                _log.Record(WriteToolName, relative, null, ToolCallLog.Refused);
                return ToolResult.Denied(
                    DenialReason.UnsupportedMediaType,
                    $"'{relative}' cannot be written. Allowed: {ProjectFileKinds.DescribeTextFiles()}.");
            }

            var bytes = StrictUtf8.GetBytes(content);
            if (bytes.Length > MaxWriteBytes)
            {
                _log.Record(WriteToolName, relative, bytes.Length, ToolCallLog.Refused);
                return ToolResult.Denied(
                    DenialReason.ResourceTooLarge,
                    $"The content is {Kib(bytes.Length)} KiB; a file can be at most {Kib(MaxWriteBytes)} KiB.");
            }

            if (Directory.Exists(full))
            {
                _log.Record(WriteToolName, relative, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.InvalidRequest, $"'{relative}' is a folder, not a file.");
            }

            // Replace or create, and say which: the answer, the log, and the event all carry it
            var existed = File.Exists(full);
            var oldLength = existed ? new FileInfo(full).Length : 0;
            ReplaceFile(full, bytes);

            _log.Record(WriteToolName, relative, bytes.Length, ToolCallLog.Done);
            RaiseFileWritten(new FileWrittenEventArgs(relative, !existed));
            return ToolResult.Text(existed
                ? $"File '{relative}' replaced ({oldLength.ToString("N0", CultureInfo.InvariantCulture)} bytes -> {bytes.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes). Call render_image to check the result."
                : $"File '{relative}' created ({bytes.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes). Call render_image to check the result.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            _log.Record(WriteToolName, null, null, ToolCallLog.Failed);
            return ToolResult.Text($"The file could not be written ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    ///     Writes the bytes to a file by way of a temporary file in the same folder, which is then moved over the file.
    /// </summary>
    /// <remarks>
    ///     A failure part way leaves the old file as it was and removes the temporary file. Moving over the old file
    ///     replaces the name and not the content of a hard link, so another name of the same content (possibly outside
    ///     the project) is never written through. Folders that are missing are created; they are below the project folder
    ///     because the path was checked, and no part of the path was a link.
    /// </remarks>
    /// <param name="full">The full path of the file inside the project.</param>
    /// <param name="bytes">The bytes to write.</param>
    private static void ReplaceFile(string full, byte[] bytes)
    {
        var folder = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }

            File.Move(temp, full, true);
        }
        catch
        {
            // Nothing is left behind when the write fails
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // The file system is not cooperating; the original error is what matters
            }
            catch (UnauthorizedAccessException)
            {
                // The same
            }

            throw;
        }
    }

    /// <summary>
    ///     Tells the shell that a file was written, without letting a failing listener fail the tool.
    /// </summary>
    /// <param name="args">What was written.</param>
    private void RaiseFileWritten(FileWrittenEventArgs args)
    {
        try
        {
            _fileWritten(args);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The file is written; a listener that fails cannot undo that, and the tool must still answer
            _log.Record("file_written_listener", args.RelativePath, null, ToolCallLog.Failed);
        }
    }

    /// <summary>
    ///     Reads a file and refuses it when it has more bytes than a limit.
    /// </summary>
    /// <param name="full">The full path of the file.</param>
    /// <param name="limit">The most bytes to read.</param>
    /// <returns>The bytes, or <see langword="null" /> when the file has more than the limit.</returns>
    private static byte[]? ReadBounded(string full, int limit)
    {
        using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[limit + 1];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += read;
        }

        return total > limit ? null : buffer[..total];
    }

    /// <summary>
    ///     Collects the files below a folder in a fixed order, files before folders, without following links and without the
    ///     files that belong to the application.
    /// </summary>
    /// <param name="folder">The folder to look into.</param>
    /// <param name="prefix">The path of the folder below the project folder, with a trailing slash, or empty.</param>
    /// <param name="found">Receives the files.</param>
    /// <param name="visited">The number of folders looked into so far; updated.</param>
    /// <returns><see langword="true" /> when the collection stopped at a ceiling before it had seen everything.</returns>
    private bool Collect(DirectoryInfo folder, string prefix, List<(string Path, long Size, bool IsImage)> found, ref int visited)
    {
        if (++visited > MaxVisitedFolders)
        {
            return true;
        }

        FileSystemInfo[] items;
        try
        {
            items = folder.GetFileSystemInfos();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be read has nothing to list
            return false;
        }

        var visible = items.Where(IsVisible).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();

        // Files of the folder first, so the top of a project is never pushed out by the depth of a subfolder
        foreach (var file in visible.OfType<FileInfo>())
        {
            if (found.Count >= MaxListedFiles)
            {
                return true;
            }

            found.Add((prefix + file.Name, file.Length, _kinds.IsImageSource(file.Name)));
        }

        foreach (var directory in visible.OfType<DirectoryInfo>())
        {
            if (Collect(directory, prefix + directory.Name + "/", found, ref visited))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether a file or folder is listed: not hidden by the system, not a link, not the data folder or a project
    ///     file, and not a folder whose name starts with a dot (<c>.git</c> and the like).
    /// </summary>
    /// <param name="item">The file or folder.</param>
    /// <returns><see langword="true" /> when it is listed.</returns>
    private static bool IsVisible(FileSystemInfo item)
    {
        if ((item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
        {
            return false;
        }

        var isFolder = item is DirectoryInfo;
        if (string.Equals(item.Name, ProjectDocument.DataFolderName, StringComparison.OrdinalIgnoreCase)
            || item.Name.EndsWith(ProjectDocument.FileExtension, StringComparison.OrdinalIgnoreCase)
            || (isFolder && item.Name.StartsWith('.')))
        {
            return false;
        }

        return !ProjectPathGuard.IsLink(item.FullName);
    }

    /// <summary>
    ///     Writes the answer of the listing.
    /// </summary>
    /// <param name="prefix">The folder that was listed, with a trailing slash, or empty for the project.</param>
    /// <param name="found">The files.</param>
    /// <param name="truncated">Whether the listing stopped at a ceiling.</param>
    /// <returns>The text.</returns>
    private static string Describe(string prefix, List<(string Path, long Size, bool IsImage)> found, bool truncated)
    {
        var text = new StringBuilder();
        var where = prefix.Length == 0 ? "the project" : $"'{prefix.TrimEnd('/')}'";
        if (found.Count == 0)
        {
            return $"There are no files in {where}.";
        }

        text.Append(CultureInfo.InvariantCulture, $"Files in {where} ({found.Count}):\n");
        foreach (var (path, size, isImage) in found)
        {
            text.Append(CultureInfo.InvariantCulture, $"- {path} ({size:N0} bytes{(isImage ? ", image source" : string.Empty)})\n");
        }

        if (truncated)
        {
            text.Append("The list is not complete. Give a folder to list a part of the project.\n");
        }

        return text.ToString();
    }

    /// <summary>
    ///     Writes a number of bytes as kibibytes, rounded up.
    /// </summary>
    /// <param name="bytes">The number of bytes.</param>
    /// <returns>The number of KiB as text.</returns>
    private static string Kib(long bytes)
    {
        return ((bytes + 1023) / 1024).ToString(CultureInfo.InvariantCulture);
    }
}

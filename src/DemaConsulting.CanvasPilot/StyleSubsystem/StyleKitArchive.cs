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

using System.IO.Compression;
using System.Text;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Writes a style kit as a zip file and reads a kit from a zip file that may come from anywhere.
/// </summary>
/// <remarks>
///     <para>
///         The zip holds <c>style.json</c> and the files of <c>examples/</c> at its top; an archive whose files are all in
///         one folder is also read. The import treats the archive as hostile, because it is sent by mail or downloaded: the
///         names of all entries are checked before anything is written (no absolute or drive path, no backslash, no
///         <c>.</c> or <c>..</c>, no character that a Windows file name cannot have, no device name, no link, and nothing
///         but <c>style.json</c> and files directly in <c>examples/</c>), the number of entries and their sizes are
///         limited, the sizes are counted while the bytes are copied and not read from the headers, which can lie, the bytes
///         that are copied must match the size and the CRC-32 of their header, every
///         destination is joined to the folder with the safe combination, and the files are written into a hidden folder
///         of the target that is moved into place only when the style file is valid and removed on every other outcome.
///     </para>
///     <para>
///         Stateless and safe to call from several threads for different files. It uses only <c>System.IO.Compression</c>.
///     </para>
/// </remarks>
public static class StyleKitArchive
{
    /// <summary>
    ///     The most entries that an archive may have, with the folders.
    /// </summary>
    public const int MaxEntries = 100;

    /// <summary>
    ///     The largest file in an archive, in bytes, counted as it is copied.
    /// </summary>
    public const long MaxEntryBytes = 16L * 1024 * 1024;

    /// <summary>
    ///     The largest total of the files of an archive, in bytes, counted as they are copied.
    /// </summary>
    public const long MaxTotalBytes = 64L * 1024 * 1024;

    /// <summary>
    ///     The longest name of an entry, in characters.
    /// </summary>
    private const int MaxNameLength = 200;

    /// <summary>
    ///     The longest part of the name of an entry between two slashes, in characters.
    /// </summary>
    private const int MaxSegmentLength = 100;

    /// <summary>
    ///     The size of the buffer used to copy a file out of an archive.
    /// </summary>
    private const int CopyBufferBytes = 81920;

    /// <summary>
    ///     The characters that no part of a name may hold, besides the control characters; they are those that a Windows
    ///     file name cannot have, which the application would otherwise write on one platform and not on another.
    /// </summary>
    private const string InvalidNameCharacters = ":*?\"<>|";

    /// <summary>
    ///     The table of the CRC-32 that a zip file keeps of each file.
    /// </summary>
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>
    ///     The names that Windows reserves for devices; a file with such a name, with or without an extension, cannot be
    ///     made.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    ///     Writes a kit as a zip file with <c>style.json</c> and the example files of the kit in <c>examples/</c>.
    /// </summary>
    /// <remarks>
    ///     The archive is written to a temporary file next to the target and moved over it, so a failure leaves an existing
    ///     file as it was. The style file is the canonical text of the kit, so an archive of the same kit is the same
    ///     content.
    /// </remarks>
    /// <param name="entry">The kit to export.</param>
    /// <param name="zipPath">The path of the zip file to write; an existing file is replaced.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="zipPath" /> is empty or blank.</exception>
    /// <exception cref="IOException">Thrown when a file of the kit cannot be read or the archive cannot be written.</exception>
    public static void Export(StyleKitEntry entry, string zipPath)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);

        var target = Path.GetFullPath(zipPath);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                // The style file first, then every example that the kit lists, under the names that the import expects
                var style = archive.CreateEntry(StyleKitSerializer.FileName, CompressionLevel.Optimal);
                using (var writer = new StreamWriter(style.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(StyleKitSerializer.Write(entry.Kit));
                }

                foreach (var example in entry.Kit.Examples)
                {
                    var name = Path.GetFileName(example.RelativePath);
                    var source = PathHelpers.SafePathCombine(
                        PathHelpers.SafePathCombine(entry.FolderPath, StyleKitSerializer.ExamplesFolderName),
                        name);
                    archive.CreateEntryFromFile(source, $"{StyleKitSerializer.ExamplesFolderName}/{name}", CompressionLevel.Optimal);
                }
            }

            File.Move(temp, target, overwrite: true);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            TryDeleteFile(temp);
            throw new IOException($"The kit could not be exported to '{target}': {exception.Message}", exception);
        }
        catch
        {
            TryDeleteFile(temp);
            throw;
        }
    }

    /// <summary>
    ///     Reads a kit from a zip file into a new folder of the target folder.
    /// </summary>
    /// <remarks>
    ///     Nothing is written until every name and the size of every entry that the headers claim have been checked. The
    ///     files are then written to a hidden folder of the target, the style file is read with the rules of
    ///     <see cref="StyleKitSerializer" />, and only a valid kit is moved to the folder named as its id. A failure at any
    ///     point removes the hidden folder, so the target holds either the whole kit or nothing of it. The method never
    ///     throws for a bad archive or a failure to write.
    /// </remarks>
    /// <param name="zipPath">The path of the zip file.</param>
    /// <param name="targetRoot">The folder that holds the kits of the place the kit is imported to; it is made when needed.</param>
    /// <param name="onConflict">What to do when the target already has a kit with the id of the imported kit.</param>
    /// <returns>The kit and its folder, or the reason that nothing was imported.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="zipPath" /> or <paramref name="targetRoot" /> is empty or blank.</exception>
    public static StyleImportResult Import(string zipPath, string targetRoot, StyleImportConflict onConflict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);

        string? staging = null;
        try
        {
            var root = Path.GetFullPath(targetRoot);
            using var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            // Check every name and every size that the headers claim before anything is written
            var plan = Plan(archive);
            if (plan.Error is not null)
            {
                return Failure(plan.Error);
            }

            // Write the files into a hidden folder of the target, counting the bytes that are really copied
            Directory.CreateDirectory(root);
            staging = PathHelpers.SafePathCombine(root, $".import-{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(staging);
            var copyError = Extract(plan.Files, staging);
            if (copyError is not null)
            {
                return Failure(copyError);
            }

            // Read the style file with the rules of the format; a kit with an error is not imported
            var read = StyleKitSerializer.ReadFolder(staging);
            var issues = read.Issues.Where(issue => !IsAboutTheFolderName(issue)).ToList();
            if (!read.Succeeded)
            {
                return new StyleImportResult(null, null, "The style file in the zip file has errors: " + string.Join(" ", read.Errors), issues);
            }

            return Place(read.Kit!, staging, root, onConflict, issues);
        }
        catch (InvalidDataException exception)
        {
            return Failure($"The file is not a usable zip file: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return Failure($"The kit could not be imported: {exception.Message}");
        }
        finally
        {
            if (staging is not null)
            {
                TryDeleteFolder(staging);
            }
        }
    }

    /// <summary>
    ///     Checks the names, the number, the kinds, and the claimed sizes of the entries of an archive and works out which
    ///     files to extract and where.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <returns>The files to extract with their names in the kit, or the reason the archive is refused.</returns>
    private static (string? Error, List<(ZipArchiveEntry Entry, string Name)> Files) Plan(ZipArchive archive)
    {
        var none = new List<(ZipArchiveEntry, string)>();
        if (archive.Entries.Count > MaxEntries)
        {
            return ($"The zip file has {archive.Entries.Count} entries; the limit is {MaxEntries}.", none);
        }

        // Every name is checked, including those of folders, because a name that climbs out is an attack in either
        var files = new List<(ZipArchiveEntry Entry, string Name)>();
        long claimed = 0;
        foreach (var entry in archive.Entries)
        {
            var problem = NameProblem(entry.FullName);
            if (problem is not null)
            {
                return ($"The zip file has an entry '{Printable(entry.FullName)}' that {problem}; nothing was imported.", none);
            }

            // A link would be written as a file that holds the name of its target, which is not what its maker meant, and
            // a kit has no use for one
            if (IsLink(entry))
            {
                return ($"The zip file has an entry '{Printable(entry.FullName)}' that is a link; nothing was imported.", none);
            }

            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (entry.Length > MaxEntryBytes)
            {
                return ($"The zip file has an entry '{Printable(entry.FullName)}' of {entry.Length} bytes; the limit is {MaxEntryBytes}.", none);
            }

            claimed += entry.Length;
            files.Add((entry, entry.FullName));
        }

        if (claimed > MaxTotalBytes)
        {
            return ($"The files of the zip file are {claimed} bytes together; the limit is {MaxTotalBytes}.", none);
        }

        return Layout(files);
    }

    /// <summary>
    ///     Finds the layout of the files: <c>style.json</c> and <c>examples/</c> at the top, or all in one folder, and refuses
    ///     any file that is not a part of a kit.
    /// </summary>
    /// <param name="files">The file entries with their names in the archive.</param>
    /// <returns>The files with their names in the kit, or the reason the archive is refused.</returns>
    private static (string? Error, List<(ZipArchiveEntry Entry, string Name)> Files) Layout(List<(ZipArchiveEntry Entry, string Name)> files)
    {
        var none = new List<(ZipArchiveEntry, string)>();

        // One wrapper folder is accepted, when the style file is directly in it and there is none at the top
        var prefix = string.Empty;
        if (!files.Any(f => f.Name == StyleKitSerializer.FileName) && files.Count > 0)
        {
            var first = files[0].Name.Split('/')[0];
            if (files.All(f => f.Name.StartsWith(first + "/", StringComparison.Ordinal))
                && files.Any(f => f.Name == $"{first}/{StyleKitSerializer.FileName}"))
            {
                prefix = first + "/";
            }
        }

        var result = new List<(ZipArchiveEntry, string)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var examples = 0;
        foreach (var (entry, rawName) in files)
        {
            var name = rawName[prefix.Length..];
            var isExample = name.StartsWith(StyleKitSerializer.ExamplesFolderName + "/", StringComparison.Ordinal)
                            && name.Length > StyleKitSerializer.ExamplesFolderName.Length + 1
                            && name.IndexOf('/', StyleKitSerializer.ExamplesFolderName.Length + 1) < 0;
            if (name != StyleKitSerializer.FileName && !isExample)
            {
                return ($"The zip file has a file '{Printable(rawName)}' that is not part of a kit: a kit has {StyleKitSerializer.FileName} and files directly in {StyleKitSerializer.ExamplesFolderName}/; nothing was imported.", none);
            }

            if (!seen.Add(name))
            {
                return ($"The zip file has the file '{Printable(name)}' more than once; nothing was imported.", none);
            }

            examples += isExample ? 1 : 0;
            result.Add((entry, name));
        }

        if (!seen.Contains(StyleKitSerializer.FileName))
        {
            return ($"The zip file has no {StyleKitSerializer.FileName}; it is not a style kit.", none);
        }

        return examples > StyleKitSerializer.MaxExamples
            ? ($"The zip file has {examples} example files; the limit is {StyleKitSerializer.MaxExamples}.", none)
            : (null, result);
    }

    /// <summary>
    ///     Copies the files of the plan into a folder, counting the bytes that are really read from the archive.
    /// </summary>
    /// <param name="files">The files and their names in the kit.</param>
    /// <param name="folder">The hidden folder that the files are written to.</param>
    /// <returns>The reason a file was refused, or <see langword="null" /> when every file was copied.</returns>
    private static string? Extract(List<(ZipArchiveEntry Entry, string Name)> files, string folder)
    {
        long total = 0;
        var buffer = new byte[CopyBufferBytes];
        Directory.CreateDirectory(PathHelpers.SafePathCombine(folder, StyleKitSerializer.ExamplesFolderName));
        foreach (var (entry, name) in files)
        {
            var destination = PathHelpers.SafePathCombine(folder, name);
            long written = 0;
            var crc = 0xFFFFFFFFu;
            using var source = entry.Open();
            using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                // The headers of an archive can claim a small size for a large file, so the count is of what is copied
                written += read;
                total += read;
                if (written > MaxEntryBytes)
                {
                    return $"The zip file has an entry '{Printable(entry.FullName)}' that is larger than {MaxEntryBytes} bytes; nothing was imported.";
                }

                if (total > MaxTotalBytes)
                {
                    return $"The files of the zip file are larger than {MaxTotalBytes} bytes together; nothing was imported.";
                }

                crc = UpdateCrc(crc, buffer.AsSpan(0, read));
                target.Write(buffer, 0, read);
            }

            // The framework stops reading at the size in the header, so a header that claims too little gives a short file;
            // the checksum of what was read must be the one of the header, or the archive is damaged or made to mislead
            if (written != entry.Length || (crc ^ 0xFFFFFFFFu) != entry.Crc32)
            {
                return $"The entry '{Printable(entry.FullName)}' of the zip file does not match its header; the file is damaged. Nothing was imported.";
            }
        }

        return null;
    }

    /// <summary>
    ///     Updates a running CRC-32, the checksum that a zip file keeps of each file.
    /// </summary>
    /// <param name="crc">The running value, which starts as all ones and is inverted at the end.</param>
    /// <param name="data">The next bytes.</param>
    /// <returns>The new running value.</returns>
    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    /// <summary>
    ///     Makes the table of the CRC-32 with the reflected polynomial 0xEDB88320.
    /// </summary>
    /// <returns>The 256 entries.</returns>
    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var bit = 0; bit < 8; bit++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>
    ///     Moves the staged kit to the folder named as its id, as the conflict setting says, and reads it from there.
    /// </summary>
    /// <param name="kit">The kit that was read from the staging folder.</param>
    /// <param name="staging">The staging folder.</param>
    /// <param name="root">The folder that holds the kits of the target.</param>
    /// <param name="onConflict">What to do when the id is taken.</param>
    /// <param name="issues">The warnings of the kit.</param>
    /// <returns>The imported kit, or the reason it was not imported.</returns>
    private static StyleImportResult Place(StyleKit kit, string staging, string root, StyleImportConflict onConflict, List<StyleIssue> issues)
    {
        var id = kit.Id;
        var final = PathHelpers.SafePathCombine(root, id);
        string? backup = null;
        if (Directory.Exists(final) || File.Exists(final))
        {
            switch (onConflict)
            {
                case StyleImportConflict.Refuse:
                    return new StyleImportResult(null, null, $"The place already has a kit with the id '{id}'; nothing was imported.", issues);
                case StyleImportConflict.KeepBoth:
                    // The copy gets a number in its id and its name, and its style file is written again with them
                    var number = 2;
                    while (Directory.Exists(PathHelpers.SafePathCombine(root, $"{id}-{number}")) || File.Exists(PathHelpers.SafePathCombine(root, $"{id}-{number}")))
                    {
                        number++;
                    }

                    var suffix = $" ({number})";
                    var name = kit.Name.Length + suffix.Length > StyleKitSerializer.MaxNameLength
                        ? kit.Name[..(StyleKitSerializer.MaxNameLength - suffix.Length)].TrimEnd() + suffix
                        : kit.Name + suffix;
                    id = $"{id}-{number}";
                    File.WriteAllText(
                        Path.Combine(staging, StyleKitSerializer.FileName),
                        StyleKitSerializer.Write(kit with { Id = id, Name = name }),
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    final = PathHelpers.SafePathCombine(root, id);
                    break;
                case StyleImportConflict.Replace:
                    // The kit that is there is moved aside first and removed only when the new kit is in place
                    backup = PathHelpers.SafePathCombine(root, $".{id}.{Guid.NewGuid():N}.old");
                    Directory.Move(final, backup);
                    break;
            }
        }

        try
        {
            Directory.Move(staging, final);
        }
        catch
        {
            if (backup is not null && !Directory.Exists(final))
            {
                Directory.Move(backup, final);
            }

            throw;
        }

        if (backup is not null)
        {
            TryDeleteFolder(backup);
        }

        // Read the kit from its place, so that the paths of its examples are those of the place
        var placed = StyleKitSerializer.ReadFolder(final);
        return placed.Succeeded
            ? new StyleImportResult(placed.Kit, final, null, [.. placed.Warnings])
            : new StyleImportResult(null, null, "The imported kit cannot be read: " + string.Join(" ", placed.Errors), [.. placed.Issues]);
    }

    /// <summary>
    ///     Tells whether an issue is the warning that the folder of the kit is named differently than its id, which is true
    ///     of the hidden folder that an archive is read in and says nothing about the archive.
    /// </summary>
    /// <param name="issue">The issue.</param>
    /// <returns><see langword="true" /> for that warning.</returns>
    private static bool IsAboutTheFolderName(StyleIssue issue) =>
        !issue.IsError && issue.Path == "id" && issue.Message.StartsWith("the folder is named", StringComparison.Ordinal);

    /// <summary>
    ///     Finds what is wrong with the name of an entry, which is used to build a path.
    /// </summary>
    /// <param name="name">The name of the entry, with forward slashes; a folder ends with one.</param>
    /// <returns>A phrase that completes the sentence "an entry that ...", or <see langword="null" /> when the name is usable.</returns>
    private static string? NameProblem(string name)
    {
        if (name.Length == 0)
        {
            return "has no name";
        }

        if (name.Length > MaxNameLength)
        {
            return $"has a name of more than {MaxNameLength} characters";
        }

        if (name.Contains('\\'))
        {
            return "uses a backslash in its name";
        }

        if (name.StartsWith('/'))
        {
            return "has an absolute name";
        }

        if (name.Any(c => c < ' ' || c == '\u007f' || InvalidNameCharacters.Contains(c, StringComparison.Ordinal)))
        {
            return "has a character in its name that a file name cannot have, or names a drive";
        }

        // The parts between the slashes; a folder has an empty part at its end, which is allowed
        var parts = name.EndsWith('/') ? name[..^1].Split('/') : name.Split('/');
        foreach (var part in parts)
        {
            if (part.Length == 0 || part is "." or "..")
            {
                return "has a part that is empty, '.', or '..'";
            }

            if (part.Length > MaxSegmentLength || part.EndsWith('.') || part.EndsWith(' ') || ReservedNames.Contains(part.Split('.')[0]))
            {
                return "has a part that cannot be a file name here";
            }
        }

        return null;
    }

    /// <summary>
    ///     Tells whether an entry was made as a symbolic link, by the Unix mode in its attributes.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true" /> for a link.</returns>
    private static bool IsLink(ZipArchiveEntry entry)
    {
        // The upper 16 bits of the external attributes hold the Unix mode when the archive was made on Unix; 0xA000 is a link
        var mode = (entry.ExternalAttributes >> 16) & 0xF000;
        return mode == 0xA000;
    }

    /// <summary>
    ///     Makes a name safe to put in a message: control characters are replaced and a long name is cut.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The text.</returns>
    private static string Printable(string name)
    {
        var text = new string(name.Select(c => c < ' ' || c == '\u007f' ? '?' : c).ToArray());
        return text.Length <= 80 ? text : text[..80] + "...";
    }

    /// <summary>
    ///     Makes the result of an import that failed.
    /// </summary>
    /// <param name="error">The reason.</param>
    /// <param name="issues">The issues of the style file, when there are some.</param>
    /// <returns>The result.</returns>
    private static StyleImportResult Failure(string error, IReadOnlyList<StyleIssue>? issues = null) =>
        new(null, null, error, issues ?? []);

    /// <summary>
    ///     Deletes a file, ignoring failures.
    /// </summary>
    /// <param name="path">The file.</param>
    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file is not read as anything.
        }
    }

    /// <summary>
    ///     Deletes a folder with its contents, ignoring failures.
    /// </summary>
    /// <param name="folder">The folder.</param>
    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: a hidden folder that is left is not listed as a kit.
        }
    }
}

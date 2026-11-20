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
using System.Text.RegularExpressions;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     Keeps the files that belong to attachments in the project folder: pictures pasted from the clipboard are saved
///     in the project's private data folder while the message is a draft, and when the message is sent the things its
///     chips stand for are copied into the project's <c>attachments</c> folder, where the message can refer to them.
/// </summary>
/// <remarks>
///     <para>
///         The content of an attachment is data. The store never opens, runs, renders, or interprets it: it saves bytes
///         of a pasted picture after checking the PNG signature and a size, and it copies files byte by byte. Names that
///         come from outside are made safe before a file or folder is created, every path is built with
///         <see cref="PathHelpers.SafePathCombine" /> so that it stays inside the folder it is built for, links that
///         could lead out of a dropped folder are not followed, and the copy is bounded: at most
///         <see cref="MaximumFiles" /> files, <see cref="MaximumFolders" /> folders, and <see cref="MaximumBytes" /> bytes
///         for one message. A name that is taken is never overwritten: the copy gets a number in parentheses.
///     </para>
///     <para>
///         Copying all attachments is all or nothing. The limits are checked on the whole set before anything is copied
///         and again while copying, and a failure removes everything that the call created. Things that already are in
///         the project are referred to in place and not copied. The store holds no state besides its folder, so calls
///         do not influence each other, but two calls at the same time on one project are not supported. All methods do
///         file I/O and may be called on a background thread.
///     </para>
/// </remarks>
public sealed partial class AttachmentStore : IAttachmentStore
{
    /// <summary>
    ///     The largest number of files that are copied into the project for one message.
    /// </summary>
    public const int MaximumFiles = 500;

    /// <summary>
    ///     The largest number of folders, including nested ones, that are created in the project for one message.
    /// </summary>
    public const int MaximumFolders = 500;

    /// <summary>
    ///     The largest total size, in bytes, of the files that are copied into the project for one message: 200 MB.
    /// </summary>
    public const long MaximumBytes = 200L * 1024 * 1024;

    /// <summary>
    ///     The largest size, in bytes, of one pasted picture: 50 MB.
    /// </summary>
    public const long MaximumPastedImageBytes = 50L * 1024 * 1024;

    /// <summary>
    ///     The name of the folder, in the project folder, that holds the attachments of sent messages, and the name of
    ///     the folder for pasted pictures inside the project's private data folder.
    /// </summary>
    public const string AttachmentsFolderName = "attachments";

    /// <summary>
    ///     The size of the buffer with which files are copied.
    /// </summary>
    private const int CopyBufferSize = 81920;

    /// <summary>
    ///     The number of attempts to find a free name before giving up, which only matters when another program is
    ///     creating files in the folder at the same time.
    /// </summary>
    private const int NameAttempts = 100;

    /// <summary>
    ///     The first bytes of every PNG file.
    /// </summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The source of the time that is part of the names of pasted pictures.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AttachmentStore" /> class for one project.
    /// </summary>
    /// <param name="projectFolder">The full path of the project folder; it is not created.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="timeProvider">The source of time for names; the system time when <see langword="null" />.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="projectFolder" /> is not a complete path.</exception>
    public AttachmentStore(string projectFolder, RollingFileLogger? logger = null, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        if (!Path.IsPathFullyQualified(projectFolder))
        {
            throw new ArgumentException("The project folder must be a complete path.", nameof(projectFolder));
        }

        ProjectFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectFolder));
        DataFolder = PathHelpers.SafePathCombine(ProjectFolder, ProjectDocument.DataFolderName);
        PastedImagesFolder = PathHelpers.SafePathCombine(DataFolder, AttachmentsFolderName);
        AttachmentsFolder = PathHelpers.SafePathCombine(ProjectFolder, AttachmentsFolderName);
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string ProjectFolder { get; }

    /// <inheritdoc />
    public string PastedImagesFolder { get; }

    /// <summary>
    ///     Gets the full path of the folder, in the project folder, that holds the attachments of sent messages.
    /// </summary>
    public string AttachmentsFolder { get; }

    /// <summary>
    ///     Gets the full path of the project's private data folder.
    /// </summary>
    private string DataFolder { get; }

    /// <inheritdoc />
    public string SavePastedImage(ReadOnlyMemory<byte> png)
    {
        // Accept only what is plainly a PNG picture of a sane size; the bytes are never decoded here
        if (png.IsEmpty)
        {
            throw new ArgumentException("The picture is empty.", nameof(png));
        }

        if (png.Length > MaximumPastedImageBytes)
        {
            throw new ArgumentException($"The picture is larger than {MaximumPastedImageBytes / 1024 / 1024} MB.", nameof(png));
        }

        if (!png.Span.StartsWith(PngSignature))
        {
            throw new ArgumentException("The data is not a PNG picture.", nameof(png));
        }

        // Find a free name and create the file exclusively, so that nothing is ever overwritten
        Directory.CreateDirectory(PastedImagesFolder);
        var stamp = _timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        for (var number = 1; number <= NameAttempts; number++)
        {
            var path = PathHelpers.SafePathCombine(PastedImagesFolder, string.Create(CultureInfo.InvariantCulture, $"paste-{stamp}-{number}.png"));
            FileStream target;
            try
            {
                target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException) when (File.Exists(path))
            {
                continue;
            }

            // A file that was cut short is not left behind
            var written = false;
            try
            {
                using (target)
                {
                    target.Write(png.Span);
                }

                written = true;
            }
            finally
            {
                if (!written)
                {
                    TryDelete(path);
                }
            }

            return path;
        }

        throw new IOException("No free name was found for the pasted picture.");
    }

    /// <inheritdoc />
    public int RemoveLeftovers(IReadOnlyCollection<string> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        var removed = 0;
        try
        {
            if (!Directory.Exists(PastedImagesFolder))
            {
                return 0;
            }

            // Compare full paths, so a chip that refers to a file by another spelling of its path keeps the file
            var kept = new HashSet<string>(keep.Select(SafeFullPath), AttachmentNames.PathComparer);
            foreach (var file in Directory.EnumerateFiles(PastedImagesFolder))
            {
                if (!PastedFileName().IsMatch(Path.GetFileName(file)) || kept.Contains(Path.GetFullPath(file)))
                {
                    continue;
                }

                if (TryDelete(file))
                {
                    removed++;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Warning, $"The folder '{PastedImagesFolder}' of pasted pictures could not be cleaned up.", ex);
        }

        return removed;
    }

    /// <inheritdoc />
    public StagingResult StageForSend(IReadOnlyList<AttachmentChip> chips)
    {
        ArgumentNullException.ThrowIfNull(chips);
        if (chips.Any(chip => chip is null))
        {
            throw new ArgumentException("A chip must not be null.", nameof(chips));
        }

        // Plan every chip first: where it is, what it needs, and whether the whole message fits the limits
        var plans = new Dictionary<string, SourcePlan>(AttachmentNames.PathComparer);
        var chipPlans = new List<SourcePlan>();
        var totals = new Totals();
        foreach (var chip in chips)
        {
            var problem = AttachmentNames.FindProblemWithPath(chip.FullPath);
            if (problem is not null)
            {
                return Fail(chip, $"'{chip.Name}' cannot be attached: {problem}");
            }

            // The same thing attached twice is put into the project once
            var source = SafeFullPath(chip.FullPath);
            if (!plans.TryGetValue(source, out var plan))
            {
                var reason = TryPlan(chip, source, totals, out plan);
                if (reason is not null || plan is null)
                {
                    return Fail(chip, reason ?? $"'{chip.Name}' cannot be attached.");
                }

                plans[source] = plan;
            }

            chipPlans.Add(plan);
        }

        // Copy what has to be copied; a failure takes back everything this call created
        var copyFailure = CopyAll(plans.Values.Where(plan => plan.Mode != StageMode.InPlace).ToList());
        if (copyFailure is not null)
        {
            return copyFailure;
        }

        // Pasted pictures were moved: the draft copies are not needed any more
        foreach (var plan in plans.Values.Where(plan => plan.Mode == StageMode.MovePasted))
        {
            TryDelete(plan.Source);
        }

        var staged = chips.Select((chip, index) => ToStaged(chip, chipPlans[index])).ToList();
        Log(LogLevel.Info, $"{staged.Count} attachment(s) were put into the project ({totals.Files} file(s), {totals.Bytes} byte(s) copied).");
        return StagingResult.Success(staged);
    }

    /// <summary>
    ///     Matches the names of the files that <see cref="SavePastedImage" /> writes.
    /// </summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(@"^paste-\d{8}-\d{6}-\d+\.png$", RegexOptions.CultureInvariant)]
    private static partial Regex PastedFileName();

    /// <summary>
    ///     Makes a complete, normalized path without a trailing separator, or the path as given when it cannot be
    ///     normalized.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The full path.</returns>
    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    /// <summary>
    ///     Determines whether a path is the same as a folder or lies inside it.
    /// </summary>
    /// <param name="folder">The full path of the folder.</param>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true" /> when the path is the folder or below it.</returns>
    private static bool IsSameOrInside(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative == "." ||
               (relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) &&
                !Path.IsPathRooted(relative));
    }

    /// <summary>
    ///     Determines whether a file or folder is a link that could lead somewhere else than where it is.
    /// </summary>
    /// <param name="info">The file or folder.</param>
    /// <returns><see langword="true" /> for symbolic links and junctions.</returns>
    private static bool IsLink(FileSystemInfo info) => info.LinkTarget is not null;

    /// <summary>
    ///     Copies a file into a new file of a folder without ever replacing a file, and counts it.
    /// </summary>
    /// <param name="source">The full path of the source file.</param>
    /// <param name="destination">The full path of the new file, which must not exist.</param>
    /// <param name="state">The running totals and the list of what was created.</param>
    /// <returns><see langword="false" /> when the destination exists, <see langword="true" /> when the file was copied.</returns>
    /// <exception cref="CopyLimitException">Thrown when the limits for one message are passed.</exception>
    /// <exception cref="IOException">Thrown when the source cannot be read or the destination cannot be written.</exception>
    private static bool TryCopyExclusive(string source, string destination, CopyState state)
    {
        FileStream target;
        try
        {
            target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(destination) || Directory.Exists(destination))
        {
            return false;
        }

        using (target)
        {
            state.Created.Add((destination, false));
            if (++state.Files > MaximumFiles)
            {
                throw new CopyLimitException($"More than {MaximumFiles} files would be copied into the project with this message.");
            }

            // Copy in blocks and count, so a file that grew since it was measured cannot pass the limit
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[CopyBufferSize];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                state.Bytes += read;
                if (state.Bytes > MaximumBytes)
                {
                    throw new CopyLimitException($"More than {MaximumBytes / 1024 / 1024} MB would be copied into the project with this message.");
                }

                target.Write(buffer, 0, read);
            }
        }

        return true;
    }

    /// <summary>
    ///     Makes the staged attachment that tells where a chip's thing is in the project.
    /// </summary>
    /// <param name="chip">The chip.</param>
    /// <param name="plan">The plan of the chip's thing, after it was carried out.</param>
    /// <returns>The staged attachment.</returns>
    private static StagedAttachment ToStaged(AttachmentChip chip, SourcePlan plan) =>
        new(chip, chip.FullPath, plan.RelativePath, plan.Kind, plan.Name, plan.Bytes, plan.Files, plan.Mode != StageMode.InPlace, plan.SkippedLinks);

    /// <summary>
    ///     Makes a failed result, naming the chip, and writes it to the log.
    /// </summary>
    /// <param name="chip">The chip of the thing that cannot be attached.</param>
    /// <param name="reason">The sentence for the user.</param>
    /// <returns>The failed result.</returns>
    private StagingResult Fail(AttachmentChip chip, string reason)
    {
        Log(LogLevel.Warning, $"The attachments could not be put into the project: {reason}");
        return StagingResult.Fail(chip, reason);
    }

    /// <summary>
    ///     Works out what has to be done for one thing: where it is, whether it is copied, and what that costs.
    /// </summary>
    /// <param name="chip">The chip.</param>
    /// <param name="source">The full path of the thing.</param>
    /// <param name="totals">What the message has used of the limits so far; updated when the thing is copied.</param>
    /// <param name="plan">The plan, when there is no reason.</param>
    /// <returns>A sentence that says why the thing cannot be attached, or <see langword="null" /> when it can.</returns>
    private string? TryPlan(AttachmentChip chip, string source, Totals totals, out SourcePlan? plan)
    {
        plan = null;
        var kind = chip switch
        {
            FolderChip => AttachmentKind.Folder,
            ImageChip => AttachmentKind.Image,
            _ => AttachmentKind.File
        };

        try
        {
            // The thing has to be there, and be what the chip says
            if (kind == AttachmentKind.Folder ? !Directory.Exists(source) : !File.Exists(source))
            {
                if (kind == AttachmentKind.Folder)
                {
                    return $"The folder '{chip.Name}' no longer exists.";
                }

                return Directory.Exists(source) ? $"'{chip.Name}' is a folder, not a file." : $"The file '{chip.Name}' no longer exists.";
            }

            // Where it is decides what happens: the project's own folder tree is referred to, the rest is copied
            var mode = ChooseMode(chip, source, out var refusal);
            if (refusal is not null)
            {
                return refusal;
            }

            var candidate = new SourcePlan(chip, source, kind, mode) { Name = Path.GetFileName(source) };
            var limitReason = MeasureAndCheckLimits(candidate, totals);
            if (limitReason is not null)
            {
                return limitReason;
            }

            candidate.RelativePath = mode == StageMode.InPlace
                ? Path.GetRelativePath(ProjectFolder, source).Replace('\\', '/')
                : string.Empty;
            plan = candidate;
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log(LogLevel.Warning, $"'{source}' could not be inspected for sending.", ex);
            return $"'{chip.Name}' could not be read: {ex.Message}";
        }
    }

    /// <summary>
    ///     Decides how a thing gets into the project, or why it cannot.
    /// </summary>
    /// <param name="chip">The chip.</param>
    /// <param name="source">The full path of the thing.</param>
    /// <param name="refusal">A sentence that says why the thing cannot be attached, or <see langword="null" />.</param>
    /// <returns>The way the thing is put into the project.</returns>
    private StageMode ChooseMode(AttachmentChip chip, string source, out string? refusal)
    {
        refusal = null;

        // A folder that holds the project would be copied into itself without end
        if (IsSameOrInside(source, ProjectFolder))
        {
            refusal = $"'{chip.Name}' contains the project folder and cannot be attached.";
            return StageMode.InPlace;
        }

        // The private data folder is the application's: only the pictures pasted into the draft may leave it
        if (IsSameOrInside(DataFolder, source))
        {
            if (chip is ImageChip { IsPasted: true } && IsPastedFile(source))
            {
                return StageMode.MovePasted;
            }

            refusal = $"'{chip.Name}' is in the private data folder of the project and cannot be attached.";
            return StageMode.InPlace;
        }

        if (chip is ImageChip { IsPasted: true })
        {
            refusal = $"'{chip.Name}' is not a picture that was pasted into this project.";
            return StageMode.InPlace;
        }

        return IsSameOrInside(ProjectFolder, source) ? StageMode.InPlace : StageMode.Copy;
    }

    /// <summary>
    ///     Determines whether a file is one of the pictures that <see cref="SavePastedImage" /> saved.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns><see langword="true" /> for a file with the name pattern directly in the draft folder.</returns>
    private bool IsPastedFile(string path) =>
        string.Equals(Path.GetDirectoryName(path), PastedImagesFolder, AttachmentNames.PathComparison) &&
        PastedFileName().IsMatch(Path.GetFileName(path));

    /// <summary>
    ///     Measures what a thing costs and checks the limits for one message.
    /// </summary>
    /// <param name="plan">The plan, whose sizes are filled in.</param>
    /// <param name="totals">What the message has used so far; updated when the thing is copied.</param>
    /// <returns>A sentence that says which limit is passed, or <see langword="null" />.</returns>
    private static string? MeasureAndCheckLimits(SourcePlan plan, Totals totals)
    {
        var name = plan.Chip.Name;
        if (plan.Kind != AttachmentKind.Folder)
        {
            plan.Bytes = new FileInfo(plan.Source).Length;
            plan.Files = 1;
            if (plan.Mode == StageMode.InPlace)
            {
                return null;
            }

            totals.Files++;
            totals.Bytes += plan.Bytes;
            if (totals.Files > MaximumFiles)
            {
                return FilesLimitReason(name);
            }

            return totals.Bytes > MaximumBytes ? BytesLimitReason(name) : null;
        }

        // A folder that is already in the project is not copied, and so is neither measured nor limited
        if (plan.Mode == StageMode.InPlace)
        {
            return null;
        }

        var listing = ListFolder(plan.Source, MaximumFiles - totals.Files, MaximumFolders - totals.Folders, MaximumBytes - totals.Bytes);
        if (listing.Exceeded is not null)
        {
            return listing.Exceeded switch
            {
                Cap.Files => FilesLimitReason(name),
                Cap.Folders => $"Attaching '{name}' would create more than {MaximumFolders} folders in the project with this message.",
                _ => BytesLimitReason(name)
            };
        }

        plan.Listing = listing;
        plan.Bytes = listing.Bytes;
        plan.Files = listing.Files;
        plan.SkippedLinks = listing.SkippedLinks;
        totals.Files += listing.Files;
        totals.Folders += listing.Folders;
        totals.Bytes += listing.Bytes;
        return null;
    }

    /// <summary>
    ///     Makes the sentence for passing the limit of files.
    /// </summary>
    /// <param name="name">The name of the thing.</param>
    /// <returns>The sentence.</returns>
    private static string FilesLimitReason(string name) =>
        $"Attaching '{name}' would copy more than {MaximumFiles} files into the project with this message.";

    /// <summary>
    ///     Makes the sentence for passing the limit of size.
    /// </summary>
    /// <param name="name">The name of the thing.</param>
    /// <returns>The sentence.</returns>
    private static string BytesLimitReason(string name) =>
        $"Attaching '{name}' would copy more than {MaximumBytes / 1024 / 1024} MB into the project with this message.";

    /// <summary>
    ///     Lists what a folder holds, as it is going to be copied, and stops as soon as a limit is passed so that a
    ///     huge folder is never enumerated to its end.
    /// </summary>
    /// <remarks>
    ///     Symbolic links and junctions are skipped and counted, never followed, because a link could lead out of the
    ///     dropped folder to files the user did not choose. Names are made safe, and names that become equal are made
    ///     different, folder by folder.
    /// </remarks>
    /// <param name="source">The full path of the folder.</param>
    /// <param name="remainingFiles">How many more files the message may copy.</param>
    /// <param name="remainingFolders">How many more folders the message may create.</param>
    /// <param name="remainingBytes">How many more bytes the message may copy.</param>
    /// <returns>The listing, with the limit that was passed if one was.</returns>
    private static FolderListing ListFolder(string source, int remainingFiles, int remainingFolders, long remainingBytes)
    {
        var listing = new FolderListing();
        var pending = new Stack<(string Source, string Destination)>();
        pending.Push((source, string.Empty));
        while (pending.Count > 0)
        {
            var (folder, destination) = pending.Pop();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // A fixed order makes the names, and so the result, the same every time
            foreach (var child in new DirectoryInfo(folder).EnumerateFileSystemInfos().OrderBy(info => info.Name, StringComparer.Ordinal))
            {
                if (IsLink(child))
                {
                    listing.SkippedLinks++;
                    continue;
                }

                var name = AttachmentNames.UniqueName(taken, AttachmentNames.SanitizeName(child.Name));
                var relative = destination.Length == 0 ? name : destination + Path.DirectorySeparatorChar + name;
                if (child is DirectoryInfo)
                {
                    listing.Folders++;
                    listing.Entries.Add(new ListedEntry(child.FullName, relative, true));
                    pending.Push((child.FullName, relative));
                }
                else
                {
                    listing.Files++;
                    listing.Bytes += ((FileInfo)child).Length;
                    listing.Entries.Add(new ListedEntry(child.FullName, relative, false));
                }

                var exceeded = ExceededCap(listing, remainingFiles, remainingFolders, remainingBytes);
                if (exceeded is not null)
                {
                    listing.Exceeded = exceeded;
                    return listing;
                }
            }
        }

        return listing;
    }

    /// <summary>
    ///     Finds the first limit that a listing has passed.
    /// </summary>
    /// <param name="listing">The listing so far.</param>
    /// <param name="remainingFiles">How many more files the message may copy.</param>
    /// <param name="remainingFolders">How many more folders the message may create.</param>
    /// <param name="remainingBytes">How many more bytes the message may copy.</param>
    /// <returns>The limit that was passed, or <see langword="null" />.</returns>
    private static Cap? ExceededCap(FolderListing listing, int remainingFiles, int remainingFolders, long remainingBytes)
    {
        if (listing.Files > remainingFiles)
        {
            return Cap.Files;
        }

        if (listing.Folders > remainingFolders)
        {
            return Cap.Folders;
        }

        return listing.Bytes > remainingBytes ? Cap.Bytes : null;
    }

    /// <summary>
    ///     Copies the things that have to be copied into the project's attachments folder, all or none.
    /// </summary>
    /// <param name="plans">The plans of the things to copy.</param>
    /// <returns>The failed result, after everything that was created has been removed, or <see langword="null" /> when all were copied.</returns>
    private StagingResult? CopyAll(List<SourcePlan> plans)
    {
        if (plans.Count == 0)
        {
            return null;
        }

        var state = new CopyState();
        var createdFolder = false;
        var current = plans[0];
        try
        {
            if (!Directory.Exists(AttachmentsFolder))
            {
                Directory.CreateDirectory(AttachmentsFolder);
                createdFolder = true;
            }

            foreach (var plan in plans)
            {
                current = plan;
                CopyOne(plan, state);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Rollback(state, createdFolder);
            var reason = ex is CopyLimitException
                ? $"Attaching '{current.Chip.Name}' failed: {ex.Message}"
                : $"'{current.Chip.Name}' could not be copied into the project: {ex.Message}";
            Log(LogLevel.Warning, $"Copying '{current.Source}' into the project failed.", ex);
            return Fail(current.Chip, reason);
        }
    }

    /// <summary>
    ///     Copies one thing into the attachments folder and records where it went in its plan.
    /// </summary>
    /// <param name="plan">The plan of the thing.</param>
    /// <param name="state">The running totals and the list of what was created.</param>
    private void CopyOne(SourcePlan plan, CopyState state)
    {
        var wanted = AttachmentNames.SanitizeName(Path.GetFileName(plan.Source));
        if (plan.Kind != AttachmentKind.Folder)
        {
            // Another program may take the name between asking and creating, so the copy is tried with new names
            for (var attempt = 0; attempt < NameAttempts; attempt++)
            {
                var name = AttachmentNames.UniqueName(AttachmentsFolder, wanted);
                if (TryCopyExclusive(plan.Source, PathHelpers.SafePathCombine(AttachmentsFolder, name), state))
                {
                    plan.Name = name;
                    plan.RelativePath = $"{AttachmentsFolderName}/{name}";
                    return;
                }
            }

            throw new IOException($"No free name was found for '{wanted}'.");
        }

        // A folder is copied into a new folder of its own, whose entries have names that are unique inside it
        var folderName = AttachmentNames.UniqueName(AttachmentsFolder, wanted);
        var root = PathHelpers.SafePathCombine(AttachmentsFolder, folderName);
        Directory.CreateDirectory(root);
        state.Created.Add((root, true));
        foreach (var entry in plan.Listing!.Entries)
        {
            var target = PathHelpers.SafePathCombine(root, entry.Destination);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(target);
                state.Created.Add((target, true));
            }
            else if (!TryCopyExclusive(entry.Source, target, state))
            {
                throw new IOException($"'{entry.Destination}' already exists.");
            }
        }

        plan.Name = folderName;
        plan.RelativePath = $"{AttachmentsFolderName}/{folderName}";
    }

    /// <summary>
    ///     Removes what a failed copy created, newest first, so that nothing of it is left behind.
    /// </summary>
    /// <param name="state">What was created.</param>
    /// <param name="createdFolder">Whether the attachments folder itself was created by the call.</param>
    private void Rollback(CopyState state, bool createdFolder)
    {
        for (var index = state.Created.Count - 1; index >= 0; index--)
        {
            var (path, isDirectory) = state.Created[index];
            if (isDirectory)
            {
                TryDeleteEmptyFolder(path);
            }
            else
            {
                TryDelete(path);
            }
        }

        if (createdFolder)
        {
            TryDeleteEmptyFolder(AttachmentsFolder);
        }
    }

    /// <summary>
    ///     Deletes a folder that is empty, ignoring a folder that is missing, not empty, or in use.
    /// </summary>
    /// <param name="path">The folder.</param>
    private void TryDeleteEmptyFolder(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Warning, $"The folder '{path}' could not be deleted.", ex);
        }
    }

    /// <summary>
    ///     Deletes a file, ignoring a file that is missing or in use.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true" /> when the file was deleted.</returns>
    private bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log(LogLevel.Warning, $"The file '{path}' could not be deleted.", ex);
            return false;
        }
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks an attachment.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    /// <param name="exception">Optional failure context.</param>
    private void Log(LogLevel level, string message, Exception? exception = null)
    {
        try
        {
            _logger?.Log(level, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a file problem worse.
        }
    }

    /// <summary>
    ///     How a thing gets into the project.
    /// </summary>
    private enum StageMode
    {
        /// <summary>The thing is in the project already and is referred to where it is.</summary>
        InPlace,

        /// <summary>The thing is copied into the attachments folder.</summary>
        Copy,

        /// <summary>The picture was pasted into the draft; it is copied and the draft file is deleted.</summary>
        MovePasted
    }

    /// <summary>
    ///     The limits of one message.
    /// </summary>
    private enum Cap
    {
        /// <summary>The number of files.</summary>
        Files,

        /// <summary>The number of folders.</summary>
        Folders,

        /// <summary>The total size.</summary>
        Bytes
    }

    /// <summary>
    ///     One entry of a folder that is going to be copied.
    /// </summary>
    /// <param name="Source">The full path of the entry.</param>
    /// <param name="Destination">The path of the entry relative to the copy of the folder, with safe names.</param>
    /// <param name="IsDirectory"><see langword="true" /> for a folder.</param>
    private readonly record struct ListedEntry(string Source, string Destination, bool IsDirectory);

    /// <summary>
    ///     What a message has used of its limits.
    /// </summary>
    private sealed class Totals
    {
        /// <summary>Gets or sets the number of files.</summary>
        public int Files { get; set; }

        /// <summary>Gets or sets the number of folders.</summary>
        public int Folders { get; set; }

        /// <summary>Gets or sets the number of bytes.</summary>
        public long Bytes { get; set; }
    }

    /// <summary>
    ///     What is in a folder that is going to be copied.
    /// </summary>
    private sealed class FolderListing
    {
        /// <summary>Gets the entries, every folder before what is in it.</summary>
        public List<ListedEntry> Entries { get; } = [];

        /// <summary>Gets or sets the number of files.</summary>
        public int Files { get; set; }

        /// <summary>Gets or sets the number of folders.</summary>
        public int Folders { get; set; }

        /// <summary>Gets or sets the number of bytes.</summary>
        public long Bytes { get; set; }

        /// <summary>Gets or sets the number of links that were skipped.</summary>
        public int SkippedLinks { get; set; }

        /// <summary>Gets or sets the limit that was passed, or <see langword="null" />.</summary>
        public Cap? Exceeded { get; set; }
    }

    /// <summary>
    ///     What is to be done for one thing that is attached.
    /// </summary>
    private sealed class SourcePlan
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SourcePlan" /> class.
        /// </summary>
        /// <param name="chip">The first chip that stands for the thing.</param>
        /// <param name="source">The full path of the thing.</param>
        /// <param name="kind">The kind of the thing.</param>
        /// <param name="mode">How the thing gets into the project.</param>
        public SourcePlan(AttachmentChip chip, string source, AttachmentKind kind, StageMode mode)
        {
            Chip = chip;
            Source = source;
            Kind = kind;
            Mode = mode;
        }

        /// <summary>Gets the first chip that stands for the thing.</summary>
        public AttachmentChip Chip { get; }

        /// <summary>Gets the full path of the thing.</summary>
        public string Source { get; }

        /// <summary>Gets the kind of the thing.</summary>
        public AttachmentKind Kind { get; }

        /// <summary>Gets how the thing gets into the project.</summary>
        public StageMode Mode { get; }

        /// <summary>Gets or sets the name of the thing in the project.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the path of the thing relative to the project folder, with forward slashes.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the size in bytes of the file, or of the files of a copied folder.</summary>
        public long Bytes { get; set; }

        /// <summary>Gets or sets the number of files.</summary>
        public int Files { get; set; }

        /// <summary>Gets or sets the number of links that were skipped.</summary>
        public int SkippedLinks { get; set; }

        /// <summary>Gets or sets the listing of a folder that is copied.</summary>
        public FolderListing? Listing { get; set; }
    }

    /// <summary>
    ///     The running totals of a copy and what it created, for the limits and for taking back a failed copy.
    /// </summary>
    private sealed class CopyState
    {
        /// <summary>Gets or sets the number of files copied.</summary>
        public int Files { get; set; }

        /// <summary>Gets or sets the number of bytes copied.</summary>
        public long Bytes { get; set; }

        /// <summary>Gets the files and folders that were created, in order.</summary>
        public List<(string Path, bool IsDirectory)> Created { get; } = [];
    }

    /// <summary>
    ///     Thrown when a copy passes the limits of one message.
    /// </summary>
    private sealed class CopyLimitException : IOException
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CopyLimitException" /> class.
        /// </summary>
        /// <param name="message">The sentence that says which limit was passed.</param>
        public CopyLimitException(string message)
            : base(message)
        {
        }
    }
}

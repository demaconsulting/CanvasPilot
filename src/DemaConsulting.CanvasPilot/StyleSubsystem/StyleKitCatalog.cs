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
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Finds the style kits in the program, the user's folder, the open project, and the shared folders, chooses the kit of
///     the most local place for each id, and creates, changes, and removes the kits of the user and of the project.
/// </summary>
/// <remarks>
///     <para>
///         Reading is split by how far a place can be trusted to answer: the places on this computer are read on a worker
///         thread, and each shared folder is read on a worker of its own with a time limit, so one network folder that is
///         offline costs at most the limit, is reported as not reachable, and never stops the other places or blocks the
///         caller. A scan that outlives its limit is left to finish and its result is thrown away.
///     </para>
///     <para>
///         The result of a scan of a place is kept, so that a change to a kit of the user or of the project, which is
///         quick and on this computer, reads those two places again and reuses what was read from the shared folders and
///         from the program. The class is safe for concurrent use; the entries are replaced as a whole.
///     </para>
/// </remarks>
public sealed class StyleKitCatalog : IStyleKitCatalog, IDisposable
{
    /// <summary>
    ///     The name of the folder of a project that holds its kits.
    /// </summary>
    public const string ProjectFolderName = "styles";

    /// <summary>
    ///     The name of the folder in the data folder of the user that holds the kits of the user.
    /// </summary>
    public const string UserFolderName = "styles";

    /// <summary>
    ///     The name of the folder beside the program that holds the kits that come with it.
    /// </summary>
    public const string BuiltInFolderName = "StyleKits";

    /// <summary>
    ///     How long a shared folder is given to answer, by default.
    /// </summary>
    public static readonly TimeSpan DefaultSharedScanTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     The name of a kit that is created without a name, and the base of its id.
    /// </summary>
    private const string DefaultKitName = "New kit";

    /// <summary>
    ///     The longest base of an id that is made from a name, which leaves room for a number to make it unique.
    /// </summary>
    private const int MaxIdBaseLength = 40;

    /// <summary>
    ///     Gives the project and the settings.
    /// </summary>
    private readonly IProjectService _projects;

    /// <summary>
    ///     Gives the shared folders and tells when they change.
    /// </summary>
    private readonly ISettingsService _settings;

    /// <summary>
    ///     The folder of the kits that come with the program.
    /// </summary>
    private readonly string _builtInFolder;

    /// <summary>
    ///     The folder of the kits of the user.
    /// </summary>
    private readonly string _userFolder;

    /// <summary>
    ///     How long a shared folder is given to answer.
    /// </summary>
    private readonly TimeSpan _sharedScanTimeout;

    /// <summary>
    ///     Lists the kit folders of a folder; replaced in tests so that a folder can be made to hang or to fail.
    /// </summary>
    private readonly Func<string, IReadOnlyList<string>> _listSubFolders;

    /// <summary>
    ///     Optional logger for what is found and what goes wrong.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Guards the scans, the snapshot, and the counters.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    ///     The last scan of the folder of the open project, or null when no project was open.
    /// </summary>
    private RootScan? _projectScan;

    /// <summary>
    ///     The last scan of the folder of the user.
    /// </summary>
    private RootScan? _userScan;

    /// <summary>
    ///     The last scan of the shared folders, in the order of the settings.
    /// </summary>
    private IReadOnlyList<RootScan> _sharedScans = [];

    /// <summary>
    ///     The last scan of the kits that come with the program.
    /// </summary>
    private RootScan? _builtInScan;

    /// <summary>
    ///     What readers see.
    /// </summary>
    private Snapshot _snapshot = Snapshot.Empty;

    /// <summary>
    ///     The number of the latest refresh that was started.
    /// </summary>
    private long _startedRefresh;

    /// <summary>
    ///     The number of the latest refresh whose result was applied.
    /// </summary>
    private long _appliedRefresh;

    /// <summary>
    ///     Counts the changes made to the folders of the user and of the project, so that a refresh that started before a
    ///     change does not replace what the change read.
    /// </summary>
    private long _localStamp;

    /// <summary>
    ///     The shared folders as they were when they were last read, to tell a change of them from other settings changes.
    /// </summary>
    private IReadOnlyList<string> _knownSharedFolders;

    /// <summary>
    ///     The refresh that a change of the project or of the settings started last; tests wait for it.
    /// </summary>
    private Task _pendingRefresh = Task.CompletedTask;

    /// <summary>
    ///     Whether the catalog has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitCatalog" /> class. Nothing is read until
    ///     <see cref="RefreshAsync" /> is called; the catalog then refreshes itself when the open project or the shared
    ///     folders change.
    /// </summary>
    /// <param name="projects">The project service, which says which project is open.</param>
    /// <param name="settings">The settings service, which holds the shared folders.</param>
    /// <param name="builtInFolder">The folder of the kits that come with the program, or <see langword="null" /> for <c>StyleKits</c> beside the program.</param>
    /// <param name="userFolder">The folder of the kits of the user, or <see langword="null" /> for <c>styles</c> in the data folder of the application.</param>
    /// <param name="sharedScanTimeout">How long a shared folder is given to answer, or <see langword="null" /> for 3 seconds.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="ArgumentNullException">Thrown when the project service or the settings service is <see langword="null" />.</exception>
    public StyleKitCatalog(
        IProjectService projects,
        ISettingsService settings,
        string? builtInFolder = null,
        string? userFolder = null,
        TimeSpan? sharedScanTimeout = null,
        RollingFileLogger? logger = null)
        : this(projects, settings, builtInFolder, userFolder, sharedScanTimeout, logger, StyleKitScanner.ListSubFolders)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitCatalog" /> class with a function that lists the kit folders,
    ///     so that tests can make a folder hang or fail without a network.
    /// </summary>
    /// <param name="projects">The project service.</param>
    /// <param name="settings">The settings service.</param>
    /// <param name="builtInFolder">The folder of the kits that come with the program, or <see langword="null" />.</param>
    /// <param name="userFolder">The folder of the kits of the user, or <see langword="null" />.</param>
    /// <param name="sharedScanTimeout">How long a shared folder is given to answer, or <see langword="null" />.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="listSubFolders">Lists the kit folders of a folder.</param>
    internal StyleKitCatalog(
        IProjectService projects,
        ISettingsService settings,
        string? builtInFolder,
        string? userFolder,
        TimeSpan? sharedScanTimeout,
        RollingFileLogger? logger,
        Func<string, IReadOnlyList<string>> listSubFolders)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(listSubFolders);

        _projects = projects;
        _settings = settings;
        _builtInFolder = Path.GetFullPath(builtInFolder ?? Path.Combine(AppContext.BaseDirectory, BuiltInFolderName));
        _userFolder = Path.GetFullPath(userFolder ?? Path.Combine(SettingsService.DefaultSettingsFolder, UserFolderName));
        _sharedScanTimeout = sharedScanTimeout ?? DefaultSharedScanTimeout;
        _logger = logger;
        _listSubFolders = listSubFolders;
        _knownSharedFolders = settings.Current.Styles.SharedKitFolders;

        _projects.CurrentChanged += OnProjectChanged;
        _settings.Changed += OnSettingsChanged;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IReadOnlyList<StyleKitEntry> Entries => Volatile.Read(ref _snapshot).Entries;

    /// <inheritdoc />
    public IReadOnlyList<StyleLocationStatus> Locations => Volatile.Read(ref _snapshot).Locations;

    /// <summary>
    ///     Gets the task of the refresh that the last change of the project or of the shared folders started, or a
    ///     completed task when there was none. Tests wait for it.
    /// </summary>
    internal Task PendingRefresh
    {
        get
        {
            lock (_gate)
            {
                return _pendingRefresh;
            }
        }
    }

    /// <inheritdoc />
    public StyleKitEntry? Resolve(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Volatile.Read(ref _snapshot).ById.GetValueOrDefault(id);
    }

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Note what the refresh starts from, so that a newer refresh or a change of the user's or the project's folder that
        // finishes first is not replaced by this one
        long number;
        long localStamp;
        lock (_gate)
        {
            number = ++_startedRefresh;
            localStamp = _localStamp;
        }

        var projectFolder = _projects.Current?.FolderPath;
        var sharedFolders = _settings.Current.Styles.SharedKitFolders;

        // The places on this computer are read together on one worker; every shared folder gets a worker and a time limit,
        // so that all of them wait at the same time and the whole refresh takes about the longest limit and no more
        var local = Task.Run(() => (Project: ScanProject(projectFolder), User: ScanUser(), BuiltIn: ScanBuiltIn()), CancellationToken.None);
        var shared = sharedFolders.Select(ScanSharedAsync).ToArray();
        var sharedScans = await Task.WhenAll(shared).ConfigureAwait(false);
        var scans = await local.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // Apply the result unless a newer refresh has been applied; a change made to the user's or the project's folder
        // while this refresh was reading keeps what that change read
        var applied = false;
        lock (_gate)
        {
            if (number > _appliedRefresh)
            {
                _appliedRefresh = number;
                _builtInScan = scans.BuiltIn;
                _sharedScans = sharedScans;
                _knownSharedFolders = sharedFolders;
                if (_localStamp == localStamp)
                {
                    _projectScan = scans.Project;
                    _userScan = scans.User;
                }

                Rebuild();
                applied = true;
            }
        }

        if (applied)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public StyleKitEntry Save(StyleKit kit, StyleKitLocation target)
    {
        ArgumentNullException.ThrowIfNull(kit);

        var root = WritableRoot(target);
        var errors = StyleKitSerializer.Validate(kit).Where(issue => issue.IsError).ToList();
        if (errors.Count > 0)
        {
            throw new ArgumentException($"The kit cannot be saved: {string.Join(" ", errors)}", nameof(kit));
        }

        var folder = PathHelpers.SafePathCombine(root, kit.Id);
        Directory.CreateDirectory(folder);
        WriteKitFile(folder, kit);
        Log(LogLevel.Info, $"Saved the style kit '{kit.Id}' to '{folder}'.");
        return RescanLocalAndFind(kit.Id, target);
    }

    /// <inheritdoc />
    public StyleKitEntry CreateBlank(string name, StyleKitLocation target)
    {
        var root = WritableRoot(target);
        var displayName = Truncate(string.IsNullOrWhiteSpace(name) ? DefaultKitName : name.Trim(), StyleKitSerializer.MaxNameLength);
        var id = CreateUniqueId(displayName, root);

        // A starter that is valid and readable: dark text on a white page with one accent color
        var kit = new StyleKit(
            id,
            displayName,
            string.Empty,
            new Dictionary<string, string> { ["background"] = "#FFFFFF", ["text"] = "#0F172A", ["primary"] = "#2563EB" },
            new StyleFonts("sans-serif", "sans-serif", "monospace"),
            2,
            8,
            16,
            null,
            null,
            new StyleGuidanceText(string.Empty, [], []),
            [],
            []);
        return Save(kit, target);
    }

    /// <inheritdoc />
    public StyleKitEntry Duplicate(StyleKitEntry source, string newName, StyleKitLocation target)
    {
        ArgumentNullException.ThrowIfNull(source);

        var root = WritableRoot(target);
        var displayName = Truncate(string.IsNullOrWhiteSpace(newName) ? source.Kit.Name : newName.Trim(), StyleKitSerializer.MaxNameLength);
        var id = CreateUniqueId(displayName, root);
        CopyKitFolder(source, root, id, displayName);
        Log(LogLevel.Info, $"Duplicated the style kit '{source.Kit.Id}' as '{id}' in '{root}'.");
        return RescanLocalAndFind(id, target);
    }

    /// <inheritdoc />
    public void Delete(StyleKitEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Only the kits of the user and of the project may be removed, and only a folder that is a direct child of the
        // place that the entry says it is in, so that a crafted entry cannot make the catalog remove another folder
        if (entry.Location is not (StyleKitLocation.User or StyleKitLocation.Project))
        {
            throw new InvalidOperationException(entry.Location == StyleKitLocation.BuiltIn
                ? "A kit that comes with the program cannot be deleted."
                : "A shared kit cannot be deleted here; ask whoever keeps the shared folder.");
        }

        var root = WritableRoot(entry.Location);
        var folder = Path.GetFullPath(entry.FolderPath);
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder));
        if (parent is null || !string.Equals(parent, root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(folder))
        {
            throw new InvalidOperationException($"The kit folder '{entry.FolderPath}' is not in the place that holds the kits of this kind.");
        }

        Directory.Delete(folder, recursive: true);
        Log(LogLevel.Info, $"Deleted the style kit folder '{folder}'.");
        RescanLocal();
    }

    /// <inheritdoc />
    public StyleKitEntry CopyIntoProject(StyleKitEntry source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var root = WritableRoot(StyleKitLocation.Project);
        var id = source.Kit.Id;
        if (Directory.Exists(PathHelpers.SafePathCombine(root, id)))
        {
            throw new InvalidOperationException($"The project already has a kit with the id '{id}'.");
        }

        CopyKitFolder(source, root, id, source.Kit.Name);
        Log(LogLevel.Info, $"Copied the style kit '{id}' into the project folder '{root}'.");
        return RescanLocalAndFind(id, StyleKitLocation.Project);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _projects.CurrentChanged -= OnProjectChanged;
        _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>
    ///     Makes the id of a new kit from its name: lower-case letters and digits, with a dash for each run of other
    ///     characters, and a number when that id is taken.
    /// </summary>
    /// <param name="name">The name of the kit.</param>
    /// <returns>The id base, without a number; <c>new-kit</c> when the name has no letter or digit.</returns>
    internal static string IdBaseFromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var builder = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var text = builder.ToString().Trim('-');
        if (text.Length > MaxIdBaseLength)
        {
            text = text[..MaxIdBaseLength].TrimEnd('-');
        }

        return text.Length == 0 ? "new-kit" : text;
    }

    /// <summary>
    ///     Reads the folder of the open project.
    /// </summary>
    /// <param name="projectFolder">The folder of the open project, or <see langword="null" /> when none is open.</param>
    /// <returns>The scan, or <see langword="null" /> when no project is open.</returns>
    private RootScan? ScanProject(string? projectFolder)
    {
        return projectFolder is null
            ? null
            : StyleKitScanner.Scan(
                StyleKitLocation.Project,
                PathHelpers.SafePathCombine(Path.GetFullPath(projectFolder), ProjectFolderName),
                null,
                missingIsProblem: false,
                _listSubFolders,
                _logger);
    }

    /// <summary>
    ///     Reads the folder of the kits of the user.
    /// </summary>
    /// <returns>The scan.</returns>
    private RootScan ScanUser() =>
        StyleKitScanner.Scan(StyleKitLocation.User, _userFolder, null, missingIsProblem: false, _listSubFolders, _logger);

    /// <summary>
    ///     Reads the folder of the kits that come with the program.
    /// </summary>
    /// <returns>The scan.</returns>
    private RootScan ScanBuiltIn() =>
        StyleKitScanner.Scan(StyleKitLocation.BuiltIn, _builtInFolder, null, missingIsProblem: true, _listSubFolders, _logger);

    /// <summary>
    ///     Reads a shared folder on a worker with the time limit.
    /// </summary>
    /// <param name="folder">The shared folder, a full path.</param>
    /// <returns>The scan, or a scan that says the folder did not answer in time or could not be read.</returns>
    private async Task<RootScan> ScanSharedAsync(string folder)
    {
        var work = Task.Run(
            () => StyleKitScanner.Scan(StyleKitLocation.Shared, folder, folder, missingIsProblem: true, _listSubFolders, _logger),
            CancellationToken.None);
        try
        {
            var finished = await Task.WhenAny(work, Task.Delay(_sharedScanTimeout)).ConfigureAwait(false);
            if (finished == work)
            {
                return await work.ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // An unexpected failure of the scan is a folder that cannot be used, not a failure of the refresh
            Log(LogLevel.Error, $"Reading the shared style kit folder '{folder}' failed.", exception);
            return RootScan.Unavailable(StyleKitLocation.Shared, folder, folder, $"The folder could not be read: {exception.Message}");
        }

        // The scan is left to finish by itself; its result and any failure are not used, but a failure must be observed
        _ = work.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        Log(LogLevel.Warning, $"The shared style kit folder '{folder}' did not answer within {_sharedScanTimeout.TotalSeconds:0.#} seconds.");
        return RootScan.Unavailable(
            StyleKitLocation.Shared,
            folder,
            folder,
            $"The folder did not answer within {_sharedScanTimeout.TotalSeconds:0.#} seconds; it may be offline.");
    }

    /// <summary>
    ///     Reads the folders of the user and of the open project again, now, and announces the change.
    /// </summary>
    private void RescanLocal()
    {
        var projectFolder = _projects.Current?.FolderPath;
        lock (_gate)
        {
            _localStamp++;
            _userScan = ScanUser();
            _projectScan = ScanProject(projectFolder);
            Rebuild();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Reads the folders of the user and of the open project again and finds the entry of a kit that was just written.
    /// </summary>
    /// <param name="id">The id of the kit.</param>
    /// <param name="location">The place it was written to.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="IOException">Thrown when the kit that was written cannot be read back.</exception>
    private StyleKitEntry RescanLocalAndFind(string id, StyleKitLocation location)
    {
        RescanLocal();
        return Volatile.Read(ref _snapshot).Entries.FirstOrDefault(e => e.Location == location && e.Kit.Id == id)
               ?? throw new IOException($"The style kit '{id}' was written but cannot be read back; see the log.");
    }

    /// <summary>
    ///     Gives the folder of a place that the application may write to.
    /// </summary>
    /// <param name="target">The place.</param>
    /// <returns>The full path of the folder, which may not exist yet.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the place is read only, or is the project and none is open.</exception>
    private string WritableRoot(StyleKitLocation target)
    {
        switch (target)
        {
            case StyleKitLocation.User:
                return _userFolder;
            case StyleKitLocation.Project:
                var project = _projects.Current
                              ?? throw new InvalidOperationException("No project is open, so there is no project folder for the kit.");
                return PathHelpers.SafePathCombine(Path.GetFullPath(project.FolderPath), ProjectFolderName);
            default:
                throw new InvalidOperationException(target == StyleKitLocation.BuiltIn
                    ? "The kits that come with the program cannot be changed."
                    : "Shared kits cannot be changed here.");
        }
    }

    /// <summary>
    ///     Makes an id from a name that no known kit and no folder of the target has.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="root">The folder that the kit is written to.</param>
    /// <returns>The id.</returns>
    private string CreateUniqueId(string name, string root)
    {
        // Every id that is known counts, also those of other places: a new kit with the id of a kit that comes with the
        // program would hide it, which is what a copy is for and not what a new kit is for
        var known = Volatile.Read(ref _snapshot).Entries.Select(e => e.Kit.Id).ToHashSet(StringComparer.Ordinal);
        var baseId = IdBaseFromName(name);
        var id = baseId;
        var number = 2;
        while (known.Contains(id) || Directory.Exists(PathHelpers.SafePathCombine(root, id)))
        {
            id = $"{baseId}-{number++}";
        }

        return id;
    }

    /// <summary>
    ///     Copies a kit folder to a new folder named as an id: the style file with a new id and name, and each example file
    ///     of the kit. The copy is made in a hidden folder and moved into place, so a failure leaves no half kit.
    /// </summary>
    /// <param name="source">The kit to copy.</param>
    /// <param name="root">The folder that the copy is made in.</param>
    /// <param name="id">The id of the copy.</param>
    /// <param name="name">The name of the copy.</param>
    /// <exception cref="IOException">Thrown when the copy cannot be made.</exception>
    private static void CopyKitFolder(StyleKitEntry source, string root, string id, string name)
    {
        Directory.CreateDirectory(root);
        var target = PathHelpers.SafePathCombine(root, id);
        var staging = PathHelpers.SafePathCombine(root, $".{id}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(staging);

            // Only the files that the kit lists as examples are copied, which were found as files of the examples folder
            // and are not links, and each name is joined to the new folder with the safe combination
            var examples = new List<StyleExample>();
            if (source.Kit.Examples.Count > 0)
            {
                var examplesFolder = Directory.CreateDirectory(PathHelpers.SafePathCombine(staging, StyleKitSerializer.ExamplesFolderName)).FullName;
                foreach (var example in source.Kit.Examples)
                {
                    var fileName = Path.GetFileName(example.RelativePath);
                    var destination = PathHelpers.SafePathCombine(examplesFolder, fileName);
                    File.Copy(PathHelpers.SafePathCombine(PathHelpers.SafePathCombine(source.FolderPath, StyleKitSerializer.ExamplesFolderName), fileName), destination);
                    examples.Add(example with { FullPath = PathHelpers.SafePathCombine(PathHelpers.SafePathCombine(target, StyleKitSerializer.ExamplesFolderName), fileName) });
                }
            }

            WriteKitFile(staging, source.Kit with { Id = id, Name = name, Examples = examples });
            Directory.Move(staging, target);
        }
        catch
        {
            TryDeleteFolder(staging);
            throw;
        }
    }

    /// <summary>
    ///     Writes the style file of a kit into a folder, through a temporary file that is moved over the style file.
    /// </summary>
    /// <param name="folder">The kit folder, which exists.</param>
    /// <param name="kit">The kit.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    private static void WriteKitFile(string folder, StyleKit kit)
    {
        var path = PathHelpers.SafePathCombine(folder, StyleKitSerializer.FileName);
        var temp = PathHelpers.SafePathCombine(folder, $"{StyleKitSerializer.FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, StyleKitSerializer.Write(kit), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temp, path, overwrite: true);
        }
        catch (UnauthorizedAccessException exception)
        {
            TryDeleteFile(temp);
            throw new IOException($"The kit file '{path}' could not be written: {exception.Message}", exception);
        }
        catch
        {
            TryDeleteFile(temp);
            throw;
        }
    }

    /// <summary>
    ///     Cuts a text to a length.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The most characters to keep.</param>
    /// <returns>The text, at most that long, without white space at the end.</returns>
    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : text[..length].TrimEnd();

    /// <summary>
    ///     Deletes a folder, ignoring failures.
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
            // Best effort: a leftover hidden folder is not listed as a kit.
        }
    }

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
            // Best effort: a leftover temporary file is not read as a kit.
        }
    }

    /// <summary>
    ///     Starts a refresh because the open project changed.
    /// </summary>
    /// <param name="sender">The project service.</param>
    /// <param name="e">The event data.</param>
    private void OnProjectChanged(object? sender, EventArgs e) => StartBackgroundRefresh();

    /// <summary>
    ///     Starts a refresh when the shared folders changed; other settings changes are none of the catalog's business.
    /// </summary>
    /// <param name="sender">The settings service.</param>
    /// <param name="e">The event data, with the settings now in effect.</param>
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        bool changed;
        lock (_gate)
        {
            changed = !e.Settings.Styles.SharedKitFolders.SequenceEqual(_knownSharedFolders, StringComparer.Ordinal);
        }

        if (changed)
        {
            StartBackgroundRefresh();
        }
    }

    /// <summary>
    ///     Starts a refresh that nobody waits for, and logs a failure of it.
    /// </summary>
    private void StartBackgroundRefresh()
    {
        if (_disposed)
        {
            return;
        }

        var task = RefreshInBackgroundAsync();
        lock (_gate)
        {
            _pendingRefresh = task;
        }
    }

    /// <summary>
    ///     Refreshes and logs a failure instead of letting it escape on a task that nobody observes.
    /// </summary>
    /// <returns>A task that completes when the refresh has finished or failed.</returns>
    private async Task RefreshInBackgroundAsync()
    {
        try
        {
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log(LogLevel.Error, "Refreshing the style kits failed.", exception);
        }
    }

    /// <summary>
    ///     Builds the entries, the locations, and the index of the winners from the scans, in the order of precedence. The
    ///     caller holds the lock.
    /// </summary>
    private void Rebuild()
    {
        var scans = new List<RootScan>();
        if (_projectScan is not null)
        {
            scans.Add(_projectScan);
        }

        if (_userScan is not null)
        {
            scans.Add(_userScan);
        }

        scans.AddRange(_sharedScans);
        if (_builtInScan is not null)
        {
            scans.Add(_builtInScan);
        }

        // The first scan that has an id owns it, and every later kit of that id is overridden by that scan's place
        var owners = new Dictionary<string, RootScan>(StringComparer.Ordinal);
        foreach (var scan in scans)
        {
            foreach (var kit in scan.Kits)
            {
                owners.TryAdd(kit.Kit.Id, scan);
            }
        }

        var entries = new List<StyleKitEntry>();
        var byId = new Dictionary<string, StyleKitEntry>(StringComparer.Ordinal);
        foreach (var scan in scans)
        {
            foreach (var kit in scan.Kits.OrderBy(k => k.Kit.Name, StringComparer.OrdinalIgnoreCase).ThenBy(k => k.Kit.Id, StringComparer.Ordinal))
            {
                var owner = owners[kit.Kit.Id];
                var overridden = !ReferenceEquals(owner, scan);
                var entry = new StyleKitEntry(
                    kit.Kit,
                    scan.Location,
                    kit.FolderPath,
                    scan.SharedRoot,
                    scan.Location is StyleKitLocation.Shared or StyleKitLocation.BuiltIn,
                    overridden,
                    overridden ? Describe(owner) : null,
                    kit.Warnings);
                entries.Add(entry);
                if (!overridden)
                {
                    byId[kit.Kit.Id] = entry;
                }
            }
        }

        var locations = scans
            .Select(s => new StyleLocationStatus(s.Location, s.Path, s.IsAvailable, s.Problem, s.Broken))
            .ToList();
        Volatile.Write(ref _snapshot, new Snapshot(entries, locations, byId));
    }

    /// <summary>
    ///     Names a place for the note that a kit is overridden by it.
    /// </summary>
    /// <param name="scan">The place.</param>
    /// <returns>The name.</returns>
    private static string Describe(RootScan scan) => scan.Location switch
    {
        StyleKitLocation.Project => "This project",
        StyleKitLocation.User => "My kits",
        StyleKitLocation.Shared => $"Shared folder {scan.SharedRoot}",
        _ => "Built-in kits"
    };

    /// <summary>
    ///     Writes a log entry when there is a logger; failing to log never fails an operation.
    /// </summary>
    /// <param name="level">The severity.</param>
    /// <param name="message">The text.</param>
    /// <param name="exception">The failure, or <see langword="null" />.</param>
    private void Log(LogLevel level, string message, Exception? exception = null)
    {
        try
        {
            _logger?.Log(level, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a problem with a kit worse.
        }
    }

    /// <summary>
    ///     What readers see: the entries, the locations, and the entry of each id that applies.
    /// </summary>
    /// <param name="Entries">Every kit, in the order of the places.</param>
    /// <param name="Locations">The state of each place.</param>
    /// <param name="ById">The kit that is not overridden, by id.</param>
    private sealed record Snapshot(
        IReadOnlyList<StyleKitEntry> Entries,
        IReadOnlyList<StyleLocationStatus> Locations,
        IReadOnlyDictionary<string, StyleKitEntry> ById)
    {
        /// <summary>
        ///     Gets the snapshot before the first refresh: nothing was found.
        /// </summary>
        public static Snapshot Empty { get; } = new([], [], new Dictionary<string, StyleKitEntry>());
    }
}

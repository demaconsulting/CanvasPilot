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

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     View model of the project explorer. It shows the open project's folder as a tree: folders first and then files, each
///     sorted by name, with the sub-folders of the project (such as <c>attachments</c> and <c>styles</c>) down to a limit.
///     A file is listed when CanvasNet draws it or when it is an asset of the project (a bitmap, a PDF, or a
///     <c>style.json</c> file of a kit). The view model keeps the tree current by watching the folder, remembers which
///     folders are expanded, and deletes or renames the selected picture. The hidden <c>.canvaspilot</c> data folder, the
///     project file, and hidden files are never listed.
/// </summary>
/// <remarks>
///     <see cref="Items" /> is the flat list of the files that can be previewed, wherever they lie in the tree, and
///     <see cref="SelectedItem" /> is the selected one of them; selecting a folder or a file that is not drawn leaves it
///     <see langword="null" />. <see cref="SelectedNode" /> is the selected node of the tree, whatever it is.
/// </remarks>
public sealed partial class ProjectExplorerViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     How long the explorer waits after the last file system change before it refreshes.
    /// </summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(250);

    /// <summary>
    ///     The most nodes the tree holds, so that a folder with a huge number of files cannot stall the application.
    /// </summary>
    public const int MaximumNodes = 5000;

    /// <summary>
    ///     How many folders deep the tree goes.
    /// </summary>
    public const int MaximumDepth = 12;

    /// <summary>
    ///     Decides which files are listed.
    /// </summary>
    private readonly IImageSourceFilter _filter;

    /// <summary>
    ///     Delays refreshes so a burst of changes causes one refresh.
    /// </summary>
    private readonly IRefreshScheduler _scheduler;

    /// <summary>
    ///     How long to wait after a change before refreshing.
    /// </summary>
    private readonly TimeSpan _debounce;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     How many nodes the tree may hold.
    /// </summary>
    private readonly int _maximumNodes;

    /// <summary>
    ///     The listed files that can be previewed.
    /// </summary>
    private readonly ObservableCollection<ProjectFileItem> _items = [];

    /// <summary>
    ///     The nodes at the top of the tree.
    /// </summary>
    private readonly ObservableCollection<ProjectNodeViewModel> _roots = [];

    /// <summary>
    ///     The full paths of the folders that the user has expanded, which survive a refresh.
    /// </summary>
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);

    /// <summary>
    ///     Guards the watcher, the pending refresh, and the generation, which file system callbacks reach from
    ///     other threads.
    /// </summary>
    private readonly Lock _syncRoot = new();

    /// <summary>
    ///     The watcher on the project folder, or null.
    /// </summary>
    private FileSystemWatcher? _watcher;

    /// <summary>
    ///     The refresh waiting for its delay, or null.
    /// </summary>
    private IDisposable? _pendingRefresh;

    /// <summary>
    ///     Counts the projects shown so far, so a refresh scheduled for an earlier project is ignored.
    /// </summary>
    private int _generation;

    /// <summary>
    ///     Whether a project folder is shown, so file system callbacks have something to refresh.
    /// </summary>
    private bool _active;

    /// <summary>
    ///     Whether the view model has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Whether the selected file and the selected node are being brought in line with each other.
    /// </summary>
    private bool _syncingSelection;

    /// <summary>
    ///     The selected file that can be previewed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFile))]
    private ProjectFileItem? _selectedItem;

    /// <summary>
    ///     The selected node of the tree, a folder or a file.
    /// </summary>
    [ObservableProperty]
    private ProjectNodeViewModel? _selectedNode;

    /// <summary>
    ///     The project folder shown, or null.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject), nameof(EmptyMessage))]
    private string? _folderPath;

    /// <summary>
    ///     The last problem, or an empty string.
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    ///     Whether the tree was cut short because the project folder holds more than <see cref="MaximumNodes" /> entries or
    ///     goes deeper than <see cref="MaximumDepth" /> folders.
    /// </summary>
    [ObservableProperty]
    private bool _isTruncated;

    /// <summary>
    ///     Creates an explorer that shows nothing until <see cref="Open" /> is called.
    /// </summary>
    /// <param name="filter">Decides which files are listed.</param>
    /// <param name="scheduler">Delays refreshes after file system changes.</param>
    /// <param name="debounce">How long to wait after a change before refreshing; <see cref="DefaultDebounce" /> by default.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="maximumNodes">The most nodes the tree holds; <see cref="MaximumNodes" /> by default.</param>
    public ProjectExplorerViewModel(
        IImageSourceFilter filter,
        IRefreshScheduler scheduler,
        TimeSpan? debounce = null,
        RollingFileLogger? logger = null,
        int maximumNodes = MaximumNodes)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumNodes, 1);

        _filter = filter;
        _scheduler = scheduler;
        _debounce = debounce ?? DefaultDebounce;
        _logger = logger;
        _maximumNodes = maximumNodes;
        Items = new ReadOnlyObservableCollection<ProjectFileItem>(_items);
        Roots = new ReadOnlyObservableCollection<ProjectNodeViewModel>(_roots);
        _items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EmptyMessage));
        _roots.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EmptyMessage));
    }

    /// <summary>
    ///     Gets the listed files that can be previewed, wherever they lie in the tree, in the order of the tree.
    /// </summary>
    public ReadOnlyObservableCollection<ProjectFileItem> Items { get; }

    /// <summary>
    ///     Gets the nodes at the top of the tree: the folders of the project folder, then its files.
    /// </summary>
    public ReadOnlyObservableCollection<ProjectNodeViewModel> Roots { get; }

    /// <summary>
    ///     Gets the full path of the selected file, or <see langword="null" /> when nothing is selected.
    /// </summary>
    public string? SelectedFile => SelectedItem?.FullPath;

    /// <summary>
    ///     Gets a value indicating whether a project folder is shown.
    /// </summary>
    public bool HasProject => FolderPath is not null;

    /// <summary>
    ///     Gets a value indicating whether the project folder is being watched for changes.
    /// </summary>
    public bool IsWatching
    {
        get
        {
            lock (_syncRoot)
            {
                return _watcher is not null;
            }
        }
    }

    /// <summary>
    ///     Gets text that explains an empty list, or an empty string when files are listed.
    /// </summary>
    public string EmptyMessage
    {
        get
        {
            if (FolderPath is null)
            {
                return "No project is open.";
            }

            return _roots.Count == 0 ? "This project has no image files yet." : string.Empty;
        }
    }

    /// <summary>
    ///     Shows the files of a project folder, replacing any folder shown before, and starts watching it.
    /// </summary>
    /// <param name="projectFolder">The project folder.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the view model has been disposed.</exception>
    public void Open(string projectFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var folder = Path.GetFullPath(projectFolder.Trim());
        lock (_syncRoot)
        {
            StopWatching();
            _generation++;
            _active = true;
        }

        // Another project has other folders to expand
        if (!string.Equals(FolderPath, folder, StringComparison.Ordinal))
        {
            _expanded.Clear();
        }

        FolderPath = folder;
        SelectedItem = null;
        SelectedNode = null;
        _items.Clear();
        _roots.Clear();
        Refresh();
        StartWatching(folder);
    }

    /// <summary>
    ///     Stops watching, forgets the project folder, and empties the list.
    /// </summary>
    public void Close()
    {
        lock (_syncRoot)
        {
            StopWatching();
            _generation++;
            _active = false;
        }

        FolderPath = null;
        SelectedItem = null;
        SelectedNode = null;
        _items.Clear();
        _roots.Clear();
        _expanded.Clear();
        IsTruncated = false;
        ErrorMessage = string.Empty;
    }

    /// <summary>
    ///     Reads the project folder again and updates the tree and the list, keeping the selection and the folders that are
    ///     expanded when they are still there. Must be called on the thread that owns the list.
    /// </summary>
    public void Refresh()
    {
        if (FolderPath is not { } folder)
        {
            return;
        }

        var files = new List<ProjectFileItem>();
        var budget = _maximumNodes;
        try
        {
            // A problem in the top folder is shown, while one in a folder below it only leaves that folder empty
            IsTruncated = false;
            SyncFolder(_roots, folder, 0, files, ref budget);
            ErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Error, $"The project folder '{folder}' could not be read.", ex);
            ErrorMessage = $"The project folder could not be read: {ex.Message}";
            files.Clear();
            _roots.Clear();
        }

        SyncItems(files);
        if (SelectedItem is not null && !_items.Contains(SelectedItem))
        {
            SelectedItem = null;
        }

        if (SelectedNode is not null && FindNode(SelectedNode.FullPath) is null)
        {
            SelectedNode = null;
        }
    }

    /// <summary>
    ///     Asks for a refresh after the debounce delay. Several requests in quick succession cause one refresh.
    ///     May be called from any thread.
    /// </summary>
    public void RequestRefresh()
    {
        lock (_syncRoot)
        {
            if (!_active || _disposed)
            {
                return;
            }

            _pendingRefresh?.Dispose();
            var generation = _generation;
            _pendingRefresh = _scheduler.Schedule(_debounce, () => RunScheduledRefresh(generation));
        }
    }

    /// <summary>
    ///     Selects the listed file with the given path.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns><see langword="true" /> when the file is listed and is now selected.</returns>
    public bool SelectFile(string path)
    {
        var item = _items.FirstOrDefault(candidate => string.Equals(candidate.FullPath, path, StringComparison.OrdinalIgnoreCase));
        SelectedItem = item;
        if (item is not null)
        {
            ExpandAncestorsOf(item.FullPath);
        }

        return item is not null;
    }

    /// <summary>
    ///     Finds the node of the tree with a path.
    /// </summary>
    /// <param name="path">The full path of the file or folder.</param>
    /// <returns>The node, or <see langword="null" /> when the tree has none for the path.</returns>
    public ProjectNodeViewModel? FindNode(string path)
    {
        return FindNode(_roots, path);
    }

    /// <summary>
    ///     Deletes the selected file. The caller is responsible for asking the user to confirm first.
    /// </summary>
    /// <returns>A user-readable problem, or an empty string when the file was deleted.</returns>
    public string DeleteSelected()
    {
        if (!TryGetSelectedItem(out var item, out var problem))
        {
            return problem;
        }

        try
        {
            File.Delete(item.FullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Error, $"File '{item.FullPath}' could not be deleted.", ex);
            return ReportProblem($"'{item.Name}' could not be deleted: {ex.Message}");
        }

        Log(LogLevel.Info, $"Deleted '{item.FullPath}'.");
        Refresh();
        return string.Empty;
    }

    /// <summary>
    ///     Renames the selected file within the project folder, keeping it a supported image source file. An
    ///     existing file is never replaced.
    /// </summary>
    /// <param name="newName">The new file name, with its extension.</param>
    /// <returns>A user-readable problem, or an empty string when the file was renamed and selected.</returns>
    public string RenameSelected(string? newName)
    {
        if (!TryGetSelectedItem(out var item, out var problem))
        {
            return problem;
        }

        var trimmed = newName?.Trim() ?? string.Empty;
        var invalid = ProjectNameRules.Validate(trimmed, "file name");
        if (invalid is null && !_filter.IsImageSource(trimmed))
        {
            invalid = "The file name must end with an extension of a supported image source file.";
        }

        if (invalid is not null)
        {
            return ReportProblem(invalid);
        }

        if (string.Equals(trimmed, item.Name, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        string target;
        try
        {
            target = PathHelpers.SafePathCombine(Path.GetDirectoryName(item.FullPath) ?? FolderPath!, trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ReportProblem($"'{trimmed}' is not a valid file name.");
        }

        var onlyCaseChanges = string.Equals(target, item.FullPath, StringComparison.OrdinalIgnoreCase);
        if (!onlyCaseChanges && (File.Exists(target) || Directory.Exists(target)))
        {
            return ReportProblem($"A file or folder named '{trimmed}' already exists in the project.");
        }

        try
        {
            File.Move(item.FullPath, target, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Error, $"File '{item.FullPath}' could not be renamed to '{target}'.", ex);
            return ReportProblem($"'{item.Name}' could not be renamed: {ex.Message}");
        }

        Log(LogLevel.Info, $"Renamed '{item.FullPath}' to '{target}'.");
        Refresh();
        SelectFile(target);
        return string.Empty;
    }

    /// <summary>
    ///     Stops watching and discards any pending refresh. The view model cannot be used afterwards.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            _disposed = true;
            StopWatching();
            _generation++;
            _active = false;
        }
    }

    /// <summary>
    ///     Brings the nodes of a folder in line with the folder on disk with the fewest changes, so that the selection, the
    ///     expansion, and the scroll position of what remains are kept, and does the same for the folders below it. The files
    ///     that can be previewed are added to a list in the order of the tree.
    /// </summary>
    /// <param name="target">The nodes of the folder.</param>
    /// <param name="folder">The full path of the folder.</param>
    /// <param name="depth">How many folders deep the folder is.</param>
    /// <param name="files">The files that can be previewed, which this method adds to.</param>
    /// <param name="budget">How many more nodes the tree may hold.</param>
    /// <exception cref="IOException">Thrown when the top folder cannot be read; a folder below it that cannot be read is left empty.</exception>
    private void SyncFolder(ObservableCollection<ProjectNodeViewModel> target, string folder, int depth, List<ProjectFileItem> files, ref int budget)
    {
        List<EntryInfo> desired;
        try
        {
            desired = ReadEntries(folder);
        }
        catch (Exception ex) when (depth > 0 && ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Warning, $"The folder '{folder}' could not be read.", ex);
            desired = [];
        }

        if (desired.Count > budget)
        {
            desired = desired.GetRange(0, budget);
            IsTruncated = true;
        }

        budget -= desired.Count;
        var wanted = desired.Select(entry => entry.FullPath).ToHashSet(StringComparer.Ordinal);
        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(target[index].FullPath))
            {
                target.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            var entry = desired[index];
            var found = -1;
            for (var candidate = index; candidate < target.Count && found < 0; candidate++)
            {
                if (string.Equals(target[candidate].FullPath, entry.FullPath, StringComparison.Ordinal))
                {
                    found = candidate;
                }
            }

            ProjectNodeViewModel node;
            if (found < 0)
            {
                node = CreateNode(entry);
                target.Insert(index, node);
            }
            else
            {
                node = target[found];
                if (found != index)
                {
                    target.Move(found, index);
                }
            }

            if (node.IsFolder)
            {
                if (depth + 1 < MaximumDepth)
                {
                    SyncFolder(node.MutableChildren, node.FullPath, depth + 1, files, ref budget);
                }
                else
                {
                    IsTruncated = true;
                }
            }
            else if (node.IsPreviewable)
            {
                files.Add(new ProjectFileItem(node.Name, node.FullPath));
            }
        }
    }

    /// <summary>
    ///     Reads what a folder holds that the explorer lists: its folders, except hidden ones, the data folder, and links to
    ///     other folders, and its files that CanvasNet draws or that are assets of the project, except hidden ones and the
    ///     project file. Folders come first, and each group is sorted by name ignoring case.
    /// </summary>
    /// <param name="folder">The full path of the folder.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="IOException">Thrown when the folder cannot be read.</exception>
    private List<EntryInfo> ReadEntries(string folder)
    {
        var info = new DirectoryInfo(folder);
        var folders = info.EnumerateDirectories()
            .Where(directory => !directory.Name.StartsWith('.')
                                && (directory.Attributes & (FileAttributes.Hidden | FileAttributes.ReparsePoint)) == 0)
            .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
            .Select(directory => new EntryInfo(directory.Name, directory.FullName, true));
        var files = info.EnumerateFiles()
            .Where(file => !file.Name.StartsWith('.')
                           && (file.Attributes & FileAttributes.Hidden) == 0
                           && !string.Equals(file.Extension, ProjectDocument.FileExtension, StringComparison.OrdinalIgnoreCase)
                           && (_filter.IsImageSource(file.Name) || ProjectNodeViewModel.IsListedAsset(file.Name)))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new EntryInfo(file.Name, file.FullName, false));
        return [.. folders, .. files];
    }

    /// <summary>
    ///     Makes the node for an entry, expanded when the user had expanded the folder before.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The node.</returns>
    private ProjectNodeViewModel CreateNode(EntryInfo entry)
    {
        var node = new ProjectNodeViewModel(
            entry.Name,
            entry.FullPath,
            entry.IsFolder,
            entry.IsFolder ? ProjectNodeKind.Folder : ProjectNodeViewModel.KindOf(entry.Name),
            !entry.IsFolder && _filter.IsImageSource(entry.Name));
        if (entry.IsFolder)
        {
            node.IsExpanded = _expanded.Contains(entry.FullPath);
            node.PropertyChanged += OnNodePropertyChanged;
        }

        return node;
    }

    /// <summary>
    ///     Remembers which folders the user expands and collapses.
    /// </summary>
    /// <param name="sender">The node.</param>
    /// <param name="e">The change details.</param>
    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectNodeViewModel.IsExpanded) && sender is ProjectNodeViewModel { IsFolder: true } node)
        {
            if (node.IsExpanded)
            {
                _expanded.Add(node.FullPath);
            }
            else
            {
                _expanded.Remove(node.FullPath);
            }
        }
    }

    /// <summary>
    ///     Finds a node below a list of nodes by its path.
    /// </summary>
    /// <param name="nodes">The nodes to look in.</param>
    /// <param name="path">The full path.</param>
    /// <returns>The node, or <see langword="null" />.</returns>
    private static ProjectNodeViewModel? FindNode(IEnumerable<ProjectNodeViewModel> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            if (node.IsFolder
                && path.StartsWith(node.FullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && FindNode(node.Children, path) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }

    /// <summary>
    ///     Expands the folders that hold a file, so that the file is visible in the tree.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    private void ExpandAncestorsOf(string path)
    {
        IEnumerable<ProjectNodeViewModel> level = _roots;
        while (true)
        {
            var folder = level.FirstOrDefault(node => node.IsFolder
                                                      && path.StartsWith(node.FullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (folder is null)
            {
                return;
            }

            folder.IsExpanded = true;
            level = folder.Children;
        }
    }

    /// <summary>
    ///     Brings the selected file and the selected node in line when the selected file was set.
    /// </summary>
    /// <param name="value">The selected file.</param>
    partial void OnSelectedItemChanged(ProjectFileItem? value)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            SelectedNode = value is null ? null : FindNode(value.FullPath);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>
    ///     Brings the selected file and the selected node in line when the selected node was set: the file is the node when
    ///     it can be previewed, and none otherwise.
    /// </summary>
    /// <param name="value">The selected node.</param>
    partial void OnSelectedNodeChanged(ProjectNodeViewModel? value)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            SelectedItem = value is { IsPreviewable: true }
                ? _items.FirstOrDefault(item => string.Equals(item.FullPath, value.FullPath, StringComparison.Ordinal))
                : null;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>
    ///     An entry of a folder on disk that the explorer lists.
    /// </summary>
    /// <param name="Name">The name of the file or folder.</param>
    /// <param name="FullPath">The full path.</param>
    /// <param name="IsFolder">Whether the entry is a folder.</param>
    private sealed record EntryInfo(string Name, string FullPath, bool IsFolder);

    /// <summary>
    ///     Changes the list to match a new list with the fewest changes, so the selection and scroll position of
    ///     files that remain are kept.
    /// </summary>
    /// <param name="desired">The files that should be listed, sorted by name.</param>
    private void SyncItems(List<ProjectFileItem> desired)
    {
        var wanted = desired.Select(item => item.FullPath).ToHashSet(StringComparer.Ordinal);
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(_items[index].FullPath))
            {
                _items.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            if (index >= _items.Count
                || !string.Equals(_items[index].FullPath, desired[index].FullPath, StringComparison.Ordinal))
            {
                _items.Insert(index, desired[index]);
            }
        }
    }

    /// <summary>
    ///     Gets the selected file when it can be changed.
    /// </summary>
    /// <param name="item">The selected file.</param>
    /// <param name="problem">The user-readable problem when there is no usable selection.</param>
    /// <returns><see langword="true" /> when a listed file is selected.</returns>
    private bool TryGetSelectedItem(out ProjectFileItem item, out string problem)
    {
        if (SelectedItem is { } selected && _items.Contains(selected))
        {
            item = selected;
            problem = string.Empty;
            return true;
        }

        item = null!;
        problem = ReportProblem("Select a file in the project first.");
        return false;
    }

    /// <summary>
    ///     Shows a problem to the user.
    /// </summary>
    /// <param name="message">The problem.</param>
    /// <returns>The same message.</returns>
    private string ReportProblem(string message)
    {
        ErrorMessage = message;
        return message;
    }

    /// <summary>
    ///     Starts watching a project folder. A folder that cannot be watched still lists correctly; it just does
    ///     not refresh by itself.
    /// </summary>
    /// <param name="folder">The project folder.</param>
    private void StartWatching(string folder)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            watcher.Created += OnFileSystemChanged;
            watcher.Deleted += OnFileSystemChanged;
            watcher.Changed += OnFileSystemChanged;
            watcher.Renamed += OnFileSystemChanged;
            watcher.Error += OnWatcherError;
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            Log(LogLevel.Warning, $"The project folder '{folder}' cannot be watched for changes.", ex);
            watcher?.Dispose();
            return;
        }

        lock (_syncRoot)
        {
            if (_disposed || !_active)
            {
                watcher.Dispose();
                return;
            }

            _watcher = watcher;
        }
    }

    /// <summary>
    ///     Stops the watcher and cancels the pending refresh. The caller holds the lock.
    /// </summary>
    private void StopWatching()
    {
        _pendingRefresh?.Dispose();
        _pendingRefresh = null;
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    /// <summary>
    ///     Requests a refresh when a file or folder in the project folder changed, unless it lies in the hidden data folder
    ///     of the project, which the explorer never lists.
    /// </summary>
    /// <param name="sender">The watcher.</param>
    /// <param name="e">The change.</param>
    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (FolderPath is { } folder && IsInDataFolder(folder, e.FullPath))
        {
            return;
        }

        RequestRefresh();
    }

    /// <summary>
    ///     Tells whether a path lies in the hidden data folder of the project, or is that folder.
    /// </summary>
    /// <param name="projectFolder">The full path of the project folder.</param>
    /// <param name="path">The full path that changed.</param>
    /// <returns><see langword="true" /> when the path is the data folder or lies inside it.</returns>
    private static bool IsInDataFolder(string projectFolder, string path)
    {
        var dataFolder = Path.Combine(projectFolder, ProjectDocument.DataFolderName);
        return string.Equals(path, dataFolder, StringComparison.OrdinalIgnoreCase)
               || path.StartsWith(dataFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Requests a refresh when the watcher lost track of changes.
    /// </summary>
    /// <param name="sender">The watcher.</param>
    /// <param name="e">The error.</param>
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Log(LogLevel.Warning, "The project folder watcher reported an error; refreshing the list.", e.GetException());
        RequestRefresh();
    }

    /// <summary>
    ///     Runs a refresh that was scheduled, unless the project changed or the view model closed meanwhile.
    /// </summary>
    /// <param name="generation">The generation that was current when the refresh was scheduled.</param>
    private void RunScheduledRefresh(int generation)
    {
        lock (_syncRoot)
        {
            if (_disposed || !_active || generation != _generation)
            {
                return;
            }

            _pendingRefresh = null;
        }

        Refresh();
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks the explorer.
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
}

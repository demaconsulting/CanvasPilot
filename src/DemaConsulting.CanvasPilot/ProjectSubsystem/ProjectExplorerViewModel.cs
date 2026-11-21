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
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     View model of the project explorer. It lists the image source files in the root of the open project's
///     folder, keeps the list current by watching the folder, and deletes or renames the selected file. Folders
///     inside the project, the hidden <c>.canvaspilot</c> data folder, the project file, and hidden files are
///     never listed.
/// </summary>
public sealed partial class ProjectExplorerViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     How long the explorer waits after the last file system change before it refreshes.
    /// </summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(250);

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
    ///     The listed files.
    /// </summary>
    private readonly ObservableCollection<ProjectFileItem> _items = [];

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
    ///     The selected file.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFile))]
    private ProjectFileItem? _selectedItem;

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
    ///     Creates an explorer that shows nothing until <see cref="Open" /> is called.
    /// </summary>
    /// <param name="filter">Decides which files are listed.</param>
    /// <param name="scheduler">Delays refreshes after file system changes.</param>
    /// <param name="debounce">How long to wait after a change before refreshing; <see cref="DefaultDebounce" /> by default.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ProjectExplorerViewModel(
        IImageSourceFilter filter,
        IRefreshScheduler scheduler,
        TimeSpan? debounce = null,
        RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scheduler);

        _filter = filter;
        _scheduler = scheduler;
        _debounce = debounce ?? DefaultDebounce;
        _logger = logger;
        Items = new ReadOnlyObservableCollection<ProjectFileItem>(_items);
        _items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EmptyMessage));
    }

    /// <summary>
    ///     Gets the listed files, sorted by name.
    /// </summary>
    public ReadOnlyObservableCollection<ProjectFileItem> Items { get; }

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

            return _items.Count == 0 ? "This project has no image files yet." : string.Empty;
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

        FolderPath = folder;
        SelectedItem = null;
        _items.Clear();
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
        _items.Clear();
        ErrorMessage = string.Empty;
    }

    /// <summary>
    ///     Reads the project folder again and updates the list, keeping the selection when the selected file is
    ///     still there. Must be called on the thread that owns the list.
    /// </summary>
    public void Refresh()
    {
        if (FolderPath is not { } folder)
        {
            return;
        }

        List<ProjectFileItem> found;
        try
        {
            found = Enumerate(folder);
            ErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Error, $"The project folder '{folder}' could not be read.", ex);
            ErrorMessage = $"The project folder could not be read: {ex.Message}";
            found = [];
        }

        SyncItems(found);
        if (SelectedItem is not null && !_items.Contains(SelectedItem))
        {
            SelectedItem = null;
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
        return item is not null;
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
            target = PathHelpers.SafePathCombine(FolderPath!, trimmed);
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
    ///     Lists the image source files in the root of a folder, sorted by name.
    /// </summary>
    /// <param name="folder">The project folder.</param>
    /// <returns>The files.</returns>
    /// <exception cref="IOException">Thrown when the folder cannot be read.</exception>
    private List<ProjectFileItem> Enumerate(string folder)
    {
        return new DirectoryInfo(folder)
            .EnumerateFiles()
            .Where(file => !file.Name.StartsWith('.')
                           && (file.Attributes & FileAttributes.Hidden) == 0
                           && !string.Equals(file.Extension, ProjectDocument.FileExtension, StringComparison.OrdinalIgnoreCase)
                           && _filter.IsImageSource(file.Name))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new ProjectFileItem(file.Name, file.FullName))
            .ToList();
    }

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
                IncludeSubdirectories = false,
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
    ///     Requests a refresh when a file or folder in the project folder changed.
    /// </summary>
    /// <param name="sender">The watcher.</param>
    /// <param name="e">The change.</param>
    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        RequestRefresh();
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

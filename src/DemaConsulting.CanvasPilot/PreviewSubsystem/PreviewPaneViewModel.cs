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
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     View model of the preview pane. It follows the file selected in the project explorer: whenever the
///     selection changes it cancels any render that is still running and renders the new file in the background,
///     and it renders again, after a short delay, when files in the project folder change on disk. A result of an
///     earlier render never replaces the result of a later one. It exposes the picture, the busy state, the error
///     (with its code and hint), and the notes of the render for its view.
/// </summary>
public sealed partial class PreviewPaneViewModel : ObservableObject, IRenderedImageSource, IDisposable
{
    /// <summary>
    ///     How long the pane waits after the last file system change before it renders again.
    /// </summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(400);

    /// <summary>
    ///     Draws the files.
    /// </summary>
    private readonly IImageRenderService _renderer;

    /// <summary>
    ///     The project explorer whose selection and folder the pane follows.
    /// </summary>
    private readonly ProjectExplorerViewModel _explorer;

    /// <summary>
    ///     Delays renders after file system changes so a burst of changes causes one render.
    /// </summary>
    private readonly IRefreshScheduler _scheduler;

    /// <summary>
    ///     How long to wait after a change before rendering again.
    /// </summary>
    private readonly TimeSpan _debounce;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The notes of the shown picture.
    /// </summary>
    private readonly ObservableCollection<RenderDiagnostic> _diagnostics = [];

    /// <summary>
    ///     Guards the watcher, the pending render, the cancellation source, and the generation, which file system
    ///     callbacks reach from other threads.
    /// </summary>
    private readonly Lock _syncRoot = new();

    /// <summary>
    ///     The watcher on the project folder, or null.
    /// </summary>
    private FileSystemWatcher? _watcher;

    /// <summary>
    ///     The render waiting for its delay, or null.
    /// </summary>
    private IDisposable? _pendingRender;

    /// <summary>
    ///     Cancels the render that is running, or null.
    /// </summary>
    private CancellationTokenSource? _renderCancellation;

    /// <summary>
    ///     Counts the renders started so far, so only the newest render may show its result.
    /// </summary>
    private int _generation;

    /// <summary>
    ///     Whether the view model has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     The task that completes when every render started so far has finished.
    /// </summary>
    private Task _renderCompletion = Task.CompletedTask;

    /// <summary>
    ///     Whether a render is running.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder), nameof(StatusText))]
    private bool _isBusy;

    /// <summary>
    ///     The shown picture.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage), nameof(ShowPlaceholder), nameof(StatusText))]
    private RenderedImage? _image;

    /// <summary>
    ///     Why the selected file could not be drawn.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasError),
        nameof(ErrorTitle),
        nameof(ErrorMessage),
        nameof(ErrorHint),
        nameof(HasErrorHint),
        nameof(ShowPlaceholder),
        nameof(StatusText))]
    private RenderError? _error;

    /// <summary>
    ///     Whether the picture is scaled down to fit the pane instead of being shown at its size.
    /// </summary>
    [ObservableProperty]
    private bool _fitToWindow = true;

    /// <summary>
    ///     The file that is rendered or was last rendered, or null.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _currentFile;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreviewPaneViewModel" /> class. It starts rendering as soon
    ///     as the explorer has a selected file.
    /// </summary>
    /// <param name="renderer">Draws the files.</param>
    /// <param name="explorer">The project explorer whose selection and folder the pane follows.</param>
    /// <param name="scheduler">Delays renders after file system changes.</param>
    /// <param name="debounce">How long to wait after a change before rendering; <see cref="DefaultDebounce" /> by default.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PreviewPaneViewModel(
        IImageRenderService renderer,
        ProjectExplorerViewModel explorer,
        IRefreshScheduler scheduler,
        TimeSpan? debounce = null,
        RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(scheduler);

        _renderer = renderer;
        _explorer = explorer;
        _scheduler = scheduler;
        _debounce = debounce ?? DefaultDebounce;
        _logger = logger;
        Diagnostics = new ReadOnlyObservableCollection<RenderDiagnostic>(_diagnostics);
        _diagnostics.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasDiagnostics));
            OnPropertyChanged(nameof(DiagnosticsHeader));
            OnPropertyChanged(nameof(StatusText));
        };

        _explorer.PropertyChanged += OnExplorerPropertyChanged;
        RestartWatcher();
        if (_explorer.SelectedFile is not null)
        {
            StartRender(keepCurrent: false);
        }
    }

    /// <inheritdoc />
    public event EventHandler? CurrentImageChanged;

    /// <summary>
    ///     Gets the notes of the shown picture, or of nothing when no picture is shown.
    /// </summary>
    public ReadOnlyObservableCollection<RenderDiagnostic> Diagnostics { get; }

    /// <inheritdoc />
    public RenderedImage? CurrentImage => Image;

    /// <summary>
    ///     Gets a task that completes when every render started so far has either applied its result or been
    ///     discarded because a newer render superseded it.
    /// </summary>
    public Task RenderCompletion => _renderCompletion;

    /// <summary>
    ///     Gets a value indicating whether a picture is shown.
    /// </summary>
    public bool HasImage => Image is not null;

    /// <summary>
    ///     Gets a value indicating whether the render failed.
    /// </summary>
    public bool HasError => Error is not null;

    /// <summary>
    ///     Gets a value indicating whether the explanation that nothing is shown applies: no picture, no error, and
    ///     no render running.
    /// </summary>
    public bool ShowPlaceholder => !HasImage && !HasError && !IsBusy;

    /// <summary>
    ///     Gets the text of the explanation that nothing is shown.
    /// </summary>
    public string PlaceholderText => _explorer.HasProject ? "Select an image file to preview it." : "No project is open.";

    /// <summary>
    ///     Gets the code of the error, shown as its title, or an empty string.
    /// </summary>
    public string ErrorTitle => Error is { } error ? $"Render failed ({error.Code})" : string.Empty;

    /// <summary>
    ///     Gets the message of the error, or an empty string.
    /// </summary>
    public string ErrorMessage => Error?.Message ?? string.Empty;

    /// <summary>
    ///     Gets the hint of the error, or an empty string when there is none.
    /// </summary>
    public string ErrorHint => Error?.Hint is { Length: > 0 } hint ? $"Hint: {hint}" : string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the error has a hint.
    /// </summary>
    public bool HasErrorHint => ErrorHint.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether the shown picture has notes.
    /// </summary>
    public bool HasDiagnostics => _diagnostics.Count > 0;

    /// <summary>
    ///     Gets the heading of the list of notes, with their number.
    /// </summary>
    public string DiagnosticsHeader => _diagnostics.Count == 1 ? "1 note" : $"{_diagnostics.Count} notes";

    /// <summary>
    ///     Gets the line shown in the status area: what is being rendered, the size and format of the picture and
    ///     the number of notes, or that the render failed.
    /// </summary>
    public string StatusText
    {
        get
        {
            var name = CurrentFile is null ? string.Empty : Path.GetFileName(CurrentFile);
            if (IsBusy)
            {
                return $"Rendering {name}...";
            }

            if (Image is { } image)
            {
                var text = $"{name}: {image.Width} x {image.Height} px, {image.Format}";
                return _diagnostics.Count > 0 ? $"{text}, {DiagnosticsHeader}" : text;
            }

            return Error is not null ? $"{name}: render failed" : string.Empty;
        }
    }

    /// <summary>
    ///     Decides whether a change of a file or folder inside the project folder should cause a new render. Changes
    ///     inside the hidden data folder and to the project file are not pictures or their dependencies.
    /// </summary>
    /// <param name="projectFolder">The full path of the project folder.</param>
    /// <param name="changedPath">The full path of the changed file or folder.</param>
    /// <returns><see langword="true" /> when the change can affect a rendered picture.</returns>
    public static bool IsRelevantChange(string projectFolder, string changedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedPath);

        var relative = Path.GetRelativePath(projectFolder, changedPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return true;
        }

        var firstSegment = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar)[0];
        if (string.Equals(firstSegment, ProjectDocument.DataFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isRootFile = string.Equals(relative, firstSegment, StringComparison.Ordinal);
        return !(isRootFile && string.Equals(Path.GetExtension(relative), ProjectDocument.FileExtension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Asks for a new render of the selected file after the debounce delay. Several requests in quick
    ///     succession cause one render. May be called from any thread; ignored while nothing is selected.
    /// </summary>
    public void RequestRerender()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _pendingRender?.Dispose();
            var generation = _generation;
            _pendingRender = _scheduler.Schedule(_debounce, () => RunScheduledRender(generation));
        }
    }

    /// <summary>
    ///     Stops watching, cancels any render, and discards any pending render. The view model cannot be used
    ///     afterwards.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;
            StopWatching();
            CancelRender();
        }

        _explorer.PropertyChanged -= OnExplorerPropertyChanged;
    }

    /// <summary>
    ///     Raises <see cref="CurrentImageChanged" /> after the picture changed.
    /// </summary>
    /// <param name="value">The new picture.</param>
    partial void OnImageChanged(RenderedImage? value)
    {
        CurrentImageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Follows the explorer's project folder and selection.
    /// </summary>
    /// <param name="sender">The explorer.</param>
    /// <param name="e">The change details.</param>
    private void OnExplorerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProjectExplorerViewModel.FolderPath):
                // A different project: whatever was shown belongs to the old one. The selection change that
                // follows (or the absence of one) decides what to render next.
                RestartWatcher();
                Reset();
                OnPropertyChanged(nameof(PlaceholderText));
                break;

            case nameof(ProjectExplorerViewModel.SelectedItem):
                StartRender(keepCurrent: false);
                break;
        }
    }

    /// <summary>
    ///     Runs a render that was requested after a file system change, unless the pane was disposed or the
    ///     selection changed meanwhile (which already started a newer render).
    /// </summary>
    /// <param name="generation">The generation that was current when the render was requested.</param>
    private void RunScheduledRender(int generation)
    {
        lock (_syncRoot)
        {
            if (_disposed || generation != _generation)
            {
                return;
            }

            _pendingRender = null;
        }

        StartRender(keepCurrent: true);
    }

    /// <summary>
    ///     Cancels the running render and starts rendering the selected file, or clears the pane when no file is
    ///     selected. Must run on the thread that owns the view model's state.
    /// </summary>
    /// <param name="keepCurrent">
    ///     Whether to keep showing the current picture and error until the new result arrives, as for a render
    ///     caused by a file change.
    /// </param>
    private void StartRender(bool keepCurrent)
    {
        var file = _explorer.SelectedFile;
        var folder = _explorer.FolderPath;
        if (file is null || folder is null)
        {
            Reset();
            return;
        }

        CancellationToken token;
        int generation;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _pendingRender?.Dispose();
            _pendingRender = null;
            CancelRender();
            generation = ++_generation;
            _renderCancellation = new CancellationTokenSource();
            token = _renderCancellation.Token;
        }

        if (!keepCurrent || !string.Equals(CurrentFile, file, StringComparison.Ordinal))
        {
            ClearResult();
        }

        CurrentFile = file;
        IsBusy = true;
        _renderCompletion = Task.WhenAll(_renderCompletion, RunRenderAsync(generation, file, folder, token));
    }

    /// <summary>
    ///     Cancels the running render, discards the pending render, makes any result still to come stale, and
    ///     clears everything the pane shows.
    /// </summary>
    private void Reset()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _pendingRender?.Dispose();
            _pendingRender = null;
            CancelRender();
            _generation++;
        }

        ClearAll();
    }

    /// <summary>
    ///     Renders a file and shows the result unless a newer render was started meanwhile.
    /// </summary>
    /// <param name="generation">The generation of this render.</param>
    /// <param name="file">The file to render.</param>
    /// <param name="folder">The project folder.</param>
    /// <param name="token">Cancels this render.</param>
    /// <returns>A task that completes when the result was applied or discarded.</returns>
    private async Task RunRenderAsync(int generation, string file, string folder, CancellationToken token)
    {
        RenderResult result;
        try
        {
            result = await _renderer.RenderAsync(file, folder, token);
        }
        catch (OperationCanceledException)
        {
            result = RenderResult.Cancelled();
        }
        catch (Exception ex)
        {
            // The render service reports ordinary failures as results; anything else is a defect in it, which
            // must not leave the pane busy forever or take the application down.
            Log(LogLevel.Error, $"Rendering '{file}' failed unexpectedly.", ex);
            result = RenderResult.Failure(new RenderError(RenderErrorCodes.OperationFailed, ex.Message));
        }

        lock (_syncRoot)
        {
            if (_disposed || generation != _generation)
            {
                return;
            }
        }

        Apply(result);
    }

    /// <summary>
    ///     Shows the result of the current render.
    /// </summary>
    /// <param name="result">The result.</param>
    private void Apply(RenderResult result)
    {
        IsBusy = false;
        if (result.IsCancelled)
        {
            return;
        }

        if (result.Image is { } image)
        {
            ReplaceDiagnostics(image.Diagnostics);
            Error = null;
            Image = image;
        }
        else
        {
            ReplaceDiagnostics([]);
            Image = null;
            Error = result.Error ?? new RenderError(RenderErrorCodes.OperationFailed, "The file could not be rendered.");
        }
    }

    /// <summary>
    ///     Cancels the running render and clears everything the pane shows.
    /// </summary>
    private void ClearAll()
    {
        CurrentFile = null;
        IsBusy = false;
        ClearResult();
    }

    /// <summary>
    ///     Clears the picture, the error, and the notes.
    /// </summary>
    private void ClearResult()
    {
        ReplaceDiagnostics([]);
        Image = null;
        Error = null;
    }

    /// <summary>
    ///     Replaces the notes with a new list.
    /// </summary>
    /// <param name="notes">The new notes.</param>
    private void ReplaceDiagnostics(IReadOnlyList<RenderDiagnostic> notes)
    {
        _diagnostics.Clear();
        foreach (var note in notes)
        {
            _diagnostics.Add(note);
        }
    }

    /// <summary>
    ///     Cancels the running render, if any. The caller holds the lock.
    /// </summary>
    private void CancelRender()
    {
        _renderCancellation?.Cancel();
        _renderCancellation?.Dispose();
        _renderCancellation = null;
    }

    /// <summary>
    ///     Starts watching the explorer's project folder, and everything below it, replacing any earlier watcher.
    ///     A folder that cannot be watched is logged; the pane then renders only when the selection changes.
    /// </summary>
    private void RestartWatcher()
    {
        lock (_syncRoot)
        {
            StopWatching();
        }

        var folder = _explorer.FolderPath;
        if (folder is null)
        {
            return;
        }

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
            if (_disposed)
            {
                watcher.Dispose();
                return;
            }

            _watcher = watcher;
        }
    }

    /// <summary>
    ///     Stops the watcher and discards the pending render. The caller holds the lock.
    /// </summary>
    private void StopWatching()
    {
        _pendingRender?.Dispose();
        _pendingRender = null;
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    /// <summary>
    ///     Requests a new render when a file or folder in the project folder changed and a file is selected.
    /// </summary>
    /// <param name="sender">The watcher.</param>
    /// <param name="e">The change.</param>
    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        var folder = _explorer.FolderPath;
        if (folder is null || _explorer.SelectedFile is null)
        {
            return;
        }

        if (IsRelevantChange(folder, e.FullPath) || (e is RenamedEventArgs renamed && IsRelevantChange(folder, renamed.OldFullPath)))
        {
            RequestRerender();
        }
    }

    /// <summary>
    ///     Requests a new render when the watcher lost track of changes.
    /// </summary>
    /// <param name="sender">The watcher.</param>
    /// <param name="e">The error.</param>
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Log(LogLevel.Warning, "The project folder watcher reported an error; rendering again.", e.GetException());
        if (_explorer.SelectedFile is not null)
        {
            RequestRerender();
        }
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks the pane.
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

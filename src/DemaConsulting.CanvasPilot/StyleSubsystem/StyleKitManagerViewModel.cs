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
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The window in which the user sees the style kits that were found, in groups by place, with a preview of the kit that
///     is chosen, and manages them: uses a kit in the project, creates one, duplicates one, edits one, deletes one, imports
///     and exports kits as zip files, and copies a kit into the project.
/// </summary>
/// <remarks>
///     <para>
///         The kits that come with the program and the kits that a team shares are read only: the commands that would
///         change them are not available for them, and the way to change one is to duplicate it. A kit that a more local
///         kit with the same id hides is shown dimmed and cannot be used in the project (the id would name the other
///         kit). The window shows a place that could not be read, and the folders that hold a kit that cannot be used, as
///         notes in their group, so a missing kit is never a mystery.
///     </para>
///     <para>
///         Everything that needs a window or a picker goes through <see cref="IStyleKitDialogs" />, so the view model is
///         tested without either. A problem that a command meets (a folder that cannot be written, an archive that is
///         refused) is said in <see cref="StatusMessage" /> and is not an exception. The view model follows the catalog,
///         the active style, and the project, on the thread that owns the user interface, until it is disposed.
///     </para>
/// </remarks>
public sealed partial class StyleKitManagerViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     The title of the window.
    /// </summary>
    public const string WindowTitle = "Style Kits";

    /// <summary>
    ///     The start of the message of the archive when the place already has a kit with the id of the kit that is imported.
    /// </summary>
    private const string ConflictMessageStart = "The place already has a kit with the id";

    /// <summary>
    ///     The names of the two places that a new kit can be made in, in the order of <see cref="TargetIndex" />.
    /// </summary>
    private static readonly string[] TargetNames = ["My kits", "This project"];

    /// <summary>
    ///     The kits and the places that hold them.
    /// </summary>
    private readonly IStyleKitCatalog _catalog;

    /// <summary>
    ///     The kit that the open project uses.
    /// </summary>
    private readonly IActiveStyle _active;

    /// <summary>
    ///     The windows and pickers.
    /// </summary>
    private readonly IStyleKitDialogs _dialogs;

    /// <summary>
    ///     Says whether a project is open.
    /// </summary>
    private readonly IProjectService _projects;

    /// <summary>
    ///     The pictures of the examples.
    /// </summary>
    private readonly StyleThumbnailCache _thumbnails;

    /// <summary>
    ///     Whether this view model made the cache and so disposes it.
    /// </summary>
    private readonly bool _ownsThumbnails;

    /// <summary>
    ///     Optional logger.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The synchronization context of the thread that created the view model, which owns the user interface, or null.
    /// </summary>
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>
    ///     The groups shown in the list.
    /// </summary>
    private readonly ObservableCollection<StyleKitGroupViewModel> _groups = [];

    /// <summary>
    ///     Keeps two rebuilds apart: outside a user interface the announcements of the catalog arrive on the thread that
    ///     read the kits, and two reads can finish at the same time.
    /// </summary>
    private readonly Lock _rebuildLock = new();

    /// <summary>
    ///     Cancels the drawing of the pictures of the kit that was chosen before.
    /// </summary>
    private CancellationTokenSource? _thumbnailCancellation;

    /// <summary>
    ///     The drawing of the pictures of the kit that is chosen, which tests wait for.
    /// </summary>
    private Task _thumbnailTask = Task.CompletedTask;

    /// <summary>
    ///     Whether the view model has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     The kit that is chosen in the list, or <see langword="null" />.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(IsSelectionReadOnly))]
    private StyleKitItemViewModel? _selectedItem;

    /// <summary>
    ///     What the last command did or why it could not, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    /// <summary>
    ///     The place that a new kit, a duplicate, and an imported kit go to: 0 for the kits of the user, 1 for the project.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetLocation))]
    private int _targetIndex;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitManagerViewModel" /> class and shows the kits that are known
    ///     now.
    /// </summary>
    /// <param name="catalog">The kits and the places that hold them.</param>
    /// <param name="active">The kit that the open project uses, and the means to change it.</param>
    /// <param name="renderer">Draws the example images of the kits as small pictures.</param>
    /// <param name="dialogs">The windows, questions, and pickers.</param>
    /// <param name="projects">Says whether a project is open.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="thumbnails">The pictures of the examples, or <see langword="null" /> to make a cache for this window.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null" />.</exception>
    public StyleKitManagerViewModel(
        IStyleKitCatalog catalog,
        IActiveStyle active,
        IImageRenderService renderer,
        IStyleKitDialogs dialogs,
        IProjectService projects,
        RollingFileLogger? logger = null,
        StyleThumbnailCache? thumbnails = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(projects);

        _catalog = catalog;
        _active = active;
        _dialogs = dialogs;
        _projects = projects;
        _logger = logger;
        _thumbnails = thumbnails ?? new StyleThumbnailCache(renderer);
        _ownsThumbnails = thumbnails is null;
        Groups = new ReadOnlyObservableCollection<StyleKitGroupViewModel>(_groups);

        Rebuild();
        _catalog.Changed += OnSourceChanged;
        _active.Changed += OnActiveChanged;
        _projects.CurrentChanged += OnSourceChanged;
    }

    /// <summary>
    ///     Raised when the window is asked to close.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    ///     Gets the groups of kits: the project, the user, the shared folders, and the kits that come with the program. The
    ///     group of the project is absent while no project is open.
    /// </summary>
    public ReadOnlyObservableCollection<StyleKitGroupViewModel> Groups { get; }

    /// <summary>
    ///     Gets the names of the places that a new kit can be made in, for the list that chooses one.
    /// </summary>
    public static IReadOnlyList<string> TargetChoices => TargetNames;

    /// <summary>
    ///     Gets a value indicating whether a kit is chosen.
    /// </summary>
    public bool HasSelection => SelectedItem is not null;

    /// <summary>
    ///     Gets a value indicating whether the chosen kit cannot be changed.
    /// </summary>
    public bool IsSelectionReadOnly => SelectedItem?.IsReadOnly == true;

    /// <summary>
    ///     Gets a value indicating whether a project is open.
    /// </summary>
    public bool HasProject => _projects.Current is not null;

    /// <summary>
    ///     Gets a value indicating whether there is a message about the last command.
    /// </summary>
    public bool HasStatusMessage => StatusMessage.Length > 0;

    /// <summary>
    ///     Gets the place that a new kit, a duplicate, and an imported kit go to.
    /// </summary>
    public StyleKitLocation TargetLocation => TargetIndex == 1 ? StyleKitLocation.Project : StyleKitLocation.User;

    /// <summary>
    ///     Gets the task that draws the pictures of the examples of the chosen kit, or a completed task when there is
    ///     nothing to draw. A window does not wait for it; tests do.
    /// </summary>
    public Task ThumbnailTask => Volatile.Read(ref _thumbnailTask);

    /// <summary>
    ///     Stops following the kits and the project and stops drawing pictures.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog.Changed -= OnSourceChanged;
        _active.Changed -= OnActiveChanged;
        _projects.CurrentChanged -= OnSourceChanged;
        CancelThumbnails();
        if (_ownsThumbnails)
        {
            _thumbnails.Dispose();
        }
    }

    /// <summary>
    ///     Chooses a kit in the list.
    /// </summary>
    /// <param name="item">The kit to choose.</param>
    [RelayCommand]
    private void SelectItem(StyleKitItemViewModel? item)
    {
        SelectedItem = item;
    }

    /// <summary>
    ///     Makes the chosen kit the style of the open project.
    /// </summary>
    /// <returns>A task that completes when the project file is written or the problem was said.</returns>
    [RelayCommand(CanExecute = nameof(CanUseInProject))]
    private async Task UseInProjectAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        try
        {
            await _active.SetActiveAsync(item.Id);
            Say($"'{item.Name}' is now the style of this project.");
        }
        catch (InvalidOperationException ex)
        {
            Log(LogLevel.Warning, $"The style kit '{item.Id}' was not made the style of the project: {ex.Message}");
            Say(ex.Message);
        }
    }

    /// <summary>
    ///     Asks for a name and makes a kit with a starter palette, then opens it in the editor.
    /// </summary>
    /// <returns>A task that completes when the editor has closed or the problem was said.</returns>
    [RelayCommand]
    private async Task NewAsync()
    {
        var name = await _dialogs.PromptAsync("New Style Kit", $"Name of the new kit (it is saved in {TargetNames[TargetIndex]}):", "New kit", "Create");
        if (name is null)
        {
            return;
        }

        if (!TryChange(() => _catalog.CreateBlank(name, TargetLocation), out var entry))
        {
            return;
        }

        Say($"Made the kit '{entry.Kit.Name}'.");
        await ShowAndEditAsync(entry);
    }

    /// <summary>
    ///     Asks for a name and makes a copy of the chosen kit, with its examples, then opens the copy in the editor.
    /// </summary>
    /// <returns>A task that completes when the editor has closed or the problem was said.</returns>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DuplicateAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var name = await _dialogs.PromptAsync(
            "Duplicate Style Kit",
            $"Name of the copy of '{item.Name}' (it is saved in {TargetNames[TargetIndex]}):",
            $"{item.Name} copy",
            "Duplicate");
        if (name is null)
        {
            return;
        }

        if (!TryChange(() => _catalog.Duplicate(item.Entry, name, TargetLocation), out var entry))
        {
            return;
        }

        Say($"Made the kit '{entry.Kit.Name}' as a copy of '{item.Name}'.");
        await ShowAndEditAsync(entry);
    }

    /// <summary>
    ///     Opens the chosen kit in the editor.
    /// </summary>
    /// <returns>A task that completes when the editor has closed.</returns>
    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task EditAsync()
    {
        if (SelectedItem is { } item)
        {
            await EditEntryAsync(item.Entry);
        }
    }

    /// <summary>
    ///     Asks to confirm and then deletes the chosen kit with its folder.
    /// </summary>
    /// <returns>A task that completes when the kit is deleted or the problem was said.</returns>
    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task DeleteAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Delete Style Kit",
            $"Delete the kit '{item.Name}' and everything in its folder?{Environment.NewLine}{item.Entry.FolderPath}",
            "Delete");
        if (!confirmed)
        {
            return;
        }

        try
        {
            _catalog.Delete(item.Entry);
            Rebuild();
            Say($"Deleted the kit '{item.Name}'.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Warning, $"The style kit '{item.Id}' could not be deleted: {ex.Message}");
            Say($"The kit could not be deleted: {ex.Message}");
        }
    }

    /// <summary>
    ///     Asks for a zip file and reads a kit from it into the place that new kits go to, asking what to do when that place
    ///     has a kit with the same id.
    /// </summary>
    /// <returns>A task that completes when the kit is imported or the reason it was not was said.</returns>
    [RelayCommand]
    private async Task ImportAsync()
    {
        var zip = await _dialogs.PickZipToImportAsync(null);
        if (zip is null)
        {
            return;
        }

        var root = _catalog.Locations.FirstOrDefault(place => place.Location == TargetLocation)?.Path;
        if (root is null)
        {
            Say("Open a project first: it is the place that this kit would be imported to.");
            return;
        }

        var result = await Task.Run(() => StyleKitArchive.Import(zip, root, StyleImportConflict.Refuse), CancellationToken.None);
        if (!result.Succeeded && result.Error?.StartsWith(ConflictMessageStart, StringComparison.Ordinal) == true)
        {
            var conflict = await AskConflictAsync(result.Error);
            if (conflict is null)
            {
                Say("Nothing was imported.");
                return;
            }

            result = await Task.Run(() => StyleKitArchive.Import(zip, root, conflict.Value), CancellationToken.None);
        }

        if (!result.Succeeded)
        {
            Log(LogLevel.Warning, $"The style kit in '{zip}' was not imported: {result.Error}");
            Say(result.Error ?? "The kit could not be imported.");
            return;
        }

        await _catalog.RefreshAsync(CancellationToken.None);
        Rebuild();
        SelectFolder(result.FolderPath);
        Say(result.Issues.Count == 0
            ? $"Imported the kit '{result.Kit!.Name}'."
            : $"Imported the kit '{result.Kit!.Name}' with {result.Issues.Count} warning(s); they are in the preview.");
    }

    /// <summary>
    ///     Asks where to write a zip file and writes the chosen kit to it.
    /// </summary>
    /// <returns>A task that completes when the file is written or the problem was said.</returns>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ExportAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var zip = await _dialogs.PickZipToExportAsync($"{item.Id}.zip");
        if (zip is null)
        {
            return;
        }

        try
        {
            await Task.Run(() => StyleKitArchive.Export(item.Entry, zip), CancellationToken.None);
            Say($"Exported the kit '{item.Name}' to {zip}.");
        }
        catch (IOException ex)
        {
            Log(LogLevel.Warning, $"The style kit '{item.Id}' could not be exported to '{zip}': {ex.Message}");
            Say(ex.Message);
        }
    }

    /// <summary>
    ///     Copies the chosen kit, with its examples and its id, into the <c>styles</c> folder of the open project.
    /// </summary>
    /// <returns>A task that completes when the kit is copied or the problem was said.</returns>
    [RelayCommand(CanExecute = nameof(CanCopyIntoProject))]
    private Task CopyIntoProjectAsync()
    {
        if (SelectedItem is { } item && TryChange(() => _catalog.CopyIntoProject(item.Entry), out var entry))
        {
            Rebuild();
            SelectFolder(entry.FolderPath, StyleKitLocation.Project);
            Say($"Copied the kit '{item.Name}' into the project, which now keeps its own copy.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Reads the places again.
    /// </summary>
    /// <returns>A task that completes when the kits are read.</returns>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _catalog.RefreshAsync(CancellationToken.None);
        Rebuild();
        Say("The kits were read again.");
    }

    /// <summary>
    ///     Asks the window to close.
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Gets a value indicating whether the chosen kit can be made the style of the open project: a project is open and
    ///     no more local kit has the id of the kit.
    /// </summary>
    private bool CanUseInProject() => SelectedItem is { IsOverridden: false } && HasProject;

    /// <summary>
    ///     Gets a value indicating whether the chosen kit can be edited and deleted: it is of the user or of the project.
    /// </summary>
    private bool CanChange() => SelectedItem is { IsReadOnly: false };

    /// <summary>
    ///     Gets a value indicating whether the chosen kit can be copied into the open project: a project is open and the
    ///     kit is not in it already.
    /// </summary>
    private bool CanCopyIntoProject() => SelectedItem is { } item && HasProject && item.Entry.Location != StyleKitLocation.Project;

    /// <summary>
    ///     Makes the lists and the commands follow a new choice: the flag of the item, the pictures of its examples, and
    ///     the commands that depend on it.
    /// </summary>
    /// <param name="value">The kit that is chosen now.</param>
    partial void OnSelectedItemChanged(StyleKitItemViewModel? value)
    {
        foreach (var item in _groups.SelectMany(group => group.Items))
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        NotifyCommands();
        StartThumbnails(value);
    }

    /// <summary>
    ///     Makes the target place valid and the commands follow it.
    /// </summary>
    /// <param name="value">The new index.</param>
    partial void OnTargetIndexChanged(int value)
    {
        if (value is < 0 or > 1)
        {
            TargetIndex = Math.Clamp(value, 0, 1);
        }
    }

    /// <summary>
    ///     Follows a change of the kits or of the project.
    /// </summary>
    /// <param name="sender">The source of the announcement.</param>
    /// <param name="e">The event data.</param>
    private void OnSourceChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(() =>
        {
            Rebuild();
            OnPropertyChanged(nameof(HasProject));
        });
    }

    /// <summary>
    ///     Follows a change of the kit that the project uses.
    /// </summary>
    /// <param name="sender">The source of the announcement.</param>
    /// <param name="e">The event data.</param>
    private void OnActiveChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(UpdateInUse);
    }

    /// <summary>
    ///     Makes the groups and their kits from the catalog, keeps the choice when the same kit is still there, and updates
    ///     which kit the project uses.
    /// </summary>
    private void Rebuild()
    {
        lock (_rebuildLock)
        {
            RebuildGroups();
        }
    }

    /// <summary>
    ///     Does the work of <see cref="Rebuild" /> while no other rebuild runs.
    /// </summary>
    private void RebuildGroups()
    {
        var previous = SelectedItem;
        var entries = _catalog.Entries;
        var places = _catalog.Locations;
        var groups = new List<StyleKitGroupViewModel>();
        foreach (var location in new[] { StyleKitLocation.Project, StyleKitLocation.User, StyleKitLocation.Shared, StyleKitLocation.BuiltIn })
        {
            var status = places.Where(place => place.Location == location).ToList();
            if (location == StyleKitLocation.Project && status.Count == 0)
            {
                // No project is open, so there is no place to show
                continue;
            }

            var items = entries.Where(entry => entry.Location == location).Select(entry => new StyleKitItemViewModel(entry)).ToList();
            groups.Add(new StyleKitGroupViewModel(location, GroupTitle(location), items, NotesOf(status), EmptyText(location, status.Count)));
        }

        _groups.Clear();
        foreach (var group in groups)
        {
            _groups.Add(group);
        }

        UpdateInUse();
        var match = previous is null ? null : _groups.SelectMany(group => group.Items).FirstOrDefault(item => item.IsSameKitAs(previous));
        if (ReferenceEquals(match, SelectedItem))
        {
            NotifyCommands();
        }
        else
        {
            SelectedItem = match;
        }

        OnPropertyChanged(nameof(HasProject));
    }

    /// <summary>
    ///     Marks the kit that the open project uses.
    /// </summary>
    private void UpdateInUse()
    {
        var id = _active.ActiveId;
        foreach (var item in _groups.SelectMany(group => group.Items))
        {
            item.IsInUse = id is not null && !item.IsOverridden && string.Equals(item.Id, id, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Tells the commands that what they depend on may have changed.
    /// </summary>
    private void NotifyCommands()
    {
        UseInProjectCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        CopyIntoProjectCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Stops drawing the pictures of the kit that was chosen and starts drawing those of the kit that is chosen now.
    /// </summary>
    /// <param name="item">The kit that is chosen now, or <see langword="null" />.</param>
    private void StartThumbnails(StyleKitItemViewModel? item)
    {
        CancelThumbnails();
        if (item is null || _disposed)
        {
            Volatile.Write(ref _thumbnailTask, Task.CompletedTask);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _thumbnailCancellation = cancellation;
        Volatile.Write(ref _thumbnailTask, LoadThumbnailsAsync(item, cancellation.Token));
    }

    /// <summary>
    ///     Cancels the drawing of the pictures that is going on.
    /// </summary>
    private void CancelThumbnails()
    {
        var cancellation = Interlocked.Exchange(ref _thumbnailCancellation, null);
        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    /// <summary>
    ///     Draws the small picture of each example of a kit and shows each as soon as it is drawn.
    /// </summary>
    /// <param name="item">The kit.</param>
    /// <param name="cancellationToken">Stops the work when another kit is chosen.</param>
    /// <returns>A task that completes when every picture is shown or its reason is.</returns>
    private async Task LoadThumbnailsAsync(StyleKitItemViewModel item, CancellationToken cancellationToken)
    {
        var rows = item.PrepareThumbnails();
        var examples = item.Entry.Kit.Examples;
        var tasks = new List<Task>();
        for (var i = 0; i < rows.Count; i++)
        {
            tasks.Add(LoadThumbnailAsync(item.Entry.FolderPath, examples[i], rows[i], cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    ///     Draws the small picture of one example and shows it.
    /// </summary>
    /// <param name="kitFolder">The folder of the kit.</param>
    /// <param name="example">The example.</param>
    /// <param name="row">The thumbnail that shows it.</param>
    /// <param name="cancellationToken">Stops the work when another kit is chosen.</param>
    /// <returns>A task that completes when the picture or its reason is shown.</returns>
    private async Task LoadThumbnailAsync(string kitFolder, StyleExample example, StyleThumbnailViewModel row, CancellationToken cancellationToken)
    {
        StyleThumbnail thumbnail;
        try
        {
            thumbnail = example.FullPath.Length == 0
                ? new StyleThumbnail(null, "The file is not there.")
                : await _thumbnails.GetAsync(example.FullPath, kitFolder, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Another kit was chosen, so the picture is no longer wanted
            return;
        }

        RunOnOwnerThread(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (thumbnail.Image is { } image)
            {
                row.Show(image);
            }
            else
            {
                row.ShowFailure(thumbnail.Error ?? "The file could not be drawn.");
            }
        });
    }

    /// <summary>
    ///     Opens a kit in the editor and shows the result.
    /// </summary>
    /// <param name="entry">The kit.</param>
    /// <returns>A task that completes when the editor has closed.</returns>
    private async Task EditEntryAsync(StyleKitEntry entry)
    {
        StyleKitEditorViewModel editor;
        try
        {
            editor = new StyleKitEditorViewModel(_catalog, entry, _logger) { PickExampleFiles = _dialogs.PickExampleFilesAsync };
        }
        catch (InvalidOperationException ex)
        {
            Say(ex.Message);
            return;
        }

        await _dialogs.ShowStyleKitEditorAsync(editor);
        if (editor.Saved && editor.SavedEntry is { } saved)
        {
            Rebuild();
            SelectFolder(saved.FolderPath, saved.Location);
            Say($"Saved the kit '{saved.Kit.Name}'.");
        }
    }

    /// <summary>
    ///     Chooses a kit that was just made and opens it in the editor.
    /// </summary>
    /// <param name="entry">The kit.</param>
    /// <returns>A task that completes when the editor has closed.</returns>
    private async Task ShowAndEditAsync(StyleKitEntry entry)
    {
        Rebuild();
        SelectFolder(entry.FolderPath, entry.Location);
        await EditEntryAsync(entry);
    }

    /// <summary>
    ///     Does a change to the kits and says what went wrong when it cannot be done.
    /// </summary>
    /// <param name="change">The change, which returns the entry that it made.</param>
    /// <param name="entry">Receives the entry when the change was made.</param>
    /// <returns><see langword="true" /> when the change was made.</returns>
    private bool TryChange(Func<StyleKitEntry> change, out StyleKitEntry entry)
    {
        try
        {
            entry = change();
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log(LogLevel.Warning, $"A style kit could not be made: {ex.Message}");
            Say(ex.Message);
            entry = null!;
            return false;
        }
    }

    /// <summary>
    ///     Asks what to do with a kit that has the id of a kit that is there already.
    /// </summary>
    /// <param name="message">The reason that the archive gave.</param>
    /// <returns>What to do, or <see langword="null" /> when the user cancelled.</returns>
    private async Task<StyleImportConflict?> AskConflictAsync(string message)
    {
        if (await _dialogs.ConfirmAsync("Import Style Kit", $"{message}{Environment.NewLine}Replace the kit that is there?", "Replace"))
        {
            return StyleImportConflict.Replace;
        }

        return await _dialogs.ConfirmAsync("Import Style Kit", "Keep both, and give the imported kit a new id?", "Keep both")
            ? StyleImportConflict.KeepBoth
            : null;
    }

    /// <summary>
    ///     Chooses the kit that is in a folder, when it is in the list.
    /// </summary>
    /// <param name="folder">The full path of the folder of the kit, or <see langword="null" />.</param>
    /// <param name="location">The place that the kit is in, or <see langword="null" /> for any.</param>
    private void SelectFolder(string? folder, StyleKitLocation? location = null)
    {
        if (folder is null)
        {
            return;
        }

        var wanted = Path.GetFullPath(folder);
        SelectedItem = _groups
            .SelectMany(group => group.Items)
            .FirstOrDefault(item => (location is null || item.Entry.Location == location)
                                    && string.Equals(Path.GetFullPath(item.Entry.FolderPath), wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Says what the last command did, or why it could not.
    /// </summary>
    /// <param name="message">The message.</param>
    private void Say(string message)
    {
        StatusMessage = message;
    }

    /// <summary>
    ///     Gets the title of a group.
    /// </summary>
    /// <param name="location">The place.</param>
    /// <returns>The title.</returns>
    private static string GroupTitle(StyleKitLocation location) => location switch
    {
        StyleKitLocation.Project => "This project",
        StyleKitLocation.User => "My kits",
        StyleKitLocation.Shared => "Shared",
        _ => "Built-in"
    };

    /// <summary>
    ///     Gets what a group says when it has no kit.
    /// </summary>
    /// <param name="location">The place.</param>
    /// <param name="folders">How many folders the place has.</param>
    /// <returns>The sentence.</returns>
    private static string EmptyText(StyleKitLocation location, int folders) => location switch
    {
        StyleKitLocation.Project => "This project has no kits of its own. Copy a kit into the project to keep it with the work.",
        StyleKitLocation.User => "You have no kits of your own yet. Use New or Duplicate.",
        StyleKitLocation.Shared => folders == 0
            ? "No shared folders are set. Add them in Settings, on the Style Kits page."
            : "There are no kits in the shared folders.",
        _ => "No kits come with the program."
    };

    /// <summary>
    ///     Makes the notes of a group: the folders that cannot be reached and the kits that cannot be used.
    /// </summary>
    /// <param name="places">What the catalog found out about the places of the group.</param>
    /// <returns>The notes.</returns>
    private static List<string> NotesOf(IReadOnlyList<StyleLocationStatus> places)
    {
        var notes = new List<string>();
        foreach (var place in places)
        {
            if (!place.IsAvailable)
            {
                notes.Add(string.IsNullOrWhiteSpace(place.Problem) ? $"Not reachable: {place.Path}" : $"Not reachable: {place.Path} ({place.Problem})");
            }

            foreach (var broken in place.Broken)
            {
                var reason = broken.Issues.FirstOrDefault(issue => issue.IsError)?.ToString() ?? "it has a problem";
                notes.Add($"Cannot be used: {Path.GetFileName(Path.TrimEndingDirectorySeparator(broken.FolderPath))} ({reason})");
            }
        }

        return notes;
    }

    /// <summary>
    ///     Runs an action on the user interface thread: directly when already there (or when the view model was created
    ///     outside a user interface), otherwise posted to it.
    /// </summary>
    /// <param name="action">The action.</param>
    private void RunOnOwnerThread(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_context is null || SynchronizationContext.Current == _context)
        {
            action();
            return;
        }

        _context.Post(
            _ =>
            {
                if (!_disposed)
                {
                    action();
                }
            },
            null);
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks the window.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    private void Log(LogLevel level, string message)
    {
        try
        {
            _logger?.Log(level, message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Logging must never break the window.
        }
    }
}

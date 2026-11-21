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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The list in the status bar of the main window with which the user chooses the style kit of the open project. It
///     offers no style and every kit that is not hidden by a kit of a more local place, shows the kit that the project
///     uses, writes a choice into the project file, and says so when the project names a kit that cannot be found.
/// </summary>
/// <remarks>
///     The view model keeps no state of its own about the choice: the list and the selection are rebuilt from the active
///     style and the catalog of kits whenever either announces a change, on the thread that owns the user interface. The
///     list is shown only while a project is open.
/// </remarks>
public sealed partial class StylePickerViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     The text of the choice that means that the project uses no style.
    /// </summary>
    public const string NoStyleLabel = "(No style)";

    /// <summary>
    ///     The kit that the open project uses, and the means to change it.
    /// </summary>
    private readonly IActiveStyle _active;

    /// <summary>
    ///     The kits that can be chosen.
    /// </summary>
    private readonly IStyleKitCatalog _catalog;

    /// <summary>
    ///     Says whether a project is open.
    /// </summary>
    private readonly IProjectService _projects;

    /// <summary>
    ///     Gives a new project the default kit of the user, or <see langword="null" /> when there is none to give.
    /// </summary>
    private readonly Func<Task>? _applyDefault;

    /// <summary>
    ///     Optional logger.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The synchronization context of the thread that created the view model, which owns the user interface, or null.
    /// </summary>
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>
    ///     The choices shown in the list.
    /// </summary>
    private readonly ObservableCollection<StylePickerItem> _items = [];

    /// <summary>
    ///     Keeps two rebuilds apart: outside a user interface the announcements of the catalog arrive on the thread that
    ///     read the kits, and two reads can finish at the same time.
    /// </summary>
    private readonly Lock _rebuildLock = new();

    /// <summary>
    ///     Whether the list and the selection are being rebuilt, so that the change of selection that this causes is not
    ///     taken for a choice of the user.
    /// </summary>
    private bool _refreshing;

    /// <summary>
    ///     Whether the view model has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     The latest choice that was written, which tests wait for.
    /// </summary>
    private Task _change = Task.CompletedTask;

    /// <summary>
    ///     The choice that is selected in the list.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private StylePickerItem? _selectedItem;

    /// <summary>
    ///     Why the last choice could not be kept, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StylePickerViewModel" /> class and shows the choices that are known
    ///     now.
    /// </summary>
    /// <param name="active">The kit that the open project uses, and the means to change it.</param>
    /// <param name="catalog">The kits that can be chosen.</param>
    /// <param name="projects">Says whether a project is open.</param>
    /// <param name="applyDefault">
    ///     Gives a new project the default kit of the user (<see cref="StyleSelection.ApplyDefaultToNewProjectAsync" />), or
    ///     <see langword="null" /> for none.
    /// </param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="active" />, <paramref name="catalog" />, or <paramref name="projects" /> is <see langword="null" />.</exception>
    public StylePickerViewModel(
        IActiveStyle active,
        IStyleKitCatalog catalog,
        IProjectService projects,
        Func<Task>? applyDefault = null,
        RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(projects);

        _active = active;
        _catalog = catalog;
        _projects = projects;
        _applyDefault = applyDefault;
        _logger = logger;
        Items = new ReadOnlyObservableCollection<StylePickerItem>(_items);

        Rebuild();
        _active.Changed += OnSourceChanged;
        _catalog.Changed += OnSourceChanged;
        _projects.CurrentChanged += OnSourceChanged;
    }

    /// <summary>
    ///     Gets the choices: no style first, then every kit that is not hidden by a kit of a more local place, and last the
    ///     kit that the project names when it cannot be found.
    /// </summary>
    public ReadOnlyObservableCollection<StylePickerItem> Items { get; }

    /// <summary>
    ///     Gets a value indicating whether the list is shown, which it is while a project is open.
    /// </summary>
    public bool IsVisible => _projects.Current is not null;

    /// <summary>
    ///     Gets the explanation of the selected choice: the description of the kit and where it is.
    /// </summary>
    public string ToolTipText => SelectedItem?.ToolTip ?? string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the last choice could not be kept.
    /// </summary>
    public bool HasError => ErrorMessage.Length > 0;

    /// <summary>
    ///     Gets the task of the latest choice that was written, or a completed task when there was none. A user interface
    ///     does not wait for it; tests do.
    /// </summary>
    public Task ChangeTask => Volatile.Read(ref _change);

    /// <summary>
    ///     Gives a new project the default kit of the user. The main window calls it after it created a project.
    /// </summary>
    /// <returns>A task that completes when the project file is written, or at once when there is nothing to do.</returns>
    public Task ApplyDefaultToNewProjectAsync()
    {
        return _applyDefault?.Invoke() ?? Task.CompletedTask;
    }

    /// <summary>
    ///     Gets the name of a place of kits as it is shown to the user.
    /// </summary>
    /// <param name="location">The place.</param>
    /// <returns>A short lower-case name: <c>project</c>, <c>my kits</c>, <c>shared</c>, or <c>built-in</c>.</returns>
    public static string PlaceText(StyleKitLocation location)
    {
        return location switch
        {
            StyleKitLocation.Project => "project",
            StyleKitLocation.User => "my kits",
            StyleKitLocation.Shared => "shared",
            _ => "built-in"
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _active.Changed -= OnSourceChanged;
        _catalog.Changed -= OnSourceChanged;
        _projects.CurrentChanged -= OnSourceChanged;
    }

    /// <summary>
    ///     Writes the choice of the user into the project file, unless the change of selection came from a rebuild of the
    ///     list or the choice is the kit that cannot be found, which is already what the project says.
    /// </summary>
    /// <param name="value">The new selection.</param>
    partial void OnSelectedItemChanged(StylePickerItem? value)
    {
        if (_refreshing || value is null || value.IsMissing || _disposed)
        {
            return;
        }

        Volatile.Write(ref _change, ApplyAsync(value));
    }

    /// <summary>
    ///     Makes a kit the style of the open project, and shows why that was not possible.
    /// </summary>
    /// <param name="item">The choice.</param>
    /// <returns>A task that completes when the choice is written or was refused.</returns>
    private async Task ApplyAsync(StylePickerItem item)
    {
        try
        {
            await _active.SetActiveAsync(item.Id).ConfigureAwait(false);
            RunOnOwnerThread(() => ErrorMessage = string.Empty);
        }
        catch (InvalidOperationException ex)
        {
            // No project is open, or the project file cannot be written: say so and show what the project really uses
            Log(LogLevel.Warning, $"The style kit '{item.Id ?? "(none)"}' was not chosen: {ex.Message}");
            RunOnOwnerThread(() =>
            {
                ErrorMessage = ex.Message;
                Rebuild();
            });
        }
    }

    /// <summary>
    ///     Follows a change of the project, of the kits, or of the choice.
    /// </summary>
    /// <param name="sender">The source of the announcement.</param>
    /// <param name="e">The event data.</param>
    private void OnSourceChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(Rebuild);
    }

    /// <summary>
    ///     Brings the list, the selection, and the visibility in line with the kits and the project. The list is replaced
    ///     only when it is not the same, so a control that shows it is not reset without need.
    /// </summary>
    private void Rebuild()
    {
        lock (_rebuildLock)
        {
            RebuildItems();
        }
    }

    /// <summary>
    ///     Does the work of <see cref="Rebuild" /> while no other rebuild runs.
    /// </summary>
    private void RebuildItems()
    {
        var items = new List<StylePickerItem>
        {
            new(null, NoStyleLabel, "The project has no style kit.", false)
        };

        foreach (var entry in _catalog.Entries.Where(entry => !entry.IsOverridden))
        {
            items.Add(ItemFor(entry));
        }

        var activeId = _active.ActiveId;
        if (_active.IsMissing && activeId is not null)
        {
            items.Add(new StylePickerItem(
                activeId,
                $"{activeId} (not found)",
                $"The project uses the style kit '{activeId}', which cannot be found. Choose another kit, or no style.",
                true));
        }

        var selected = activeId is null
            ? items[0]
            : items.FirstOrDefault(item => string.Equals(item.Id, activeId, StringComparison.Ordinal)) ?? items[0];

        _refreshing = true;
        try
        {
            if (!_items.SequenceEqual(items))
            {
                _items.Clear();
                foreach (var item in items)
                {
                    _items.Add(item);
                }
            }

            SelectedItem = _items.First(item => item.Equals(selected));
        }
        finally
        {
            _refreshing = false;
        }

        OnPropertyChanged(nameof(IsVisible));
    }

    /// <summary>
    ///     Makes the choice that stands for a kit.
    /// </summary>
    /// <param name="entry">The kit as it was found.</param>
    /// <returns>The choice.</returns>
    private static StylePickerItem ItemFor(StyleKitEntry entry)
    {
        var lines = new List<string>();
        if (entry.Kit.Description.Length > 0)
        {
            lines.Add(entry.Kit.Description);
        }

        lines.Add($"Kit '{entry.Kit.Id}' from {PlaceText(entry.Location)}: {entry.FolderPath}");
        return new StylePickerItem(
            entry.Kit.Id,
            $"{entry.Kit.Name} ({PlaceText(entry.Location)})",
            string.Join(Environment.NewLine, lines),
            false);
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
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks the caller.
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
            // Logging must never break the choice of a style.
        }
    }
}

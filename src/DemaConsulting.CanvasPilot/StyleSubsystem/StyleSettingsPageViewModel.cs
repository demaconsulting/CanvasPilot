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
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The "Style Kits" page of the settings dialog: the shared folders in which kits of a team are found, in the order in
///     which they win, and the kit that a new project starts with.
/// </summary>
/// <remarks>
///     <para>
///         The page edits a working copy and merges only the <see cref="AppSettings.Styles" /> member into the settings when
///         the user applies the dialog. A folder is added by choosing it with the folder picker or by typing or pasting its
///         full path (a network folder is easier to name than to browse to); the list shows for each folder whether the
///         catalog could read it the last time and why not, and a folder that cannot be reached now is kept, because it may
///         be reachable later. An entry that is not a full path cannot be saved, so it makes the page invalid.
///     </para>
///     <para>
///         While the dialog is open the page follows the catalog, so the status of the folders and the choices of the
///         default kit are current when the kits have been read, which may be after the dialog was shown.
///     </para>
/// </remarks>
public sealed partial class StyleSettingsPageViewModel : ObservableObject, ISettingsPage
{
    /// <summary>
    ///     The text of the choice that means that a new project starts with no style.
    /// </summary>
    public const string NoStyleLabel = "(No style)";

    /// <summary>
    ///     The catalog of kits, which says what could be read and which kits there are.
    /// </summary>
    private readonly IStyleKitCatalog _catalog;

    /// <summary>
    ///     Gives the folder picker when the page needs it, or <see langword="null" /> while there is none.
    /// </summary>
    private readonly Func<IFolderPicker?> _folderPicker;

    /// <summary>
    ///     The synchronization context of the thread that created the page, which owns the user interface, or null.
    /// </summary>
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>
    ///     The shared folders as they are edited, in order.
    /// </summary>
    private readonly List<string> _paths = [];

    /// <summary>
    ///     The rows of the list of folders.
    /// </summary>
    private readonly ObservableCollection<StyleFolderItem> _folders = [];

    /// <summary>
    ///     The choices of the default kit.
    /// </summary>
    private readonly ObservableCollection<StyleKitChoice> _choices = [];

    /// <summary>
    ///     The id of the default kit as it is edited; an empty string for no style.
    /// </summary>
    private string _defaultKitId = string.Empty;

    /// <summary>
    ///     Whether the page listens to the catalog, which it does between loading and closing.
    /// </summary>
    private bool _listening;

    /// <summary>
    ///     Whether the choices are being rebuilt, so that the change of selection that this causes is not taken for a choice.
    /// </summary>
    private bool _rebuilding;

    /// <summary>
    ///     The folder that is selected in the list.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveFolderCommand), nameof(MoveFolderUpCommand), nameof(MoveFolderDownCommand))]
    private StyleFolderItem? _selectedFolder;

    /// <summary>
    ///     The full path that the user is typing to add a folder.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddTypedFolderCommand))]
    private string _newFolderText = string.Empty;

    /// <summary>
    ///     Why the last folder was not added, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolderError))]
    private string _folderError = string.Empty;

    /// <summary>
    ///     The choice of the default kit that is selected.
    /// </summary>
    [ObservableProperty]
    private StyleKitChoice? _selectedDefaultKit;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleSettingsPageViewModel" /> class.
    /// </summary>
    /// <param name="catalog">The catalog of kits, for the status of the folders and the kits that can be the default.</param>
    /// <param name="folderPicker">
    ///     Gives the picker with which the user chooses a folder, or <see langword="null" /> while there is none (the main
    ///     window is not there yet); the function is called when the user asks to browse.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog" /> or <paramref name="folderPicker" /> is <see langword="null" />.</exception>
    public StyleSettingsPageViewModel(IStyleKitCatalog catalog, Func<IFolderPicker?> folderPicker)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(folderPicker);

        _catalog = catalog;
        _folderPicker = folderPicker;
        Folders = new ReadOnlyObservableCollection<StyleFolderItem>(_folders);
        DefaultKitChoices = new ReadOnlyObservableCollection<StyleKitChoice>(_choices);
        RebuildChoices();
    }

    /// <inheritdoc />
    public string Title => "Style Kits";

    /// <summary>
    ///     Gets the shared folders, in the order in which they win, each with what is known about it.
    /// </summary>
    public ReadOnlyObservableCollection<StyleFolderItem> Folders { get; }

    /// <summary>
    ///     Gets the choices of the default kit: no style, then each kit that is not hidden by a kit of a more local place, and
    ///     the kit that the settings name when no kit has it.
    /// </summary>
    public ReadOnlyObservableCollection<StyleKitChoice> DefaultKitChoices { get; }

    /// <summary>
    ///     Gets a value indicating whether the last folder was not added.
    /// </summary>
    public bool HasFolderError => FolderError.Length > 0;

    /// <inheritdoc />
    public bool IsValid => _paths.All(IsFullPath);

    /// <summary>
    ///     Determines whether a text can be a shared folder: it has no character that a path cannot have and is a full path,
    ///     the rule by which the settings keep a folder.
    /// </summary>
    /// <param name="path">The text.</param>
    /// <returns><see langword="true" /> when the text is a full path.</returns>
    public static bool IsFullPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
               && path.IndexOfAny(Path.GetInvalidPathChars()) < 0
               && Path.IsPathFullyQualified(path);
    }

    /// <inheritdoc />
    public void Load(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _paths.Clear();
        _paths.AddRange(settings.Styles.SharedKitFolders);
        _defaultKitId = settings.Styles.DefaultKitId ?? string.Empty;
        NewFolderText = string.Empty;
        FolderError = string.Empty;
        if (!_listening)
        {
            _listening = true;
            _catalog.Changed += OnCatalogChanged;
        }

        RebuildFolders(null);
        RebuildChoices();
        OnPropertyChanged(nameof(IsValid));
    }

    /// <inheritdoc />
    public AppSettings Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValid)
        {
            throw new InvalidOperationException("A shared folder in the list is not a full path.");
        }

        return settings with
        {
            Styles = settings.Styles with
            {
                SharedKitFolders = _paths.ToArray(),
                DefaultKitId = _defaultKitId
            }
        };
    }

    /// <inheritdoc />
    public void OnClosed()
    {
        if (_listening)
        {
            _listening = false;
            _catalog.Changed -= OnCatalogChanged;
        }
    }

    /// <summary>
    ///     Remembers the default kit that the user chose.
    /// </summary>
    /// <param name="value">The choice.</param>
    partial void OnSelectedDefaultKitChanged(StyleKitChoice? value)
    {
        if (!_rebuilding && value is not null)
        {
            _defaultKitId = value.Id;
        }
    }

    /// <summary>
    ///     Adds the folder that the user typed or pasted.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddTypedFolder))]
    private void AddTypedFolder()
    {
        if (TryAdd(NewFolderText))
        {
            NewFolderText = string.Empty;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether there is a typed path to add.
    /// </summary>
    /// <returns><see langword="true" /> when the text is not blank.</returns>
    private bool CanAddTypedFolder() => !string.IsNullOrWhiteSpace(NewFolderText);

    /// <summary>
    ///     Lets the user choose a folder and adds it.
    /// </summary>
    /// <returns>A task that completes when the user has chosen or cancelled.</returns>
    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var picker = _folderPicker();
        if (picker is null)
        {
            return;
        }

        var initial = SelectedFolder is { IsValid: true } selected && Directory.Exists(selected.Path) ? selected.Path : null;
        var chosen = await picker.PickFolderAsync(initial);
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            TryAdd(chosen);
        }
    }

    /// <summary>
    ///     Takes the selected folder out of the list.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedFolder))]
    private void RemoveFolder()
    {
        var index = SelectedIndex();
        if (index < 0)
        {
            return;
        }

        _paths.RemoveAt(index);
        FolderError = string.Empty;
        RebuildFolders(_paths.Count == 0 ? null : _paths[Math.Min(index, _paths.Count - 1)]);
        OnPropertyChanged(nameof(IsValid));
    }

    /// <summary>
    ///     Moves the selected folder up, so that it wins over the one it passes.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveFolderUp()
    {
        Move(-1);
    }

    /// <summary>
    ///     Moves the selected folder down, so that the one it passes wins over it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveFolderDown()
    {
        Move(1);
    }

    /// <summary>
    ///     Gets a value indicating whether a folder is selected.
    /// </summary>
    /// <returns><see langword="true" /> when a folder of the list is selected.</returns>
    private bool HasSelectedFolder() => SelectedIndex() >= 0;

    /// <summary>
    ///     Gets a value indicating whether the selected folder is not the first.
    /// </summary>
    /// <returns><see langword="true" /> when the selected folder can move up.</returns>
    private bool CanMoveUp() => SelectedIndex() > 0;

    /// <summary>
    ///     Gets a value indicating whether the selected folder is not the last.
    /// </summary>
    /// <returns><see langword="true" /> when the selected folder can move down.</returns>
    private bool CanMoveDown()
    {
        var index = SelectedIndex();
        return index >= 0 && index < _paths.Count - 1;
    }

    /// <summary>
    ///     Moves the selected folder by one place.
    /// </summary>
    /// <param name="step">-1 for up, 1 for down.</param>
    private void Move(int step)
    {
        var index = SelectedIndex();
        var target = index + step;
        if (index < 0 || target < 0 || target >= _paths.Count)
        {
            return;
        }

        (_paths[index], _paths[target]) = (_paths[target], _paths[index]);
        RebuildFolders(_paths[target]);
    }

    /// <summary>
    ///     Adds a folder to the end of the list when it is a full path that the list does not have yet, and says why when it
    ///     is not.
    /// </summary>
    /// <param name="text">The path as typed or chosen.</param>
    /// <returns><see langword="true" /> when the folder was added.</returns>
    private bool TryAdd(string text)
    {
        var path = text.Trim();
        if (!IsFullPath(path))
        {
            FolderError = $"'{path}' is not a full path, such as C:\\Kits or \\\\server\\share\\kits.";
            return false;
        }

        if (_paths.Any(existing => SamePath(existing, path)))
        {
            FolderError = $"'{path}' is in the list already.";
            return false;
        }

        FolderError = string.Empty;
        _paths.Add(path);
        RebuildFolders(path);
        OnPropertyChanged(nameof(IsValid));
        return true;
    }

    /// <summary>
    ///     Finds the place of the selected folder in the list of paths.
    /// </summary>
    /// <returns>The index, or -1 when no folder is selected.</returns>
    private int SelectedIndex()
    {
        return SelectedFolder is null ? -1 : _paths.FindIndex(path => string.Equals(path, SelectedFolder.Path, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Makes the rows of the list from the paths and what the catalog knows about them.
    /// </summary>
    /// <param name="select">The path to select, or <see langword="null" /> to keep the selection when it can be kept.</param>
    private void RebuildFolders(string? select)
    {
        var keep = select ?? SelectedFolder?.Path;
        var rows = _paths.Select(path => new StyleFolderItem(path, StatusFor(path), IsFullPath(path))).ToList();
        if (!_folders.SequenceEqual(rows))
        {
            _folders.Clear();
            foreach (var row in rows)
            {
                _folders.Add(row);
            }
        }

        SelectedFolder = keep is null ? null : _folders.FirstOrDefault(row => string.Equals(row.Path, keep, StringComparison.Ordinal));
        RemoveFolderCommand.NotifyCanExecuteChanged();
        MoveFolderUpCommand.NotifyCanExecuteChanged();
        MoveFolderDownCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Makes the choices of the default kit from the kits that the catalog has, and selects the one that is the default.
    /// </summary>
    private void RebuildChoices()
    {
        var choices = new List<StyleKitChoice> { new(string.Empty, NoStyleLabel, false) };
        foreach (var entry in _catalog.Entries.Where(entry => !entry.IsOverridden))
        {
            choices.Add(new StyleKitChoice(entry.Kit.Id, $"{entry.Kit.Name} ({StylePickerViewModel.PlaceText(entry.Location)})", false));
        }

        if (_defaultKitId.Length > 0 && choices.All(choice => choice.Id != _defaultKitId))
        {
            choices.Add(new StyleKitChoice(_defaultKitId, $"{_defaultKitId} (not found)", true));
        }

        _rebuilding = true;
        try
        {
            if (!_choices.SequenceEqual(choices))
            {
                _choices.Clear();
                foreach (var choice in choices)
                {
                    _choices.Add(choice);
                }
            }

            SelectedDefaultKit = _choices.First(choice => choice.Id == _defaultKitId);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    /// <summary>
    ///     Says what is known about a folder: whether the catalog could read it the last time, and why not.
    /// </summary>
    /// <param name="path">The folder.</param>
    /// <returns>The text for the row.</returns>
    private string StatusFor(string path)
    {
        if (!IsFullPath(path))
        {
            return "Not a full path, so it cannot be saved. Remove it.";
        }

        var status = _catalog.Locations.FirstOrDefault(location => location.Location == StyleKitLocation.Shared && SamePath(location.Path, path));
        if (status is null)
        {
            return "Not read yet. It is read after you choose OK or Apply.";
        }

        if (!status.IsAvailable)
        {
            var reason = string.IsNullOrWhiteSpace(status.Problem) ? string.Empty : $": {status.Problem}";
            return $"Not reachable now{reason}. It stays in the list and is tried again.";
        }

        return status.Broken.Count == 0
            ? "Reachable."
            : $"Reachable, but {status.Broken.Count} kit folder(s) in it cannot be used.";
    }

    /// <summary>
    ///     Tells whether two paths name the same folder: they are the same text after the full path is taken and a closing
    ///     separator is dropped, without regard to case on Windows and macOS, whose usual file systems ignore case.
    /// </summary>
    /// <param name="first">The first path.</param>
    /// <param name="second">The second path.</param>
    /// <returns><see langword="true" /> when they name the same folder.</returns>
    private static bool SamePath(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Normalize(first), Normalize(second), comparison);
    }

    /// <summary>
    ///     Makes a path comparable: the full path without a closing separator, or the text itself when it cannot be taken.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The path to compare.</returns>
    private static string Normalize(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim();
        }
    }

    /// <summary>
    ///     Follows the catalog when the kits or the places were read again.
    /// </summary>
    /// <param name="sender">The catalog.</param>
    /// <param name="e">The event data.</param>
    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(() =>
        {
            if (_listening)
            {
                RebuildFolders(null);
                RebuildChoices();
            }
        });
    }

    /// <summary>
    ///     Runs an action on the user interface thread: directly when already there (or when the page was created outside a
    ///     user interface), otherwise posted to it.
    /// </summary>
    /// <param name="action">The action.</param>
    private void RunOnOwnerThread(Action action)
    {
        if (_context is null || SynchronizationContext.Current == _context)
        {
            action();
            return;
        }

        _context.Post(_ => action(), null);
    }
}

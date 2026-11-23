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
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     An entry of the Recent Projects menu.
/// </summary>
/// <param name="name">The project name shown in the menu.</param>
/// <param name="filePath">The full path of the project file.</param>
/// <param name="openCommand">The command that opens the project.</param>
public sealed class RecentProjectItem(string name, string filePath, IAsyncRelayCommand openCommand)
{
    /// <summary>
    ///     Gets the project name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    ///     Gets the menu header: the project name with underscores doubled so the menu shows them as typed
    ///     instead of treating them as access keys.
    /// </summary>
    public string Header { get; } = name.Replace("_", "__", StringComparison.Ordinal);

    /// <summary>
    ///     Gets the full path of the project file.
    /// </summary>
    public string FilePath { get; } = filePath;

    /// <summary>
    ///     Gets the command that opens the project.
    /// </summary>
    public IAsyncRelayCommand OpenCommand { get; } = openCommand;
}

/// <summary>
///     View model of the application's main window: the File, Edit, Image, Tools, and Help menus, the open project, the
///     recent projects list, the window title, and the workspace. The Help menu shows the About dialog and the browser of
///     the drawing knowledge. The Edit menu undoes and redoes in the part of the window that has the focus
///     (<see cref="EditTarget" />), and the talk key starts and stops dictation in
///     <see cref="PushToTalk" />. It talks to the user only through an
///     <see cref="IDialogService" />, which the main window supplies, so it can be tested without a window.
/// </summary>
public sealed partial class MainWindowShell : ObservableObject, IDisposable
{
    /// <summary>
    ///     The application name shown in the window title.
    /// </summary>
    public const string ApplicationName = "CanvasPilot";

    /// <summary>
    ///     The key that is held to talk: while it is down the message is dictated.
    /// </summary>
    public const Key PushToTalkKey = Key.F8;

    /// <summary>
    ///     Creates and opens projects.
    /// </summary>
    private readonly IProjectService _projectService;

    /// <summary>
    ///     The list of recently opened project files.
    /// </summary>
    private readonly IRecentProjects _recentProjects;

    /// <summary>
    ///     Source and destination of the settings edited in the settings dialog.
    /// </summary>
    private readonly ISettingsService _settingsService;

    /// <summary>
    ///     The pages shown in the settings dialog.
    /// </summary>
    private readonly IReadOnlyList<ISettingsPage> _settingsPages;

    /// <summary>
    ///     The folder offered for new projects when no recent project suggests a better one, or null.
    /// </summary>
    private readonly string? _defaultProjectsFolder;

    /// <summary>
    ///     Gets the catalog of the drawing knowledge when it is first asked for, or null when the application has none.
    /// </summary>
    private readonly Func<IKnowledgeCatalog>? _knowledge;

    /// <summary>
    ///     The list with which the user chooses the style kit of the open project, or null when the application has none.
    /// </summary>
    private readonly StylePickerViewModel? _style;

    /// <summary>
    ///     Checks the shown picture against the style kit of the project, or null when the application cannot.
    /// </summary>
    private readonly ICheckAgainstStyle? _styleCheck;

    /// <summary>
    ///     Makes the view model of the window that manages style kits, or null when the application has none.
    /// </summary>
    private readonly Func<IStyleKitDialogs, StyleKitManagerViewModel>? _styleKits;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The synchronization context of the thread that created the shell, which owns the user interface, or null.
    /// </summary>
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>
    ///     The recent projects shown in the menu.
    /// </summary>
    private readonly ObservableCollection<RecentProjectItem> _recentItems = [];

    /// <summary>
    ///     The dialog service, or null until attached.
    /// </summary>
    private IDialogService? _dialogs;

    /// <summary>
    ///     Whether the shell has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     The window title.
    /// </summary>
    [ObservableProperty]
    private string _title = ApplicationName;

    /// <summary>
    ///     The open project, or null.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject), nameof(TitleBarName), nameof(TitleBarSuffix))]
    [NotifyCanExecuteChangedFor(nameof(SaveProjectCommand), nameof(CloseProjectCommand))]
    private ProjectInfo? _currentProject;

    /// <summary>
    ///     What the Edit menu undoes and redoes: the part of the window that has the focus, or null when it has nothing
    ///     to undo.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand), nameof(RedoCommand))]
    private IUndoRedoTarget? _editTarget;

    /// <summary>
    ///     The target that was told to start listening and is to be told to stop, or null while no key is held.
    /// </summary>
    private IPushToTalkTarget? _talkingTarget;

    /// <summary>
    ///     Creates the shell.
    /// </summary>
    /// <param name="projectService">Creates, opens, saves, and closes projects.</param>
    /// <param name="recentProjects">The recently opened project files.</param>
    /// <param name="settingsService">The settings edited in the settings dialog.</param>
    /// <param name="workspace">The content area. The shell takes ownership and disposes it.</param>
    /// <param name="settingsPages">The pages of the settings dialog, in display order.</param>
    /// <param name="imageExporter">
    ///     What the Image menu exports and copies, or <see langword="null" /> for none, which leaves the Image
    ///     menu items disabled.
    /// </param>
    /// <param name="defaultProjectsFolder">The folder offered for new projects, or <see langword="null" />.</param>
    /// <param name="knowledge">
    ///     Gets the catalog of the drawing knowledge for the Help menu, called when the browser is first asked for so that
    ///     the cards are not read at start, or <see langword="null" /> for none, which leaves the menu item disabled.
    /// </param>
    /// <param name="style">
    ///     The list with which the user chooses the style kit of the open project, shown in the title bar, or
    ///     <see langword="null" /> for none, which leaves the list out. The shell uses it to give a new project the
    ///     default style kit of the user but does not dispose it.
    /// </param>
    /// <param name="checkAgainstStyle">
    ///     What the Check Against Style item of the Image menu runs, or <see langword="null" /> for none, which leaves the item
    ///     disabled. The shell does not dispose it.
    /// </param>
    /// <param name="styleKits">
    ///     Makes the view model of the window that manages style kits from the dialogs that it asks through, called each time the
    ///     Style Kits item of the Tools menu is chosen, or <see langword="null" /> for none, which leaves the item unusable. The
    ///     window disposes the view model it is given.
    /// </param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public MainWindowShell(
        IProjectService projectService,
        IRecentProjects recentProjects,
        ISettingsService settingsService,
        ProjectWorkspaceViewModel workspace,
        IReadOnlyList<ISettingsPage> settingsPages,
        IImageExporter? imageExporter = null,
        string? defaultProjectsFolder = null,
        Func<IKnowledgeCatalog>? knowledge = null,
        StylePickerViewModel? style = null,
        ICheckAgainstStyle? checkAgainstStyle = null,
        Func<IStyleKitDialogs, StyleKitManagerViewModel>? styleKits = null,
        RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(recentProjects);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(settingsPages);

        _projectService = projectService;
        _recentProjects = recentProjects;
        _settingsService = settingsService;
        _settingsPages = settingsPages;
        _defaultProjectsFolder = defaultProjectsFolder;
        _knowledge = knowledge;
        _style = style;
        _styleCheck = checkAgainstStyle;
        _styleKits = styleKits;
        _logger = logger;
        Workspace = workspace;
        StatusBar = new StatusBarViewModel(workspace);
        ImageExporter = imageExporter ?? new NullImageExporter();
        RecentProjects = new ReadOnlyObservableCollection<RecentProjectItem>(_recentItems);

        _projectService.CurrentChanged += OnProjectChanged;
        _recentProjects.Changed += OnRecentProjectsChanged;
        ImageExporter.CanExportChanged += OnCanExportChanged;
        if (_styleCheck is not null)
        {
            _styleCheck.CanCheckChanged += OnCanCheckChanged;
        }

        RefreshRecentProjects();
        SyncProjectState();
    }

    /// <summary>
    ///     Raised when the user chooses Exit. The main window closes itself in response.
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    ///     Gets the content area.
    /// </summary>
    public ProjectWorkspaceViewModel Workspace { get; }

    /// <summary>
    ///     Gets the status bar at the bottom of the window, which is always there and reports on the shown picture.
    /// </summary>
    public StatusBarViewModel StatusBar { get; }

    /// <summary>
    ///     Gets what the Image menu exports and copies.
    /// </summary>
    public IImageExporter ImageExporter { get; }

    /// <summary>
    ///     Gets the list in the title bar with which the user chooses the style kit of the open project, or
    ///     <see langword="null" /> when the application has none.
    /// </summary>
    public StylePickerViewModel? StylePicker => _style;

    /// <summary>
    ///     Gets the picker of folders that the main window supplies, or <see langword="null" /> until the window has attached
    ///     itself. A settings page that lets the user choose a folder asks for it when the user asks to browse.
    /// </summary>
    public IFolderPicker? FolderPicker => _dialogs;

    /// <summary>
    ///     Gets the recent projects shown in the Recent Projects menu, most recent first.
    /// </summary>
    public ReadOnlyObservableCollection<RecentProjectItem> RecentProjects { get; }

    /// <summary>
    ///     Gets a value indicating whether a project is open.
    /// </summary>
    public bool HasProject => CurrentProject is not null;

    /// <summary>
    ///     Gets the text the title bar shows in bold: the name of the open project, or the application name when no
    ///     project is open. The window title (<see cref="Title" />) is unchanged and stays what the system shows in the task
    ///     bar and window lists.
    /// </summary>
    public string TitleBarName => CurrentProject?.Name ?? ApplicationName;

    /// <summary>
    ///     Gets the quiet text after <see cref="TitleBarName" /> in the title bar: a middle dot and the application name
    ///     while a project is open, and nothing otherwise.
    /// </summary>
    public string TitleBarSuffix => CurrentProject is null ? string.Empty : " \u00B7 " + ApplicationName;

    /// <summary>
    ///     Gets a value indicating whether the Recent Projects menu has entries.
    /// </summary>
    public bool HasRecentProjects => _recentItems.Count > 0;

    /// <summary>
    ///     Gets or sets what listens while <see cref="PushToTalkKey" /> is held, normally the chat panel, or null when
    ///     nothing does, in which case the key does nothing.
    /// </summary>
    public IPushToTalkTarget? PushToTalk { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the talk key is held and listening was started for it.
    /// </summary>
    public bool IsPushToTalkActive => _talkingTarget is not null;

    /// <summary>
    ///     Starts listening because the talk key went down. A key that is already down, which is the keyboard repeating it,
    ///     and a key with no target are ignored.
    /// </summary>
    /// <returns>A task that completes when listening has started, or when it was found that it cannot.</returns>
    public Task BeginPushToTalkAsync()
    {
        var target = PushToTalk;
        if (target is null || _talkingTarget is not null)
        {
            return Task.CompletedTask;
        }

        _talkingTarget = target;
        return target.BeginPushToTalkAsync();
    }

    /// <summary>
    ///     Stops listening because the talk key went up or the window lost the focus. The target that was told to start is
    ///     told to stop, even when <see cref="PushToTalk" /> was changed in between; a release with no key down is ignored.
    /// </summary>
    /// <returns>A task that completes when what was said has been added to the message.</returns>
    public Task EndPushToTalkAsync()
    {
        var target = _talkingTarget;
        if (target is null)
        {
            return Task.CompletedTask;
        }

        _talkingTarget = null;
        return target.EndPushToTalkAsync();
    }

    /// <summary>
    ///     Gives the shell the dialog service it uses to talk to the user, and passes it on to the workspace.
    ///     Until this is called, commands that need the user do nothing. The main window calls it with itself.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    public void AttachDialogService(IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        _dialogs = dialogs;
        Workspace.AttachDialogService(dialogs);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _projectService.CurrentChanged -= OnProjectChanged;
        _recentProjects.Changed -= OnRecentProjectsChanged;
        ImageExporter.CanExportChanged -= OnCanExportChanged;
        if (_styleCheck is not null)
        {
            _styleCheck.CanCheckChanged -= OnCanCheckChanged;
        }

        if (EditTarget is not null)
        {
            EditTarget.UndoRedoStateChanged -= OnUndoRedoStateChanged;
        }

        StatusBar.Dispose();
        Workspace.Dispose();
    }

    /// <summary>
    ///     Undoes the latest step of the <see cref="EditTarget" />.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndoEdit))]
    private void Undo()
    {
        EditTarget?.Undo();
    }

    /// <summary>
    ///     Redoes the step of the <see cref="EditTarget" /> that was undone last.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedoEdit))]
    private void Redo()
    {
        EditTarget?.Redo();
    }

    /// <summary>
    ///     Gets a value indicating whether the Undo command can run.
    /// </summary>
    /// <returns><see langword="true" /> when the edit target has a step to undo.</returns>
    private bool CanUndoEdit() => EditTarget?.CanUndo == true;

    /// <summary>
    ///     Gets a value indicating whether the Redo command can run.
    /// </summary>
    /// <returns><see langword="true" /> when the edit target has a step to redo.</returns>
    private bool CanRedoEdit() => EditTarget?.CanRedo == true;

    /// <summary>
    ///     Follows the state of the edit target, so the commands are re-evaluated when its steps change.
    /// </summary>
    /// <param name="oldValue">The previous target.</param>
    /// <param name="newValue">The new target.</param>
    partial void OnEditTargetChanged(IUndoRedoTarget? oldValue, IUndoRedoTarget? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.UndoRedoStateChanged -= OnUndoRedoStateChanged;
        }

        if (newValue is not null)
        {
            newValue.UndoRedoStateChanged += OnUndoRedoStateChanged;
        }
    }

    /// <summary>
    ///     Re-evaluates the Undo and Redo commands when the steps of the edit target changed.
    /// </summary>
    /// <param name="sender">The edit target.</param>
    /// <param name="e">The event data.</param>
    private void OnUndoRedoStateChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(() =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>
    ///     Opens the New Project dialog and creates the project when the user accepts it.
    /// </summary>
    /// <returns>A task that completes when the command has finished.</returns>
    [RelayCommand]
    private async Task NewProjectAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        var dialog = new NewProjectDialogViewModel(_projectService, _dialogs, GetStartFolder());
        await _dialogs.ShowNewProjectAsync(dialog);
        if (!dialog.Accepted)
        {
            return;
        }

        var result = await _projectService.CreateAsync(dialog.ParentFolder, dialog.ProjectName);
        if (result.Succeeded && _style is not null)
        {
            // A new project says which style it uses from the start; a project that is only opened keeps what it says
            await _style.ApplyDefaultToNewProjectAsync();
        }

        await CompleteOpenAsync(result, "New Project");
    }

    /// <summary>
    ///     Asks the user for a project file and opens it.
    /// </summary>
    /// <returns>A task that completes when the command has finished.</returns>
    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        var file = await _dialogs.PickProjectFileAsync(GetStartFolder());
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }

        await OpenProjectFileAsync(file);
    }

    /// <summary>
    ///     Saves the open project's file.
    /// </summary>
    /// <returns>A task that completes when the command has finished.</returns>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task SaveProjectAsync()
    {
        var result = await _projectService.SaveAsync();
        if (result.Succeeded)
        {
            SyncProjectState();
        }
        else if (_dialogs is not null)
        {
            await _dialogs.ShowMessageAsync("Save Project", result.Error);
        }
    }

    /// <summary>
    ///     Closes the open project.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void CloseProject()
    {
        _projectService.Close();
        SyncProjectState();
    }

    /// <summary>
    ///     Asks the main window to close, which ends the application.
    /// </summary>
    [RelayCommand]
    private void Exit()
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Exports the rendered image as a PNG file.
    /// </summary>
    /// <returns>A task that completes when the command has finished.</returns>
    [RelayCommand(CanExecute = nameof(CanExportImage))]
    private async Task ExportPngAsync()
    {
        if (_dialogs is not null && ImageExporter.CanExport)
        {
            await ImageExporter.ExportPngAsync(_dialogs);
        }
    }

    /// <summary>
    ///     Copies the rendered image to the clipboard.
    /// </summary>
    /// <returns>A task that completes when the command has finished.</returns>
    [RelayCommand(CanExecute = nameof(CanExportImage))]
    private async Task CopyImageAsync()
    {
        if (_dialogs is not null && ImageExporter.CanExport)
        {
            await ImageExporter.CopyImageAsync(_dialogs);
        }
    }

    /// <summary>
    ///     Checks the shown picture against the style kit of the project and shows the result among the notes of the picture;
    ///     when nothing could be checked, tells the user why.
    /// </summary>
    /// <returns>A task that completes when the check has finished and the user was told, if there was need.</returns>
    [RelayCommand(CanExecute = nameof(CanCheckAgainstStyle))]
    private async Task CheckAgainstStyleAsync()
    {
        if (_styleCheck is null)
        {
            return;
        }

        var message = await _styleCheck.RunAsync();
        if (message is not null && _dialogs is not null)
        {
            await _dialogs.ShowMessageAsync("Check Against Style", message);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the Check Against Style command can run.
    /// </summary>
    /// <returns><see langword="true" /> when the application can check and a picture is shown.</returns>
    private bool CanCheckAgainstStyle() => _styleCheck?.CanCheck == true;

    /// <summary>
    ///     Opens the window that manages style kits, which does not block the application. There is one such window at a time:
    ///     when it is open, it is brought to the front.
    /// </summary>
    /// <returns>A task that completes when the window is shown, not when it is closed.</returns>
    [RelayCommand(CanExecute = nameof(HasStyleKits))]
    private async Task ShowStyleKitsAsync()
    {
        if (_dialogs is null || _styleKits is null)
        {
            return;
        }

        await _dialogs.ShowStyleKitManagerAsync(_styleKits(_dialogs));
    }

    /// <summary>
    ///     Gets a value indicating whether the application has a window that manages style kits to show.
    /// </summary>
    private bool HasStyleKits => _styleKits is not null;

    /// <summary>
    ///     Opens the settings dialog.
    /// </summary>
    /// <returns>A task that completes when the dialog has closed.</returns>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        var dialog = new SettingsDialogViewModel(_settingsService, _settingsPages);
        try
        {
            await _dialogs.ShowSettingsAsync(dialog);
        }
        finally
        {
            dialog.NotifyClosed();
        }
    }

    /// <summary>
    ///     Opens the About dialog.
    /// </summary>
    /// <returns>A task that completes when the dialog has closed.</returns>
    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        if (_dialogs is not null)
        {
            await _dialogs.ShowAboutAsync(new AboutDialogViewModel());
        }
    }

    /// <summary>
    ///     Opens the browser of the drawing knowledge in a window that does not block the application. The cards are read
    ///     the first time this runs; when they cannot be read, the problem is logged and shown to the user.
    /// </summary>
    /// <returns>A task that completes when the browser is shown, or the problem was shown.</returns>
    [RelayCommand(CanExecute = nameof(HasKnowledge))]
    private async Task ShowDrawingKnowledgeAsync()
    {
        if (_dialogs is null || _knowledge is null)
        {
            return;
        }

        KnowledgeBrowserViewModel browser;
        try
        {
            var catalog = _knowledge();
            browser = new KnowledgeBrowserViewModel(catalog, new KnowledgeSearch(catalog), _logger);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A card of the application that cannot be read is a defect of the application, not of the user's work
            _logger?.Log(LogLevel.Error, "The drawing knowledge could not be loaded.", ex);
            await _dialogs.ShowMessageAsync("Drawing Knowledge", $"The drawing knowledge could not be loaded: {ex.Message}");
            return;
        }

        await _dialogs.ShowKnowledgeBrowserAsync(browser);
    }

    /// <summary>
    ///     Gets a value indicating whether the application has drawing knowledge to show.
    /// </summary>
    private bool HasKnowledge => _knowledge is not null;

    /// <summary>
    ///     Gets a value indicating whether the Image menu commands can run.
    /// </summary>
    /// <returns><see langword="true" /> when there is a rendered image.</returns>
    private bool CanExportImage() => ImageExporter.CanExport;

    /// <summary>
    ///     Opens a project file, remembers it, and reports a problem to the user.
    /// </summary>
    /// <param name="projectFile">The project file.</param>
    /// <returns>A task that completes when the project has been opened or the problem shown.</returns>
    private async Task OpenProjectFileAsync(string projectFile)
    {
        var result = await _projectService.OpenAsync(projectFile);
        if (!result.Succeeded && !File.Exists(projectFile))
        {
            _recentProjects.Remove(projectFile);
        }

        await CompleteOpenAsync(result, "Open Project");
    }

    /// <summary>
    ///     Finishes a create or open: remembers the project and shows it, or shows the problem.
    /// </summary>
    /// <param name="result">The outcome of the operation.</param>
    /// <param name="title">The title of the dialog that reports a problem.</param>
    /// <returns>A task that completes when the problem, if any, was shown.</returns>
    private async Task CompleteOpenAsync(ProjectResult result, string title)
    {
        if (result.Succeeded)
        {
            _recentProjects.Add(result.Project!.FilePath);
            SyncProjectState();
        }
        else if (_dialogs is not null)
        {
            await _dialogs.ShowMessageAsync(title, result.Error);
        }
    }

    /// <summary>
    ///     Finds the folder that file and folder pickers start in: the folder that holds the most recent project's
    ///     folder, otherwise the default projects folder.
    /// </summary>
    /// <returns>An existing folder, or <see langword="null" />.</returns>
    private string? GetStartFolder()
    {
        var recent = _recentProjects.GetRecent();
        if (recent.Count > 0)
        {
            var projectFolder = Path.GetDirectoryName(recent[0]);
            var parent = projectFolder is null ? null : Path.GetDirectoryName(projectFolder);
            if (parent is not null && Directory.Exists(parent))
            {
                return parent;
            }
        }

        return _defaultProjectsFolder is not null && Directory.Exists(_defaultProjectsFolder) ? _defaultProjectsFolder : null;
    }

    /// <summary>
    ///     Brings the title, the open project, and the workspace in line with the project service.
    /// </summary>
    private void SyncProjectState()
    {
        var project = _projectService.Current;
        CurrentProject = project;
        Title = project is null ? ApplicationName : $"{project.Name} - {ApplicationName}";
        Workspace.ShowProject(project);
    }

    /// <summary>
    ///     Rebuilds the Recent Projects menu from the recent projects list.
    /// </summary>
    private void RefreshRecentProjects()
    {
        // Read the list first: reading drops missing files and may announce a change, which refreshes the menu.
        var files = _recentProjects.GetRecent();
        _recentItems.Clear();
        foreach (var file in files)
        {
            var path = file;
            var name = Path.GetFileNameWithoutExtension(path);
            _recentItems.Add(new RecentProjectItem(name, path, new AsyncRelayCommand(() => OpenProjectFileAsync(path))));
        }

        OnPropertyChanged(nameof(HasRecentProjects));
    }

    /// <summary>
    ///     Runs an action on the user interface thread: directly when already there (or when the shell was created
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
    ///     Follows the project service when the open project changed.
    /// </summary>
    /// <param name="sender">The project service.</param>
    /// <param name="e">The event data.</param>
    private void OnProjectChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(SyncProjectState);
    }

    /// <summary>
    ///     Follows the recent projects list when it changed.
    /// </summary>
    /// <param name="sender">The recent projects list.</param>
    /// <param name="e">The event data.</param>
    private void OnRecentProjectsChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(RefreshRecentProjects);
    }

    /// <summary>
    ///     Re-evaluates the Image menu commands when the exporter's state changed.
    /// </summary>
    /// <param name="sender">The exporter.</param>
    /// <param name="e">The event data.</param>
    private void OnCanExportChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(() =>
        {
            ExportPngCommand.NotifyCanExecuteChanged();
            CopyImageCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>
    ///     Re-evaluates the Check Against Style command when the picture that can be checked appeared or went away.
    /// </summary>
    /// <param name="sender">The check.</param>
    /// <param name="e">The event data.</param>
    private void OnCanCheckChanged(object? sender, EventArgs e)
    {
        RunOnOwnerThread(() => CheckAgainstStyleCommand.NotifyCanExecuteChanged());
    }
}

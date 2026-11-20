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
///     View model of the application's main window: the File, Image, Tools, and Help menus, the open project, the
///     recent projects list, the window title, and the workspace. It talks to the user only through an
///     <see cref="IDialogService" />, which the main window supplies, so it can be tested without a window.
/// </summary>
public sealed partial class MainWindowShell : ObservableObject, IDisposable
{
    /// <summary>
    ///     The application name shown in the window title.
    /// </summary>
    public const string ApplicationName = "CanvasPilot";

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
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyCanExecuteChangedFor(nameof(SaveProjectCommand), nameof(CloseProjectCommand))]
    private ProjectInfo? _currentProject;

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
    public MainWindowShell(
        IProjectService projectService,
        IRecentProjects recentProjects,
        ISettingsService settingsService,
        ProjectWorkspaceViewModel workspace,
        IReadOnlyList<ISettingsPage> settingsPages,
        IImageExporter? imageExporter = null,
        string? defaultProjectsFolder = null)
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
        Workspace = workspace;
        ImageExporter = imageExporter ?? new NullImageExporter();
        RecentProjects = new ReadOnlyObservableCollection<RecentProjectItem>(_recentItems);

        _projectService.CurrentChanged += OnProjectChanged;
        _recentProjects.Changed += OnRecentProjectsChanged;
        ImageExporter.CanExportChanged += OnCanExportChanged;

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
    ///     Gets what the Image menu exports and copies.
    /// </summary>
    public IImageExporter ImageExporter { get; }

    /// <summary>
    ///     Gets the recent projects shown in the Recent Projects menu, most recent first.
    /// </summary>
    public ReadOnlyObservableCollection<RecentProjectItem> RecentProjects { get; }

    /// <summary>
    ///     Gets a value indicating whether a project is open.
    /// </summary>
    public bool HasProject => CurrentProject is not null;

    /// <summary>
    ///     Gets a value indicating whether the Recent Projects menu has entries.
    /// </summary>
    public bool HasRecentProjects => _recentItems.Count > 0;

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
        Workspace.Dispose();
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
}

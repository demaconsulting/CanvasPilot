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

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     View model of the main window's content area: the project explorer on the left and, on the right, a
///     content area that shows <see cref="Preview" /> when one is supplied and an explanation otherwise. The
///     workspace also asks the user to confirm deleting a file and to enter a new name when renaming one.
/// </summary>
public sealed partial class ProjectWorkspaceViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     The dialog service used to confirm and report file operations, or null until attached.
    /// </summary>
    private IDialogService? _dialogs;

    /// <summary>
    ///     The content of the right-hand area, or null while none is supplied.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview), nameof(PlaceholderText))]
    private object? _preview;

    /// <summary>
    ///     Creates the workspace around a project explorer. The workspace takes ownership of the explorer and
    ///     disposes it.
    /// </summary>
    /// <param name="explorer">The explorer shown on the left.</param>
    public ProjectWorkspaceViewModel(ProjectExplorerViewModel explorer)
    {
        ArgumentNullException.ThrowIfNull(explorer);

        Explorer = explorer;
        Explorer.PropertyChanged += OnExplorerPropertyChanged;
    }

    /// <summary>
    ///     Gets the project explorer shown on the left.
    /// </summary>
    public ProjectExplorerViewModel Explorer { get; }

    /// <summary>
    ///     Gets a value indicating whether a project is shown.
    /// </summary>
    public bool HasProject => Explorer.HasProject;

    /// <summary>
    ///     Gets a value indicating whether a file is selected in the explorer.
    /// </summary>
    public bool HasSelectedFile => Explorer.SelectedItem is not null;

    /// <summary>
    ///     Gets a value indicating whether <see cref="Preview" /> holds content, which then replaces the
    ///     placeholder.
    /// </summary>
    public bool HasPreview => Preview is not null;

    /// <summary>
    ///     Gets the text shown in the right-hand area while there is no preview content.
    /// </summary>
    public string PlaceholderText
    {
        get
        {
            if (!HasProject)
            {
                return "No project open";
            }

            return HasSelectedFile ? "No preview available" : "No file selected";
        }
    }

    /// <summary>
    ///     Gets a hint on how to start, shown below the placeholder while no project is open, or an empty string.
    /// </summary>
    public string HintText => HasProject ? string.Empty : "Create a project with File > New Project, or open one with File > Open Project.";

    /// <summary>
    ///     Gets the file the user selected in the explorer, or <see langword="null" />.
    /// </summary>
    public string? SelectedFile => Explorer.SelectedFile;

    /// <summary>
    ///     Gives the workspace the dialog service it uses to confirm deletions, ask for new names, and report
    ///     problems. Until this is called the delete and rename commands do nothing.
    /// </summary>
    /// <param name="dialogs">The dialog service.</param>
    public void AttachDialogService(IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        _dialogs = dialogs;
    }

    /// <summary>
    ///     Shows a project in the explorer, or empties the explorer when no project is open. Showing the project
    ///     that is already shown changes nothing.
    /// </summary>
    /// <param name="project">The open project, or <see langword="null" />.</param>
    public void ShowProject(ProjectInfo? project)
    {
        if (project is null)
        {
            if (Explorer.HasProject)
            {
                Explorer.Close();
            }

            return;
        }

        var folder = Path.GetFullPath(project.FolderPath);
        if (!string.Equals(Explorer.FolderPath, folder, StringComparison.Ordinal))
        {
            Explorer.Open(folder);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Explorer.PropertyChanged -= OnExplorerPropertyChanged;
        Explorer.Dispose();
    }

    /// <summary>
    ///     Deletes the selected file after the user confirms, and reports a problem if it could not be deleted.
    /// </summary>
    /// <returns>A task that completes when the operation has finished.</returns>
    [RelayCommand(CanExecute = nameof(HasSelectedFile))]
    private async Task DeleteSelectedAsync()
    {
        var item = Explorer.SelectedItem;
        if (_dialogs is null || item is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Delete File",
            $"Delete '{item.Name}'? The file will be removed from the project folder.",
            "Delete");
        if (!confirmed)
        {
            return;
        }

        var problem = Explorer.DeleteSelected();
        if (problem.Length > 0)
        {
            await _dialogs.ShowMessageAsync("Delete File", problem);
        }
    }

    /// <summary>
    ///     Asks the user for a new name for the selected file and renames it, reporting a problem if it could
    ///     not be renamed.
    /// </summary>
    /// <returns>A task that completes when the operation has finished.</returns>
    [RelayCommand(CanExecute = nameof(HasSelectedFile))]
    private async Task RenameSelectedAsync()
    {
        var item = Explorer.SelectedItem;
        if (_dialogs is null || item is null)
        {
            return;
        }

        var newName = await _dialogs.PromptAsync("Rename File", $"New name for '{item.Name}':", item.Name, "Rename");
        if (newName is null)
        {
            return;
        }

        var problem = Explorer.RenameSelected(newName);
        if (problem.Length > 0)
        {
            await _dialogs.ShowMessageAsync("Rename File", problem);
        }
    }

    /// <summary>
    ///     Forwards the explorer's changes that affect the workspace's own state.
    /// </summary>
    /// <param name="sender">The explorer.</param>
    /// <param name="e">The change details.</param>
    private void OnExplorerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProjectExplorerViewModel.SelectedItem):
                OnPropertyChanged(nameof(HasSelectedFile));
                OnPropertyChanged(nameof(SelectedFile));
                OnPropertyChanged(nameof(PlaceholderText));
                DeleteSelectedCommand.NotifyCanExecuteChanged();
                RenameSelectedCommand.NotifyCanExecuteChanged();
                break;

            case nameof(ProjectExplorerViewModel.HasProject):
                OnPropertyChanged(nameof(HasProject));
                OnPropertyChanged(nameof(PlaceholderText));
                OnPropertyChanged(nameof(HintText));
                break;
        }
    }
}

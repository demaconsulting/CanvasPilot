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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     Event data for <see cref="NewProjectDialogViewModel.CloseRequested" />.
/// </summary>
/// <param name="accepted">
///     <see langword="true" /> when the dialog closed through OK; <see langword="false" /> when it was cancelled.
/// </param>
public sealed class NewProjectCloseRequestedEventArgs(bool accepted) : EventArgs
{
    /// <summary>
    ///     Gets a value indicating whether the dialog closed through OK.
    /// </summary>
    public bool Accepted { get; } = accepted;
}

/// <summary>
///     View model of the New Project dialog: a project name and the folder that will contain the project folder,
///     each validated as it is typed. OK is enabled only when the project could be created; the caller creates
///     it from <see cref="ProjectName" /> and <see cref="ParentFolder" /> when the dialog was accepted.
/// </summary>
public sealed partial class NewProjectDialogViewModel : ObservableObject
{
    /// <summary>
    ///     Validates the entries.
    /// </summary>
    private readonly IProjectService _projectService;

    /// <summary>
    ///     Lets the user browse for the parent folder.
    /// </summary>
    private readonly IFolderPicker _folderPicker;

    /// <summary>
    ///     The project name as typed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameError), nameof(TargetError), nameof(TargetPath), nameof(IsValid))]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private string _projectName = string.Empty;

    /// <summary>
    ///     The parent folder as typed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FolderError), nameof(TargetError), nameof(TargetPath), nameof(IsValid))]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private string _parentFolder;

    /// <summary>
    ///     Creates the dialog view model.
    /// </summary>
    /// <param name="projectService">Validates the entries.</param>
    /// <param name="folderPicker">Lets the user browse for the parent folder.</param>
    /// <param name="initialParentFolder">The parent folder to start with, or <see langword="null" /> for none.</param>
    public NewProjectDialogViewModel(IProjectService projectService, IFolderPicker folderPicker, string? initialParentFolder = null)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(folderPicker);

        _projectService = projectService;
        _folderPicker = folderPicker;
        _parentFolder = initialParentFolder ?? string.Empty;
    }

    /// <summary>
    ///     Raised when the dialog asks its host to close.
    /// </summary>
    public event EventHandler<NewProjectCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    ///     Gets the dialog title.
    /// </summary>
    public string Title { get; } = "New Project";

    /// <summary>
    ///     Gets a value indicating whether the dialog was closed through OK.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    ///     Gets the project name problem, or an empty string when the name is acceptable.
    /// </summary>
    public string NameError => _projectService.ValidateProjectName(ProjectName) ?? string.Empty;

    /// <summary>
    ///     Gets the parent folder problem, or an empty string when the folder is acceptable.
    /// </summary>
    public string FolderError => _projectService.ValidateParentFolder(ParentFolder) ?? string.Empty;

    /// <summary>
    ///     Gets the problem with the project folder itself, such as an existing non-empty folder, or an empty
    ///     string. It is empty while the name or the parent folder has its own problem.
    /// </summary>
    public string TargetError =>
        NameError.Length > 0 || FolderError.Length > 0
            ? string.Empty
            : _projectService.ValidateNewProject(ParentFolder, ProjectName) ?? string.Empty;

    /// <summary>
    ///     Gets the full path of the project folder that would be created, or an empty string while the entries
    ///     are not valid.
    /// </summary>
    public string TargetPath
    {
        get
        {
            if (!IsValid)
            {
                return string.Empty;
            }

            return PathHelpers.SafePathCombine(Path.GetFullPath(ParentFolder.Trim()), ProjectName.Trim());
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the project can be created from the entries.
    /// </summary>
    public bool IsValid => NameError.Length == 0 && FolderError.Length == 0 && TargetError.Length == 0;

    /// <summary>
    ///     Lets the user choose the parent folder. Canceling leaves the entry unchanged.
    /// </summary>
    /// <returns>A task that completes when the user has chosen or cancelled.</returns>
    [RelayCommand]
    private async Task BrowseAsync()
    {
        var start = string.IsNullOrWhiteSpace(ParentFolder) ? null : ParentFolder.Trim();
        var chosen = await _folderPicker.PickFolderAsync(start);
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            ParentFolder = chosen;
        }
    }

    /// <summary>
    ///     Accepts the entries and closes the dialog.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsValid))]
    private void Ok()
    {
        Accepted = true;
        CloseRequested?.Invoke(this, new NewProjectCloseRequestedEventArgs(accepted: true));
    }

    /// <summary>
    ///     Closes the dialog without accepting.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new NewProjectCloseRequestedEventArgs(accepted: false));
    }
}

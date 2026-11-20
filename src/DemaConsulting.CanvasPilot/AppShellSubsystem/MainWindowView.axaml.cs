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

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     The application main window, bound to a <see cref="MainWindowShell" />. It also implements
///     <see cref="IDialogService" />, because showing dialogs and platform pickers needs the window as owner.
///     Exit closes the window.
/// </summary>
public sealed partial class MainWindowView : Window, IDialogService
{
    /// <summary>
    ///     The project file type offered by the open file picker.
    /// </summary>
    private static readonly FilePickerFileType ProjectFileType = new("CanvasPilot projects")
    {
        Patterns = ["*" + ProjectDocument.FileExtension]
    };

    /// <summary>
    ///     View model bound to the window.
    /// </summary>
    private readonly MainWindowShell? _shell;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindowView" /> class for the XAML loader. The window
    ///     has no content until a view model is supplied.
    /// </summary>
    public MainWindowView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindowView" /> class.
    /// </summary>
    /// <param name="shell">The view model that backs the window.</param>
    public MainWindowView(MainWindowShell shell)
        : this()
    {
        ArgumentNullException.ThrowIfNull(shell);

        _shell = shell;
        DataContext = shell;
        shell.AttachDialogService(this);
        shell.ExitRequested += OnExitRequested;
    }

    /// <inheritdoc />
    public async Task ShowNewProjectAsync(NewProjectDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        await new NewProjectDialogView(viewModel).ShowDialog<bool>(this);
    }

    /// <inheritdoc />
    public async Task ShowSettingsAsync(SettingsDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        await new SettingsDialogView(viewModel).ShowDialog<bool>(this);
    }

    /// <inheritdoc />
    public async Task ShowAboutAsync(AboutDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        await new AboutDialogView(viewModel).ShowDialog(this);
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string? initialFolder)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Choose Folder",
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetFolderAsync(initialFolder)
        };

        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickProjectFileAsync(string? initialFolder)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Open Project",
            AllowMultiple = false,
            FileTypeFilter = [ProjectFileType],
            SuggestedStartLocation = await TryGetFolderAsync(initialFolder)
        };

        var files = await StorageProvider.OpenFilePickerAsync(options);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string filterName, string filterPattern)
    {
        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = Path.GetExtension(filterPattern),
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = [filterPattern] }]
        };

        var file = await StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task ShowMessageAsync(string title, string message)
    {
        await new MessageDialogView(new MessageDialogViewModel(title, message)).ShowDialog<bool>(this);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var viewModel = new MessageDialogViewModel(title, message, confirmText, showCancel: true);
        await new MessageDialogView(viewModel).ShowDialog<bool>(this);
        return viewModel.Accepted;
    }

    /// <inheritdoc />
    public async Task<string?> PromptAsync(string title, string message, string initialText, string confirmText)
    {
        var viewModel = new MessageDialogViewModel(title, message, confirmText, showCancel: true, inputText: initialText);
        await new MessageDialogView(viewModel).ShowDialog<bool>(this);
        return viewModel.Accepted ? viewModel.InputText.Trim() : null;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (_shell is not null)
        {
            _shell.ExitRequested -= OnExitRequested;
        }

        base.OnClosed(e);
    }

    /// <summary>
    ///     Resolves a folder path to a storage folder for a picker's start location.
    /// </summary>
    /// <param name="folder">The folder path, or <see langword="null" />.</param>
    /// <returns>The storage folder, or <see langword="null" /> when none was given or it cannot be used.</returns>
    private async Task<IStorageFolder?> TryGetFolderAsync(string? folder)
    {
        return string.IsNullOrWhiteSpace(folder) ? null : await StorageProvider.TryGetFolderFromPathAsync(folder);
    }

    /// <summary>
    ///     Closes the window when the user chose Exit.
    /// </summary>
    /// <param name="sender">The shell.</param>
    /// <param name="e">The event data.</param>
    private void OnExitRequested(object? sender, EventArgs e)
    {
        Close();
    }
}

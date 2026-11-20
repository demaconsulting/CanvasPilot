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

using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Hand-written dialog service for tests: it records what the shell asked for and answers as the test
///     configured, so no window is needed.
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>
    ///     Gets or sets what the user does in the New Project dialog, for example typing a name and choosing OK.
    ///     When null, the dialog is cancelled.
    /// </summary>
    public Action<NewProjectDialogViewModel>? OnNewProject { get; set; }

    /// <summary>
    ///     Gets or sets what the user does in the settings dialog, for example editing a page and choosing OK.
    ///     When null, the dialog is only recorded.
    /// </summary>
    public Action<SettingsDialogViewModel>? OnSettings { get; set; }

    /// <summary>
    ///     Gets or sets the project file the user picks, or null for cancel.
    /// </summary>
    public string? ProjectFileToPick { get; set; }

    /// <summary>
    ///     Gets or sets the folder the user picks, or null for cancel.
    /// </summary>
    public string? FolderToPick { get; set; }

    /// <summary>
    ///     Gets or sets the file the user picks to save to, or null for cancel.
    /// </summary>
    public string? SaveFileToPick { get; set; }

    /// <summary>
    ///     Gets or sets whether the user confirms.
    /// </summary>
    public bool ConfirmResult { get; set; }

    /// <summary>
    ///     Gets or sets the text the user enters in a prompt, or null for cancel.
    /// </summary>
    public string? PromptResult { get; set; }

    /// <summary>
    ///     Gets or sets whether copying to the clipboard succeeds.
    /// </summary>
    public bool ClipboardAvailable { get; set; } = true;

    /// <summary>
    ///     Gets the pictures that were copied to the clipboard.
    /// </summary>
    public List<RenderedImage> CopiedImages { get; } = [];

    /// <summary>
    ///     Gets how many times the New Project dialog was shown.
    /// </summary>
    public int NewProjectCount { get; private set; }

    /// <summary>
    ///     Gets how many times the settings dialog was shown.
    /// </summary>
    public int SettingsCount { get; private set; }

    /// <summary>
    ///     Gets the last settings dialog view model shown.
    /// </summary>
    public SettingsDialogViewModel? LastSettings { get; private set; }

    /// <summary>
    ///     Gets how many times the About dialog was shown.
    /// </summary>
    public int AboutCount { get; private set; }

    /// <summary>
    ///     Gets the last About dialog view model shown.
    /// </summary>
    public AboutDialogViewModel? LastAbout { get; private set; }

    /// <summary>
    ///     Gets how many times the project file picker was shown.
    /// </summary>
    public int ProjectPickCount { get; private set; }

    /// <summary>
    ///     Gets the folder the last picker was asked to start in.
    /// </summary>
    public string? LastInitialFolder { get; private set; }

    /// <summary>
    ///     Gets the messages shown, as title and message.
    /// </summary>
    public List<(string Title, string Message)> Messages { get; } = [];

    /// <summary>
    ///     Gets the confirmations asked, as title and message.
    /// </summary>
    public List<(string Title, string Message)> Confirmations { get; } = [];

    /// <summary>
    ///     Gets the prompts asked, as title, message, and initial text.
    /// </summary>
    public List<(string Title, string Message, string InitialText)> Prompts { get; } = [];

    /// <inheritdoc />
    public Task ShowNewProjectAsync(NewProjectDialogViewModel viewModel)
    {
        NewProjectCount++;
        if (OnNewProject is null)
        {
            viewModel.CancelCommand.Execute(null);
        }
        else
        {
            OnNewProject(viewModel);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowSettingsAsync(SettingsDialogViewModel viewModel)
    {
        SettingsCount++;
        LastSettings = viewModel;
        OnSettings?.Invoke(viewModel);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowAboutAsync(AboutDialogViewModel viewModel)
    {
        AboutCount++;
        LastAbout = viewModel;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> PickFolderAsync(string? initialFolder)
    {
        LastInitialFolder = initialFolder;
        return Task.FromResult(FolderToPick);
    }

    /// <inheritdoc />
    public Task<string?> PickProjectFileAsync(string? initialFolder)
    {
        ProjectPickCount++;
        LastInitialFolder = initialFolder;
        return Task.FromResult(ProjectFileToPick);
    }

    /// <inheritdoc />
    public Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string filterName, string filterPattern)
    {
        return Task.FromResult(SaveFileToPick);
    }

    /// <inheritdoc />
    public Task<bool> CopyImageToClipboardAsync(RenderedImage image)
    {
        CopiedImages.Add(image);
        return Task.FromResult(ClipboardAvailable);
    }

    /// <inheritdoc />
    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Confirmations.Add((title, message));
        return Task.FromResult(ConfirmResult);
    }

    /// <inheritdoc />
    public Task<string?> PromptAsync(string title, string message, string initialText, string confirmText)
    {
        Prompts.Add((title, message, initialText));
        return Task.FromResult(PromptResult);
    }
}

/// <summary>
///     Hand-written image exporter for tests: it can be switched on and off and records how often it was asked to
///     export or copy.
/// </summary>
public sealed class FakeImageExporter : IImageExporter
{
    /// <summary>
    ///     Whether an image can be exported.
    /// </summary>
    private bool _canExport;

    /// <inheritdoc />
    public event EventHandler? CanExportChanged;

    /// <inheritdoc />
    public bool CanExport
    {
        get => _canExport;
        set
        {
            _canExport = value;
            CanExportChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Gets how many times a PNG export was requested.
    /// </summary>
    public int ExportCount { get; private set; }

    /// <summary>
    ///     Gets how many times a copy was requested.
    /// </summary>
    public int CopyCount { get; private set; }

    /// <summary>
    ///     Gets the dialog service passed to the last request.
    /// </summary>
    public IDialogService? LastDialogs { get; private set; }

    /// <inheritdoc />
    public Task ExportPngAsync(IDialogService dialogs)
    {
        ExportCount++;
        LastDialogs = dialogs;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CopyImageAsync(IDialogService dialogs)
    {
        CopyCount++;
        LastDialogs = dialogs;
        return Task.CompletedTask;
    }
}

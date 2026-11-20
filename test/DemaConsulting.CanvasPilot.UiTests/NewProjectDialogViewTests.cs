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
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the New Project dialog window.
/// </summary>
public sealed class NewProjectDialogViewTests : IDisposable
{
    /// <summary>
    ///     Temporary folder that plays the user's documents folder, removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <summary>
    ///     Folder picker that returns what a test sets.
    /// </summary>
    private readonly FakeFolderPicker _picker = new();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a new dialog shows the folder, reports the missing name, and has OK disabled.
    /// </summary>
    [AvaloniaFact]
    public void NewProjectDialogView_Show_EmptyName_ShowsMessageAndDisablesOk()
    {
        // Arrange / Act: show a dialog that starts in an existing folder
        var (window, _) = ShowDialog();

        // Assert: the title and folder show, the name message is visible, and OK is disabled
        Assert.Equal("New Project", window.Title);
        Assert.Equal(_tempRoot, SettingsUiHelpers.Find<TextBox>(window, "NewProjectFolderTextBox").Text);
        Assert.True(SettingsUiHelpers.Find<TextBlock>(window, "NewProjectNameError").IsVisible);
        Assert.False(SettingsUiHelpers.Find<TextBlock>(window, "NewProjectFolderError").IsVisible);
        Assert.False(SettingsUiHelpers.Find<Button>(window, "NewProjectOkButton").IsEffectivelyEnabled);
        Assert.True(SettingsUiHelpers.Find<Button>(window, "NewProjectCancelButton").IsEffectivelyEnabled);

        window.Close();
    }

    /// <summary>
    ///     Test that typing a valid name clears the message, shows where the project goes, and enables OK.
    /// </summary>
    [AvaloniaFact]
    public void NewProjectDialogView_TypeValidName_EnablesOkAndShowsTargetPath()
    {
        // Arrange: a shown dialog
        var (window, _) = ShowDialog();

        // Act: type a name into the name box
        SettingsUiHelpers.Find<TextBox>(window, "NewProjectNameTextBox").Text = "Poster";

        // Assert: no name message, the target path is shown, and OK is enabled
        Assert.False(SettingsUiHelpers.Find<TextBlock>(window, "NewProjectNameError").IsVisible);
        var target = SettingsUiHelpers.Find<TextBlock>(window, "NewProjectTargetPath");
        Assert.True(target.IsVisible);
        Assert.Contains(Path.Combine(_tempRoot, "Poster"), target.Text);
        Assert.True(SettingsUiHelpers.Find<Button>(window, "NewProjectOkButton").IsEffectivelyEnabled);

        window.Close();
    }

    /// <summary>
    ///     Test that an invalid name and a missing folder show their messages and keep OK disabled.
    /// </summary>
    [AvaloniaFact]
    public void NewProjectDialogView_InvalidEntries_ShowMessagesAndDisableOk()
    {
        // Arrange: a shown dialog with a valid name
        var (window, _) = ShowDialog();
        var name = SettingsUiHelpers.Find<TextBox>(window, "NewProjectNameTextBox");
        name.Text = "Poster";

        // Act: make the name invalid, then point at a missing folder
        name.Text = "a/b";
        var nameMessage = SettingsUiHelpers.Find<TextBlock>(window, "NewProjectNameError").Text;
        name.Text = "Poster";
        SettingsUiHelpers.Find<TextBox>(window, "NewProjectFolderTextBox").Text = Path.Combine(_tempRoot, "missing");

        // Assert: each message was shown, and OK is disabled for the missing folder
        Assert.Contains("must not contain", nameMessage);
        Assert.True(SettingsUiHelpers.Find<TextBlock>(window, "NewProjectFolderError").IsVisible);
        Assert.Contains("does not exist", SettingsUiHelpers.Find<TextBlock>(window, "NewProjectFolderError").Text);
        Assert.False(SettingsUiHelpers.Find<Button>(window, "NewProjectOkButton").IsEffectivelyEnabled);

        window.Close();
    }

    /// <summary>
    ///     Test that the Browse button asks the folder picker and shows the folder it returns.
    /// </summary>
    [AvaloniaFact]
    public void NewProjectDialogView_BrowseButton_ShowsChosenFolder()
    {
        // Arrange: a shown dialog and a picker that will return another folder
        var chosen = Directory.CreateDirectory(Path.Combine(_tempRoot, "chosen")).FullName;
        _picker.Result = chosen;
        var (window, _) = ShowDialog();

        // Act: click Browse
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "NewProjectBrowseButton"));
        Dispatcher.UIThread.RunJobs();

        // Assert: the picker started in the old folder and the folder box shows the chosen folder
        Assert.Equal(_tempRoot, _picker.LastInitialFolder);
        Assert.Equal(chosen, SettingsUiHelpers.Find<TextBox>(window, "NewProjectFolderTextBox").Text);

        window.Close();
    }

    /// <summary>
    ///     Test that the OK button closes the window and marks the dialog accepted.
    /// </summary>
    [AvaloniaFact]
    public void NewProjectDialogView_OkButton_ClosesWindowAndAccepts()
    {
        // Arrange: a shown dialog with a valid name
        var (window, viewModel) = ShowDialog();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        viewModel.ProjectName = "Poster";

        // Act: click OK
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "NewProjectOkButton"));

        // Assert: the window closed and the dialog was accepted
        Assert.True(closed);
        Assert.True(viewModel.Accepted);
    }

    /// <summary>
    ///     Test that the Cancel button closes the window without accepting.
    /// </summary>
    [AvaloniaFact]
    public void NewProjectDialogView_CancelButton_ClosesWindowWithoutAccepting()
    {
        // Arrange: a shown dialog with a valid name
        var (window, viewModel) = ShowDialog();
        viewModel.ProjectName = "Poster";

        // Act: click Cancel
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "NewProjectCancelButton"));

        // Assert: the window closed and the dialog was not accepted
        Assert.False(window.IsVisible);
        Assert.False(viewModel.Accepted);
    }

    /// <summary>
    ///     Shows the dialog over a real project service, starting in the temporary folder.
    /// </summary>
    /// <returns>The window and its view model.</returns>
    private (NewProjectDialogView Window, NewProjectDialogViewModel ViewModel) ShowDialog()
    {
        var viewModel = new NewProjectDialogViewModel(new ProjectService(), _picker, _tempRoot);
        var window = new NewProjectDialogView(viewModel);
        window.Show();
        return (window, viewModel);
    }

    /// <summary>
    ///     Hand-written folder picker that returns a preset folder and records where it was asked to start.
    /// </summary>
    private sealed class FakeFolderPicker : IFolderPicker
    {
        /// <summary>
        ///     Gets or sets the folder to return, or <see langword="null" /> to behave as if the user cancelled.
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        ///     Gets the folder the last request asked to start in.
        /// </summary>
        public string? LastInitialFolder { get; private set; }

        /// <inheritdoc />
        public Task<string?> PickFolderAsync(string? initialFolder)
        {
            LastInitialFolder = initialFolder;
            return Task.FromResult(Result);
        }
    }
}

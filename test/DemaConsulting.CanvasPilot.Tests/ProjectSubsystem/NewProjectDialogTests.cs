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

using DemaConsulting.CanvasPilot.ProjectSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="NewProjectDialogViewModel" />.
/// </summary>
public sealed class NewProjectDialogTests : IDisposable
{
    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     The folder picker the dialog delegates browsing to.
    /// </summary>
    private readonly IFolderPicker _picker = Substitute.For<IFolderPicker>();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a new dialog starts with the supplied folder, an empty name, and OK disabled.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_New_InitialFolder_ShowsFolderAndDisablesOk()
    {
        // Arrange / Act: a dialog starting in an existing folder
        var dialog = CreateDialog(_tempRoot);

        // Assert: the folder is shown, the name is empty and reported, and OK is disabled
        Assert.Equal("New Project", dialog.Title);
        Assert.Equal(_tempRoot, dialog.ParentFolder);
        Assert.Equal(string.Empty, dialog.ProjectName);
        Assert.Contains("Enter a project name", dialog.NameError);
        Assert.Equal(string.Empty, dialog.FolderError);
        Assert.False(dialog.IsValid);
        Assert.False(dialog.OkCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that a dialog without an initial folder asks the user to choose one.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_New_NoInitialFolder_AsksForFolder()
    {
        // Arrange / Act: a dialog with no starting folder
        var dialog = CreateDialog(null);

        // Assert: the folder entry is empty and reported
        Assert.Equal(string.Empty, dialog.ParentFolder);
        Assert.Contains("Choose the folder", dialog.FolderError);
    }

    /// <summary>
    ///     Test that a valid name and folder enable OK, clear the messages, and show where the project goes.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_ValidEntries_EnableOkAndShowTargetPath()
    {
        // Arrange: a dialog in an existing folder
        var dialog = CreateDialog(_tempRoot);
        var changed = new List<string?>();
        dialog.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: type a name
        dialog.ProjectName = "  Poster  ";

        // Assert: OK is enabled, nothing is reported, the target path is shown, and the dependent properties were
        // announced
        Assert.True(dialog.IsValid);
        Assert.True(dialog.OkCommand.CanExecute(null));
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.Equal(string.Empty, dialog.TargetError);
        Assert.Equal(Path.Combine(_tempRoot, "Poster"), dialog.TargetPath);
        Assert.Contains(nameof(NewProjectDialogViewModel.IsValid), changed);
        Assert.Contains(nameof(NewProjectDialogViewModel.NameError), changed);
    }

    /// <summary>
    ///     Test that an invalid name shows its message and keeps OK disabled.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_InvalidName_ShowsMessageAndDisablesOk()
    {
        // Arrange: a dialog in an existing folder
        var dialog = CreateDialog(_tempRoot);

        // Act: type a name with a forbidden character
        dialog.ProjectName = "a/b";

        // Assert: the message explains and OK is disabled
        Assert.Contains("must not contain", dialog.NameError);
        Assert.False(dialog.IsValid);
        Assert.False(dialog.OkCommand.CanExecute(null));
        Assert.Equal(string.Empty, dialog.TargetPath);
    }

    /// <summary>
    ///     Test that a missing parent folder shows its message and keeps OK disabled.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_MissingFolder_ShowsMessageAndDisablesOk()
    {
        // Arrange: a dialog with a name
        var dialog = CreateDialog(_tempRoot);
        dialog.ProjectName = "Poster";

        // Act: point at a folder that does not exist
        dialog.ParentFolder = Path.Combine(_tempRoot, "missing");

        // Assert: the folder message appears and OK is disabled
        Assert.Contains("does not exist", dialog.FolderError);
        Assert.False(dialog.OkCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that an existing non-empty project folder is reported and keeps OK disabled.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_ExistingNonEmptyFolder_ShowsTargetErrorAndDisablesOk()
    {
        // Arrange: a folder with content named like the project
        var taken = Directory.CreateDirectory(Path.Combine(_tempRoot, "Taken")).FullName;
        File.WriteAllText(Path.Combine(taken, "file.txt"), "x");
        var dialog = CreateDialog(_tempRoot);

        // Act: type that name
        dialog.ProjectName = "Taken";

        // Assert: the target problem is reported and OK is disabled
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.Contains("not empty", dialog.TargetError);
        Assert.False(dialog.OkCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that browsing starts in the current folder and puts the chosen folder in the entry.
    /// </summary>
    [Fact]
    public async Task NewProjectDialogViewModel_Browse_FolderChosen_SetsParentFolder()
    {
        // Arrange: a picker that returns another existing folder
        var chosen = Directory.CreateDirectory(Path.Combine(_tempRoot, "chosen")).FullName;
        _picker.PickFolderAsync(Arg.Any<string?>()).Returns(Task.FromResult<string?>(chosen));
        var dialog = CreateDialog(_tempRoot);

        // Act: browse
        await dialog.BrowseCommand.ExecuteAsync(null);

        // Assert: the picker started in the old folder and the entry holds the chosen one
        await _picker.Received(1).PickFolderAsync(_tempRoot);
        Assert.Equal(chosen, dialog.ParentFolder);
    }

    /// <summary>
    ///     Test that canceling the folder picker leaves the entry alone.
    /// </summary>
    [Fact]
    public async Task NewProjectDialogViewModel_Browse_Cancelled_KeepsParentFolder()
    {
        // Arrange: a picker that is cancelled
        _picker.PickFolderAsync(Arg.Any<string?>()).Returns(Task.FromResult<string?>(null));
        var dialog = CreateDialog(_tempRoot);

        // Act: browse
        await dialog.BrowseCommand.ExecuteAsync(null);

        // Assert: the entry is unchanged
        Assert.Equal(_tempRoot, dialog.ParentFolder);
    }

    /// <summary>
    ///     Test that browsing with no folder entered lets the picker choose where to start.
    /// </summary>
    [Fact]
    public async Task NewProjectDialogViewModel_Browse_NoFolderEntered_PassesNoStartFolder()
    {
        // Arrange: a dialog with no folder
        _picker.PickFolderAsync(Arg.Any<string?>()).Returns(Task.FromResult<string?>(null));
        var dialog = CreateDialog(null);

        // Act: browse
        await dialog.BrowseCommand.ExecuteAsync(null);

        // Assert: the picker was given no start folder
        await _picker.Received(1).PickFolderAsync(null);
    }

    /// <summary>
    ///     Test that OK with valid entries marks the dialog accepted and asks the host to close with acceptance.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_Ok_ValidEntries_AcceptsAndRequestsClose()
    {
        // Arrange: a valid dialog
        var dialog = CreateDialog(_tempRoot);
        dialog.ProjectName = "Poster";
        var requests = new List<bool>();
        dialog.CloseRequested += (_, e) => requests.Add(e.Accepted);

        // Act: choose OK
        dialog.OkCommand.Execute(null);

        // Assert: accepted, with one close request
        Assert.True(dialog.Accepted);
        Assert.Equal([true], requests);
    }

    /// <summary>
    ///     Test that OK with an invalid name does nothing, because the command is disabled.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_Ok_InvalidEntries_DoesNothing()
    {
        // Arrange: an invalid dialog
        var dialog = CreateDialog(_tempRoot);
        dialog.ProjectName = "bad|name";
        var requests = 0;
        dialog.CloseRequested += (_, _) => requests++;

        // Act: try OK
        if (dialog.OkCommand.CanExecute(null))
        {
            dialog.OkCommand.Execute(null);
        }

        // Assert: not accepted and no close request
        Assert.False(dialog.Accepted);
        Assert.Equal(0, requests);
    }

    /// <summary>
    ///     Test that Cancel asks the host to close without acceptance.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_Cancel_RequestsCloseWithoutAcceptance()
    {
        // Arrange: a valid dialog
        var dialog = CreateDialog(_tempRoot);
        dialog.ProjectName = "Poster";
        var requests = new List<bool>();
        dialog.CloseRequested += (_, e) => requests.Add(e.Accepted);

        // Act: cancel
        dialog.CancelCommand.Execute(null);

        // Assert: one close request without acceptance
        Assert.False(dialog.Accepted);
        Assert.Equal([false], requests);
    }

    /// <summary>
    ///     Test that the OK command's state change is signaled when the entries become valid.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_ProjectName_BecomesValid_SignalsOkCanExecuteChanged()
    {
        // Arrange: an invalid dialog that counts state changes
        var dialog = CreateDialog(_tempRoot);
        var signals = 0;
        dialog.OkCommand.CanExecuteChanged += (_, _) => signals++;

        // Act: type a valid name
        dialog.ProjectName = "Poster";

        // Assert: the change was signaled
        Assert.True(signals > 0);
    }

    /// <summary>
    ///     Test that null collaborators are refused.
    /// </summary>
    [Fact]
    public void NewProjectDialogViewModel_Constructor_NullArguments_Throw()
    {
        // Arrange / Act / Assert: each null argument is refused
        Assert.Throws<ArgumentNullException>(() => new NewProjectDialogViewModel(null!, _picker));
        Assert.Throws<ArgumentNullException>(() => new NewProjectDialogViewModel(new ProjectService(), null!));
    }

    /// <summary>
    ///     Creates a dialog view model over a real project service and the substitute folder picker.
    /// </summary>
    /// <param name="initialFolder">The folder to start in.</param>
    /// <returns>The view model.</returns>
    private NewProjectDialogViewModel CreateDialog(string? initialFolder) =>
        new(new ProjectService(), _picker, initialFolder);
}

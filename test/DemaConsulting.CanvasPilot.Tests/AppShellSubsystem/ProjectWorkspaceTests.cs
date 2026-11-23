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
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the ProjectWorkspace unit: the explorer host, the preview content area, and the delete and
///     rename commands, over a real explorer on temporary folders and a hand-written dialog service.
/// </summary>
public sealed class ProjectWorkspaceTests : IDisposable
{
    /// <summary>
    ///     Temporary project folder removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     The workspaces created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ProjectWorkspaceViewModel> _workspaces = [];

    /// <summary>
    ///     The dialog service used by the workspaces.
    /// </summary>
    private readonly FakeDialogService _dialogs = new();

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var workspace in _workspaces)
        {
            workspace.Dispose();
        }

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a new workspace has no project, no preview, and explains how to start.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_New_NoProject_ShowsNoProjectPlaceholder()
    {
        // Arrange & Act: a workspace with nothing shown
        var workspace = CreateWorkspace();

        // Assert: the placeholder says no project is open and the delete and rename commands are disabled
        Assert.False(workspace.HasProject);
        Assert.False(workspace.HasPreview);
        Assert.False(workspace.HasSelectedFile);
        Assert.Null(workspace.SelectedFile);
        Assert.Equal("No project open", workspace.PlaceholderText);
        Assert.Contains("New Project", workspace.HintText, StringComparison.Ordinal);
        Assert.False(workspace.DeleteSelectedCommand.CanExecute(null));
        Assert.False(workspace.RenameSelectedCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that the workspace refuses a missing explorer.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_Constructor_NullExplorer_Throws()
    {
        // Arrange, Act & Assert: a missing explorer is refused
        Assert.Throws<ArgumentNullException>(() => new ProjectWorkspaceViewModel(null!));
    }

    /// <summary>
    ///     Test that showing a project lists its files, and that selecting one changes the placeholder text.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_ShowProject_ListsFilesAndTracksSelection()
    {
        // Arrange: a project folder with two images and a workspace
        Touch("b.svg", "a.svg", "notes.txt");
        var workspace = CreateWorkspace();
        var changed = new List<string?>();
        workspace.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: show the project, then select a file
        workspace.ShowProject(Project());
        var withoutSelection = workspace.PlaceholderText;
        workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));

        // Assert: the files are listed, the placeholder follows the selection, and the changes were announced
        Assert.True(workspace.HasProject);
        Assert.Equal(["a.svg", "b.svg"], workspace.Explorer.Items.Select(item => item.Name));
        Assert.Equal("No file selected", withoutSelection);
        Assert.Equal("No preview available", workspace.PlaceholderText);
        Assert.True(workspace.HasSelectedFile);
        Assert.Equal(Path.Combine(_folder, "a.svg"), workspace.SelectedFile);
        Assert.Equal(string.Empty, workspace.HintText);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.HasProject), changed);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.HasSelectedFile), changed);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.PlaceholderText), changed);
        Assert.True(workspace.DeleteSelectedCommand.CanExecute(null));
        Assert.True(workspace.RenameSelectedCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that showing the project that is already shown keeps the selection, and that showing no project
    ///     empties the explorer.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_ShowProject_SameProjectKeepsSelectionAndNullCloses()
    {
        // Arrange: a shown project with a selected file
        Touch("a.svg");
        var workspace = CreateWorkspace();
        workspace.ShowProject(Project());
        workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));

        // Act: show the same project again, then show none
        workspace.ShowProject(Project());
        var selectionKept = workspace.HasSelectedFile;
        workspace.ShowProject(null);
        workspace.ShowProject(null);

        // Assert: the selection was kept, then the explorer closed
        Assert.True(selectionKept);
        Assert.False(workspace.HasProject);
        Assert.Empty(workspace.Explorer.Items);
        Assert.Equal("No project open", workspace.PlaceholderText);
    }

    /// <summary>
    ///     Test that preview content replaces the placeholder and that removing it brings the placeholder back.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_Preview_SuppliedContent_ReplacesPlaceholder()
    {
        // Arrange: a workspace and an observer of its changes
        var workspace = CreateWorkspace();
        var changed = new List<string?>();
        workspace.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var content = new object();

        // Act: supply preview content, then remove it
        workspace.Preview = content;
        var shown = (workspace.HasPreview, workspace.Preview);
        workspace.Preview = null;

        // Assert: the content was exposed and announced, and removing it restored the placeholder state
        Assert.True(shown.HasPreview);
        Assert.Same(content, shown.Preview);
        Assert.False(workspace.HasPreview);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.HasPreview), changed);
    }

    /// <summary>
    ///     Test that a confirmed delete removes the selected file, and that a declined one keeps it.
    /// </summary>
    [Fact]
    public async Task ProjectWorkspace_DeleteSelected_ConfirmedDeletesAndDeclinedKeeps()
    {
        // Arrange: a shown project with two files and the first one selected
        Touch("a.svg", "b.svg");
        var workspace = CreateWorkspace();
        workspace.ShowProject(Project());
        workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));

        // Act: decline the confirmation, then confirm it
        _dialogs.ConfirmResult = false;
        await workspace.DeleteSelectedCommand.ExecuteAsync(null);
        var keptWhenDeclined = File.Exists(Path.Combine(_folder, "a.svg"));
        _dialogs.ConfirmResult = true;
        await workspace.DeleteSelectedCommand.ExecuteAsync(null);

        // Assert: the file survived the decline and was removed by the confirmation, which named the file
        Assert.True(keptWhenDeclined);
        Assert.False(File.Exists(Path.Combine(_folder, "a.svg")));
        Assert.Equal(["b.svg"], workspace.Explorer.Items.Select(item => item.Name));
        Assert.Equal(2, _dialogs.Confirmations.Count);
        Assert.Contains("a.svg", _dialogs.Confirmations[0].Message, StringComparison.Ordinal);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that renaming asks for the new name, renames the file, and keeps it selected.
    /// </summary>
    [Fact]
    public async Task ProjectWorkspace_RenameSelected_NewName_RenamesFile()
    {
        // Arrange: a shown project with a selected file and a user who types a new name
        Touch("a.svg");
        var workspace = CreateWorkspace();
        workspace.ShowProject(Project());
        workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _dialogs.PromptResult = "cover.svg";

        // Act: run Rename
        await workspace.RenameSelectedCommand.ExecuteAsync(null);

        // Assert: the prompt started with the current name, the file was renamed, and it is selected
        Assert.Equal("a.svg", _dialogs.Prompts[0].InitialText);
        Assert.False(File.Exists(Path.Combine(_folder, "a.svg")));
        Assert.True(File.Exists(Path.Combine(_folder, "cover.svg")));
        Assert.Equal(Path.Combine(_folder, "cover.svg"), workspace.SelectedFile);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that canceling the rename prompt changes nothing, and that an unacceptable name is reported.
    /// </summary>
    [Fact]
    public async Task ProjectWorkspace_RenameSelected_CancelledOrInvalidName_ChangesNothingAndReports()
    {
        // Arrange: a shown project with a selected file
        Touch("a.svg");
        var workspace = CreateWorkspace();
        workspace.ShowProject(Project());
        workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));

        // Act: cancel the prompt, then enter a name that is not an image source file name
        _dialogs.PromptResult = null;
        await workspace.RenameSelectedCommand.ExecuteAsync(null);
        var messagesAfterCancel = _dialogs.Messages.Count;
        _dialogs.PromptResult = "notes.txt";
        await workspace.RenameSelectedCommand.ExecuteAsync(null);

        // Assert: nothing was renamed, the cancel was silent, and the invalid name was reported
        Assert.True(File.Exists(Path.Combine(_folder, "a.svg")));
        Assert.Equal(0, messagesAfterCancel);
        var message = Assert.Single(_dialogs.Messages);
        Assert.Equal("Rename File", message.Title);
        Assert.False(string.IsNullOrWhiteSpace(message.Message));
    }

    /// <summary>
    ///     Test that delete and rename do nothing before a dialog service is attached.
    /// </summary>
    [Fact]
    public async Task ProjectWorkspace_FileCommands_NoDialogService_DoNothing()
    {
        // Arrange: a workspace with a selected file and no dialog service
        Touch("a.svg");
        var workspace = new ProjectWorkspaceViewModel(NewExplorer());
        _workspaces.Add(workspace);
        workspace.ShowProject(Project());
        workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));

        // Act: run both commands
        await workspace.DeleteSelectedCommand.ExecuteAsync(null);
        await workspace.RenameSelectedCommand.ExecuteAsync(null);

        // Assert: the file is untouched
        Assert.True(File.Exists(Path.Combine(_folder, "a.svg")));
    }

    /// <summary>
    ///     Test that disposing the workspace disposes its explorer.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_Dispose_DisposesExplorer()
    {
        // Arrange: a workspace
        var workspace = new ProjectWorkspaceViewModel(NewExplorer());

        // Act: dispose it
        workspace.Dispose();

        // Assert: the explorer refuses further use
        Assert.Throws<ObjectDisposedException>(() => workspace.Explorer.Open(_folder));
    }

    /// <summary>
    ///     Test that disposing the workspace also disposes a preview that needs disposal, such as the preview pane.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_Dispose_DisposesPreviewContent()
    {
        // Arrange: a workspace whose preview needs disposal
        var probe = new DisposeProbe();
        var workspace = new ProjectWorkspaceViewModel(NewExplorer()) { Preview = probe };

        // Act: dispose the workspace
        workspace.Dispose();

        // Assert: the preview was disposed
        Assert.True(probe.IsDisposed);
    }

    /// <summary>
    ///     Test that the chat content is shown only while a project is open: not before one is shown, and not after it is
    ///     closed, and that the changes are announced.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_Chat_ShownOnlyWhileProjectIsOpen()
    {
        // Arrange: a workspace with chat content and an observer of its changes
        Touch("a.svg");
        var workspace = CreateWorkspace();
        var changed = new List<string?>();
        workspace.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var content = new object();

        // Act: supply the chat, show a project, and close it again
        workspace.Chat = content;
        var beforeProject = workspace.IsChatVisible;
        workspace.ShowProject(Project());
        var withProject = workspace.IsChatVisible;
        workspace.ShowProject(null);
        var afterClose = workspace.IsChatVisible;

        // Assert: the chat was visible only with a project, and every change was announced
        Assert.Same(content, workspace.Chat);
        Assert.False(beforeProject);
        Assert.True(withProject);
        Assert.False(afterClose);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.IsChatVisible), changed);
    }

    /// <summary>
    ///     Test that the workspace starts with the default widths, keeps what the user chooses within the limits, announces a
    ///     choice but not a restore, and says nothing when a choice changes nothing.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_ColumnWidths_RestoreAndChoose_AreClampedAndAnnouncedOnlyForChoices()
    {
        // Arrange: a new workspace that counts the announcements
        var workspace = CreateWorkspace();
        var layoutChanges = 0;
        var changed = new List<string?>();
        workspace.LayoutChanged += (_, _) => layoutChanges++;
        workspace.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var initial = (workspace.ExplorerWidth, workspace.ChatWidth);

        // Act: restore saved widths, choose too small and too large widths, and choose the same widths again
        workspace.RestoreColumnWidths(300, 500);
        var afterRestore = (workspace.ExplorerWidth, workspace.ChatWidth, layoutChanges);
        workspace.SetColumnWidths(10, 9999);
        var afterClamp = (workspace.ExplorerWidth, workspace.ChatWidth, layoutChanges);
        workspace.SetColumnWidths(180, 640);

        // Assert: the defaults, the restore without an announcement, the limits, and no announcement for no change
        Assert.Equal((264, 400), initial);
        Assert.Equal((300, 500, 0), afterRestore);
        Assert.Equal((180, 640, 1), afterClamp);
        Assert.Equal(1, layoutChanges);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.ExplorerWidth), changed);
        Assert.Contains(nameof(ProjectWorkspaceViewModel.ChatWidth), changed);
    }

    /// <summary>
    ///     Test that a workspace with no chat content never shows the chat column, even with a project open.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_Chat_NoContent_NotShownWithProject()
    {
        // Arrange: a workspace with a project and no chat content
        Touch("a.svg");
        var workspace = CreateWorkspace();

        // Act: show the project
        workspace.ShowProject(Project());

        // Assert: the chat column is not shown
        Assert.Null(workspace.Chat);
        Assert.False(workspace.IsChatVisible);
    }

    /// <summary>
    ///     Test that disposing the workspace leaves the chat content alone, because the composition root owns it.
    /// </summary>
    [Fact]
    public void ProjectWorkspace_Dispose_DoesNotDisposeChatContent()
    {
        // Arrange: a workspace whose chat content needs disposal
        var probe = new DisposeProbe();
        var workspace = new ProjectWorkspaceViewModel(NewExplorer()) { Chat = probe };

        // Act: dispose the workspace
        workspace.Dispose();

        // Assert: the chat content was not disposed
        Assert.False(probe.IsDisposed);
    }

    /// <summary>
    ///     Creates a workspace with the dialog service attached.
    /// </summary>
    /// <returns>The workspace, disposed with the fixture.</returns>
    private ProjectWorkspaceViewModel CreateWorkspace()
    {
        var workspace = new ProjectWorkspaceViewModel(NewExplorer());
        workspace.AttachDialogService(_dialogs);
        _workspaces.Add(workspace);
        return workspace;
    }

    /// <summary>
    ///     Creates an explorer that treats <c>.svg</c> files as image sources and never refreshes by itself.
    /// </summary>
    /// <returns>The explorer.</returns>
    private static ProjectExplorerViewModel NewExplorer()
    {
        return new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), new FakeRefreshScheduler());
    }

    /// <summary>
    ///     Describes the project folder as an open project.
    /// </summary>
    /// <returns>The project.</returns>
    private ProjectInfo Project()
    {
        return new ProjectInfo("Test", _folder, Path.Combine(_folder, "Test.canvaspilot"), new ProjectDocument { Name = "Test" });
    }

    /// <summary>
    ///     Creates files in the project folder.
    /// </summary>
    /// <param name="names">The file names.</param>
    private void Touch(params string[] names)
    {
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(_folder, name), "x");
        }
    }
}

/// <summary>
///     Content that records whether it was disposed, standing in for a preview that needs disposal.
/// </summary>
internal sealed class DisposeProbe : IDisposable
{
    /// <summary>
    ///     Gets a value indicating whether the object was disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        IsDisposed = true;
    }
}

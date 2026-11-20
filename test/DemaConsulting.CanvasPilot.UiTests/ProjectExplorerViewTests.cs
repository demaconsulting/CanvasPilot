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
///     Headless UI tests for the project explorer view.
/// </summary>
public sealed class ProjectExplorerViewTests : IDisposable
{
    /// <summary>
    ///     Temporary project folder removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <summary>
    ///     The explorers created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ProjectExplorerViewModel> _explorers = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var explorer in _explorers)
        {
            explorer.Dispose();
        }

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the view lists the image files of the open project and hides the empty message.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_OpenProject_ListsImageFiles()
    {
        // Arrange: a project folder with two images and a text file
        Touch("b.svg", "a.svg", "notes.txt");
        var (window, _) = ShowExplorer(project: true);

        // Act / Assert: the list holds the two images in name order and no empty message shows
        var list = SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList");
        Assert.Equal(2, list.ItemCount);
        Assert.Equal(
            ["a.svg", "b.svg"],
            list.Items.OfType<ProjectFileItem>().Select(item => item.Name));
        Assert.False(SettingsUiHelpers.Find<TextBlock>(window, "ProjectExplorerEmptyMessage").IsVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that the view explains an empty explorer, both with no project and with a project without images.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_NoProjectOrNoImages_ShowsEmptyMessage()
    {
        // Arrange: an explorer with no project
        var (window, viewModel) = ShowExplorer(project: false);
        var message = SettingsUiHelpers.Find<TextBlock>(window, "ProjectExplorerEmptyMessage");
        var withoutProject = (message.IsVisible, message.Text);

        // Act: open a project folder that holds no images
        viewModel.Open(_folder);

        // Assert: each state showed its own explanation
        Assert.True(withoutProject.IsVisible);
        Assert.Contains("No project", withoutProject.Text);
        Assert.True(message.IsVisible);
        Assert.Contains("no image files", message.Text);

        window.Close();
    }

    /// <summary>
    ///     Test that choosing a file in the list selects it in the view model.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_SelectItem_UpdatesSelectedFile()
    {
        // Arrange: a shown explorer with two files
        Touch("a.svg", "b.svg");
        var (window, viewModel) = ShowExplorer(project: true);
        var list = SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList");

        // Act: select the second file in the list
        list.SelectedIndex = 1;

        // Assert: the view model reports its path
        Assert.Equal(Path.Combine(_folder, "b.svg"), viewModel.SelectedFile);

        window.Close();
    }

    /// <summary>
    ///     Test that selecting a file in the view model selects it in the list.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_SelectFileInViewModel_SelectsItemInList()
    {
        // Arrange: a shown explorer with two files
        Touch("a.svg", "b.svg");
        var (window, viewModel) = ShowExplorer(project: true);
        var list = SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList");

        // Act: select a file by path
        viewModel.SelectFile(Path.Combine(_folder, "a.svg"));

        // Assert: the list shows it selected
        Assert.Equal(0, list.SelectedIndex);

        window.Close();
    }

    /// <summary>
    ///     Test that a problem renaming a file appears in the view.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_RenameRefused_ShowsErrorMessage()
    {
        // Arrange: a shown explorer with a selected file
        Touch("a.svg");
        var (window, viewModel) = ShowExplorer(project: true);
        var error = SettingsUiHelpers.Find<TextBlock>(window, "ProjectExplorerError");
        var initiallyVisible = error.IsVisible;
        viewModel.SelectFile(Path.Combine(_folder, "a.svg"));

        // Act: try an invalid new name
        viewModel.RenameSelected("not/valid.svg");

        // Assert: the error appears and the file is unchanged
        Assert.False(initiallyVisible);
        Assert.True(error.IsVisible);
        Assert.Contains("must not contain", error.Text);
        Assert.True(File.Exists(Path.Combine(_folder, "a.svg")));

        window.Close();
    }

    /// <summary>
    ///     Test that a file added to and a file deleted from the project folder appear in the list by themselves,
    ///     through the real folder watcher and the debounced refresh on the user interface thread.
    /// </summary>
    [AvaloniaFact]
    public async Task ProjectExplorerView_FilesChangeOnDisk_ListRefreshesAfterDebounce()
    {
        // Arrange: a shown explorer using the real scheduler with a short delay, and one file
        var token = TestContext.Current.CancellationToken;
        Touch("first.svg");
        var (window, viewModel) = ShowExplorer(project: true, scheduler: new DispatcherRefreshScheduler(), debounce: TimeSpan.FromMilliseconds(50));
        var list = SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList");

        // Act: add a file and wait for the list to follow, then delete the first and wait again
        await File.WriteAllTextAsync(Path.Combine(_folder, "second.svg"), string.Empty, token);
        await WaitUntilAsync(() => list.ItemCount == 2, token);
        var afterAdd = viewModel.Items.Select(item => item.Name).ToList();
        File.Delete(Path.Combine(_folder, "first.svg"));
        await WaitUntilAsync(() => list.ItemCount == 1, token);

        // Assert: the list followed both changes
        Assert.Equal(["first.svg", "second.svg"], afterAdd);
        Assert.Equal(["second.svg"], viewModel.Items.Select(item => item.Name));

        window.Close();
    }

    /// <summary>
    ///     Waits, keeping the user interface thread running, until a condition holds or a timeout passes.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds or the timeout passed.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, cancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    ///     Shows an explorer view in a plain window.
    /// </summary>
    /// <param name="project">Whether to open the temporary folder as the project.</param>
    /// <param name="scheduler">The scheduler to use; a manual one by default.</param>
    /// <param name="debounce">The debounce delay, or <see langword="null" /> for the default.</param>
    /// <returns>The window and the view model.</returns>
    private (Window Window, ProjectExplorerViewModel ViewModel) ShowExplorer(
        bool project,
        IRefreshScheduler? scheduler = null,
        TimeSpan? debounce = null)
    {
        var viewModel = new ProjectExplorerViewModel(
            new CanvasNetImageSourceFilter(),
            scheduler ?? new ManualScheduler(),
            debounce);
        _explorers.Add(viewModel);
        if (project)
        {
            viewModel.Open(_folder);
        }

        var window = new Window { Content = new ProjectExplorerView { DataContext = viewModel } };
        window.Show();
        return (window, viewModel);
    }

    /// <summary>
    ///     Creates empty files in the project folder.
    /// </summary>
    /// <param name="names">The file names.</param>
    private void Touch(params string[] names)
    {
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(_folder, name), string.Empty);
        }
    }

    /// <summary>
    ///     A scheduler that never runs its callbacks, so a test sees only explicit refreshes.
    /// </summary>
    private sealed class ManualScheduler : IRefreshScheduler
    {
        /// <inheritdoc />
        public IDisposable Schedule(TimeSpan delay, Action callback) => new Nothing();

        /// <summary>
        ///     A cancellation that has nothing to cancel.
        /// </summary>
        private sealed class Nothing : IDisposable
        {
            /// <inheritdoc />
            public void Dispose()
            {
                // Nothing was scheduled, so there is nothing to cancel.
            }
        }
    }
}

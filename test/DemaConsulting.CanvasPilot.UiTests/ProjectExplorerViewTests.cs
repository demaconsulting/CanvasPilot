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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
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
        var list = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");
        Assert.Equal(2, list.ItemCount);
        Assert.Equal(
            ["a.svg", "b.svg"],
            list.Items.OfType<ProjectNodeViewModel>().Select(node => node.Name));
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
        var list = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");

        // Act: select the second file in the list
        list.SelectedItem = list.Items[1];

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
        var list = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");

        // Act: select a file by path
        viewModel.SelectFile(Path.Combine(_folder, "a.svg"));

        // Assert: the list shows it selected
        Assert.Same(list.Items[0], list.SelectedItem);

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
        var initiallyVisible = error.IsEffectivelyVisible;
        viewModel.SelectFile(Path.Combine(_folder, "a.svg"));

        // Act: try an invalid new name
        viewModel.RenameSelected("not/valid.svg");

        // Assert: the error appears and the file is unchanged
        Assert.False(initiallyVisible);
        Assert.True(error.IsEffectivelyVisible);
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
        var list = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");

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
    ///     Test that the view shows folders and files as a tree: a folder row with a chevron and its children indented 16 pixels
    ///     below it once it is expanded, a badge of the kind in front of each name, rows 26 high, and that selecting a folder
    ///     selects no file while selecting a picture does.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_FoldersAndFiles_ShowAsRowsWithBadgesAndIndent()
    {
        // Arrange: a project with a folder that holds a picture and a PDF, and a picture at the top
        Touch("top.svg", "assets/inner.svg", "assets/plan.pdf");
        var (window, viewModel) = ShowExplorer(project: true);
        var tree = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");
        var folderItem = Assert.IsType<TreeViewItem>(tree.ContainerFromIndex(0));
        var collapsedHeight = folderItem.Bounds.Height;

        // Act: expand the folder, then read the rows
        viewModel.Roots[0].IsExpanded = true;
        window.UpdateLayout();
        AppShellUiHelpers.Flush();
        window.UpdateLayout();
        var inner = Assert.IsType<TreeViewItem>(folderItem.ContainerFromIndex(0));
        var topFile = Assert.IsType<TreeViewItem>(tree.ContainerFromIndex(1));
        var badges = window.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("badge")).Select(border => border.Tag).ToArray();
        var indent = LeftOf(inner, window) - LeftOf(topFile, window);
        var rowHeight = inner.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot").Bounds.Height;

        // Assert: the folder and its children are rows with badges, nested one level in, 26 high
        Assert.True(folderItem.IsExpanded);
        Assert.Equal(["folder", "svg", "pdf", "svg"], badges);
        Assert.Equal(16, indent, 0.5);
        Assert.Equal(26, rowHeight, 0.5);
        Assert.True(folderItem.Bounds.Height > collapsedHeight);

        // Act: select the folder, then the nested picture
        tree.SelectedItem = viewModel.Roots[0];
        var afterFolder = viewModel.SelectedFile;
        tree.SelectedItem = viewModel.Roots[0].Children[0];
        var afterPicture = viewModel.SelectedFile;

        // Assert: a folder selects no file and a picture selects its file
        Assert.Null(afterFolder);
        Assert.Equal(Path.Combine(_folder, "assets", "inner.svg"), afterPicture);

        window.Close();
    }

    /// <summary>
    ///     Test that the selected row shows the accent bar and a semibold name, and that rows that are not selected show
    ///     neither.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_SelectedRow_ShowsBarAndSemiboldName()
    {
        // Arrange: a shown explorer with two files
        Touch("a.svg", "b.svg");
        var (window, viewModel) = ShowExplorer(project: true);
        var tree = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");
        var first = Assert.IsType<TreeViewItem>(tree.ContainerFromIndex(0));
        var second = Assert.IsType<TreeViewItem>(tree.ContainerFromIndex(1));

        // Act: select the first file
        viewModel.SelectFile(Path.Combine(_folder, "a.svg"));
        window.UpdateLayout();
        AppShellUiHelpers.Flush();

        // Assert: only the selected row has the bar and the heavier name
        Assert.True(BarOf(first).IsVisible);
        Assert.False(BarOf(second).IsVisible);
        Assert.Equal(Avalonia.Media.FontWeight.SemiBold, first.FontWeight);
        Assert.NotEqual(Avalonia.Media.FontWeight.SemiBold, second.FontWeight);

        window.Close();
    }

    /// <summary>
    ///     Test that an unreadable project folder shows its problem with the danger icon and words, and that the explorer
    ///     lists nothing then.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_UnreadableFolder_ShowsTheProblemWithTheDangerIcon()
    {
        // Arrange: an explorer that is shown with a project folder that does not exist
        var (window, viewModel) = ShowExplorer(project: false);
        var error = SettingsUiHelpers.Find<TextBlock>(window, "ProjectExplorerError");

        // Act: open the missing folder
        viewModel.Open(Path.Combine(_folder, "missing"));
        window.UpdateLayout();
        var icon = error.GetVisualAncestors().OfType<Grid>().First().GetVisualDescendants().OfType<PathIcon>().First();

        // Assert: the words say what happened, in the danger color with an icon of the same color, and nothing is listed
        Assert.True(error.IsEffectivelyVisible);
        Assert.Contains("could not be read", error.Text, StringComparison.Ordinal);
        Assert.Equal(ThemeUiHelpers.ColorOf("CpDanger", window.ActualThemeVariant), Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(error.Foreground).Color);
        Assert.Equal(((Avalonia.Media.ISolidColorBrush)error.Foreground!).Color, ((Avalonia.Media.ISolidColorBrush)icon.Foreground!).Color);
        Assert.Equal(0, SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList").ItemCount);

        window.Close();
    }

    /// <summary>
    ///     Reads the selection bar of a row.
    /// </summary>
    /// <param name="row">The tree item.</param>
    /// <returns>The bar.</returns>
    private static Border BarOf(TreeViewItem row)
    {
        return row.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_SelectionBar");
    }

    /// <summary>
    ///     Finds the place of the left edge of the badge of a row in a window, which is where the content of the row starts.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <param name="window">The window.</param>
    /// <returns>The place, in the coordinates of the window.</returns>
    private static double LeftOf(Control control, Window window)
    {
        return control.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("badge"))
            .TranslatePoint(new Point(0, 0), window)?.X ?? double.NaN;
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
    ///     Creates empty files in the project folder, creating folders as needed.
    /// </summary>
    /// <param name="names">The file names, relative to the project folder.</param>
    private void Touch(params string[] names)
    {
        foreach (var name in names)
        {
            var path = Path.Combine(_folder, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
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

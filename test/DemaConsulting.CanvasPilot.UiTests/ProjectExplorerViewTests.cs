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
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
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
    ///     Test that a row is 26 high while the rows are compact, 32 high when the explorer is set to comfortable rows, and 26
    ///     high again when the setting is turned off.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_ComfortableRows_RowsAre32HighInsteadOf26()
    {
        // Arrange: a shown explorer with a file
        Touch("a.svg");
        var (window, viewModel) = ShowExplorer(project: true);
        var tree = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");
        var row = Assert.IsType<TreeViewItem>(tree.ContainerFromIndex(0));

        // Act: read the height of the row compact, comfortable, and compact again
        var compact = RowHeightOf(row, window);
        viewModel.ComfortableRows = true;
        var comfortable = RowHeightOf(row, window);
        viewModel.ComfortableRows = false;
        var compactAgain = RowHeightOf(row, window);

        // Assert: the heights follow the setting
        Assert.Equal(26, compact, 0.5);
        Assert.Equal(32, comfortable, 0.5);
        Assert.Equal(26, compactAgain, 0.5);

        window.Close();
    }

    /// <summary>
    ///     Lets the layout settle and reads the height of the row of a tree item.
    /// </summary>
    /// <param name="row">The tree item.</param>
    /// <param name="window">The window that shows it.</param>
    /// <returns>The height of the row in device independent pixels.</returns>
    private static double RowHeightOf(TreeViewItem row, Window window)
    {
        window.UpdateLayout();
        AppShellUiHelpers.Flush();
        window.UpdateLayout();
        return row.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot").Bounds.Height;
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
    ///     Test that the header shows the label of the pane in capitals and three icon buttons with names and tooltips, that the
    ///     buttons run the commands of the explorer, and that the refresh button reads the folder again.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_Header_ShowsLabelAndButtonsThatRunTheCommands()
    {
        // Arrange: a shown explorer over a project, whose New File and New Folder commands count their runs
        Touch("a.svg");
        var (window, viewModel) = ShowExplorer(project: true);
        var fileRuns = 0;
        var folderRuns = 0;
        viewModel.NewFileCommand = new RelayCommand(() => fileRuns++);
        viewModel.NewFolderCommand = new RelayCommand(() => folderRuns++);
        window.UpdateLayout();
        var newFile = SettingsUiHelpers.Find<Button>(window, "ExplorerNewFileButton");
        var newFolder = SettingsUiHelpers.Find<Button>(window, "ExplorerNewFolderButton");
        var refresh = SettingsUiHelpers.Find<Button>(window, "ExplorerRefreshButton");
        var label = window.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "EXPLORER");
        var names = new[] { newFile, newFolder, refresh }.Select(button => (AutomationProperties.GetName(button), ToolTip.GetTip(button))).ToArray();

        // Act: use the three buttons, with a file that appeared behind the explorer's back
        Touch("b.svg");
        SettingsUiHelpers.Click(window, newFile);
        SettingsUiHelpers.Click(window, newFolder);
        SettingsUiHelpers.Click(window, refresh);

        // Assert: the label and the names, the commands that ran, and the refreshed list
        Assert.True(label.IsEffectivelyVisible);
        Assert.Equal([("New file", (object?)"New file"), ("New folder", "New folder"), ("Refresh", "Refresh")], names);
        Assert.Equal((1, 1), (fileRuns, folderRuns));
        Assert.Equal(["a.svg", "b.svg"], viewModel.Items.Select(item => item.Name));
        Assert.All(new[] { newFile, newFolder, refresh }, button => Assert.True(button.Bounds.Width >= 28 && button.Bounds.Height >= 28));

        window.Close();
    }

    /// <summary>
    ///     Test that the buttons that need a command are not usable without one, and that the drop zone is shown only while a
    ///     project is open.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_NoCommandsNoProject_ButtonsOffAndDropZoneHidden()
    {
        // Arrange: a shown explorer with no project and no commands
        var (window, viewModel) = ShowExplorer(project: false);
        var newFile = SettingsUiHelpers.Find<Button>(window, "ExplorerNewFileButton");
        var refresh = SettingsUiHelpers.Find<Button>(window, "ExplorerRefreshButton");
        var zone = SettingsUiHelpers.Find<Panel>(window, "ExplorerDropZone");
        var before = (newFile.IsEffectivelyEnabled, refresh.IsEffectivelyEnabled, zone.IsVisible);

        // Act: open a project
        viewModel.Open(_folder);
        var after = (refresh.IsEffectivelyEnabled, zone.IsVisible);

        // Assert: without a project or a command the buttons are off and the zone is hidden; a project turns the refresh and the zone on
        Assert.Equal((false, false, false), before);
        Assert.Equal((true, true), after);

        window.Close();
    }

    /// <summary>
    ///     Test that files dropped on the drop zone are copied into the attachments folder while the zone is lit during the
    ///     drag, that files dropped on a folder row are copied into that folder, and that the highlight goes away afterwards.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_DropFiles_OnZoneAndOnFolderRow_CopiesAndHighlights()
    {
        // Arrange: a project with a folder, and two pictures outside it
        Touch("assets/one.svg");
        var outside = Directory.CreateTempSubdirectory("canvaspilot-ui-drop-").FullName;
        var first = Path.Combine(outside, "logo.png");
        var second = Path.Combine(outside, "chart.png");
        File.WriteAllText(first, "png");
        File.WriteAllText(second, "png");
        var (window, viewModel) = ShowExplorer(project: true);
        var zone = SettingsUiHelpers.Find<Panel>(window, "ExplorerDropZone");
        var folderRow = Assert.IsType<TreeViewItem>(SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList").ContainerFromIndex(0));

        try
        {
            // Act: drag a file over the zone, look, and drop it; then drag a file over the folder row, look, and drop it
            var onZone = CenterOf(zone, window);
            var data = FilesTransfer(window, first);
            window.DragDrop(onZone, RawDragEventType.DragEnter, data, DragDropEffects.Copy);
            window.DragDrop(onZone, RawDragEventType.DragOver, data, DragDropEffects.Copy);
            var zoneLit = zone.Classes.Contains("drag-active");
            window.DragDrop(onZone, RawDragEventType.Drop, data, DragDropEffects.Copy);
            var zoneAfter = zone.Classes.Contains("drag-active");
            AppShellUiHelpers.Flush();
            var onRow = CenterOf(folderRow.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot"), window);
            var rowData = FilesTransfer(window, second);
            window.DragDrop(onRow, RawDragEventType.DragEnter, rowData, DragDropEffects.Copy);
            window.DragDrop(onRow, RawDragEventType.DragOver, rowData, DragDropEffects.Copy);
            var rowLit = folderRow.Classes.Contains("droptarget");
            window.DragDrop(onRow, RawDragEventType.Drop, rowData, DragDropEffects.Copy);
            var rowAfter = folderRow.Classes.Contains("droptarget");

            // Assert: each file went where it was dropped, and the highlights lit up during the drag and went after the drop
            Assert.True(zoneLit);
            Assert.False(zoneAfter);
            Assert.True(rowLit);
            Assert.False(rowAfter);
            Assert.True(File.Exists(Path.Combine(_folder, "attachments", "logo.png")));
            Assert.True(File.Exists(Path.Combine(_folder, "assets", "chart.png")));
            Assert.True(File.Exists(first));
            Assert.Equal(string.Empty, viewModel.ErrorMessage);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }

        window.Close();
    }

    /// <summary>
    ///     Test that a drop of something that is not a file changes nothing, and that a file that is not a picture, a PDF, or a
    ///     drawing is refused with a message.
    /// </summary>
    [AvaloniaFact]
    public void ProjectExplorerView_DropTextOrOtherFile_ChangesNothingOrExplains()
    {
        // Arrange: a project and a text file outside it
        Touch("a.svg");
        var outside = Directory.CreateTempSubdirectory("canvaspilot-ui-drop-").FullName;
        var notes = Path.Combine(outside, "notes.txt");
        File.WriteAllText(notes, "text");
        var (window, viewModel) = ShowExplorer(project: true);
        var zone = SettingsUiHelpers.Find<Panel>(window, "ExplorerDropZone");
        var onZone = CenterOf(zone, window);

        try
        {
            // Act: drop text, and then drop the text file
            var text = new DataTransfer();
            var entry = new DataTransferItem();
            entry.SetText("hello");
            text.Add(entry);
            window.DragDrop(onZone, RawDragEventType.DragEnter, text, DragDropEffects.Copy);
            window.DragDrop(onZone, RawDragEventType.DragOver, text, DragDropEffects.Copy);
            var litByText = zone.Classes.Contains("drag-active");
            window.DragDrop(onZone, RawDragEventType.Drop, text, DragDropEffects.Copy);
            var afterText = (Directory.Exists(Path.Combine(_folder, "attachments")), viewModel.ErrorMessage);
            var fileData = FilesTransfer(window, notes);
            window.DragDrop(onZone, RawDragEventType.DragEnter, fileData, DragDropEffects.Copy);
            window.DragDrop(onZone, RawDragEventType.DragOver, fileData, DragDropEffects.Copy);
            window.DragDrop(onZone, RawDragEventType.Drop, fileData, DragDropEffects.Copy);

            // Assert: text lit nothing and added nothing; the text file was refused with a message and not copied
            Assert.False(litByText);
            Assert.Equal((false, string.Empty), afterText);
            Assert.Contains("'notes.txt' was not added", viewModel.ErrorMessage, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(_folder, "attachments", "notes.txt")));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }

        window.Close();
    }

    /// <summary>
    ///     Makes a data transfer that carries files, one item for each, as a file manager does.
    /// </summary>
    /// <param name="window">The window, which makes the storage items.</param>
    /// <param name="paths">The full paths of the files.</param>
    /// <returns>The data transfer.</returns>
    private static DataTransfer FilesTransfer(Window window, params string[] paths)
    {
        var data = new DataTransfer();
        foreach (var path in paths)
        {
            var item = window.StorageProvider.TryGetFileFromPathAsync(new Uri(path)).GetAwaiter().GetResult();
            var entry = new DataTransferItem();
            entry.SetFile(item!);
            data.Add(entry);
        }

        return data;
    }

    /// <summary>
    ///     Finds the middle of a control in the coordinates of a window.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <param name="window">The window.</param>
    /// <returns>The point.</returns>
    private static Point CenterOf(Control control, Window window)
    {
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
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

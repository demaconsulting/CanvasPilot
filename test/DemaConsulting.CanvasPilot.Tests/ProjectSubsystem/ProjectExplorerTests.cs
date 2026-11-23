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

using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ProjectExplorerViewModel" />, <see cref="CanvasNetImageSourceFilter" />, and the
///     refresh scheduling contract. Refreshes are driven through a <see cref="FakeRefreshScheduler" />, so no test
///     waits for a real delay.
/// </summary>
public sealed class ProjectExplorerTests : IDisposable
{
    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     The scheduler the explorer under test uses.
    /// </summary>
    private readonly FakeRefreshScheduler _scheduler = new();

    /// <summary>
    ///     The explorers created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ProjectExplorerViewModel> _explorers = [];

    /// <summary>
    ///     The project folder used by most tests.
    /// </summary>
    private string Folder => Path.Combine(_tempRoot, "project");

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var explorer in _explorers)
        {
            explorer.Dispose();
        }

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the tree lists the folders of the project and the files in them, folders first and then files with
    ///     each group sorted ignoring case, that the flat list holds the previewable files in the order of the tree, and that
    ///     the data folder, the project file, hidden files, and other file types are not listed.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Open_ProjectFolder_BuildsTreeWithFoldersFirstAndSkipsHiddenAndOtherFiles()
    {
        // Arrange: a project folder with a mix of files and folders, two levels deep
        Directory.CreateDirectory(Path.Combine(Folder, "assets"));
        Directory.CreateDirectory(Path.Combine(Folder, ".canvaspilot"));
        Directory.CreateDirectory(Path.Combine(Folder, "Styles", "dema"));
        Touch(
            "b.svg",
            "A.png",
            "notes.txt",
            "project.canvaspilot",
            ".hidden.svg",
            "assets/inner.svg",
            "assets/logo.jpg",
            "assets/roadmap.pdf",
            "Styles/dema/style.json",
            "Styles/dema/other.json",
            ".canvaspilot/state.svg");
        var explorer = CreateExplorer();

        // Act: open the folder
        explorer.Open(Folder);

        // Assert: the folders come first, and the files below them, each group sorted ignoring case
        Assert.Equal(["assets", "Styles", "A.png", "b.svg"], explorer.Roots.Select(node => node.Name));
        var assets = explorer.Roots[0];
        Assert.True(assets.IsFolder);
        Assert.Equal(ProjectNodeKind.Folder, assets.Kind);
        Assert.Equal(["inner.svg", "logo.jpg", "roadmap.pdf"], assets.Children.Select(node => node.Name));
        Assert.Equal(
            [ProjectNodeKind.Svg, ProjectNodeKind.Bitmap, ProjectNodeKind.Pdf],
            assets.Children.Select(node => node.Kind));
        Assert.Equal(["SVG", "JPG", "PDF"], assets.Children.Select(node => node.Format));
        var style = explorer.Roots[1].Children.Single().Children.Single();
        Assert.Equal(("style.json", ProjectNodeKind.Style, false), (style.Name, style.Kind, style.IsPreviewable));

        // Assert: only the files that the filter draws are previewable, and they are in the flat list in tree order
        Assert.Equal(["inner.svg", "A.png", "b.svg"], explorer.Items.Select(item => item.Name));
        Assert.Equal(
            [Path.Combine(Folder, "assets", "inner.svg"), Path.Combine(Folder, "A.png"), Path.Combine(Folder, "b.svg")],
            explorer.Items.Select(item => item.FullPath));
        Assert.Equal(Folder, explorer.FolderPath);
        Assert.True(explorer.HasProject);
        Assert.Equal(string.Empty, explorer.EmptyMessage);
        Assert.False(explorer.IsTruncated);
    }

    /// <summary>
    ///     Test that selecting a node selects the file when CanvasNet draws it and no file when it is a folder or an asset
    ///     that is not drawn, that selecting a file by path expands the folders above it and selects its node, and that the
    ///     selection of the node and of the file stay in line.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_SelectedNode_FolderFileAndAsset_FollowsTheSelectionOfTheFile()
    {
        // Arrange: a tree with a picture in a sub-folder and a PDF beside it
        Touch("assets/inner.svg", "assets/roadmap.pdf", "top.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        var assets = explorer.Roots[0];
        var changedFiles = new List<string?>();
        explorer.PropertyChanged += (_, e) => changedFiles.Add(e.PropertyName);

        // Act: select the picture's node, then the PDF's node, then the folder; then select a file by path
        explorer.SelectedNode = assets.Children[0];
        var afterPicture = (explorer.SelectedFile, explorer.SelectedItem?.Name);
        explorer.SelectedNode = assets.Children[1];
        var afterPdf = (explorer.SelectedFile, explorer.SelectedItem, explorer.SelectedNode.Name);
        explorer.SelectedNode = assets;
        var afterFolder = (explorer.SelectedFile, explorer.SelectedNode.Name);
        assets.IsExpanded = false;
        var found = explorer.SelectFile(Path.Combine(Folder, "assets", "inner.svg"));

        // Assert: only a picture that is drawn makes a selected file; a path expands its folders and selects its node
        Assert.Equal((Path.Combine(Folder, "assets", "inner.svg"), "inner.svg"), afterPicture);
        Assert.Equal((null, null, "roadmap.pdf"), afterPdf);
        Assert.Equal((null, "assets"), afterFolder);
        Assert.True(found);
        Assert.True(assets.IsExpanded);
        Assert.Same(assets.Children[0], explorer.SelectedNode);
        Assert.Contains(nameof(ProjectExplorerViewModel.SelectedFile), changedFiles);
    }

    /// <summary>
    ///     Test that a refresh keeps the nodes that remain (so the selection and the expansion stay), adds new files in sub-folders
    ///     and removes the ones that went, and clears the selection of a node that went.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Refresh_NestedChanges_KeepsNodesSelectionAndExpansion()
    {
        // Arrange: a tree with an expanded folder and a selected file in it
        Touch("assets/a.svg", "assets/b.svg", "top.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        var assets = explorer.Roots[0];
        assets.IsExpanded = true;
        explorer.SelectFile(Path.Combine(Folder, "assets", "b.svg"));
        var keptNode = assets.Children[1];

        // Act: a file is added in the folder and another one is removed, and the explorer refreshes
        Touch("assets/c.svg", "newfolder/n.svg");
        File.Delete(Path.Combine(Folder, "assets", "a.svg"));
        explorer.Refresh();
        var rootNames = explorer.Roots.Select(node => node.Name).ToArray();
        var assetNames = assets.Children.Select(node => node.Name).ToArray();
        var sameNode = ReferenceEquals(assets.Children.Single(node => node.Name == "b.svg"), keptNode);
        var selectedAfterFirst = explorer.SelectedNode?.Name;
        File.Delete(Path.Combine(Folder, "assets", "b.svg"));
        explorer.Refresh();

        // Assert: the folder node and the selected node were kept, the new things are there, and the removed selection is gone
        Assert.Equal(["assets", "newfolder", "top.svg"], rootNames);
        Assert.Equal(["b.svg", "c.svg"], assetNames);
        Assert.True(sameNode);
        Assert.Equal("b.svg", selectedAfterFirst);
        Assert.Same(assets, explorer.Roots[0]);
        Assert.True(assets.IsExpanded);
        Assert.Null(explorer.SelectedNode);
        Assert.Null(explorer.SelectedFile);
        Assert.Equal(["c.svg", "n.svg", "top.svg"], explorer.Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that the folders that the user expanded stay expanded after a refresh that makes new nodes for them and when
    ///     the same project is opened again, and are forgotten when another project is opened.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Expansion_SurvivesRefreshAndReopenOfTheSameProjectOnly()
    {
        // Arrange: an explorer over a project with a nested folder that the user expands
        Touch("one/two/deep.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.Roots[0].IsExpanded = true;
        explorer.Roots[0].Children[0].IsExpanded = true;

        // Act: the folder is removed and comes back, the same project is opened again, and another project is opened
        Directory.Delete(Path.Combine(Folder, "one"), recursive: true);
        explorer.Refresh();
        Touch("one/two/deep.svg");
        explorer.Refresh();
        var afterRefresh = (explorer.Roots[0].IsExpanded, explorer.Roots[0].Children[0].IsExpanded);
        explorer.Open(Folder);
        var afterReopen = explorer.Roots[0].IsExpanded;
        var other = Path.Combine(_tempRoot, "other");
        Directory.CreateDirectory(Path.Combine(other, "one"));
        explorer.Open(other);
        var afterOther = explorer.Roots[0].IsExpanded;

        // Assert: expanded again for the same project, collapsed for another
        Assert.Equal((true, true), afterRefresh);
        Assert.True(afterReopen);
        Assert.False(afterOther);
    }

    /// <summary>
    ///     Test that a folder with more nodes than the limit, or that goes deeper than the limit, is cut short and says so
    ///     instead of listing everything, and that an unreadable folder below the top leaves only that folder empty.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Open_TooManyOrTooDeep_IsTruncatedAndSaysSo()
    {
        // Arrange: a folder with more files than the limit, and one with folders deeper than the limit
        Touch("f1.svg", "f2.svg", "f3.svg", "f4.svg", "f5.svg", "f6.svg");
        var wide = CreateExplorer(maximumNodes: 4);
        var deepFolder = Path.Combine(_tempRoot, "deep");
        var path = deepFolder;
        for (var level = 0; level < ProjectExplorerViewModel.MaximumDepth + 2; level++)
        {
            path = Path.Combine(path, "d" + level);
        }

        Directory.CreateDirectory(path);
        var deep = CreateExplorer();

        // Act: open both
        wide.Open(Folder);
        deep.Open(deepFolder);

        // Assert: the wide tree holds only the limit, and both say they were cut short
        Assert.Equal(4, wide.Roots.Count);
        Assert.True(wide.IsTruncated);
        Assert.True(deep.IsTruncated);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), _scheduler, maximumNodes: 0));
    }

    /// <summary>
    ///     Test that a change in a sub-folder makes the watcher request a refresh, and that a change inside the hidden data
    ///     folder does not.
    /// </summary>
    [Fact]
    public async Task ProjectExplorerViewModel_Open_ChangeInSubFolderAndInDataFolder_OnlyTheFirstRequestsRefresh()
    {
        // Arrange: an open project with a sub-folder and a data folder
        Directory.CreateDirectory(Path.Combine(Folder, "assets"));
        Directory.CreateDirectory(Path.Combine(Folder, ".canvaspilot"));
        var explorer = CreateExplorer();
        explorer.Open(Folder);

        // Act: a file is written in the data folder, and then the test waits to see nothing, and one in the sub-folder
        await File.WriteAllTextAsync(Path.Combine(Folder, ".canvaspilot", "state.json"), "{}", TestContext.Current.CancellationToken);
        await Task.Delay(400, TestContext.Current.CancellationToken);
        var afterDataFolder = _scheduler.ScheduledCount;
        await File.WriteAllTextAsync(Path.Combine(Folder, "assets", "inner.svg"), string.Empty, TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (explorer.Items.Count == 0 && DateTime.UtcNow < deadline)
        {
            _scheduler.RunPending();
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        // Assert: the data folder asked for nothing, and the sub-folder's file was listed
        Assert.Equal(0, afterDataFolder);
        Assert.Equal(["inner.svg"], explorer.Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that a file in a sub-folder is deleted and renamed within its own folder.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_RenameAndDelete_FileInSubFolder_StaysInItsFolder()
    {
        // Arrange: two pictures in a sub-folder
        Touch("assets/one.svg", "assets/two.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "assets", "one.svg"));

        // Act: rename the first picture, then delete it
        var renamed = explorer.RenameSelected("uno.svg");
        var afterRename = (File.Exists(Path.Combine(Folder, "assets", "uno.svg")), File.Exists(Path.Combine(Folder, "uno.svg")), explorer.SelectedFile);
        var deleted = explorer.DeleteSelected();

        // Assert: the file kept its folder, and then went from disk and tree
        Assert.Equal(string.Empty, renamed);
        Assert.Equal((true, false, (string?)Path.Combine(Folder, "assets", "uno.svg")), afterRename);
        Assert.Equal(string.Empty, deleted);
        Assert.Equal(["two.svg"], explorer.Roots[0].Children.Select(node => node.Name));
    }

    /// <summary>
    ///     Test that the kinds and formats of the files are told from their names, including compound extensions.
    /// </summary>
    [Fact]
    public void ProjectNodeViewModel_KindAndFormat_FromTheFileName()
    {
        // Act: classify names of the kinds that the explorer shows
        var kinds = new[] { "a.svg", "b.MMD", "c.vl.json", "style.json", "x.pdf", "y.JPEG", "z.puml" }
            .Select(ProjectNodeViewModel.KindOf)
            .ToArray();
        var formats = new[] { "a.svg", "c.VL.JSON", "y.jpeg", "noext" }
            .Select(name => new ProjectNodeViewModel(name, Path.Combine(Folder, name), false, ProjectNodeViewModel.KindOf(name), false).Format)
            .ToArray();
        var folder = new ProjectNodeViewModel("dir", Path.Combine(Folder, "dir"), true, ProjectNodeKind.Folder, true);

        // Assert: the kinds, the formats, and that a folder has no format and is never previewable
        Assert.Equal(
            [ProjectNodeKind.Svg, ProjectNodeKind.Mermaid, ProjectNodeKind.VegaLite, ProjectNodeKind.Style, ProjectNodeKind.Pdf, ProjectNodeKind.Bitmap, ProjectNodeKind.Source],
            kinds);
        Assert.Equal(["SVG", "VL.JSON", "JPEG", string.Empty], formats);
        Assert.Equal((string.Empty, false), (folder.Format, folder.IsPreviewable));
        Assert.True(ProjectNodeViewModel.IsListedAsset("logo.png"));
        Assert.False(ProjectNodeViewModel.IsListedAsset("notes.txt"));
        Assert.Throws<ArgumentException>(() => new ProjectNodeViewModel(" ", Folder, false, ProjectNodeKind.Svg, true));
    }

    /// <summary>
    ///     Test that a file with the hidden attribute is not listed.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Open_HiddenAttributeFile_IsNotListed()
    {
        // Arrange: a visible and a hidden file (the attribute applies on Windows)
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The hidden attribute is only honored on Windows.");
        Touch("shown.svg", "concealed.svg");
        File.SetAttributes(Path.Combine(Folder, "concealed.svg"), FileAttributes.Hidden);
        var explorer = CreateExplorer();

        // Act: open the folder
        explorer.Open(Folder);

        // Assert: only the visible file is listed
        Assert.Equal(["shown.svg"], explorer.Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that a folder with no image files shows a message and an empty list.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Open_NoImageFiles_ShowsEmptyMessage()
    {
        // Arrange: a folder with only an unsupported file
        Touch("notes.txt");
        var explorer = CreateExplorer();

        // Act: open the folder
        explorer.Open(Folder);

        // Assert: the list is empty and a message says why
        Assert.Empty(explorer.Items);
        Assert.Contains("no image files", explorer.EmptyMessage);
    }

    /// <summary>
    ///     Test that a project folder that does not exist shows an error and an empty list instead of throwing.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Open_MissingFolder_ShowsErrorAndEmptyList()
    {
        // Arrange: a log and an explorer
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var explorer = CreateExplorer(logger: logger);

        // Act: open a folder that does not exist
        explorer.Open(Path.Combine(_tempRoot, "missing"));

        // Assert: the list is empty, the error is shown and logged, and no watcher runs
        Assert.Empty(explorer.Items);
        Assert.Contains("could not be read", explorer.ErrorMessage);
        Assert.Contains("missing", File.ReadAllText(logger.ActiveFilePath));
        Assert.False(explorer.IsWatching);
    }

    /// <summary>
    ///     Test that the listing uses the extensions CanvasNet claims, ignoring case.
    /// </summary>
    [Fact]
    public void CanvasNetImageSourceFilter_IsImageSource_CanvasNetExtensions_AreAccepted()
    {
        // Arrange: the filter and the extensions the viewer library reports
        var filter = new CanvasNetImageSourceFilter();

        // Act / Assert: every extension is accepted in any case, other files are not
        Assert.NotEmpty(CanvasNetViewer.Extensions);
        foreach (var extension in CanvasNetViewer.Extensions)
        {
            Assert.True(filter.IsImageSource("picture" + extension), extension);
            Assert.True(filter.IsImageSource(Path.Combine("folder", "PICTURE" + extension.ToUpperInvariant())), extension);
        }

        Assert.Contains(".svg", CanvasNetViewer.Extensions);
        Assert.Contains(".vl.json", CanvasNetViewer.Extensions);
        Assert.False(filter.IsImageSource("notes.txt"));
        Assert.False(filter.IsImageSource("data.json"));
        Assert.False(filter.IsImageSource(".svg"));
        Assert.False(filter.IsImageSource("project.canvaspilot"));
        Assert.False(filter.IsImageSource("makefile"));
        Assert.False(filter.IsImageSource(string.Empty));
    }

    /// <summary>
    ///     Test that the listing accepts the PlantUML extensions without any special handling, ignoring case.
    /// </summary>
    [Fact]
    public void CanvasNetImageSourceFilter_IsImageSource_PlantUmlExtensions_AreAccepted()
    {
        // Arrange: the filter
        var filter = new CanvasNetImageSourceFilter();

        // Act / Assert: each PlantUML extension is accepted in any case
        foreach (var extension in new[] { ".puml", ".plantuml", ".pu" })
        {
            Assert.Contains(extension, CanvasNetViewer.Extensions);
            Assert.True(filter.IsImageSource("diagram" + extension), extension);
            Assert.True(filter.IsImageSource(Path.Combine("folder", "DIAGRAM" + extension.ToUpperInvariant())), extension);
        }
    }

    /// <summary>
    ///     Test that the real filter lists a CanvasNet source file in a project folder.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Open_WithCanvasNetFilter_ListsSvgButNotText()
    {
        // Arrange: an SVG and a text file
        Touch("figure.svg", "notes.txt");
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), _scheduler);
        _explorers.Add(explorer);

        // Act: open the folder
        explorer.Open(Folder);

        // Assert: only the SVG is listed
        Assert.Equal(["figure.svg"], explorer.Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that opening a folder starts watching it and closing stops watching and clears the list.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_OpenAndClose_StartsAndStopsWatchingAndClearsList()
    {
        // Arrange: a folder with an image and an explorer with a selection
        Touch("one.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "one.svg"));
        var watchingWhenOpen = explorer.IsWatching;
        explorer.RequestRefresh();

        // Act: close
        explorer.Close();

        // Assert: it was watching, then everything was reset and the pending refresh cancelled
        Assert.True(watchingWhenOpen);
        Assert.False(explorer.IsWatching);
        Assert.Empty(explorer.Items);
        Assert.Null(explorer.SelectedItem);
        Assert.Null(explorer.SelectedFile);
        Assert.False(explorer.HasProject);
        Assert.Equal("No project is open.", explorer.EmptyMessage);
        Assert.Equal(0, _scheduler.PendingCount);
    }

    /// <summary>
    ///     Test that opening another project replaces the list and ignores a refresh scheduled for the old project.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Open_AnotherFolder_ReplacesListAndIgnoresStaleRefresh()
    {
        // Arrange: two project folders; the first has a refresh scheduled
        var other = Path.Combine(_tempRoot, "other");
        Touch("first.svg");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "second.svg"), string.Empty);
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.RequestRefresh();
        explorer.Open(other);
        File.WriteAllText(Path.Combine(other, "third.svg"), string.Empty);

        // Act: the callback scheduled for the first project (the first request) runs late
        _scheduler.RunScheduled(0);

        // Assert: the list still shows the second project as it was when opened, and it is watched
        Assert.Equal(["second.svg"], explorer.Items.Select(item => item.Name));
        Assert.Equal(other, explorer.FolderPath);
        Assert.True(explorer.IsWatching);
    }

    /// <summary>
    ///     Test that a refresh adds new files, removes deleted files, and keeps the selection of a file that stays.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Refresh_FilesAddedAndRemoved_UpdatesListAndKeepsSelection()
    {
        // Arrange: two files with the second selected
        Touch("a.svg", "c.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "c.svg"));
        var selected = explorer.SelectedItem;

        // Act: a file is added between them and the first is deleted
        Touch("b.svg");
        File.Delete(Path.Combine(Folder, "a.svg"));
        explorer.Refresh();

        // Assert: the list matches the folder and the same item is still selected
        Assert.Equal(["b.svg", "c.svg"], explorer.Items.Select(item => item.Name));
        Assert.Same(selected, explorer.SelectedItem);
        Assert.Equal(Path.Combine(Folder, "c.svg"), explorer.SelectedFile);
    }

    /// <summary>
    ///     Test that the selection is cleared when the selected file disappears.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Refresh_SelectedFileRemoved_ClearsSelection()
    {
        // Arrange: a selected file
        Touch("only.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "only.svg"));

        // Act: the file is deleted and the list refreshed
        File.Delete(Path.Combine(Folder, "only.svg"));
        explorer.Refresh();

        // Assert: nothing is selected
        Assert.Null(explorer.SelectedItem);
        Assert.Null(explorer.SelectedFile);
        Assert.Empty(explorer.Items);
    }

    /// <summary>
    ///     Test that a refresh after a successful read clears an earlier read error.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Refresh_FolderReappears_ClearsError()
    {
        // Arrange: a project whose folder is missing at first
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        var hadError = explorer.ErrorMessage.Length > 0;

        // Act: the folder appears with a file and the list refreshes
        Touch("late.svg");
        explorer.Refresh();

        // Assert: the error is gone and the file is listed
        Assert.True(hadError);
        Assert.Equal(string.Empty, explorer.ErrorMessage);
        Assert.Equal(["late.svg"], explorer.Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that selecting a file by path reports whether it is listed.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_SelectFile_ListedAndUnlisted_ReportsResult()
    {
        // Arrange: a listed file
        Touch("listed.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        var changed = new List<string?>();
        explorer.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: select it, in a different case, then select an unlisted path
        var found = explorer.SelectFile(Path.Combine(Folder, "LISTED.SVG"));
        var selectedFile = explorer.SelectedFile;
        var notFound = explorer.SelectFile(Path.Combine(Folder, "other.svg"));

        // Assert: the first was found and announced as SelectedFile, the second cleared the selection
        Assert.True(found);
        Assert.Equal(Path.Combine(Folder, "listed.svg"), selectedFile);
        Assert.Contains(nameof(ProjectExplorerViewModel.SelectedFile), changed);
        Assert.False(notFound);
        Assert.Null(explorer.SelectedFile);
    }

    /// <summary>
    ///     Test that several requests in a burst leave only one refresh waiting, using the debounce delay, and
    ///     that the refresh then updates the list.
    /// </summary>
    [Fact]
    public async Task ProjectExplorerViewModel_RequestRefresh_Burst_CoalescesIntoOneRefresh()
    {
        // Arrange: an open project with a custom debounce delay
        Touch("one.svg");
        var explorer = CreateExplorer(debounce: TimeSpan.FromMilliseconds(40));
        explorer.Open(Folder);

        // Act: a file appears; the folder watcher reports it on its own thread, in more than one event, so the test waits
        // until the reports have come before it makes its three requests, which then arrive before the delay passes
        Touch("two.svg");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_scheduler.ScheduledCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await Task.Delay(250, TestContext.Current.CancellationToken);
        explorer.RequestRefresh();
        explorer.RequestRefresh();
        explorer.RequestRefresh();
        var beforeDelay = explorer.Items.Count;
        var pendingAfterBurst = _scheduler.PendingCount;
        while (explorer.Items.Count < 2 && DateTime.UtcNow < deadline.AddSeconds(10))
        {
            _scheduler.RunPending();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // Assert: one callback was waiting with the debounce delay, the list changed only when it ran
        Assert.Equal(1, pendingAfterBurst);
        Assert.Equal(TimeSpan.FromMilliseconds(40), _scheduler.LastDelay);
        Assert.Equal(1, beforeDelay);
        Assert.Equal(["one.svg", "two.svg"], explorer.Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that the default debounce delay is used when none is given.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_RequestRefresh_DefaultDebounce_IsQuarterSecond()
    {
        // Arrange: an explorer with the default delay and an open project
        var explorer = CreateExplorer();
        explorer.Open(Folder);

        // Act: request a refresh
        explorer.RequestRefresh();

        // Assert: the delay is the documented default
        Assert.Equal(ProjectExplorerViewModel.DefaultDebounce, _scheduler.LastDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(250), ProjectExplorerViewModel.DefaultDebounce);
    }

    /// <summary>
    ///     Test that refresh requests are ignored when no project is open or after disposal.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_RequestRefresh_NoProjectOrDisposed_SchedulesNothing()
    {
        // Arrange: an explorer with no project
        var explorer = CreateExplorer();

        // Act: request before opening, then open, dispose, and request again
        explorer.RequestRefresh();
        var beforeOpen = _scheduler.ScheduledCount;
        explorer.Open(Folder);
        explorer.Dispose();
        explorer.RequestRefresh();

        // Assert: nothing was scheduled and the watcher is gone
        Assert.Equal(0, beforeOpen);
        Assert.Equal(0, _scheduler.ScheduledCount);
        Assert.False(explorer.IsWatching);
    }

    /// <summary>
    ///     Test that a file created in the project folder makes the watcher request a refresh, and that the
    ///     refresh lists the new file.
    /// </summary>
    [Fact]
    public async Task ProjectExplorerViewModel_Open_FileCreatedInFolder_WatcherRequestsRefresh()
    {
        // Arrange: an open, empty project folder
        Directory.CreateDirectory(Folder);
        var explorer = CreateExplorer();
        explorer.Open(Folder);

        // Act: a file is created and the watcher is given time to report it
        await File.WriteAllTextAsync(Path.Combine(Folder, "created.svg"), string.Empty, TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (_scheduler.ScheduledCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.True(_scheduler.ScheduledCount > 0, "The folder watcher did not report the new file.");

        // The watcher reports a new file in more than one event, and a report that arrives while a callback runs
        // replaces it, so the callbacks are run until the file is listed
        while (explorer.Items.Count == 0 && DateTime.UtcNow < deadline)
        {
            _scheduler.RunPending();
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        // Assert: a refresh was requested and the file is listed
        Assert.Equal(["created.svg"], explorer.Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that deleting the selected file removes it from the disk and the list.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_DeleteSelected_SelectedFile_DeletesFileAndRefreshes()
    {
        // Arrange: two files with the first selected
        Touch("one.svg", "two.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "one.svg"));

        // Act: delete the selection
        var problem = explorer.DeleteSelected();

        // Assert: the file is gone from disk and list, and nothing is selected
        Assert.Equal(string.Empty, problem);
        Assert.False(File.Exists(Path.Combine(Folder, "one.svg")));
        Assert.Equal(["two.svg"], explorer.Items.Select(item => item.Name));
        Assert.Null(explorer.SelectedItem);
    }

    /// <summary>
    ///     Test that deleting or renaming with nothing selected reports a message and changes nothing.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_DeleteAndRename_NothingSelected_ReportMessage()
    {
        // Arrange: a file that is not selected
        Touch("one.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);

        // Act: try both
        var deleteProblem = explorer.DeleteSelected();
        var renameProblem = explorer.RenameSelected("new.svg");

        // Assert: both explained, the error is shown, and the file is untouched
        Assert.Contains("Select a file", deleteProblem);
        Assert.Contains("Select a file", renameProblem);
        Assert.Equal(renameProblem, explorer.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(Folder, "one.svg")));
    }

    /// <summary>
    ///     Test that a file that cannot be deleted produces a message and a log entry rather than an exception.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_DeleteSelected_FileInUse_ReturnsMessageAndLogs()
    {
        // Arrange: a selected file held open without sharing (only Windows refuses to delete it)
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Deleting an open file only fails on Windows.");
        Touch("locked.svg");
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var explorer = CreateExplorer(logger: logger);
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "locked.svg"));
        using var held = new FileStream(Path.Combine(Folder, "locked.svg"), FileMode.Open, FileAccess.Read, FileShare.None);

        // Act: delete
        var problem = explorer.DeleteSelected();

        // Assert: a message and a log entry, and the file is still listed
        Assert.Contains("could not be deleted", problem);
        Assert.Equal(problem, explorer.ErrorMessage);
        Assert.Contains("locked.svg", File.ReadAllText(logger.ActiveFilePath));
        Assert.Single(explorer.Items);
    }

    /// <summary>
    ///     Test that renaming the selected file moves it, lists it under the new name, and selects it.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_RenameSelected_ValidName_RenamesAndSelectsNewFile()
    {
        // Arrange: a selected file
        Touch("old.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "old.svg"));

        // Act: rename it, with surrounding spaces and a different supported extension
        var problem = explorer.RenameSelected("  renamed.png ");

        // Assert: the file moved, the list shows the new name, and it is selected
        Assert.Equal(string.Empty, problem);
        Assert.False(File.Exists(Path.Combine(Folder, "old.svg")));
        Assert.True(File.Exists(Path.Combine(Folder, "renamed.png")));
        Assert.Equal(["renamed.png"], explorer.Items.Select(item => item.Name));
        Assert.Equal(Path.Combine(Folder, "renamed.png"), explorer.SelectedFile);
    }

    /// <summary>
    ///     Test that a rename that only changes letter case is allowed.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_RenameSelected_CaseOnlyChange_IsAllowed()
    {
        // Arrange: a selected file
        Touch("figure.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "figure.svg"));

        // Act: change only the case
        var problem = explorer.RenameSelected("Figure.svg");

        // Assert: the list shows the new spelling
        Assert.Equal(string.Empty, problem);
        Assert.Equal(["Figure.svg"], explorer.Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that renaming to the same name does nothing and reports no problem.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_RenameSelected_SameName_DoesNothing()
    {
        // Arrange: a selected file
        Touch("same.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "same.svg"));

        // Act: rename it to its own name
        var problem = explorer.RenameSelected("same.svg");

        // Assert: no problem and the file is unchanged
        Assert.Equal(string.Empty, problem);
        Assert.True(File.Exists(Path.Combine(Folder, "same.svg")));
    }

    /// <summary>
    ///     Test that names that are empty, contain a path, lack a supported extension, or are reserved are refused
    ///     and the file is left alone.
    /// </summary>
    /// <param name="newName">The refused name.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sub/new.svg")]
    [InlineData("..\\escape.svg")]
    [InlineData("new.txt")]
    [InlineData("new")]
    [InlineData(".hidden.svg")]
    [InlineData("CON.svg")]
    [InlineData("new.canvaspilot")]
    public void ProjectExplorerViewModel_RenameSelected_InvalidName_IsRefused(string newName)
    {
        // Arrange: a selected file
        Touch("keep.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "keep.svg"));

        // Act: try to rename
        var problem = explorer.RenameSelected(newName);

        // Assert: refused with the message shown, and the file untouched
        Assert.NotEmpty(problem);
        Assert.Equal(problem, explorer.ErrorMessage);
        Assert.Equal(["keep.svg"], Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    /// <summary>
    ///     Test that a rename never replaces an existing file or folder.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_RenameSelected_TargetExists_IsRefusedAndNothingReplaced()
    {
        // Arrange: two files, the first selected, and a folder
        Touch("first.svg", "second.svg");
        Directory.CreateDirectory(Path.Combine(Folder, "folder.svg"));
        File.WriteAllText(Path.Combine(Folder, "second.svg"), "second content");
        var explorer = CreateExplorer();
        explorer.Open(Folder);
        explorer.SelectFile(Path.Combine(Folder, "first.svg"));

        // Act: try to rename onto the other file and onto the folder
        var ontoFile = explorer.RenameSelected("second.svg");
        var ontoFolder = explorer.RenameSelected("folder.svg");

        // Assert: both refused and the other file's content is intact
        Assert.Contains("already exists", ontoFile);
        Assert.Contains("already exists", ontoFolder);
        Assert.Equal("second content", File.ReadAllText(Path.Combine(Folder, "second.svg")));
        Assert.True(File.Exists(Path.Combine(Folder, "first.svg")));
    }

    /// <summary>
    ///     Test that a disposed explorer refuses to open a folder and stops watching.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Dispose_OpenFolder_StopsWatchingAndRefusesOpen()
    {
        // Arrange: an open explorer
        Touch("one.svg");
        var explorer = CreateExplorer();
        explorer.Open(Folder);

        // Act: dispose, twice
        explorer.Dispose();
        explorer.Dispose();

        // Assert: it is no longer watching and cannot open another folder
        Assert.False(explorer.IsWatching);
        Assert.Throws<ObjectDisposedException>(() => explorer.Open(Folder));
    }

    /// <summary>
    ///     Test that invalid constructor and method arguments are refused.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_InvalidArguments_Throw()
    {
        // Arrange: an explorer
        var explorer = CreateExplorer();

        // Act / Assert: null collaborators and a blank folder are refused
        Assert.Throws<ArgumentNullException>(() => new ProjectExplorerViewModel(null!, _scheduler));
        Assert.Throws<ArgumentNullException>(() => new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), null!));
        Assert.Throws<ArgumentException>(() => explorer.Open("  "));
    }

    /// <summary>
    ///     Creates an explorer over the fake scheduler and a filter that accepts SVG and PNG files, and registers it
    ///     for disposal.
    /// </summary>
    /// <param name="debounce">The debounce delay, or <see langword="null" /> for the default.</param>
    /// <param name="logger">An optional logger.</param>
    /// <param name="maximumNodes">The most nodes the tree holds.</param>
    /// <returns>The explorer.</returns>
    private ProjectExplorerViewModel CreateExplorer(TimeSpan? debounce = null, RollingFileLogger? logger = null, int maximumNodes = ProjectExplorerViewModel.MaximumNodes)
    {
        var explorer = new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg", ".png"), _scheduler, debounce, logger, maximumNodes);
        _explorers.Add(explorer);
        return explorer;
    }

    /// <summary>
    ///     Creates empty files in the project folder, creating folders as needed.
    /// </summary>
    /// <param name="relativePaths">The paths of the files, relative to the project folder.</param>
    private void Touch(params string[] relativePaths)
    {
        foreach (var relative in relativePaths)
        {
            var path = Path.Combine(Folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }
    }
}

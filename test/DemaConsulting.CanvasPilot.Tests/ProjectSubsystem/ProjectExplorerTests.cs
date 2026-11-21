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
    ///     Test that only image source files in the root of the project folder are listed, sorted by name, and
    ///     that sub-folders, the data folder, the project file, hidden files, and other file types are not.
    /// </summary>
    [Fact]
    public void ProjectExplorerViewModel_Open_ProjectFolder_ListsOnlyRootImageSourceFilesSorted()
    {
        // Arrange: a project folder with a mix of files and folders
        Directory.CreateDirectory(Path.Combine(Folder, "assets"));
        Directory.CreateDirectory(Path.Combine(Folder, ".canvaspilot"));
        Touch("b.svg", "A.png", "notes.txt", "project.canvaspilot", ".hidden.svg", "assets/inner.svg", ".canvaspilot/state.svg");
        var explorer = CreateExplorer();

        // Act: open the folder
        explorer.Open(Folder);

        // Assert: only the root image files are listed, sorted ignoring case
        Assert.Equal(["A.png", "b.svg"], explorer.Items.Select(item => item.Name));
        Assert.Equal(
            [Path.Combine(Folder, "A.png"), Path.Combine(Folder, "b.svg")],
            explorer.Items.Select(item => item.FullPath));
        Assert.Equal(Folder, explorer.FolderPath);
        Assert.True(explorer.HasProject);
        Assert.Equal(string.Empty, explorer.EmptyMessage);
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
    /// <returns>The explorer.</returns>
    private ProjectExplorerViewModel CreateExplorer(TimeSpan? debounce = null, RollingFileLogger? logger = null)
    {
        var explorer = new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg", ".png"), _scheduler, debounce, logger);
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

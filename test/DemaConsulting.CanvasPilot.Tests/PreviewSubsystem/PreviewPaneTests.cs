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

using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the <see cref="PreviewPaneViewModel" /> unit with a real project explorer on temporary folders,
///     a fake render service whose results the test completes, and fake schedulers so no test waits for a delay.
/// </summary>
public sealed class PreviewPaneTests : IDisposable
{
    /// <summary>
    ///     Temporary project folder removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-preview-tests-").FullName;

    /// <summary>
    ///     The render service whose results the tests control.
    /// </summary>
    private readonly FakeImageRenderService _renderer = new();

    /// <summary>
    ///     The scheduler of the explorer.
    /// </summary>
    private readonly FakeRefreshScheduler _explorerScheduler = new();

    /// <summary>
    ///     The scheduler of the preview pane.
    /// </summary>
    private readonly FakeRefreshScheduler _previewScheduler = new();

    /// <summary>
    ///     The explorer the pane follows.
    /// </summary>
    private readonly ProjectExplorerViewModel _explorer;

    /// <summary>
    ///     The view models created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<IDisposable> _disposables = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreviewPaneTests" /> class with an explorer over a project
    ///     folder that holds three SVG files.
    /// </summary>
    public PreviewPaneTests()
    {
        foreach (var name in new[] { "a.svg", "b.svg", "c.svg" })
        {
            File.WriteAllText(Path.Combine(_folder, name), "<svg/>");
        }

        _explorer = new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), _explorerScheduler);
        _disposables.Add(_explorer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the pane gives the folder of the open project through the picture source, and none while no project
    ///     is open, so that a part that draws the shown file again at another size knows which folder the file may read.
    /// </summary>
    [Fact]
    public void PreviewPaneViewModel_ProjectFolder_FollowsTheExplorer()
    {
        // Arrange: a pane over an explorer with no project
        var pane = CreatePane();
        IRenderedImageSource source = pane;
        var before = source.ProjectFolder;

        // Act: open the project and close it again
        _explorer.Open(_folder);
        var whileOpen = source.ProjectFolder;
        _explorer.Close();
        var after = source.ProjectFolder;

        // Assert
        Assert.Null(before);
        Assert.Equal(_folder, whileOpen);
        Assert.Null(after);
    }

    /// <summary>
    ///     Test that selecting a file starts a render of that file in the project folder, shows that it is busy, and
    ///     shows the picture, its size, and its notes when the render finishes.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_SelectFile_RenderSucceeds_ShowsBusyThenImageStatusAndNotes()
    {
        // Arrange: a pane over an open project, with a handler that counts picture changes
        var pane = CreatePane();
        _explorer.Open(_folder);
        var imageChanges = 0;
        pane.CurrentImageChanged += (_, _) => imageChanges++;
        var note = new RenderDiagnostic(RenderDiagnosticSeverity.Warning, "image-access-denied", "not drawn");

        // Act: select a file and, later, finish its render
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        var busyText = pane.StatusText;
        var wasBusy = pane.IsBusy;
        var request = Assert.Single(_renderer.Requests);
        request.Succeed(8, 4, note);
        await pane.RenderCompletion;

        // Assert: the render was for the selected file and project; the pane showed busy, then the picture
        Assert.Equal(Path.Combine(_folder, "a.svg"), request.FilePath);
        Assert.Equal(_explorer.FolderPath, request.ProjectFolder);
        Assert.True(wasBusy);
        Assert.Equal("Rendering a.svg...", busyText);
        Assert.False(pane.IsBusy);
        Assert.True(pane.HasImage);
        Assert.Equal(8, pane.Image!.Width);
        Assert.Same(pane.Image, pane.CurrentImage);
        Assert.False(pane.HasError);
        Assert.False(pane.ShowPlaceholder);
        Assert.Equal([note], pane.Diagnostics);
        Assert.True(pane.HasDiagnostics);
        Assert.Equal("1 note", pane.DiagnosticsHeader);
        Assert.Equal("a.svg: 8 x 4 px, svg, 1 note", pane.StatusText);
        Assert.True(imageChanges >= 1);
    }

    /// <summary>
    ///     Test that a pane created when a file is already selected renders it at once.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_New_FileAlreadySelected_StartsRender()
    {
        // Arrange: a project with a selected file before the pane exists
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "b.svg"));

        // Act: create the pane
        var pane = CreatePane();
        var request = Assert.Single(_renderer.Requests);
        request.Succeed();
        await pane.RenderCompletion;

        // Assert: it rendered the selected file
        Assert.Equal(Path.Combine(_folder, "b.svg"), request.FilePath);
        Assert.True(pane.HasImage);
    }

    /// <summary>
    ///     Test that selecting another file cancels the render that is still running and that a result of the
    ///     cancelled render never replaces the newer picture, whichever result arrives first.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreviewPaneViewModel_SelectAnotherFile_CancelsRunningRenderAndIgnoresStaleResult(bool staleResultArrivesFirst)
    {
        // Arrange: a pane with a render of the first file still running
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        var stale = _renderer.Requests[0];

        // Act: select another file and finish both renders in the chosen order
        _explorer.SelectFile(Path.Combine(_folder, "b.svg"));
        var fresh = _renderer.Requests[1];
        var staleWasCancelled = stale.Token.IsCancellationRequested;
        var freshWasCancelled = fresh.Token.IsCancellationRequested;
        if (staleResultArrivesFirst)
        {
            stale.Succeed(99, 99);
            fresh.Succeed(6, 3);
        }
        else
        {
            fresh.Succeed(6, 3);
            stale.Succeed(99, 99);
        }

        await pane.RenderCompletion;

        // Assert: the first render was cancelled, the second was not, and only the second picture is shown
        Assert.True(staleWasCancelled);
        Assert.False(freshWasCancelled);
        Assert.Equal(6, pane.Image!.Width);
        Assert.Equal(Path.Combine(_folder, "b.svg"), pane.Image.SourcePath);
        Assert.Equal("b.svg: 6 x 3 px, svg", pane.StatusText);
        Assert.False(pane.IsBusy);
    }

    /// <summary>
    ///     Test that a stale error does not replace a newer picture either.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_SelectAnotherFile_StaleErrorIsIgnored()
    {
        // Arrange: two renders where the first will fail late
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _explorer.SelectFile(Path.Combine(_folder, "b.svg"));

        // Act: the newer render succeeds, then the older one reports an error
        _renderer.Requests[1].Succeed();
        _renderer.Requests[0].Fail("cannot-draw", "late failure");
        await pane.RenderCompletion;

        // Assert: the picture is kept and no error shows
        Assert.True(pane.HasImage);
        Assert.False(pane.HasError);
    }

    /// <summary>
    ///     Test that a render error shows its code, message, and hint, removes the picture, and that the
    ///     picture is back after the next successful render.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_RenderFails_ShowsCodeMessageAndHintAndClearsImage()
    {
        // Arrange: a pane that shows a picture of the first file
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _renderer.Requests[0].Succeed();
        await pane.RenderCompletion;

        // Act: select a file whose render fails with a hint
        _explorer.SelectFile(Path.Combine(_folder, "b.svg"));
        _renderer.Requests[1].Fail("scad-error", "Line 3: unexpected token", "Close the bracket.");
        await pane.RenderCompletion;

        // Assert: the error and its hint are shown, there is no picture to export, and the status says so
        Assert.True(pane.HasError);
        Assert.Equal("Render failed (scad-error)", pane.ErrorTitle);
        Assert.Equal("Line 3: unexpected token", pane.ErrorMessage);
        Assert.Equal("Hint: Close the bracket.", pane.ErrorHint);
        Assert.True(pane.HasErrorHint);
        Assert.False(pane.HasImage);
        Assert.Null(pane.CurrentImage);
        Assert.Empty(pane.Diagnostics);
        Assert.Equal("b.svg: render failed", pane.StatusText);
        Assert.False(pane.ShowPlaceholder);

        // Act: select a file that renders
        _explorer.SelectFile(Path.Combine(_folder, "c.svg"));
        _renderer.Requests[2].Succeed();
        await pane.RenderCompletion;

        // Assert: the error is gone and the picture is shown
        Assert.False(pane.HasError);
        Assert.True(pane.HasImage);
    }

    /// <summary>
    ///     Test that an error without a hint shows no hint.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_RenderFailsWithoutHint_ShowsNoHint()
    {
        // Arrange / Act: a failed render without a hint
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _renderer.Requests[0].Fail("operation-failed", "went wrong");
        await pane.RenderCompletion;

        // Assert: the error shows and the hint does not
        Assert.True(pane.HasError);
        Assert.False(pane.HasErrorHint);
        Assert.Equal(string.Empty, pane.ErrorHint);
    }

    /// <summary>
    ///     Test that a render service that throws, which it should not, shows an error and does not leave the pane
    ///     busy.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_RenderServiceThrows_ShowsOperationFailedAndIsNotBusy()
    {
        // Arrange: a service that will throw
        var pane = CreatePane();
        _explorer.Open(_folder);
        _renderer.ThrowOnNextRender = new InvalidOperationException("defect");

        // Act: select a file
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        await pane.RenderCompletion;

        // Assert: an error result was shown
        Assert.False(pane.IsBusy);
        Assert.Equal(RenderErrorCodes.OperationFailed, pane.Error?.Code);
        Assert.Equal("defect", pane.ErrorMessage);
    }

    /// <summary>
    ///     Test that a cancelled result of the current render leaves nothing busy and shows nothing.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_CurrentRenderReportsCancelled_IsNotBusyAndShowsNothing()
    {
        // Arrange / Act: a render that reports being cancelled although nothing cancelled it
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _renderer.Requests[0].Cancel();
        await pane.RenderCompletion;

        // Assert: idle, with neither picture nor error
        Assert.False(pane.IsBusy);
        Assert.False(pane.HasImage);
        Assert.False(pane.HasError);
        Assert.True(pane.ShowPlaceholder);
    }

    /// <summary>
    ///     Test that clearing the selection cancels the running render and empties the pane.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_SelectionCleared_CancelsRenderAndClearsPane()
    {
        // Arrange: a pane that shows a picture, then a new render that is running
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _renderer.Requests[0].Succeed();
        await pane.RenderCompletion;
        _explorer.SelectFile(Path.Combine(_folder, "b.svg"));
        var running = _renderer.Requests[1];

        // Act: clear the selection
        _explorer.SelectedItem = null;
        running.Succeed();
        await pane.RenderCompletion;

        // Assert: the running render was cancelled, its result ignored, and the pane is empty
        Assert.True(running.Token.IsCancellationRequested);
        Assert.False(pane.HasImage);
        Assert.False(pane.IsBusy);
        Assert.Null(pane.CurrentFile);
        Assert.True(pane.ShowPlaceholder);
        Assert.Equal(string.Empty, pane.StatusText);
        Assert.Equal("Select an image file to preview it.", pane.PlaceholderText);
    }

    /// <summary>
    ///     Test that closing or replacing the project cancels the running render and empties the pane.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_ProjectClosed_CancelsRenderAndClearsPane()
    {
        // Arrange: a pane with a running render
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        var running = _renderer.Requests[0];

        // Act: close the project
        _explorer.Close();
        running.Succeed();
        await pane.RenderCompletion;

        // Assert: cancelled, ignored, empty, and the placeholder explains there is no project
        Assert.True(running.Token.IsCancellationRequested);
        Assert.False(pane.HasImage);
        Assert.False(pane.IsBusy);
        Assert.Equal("No project is open.", pane.PlaceholderText);
    }

    /// <summary>
    ///     Test that disposing cancels the running render, ignores its result, stops watching, and may be repeated.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_Dispose_CancelsRenderIgnoresResultAndIsRepeatable()
    {
        // Arrange: a pane with a running render and a request waiting for its delay
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        var running = _renderer.Requests[0];
        pane.RequestRerender();

        // Act: dispose, finish the render, then try to use the disposed pane
        pane.Dispose();
        pane.Dispose();
        running.Succeed();
        await pane.RenderCompletion;
        pane.RequestRerender();
        _explorer.SelectFile(Path.Combine(_folder, "b.svg"));

        // Assert: cancelled, nothing was shown, nothing is waiting, and no further render started
        Assert.True(running.Token.IsCancellationRequested);
        Assert.False(pane.HasImage);
        Assert.Equal(0, _previewScheduler.PendingCount);
        Assert.Single(_renderer.Requests);
    }

    /// <summary>
    ///     Test that several requests for a new render cause one render after the debounce delay, and that the old
    ///     picture stays visible while the new one is rendered.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_RequestRerender_Burst_CoalescesIntoOneRenderAndKeepsPicture()
    {
        // Arrange: a pane that shows a picture
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _renderer.Requests[0].Succeed(5, 5);
        await pane.RenderCompletion;

        // Act: several requests in a burst, then the delay passes
        pane.RequestRerender();
        pane.RequestRerender();
        pane.RequestRerender();
        var pendingBeforeDelay = _previewScheduler.PendingCount;
        _previewScheduler.RunPending();
        var stillShowsOldPicture = pane.HasImage && pane.IsBusy;
        _renderer.Requests[1].Succeed(7, 7);
        await pane.RenderCompletion;

        // Assert: one render was waiting, one new render ran with the debounce delay, and the new picture replaced the old
        Assert.Equal(1, pendingBeforeDelay);
        Assert.Equal(PreviewPaneViewModel.DefaultDebounce, _previewScheduler.LastDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(400), PreviewPaneViewModel.DefaultDebounce);
        Assert.True(stillShowsOldPicture);
        Assert.Equal(2, _renderer.Requests.Count);
        Assert.Equal(7, pane.Image!.Width);
    }

    /// <summary>
    ///     Test that changing the selection discards a render that was waiting for its delay, and that a callback
    ///     that was already queued when that happened does nothing.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_SelectionChangesWhileRerenderPending_DiscardsPendingRender()
    {
        // Arrange: a pane with a shown picture and a re-render waiting for its delay
        var pane = CreatePane();
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _renderer.Requests[0].Succeed();
        await pane.RenderCompletion;
        pane.RequestRerender();

        // Act: select another file, then let the queued callback run anyway
        _explorer.SelectFile(Path.Combine(_folder, "b.svg"));
        var pendingAfterSelection = _previewScheduler.PendingCount;
        _previewScheduler.RunScheduled(0);

        // Assert: the waiting render was cancelled and the late callback started no render
        Assert.Equal(0, pendingAfterSelection);
        Assert.Equal(2, _renderer.Requests.Count);
    }

    /// <summary>
    ///     Test that a file that changes on disk makes the pane render again, using the real folder watcher, for a
    ///     change in the project folder and for a change in a sub-folder where assets live.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_FileChangesOnDisk_WatcherRequestsRerender()
    {
        // Arrange: a pane that shows a picture of a file, with a sub-folder of assets
        var pane = CreatePane();
        Directory.CreateDirectory(Path.Combine(_folder, "assets"));
        _explorer.Open(_folder);
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        _renderer.Requests[0].Succeed();
        await pane.RenderCompletion;

        // Act: the selected file changes and the watcher is given time to report it
        await File.WriteAllTextAsync(Path.Combine(_folder, "a.svg"), "<svg width='5' height='5'/>", TestContext.Current.CancellationToken);
        await WaitForScheduledAsync(1);
        var afterFileChange = _previewScheduler.ScheduledCount;
        _previewScheduler.RunPending();

        // Act: an asset in a sub-folder changes
        await File.WriteAllTextAsync(Path.Combine(_folder, "assets", "logo.svg"), "<svg/>", TestContext.Current.CancellationToken);
        await WaitForScheduledAsync(afterFileChange + 1);
        _previewScheduler.RunPending();

        // Assert: both changes caused a new render of the selected file
        Assert.True(afterFileChange > 0, "The folder watcher did not report the changed file.");
        Assert.True(_renderer.Requests.Count >= 3, "The changes did not cause new renders.");
        Assert.All(_renderer.Requests, request => Assert.Equal(Path.Combine(_folder, "a.svg"), request.FilePath));
    }

    /// <summary>
    ///     Test that file system changes while nothing is selected request no render.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_FileChangesWithoutSelection_RequestsNoRender()
    {
        // Arrange: a pane with an open project and no selected file
        CreatePane();
        _explorer.Open(_folder);

        // Act: a file changes; wait until the explorer's watcher on the same folder has reported the change
        await File.WriteAllTextAsync(Path.Combine(_folder, "a.svg"), "<svg/>", TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_explorerScheduler.ScheduledCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // Assert: nothing was scheduled and nothing was rendered
        Assert.Equal(0, _previewScheduler.ScheduledCount);
        Assert.Empty(_renderer.Requests);
    }

    /// <summary>
    ///     Test which changes in the project folder can affect a picture: everything except the hidden data folder
    ///     at the root and the project file at the root.
    /// </summary>
    [Theory]
    [InlineData("a.svg", true)]
    [InlineData("assets", true)]
    [InlineData("assets/logo.svg", true)]
    [InlineData("assets/.canvaspilot/x.svg", true)]
    [InlineData("sub/Brochure.canvaspilot", true)]
    [InlineData(".canvaspilot", false)]
    [InlineData(".canvaspilot/history.json", false)]
    [InlineData(".canvaspilot/deep/er/file.txt", false)]
    [InlineData("Brochure.canvaspilot", false)]
    [InlineData("BROCHURE.CANVASPILOT", false)]
    [InlineData("..other/a.svg", true)]
    public void PreviewPaneViewModel_IsRelevantChange_ProjectPaths_IgnoresDataFolderAndProjectFile(string relativePath, bool expected)
    {
        // Arrange: a project folder and a changed path inside it
        var project = Path.Combine(Path.GetTempPath(), "project");
        var changed = Path.Combine(project, relativePath.Replace('/', Path.DirectorySeparatorChar));

        // Act / Assert: the decision matches
        Assert.Equal(expected, PreviewPaneViewModel.IsRelevantChange(project, changed));
    }

    /// <summary>
    ///     Test that a path outside the project folder is treated as relevant, so an unexpected event is never lost.
    /// </summary>
    [Fact]
    public void PreviewPaneViewModel_IsRelevantChange_PathOutsideProject_IsRelevant()
    {
        // Act / Assert: a sibling folder and invalid arguments
        var project = Path.Combine(Path.GetTempPath(), "project");
        Assert.True(PreviewPaneViewModel.IsRelevantChange(project, Path.Combine(Path.GetTempPath(), "other", "a.svg")));
        Assert.Throws<ArgumentException>(() => PreviewPaneViewModel.IsRelevantChange(" ", "a"));
        Assert.Throws<ArgumentException>(() => PreviewPaneViewModel.IsRelevantChange(project, ""));
    }

    /// <summary>
    ///     Test the defaults of the pane: it fits the window and, with no project, explains that none is open.
    /// </summary>
    [Fact]
    public void PreviewPaneViewModel_New_NoProject_ShowsPlaceholderAndFitsWindow()
    {
        // Arrange / Act: a pane over an explorer with no project
        var pane = CreatePane();

        // Assert: idle, with the placeholder and the default fit
        Assert.True(pane.FitToWindow);
        Assert.False(pane.IsBusy);
        Assert.True(pane.ShowPlaceholder);
        Assert.Equal("No project is open.", pane.PlaceholderText);
        Assert.Equal(string.Empty, pane.StatusText);
        Assert.False(pane.HasDiagnostics);
        Assert.Equal("0 notes", pane.DiagnosticsHeader);
        Assert.Null(pane.CurrentImage);
    }

    /// <summary>
    ///     Test that content assigned to the overlay slot is exposed and announced, that the pane does not interpret
    ///     it, and that clearing the slot is announced too.
    /// </summary>
    [Fact]
    public void PreviewPaneViewModel_Overlay_AssignedContent_IsExposedAndAnnounced()
    {
        // Arrange: a pane with an empty overlay slot that records which properties change
        var pane = CreatePane();
        var changed = new List<string?>();
        pane.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var emptyBefore = (pane.Overlay, pane.HasOverlay);
        var content = new object();

        // Act: assign content, then clear it
        pane.Overlay = content;
        var assigned = (pane.Overlay, pane.HasOverlay);
        pane.Overlay = null;

        // Assert: the slot starts empty, holds exactly what was assigned, and announces both changes
        Assert.Equal((null, false), emptyBefore);
        Assert.Same(content, assigned.Overlay);
        Assert.True(assigned.HasOverlay);
        Assert.False(pane.HasOverlay);
        Assert.Equal(2, changed.Count(name => name == nameof(PreviewPaneViewModel.Overlay)));
        Assert.Equal(2, changed.Count(name => name == nameof(PreviewPaneViewModel.HasOverlay)));
    }

    /// <summary>
    ///     Test that content assigned to the slot in the top bar is exposed and announced like the overlay, and that
    ///     clearing the slot is announced too.
    /// </summary>
    [Fact]
    public void PreviewPaneViewModel_Toolbar_AssignedContent_IsExposedAndAnnounced()
    {
        // Arrange: a pane with an empty slot in the top bar that records which properties change
        var pane = CreatePane();
        var changed = new List<string?>();
        pane.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var emptyBefore = (pane.Toolbar, pane.HasToolbar);
        var content = new object();

        // Act: assign content, then clear it
        pane.Toolbar = content;
        var assigned = (pane.Toolbar, pane.HasToolbar);
        pane.Toolbar = null;

        // Assert: the slot starts empty, holds exactly what was assigned, is independent of the overlay, and announces both changes
        Assert.Equal((null, false), emptyBefore);
        Assert.Same(content, assigned.Toolbar);
        Assert.True(assigned.HasToolbar);
        Assert.False(pane.HasToolbar);
        Assert.False(pane.HasOverlay);
        Assert.Equal(2, changed.Count(name => name == nameof(PreviewPaneViewModel.Toolbar)));
        Assert.Equal(2, changed.Count(name => name == nameof(PreviewPaneViewModel.HasToolbar)));
    }

    /// <summary>
    ///     Test that the pane prepares the shown picture for dragging only while a picture is shown and dragging out is
    ///     turned on, and only when it has a drag source.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_PrepareDragFile_PictureShown_WritesPngOnlyWhenAllowed()
    {
        // Arrange: a pane with a drag source over a temporary folder, and one without a drag source
        var dragFolder = Path.Combine(_folder, "drag-out");
        var withSource = new PreviewPaneViewModel(_renderer, _explorer, _previewScheduler, dragSource: new ImageDragSource(_renderer, dragFolder));
        _disposables.Insert(0, withSource);
        var withoutSource = CreatePane();
        _explorer.Open(_folder);

        // Act: ask with no picture, then with a picture, then with dragging out turned off, then again turned on
        var noPicture = withSource.PrepareDragFile();
        _explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        foreach (var request in _renderer.Requests)
        {
            request.Succeed(8, 4);
        }

        await withSource.RenderCompletion;
        await withoutSource.RenderCompletion;
        var shown = withSource.PrepareDragFile();
        withSource.IsDragOutEnabled = false;
        var disabled = withSource.PrepareDragFile();
        withSource.IsDragOutEnabled = true;
        var enabledAgain = withSource.PrepareDragFile();
        var noSource = withoutSource.PrepareDragFile();

        // Assert: only the enabled pane that shows a picture and has a drag source gives the same existing PNG file
        Assert.Null(noPicture);
        Assert.NotNull(shown);
        Assert.Equal(Path.Combine(dragFolder, $"a-{withSource.Image!.ContentHash[..8]}.png"), shown);
        Assert.True(File.Exists(shown));
        Assert.Null(disabled);
        Assert.Equal(shown, enabledAgain);
        Assert.Null(noSource);
        Assert.True(withSource.IsDragOutEnabled);
        Assert.NotNull(withSource.DragSource);
        Assert.Null(withoutSource.DragSource);
    }

    /// <summary>
    ///     Test that a failure of the drag in the view is written to the log, and a missing failure is refused.
    /// </summary>
    [Fact]
    public async Task PreviewPaneViewModel_ReportDragProblem_Failure_IsLogged()
    {
        // Arrange: a pane with a logger
        var logger = new RollingFileLogger(Path.Combine(_folder, "logs"));
        var pane = new PreviewPaneViewModel(_renderer, _explorer, _previewScheduler, logger: logger);
        _disposables.Insert(0, pane);

        // Act: report a failure
        pane.ReportDragProblem(new InvalidOperationException("drop target refused"));

        // Assert: the log names the problem and the failure; a missing failure is refused
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("Dragging the picture out of the preview failed", log, StringComparison.Ordinal);
        Assert.Contains("drop target refused", log, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => pane.ReportDragProblem(null!));
    }

    /// <summary>
    ///     Test that the constructor rejects missing collaborators.
    /// </summary>
    [Fact]
    public void PreviewPaneViewModel_New_NullArguments_Throw()
    {
        // Act / Assert: each required argument is checked
        Assert.Throws<ArgumentNullException>(() => new PreviewPaneViewModel(null!, _explorer, _previewScheduler));
        Assert.Throws<ArgumentNullException>(() => new PreviewPaneViewModel(_renderer, null!, _previewScheduler));
        Assert.Throws<ArgumentNullException>(() => new PreviewPaneViewModel(_renderer, _explorer, null!));
    }

    /// <summary>
    ///     Creates a pane over the fixture's explorer and renderer.
    /// </summary>
    /// <returns>The pane.</returns>
    private PreviewPaneViewModel CreatePane()
    {
        var pane = new PreviewPaneViewModel(_renderer, _explorer, _previewScheduler);
        _disposables.Insert(0, pane);
        return pane;
    }

    /// <summary>
    ///     Waits until the preview scheduler has been asked for at least a number of callbacks.
    /// </summary>
    /// <param name="count">The number of requests to wait for.</param>
    /// <returns>A task that completes when enough requests arrived or the wait gave up.</returns>
    private async Task WaitForScheduledAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (_previewScheduler.ScheduledCount < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }
}

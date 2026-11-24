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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the status bar view model over a real workspace and preview pane on temporary folders, with a fake
///     render service whose results the test completes.
/// </summary>
public sealed class StatusBarTests : IDisposable
{
    /// <summary>
    ///     Temporary project folder removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-statusbar-tests-").FullName;

    /// <summary>
    ///     The render service whose results the tests control.
    /// </summary>
    private readonly FakeImageRenderService _renderer = new();

    /// <summary>
    ///     The workspace the status bar follows.
    /// </summary>
    private readonly ProjectWorkspaceViewModel _workspace;

    /// <summary>
    ///     The preview pane in the workspace.
    /// </summary>
    private readonly PreviewPaneViewModel _pane;

    /// <summary>
    ///     The status bar under test.
    /// </summary>
    private readonly StatusBarViewModel _bar;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StatusBarTests" /> class with a project folder that holds two SVG
    ///     files, a workspace with a preview pane, and a status bar over them.
    /// </summary>
    public StatusBarTests()
    {
        foreach (var name in new[] { "a.svg", "b.svg" })
        {
            File.WriteAllText(Path.Combine(_folder, name), "<svg/>");
        }

        var explorer = new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), new FakeRefreshScheduler());
        _workspace = new ProjectWorkspaceViewModel(explorer);
        _pane = new PreviewPaneViewModel(_renderer, explorer, new FakeRefreshScheduler());
        _workspace.Preview = _pane;
        _bar = new StatusBarViewModel(_workspace);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _bar.Dispose();
        _pane.Dispose();
        _workspace.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the bar exists with no project open, says so, and shows neither the project nor the talk hint.
    /// </summary>
    [Fact]
    public void StatusBar_NoProject_IsIdleAndSaysNoProjectIsOpen()
    {
        // Arrange and Act: the bar as it is at start, which is nothing to act on
        var state = _bar.State;

        // Assert: idle, with the words that say why, and nothing about a project
        Assert.Equal(StatusBarState.Idle, state);
        Assert.True(_bar.IsIdle);
        Assert.Equal("No project open", _bar.Summary);
        Assert.False(_bar.HasProject);
        Assert.Equal(string.Empty, _bar.FileName);
        Assert.Equal(string.Empty, _bar.SizeText);
        Assert.Equal("F9", _bar.TalkKeyText);
    }

    /// <summary>
    ///     Test that a project with no file selected says that no picture is selected and shows the project facts.
    /// </summary>
    [Fact]
    public void StatusBar_ProjectOpenNoSelection_SaysNoPictureSelected()
    {
        // Arrange: a bar that announces its changes
        var changed = new List<string?>();
        _bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: open the project
        _workspace.Explorer.Open(_folder);

        // Assert: still idle with the words for the project, and the change was announced
        Assert.Equal(StatusBarState.Idle, _bar.State);
        Assert.Equal("No picture selected", _bar.Summary);
        Assert.True(_bar.HasProject);
        Assert.Equal(string.Empty, _bar.SavedText);
        Assert.Contains(string.Empty, changed);
    }

    /// <summary>
    ///     Test that the bar goes through rendering to ready with the facts of the picture, and to failed when the render
    ///     fails, with words for each state.
    /// </summary>
    [Fact]
    public async Task StatusBar_RenderSucceedsThenFails_FollowsEveryStateWithItsFacts()
    {
        // Arrange: an open project
        _workspace.Explorer.Open(_folder);
        var note = new RenderDiagnostic(RenderDiagnosticSeverity.Warning, "image-access-denied", "not drawn");

        // Act: select a file, let the render succeed with a note, and then select another file whose render fails
        _workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        var rendering = (_bar.State, _bar.Summary, _bar.FileName);
        Assert.Single(_renderer.Requests).Succeed(900, 500, note);
        await _pane.RenderCompletion;
        var ready = (_bar.State, _bar.FileName, _bar.SizeText, _bar.FormatText, _bar.RenderTimeText, _bar.NotesText, _bar.HasNotes);
        _workspace.Explorer.SelectFile(Path.Combine(_folder, "b.svg"));
        _renderer.Requests[1].Fail("cannot-draw", "No.");
        await _pane.RenderCompletion;
        var failed = (_bar.State, _bar.Summary, _bar.FileName, _bar.SizeText, _bar.HasNotes);

        // Assert: each state has its words and only the facts that belong to it
        Assert.Equal((StatusBarState.Rendering, "Rendering a.svg...", "a.svg"), rendering);
        Assert.Equal(StatusBarState.Ready, ready.Item1);
        Assert.Equal("a.svg", ready.FileName);
        Assert.Equal("900 \u00D7 500 px", ready.SizeText);
        Assert.Equal("SVG", ready.FormatText);
        Assert.Matches(@"^Rendered in \d+ ms$", ready.RenderTimeText);
        Assert.Equal("1 note", ready.NotesText);
        Assert.True(ready.HasNotes);
        Assert.Equal((StatusBarState.Failed, "b.svg: render failed", "b.svg", string.Empty, false), failed);
    }

    /// <summary>
    ///     Test that the bar goes back to idle when the preview pane is taken away, and follows a pane that is put there later.
    /// </summary>
    [Fact]
    public void StatusBar_PreviewReplaced_FollowsTheNewPane()
    {
        // Arrange: a project with a picture being rendered
        _workspace.Explorer.Open(_folder);
        _workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));
        var rendering = _bar.IsRendering;

        // Act: take the pane away, and then put it back
        _workspace.Preview = null;
        var withoutPane = _bar.State;
        _workspace.Preview = _pane;
        var withPane = _bar.State;

        // Assert: the bar followed the pane
        Assert.True(rendering);
        Assert.Equal(StatusBarState.Idle, withoutPane);
        Assert.Equal(StatusBarState.Rendering, withPane);
    }

    /// <summary>
    ///     Test that the bar refuses a missing workspace and stops following after it is disposed.
    /// </summary>
    [Fact]
    public void StatusBar_NullWorkspaceThrows_DisposedBarNoLongerFollows()
    {
        // Arrange: a bar that is disposed
        var bar = new StatusBarViewModel(_workspace);
        var changed = 0;
        bar.PropertyChanged += (_, _) => changed++;
        bar.Dispose();

        // Act: open the project and select a file
        _workspace.Explorer.Open(_folder);
        _workspace.Explorer.SelectFile(Path.Combine(_folder, "a.svg"));

        // Assert: nothing was announced, and a missing workspace is refused
        Assert.Equal(0, changed);
        Assert.Throws<ArgumentNullException>(() => new StatusBarViewModel(null!));
    }
}

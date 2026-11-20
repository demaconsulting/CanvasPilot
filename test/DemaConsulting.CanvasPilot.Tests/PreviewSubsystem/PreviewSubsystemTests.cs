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

using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem-level tests for the Preview subsystem: the real render service (CanvasNet), preview pane, and image
///     exporter working together on temporary project folders, with only the refresh scheduler and the dialog
///     service replaced.
/// </summary>
public sealed class PreviewSubsystemTests : IDisposable
{
    /// <summary>
    ///     A 40 x 20 SVG with a red rectangle that is half transparent.
    /// </summary>
    private const string WideSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'>" +
        "<rect width='40' height='20' fill='#ff0000' fill-opacity='0.5'/></svg>";

    /// <summary>
    ///     A 10 x 10 SVG with a blue square.
    /// </summary>
    private const string SquareSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10' fill='#0000ff'/></svg>";

    /// <summary>
    ///     Temporary project folder removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-preview-subsystem-").FullName;

    /// <summary>
    ///     The scheduler of the explorer.
    /// </summary>
    private readonly FakeRefreshScheduler _explorerScheduler = new();

    /// <summary>
    ///     The scheduler of the preview pane.
    /// </summary>
    private readonly FakeRefreshScheduler _previewScheduler = new();

    /// <summary>
    ///     The units that need disposal.
    /// </summary>
    private readonly List<IDisposable> _disposables = [];

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
    ///     Test that selecting an SVG in the project renders it with CanvasNet and shows its pixels, size, and format.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_RenderSelectedImage_SvgInProject_ShowsRenderedPixels()
    {
        // Arrange: a project with an SVG and a preview built over the real render service
        await File.WriteAllTextAsync(Path.Combine(_folder, "wide.svg"), WideSvg, TestContext.Current.CancellationToken);
        var (explorer, pane) = Create();

        // Act: open the project, select the file, and wait for the render
        explorer.Open(_folder);
        explorer.SelectFile(Path.Combine(_folder, "wide.svg"));
        await pane.RenderCompletion;

        // Assert: the pane shows the rendered picture with straight-alpha pixels
        Assert.True(pane.HasImage);
        Assert.False(pane.HasError);
        Assert.False(pane.IsBusy);
        Assert.Equal(40, pane.Image!.Width);
        Assert.Equal(20, pane.Image.Height);
        Assert.Equal("wide.svg: 40 x 20 px, svg", pane.StatusText);
        Assert.Equal([255, 0, 0, 128], pane.Image.RgbaPixels.Span[..4].ToArray().Select(b => (int)b));
    }

    /// <summary>
    ///     Test that a file that cannot be drawn shows an error and a file that can be drawn shows a picture again,
    ///     and that export follows whether a picture is shown.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_ReportRenderProblems_BrokenFile_ShowsErrorThenRecovers()
    {
        // Arrange: a broken SVG and a good one, with an exporter over the preview
        await File.WriteAllTextAsync(Path.Combine(_folder, "broken.svg"), "<svg><not closed", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_folder, "good.svg"), SquareSvg, TestContext.Current.CancellationToken);
        var (explorer, pane) = Create();
        var exporter = new ImageExporter(pane, new ImageRenderService());
        explorer.Open(_folder);

        // Act: select the broken file, then the good one
        explorer.SelectFile(Path.Combine(_folder, "broken.svg"));
        await pane.RenderCompletion;
        var errorShown = (pane.HasError, pane.Error?.Code, exporter.CanExport);
        explorer.SelectFile(Path.Combine(_folder, "good.svg"));
        await pane.RenderCompletion;

        // Assert: the broken file produced a cannot-draw error and nothing to export; the good file recovered
        Assert.True(errorShown.HasError);
        Assert.Equal(RenderErrorCodes.CannotDraw, errorShown.Code);
        Assert.False(errorShown.Item3);
        Assert.False(pane.HasError);
        Assert.True(pane.HasImage);
        Assert.True(exporter.CanExport);
    }

    /// <summary>
    ///     Test that editing the selected file renders the new content after the debounce delay.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_KeepPreviewCurrent_SelectedFileEdited_RendersNewContent()
    {
        // Arrange: a shown 40 x 20 picture
        var file = Path.Combine(_folder, "wide.svg");
        await File.WriteAllTextAsync(file, WideSvg, TestContext.Current.CancellationToken);
        var (explorer, pane) = Create();
        explorer.Open(_folder);
        explorer.SelectFile(file);
        await pane.RenderCompletion;
        var before = (pane.Image!.Width, pane.Image.Height);

        // Act: the file is replaced by a 10 x 10 SVG and the debounce delay passes
        await File.WriteAllTextAsync(file, SquareSvg, TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (_previewScheduler.ScheduledCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        _previewScheduler.RunPending();
        await pane.RenderCompletion;

        // Assert: the picture shown first was the old one, and the pane now shows the new content
        Assert.Equal((40, 20), before);
        Assert.Equal((10, 10), (pane.Image.Width, pane.Image.Height));
    }

    /// <summary>
    ///     Test that the exporter saves the shown picture as a valid PNG that has the picture's size, and copies
    ///     the picture to the clipboard.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_ExportImage_ShownPicture_WritesPngAndCopiesToClipboard()
    {
        // Arrange: a shown 40 x 20 picture and a user who chooses a target file
        await File.WriteAllTextAsync(Path.Combine(_folder, "wide.svg"), WideSvg, TestContext.Current.CancellationToken);
        var (explorer, pane) = Create();
        var exporter = new ImageExporter(pane, new ImageRenderService());
        var dialogs = new FakeDialogService { SaveFileToPick = Path.Combine(_folder, "wide-export.png") };
        explorer.Open(_folder);
        explorer.SelectFile(Path.Combine(_folder, "wide.svg"));
        await pane.RenderCompletion;

        // Act: export and copy
        await exporter.ExportPngAsync(dialogs);
        await exporter.CopyImageAsync(dialogs);

        // Assert: the PNG has the signature and the picture's size in its header, and the picture was copied
        var png = await File.ReadAllBytesAsync(Path.Combine(_folder, "wide-export.png"), TestContext.Current.CancellationToken);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.Equal(40, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(20, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        Assert.Same(pane.Image, Assert.Single(dialogs.CopiedImages));
    }

    /// <summary>
    ///     Creates an explorer and a preview pane over the real render service.
    /// </summary>
    /// <returns>The explorer and the pane.</returns>
    private (ProjectExplorerViewModel Explorer, PreviewPaneViewModel Pane) Create()
    {
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), _explorerScheduler);
        var pane = new PreviewPaneViewModel(new ImageRenderService(), explorer, _previewScheduler);
        _disposables.Add(pane);
        _disposables.Add(explorer);
        return (explorer, pane);
    }
}

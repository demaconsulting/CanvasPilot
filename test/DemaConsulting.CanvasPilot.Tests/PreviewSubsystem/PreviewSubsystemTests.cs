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
    ///     Test that the shown picture of a diagram names its file, holds fingerprints of the file and the pixels, and
    ///     reports where each node was drawn, and that a picture of a kind without element data reports none.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_ReportPictureFacts_DiagramAndSvg_ShowsIdentityAndPositions()
    {
        // Arrange: a project with a diagram and an SVG, and a preview built over the real render service
        var diagram = Path.Combine(_folder, "flow.mmd");
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Start] --> B[End]\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_folder, "wide.svg"), WideSvg, TestContext.Current.CancellationToken);
        var (explorer, pane) = Create();
        explorer.Open(_folder);

        // Act: select the diagram, then the SVG
        explorer.SelectFile(diagram);
        await pane.RenderCompletion;
        var diagramImage = pane.CurrentImage;
        explorer.SelectFile(Path.Combine(_folder, "wide.svg"));
        await pane.RenderCompletion;
        var svgImage = pane.CurrentImage;

        // Assert: the diagram's picture is identified by its file and fingerprints and locates both nodes inside it
        Assert.NotNull(diagramImage);
        Assert.Equal(diagram, diagramImage.SourcePath);
        Assert.Equal(64, diagramImage.SourceFileHash?.Length);
        Assert.Equal($"{diagram}#{diagramImage.ContentHash}", diagramImage.SnapshotKey);
        var nodes = diagramImage.Positions.Where(position => position.Kind == "node").ToList();
        Assert.Equal(["Start", "End"], nodes.Select(node => node.Text));
        Assert.All(nodes, node => Assert.True(node.Box!.Value.X + node.Box.Value.Width <= diagramImage.Width));

        // Assert: the SVG's picture has its own fingerprints and no element data
        Assert.NotNull(svgImage);
        Assert.NotEqual(diagramImage.ContentHash, svgImage.ContentHash);
        Assert.Empty(svgImage.Positions);
        Assert.Empty(svgImage.Anchors);
    }

    /// <summary>
    ///     Test that content placed in the overlay slot stays there while the pane shows different pictures, renders
    ///     a changed file again, and fails to draw a file, because the layer belongs to the pane and not to a picture.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_HostOverlay_ContentAssigned_StaysWhilePicturesChange()
    {
        // Arrange: a project with two SVGs and a broken one, a pane with content in its overlay slot
        await File.WriteAllTextAsync(Path.Combine(_folder, "wide.svg"), WideSvg, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_folder, "good.svg"), SquareSvg, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_folder, "broken.svg"), "<svg><not closed", TestContext.Current.CancellationToken);
        var (explorer, pane) = Create();
        var layer = new object();
        pane.Overlay = layer;
        explorer.Open(_folder);

        // Act: show one picture, another, the broken file, and then the first picture again after it was edited
        explorer.SelectFile(Path.Combine(_folder, "wide.svg"));
        await pane.RenderCompletion;
        var wide = (pane.Overlay, pane.HasOverlay, pane.Image?.Width);
        explorer.SelectFile(Path.Combine(_folder, "good.svg"));
        await pane.RenderCompletion;
        var good = (pane.Overlay, pane.HasOverlay, pane.Image?.Width);
        explorer.SelectFile(Path.Combine(_folder, "broken.svg"));
        await pane.RenderCompletion;
        var broken = (pane.Overlay, pane.HasOverlay, pane.HasError);
        await File.WriteAllTextAsync(Path.Combine(_folder, "wide.svg"), SquareSvg, TestContext.Current.CancellationToken);
        explorer.SelectFile(Path.Combine(_folder, "wide.svg"));
        await pane.RenderCompletion;
        var edited = (pane.Overlay, pane.HasOverlay, pane.Image?.Width);

        // Assert: the same content is in the slot every time, next to whatever the pane shows
        Assert.Equal((layer, true, 40), wide);
        Assert.Equal((layer, true, 10), good);
        Assert.Equal((layer, true, true), broken);
        Assert.Equal((layer, true, 10), edited);
    }

    /// <summary>
    ///     Test that the shown picture can be prepared for dragging into another program: the drag file is a real PNG
    ///     of the picture, it is the same file the next time, and a different picture gets a different file.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_DragImageOut_ShownPicture_ProvidesPngFileAndBytes()
    {
        // Arrange: a project with two SVGs and a pane over the real render service with a drag folder
        await File.WriteAllTextAsync(Path.Combine(_folder, "wide.svg"), WideSvg, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_folder, "good.svg"), SquareSvg, TestContext.Current.CancellationToken);
        var dragFolder = Path.Combine(_folder, "drag-out");
        var (explorer, pane) = Create(dragFolder);
        explorer.Open(_folder);

        // Act: show the first picture and prepare it twice, then show the other and prepare it
        explorer.SelectFile(Path.Combine(_folder, "wide.svg"));
        await pane.RenderCompletion;
        var first = pane.PrepareDragFile();
        var again = pane.PrepareDragFile();
        var expected = new ImageRenderService().EncodePng(pane.Image!);
        explorer.SelectFile(Path.Combine(_folder, "good.svg"));
        await pane.RenderCompletion;
        var second = pane.PrepareDragFile();

        // Assert: the file is the PNG of the shown picture, with its size in the header, and is reused; the other
        // picture has a file of its own
        Assert.NotNull(first);
        Assert.Equal(first, again);
        var bytes = await File.ReadAllBytesAsync(first, TestContext.Current.CancellationToken);
        Assert.Equal(expected, bytes);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], bytes[..8]);
        Assert.Equal(40, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(20, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        Assert.StartsWith("wide-", Path.GetFileName(first), StringComparison.Ordinal);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.StartsWith("good-", Path.GetFileName(second), StringComparison.Ordinal);
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
        var dialogs = new FakeExportDialogs { SaveFileToPick = Path.Combine(_folder, "wide-export.png") };
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
    /// <summary>
    ///     Test that the shown file can be drawn again at a requested size from what the picture source tells (the file
    ///     of the shown picture and the folder of the project), and that the result is exactly the box and is encoded with
    ///     a resolution that the file keeps while decoding to the same pixels.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_RenderAtRequestedSize_ShownFile_IsDrawnAtTheBoxAndEncodedWithResolution()
    {
        // Arrange: a shown 40 x 20 picture and the render service of the application
        await File.WriteAllTextAsync(Path.Combine(_folder, "wide.svg"), WideSvg, TestContext.Current.CancellationToken);
        var (explorer, pane) = Create();
        explorer.Open(_folder);
        explorer.SelectFile(Path.Combine(_folder, "wide.svg"));
        await pane.RenderCompletion;
        IRenderedImageSource source = pane;
        var renderer = new ImageRenderService();

        // Act: draw the shown file into a 400 x 400 box with a white margin, and encode the result at 150 dots per inch
        var result = await renderer.RenderAsync(
            source.CurrentImage!.SourcePath,
            source.ProjectFolder!,
            new RenderSettings(400, 400, BackgroundHex: "#FFFFFF"),
            TestContext.Current.CancellationToken);
        var png = renderer.EncodePng(result.Image!, 150);
        using var stream = new MemoryStream(png);
        using var decoded = DemaConsulting.CanvasNet.Codecs.PngCodec.Load(stream);

        // Assert: the box size, a white margin and the red picture in the middle, and the resolution in the file
        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal((400, 400), (result.Image!.Width, result.Image.Height));
        Assert.Equal((400, 400), (decoded.Width, decoded.Height));
        Assert.Equal([255, 255, 255, 255], result.Image.RgbaPixels.Span[..4].ToArray().Select(b => (int)b));
        var middle = ((200 * 400) + 200) * 4;
        Assert.True(result.Image.RgbaPixels.Span[middle + 1] > 100 && result.Image.RgbaPixels.Span[middle] == 255);
        Assert.Equal(5906u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(41)));
    }

    /// <summary>
    ///     Test that the user is offered the sizes through the real export dialog view model, and that the shown picture is
    ///     exported at the size that was chosen: the file is a PNG of exactly the preset size with its resolution, and a
    ///     second export offers the choice of the first.
    /// </summary>
    [Fact]
    public async Task PreviewSubsystem_ExportAtPreset_ShownPicture_WritesPngOfThePresetSizeAndRemembersTheChoice()
    {
        // Arrange: a shown 40 x 20 picture, one size of the application, and a user that works the real dialog view model
        await File.WriteAllTextAsync(Path.Combine(_folder, "wide.svg"), WideSvg, TestContext.Current.CancellationToken);
        var (explorer, pane) = Create();
        var preset = new ExportPreset("app:card", "Card", 300, 300, 200, false, "#FFFFFF", ExportPresetOrigin.Application);
        var exporter = new ImageExporter(pane, new ImageRenderService(), null, new StaticPresets(preset));
        var user = new DialogViewModelUser("Card", Path.Combine(_folder, "wide-card.png"));
        explorer.Open(_folder);
        explorer.SelectFile(Path.Combine(_folder, "wide.svg"));
        await pane.RenderCompletion;

        // Act: export twice
        await exporter.ExportPngAsync(user);
        await exporter.ExportPngAsync(user);

        // Assert: the file is a PNG of 300 x 300 with 200 dots per inch (7874 pixels per meter), and the second offer
        // had the first choice selected
        var png = await File.ReadAllBytesAsync(Path.Combine(_folder, "wide-card.png"), TestContext.Current.CancellationToken);
        Assert.Equal(300, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(300, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        Assert.Equal(7874u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(41)));
        Assert.Equal(["As rendered", "Card"], user.Offers[0]);
        Assert.Equal("Card", user.SelectedWhenOffered[1]);
    }

    private (ProjectExplorerViewModel Explorer, PreviewPaneViewModel Pane) Create(string? dragFolder = null)
    {
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), _explorerScheduler);
        var renderer = new ImageRenderService();
        var pane = new PreviewPaneViewModel(
            renderer,
            explorer,
            _previewScheduler,
            dragSource: dragFolder is null ? null : new ImageDragSource(renderer, dragFolder));
        _disposables.Add(pane);
        _disposables.Add(explorer);
        return (explorer, pane);
    }

    /// <summary>
    ///     A source of sizes that always gives the same list.
    /// </summary>
    /// <param name="presets">The sizes.</param>
    private sealed class StaticPresets(params ExportPreset[] presets) : IExportPresetSource
    {
        /// <inheritdoc />
        public IReadOnlyList<ExportPreset> GetPresets() => presets;
    }

    /// <summary>
    ///     A user that works the real export dialog view model: it selects the line with a name, presses Export, and writes
    ///     down what was offered and what was selected when the dialog opened.
    /// </summary>
    /// <param name="presetName">The name of the line to select.</param>
    /// <param name="saveFile">The file the user chooses to save to.</param>
    private sealed class DialogViewModelUser(string presetName, string saveFile) : IExportDialogs
    {
        /// <summary>
        ///     Gets the names of the choices of each offer.
        /// </summary>
        public List<string[]> Offers { get; } = [];

        /// <summary>
        ///     Gets the name of the line that was selected when each offer was shown.
        /// </summary>
        public List<string?> SelectedWhenOffered { get; } = [];

        /// <inheritdoc />
        public Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string filterName, string filterPattern)
        {
            return Task.FromResult<string?>(saveFile);
        }

        /// <inheritdoc />
        public Task<ExportPreset?> PickExportPresetAsync(IReadOnlyList<ExportPreset> presets, ExportPreset? last)
        {
            var viewModel = new ExportPresetDialogViewModel(presets, last);
            Offers.Add(viewModel.Items.Select(item => item.Name).ToArray());
            SelectedWhenOffered.Add(viewModel.SelectedItem?.Name);
            viewModel.SelectedItem = viewModel.Items.First(item => item.Name == presetName);
            viewModel.ExportCommand.Execute(null);
            return Task.FromResult(viewModel.Accepted ? viewModel.SelectedPreset : null);
        }

        /// <inheritdoc />
        public Task<bool> CopyImageToClipboardAsync(RenderedImage image) => Task.FromResult(false);

        /// <inheritdoc />
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    }
}

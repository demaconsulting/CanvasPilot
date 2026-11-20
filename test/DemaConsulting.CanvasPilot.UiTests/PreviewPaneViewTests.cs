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
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Render service for UI tests whose renders wait until the test releases them, so the busy state can be seen.
/// </summary>
internal sealed class GatedRenderService : IImageRenderService
{
    /// <summary>
    ///     The renders waiting to be released.
    /// </summary>
    private readonly List<TaskCompletionSource<RenderResult>> _waiting = [];

    /// <summary>
    ///     The number of renders requested so far.
    /// </summary>
    private int _requested;

    /// <summary>
    ///     Gets how many renders were requested.
    /// </summary>
    public int Requested => _requested;

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource<RenderResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting.Add(source);
        _requested++;
        return source.Task;
    }

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image)
    {
        return [];
    }

    /// <summary>
    ///     Releases the oldest waiting render with a picture.
    /// </summary>
    /// <param name="sourcePath">The path of the file the picture comes from.</param>
    public void Release(string sourcePath)
    {
        var pixels = new byte[4 * 4 * 4];
        Array.Fill(pixels, (byte)255);
        _waiting[0].SetResult(RenderResult.Success(new RenderedImage(sourcePath, "svg", 4, 4, pixels)));
        _waiting.RemoveAt(0);
    }
}

/// <summary>
///     Headless UI tests for the preview pane view.
/// </summary>
public sealed class PreviewPaneViewTests : IDisposable
{
    /// <summary>
    ///     A 40 x 20 SVG with a red rectangle that is half transparent.
    /// </summary>
    private const string HalfRedSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'>" +
        "<rect width='40' height='20' fill='#ff0000' fill-opacity='0.5'/></svg>";

    /// <summary>
    ///     An 800 x 400 SVG with a blue rectangle, larger than the pane the tests show it in.
    /// </summary>
    private const string LargeSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='800' height='400'>" +
        "<rect width='800' height='400' fill='#0000ff'/></svg>";

    /// <summary>
    ///     Temporary project folder removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-ui-preview-").FullName;

    /// <summary>
    ///     The view models created by a test, disposed with the fixture so no watcher outlives it.
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
    ///     Test that selecting an SVG shows a bitmap of the right size in the pane, with premultiplied BGRA pixels, the
    ///     status line, and no placeholder, error, or busy indicator.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_SvgSelected_ShowsBitmapWithConvertedPixels()
    {
        // Arrange: a shown pane over a project with a half-transparent red SVG
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());

        // Act: select the file and wait for the render
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();

        // Assert: the image control shows a 40 x 20 bitmap whose first pixel is premultiplied blue, green, red, alpha
        var image = SettingsUiHelpers.Find<Image>(window, "PreviewImage");
        var bitmap = Assert.IsType<WriteableBitmap>(image.Source);
        Assert.Equal(new PixelSize(40, 20), bitmap.PixelSize);
        Assert.Equal([0, 0, 128, 128], FirstPixel(bitmap));
        Assert.True(SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller").IsVisible);
        Assert.Equal("red.svg: 40 x 20 px, svg", SettingsUiHelpers.Find<TextBlock>(window, "PreviewStatus").Text);
        Assert.False(SettingsUiHelpers.Find<TextBlock>(window, "PreviewPlaceholder").IsVisible);
        Assert.False(SettingsUiHelpers.Find<StackPanel>(window, "PreviewErrorPanel").IsVisible);
        Assert.False(SettingsUiHelpers.Find<ProgressBar>(window, "PreviewBusy").IsVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that a render that cannot draw the file shows its code, message, and hint in the pane and no image.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_BrokenFile_ShowsErrorCodeMessageAndHint()
    {
        // Arrange: a shown pane over a project with a broken SVG
        await File.WriteAllTextAsync(Path.Combine(_folder, "broken.svg"), "<svg><not closed", TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());

        // Act: select the file and wait for the render
        explorer.SelectFile(Path.Combine(_folder, "broken.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();

        // Assert: the error panel names the code, the message, and the hint; the image is gone
        Assert.True(SettingsUiHelpers.Find<StackPanel>(window, "PreviewErrorPanel").IsVisible);
        Assert.Contains("cannot-draw", SettingsUiHelpers.Find<TextBlock>(window, "PreviewErrorTitle").Text, StringComparison.Ordinal);
        Assert.Contains("well-formed", SettingsUiHelpers.Find<TextBlock>(window, "PreviewErrorMessage").Text, StringComparison.Ordinal);
        var hint = SettingsUiHelpers.Find<TextBlock>(window, "PreviewErrorHint");
        Assert.True(hint.IsVisible);
        Assert.StartsWith("Hint:", hint.Text, StringComparison.Ordinal);
        Assert.False(SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller").IsVisible);
        Assert.Equal("broken.svg: render failed", SettingsUiHelpers.Find<TextBlock>(window, "PreviewStatus").Text);

        window.Close();
    }

    /// <summary>
    ///     Test that the busy indicator shows while a render runs and goes away when it finishes.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_RenderRunning_ShowsBusyIndicatorUntilDone()
    {
        // Arrange: a shown pane over a render service that waits
        await File.WriteAllTextAsync(Path.Combine(_folder, "slow.svg"), "<svg/>", TestContext.Current.CancellationToken);
        var gate = new GatedRenderService();
        var (window, explorer, pane) = ShowPane(gate);
        var busy = SettingsUiHelpers.Find<ProgressBar>(window, "PreviewBusy");
        var placeholder = SettingsUiHelpers.Find<TextBlock>(window, "PreviewPlaceholder");
        var idle = (busy.IsVisible, placeholder.IsVisible);

        // Act: select the file, look while the render waits, then release it
        explorer.SelectFile(Path.Combine(_folder, "slow.svg"));
        AppShellUiHelpers.Flush();
        var running = (busy.IsVisible, placeholder.IsVisible, SettingsUiHelpers.Find<TextBlock>(window, "PreviewStatus").Text);
        gate.Release(Path.Combine(_folder, "slow.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();

        // Assert: idle shows the placeholder, running shows the busy indicator and status, done shows neither
        Assert.Equal((false, true), idle);
        Assert.Equal((true, false, "Rendering slow.svg..."), running);
        Assert.False(busy.IsVisible);
        Assert.Equal(1, gate.Requested);

        window.Close();
    }

    /// <summary>
    ///     Test that notes from the render appear in the diagnostics list under a header with their number.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_RenderWithNotes_ListsDiagnostics()
    {
        // Arrange: an SVG that embeds a file from outside the project, which the render reports as a note
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(_folder)!, "canvaspilot-outside.png"), "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "linked.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='10' height='10'>" +
            "<image href='../canvaspilot-outside.png' width='10' height='10'/></svg>",
            TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        var expander = SettingsUiHelpers.Find<Expander>(window, "PreviewDiagnosticsExpander");
        var hiddenBefore = !expander.IsVisible;

        // Act: select the file and wait for the render
        explorer.SelectFile(Path.Combine(_folder, "linked.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        expander.IsExpanded = true;
        AppShellUiHelpers.Flush();

        // Assert: the expander appeared with a one-note header and the list holds the note
        Assert.True(hiddenBefore);
        Assert.True(expander.IsVisible);
        Assert.Equal("1 note", expander.Header);
        var list = SettingsUiHelpers.Find<ItemsControl>(window, "PreviewDiagnostics");
        var note = Assert.IsType<RenderDiagnostic>(Assert.Single(list.Items));
        Assert.Equal("image-access-denied", note.Code);

        File.Delete(Path.Combine(Path.GetDirectoryName(_folder)!, "canvaspilot-outside.png"));
        window.Close();
    }

    /// <summary>
    ///     Test that clearing Fit to window shows the picture at its own size with scroll bars, and setting it again
    ///     scales the picture to the pane.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_FitToWindowToggled_ChangesStretchAndScrollBars()
    {
        // Arrange: a shown pane that shows a picture
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var image = SettingsUiHelpers.Find<Image>(window, "PreviewImage");
        var viewbox = SettingsUiHelpers.Find<Viewbox>(window, "PreviewViewbox");
        var scroller = SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller");
        var fitted = (viewbox.Stretch, viewbox.StretchDirection, scroller.HorizontalScrollBarVisibility);

        // Act: turn fitting off with the check box, then on again
        var check = SettingsUiHelpers.Find<CheckBox>(window, "PreviewFitToWindow");
        check.IsChecked = false;
        AppShellUiHelpers.Flush();
        var actual = (viewbox.Stretch, viewbox.StretchDirection, scroller.HorizontalScrollBarVisibility, pane.FitToWindow);
        check.IsChecked = true;
        AppShellUiHelpers.Flush();

        // Assert: fitted scales down without scroll bars; actual size does not scale and has auto scroll bars; the
        // picture itself is never stretched, because the Viewbox scales it together with the overlay
        Assert.Equal((Stretch.Uniform, StretchDirection.DownOnly, ScrollBarVisibility.Disabled), fitted);
        Assert.Equal((Stretch.None, StretchDirection.Both, ScrollBarVisibility.Auto, false), actual);
        Assert.Equal(Stretch.Uniform, viewbox.Stretch);
        Assert.Equal(Stretch.None, image.Stretch);

        window.Close();
    }

    /// <summary>
    ///     Test that at actual size the overlay slot covers the picture exactly, in the picture's own pixels and
    ///     without scaling.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_FitOff_ImageAtNaturalSize_OverlayCoversImage()
    {
        // Arrange: a shown pane with an overlay, over a picture that is larger than the pane, at actual size
        var (window, pane, overlay) = await ShowLargePictureWithOverlayAsync();
        pane.FitToWindow = false;
        AppShellUiHelpers.Flush();
        var image = SettingsUiHelpers.Find<Image>(window, "PreviewImage");

        // Act: measure the overlay and the picture in their own coordinates and in the window's
        var overlayExtent = ScreenExtent(window, overlay);
        var imageExtent = ScreenExtent(window, image);

        // Assert: both are 800 x 400 pixels in their own coordinates, unscaled, and in the same place
        Assert.Equal(new Size(800, 400), overlay.Bounds.Size);
        Assert.Equal(new Size(800, 400), image.Bounds.Size);
        Assert.Equal(new Size(800, 400), overlayExtent.Size);
        Assert.Equal(imageExtent, overlayExtent);

        window.Close();
    }

    /// <summary>
    ///     Test that when the picture is fitted to a smaller pane the overlay is still laid out in the picture's
    ///     pixels and is scaled with the picture, so it covers the picture exactly.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_FitOn_OverlaySizeEqualsImagePixelSize()
    {
        // Arrange: a shown pane with an overlay, over a picture that is larger than the pane, fitted to the pane
        var (window, _, overlay) = await ShowLargePictureWithOverlayAsync();
        var image = SettingsUiHelpers.Find<Image>(window, "PreviewImage");

        // Act: measure the overlay and the picture in their own coordinates and in the window's
        var overlayExtent = ScreenExtent(window, overlay);
        var imageExtent = ScreenExtent(window, image);

        // Assert: the overlay's own size is the picture's pixel size, while on screen both are scaled down by the same
        // factor, keeping the aspect ratio, and cover the same area
        Assert.Equal(new Size(800, 400), overlay.Bounds.Size);
        Assert.Equal(new Size(800, 400), image.Bounds.Size);
        Assert.True(overlayExtent.Width < 800);
        Assert.Equal(2.0, overlayExtent.Width / overlayExtent.Height, 3);
        Assert.Equal(imageExtent.X, overlayExtent.X, 3);
        Assert.Equal(imageExtent.Y, overlayExtent.Y, 3);
        Assert.Equal(imageExtent.Width, overlayExtent.Width, 3);
        Assert.Equal(imageExtent.Height, overlayExtent.Height, 3);

        window.Close();
    }

    /// <summary>
    ///     Test that the overlay slot is hidden while it is empty, shows the content assigned to it, and is hidden
    ///     again when the slot is cleared.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_OverlaySlot_EmptyThenAssigned_IsHiddenThenShown()
    {
        // Arrange: a shown pane with a picture and no overlay
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var host = SettingsUiHelpers.Find<ContentControl>(window, "PreviewOverlay");
        var hiddenBefore = !host.IsVisible;

        // Act: assign a control, then clear the slot
        var content = new Border { Background = Brushes.Transparent };
        pane.Overlay = content;
        AppShellUiHelpers.Flush();
        var shown = (host.IsVisible, host.Content);
        pane.Overlay = null;
        AppShellUiHelpers.Flush();

        // Assert: hidden when empty, showing exactly the assigned content, and hidden again after clearing
        Assert.True(hiddenBefore);
        Assert.True(shown.IsVisible);
        Assert.Same(content, shown.Content);
        Assert.False(host.IsVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that closing the project clears the shown picture and its bitmap.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_ProjectClosed_ClearsPicture()
    {
        // Arrange: a shown pane that shows a picture
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();

        // Act: close the project
        explorer.Close();
        AppShellUiHelpers.Flush();

        // Assert: the picture is gone and the placeholder explains why
        Assert.Null(SettingsUiHelpers.Find<Image>(window, "PreviewImage").Source);
        var placeholder = SettingsUiHelpers.Find<TextBlock>(window, "PreviewPlaceholder");
        Assert.True(placeholder.IsVisible);
        Assert.Equal("No project is open.", placeholder.Text);

        window.Close();
    }

    /// <summary>
    ///     Test that the bitmap created for a picture has the picture's size and premultiplied BGRA pixels.
    /// </summary>
    [AvaloniaFact]
    public void RenderedImageBitmap_Create_Picture_HasSizeAndPremultipliedBgraPixels()
    {
        // Arrange: a 2 x 1 picture with an opaque green and a half-transparent white pixel
        var image = new RenderedImage("a.svg", "svg", 2, 1, new byte[] { 0, 255, 0, 255, 255, 255, 255, 128 });

        // Act: create the bitmap
        using var bitmap = RenderedImageBitmap.Create(image);

        // Assert: size, format, and both converted pixels
        Assert.Equal(new PixelSize(2, 1), bitmap.PixelSize);
        Assert.Equal(AlphaFormat.Premul, bitmap.AlphaFormat);
        Assert.Equal([0, 255, 0, 255, 128, 128, 128, 128], PixelBytes(bitmap, 8));
    }

    /// <summary>
    ///     Shows a preview pane in a window, over an explorer that has the project folder open.
    /// </summary>
    /// <param name="renderer">The render service.</param>
    /// <returns>The window, the explorer, and the pane.</returns>
    private (Window Window, ProjectExplorerViewModel Explorer, PreviewPaneViewModel Pane) ShowPane(IImageRenderService renderer)
    {
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), new NeverRefreshScheduler());
        var pane = new PreviewPaneViewModel(renderer, explorer, new NeverRefreshScheduler());
        _disposables.Add(pane);
        _disposables.Add(explorer);
        explorer.Open(_folder);
        var window = new Window { Width = 400, Height = 300, Content = new PreviewPaneView { DataContext = pane } };
        window.Show();
        AppShellUiHelpers.Flush();
        return (window, explorer, pane);
    }

    /// <summary>
    ///     Shows a picture of 800 x 400 pixels, which is larger than the pane, with a transparent overlay on it.
    /// </summary>
    /// <returns>The window, the pane view model, and the overlay control.</returns>
    private async Task<(Window Window, PreviewPaneViewModel Pane, Border Overlay)> ShowLargePictureWithOverlayAsync()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "large.svg"), LargeSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        var overlay = new Border { Background = Brushes.Transparent };
        pane.Overlay = overlay;
        explorer.SelectFile(Path.Combine(_folder, "large.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        return (window, pane, overlay);
    }

    /// <summary>
    ///     Measures where a control is in a window, including every scale between them.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="control">The control.</param>
    /// <returns>The rectangle the control covers in the window's coordinates.</returns>
    private static Rect ScreenExtent(Window window, Control control)
    {
        var topLeft = control.TranslatePoint(new Point(0, 0), window) ?? throw new InvalidOperationException("The control is not in the window.");
        var bottomRight = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), window) ?? throw new InvalidOperationException("The control is not in the window.");
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>
    ///     Reads the first pixel of a bitmap as blue, green, red, alpha bytes.
    /// </summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <returns>The four bytes.</returns>
    private static byte[] FirstPixel(WriteableBitmap bitmap)
    {
        return PixelBytes(bitmap, 4);
    }

    /// <summary>
    ///     Reads the first bytes of the first row of a bitmap.
    /// </summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="count">How many bytes to read.</param>
    /// <returns>The bytes.</returns>
    private static byte[] PixelBytes(WriteableBitmap bitmap, int count)
    {
        using var framebuffer = bitmap.Lock();
        var bytes = new byte[count];
        System.Runtime.InteropServices.Marshal.Copy(framebuffer.Address, bytes, 0, count);
        return bytes;
    }
}

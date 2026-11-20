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
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
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
    ///     Test that dragging the shown picture a few pixels offers the program the user drops it on a PNG file of the
    ///     picture and the picture's bitmap, as a copy.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_DragFromImage_PictureShown_OffersPngFileAndBitmap()
    {
        // Arrange: a shown pane that can drag out, with a recording drag operation in place of the system's
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane, view) = ShowPaneView(new ImageRenderService(), withDragSource: true);
        var offered = new TaskCompletionSource<DragOffer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        view.DragStarter = (press, data, effects) =>
        {
            starts++;
            offered.TrySetResult(DragOffer.Capture(press, data, effects));
            return Task.FromResult(DragDropEffects.Copy);
        };
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var center = Center(window, SettingsUiHelpers.Find<Image>(window, "PreviewImage"));

        // Act: press the left button on the picture and move the pointer well beyond the threshold
        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(center + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
        var offer = await WaitForAsync(offered.Task);
        window.MouseUp(center + new Vector(10, 0), MouseButton.Left);
        window.MouseMove(center + new Vector(40, 0), RawInputModifiers.None);
        AppShellUiHelpers.Flush();

        // Assert: one drag started by the press, for copying, with a PNG file named after the picture and a bitmap of
        // the picture's size; moving again after the release starts nothing
        Assert.Equal(1, starts);
        Assert.True(offer.PressWasLeftButton);
        Assert.Equal(DragDropEffects.Copy, offer.Effects);
        Assert.Contains(DataFormat.File, offer.Formats);
        Assert.Contains(DataFormat.Bitmap, offer.Formats);
        Assert.StartsWith($"red-{pane.Image!.ContentHash[..8]}", offer.FileName, StringComparison.Ordinal);
        Assert.EndsWith(".png", offer.FileName, StringComparison.Ordinal);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], offer.FileHeader);
        Assert.Equal(new PixelSize(40, 20), offer.BitmapSize);

        window.Close();
    }

    /// <summary>
    ///     Test that a click, a press that moves less than the threshold, and a drag that starts while dragging out is
    ///     turned off or the pane cannot drag out start nothing.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_DragFromImage_ClickOrNotAllowed_StartsNothing()
    {
        // Arrange: a shown pane that can drag out and one that cannot, each with a counting drag operation
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane, view) = ShowPaneView(new ImageRenderService(), withDragSource: true);
        var starts = 0;
        view.DragStarter = (_, _, _) =>
        {
            starts++;
            return Task.FromResult(DragDropEffects.None);
        };
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var center = Center(window, SettingsUiHelpers.Find<Image>(window, "PreviewImage"));

        // Act: click; press and move 3 pixels, then release; press and move far while dragging out is turned off
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(center + new Vector(3, 3), RawInputModifiers.LeftMouseButton);
        window.MouseUp(center + new Vector(3, 3), MouseButton.Left);
        pane.IsDragOutEnabled = false;
        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(center + new Vector(20, 0), RawInputModifiers.LeftMouseButton);
        window.MouseUp(center + new Vector(20, 0), MouseButton.Left);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        AppShellUiHelpers.Flush();
        var (otherWindow, otherExplorer, otherPane, otherView) = ShowPaneView(new ImageRenderService());
        var otherStarts = 0;
        otherView.DragStarter = (_, _, _) =>
        {
            otherStarts++;
            return Task.FromResult(DragDropEffects.None);
        };
        otherExplorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await otherPane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var otherCenter = Center(otherWindow, SettingsUiHelpers.Find<Image>(otherWindow, "PreviewImage"));
        otherWindow.MouseDown(otherCenter, MouseButton.Left);
        otherWindow.MouseMove(otherCenter + new Vector(20, 0), RawInputModifiers.LeftMouseButton);
        otherWindow.MouseUp(otherCenter + new Vector(20, 0), MouseButton.Left);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        AppShellUiHelpers.Flush();

        // Assert: none of these started a drag
        Assert.Equal(0, starts);
        Assert.Equal(0, otherStarts);

        window.Close();
        otherWindow.Close();
    }

    /// <summary>
    ///     Test that a drag operation that fails is written to the log and leaves the pane working, so that a drag can
    ///     start again.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_DragFromImage_DragOperationFails_LogsAndKeepsWorking()
    {
        // Arrange: a shown pane with a logger and a drag operation that fails the first time
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var logger = new RollingFileLogger(Path.Combine(_folder, ".logs"));
        var (window, explorer, pane, view) = ShowPaneView(new ImageRenderService(), withDragSource: true, logger: logger);
        var starts = 0;
        view.DragStarter = (_, _, _) =>
        {
            starts++;
            return starts == 1
                ? Task.FromException<DragDropEffects>(new InvalidOperationException("the drop target refused"))
                : Task.FromResult(DragDropEffects.Copy);
        };
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var center = Center(window, SettingsUiHelpers.Find<Image>(window, "PreviewImage"));

        // Act: drag, wait for the failure to be logged, release, and drag again
        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(center + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
        await WaitForAsync(() => File.Exists(logger.ActiveFilePath) && File.ReadAllText(logger.ActiveFilePath).Contains("the drop target refused", StringComparison.Ordinal));
        window.MouseUp(center + new Vector(10, 0), MouseButton.Left);
        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(center + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
        await WaitForAsync(() => starts == 2);
        window.MouseUp(center + new Vector(10, 0), MouseButton.Left);

        // Assert: the failure was logged as a dragging problem and the second drag started normally
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("Dragging the picture out of the preview failed", log, StringComparison.Ordinal);
        Assert.Equal(2, starts);

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
        var (window, explorer, pane, _) = ShowPaneView(renderer);
        return (window, explorer, pane);
    }

    /// <summary>
    ///     Shows a preview pane in a window, over an explorer that has the project folder open, and exposes the view
    ///     so that a test can replace its drag operation.
    /// </summary>
    /// <param name="renderer">The render service.</param>
    /// <param name="withDragSource">Whether the pane can prepare its picture for dragging out.</param>
    /// <param name="logger">Optional logger of the pane.</param>
    /// <returns>The window, the explorer, the pane, and the view.</returns>
    private (Window Window, ProjectExplorerViewModel Explorer, PreviewPaneViewModel Pane, PreviewPaneView View) ShowPaneView(
        IImageRenderService renderer,
        bool withDragSource = false,
        RollingFileLogger? logger = null)
    {
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), new NeverRefreshScheduler());
        var dragSource = withDragSource ? new ImageDragSource(renderer, Path.Combine(_folder, ".drag-out"), logger) : null;
        var pane = new PreviewPaneViewModel(renderer, explorer, new NeverRefreshScheduler(), logger: logger, dragSource: dragSource);
        _disposables.Add(pane);
        _disposables.Add(explorer);
        explorer.Open(_folder);
        var view = new PreviewPaneView { DataContext = pane };
        var window = new Window { Width = 400, Height = 300, Content = view };
        window.Show();
        AppShellUiHelpers.Flush();
        return (window, explorer, pane, view);
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
    ///     Gets the center of a control in a window's coordinates.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="control">The control.</param>
    /// <returns>The point at the control's center.</returns>
    private static Point Center(Window window, Control control)
    {
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
               ?? throw new InvalidOperationException("The control is not in the window.");
    }

    /// <summary>
    ///     Waits for a task while the window's dispatcher keeps running, and fails the test when it takes too long.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="task">The task to wait for.</param>
    /// <returns>The task's result.</returns>
    private static async Task<T> WaitForAsync<T>(Task<T> task)
    {
        var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(task, winner);
        return await task;
    }

    /// <summary>
    ///     Waits until a condition holds while the window's dispatcher keeps running, and fails the test when it takes
    ///     too long.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold in time.");
            AppShellUiHelpers.Flush();
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    ///     What the view offered to the drag operation, read while the data was valid.
    /// </summary>
    /// <param name="PressWasLeftButton">Whether the press that started the drag was of the left button.</param>
    /// <param name="Effects">The allowed drop effects.</param>
    /// <param name="Formats">The formats the data offers.</param>
    /// <param name="FileName">The name of the file the data offers, or <see langword="null" />.</param>
    /// <param name="FileHeader">The first eight bytes of that file.</param>
    /// <param name="BitmapSize">The size of the bitmap the data offers, or <see langword="null" />.</param>
    private sealed record DragOffer(
        bool PressWasLeftButton,
        DragDropEffects Effects,
        IReadOnlyList<DataFormat> Formats,
        string? FileName,
        byte[] FileHeader,
        PixelSize? BitmapSize)
    {
        /// <summary>
        ///     Reads what a drag operation was given.
        /// </summary>
        /// <param name="press">The press that started the drag.</param>
        /// <param name="data">The data offered.</param>
        /// <param name="effects">The allowed effects.</param>
        /// <returns>What was offered.</returns>
        public static DragOffer Capture(PointerPressedEventArgs press, IDataTransfer data, DragDropEffects effects)
        {
            var file = data.TryGetFile();
            var header = new byte[8];
            if (file?.TryGetLocalPath() is { } path)
            {
                using var stream = File.OpenRead(path);
                stream.ReadExactly(header);
            }

            return new DragOffer(
                press.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed,
                effects,
                data.Formats.ToArray(),
                file?.Name,
                header,
                data.TryGetBitmap()?.PixelSize);
        }
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

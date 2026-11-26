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
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
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
        return RenderAsync(filePath, projectFolder, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, RenderSettings? settings, CancellationToken cancellationToken)
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

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image, double? dpi)
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
        Assert.Equal("red.svg: 40 x 20 px, svg", pane.StatusText);
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
        Assert.Equal("broken.svg: render failed", pane.StatusText);

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
        var running = (busy.IsVisible, placeholder.IsVisible, pane.StatusText);
        gate.Release(Path.Combine(_folder, "slow.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();

        // Assert: idle shows the placeholder, running shows the busy indicator and status, done shows neither
        Assert.Equal((false, true), idle);
        Assert.Equal((true, false, "Rendering slow.svg..."), running);
        Assert.Equal(2.0, busy.Bounds.Height);
        Assert.Equal(0.0, ScreenExtent(window, busy).Y - ScreenExtent(window, SettingsUiHelpers.Find<Grid>(window, "PreviewStage")).Y);
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

        // Assert: the expander appeared with a one-note header and the list holds the note, in a drawer inside the stage
        Assert.True(hiddenBefore);
        Assert.True(expander.IsVisible);
        Assert.Equal("1 note", expander.Header);
        var stage = SettingsUiHelpers.Find<Grid>(window, "PreviewStage");
        Assert.True(ScreenExtent(window, stage).Contains(ScreenExtent(window, expander).TopLeft));
        Assert.True(ScreenExtent(window, stage).Contains(ScreenExtent(window, expander).BottomRight));
        var list = SettingsUiHelpers.Find<ItemsControl>(window, "PreviewDiagnostics");
        var note = Assert.IsType<RenderDiagnostic>(Assert.Single(list.Items));
        Assert.Equal("image-access-denied", note.Code);

        File.Delete(Path.Combine(Path.GetDirectoryName(_folder)!, "canvaspilot-outside.png"));
        window.Close();
    }

    /// <summary>
    ///     Test that the diagnostics drawer, closed and open, lies above the zoom pill and does not cover it, on a stage that is
    ///     narrow (400 px).
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_DiagnosticsDrawerOnNarrowStage_DoesNotOverlapTheZoomPill()
    {
        // Arrange: an SVG that the render reports a note for, selected and drawn in a narrow pane, so that the pill and the
        // drawer both show
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(_folder)!, "canvaspilot-outside-pill.png"), "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "linked.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='10' height='10'>" +
            "<image href='../canvaspilot-outside-pill.png' width='10' height='10'/></svg>",
            TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        var expander = SettingsUiHelpers.Find<Expander>(window, "PreviewDiagnosticsExpander");
        var pill = SettingsUiHelpers.Find<Border>(window, "PreviewZoomPill");
        explorer.SelectFile(Path.Combine(_folder, "linked.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();

        // Act: measure the drawer and the pill while the drawer is closed, and again while it is open
        var closedDrawer = ScreenExtent(window, expander);
        var pillExtent = ScreenExtent(window, pill);
        expander.IsExpanded = true;
        AppShellUiHelpers.Flush();
        var openDrawer = ScreenExtent(window, expander);

        // Assert: both are shown, and the drawer is above the pill in both states
        Assert.True(expander.IsVisible);
        Assert.True(pill.IsVisible);
        Assert.False(closedDrawer.Intersects(pillExtent), $"The closed drawer {closedDrawer} covers the pill {pillExtent}.");
        Assert.False(openDrawer.Intersects(pillExtent), $"The open drawer {openDrawer} covers the pill {pillExtent}.");
        Assert.True(openDrawer.Bottom <= pillExtent.Top);

        File.Delete(Path.Combine(Path.GetDirectoryName(_folder)!, "canvaspilot-outside-pill.png"));
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

        // Act: turn fitting off with the Fit toggle, then on again
        var check = SettingsUiHelpers.Find<ToggleButton>(window, "PreviewFitToWindow");
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
    ///     Test that the Fit toggle shows when it is on: a tinted background, an outline, and an accent colored icon, and none
    ///     of them when it is off.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_FitToggle_ShowsItsOnStateWithTintOutlineAndAccentIcon()
    {
        // Arrange: a shown dark pane that shows a picture, with Fit on, which is how a picture opens
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var fit = SettingsUiHelpers.Find<ToggleButton>(window, "PreviewFitToWindow");
        var presenter = fit.GetVisualDescendants().OfType<ContentPresenter>().First(item => item.Name == "PART_ContentPresenter");
        var tint = ThemeUiHelpers.ColorOf("CpAccentTint", Avalonia.Styling.ThemeVariant.Dark);
        var accent = ThemeUiHelpers.ColorOf("CpAccentText", Avalonia.Styling.ThemeVariant.Dark);

        // Act: read how it looks with Fit on, then turn Fit off and read it again
        var onBackground = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color;
        var onOutline = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.BorderBrush).Color;
        var onThickness = presenter.BorderThickness;
        var onIcon = Assert.IsAssignableFrom<ISolidColorBrush>(fit.Foreground).Color;
        fit.IsChecked = false;
        AppShellUiHelpers.Flush();
        var offBackground = presenter.Background is ISolidColorBrush brush ? brush.Color : Colors.Transparent;
        var offThickness = presenter.BorderThickness;
        var offIcon = Assert.IsAssignableFrom<ISolidColorBrush>(fit.Foreground).Color;

        // Assert: on, it has the accent tint, an accent outline of one pixel, and the accent icon; off, it has none of them
        Assert.False(pane.FitToWindow);
        Assert.Equal(tint, onBackground);
        Assert.Equal(accent, onOutline);
        Assert.Equal(new Thickness(1), onThickness);
        Assert.Equal(accent, onIcon);
        Assert.Equal(0, offBackground.A);
        Assert.Equal(new Thickness(0), offThickness);
        Assert.NotEqual(accent, offIcon);

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
    ///     Test that the zoom pill zooms the picture and the overlay together: the buttons change the zoom to the
    ///     next step, the label says it, the overlay keeps the picture's pixel size and covers the picture exactly,
    ///     and clicking the label fits the picture again.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_ZoomPill_ZoomsPictureAndOverlayTogether()
    {
        // Arrange: a pane over a picture of 800 x 400 pixels, fitted to the pane, with an overlay
        var (window, pane, overlay) = await ShowLargePictureWithOverlayAsync();
        var image = SettingsUiHelpers.Find<Image>(window, "PreviewImage");
        var label = Assert.IsType<TextBlock>(SettingsUiHelpers.Find<Button>(window, "PreviewZoomLabel").Content);
        var fitLabel = label.Text;

        // Act: go to actual size, then out to 50 percent, then fit again through the label
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "PreviewZoomActual"));
        AppShellUiHelpers.Flush();
        var actual = (label.Text, ScreenExtent(window, image).Width, pane.FitToWindow);
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "PreviewZoomOut"));
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "PreviewZoomOut"));
        AppShellUiHelpers.Flush();
        var half = (label.Text, ScreenExtent(window, image), ScreenExtent(window, overlay));
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "PreviewZoomLabel"));
        AppShellUiHelpers.Flush();

        // Assert
        Assert.EndsWith("%", fitLabel);
        Assert.Equal(("100%", 800.0, false), actual);
        Assert.Equal("50%", half.Item1);
        Assert.Equal(400.0, half.Item2.Width, 3);
        Assert.Equal(200.0, half.Item2.Height, 3);
        Assert.Equal(half.Item2, half.Item3);
        Assert.Equal(new Size(800, 400), overlay.Bounds.Size);
        Assert.True(pane.FitToWindow);
        Assert.Equal(fitLabel, label.Text);

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
    ///     Test that the slot in the top bar is hidden while it is empty, shows the content assigned to it beside the fit
    ///     option and outside the scaled picture, and is hidden again when the slot is cleared.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_ToolbarSlot_EmptyThenAssigned_IsHiddenThenShownOutsideThePicture()
    {
        // Arrange: a shown pane with a picture and an empty slot in the top bar
        await File.WriteAllTextAsync(Path.Combine(_folder, "large.svg"), LargeSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        explorer.SelectFile(Path.Combine(_folder, "large.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var host = SettingsUiHelpers.Find<ContentControl>(window, "PreviewToolbar");
        var hiddenBefore = !host.IsVisible;

        // Act: assign a control of 100 x 20, then clear the slot
        var content = new Border { Width = 100, Height = 20, Background = Brushes.Transparent };
        pane.Toolbar = content;
        AppShellUiHelpers.Flush();
        var shown = (host.IsVisible, host.Content, ScreenExtent(window, content).Size);
        var scroller = SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller");
        var inScroller = content.GetVisualAncestors().Contains(scroller);
        pane.Toolbar = null;
        AppShellUiHelpers.Flush();

        // Assert: hidden when empty, showing exactly the assigned content at its own size although the picture is scaled
        // down, not inside the scaled picture, and hidden again after clearing
        Assert.True(hiddenBefore);
        Assert.True(shown.Item1);
        Assert.Same(content, shown.Item2);
        Assert.Equal(new Size(100, 20), shown.Item3);
        Assert.False(inScroller);
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
    ///     Test that right-clicking the shown picture opens the menu of the picture with a Copy image and an Export as PNG
    ///     item, in that order, with access keys and names for screen readers, that choosing each item runs the command
    ///     the host gave once, and that right-clicking the empty stage beside the picture opens the menu too.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_PictureMenu_RightClickOnPicture_OpensNamedItemsThatRunTheCommands()
    {
        // Arrange: a shown pane with a picture and two counting commands of a host
        var (window, _, pane, menu) = await ShowPictureWithMenuAsync();
        var copies = 0;
        var exports = 0;
        pane.CopyImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => copies++);
        pane.ExportPngCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => exports++);
        AppShellUiHelpers.Flush();
        var center = Center(window, SettingsUiHelpers.Find<Image>(window, "PreviewImage"));

        // Act: right-click the picture, read the items, and choose each; then close and right-click the empty stage
        RightClick(window, center);
        var openedOnPicture = menu.IsOpen;
        var items = menu.Items.OfType<MenuItem>().ToList();
        var usable = items.Select(item => item.IsEffectivelyEnabled).ToList();
        items[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        items[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
        AppShellUiHelpers.Flush();
        RightClick(window, new Point(20, 150));
        var openedOnStage = menu.IsOpen;
        menu.Close();

        // Assert: the menu opened both times; its items are named, in order, usable, and each ran its command once
        Assert.True(openedOnPicture);
        Assert.True(openedOnStage);
        Assert.Equal(["PictureMenuCopyImage", "PictureMenuExportPng"], items.Select(AutomationProperties.GetAutomationId));
        Assert.Equal(["_Copy image", "_Export as PNG..."], items.Select(item => item.Header));
        Assert.Equal(["Copy image", "Export as PNG"], items.Select(AutomationProperties.GetName));
        Assert.Equal("Picture", AutomationProperties.GetName(menu));
        Assert.Equal([true, true], usable);
        Assert.Equal(1, copies);
        Assert.Equal(1, exports);

        window.Close();
    }

    /// <summary>
    ///     Test that Shift+F10 and the Menu key open the menu of the picture while the stage has the keyboard focus, also
    ///     when Shift is released before F10, and that F10 alone does not.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_PictureMenu_ShiftF10OrMenuKeyOnFocusedStage_Opens()
    {
        // Arrange: a shown pane with a picture, commands of a host, and the keyboard focus on the stage
        var (window, _, pane, menu) = await ShowPictureWithMenuAsync();
        pane.CopyImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        pane.ExportPngCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        var stage = SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller");

        // Act: press Shift+F10, the Menu key, F10 alone, and Shift+F10 with Shift released before F10, each with the
        // stage focused and the menu closed before
        stage.Focus();
        PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
        var openedByShiftF10 = menu.IsOpen;
        menu.Close();
        AppShellUiHelpers.Flush();
        stage.Focus();
        PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
        var openedByMenuKey = menu.IsOpen;
        menu.Close();
        AppShellUiHelpers.Flush();
        stage.Focus();
        PressKey(window, PhysicalKey.F10, RawInputModifiers.None);
        var openedByF10 = menu.IsOpen;
        menu.Close();
        AppShellUiHelpers.Flush();
        stage.Focus();
        window.KeyPressQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.F10, RawInputModifiers.None);
        AppShellUiHelpers.Flush();
        var openedWithShiftReleasedFirst = menu.IsOpen;
        menu.Close();

        // Assert: Shift+F10, also with Shift released first, and the Menu key open the menu; F10 alone does not
        Assert.True(openedByShiftF10);
        Assert.True(openedByMenuKey);
        Assert.False(openedByF10);
        Assert.True(openedWithShiftReleasedFirst);

        window.Close();
    }

    /// <summary>
    ///     Test that Shift+F10 requests a context menu from the control inside the stage that has the keyboard focus,
    ///     the way the Menu key does: from a focused layer in the overlay slot it opens the menu of the picture, a press
    ///     whose focus moves before F10 is released opens nothing, and a focused control with a menu of its own opens
    ///     that menu instead of the menu of the picture.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_PictureMenu_ShiftF10OnFocusedControlInStage_RequestsTheMenuFromThatControl()
    {
        // Arrange: a shown pane with a picture, commands of a host, a focusable layer in the overlay slot, and a
        // focusable control with a menu of its own
        var (window, _, pane, menu) = await ShowPictureWithMenuAsync();
        pane.CopyImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        pane.ExportPngCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        var layer = new Border { Focusable = true, Background = Brushes.Transparent };
        pane.Overlay = layer;
        AppShellUiHelpers.Flush();
        var stage = SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller");
        var ownMenu = new ContextMenu { Items = { new MenuItem { Header = "Own" } } };
        var withOwnMenu = new Border { Focusable = true, Background = Brushes.Transparent, ContextMenu = ownMenu };

        // Act: with the layer focused press Shift+F10, then the Menu key; press Shift+F10 on the layer and release it
        // after the focus moved to the stage; then focus the control with its own menu and press Shift+F10
        var layerTookFocus = layer.Focus();
        PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
        var openedByShiftF10OnLayer = menu.IsOpen;
        menu.Close();
        AppShellUiHelpers.Flush();
        layer.Focus();
        PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
        var openedByMenuKeyOnLayer = menu.IsOpen;
        menu.Close();
        AppShellUiHelpers.Flush();
        layer.Focus();
        window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
        stage.Focus();
        window.KeyReleaseQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
        AppShellUiHelpers.Flush();
        var openedAfterFocusMoved = menu.IsOpen;
        menu.Close();
        AppShellUiHelpers.Flush();
        pane.Overlay = withOwnMenu;
        AppShellUiHelpers.Flush();
        withOwnMenu.Focus();
        PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
        var openedOwnMenu = ownMenu.IsOpen;
        var openedPictureMenuFromOwn = menu.IsOpen;
        ownMenu.Close();
        menu.Close();

        // Assert: the layer's keys open the menu of the picture, a moved focus opens nothing, and a control with its
        // own menu opens only that menu
        Assert.True(layerTookFocus);
        Assert.True(openedByShiftF10OnLayer);
        Assert.True(openedByMenuKeyOnLayer);
        Assert.False(openedAfterFocusMoved);
        Assert.True(openedOwnMenu);
        Assert.False(openedPictureMenuFromOwn);

        window.Close();
    }

    /// <summary>
    ///     Test that where the platform counts Shift+F10 as a menu key, as Windows does, one press asks for the menu of
    ///     the picture only once: it opens after a single opening, and while the menu is turned off it is asked once and
    ///     stays closed.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_PictureMenu_ShiftF10WherePlatformCountsItAsMenuKey_RequestsTheMenuOnce()
    {
        // Arrange: a shown pane with a picture, commands of a host, a focusable layer in the overlay slot, a count of
        // the menu's openings, and the platform's gestures for a context menu
        var (window, _, pane, menu) = await ShowPictureWithMenuAsync();
        pane.CopyImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        pane.ExportPngCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        var layer = new Border { Focusable = true, Background = Brushes.Transparent };
        pane.Overlay = layer;
        AppShellUiHelpers.Flush();
        var openings = 0;
        menu.Opening += (_, _) => openings++;
        var gestures = window.GetPlatformSettings()!.HotkeyConfiguration.OpenContextMenu;
        var shiftF10 = new KeyGesture(Key.F10, KeyModifiers.Shift);

        // Act: count Shift+F10 as a menu key, as Windows does; press it on the focused layer with the menu on, then
        // with the menu off
        bool openedWhenOn;
        int openingsWhenOn;
        bool openedWhenOff;
        int openingsWhenOff;
        gestures.Add(shiftF10);
        try
        {
            layer.Focus();
            PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
            openedWhenOn = menu.IsOpen;
            openingsWhenOn = openings;
            menu.Close();
            AppShellUiHelpers.Flush();
            pane.IsPictureMenuEnabled = false;
            layer.Focus();
            PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
            openedWhenOff = menu.IsOpen;
            openingsWhenOff = openings - openingsWhenOn;
        }
        finally
        {
            gestures.Remove(shiftF10);
        }

        // Assert: one opening opened the menu; while it was off it was asked once and stayed closed
        Assert.True(openedWhenOn);
        Assert.Equal(1, openingsWhenOn);
        Assert.False(openedWhenOff);
        Assert.Equal(1, openingsWhenOff);

        window.Close();
    }

    /// <summary>
    ///     Test that the items of the menu of the picture are unusable while the host's commands cannot run, while the
    ///     host gave no commands, and after a render failed, where right-clicking the error opens the menu.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_PictureMenu_CommandsCannotRunOrMissing_ItemsAreUnusable()
    {
        // Arrange: a shown pane with a picture, and a broken file in the project
        await File.WriteAllTextAsync(Path.Combine(_folder, "broken.svg"), "<svg><not closed", TestContext.Current.CancellationToken);
        var (window, explorer, pane, menu) = await ShowPictureWithMenuAsync();
        var runs = 0;
        var center = Center(window, SettingsUiHelpers.Find<Image>(window, "PreviewImage"));

        // Act: right-click the picture with no commands, then with commands that cannot run; then show the broken
        // file with commands that run only while a picture is shown, and right-click the error
        RightClick(window, center);
        var withoutCommands = UsableItems(menu);
        menu.Close();
        pane.CopyImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => runs++, () => false);
        pane.ExportPngCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => runs++, () => false);
        AppShellUiHelpers.Flush();
        RightClick(window, center);
        var cannotRun = UsableItems(menu);
        menu.Items.OfType<MenuItem>().First().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
        explorer.SelectFile(Path.Combine(_folder, "broken.svg"));
        await pane.RenderCompletion;
        pane.CopyImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => runs++, () => pane.HasImage);
        pane.ExportPngCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => runs++, () => pane.HasImage);
        AppShellUiHelpers.Flush();
        RightClick(window, Center(window, SettingsUiHelpers.Find<StackPanel>(window, "PreviewErrorPanel")));
        var openedOnError = menu.IsOpen;
        var afterFailure = UsableItems(menu);
        menu.Close();

        // Assert: the menu opens every time, and its items are unusable each time and ran nothing
        Assert.Equal([false, false], withoutCommands);
        Assert.Equal([false, false], cannotRun);
        Assert.True(openedOnError);
        Assert.Equal([false, false], afterFailure);
        Assert.Equal(0, runs);

        window.Close();
    }

    /// <summary>
    ///     Test that the menu of the picture does not open, by a right-click or from the keyboard, while the host has
    ///     turned it off, nor by a right-click on a control that floats over the stage (the tool bar slot, the notes
    ///     drawer, and the zoom pill), and opens again on the picture once it is turned on.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_PictureMenu_TurnedOffOrOnFloatingControls_DoesNotOpen()
    {
        // Arrange: a shown pane with a picture, commands of a host, a control in the tool bar slot, and a note in the
        // notes drawer
        var (window, _, pane, menu) = await ShowPictureWithMenuAsync();
        pane.CopyImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        pane.ExportPngCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        var toolbarContent = new Border { Width = 100, Height = 20, Background = Brushes.Transparent };
        pane.Toolbar = toolbarContent;
        pane.ShowNotes("picture-menu", [new RenderDiagnostic(RenderDiagnosticSeverity.Info, "picture-menu-note", "a note")]);
        AppShellUiHelpers.Flush();
        var drawer = SettingsUiHelpers.Find<Expander>(window, "PreviewDiagnosticsExpander");
        var center = Center(window, SettingsUiHelpers.Find<Image>(window, "PreviewImage"));
        var stage = SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller");

        // Act: turn the menu off and right-click the picture, press the Menu key and Shift+F10 on the stage; turn it on
        // and right-click the tool bar slot's control, the open notes drawer, the header of the closed drawer, and a
        // button of the zoom pill, then the picture
        pane.IsPictureMenuEnabled = false;
        RightClick(window, center);
        var openedByRightClickWhileOff = menu.IsOpen;
        stage.Focus();
        PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
        var openedByMenuKeyWhileOff = menu.IsOpen;
        stage.Focus();
        PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
        var openedByShiftF10WhileOff = menu.IsOpen;
        pane.IsPictureMenuEnabled = true;
        RightClick(window, Center(window, toolbarContent));
        var openedOnToolbar = menu.IsOpen;
        RightClick(window, Center(window, drawer));
        var openedOnOpenNotesDrawer = menu.IsOpen;
        pane.AreDiagnosticsExpanded = false;
        AppShellUiHelpers.Flush();
        RightClick(window, Center(window, drawer));
        var openedOnNotesDrawerHeader = menu.IsOpen;
        RightClick(window, Center(window, SettingsUiHelpers.Find<Button>(window, "PreviewZoomActual")));
        var openedOnZoomPill = menu.IsOpen;
        RightClick(window, center);
        var openedOnPictureWhenOn = menu.IsOpen;
        menu.Close();

        // Assert: nothing opened while off or over the floating controls; the picture opens it again once it is on
        Assert.True(drawer.IsVisible);
        Assert.False(openedByRightClickWhileOff);
        Assert.False(openedByMenuKeyWhileOff);
        Assert.False(openedByShiftF10WhileOff);
        Assert.False(openedOnToolbar);
        Assert.False(openedOnOpenNotesDrawer);
        Assert.False(openedOnNotesDrawerHeader);
        Assert.False(openedOnZoomPill);
        Assert.True(openedOnPictureWhenOn);

        window.Close();
    }

    /// <summary>
    ///     Test that neither the Menu key nor Shift+F10, also where the platform counts Shift+F10 as a menu key, opens the
    ///     menu of the picture while a control that floats over the stage has the keyboard focus (a control in the tool
    ///     bar slot, the header of the notes drawer, and a button of the zoom pill), while the Menu key on the focused
    ///     stage still opens it.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewPaneView_PictureMenu_MenuKeyOrShiftF10OnFloatingControls_DoesNotOpen()
    {
        // Arrange: a shown pane with a picture, commands of a host, a button in the tool bar slot, a note in the notes
        // drawer, the focusable floating controls, and the platform's gestures for a context menu
        var (window, _, pane, menu) = await ShowPictureWithMenuAsync();
        pane.CopyImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        pane.ExportPngCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        var toolbarButton = new Button { Content = "Tool" };
        pane.Toolbar = toolbarButton;
        pane.ShowNotes("picture-menu", [new RenderDiagnostic(RenderDiagnosticSeverity.Info, "picture-menu-note", "a note")]);
        AppShellUiHelpers.Flush();
        var drawer = SettingsUiHelpers.Find<Expander>(window, "PreviewDiagnosticsExpander");
        var drawerHeader = drawer.GetVisualDescendants().OfType<ToggleButton>().First();
        var zoomButton = SettingsUiHelpers.Find<Button>(window, "PreviewZoomActual");
        var stage = SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller");
        var gestures = window.GetPlatformSettings()!.HotkeyConfiguration.OpenContextMenu;
        var shiftF10 = new KeyGesture(Key.F10, KeyModifiers.Shift);

        // Act: on each floating control, take the focus and press the Menu key, Shift+F10, and Shift+F10 counted as a
        // menu key, as on Windows, recording whether the menu opened; then press the Menu key on the focused stage
        var tookFocus = new List<bool>();
        var openedOnFloating = new List<bool>();
        foreach (var control in new Control[] { toolbarButton, drawerHeader, zoomButton })
        {
            tookFocus.Add(control.Focus());
            PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
            openedOnFloating.Add(menu.IsOpen);
            menu.Close();
            control.Focus();
            PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
            openedOnFloating.Add(menu.IsOpen);
            menu.Close();
            gestures.Add(shiftF10);
            try
            {
                control.Focus();
                PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
                openedOnFloating.Add(menu.IsOpen);
                menu.Close();
            }
            finally
            {
                gestures.Remove(shiftF10);
            }
        }

        stage.Focus();
        PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
        var openedOnStage = menu.IsOpen;
        menu.Close();

        // Assert: every floating control took the focus and opened nothing, and the stage's Menu key opened the menu
        Assert.Equal([true, true, true], tookFocus);
        Assert.Equal(9, openedOnFloating.Count);
        Assert.DoesNotContain(true, openedOnFloating);
        Assert.True(openedOnStage);

        window.Close();
    }

    /// <summary>
    ///     Test that the Preview subsystem's menu of the picture, in the pane's view over the real render service and
    ///     image exporter given host-style commands, opens by a right-click, the Menu key, and Shift+F10, has items that
    ///     are usable only while a picture is shown, copies the shown picture and exports a PNG of its size when they are
    ///     chosen, and opens neither by a right-click nor by the Menu key while it is turned off.
    /// </summary>
    [AvaloniaFact]
    public async Task PreviewSubsystem_PictureMenu_PaneViewOverExporter_OpensAndRunsCommandsAndStaysClosedWhileOff()
    {
        // Arrange: a project with a 40 x 20 SVG and a broken one, a shown pane, an exporter over it with dialogs that
        // play the user, and host-style commands over the exporter that ask again whether they can run whenever the
        // exporter says so
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, token);
        await File.WriteAllTextAsync(Path.Combine(_folder, "broken.svg"), "<svg><not closed", token);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        var exporter = new ImageExporter(pane, new ImageRenderService());
        var dialogs = new RecordingExportDialogs { SaveFileToPick = Path.Combine(_folder, "menu.png") };
        var copy = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => exporter.CopyImageAsync(dialogs), () => exporter.CanExport);
        var export = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => exporter.ExportPngAsync(dialogs), () => exporter.CanExport);
        exporter.CanExportChanged += (_, _) =>
        {
            copy.NotifyCanExecuteChanged();
            export.NotifyCanExecuteChanged();
        };
        pane.CopyImageCommand = copy;
        pane.ExportPngCommand = export;
        AppShellUiHelpers.Flush();
        var menu = SettingsUiHelpers.Find<Grid>(window, "PreviewStage").ContextMenu
                   ?? throw new InvalidOperationException("The stage has no menu.");
        var stage = SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller");

        // Act: right-click the empty stage; show the picture, right-click it, and choose Copy image; press the Menu key
        // on the focused stage and choose Export as PNG; press Shift+F10 on the focused stage; turn the menu off,
        // right-click the picture, and press the Menu key on the focused stage; turn it on, show the broken file, and
        // right-click the error
        RightClick(window, new Point(20, 150));
        var openedOnEmptyStage = menu.IsOpen;
        var usableOnEmptyStage = UsableItems(menu);
        menu.Close();
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var shown = pane.Image;
        var picture = Center(window, SettingsUiHelpers.Find<Image>(window, "PreviewImage"));
        RightClick(window, picture);
        var openedOnPicture = menu.IsOpen;
        var usableOnPicture = UsableItems(menu);
        menu.Items.OfType<MenuItem>().ElementAt(0).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await WaitForAsync(() => copy.ExecutionTask?.IsCompleted == true);
        menu.Close();
        stage.Focus();
        PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
        var openedByMenuKey = menu.IsOpen;
        menu.Items.OfType<MenuItem>().ElementAt(1).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await WaitForAsync(() => export.ExecutionTask?.IsCompleted == true);
        menu.Close();
        stage.Focus();
        PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
        var openedByShiftF10 = menu.IsOpen;
        menu.Close();
        pane.IsPictureMenuEnabled = false;
        RightClick(window, picture);
        var openedByRightClickWhileOff = menu.IsOpen;
        stage.Focus();
        PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
        var openedByMenuKeyWhileOff = menu.IsOpen;
        pane.IsPictureMenuEnabled = true;
        explorer.SelectFile(Path.Combine(_folder, "broken.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        RightClick(window, Center(window, SettingsUiHelpers.Find<StackPanel>(window, "PreviewErrorPanel")));
        var openedOnError = menu.IsOpen;
        var usableOnError = UsableItems(menu);
        menu.Close();

        // Assert: the menu opened every way while on, with items usable only while the picture was shown, and not while
        // off; the shown picture was copied and exported as a 40 x 20 PNG, and no message was shown
        Assert.True(openedOnEmptyStage);
        Assert.Equal([false, false], usableOnEmptyStage);
        Assert.True(openedOnPicture);
        Assert.Equal([true, true], usableOnPicture);
        Assert.True(openedByMenuKey);
        Assert.True(openedByShiftF10);
        Assert.False(openedByRightClickWhileOff);
        Assert.False(openedByMenuKeyWhileOff);
        Assert.True(openedOnError);
        Assert.Equal([false, false], usableOnError);
        Assert.NotNull(shown);
        Assert.Same(shown, Assert.Single(dialogs.CopiedImages));
        var png = await File.ReadAllBytesAsync(Path.Combine(_folder, "menu.png"), token);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.Equal(40, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(20, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        Assert.Empty(dialogs.Messages);

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
    ///     Shows a picture of 40 x 20 pixels in a pane in a window, and gives the menu of the picture on its stage.
    /// </summary>
    /// <returns>The window, the explorer, the pane view model, and the menu of the picture.</returns>
    private async Task<(Window Window, ProjectExplorerViewModel Explorer, PreviewPaneViewModel Pane, ContextMenu Menu)> ShowPictureWithMenuAsync()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), HalfRedSvg, TestContext.Current.CancellationToken);
        var (window, explorer, pane) = ShowPane(new ImageRenderService());
        explorer.SelectFile(Path.Combine(_folder, "red.svg"));
        await pane.RenderCompletion;
        AppShellUiHelpers.Flush();
        var menu = SettingsUiHelpers.Find<Grid>(window, "PreviewStage").ContextMenu
                   ?? throw new InvalidOperationException("The stage has no menu.");
        return (window, explorer, pane, menu);
    }

    /// <summary>
    ///     Presses and releases the right button at a point of a window, the way a user right-clicks.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The point, in the window's coordinates.</param>
    private static void RightClick(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        AppShellUiHelpers.Flush();
    }

    /// <summary>
    ///     Presses and releases a key with modifiers on a window, the way a user types it.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="key">The physical key.</param>
    /// <param name="modifiers">The modifiers held down.</param>
    private static void PressKey(Window window, PhysicalKey key, RawInputModifiers modifiers)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        AppShellUiHelpers.Flush();
    }

    /// <summary>
    ///     Reads whether each item of a menu is usable, in the order of the items.
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <returns>Whether each item is usable.</returns>
    private static bool[] UsableItems(ContextMenu menu)
    {
        return menu.Items.OfType<MenuItem>().Select(item => item.IsEffectivelyEnabled).ToArray();
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
    ///     Export dialogs for the headless tests that play the user: they answer the save picker with a set path, put
    ///     copied pictures in a list instead of on the clipboard, and record the messages they were asked to show.
    /// </summary>
    private sealed class RecordingExportDialogs : IExportDialogs
    {
        /// <summary>
        ///     Gets or sets the path the save picker returns, or <see langword="null" /> to imitate canceling.
        /// </summary>
        public string? SaveFileToPick { get; set; }

        /// <summary>
        ///     Gets the pictures that were copied, in order.
        /// </summary>
        public List<RenderedImage> CopiedImages { get; } = [];

        /// <summary>
        ///     Gets the messages shown, as title and message.
        /// </summary>
        public List<(string Title, string Message)> Messages { get; } = [];

        /// <inheritdoc />
        public Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string filterName, string filterPattern)
        {
            return Task.FromResult(SaveFileToPick);
        }

        /// <inheritdoc />
        public Task<ExportPreset?> PickExportPresetAsync(IReadOnlyList<ExportPreset> presets, ExportPreset? last)
        {
            return Task.FromResult<ExportPreset?>(presets[0]);
        }

        /// <inheritdoc />
        public Task<bool> CopyImageToClipboardAsync(RenderedImage image)
        {
            CopiedImages.Add(image);
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public Task ShowMessageAsync(string title, string message)
        {
            Messages.Add((title, message));
            return Task.CompletedTask;
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

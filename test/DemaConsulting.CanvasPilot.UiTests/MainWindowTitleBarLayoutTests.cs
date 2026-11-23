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
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AppShellSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless tests of the layout of the title bar next to the window buttons that Avalonia draws over its right end,
///     and of the rule that the window has no full-screen mode.
/// </summary>
/// <remarks>
///     The headless platform draws no window frame, so the tests put the real overlay of the drawn decorations (built from
///     the template of the theme, three buttons without the full-screen one, as <c>TitleBarDecorations</c> asks for) over
///     the title bar of the real window, where the window of Windows has it, and measure and draw the result. When the
///     <c>CANVASPILOT_SCREENSHOT_DIR</c> folder is set, the pictures are written there as well, so that a person can look
///     at them.
/// </remarks>
public sealed class MainWindowTitleBarLayoutTests : IDisposable
{
    /// <summary>
    ///     The height of the title bar of the window.
    /// </summary>
    private const double TitleBarHeight = 44;

    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <summary>
    ///     The composition created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ApplicationComposition> _compositions = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var composition in _compositions)
        {
            composition.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the window asks for the window buttons that it keeps and not for the full-screen button, so that the
    ///     user cannot enter a mode whose way back is a pop-up bar that is hard to find.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_TitleBarDecorations_AskForNoFullScreenButton()
    {
        // Arrange and act: the composed window
        var (_, window) = CreateWindow();
        var decorations = WindowDrawnDecorations.GetTitleBarDecorations(window);

        // Assert: minimize, maximize, and close are asked for, and full screen is not
        Assert.True(decorations.HasFlag(TitleBarDecorations.MinimizeButton));
        Assert.True(decorations.HasFlag(TitleBarDecorations.MaximizeButton));
        Assert.True(decorations.HasFlag(TitleBarDecorations.CloseButton));
        Assert.False(decorations.HasFlag(TitleBarDecorations.FullScreenButton));
        window.Close();
    }

    /// <summary>
    ///     Test that the window never stays in full screen, whatever asks for it (a program, the system, or a key), and that
    ///     it keeps the maximized and the normal state.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_WindowState_FullScreenIsTurnedBackToNormal()
    {
        // Arrange: a shown window
        var (_, window) = CreateWindow();
        window.Show();
        AppShellUiHelpers.Flush();

        // Act: ask for the states one after the other
        window.WindowState = WindowState.FullScreen;
        var afterFullScreen = window.WindowState;
        window.WindowState = WindowState.Maximized;
        var afterMaximized = window.WindowState;
        window.WindowState = WindowState.FullScreen;
        var afterFullScreenFromMaximized = window.WindowState;

        // Assert
        Assert.Equal(WindowState.Normal, afterFullScreen);
        Assert.Equal(WindowState.Maximized, afterMaximized);
        Assert.Equal(WindowState.Normal, afterFullScreenFromMaximized);
        window.Close();
    }

    /// <summary>
    ///     Test that no key of the window and no menu item of it asks for full screen (F11 is not bound).
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_KeyF11_DoesNotEnterFullScreen()
    {
        // Arrange: a shown window
        var (_, window) = CreateWindow();
        window.Show();
        AppShellUiHelpers.Flush();

        // Act: press F11
        window.KeyPressQwerty(PhysicalKey.F11, RawInputModifiers.None);
        AppShellUiHelpers.Flush();

        // Assert
        Assert.NotEqual(WindowState.FullScreen, window.WindowState);
        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<MenuItem>(),
            item => item.InputGesture is { Key: Key.F11 });
        window.Close();
    }

    /// <summary>
    ///     Test that, with the window buttons laid out as Windows lays them out, the room that the title bar keeps is the
    ///     measured width of the buttons, and that the provider pill, the last thing in the title bar, ends clear of the left
    ///     edge of the first button with a normal space, at the widths of a large screen, a laptop, and the smallest window,
    ///     and in the maximized state. The pictures are drawn for a person to look at.
    /// </summary>
    /// <param name="width">The width of the window.</param>
    /// <param name="maximized">Whether the window is maximized.</param>
    [AvaloniaTheory]
    [InlineData(1920, false)]
    [InlineData(1280, false)]
    [InlineData(960, false)]
    [InlineData(1920, true)]
    public void MainWindowView_TitleBar_PillEndsClearOfTheWindowButtons(double width, bool maximized)
    {
        // Arrange: the window with the buttons over its title bar, at the width
        var (_, window) = CreateWindow();
        var buttons = AddCaptionButtons(window);
        var folder = ScreenshotSupport.GetRequestedFolder() ?? _root;
        var state = maximized ? "maximized" : "normal";
        window.WindowState = maximized ? WindowState.Maximized : WindowState.Normal;

        // Act: lay out, keep the room as the window does on Windows, and draw
        ScreenshotSupport.Capture(window, folder, $"titlebar-{width}-{state}-before-reserve.png", width, 600, ThemeVariant.Dark);
        window.UpdateCaptionReserve(isExtended: true, isWindows: true, isMacOs: false);
        var picture = ScreenshotSupport.Capture(window, folder, $"titlebar-{width}-{state}.png", width, 600, ThemeVariant.Dark);
        var reserve = window.FindControl<Border>("TitleBarCaptionReserve")!;
        var pill = SettingsUiHelpers.Find<Button>(window, "ProviderPill");
        var panelLeft = buttons.TranslatePoint(new Point(0, 0), window)!.Value.X;
        var pillRight = pill.TranslatePoint(new Point(pill.Bounds.Width, 0), window)!.Value.X;
        var visibleButtons = buttons.Children.OfType<Border>().Where(button => button.IsEffectivelyVisible).ToList();

        // Assert: three buttons remain; the room kept is what they cover, 139 for three of 45 with the space between, and
        // the pill ends at least 12 before the first button and does not leave the window
        Assert.True(File.Exists(picture));
        Assert.Equal(3, visibleButtons.Count);
        Assert.Equal(width - panelLeft, reserve.Width, 0.5);
        Assert.InRange(reserve.Width, 139, MainWindowView.WindowsCaptionButtonsWidth);
        Assert.True(pillRight <= panelLeft - 12, $"The pill ends at {pillRight} and the buttons start at {panelLeft}.");
        Assert.True(pillRight <= width - reserve.Width - 12);
        Assert.True(pill.Bounds.Width > 0 && pill.IsEffectivelyVisible);
        window.Close();
    }

    /// <summary>
    ///     Test that, until the buttons are laid out, the room that the title bar keeps on Windows is the width of three
    ///     buttons (140), and that it is the measured width once they are, which differs from the fallback when the theme
    ///     draws them differently.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_CaptionReserve_FallsBackToTheConstantUntilTheButtonsAreMeasured()
    {
        // Arrange: a window with no buttons
        var (_, window) = CreateWindow();
        window.Show();
        AppShellUiHelpers.Flush();
        var reserve = window.FindControl<Border>("TitleBarCaptionReserve")!;

        // Act: keep the room with no buttons, then with buttons
        window.UpdateCaptionReserve(isExtended: true, isWindows: true, isMacOs: false);
        var withoutButtons = (reserve.Width, MainWindowView.MeasureCaptionButtonsReserve(window));
        var buttons = AddCaptionButtons(window);
        AppShellUiHelpers.Flush();
        window.UpdateCaptionReserve(isExtended: true, isWindows: true, isMacOs: false);
        var measured = MainWindowView.MeasureCaptionButtonsReserve(window);

        // Assert: the fallback is 140, and the measured width is the width of the buttons from the edge of the window
        Assert.Equal((140d, (double?)null), withoutButtons);
        Assert.Equal(window.Bounds.Width - buttons.TranslatePoint(new Point(0, 0), window)!.Value.X, measured!.Value, 0.5);
        Assert.Equal(measured.Value, reserve.Width, 0.5);
        window.Close();
    }

    /// <summary>
    ///     Makes the main window on the temporary root.
    /// </summary>
    /// <returns>The composition and the window.</returns>
    private (ApplicationComposition Composition, MainWindowView Window) CreateWindow()
    {
        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        return (composition, new MainWindowView(composition.Shell));
    }

    /// <summary>
    ///     Puts the overlay of the drawn decorations of the theme over the window, where the host of a real window has it,
    ///     with the full-screen button hidden as the theme does for a window that does not ask for it.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The panel that holds the buttons.</returns>
    private static StackPanel AddCaptionButtons(MainWindowView window)
    {
        var theme = Assert.IsType<ControlTheme>(Application.Current!.FindResource(typeof(WindowDrawnDecorations)));
        var template = Assert.IsAssignableFrom<IWindowDrawnDecorationsTemplate>(
            theme.Setters.OfType<Setter>().Single(setter => setter.Property == WindowDrawnDecorations.TemplateProperty).Value);
        var overlay = Assert.IsAssignableFrom<Control>(template.Build().Result.Overlay);

        // The window keeps its content and gets the overlay over it, over the full size of the window
        var content = Assert.IsAssignableFrom<Control>(window.Content);
        window.Content = null;
        var host = new Grid();
        host.Children.Add(content);
        host.Children.Add(overlay);
        window.Content = host;

        var panel = overlay.GetLogicalDescendants().OfType<StackPanel>().Single(candidate => candidate.Name == "PART_OverlayPanel");
        panel.Height = TitleBarHeight;

        // A template that is built outside the host does not get the styles of its buttons (their width, their bare
        // template, their colors), so each button is replaced by a cell of the width that the theme gives it (its
        // CaptionButtonWidth) that holds the glyph of the button, drawn white because the foreground of the theme is not
        // resolved here. The full-screen button is left out, as the theme does for a window whose TitleBarDecorations leave
        // it out. The space between the cells and the glyphs are the template's, and a tint on the panel shows in a
        // picture where the buttons are.
        var width = Assert.IsType<double>(Application.Current!.FindResource("CaptionButtonWidth"));
        var cells = new List<Border>();
        foreach (var button in panel.Children.OfType<Button>().Where(candidate => candidate.Name != "PART_FullScreenButton"))
        {
            var glyph = Assert.IsType<Viewbox>(button.Content);
            button.Content = null;
            Assert.IsType<Avalonia.Controls.Shapes.Path>(glyph.Child).Fill = Brushes.White;
            cells.Add(new Border { Name = button.Name, Width = width, Background = Brushes.Transparent, Child = glyph });
        }

        panel.Children.Clear();
        panel.Children.AddRange(cells);
        panel.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x00, 0xFF));
        return panel;
    }
}

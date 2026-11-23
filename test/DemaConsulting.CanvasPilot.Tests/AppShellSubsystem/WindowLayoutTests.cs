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
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the pure logic of the window layout: where the main window opens on the screens that are connected
///     now, and how the widths of the explorer and the chat column are made to fit the window.
/// </summary>
public sealed class WindowLayoutTests
{
    /// <summary>
    ///     One screen of 1920 by 1080 pixels.
    /// </summary>
    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);

    /// <summary>
    ///     A second screen to the right of the first.
    /// </summary>
    private static readonly PixelRect SecondScreen = new(1920, 0, 1920, 1080);

    /// <summary>
    ///     Test that a saved layout that lies on a screen is kept as it is, with its state.
    /// </summary>
    /// <param name="saved">Whether the window was saved maximized.</param>
    /// <param name="expected">The state that the window opens in.</param>
    [Theory]
    [InlineData(SavedWindowState.Normal, WindowState.Normal)]
    [InlineData(SavedWindowState.Maximized, WindowState.Maximized)]
    public void WindowPlacement_Resolve_LayoutOnAScreen_IsKept(SavedWindowState saved, WindowState expected)
    {
        // Arrange: a layout that lies fully on the screen
        var settings = new WindowSettings { Width = 1200, Height = 800, Left = 100, Top = 50, State = saved };

        // Act: resolve it for the one screen
        var placement = WindowPlacement.Resolve(settings, [Screen], 1);

        // Assert: nothing changed
        Assert.Equal(new WindowPlacement(1200, 800, new PixelPoint(100, 50), expected), placement);
    }

    /// <summary>
    ///     Test that a window saved on a screen that was removed, which leaves nothing of it on a screen, is placed by the
    ///     system, and that a layout with no place or with no screens known is not given a place.
    /// </summary>
    [Fact]
    public void WindowPlacement_Resolve_ScreenRemovedOrUnknown_LeavesThePlaceToTheSystem()
    {
        // Arrange: a window that was on the second screen, and one with no saved place
        var onSecond = new WindowSettings { Width = 1200, Height = 800, Left = 2200, Top = 100 };
        var noPlace = new WindowSettings { Width = 1200, Height = 800 };

        // Act: resolve for the first screen only, for no screens, and for no place
        var removed = WindowPlacement.Resolve(onSecond, [Screen], 1);
        var unknown = WindowPlacement.Resolve(onSecond, [], 1);
        var none = WindowPlacement.Resolve(noPlace, [Screen], 1);

        // Assert: the size is kept and the place is left to the system
        Assert.Equal(new WindowPlacement(1200, 800, null, WindowState.Normal), removed);
        Assert.Equal(new WindowPlacement(1200, 800, null, WindowState.Normal), unknown);
        Assert.Equal(new WindowPlacement(1200, 800, null, WindowState.Normal), none);
    }

    /// <summary>
    ///     Test that a window that hangs over the edge of a screen is moved back inside it, that one on the second screen
    ///     stays there, and that a window larger than the screen is made as large as the screen holds.
    /// </summary>
    [Fact]
    public void WindowPlacement_Resolve_HangingOrTooLarge_IsMovedAndShrunkToTheScreen()
    {
        // Arrange: a window that hangs over the right and bottom edges, one on the second screen, and one that is too large
        var hanging = new WindowSettings { Width = 1200, Height = 800, Left = 1000, Top = 700 };
        var onSecond = new WindowSettings { Width = 1200, Height = 800, Left = 2200, Top = 100 };
        var large = new WindowSettings { Width = 4000, Height = 3000, Left = 0, Top = 0 };

        // Act: resolve them for two screens
        var moved = WindowPlacement.Resolve(hanging, [Screen, SecondScreen], 1);
        var stays = WindowPlacement.Resolve(onSecond, [Screen, SecondScreen], 1);
        var shrunk = WindowPlacement.Resolve(large, [Screen, SecondScreen], 1);

        // Assert: the hanging window lies inside the screen again, the other stays, and the large one fits
        Assert.Equal(new PixelPoint(720, 280), moved.Position);
        Assert.Equal(new PixelPoint(2200, 100), stays.Position);
        Assert.Equal((1920d, 1080d, new PixelPoint(0, 0)), (shrunk.Width, shrunk.Height, shrunk.Position));
    }

    /// <summary>
    ///     Test that the size is kept to the smallest of the window, and that the display scale is taken into account when
    ///     the window is fitted to a screen.
    /// </summary>
    [Fact]
    public void WindowPlacement_Resolve_TooSmallOrScaled_KeepsTheSmallestSizeAndUsesTheScale()
    {
        // Arrange: a window saved smaller than the window may be, and one that is as large as a screen at 200 percent
        var small = new WindowSettings { Width = 100, Height = 100, Left = 10, Top = 10 };
        var scaled = new WindowSettings { Width = 1500, Height = 900, Left = 0, Top = 0 };

        // Act: resolve them, the second for a screen that holds 1500 by 900 device-independent pixels at 200 percent
        var minimum = WindowPlacement.Resolve(small, [Screen], 1);
        var fitted = WindowPlacement.Resolve(scaled, [new PixelRect(0, 0, 3000, 1800)], 2);
        var halfScreen = WindowPlacement.Resolve(scaled, [new PixelRect(0, 0, 2000, 1200)], 2);

        // Assert: the smallest size, the size kept at the right scale, and a size shrunk to what the screen holds
        Assert.Equal((960d, 600d), (minimum.Width, minimum.Height));
        Assert.Equal((1500d, 900d), (fitted.Width, fitted.Height));
        Assert.Equal((1000d, 600d), (halfScreen.Width, halfScreen.Height));
    }

    /// <summary>
    ///     Test that the resolution refuses missing arguments.
    /// </summary>
    [Fact]
    public void WindowPlacement_Resolve_NullArguments_Throw()
    {
        // Arrange: valid settings, and a valid list of screens
        var settings = new WindowSettings();
        PixelRect[] screens = [Screen];

        // Act: resolve with each argument missing in turn
        var missingSettings = Record.Exception(() => WindowPlacement.Resolve(null!, screens, 1));
        var missingScreens = Record.Exception(() => WindowPlacement.Resolve(settings, null!, 1));

        // Assert: each argument is checked
        Assert.IsType<ArgumentNullException>(missingSettings);
        Assert.IsType<ArgumentNullException>(missingScreens);
    }

    /// <summary>
    ///     Test that the chosen widths are used when the window is wide enough for them and the stage, and are kept within
    ///     their limits.
    /// </summary>
    [Fact]
    public void WorkspaceLayout_Fit_WideWindow_UsesTheChosenWidthsWithinTheirLimits()
    {
        // Arrange: a wide window, the default widths, and widths beyond the limits
        const double wideWindow = 1600;

        // Act: fit the default widths into the wide window, and widths beyond the limits
        var normal = WorkspaceLayout.Fit(wideWindow, 264, 400, chatVisible: true);
        var beyond = WorkspaceLayout.Fit(3000, 50, 2000, chatVisible: true);

        // Assert: the chosen widths, and the widths clamped to their limits
        Assert.Equal((264d, 400d), normal);
        Assert.Equal((180d, 640d), beyond);
    }

    /// <summary>
    ///     Test that a window that is too narrow takes room from the explorer first, down to its smallest width, and only
    ///     then from the chat column, so that the stage never gets narrower than it may be.
    /// </summary>
    /// <param name="totalWidth">The width of the workspace.</param>
    /// <param name="explorer">The expected width of the explorer.</param>
    /// <param name="chat">The expected width of the chat column.</param>
    [Theory]
    [InlineData(1100, 264, 400)]
    [InlineData(1000, 238, 400)]
    [InlineData(960, 198, 400)]
    [InlineData(900, 180, 358)]
    [InlineData(862, 180, 320)]
    [InlineData(700, 180, 320)]
    public void WorkspaceLayout_Fit_NarrowWindow_TakesRoomFromTheExplorerFirst(double totalWidth, double explorer, double chat)
    {
        // Arrange: the default widths of the explorer and the chat column, in the theory data's window
        const double defaultExplorer = 264;
        const double defaultChat = 400;

        // Act: fit the default widths into the window
        var fitted = WorkspaceLayout.Fit(totalWidth, defaultExplorer, defaultChat, chatVisible: true);

        // Assert: the expected widths, and the stage keeps at least its smallest width unless the window is below the least
        Assert.Equal((explorer, chat), fitted);
        if (totalWidth >= 862)
        {
            Assert.True(totalWidth - fitted.Explorer - fitted.Chat - (2 * WorkspaceLayout.SplitterWidth) >= WorkspaceLayout.MinimumStageWidth);
        }
    }

    /// <summary>
    ///     Test that with no chat shown the chat column has no width and does not take room from the explorer.
    /// </summary>
    [Fact]
    public void WorkspaceLayout_Fit_ChatNotShown_HasNoChatWidth()
    {
        // Arrange: a narrow and a wide window, and the default widths
        const double narrowWindow = 600;
        const double wideWindow = 1600;

        // Act: fit the widths with no chat into both windows
        var narrow = WorkspaceLayout.Fit(narrowWindow, 264, 400, chatVisible: false);
        var wide = WorkspaceLayout.Fit(wideWindow, 264, 400, chatVisible: false);

        // Assert: no chat width, and the explorer gives up room only when the stage needs it
        Assert.Equal((239d, 0d), narrow);
        Assert.Equal((264d, 0d), wide);
    }
}

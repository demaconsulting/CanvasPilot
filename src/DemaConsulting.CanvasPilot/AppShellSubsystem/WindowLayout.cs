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
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Where the main window opens: the size, place, and state that were saved, made to fit the screens that are
///     connected now. It is pure logic with no window, so it can be tested with made-up screens.
/// </summary>
/// <param name="Width">The width of the window in device-independent pixels.</param>
/// <param name="Height">The height of the window in device-independent pixels.</param>
/// <param name="Position">The place of the left top corner in device pixels, or <see langword="null" /> to let the system place the window.</param>
/// <param name="State">The state the window opens in.</param>
public sealed record WindowPlacement(double Width, double Height, PixelPoint? Position, WindowState State)
{
    /// <summary>
    ///     The least width of the saved window that must still be on a screen, in device pixels, for its place to be kept.
    /// </summary>
    private const int MinimumVisibleWidth = 160;

    /// <summary>
    ///     The least height of the saved window that must still be on a screen, in device pixels, for its place to be kept.
    /// </summary>
    private const int MinimumVisibleHeight = 80;

    /// <summary>
    ///     Makes the saved layout of the window fit the screens that are connected now. The size is kept within the
    ///     smallest the window may have and the largest the best screen holds; the place is kept when enough of the window
    ///     is on a screen, moved just far enough to lie inside it, and dropped (so the system places the window) when a
    ///     screen was removed and almost none of the window is left on a screen.
    /// </summary>
    /// <param name="saved">The saved layout.</param>
    /// <param name="workAreas">The usable areas of the screens, in device pixels. An empty list means the screens are not known.</param>
    /// <param name="scaling">The display scale of the screens, for example 1.5 for 150 percent.</param>
    /// <returns>Where the window should open.</returns>
    public static WindowPlacement Resolve(WindowSettings saved, IReadOnlyList<PixelRect> workAreas, double scaling)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(workAreas);

        var scale = scaling > 0 ? scaling : 1;
        var state = saved.State == SavedWindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
        double width = Math.Max(saved.Width, WindowSettings.MinimumWidth);
        double height = Math.Max(saved.Height, WindowSettings.MinimumHeight);

        // With no screens known, nothing can be fitted: the system places the window
        if (workAreas.Count == 0 || saved.Left is not { } left || saved.Top is not { } top)
        {
            return new WindowPlacement(width, height, null, state);
        }

        // The window is as large as it was, but not larger than the screen that holds most of it, unless that would make it
        // smaller than it may be
        var rectangle = new PixelRect(left, top, (int)Math.Round(width * scale), (int)Math.Round(height * scale));
        var best = workAreas.OrderByDescending(area => IntersectionArea(area, rectangle)).First();
        var visible = best.Intersect(rectangle);
        if (visible.Width < MinimumVisibleWidth || visible.Height < MinimumVisibleHeight)
        {
            return new WindowPlacement(width, height, null, state);
        }

        width = Math.Max(WindowSettings.MinimumWidth, Math.Min(width, best.Width / scale));
        height = Math.Max(WindowSettings.MinimumHeight, Math.Min(height, best.Height / scale));
        var fitted = new PixelSize((int)Math.Round(width * scale), (int)Math.Round(height * scale));
        var x = Math.Max(best.X, Math.Min(left, best.Right - fitted.Width));
        var y = Math.Max(best.Y, Math.Min(top, best.Bottom - fitted.Height));
        return new WindowPlacement(width, height, new PixelPoint(x, y), state);
    }

    /// <summary>
    ///     Measures how much of a rectangle lies inside an area.
    /// </summary>
    /// <param name="area">The area.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The size of the overlap in square pixels, or zero when they do not meet.</returns>
    private static long IntersectionArea(PixelRect area, PixelRect rectangle)
    {
        var overlap = area.Intersect(rectangle);
        return (long)overlap.Width * overlap.Height;
    }
}

/// <summary>
///     How the widths of the explorer and the chat column are made to fit the window, so that the stage in the middle never
///     gets narrower than it may be and nothing is cropped. It is pure logic with no window.
/// </summary>
public static class WorkspaceLayout
{
    /// <summary>
    ///     The least width of the stage in the middle.
    /// </summary>
    public const double MinimumStageWidth = 360;

    /// <summary>
    ///     The width of the line of a splitter that takes room in the layout; the hit area around it is wider and overlaps
    ///     the panes.
    /// </summary>
    public const double SplitterWidth = 1;

    /// <summary>
    ///     Works out the widths that the explorer and the chat column get in a window of a width. They are the widths that
    ///     the user chose, kept within their limits, and where the window is too narrow for them and the stage the explorer
    ///     gives up room first, down to its smallest width, and then the chat column.
    /// </summary>
    /// <param name="totalWidth">The width of the area of the workspace.</param>
    /// <param name="explorer">The width of the explorer that the user chose.</param>
    /// <param name="chat">The width of the chat column that the user chose.</param>
    /// <param name="chatVisible">Whether the chat column is shown, which it is while a project is open.</param>
    /// <returns>The widths to use; the chat width is zero while the column is not shown.</returns>
    public static (double Explorer, double Chat) Fit(double totalWidth, double explorer, double chat, bool chatVisible)
    {
        var wantedExplorer = Math.Clamp(explorer, WindowSettings.MinimumExplorerWidth, WindowSettings.MaximumExplorerWidth);
        var wantedChat = chatVisible ? Math.Clamp(chat, WindowSettings.MinimumChatWidth, WindowSettings.MaximumChatWidth) : 0;
        var splitters = chatVisible ? 2 * SplitterWidth : SplitterWidth;
        var over = wantedExplorer + wantedChat + splitters + MinimumStageWidth - totalWidth;
        if (over <= 0)
        {
            return (wantedExplorer, wantedChat);
        }

        // The explorer gives up room first, then the chat column, and neither goes below its smallest width
        var fromExplorer = Math.Min(over, wantedExplorer - WindowSettings.MinimumExplorerWidth);
        over -= fromExplorer;
        var fromChat = chatVisible ? Math.Min(over, wantedChat - WindowSettings.MinimumChatWidth) : 0;
        return (wantedExplorer - fromExplorer, wantedChat - fromChat);
    }
}

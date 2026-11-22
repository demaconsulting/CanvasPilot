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

using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using DemaConsulting.CanvasPilot.AppShellSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless tests of the screenshot tool: one that always runs and proves that a window is drawn into a PNG of
///     the requested size, and the opt-in smoke test that writes a picture of the main window to the folder that
///     the screenshot script names.
/// </summary>
public sealed class ScreenshotSmokeTests : IDisposable
{
    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <summary>
    ///     The compositions created by a test, disposed with the fixture so no watcher outlives it.
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
    ///     Test that a drawn window is saved as a PNG of exactly the requested size that shows more than one color,
    ///     so the font and the theme were really drawn.
    /// </summary>
    /// <param name="dark">Whether the dark theme variant is drawn.</param>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScreenshotSupport_Capture_WritesPngOfRequestedSize(bool dark)
    {
        // Arrange: the main window over temporary folders
        var window = CreateWindow();
        var folder = Path.Combine(_root, "shots");
        var variant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

        // Act: draw it at 960 x 600 and save the picture
        var path = ScreenshotSupport.Capture(window, folder, "smoke.png", 960, 600, variant);
        window.Close();

        // Assert: the file exists, has the requested size, and is not a single flat color
        Assert.True(File.Exists(path));
        using var picture = new Bitmap(path);
        Assert.Equal(960, picture.PixelSize.Width);
        Assert.Equal(600, picture.PixelSize.Height);
        Assert.True(CountDistinctSampledColors(picture) > 1);
    }

    /// <summary>
    ///     Opt-in smoke test: draws the main window in both themes into the folder named by the screenshot
    ///     environment variable. It skips itself when the variable is not set.
    /// </summary>
    [AvaloniaFact]
    [Trait("Category", ScreenshotSupport.Category)]
    public void ScreenshotSmoke_MainWindow_WritesBothThemes()
    {
        // Arrange: the requested output folder (skips the test when none was requested) and the main window
        var folder = ScreenshotSupport.RequireFolder();
        var window = CreateWindow();

        // Act: draw the window in the dark and in the light theme
        var dark = ScreenshotSupport.Capture(window, folder, "smoke-main-dark.png", 1600, 1000, ThemeVariant.Dark);
        var light = ScreenshotSupport.Capture(window, folder, "smoke-main-light.png", 1600, 1000, ThemeVariant.Light);
        window.Close();

        // Assert: both pictures were written
        Assert.True(File.Exists(dark));
        Assert.True(File.Exists(light));
    }

    /// <summary>
    ///     Creates the main window over the application composed on temporary folders.
    /// </summary>
    /// <returns>The window, not yet shown.</returns>
    private MainWindowView CreateWindow()
    {
        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        return new MainWindowView(composition.Shell);
    }

    /// <summary>
    ///     Counts the distinct colors on a coarse grid of the picture.
    /// </summary>
    /// <param name="picture">The picture to sample.</param>
    /// <returns>The number of distinct sampled colors.</returns>
    private static int CountDistinctSampledColors(Bitmap picture)
    {
        var size = picture.PixelSize;
        var (buffer, stride) = ScreenshotSupport.ReadPixels(picture);

        // Sample every 16th pixel in both directions
        var colors = new HashSet<int>();
        for (var y = 0; y < size.Height; y += 16)
        {
            for (var x = 0; x < size.Width; x += 16)
            {
                colors.Add(BitConverter.ToInt32(buffer, (y * stride) + (x * 4)));
            }
        }

        return colors.Count;
    }
}

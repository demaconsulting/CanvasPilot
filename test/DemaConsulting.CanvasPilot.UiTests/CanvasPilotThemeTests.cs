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
///     Headless system tests of the look of the whole application: the composed main window is drawn in the dark
///     and in the light theme that the system asks for.
/// </summary>
public sealed class CanvasPilotThemeTests : IDisposable
{
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
    ///     Test that the composed main window is drawn with a dark surface in the dark theme and a light surface in
    ///     the light theme, as measured on the picture of the window.
    /// </summary>
    [AvaloniaFact]
    public void CanvasPilot_Theme_ComposedWindowIsDarkInDarkAndLightInLight()
    {
        // Arrange: the composed application in its main window
        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell);

        // Act: draw it in the dark and in the light variant and measure how bright the picture is on average
        var dark = MeanBrightness(window, "theme-dark.png", ThemeVariant.Dark);
        var light = MeanBrightness(window, "theme-light.png", ThemeVariant.Light);
        window.Close();

        // Assert: the dark picture is dark, the light one is light, and they are clearly apart
        Assert.True(dark < 0.3, $"The dark window has a mean brightness of {dark:F2}.");
        Assert.True(light > 0.6, $"The light window has a mean brightness of {light:F2}.");
    }

    /// <summary>
    ///     Draws the window in a theme variant and measures the mean brightness of the picture.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="fileName">The name of the temporary picture.</param>
    /// <param name="variant">The theme variant.</param>
    /// <returns>The mean brightness, from 0 (black) to 1 (white), sampled on a coarse grid.</returns>
    private double MeanBrightness(MainWindowView window, string fileName, ThemeVariant variant)
    {
        // Draw the window to a temporary picture and read the pixels back
        var path = ScreenshotSupport.Capture(window, _root, fileName, 960, 600, variant);
        using var picture = new Bitmap(path);
        var (pixels, stride) = ScreenshotSupport.ReadPixels(picture);

        // Average the perceived brightness of every 8th pixel in both directions
        double sum = 0;
        var count = 0;
        for (var y = 0; y < picture.PixelSize.Height; y += 8)
        {
            for (var x = 0; x < picture.PixelSize.Width; x += 8)
            {
                var offset = (y * stride) + (x * 4);
                sum += ((0.299 * pixels[offset]) + (0.587 * pixels[offset + 1]) + (0.114 * pixels[offset + 2])) / 255.0;
                count++;
            }
        }

        return sum / count;
    }
}

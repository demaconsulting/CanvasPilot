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

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Draws a window headless and saves what it shows as a PNG picture. It is the tool behind the opt-in screenshot
///     tests that produce the pictures of the README and the user guide, and it keeps that work out of the normal
///     test run: nothing is written unless a caller names a folder.
/// </summary>
/// <remarks>
///     The headless platform renders with Skia and the embedded Inter font (see <see cref="TestAppBuilder" />), so a
///     picture does not depend on the fonts, the display, or the graphics card of the machine. The class is static
///     and has no state; it must be called on the UI thread, like every Avalonia call.
/// </remarks>
internal static class ScreenshotSupport
{
    /// <summary>
    ///     The name of the environment variable that holds the folder the screenshot tests write to. Without it,
    ///     the screenshot tests skip themselves.
    /// </summary>
    public const string FolderVariable = "CANVASPILOT_SCREENSHOT_DIR";

    /// <summary>
    ///     The name of the test category that holds the opt-in screenshot tests, so a run can select only them.
    /// </summary>
    public const string Category = "Screenshots";

    /// <summary>
    ///     Gets the folder the screenshot tests write to.
    /// </summary>
    /// <returns>The full path of the folder, or <see langword="null" /> when screenshots are not requested.</returns>
    public static string? GetRequestedFolder()
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        return string.IsNullOrWhiteSpace(folder) ? null : Path.GetFullPath(folder);
    }

    /// <summary>
    ///     Gets the folder the screenshot tests write to, or skips the running test when none was requested.
    /// </summary>
    /// <returns>The full path of the folder, which exists when this method returns.</returns>
    public static string RequireFolder()
    {
        var folder = GetRequestedFolder();
        Assert.SkipUnless(folder is not null, $"Set {FolderVariable} to the output folder to generate screenshots.");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    ///     Shows a window at a fixed size and theme, lets it lay out and draw, and saves the drawing as a PNG.
    /// </summary>
    /// <param name="window">The window to draw. It is shown by this method and stays open for the caller.</param>
    /// <param name="folder">The folder to write to. It is created when it does not exist.</param>
    /// <param name="fileName">The file name of the picture, including the <c>.png</c> extension.</param>
    /// <param name="width">The width of the window in logical pixels.</param>
    /// <param name="height">The height of the window in logical pixels.</param>
    /// <param name="variant">The theme variant to draw in.</param>
    /// <returns>The full path of the PNG that was written.</returns>
    public static string Capture(
        Window window,
        string folder,
        string fileName,
        double width,
        double height,
        ThemeVariant variant)
    {
        // Fix the size and theme before the first layout so the drawing is deterministic
        window.Width = width;
        window.Height = height;
        window.RequestedThemeVariant = variant;
        window.Show();

        // Let layout, bindings, and the pending render jobs finish, then draw once more for a settled frame
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException("The window did not draw a frame.");

        // Write the frame with the non-obsolete encoder overload
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        frame.Save(path, new PngBitmapEncoderOptions());
        return path;
    }

    /// <summary>
    ///     Reads the pixels of a picture so a test can look at what was drawn.
    /// </summary>
    /// <param name="picture">The picture to read.</param>
    /// <returns>The pixels as bytes in red, green, blue, alpha order, row by row, and the width of a row in bytes.</returns>
    public static (byte[] Pixels, int Stride) ReadPixels(Bitmap picture)
    {
        // Copy the pixels (4 bytes each) out of the bitmap into a pinned buffer
        var size = picture.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            picture.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        // The headless platform draws red first, but other platforms draw blue first, so put the channels in one order
        if (picture.Format == PixelFormat.Bgra8888)
        {
            for (var i = 0; i + 3 < buffer.Length; i += 4)
            {
                (buffer[i], buffer[i + 2]) = (buffer[i + 2], buffer[i]);
            }
        }

        return (buffer, stride);
    }
}

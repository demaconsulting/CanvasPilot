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
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Converts a <see cref="RenderedImage" /> (red, green, blue, alpha bytes with straight alpha, as CanvasNet
///     produces) into an Avalonia bitmap (blue, green, red, alpha bytes with premultiplied alpha, which is what
///     Avalonia's <see cref="WriteableBitmap" /> composites). The pixel conversion is separate from the bitmap
///     creation so it can be tested without a platform.
/// </summary>
public static class RenderedImageBitmap
{
    /// <summary>
    ///     Converts pixels from red, green, blue, alpha with straight alpha to blue, green, red, alpha with
    ///     premultiplied alpha: each color channel is multiplied by the alpha and divided by 255, rounded to the
    ///     nearest value. Fully opaque pixels keep their color, fully transparent pixels become zero.
    /// </summary>
    /// <param name="rgba">The source pixels, four bytes per pixel, rows with no padding.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationRowBytes">The number of bytes from the start of one destination row to the next; at least <c>width * 4</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is below 1 or the row size is too small.</exception>
    /// <exception cref="ArgumentException">Thrown when the source or destination is too small.</exception>
    public static void ConvertToPremultipliedBgra(
        ReadOnlySpan<byte> rgba,
        int width,
        int height,
        Span<byte> destination,
        int destinationRowBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var sourceRowBytes = width * 4;
        ArgumentOutOfRangeException.ThrowIfLessThan(destinationRowBytes, sourceRowBytes);
        if (rgba.Length < (long)sourceRowBytes * height)
        {
            throw new ArgumentException("The source holds fewer pixels than the dimensions require.", nameof(rgba));
        }

        if (destination.Length < ((long)destinationRowBytes * (height - 1)) + sourceRowBytes)
        {
            throw new ArgumentException("The destination is too small for the dimensions.", nameof(destination));
        }

        for (var y = 0; y < height; y++)
        {
            var source = rgba.Slice(y * sourceRowBytes, sourceRowBytes);
            var target = destination.Slice(y * destinationRowBytes, sourceRowBytes);
            for (var x = 0; x < sourceRowBytes; x += 4)
            {
                var alpha = source[x + 3];
                target[x] = Premultiply(source[x + 2], alpha);
                target[x + 1] = Premultiply(source[x + 1], alpha);
                target[x + 2] = Premultiply(source[x], alpha);
                target[x + 3] = alpha;
            }
        }
    }

    /// <summary>
    ///     Creates an Avalonia bitmap with the pixels of a rendered image. Needs the Avalonia platform (it runs
    ///     in the application and in headless UI tests).
    /// </summary>
    /// <param name="image">The rendered image.</param>
    /// <returns>A new bitmap that the caller owns and disposes.</returns>
    public static WriteableBitmap Create(RenderedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var bitmap = new WriteableBitmap(
            new PixelSize(image.Width, image.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        try
        {
            using var framebuffer = bitmap.Lock();
            var rowBytes = framebuffer.RowBytes;
            var buffer = new byte[rowBytes * image.Height];
            ConvertToPremultipliedBgra(image.RgbaPixels.Span, image.Width, image.Height, buffer, rowBytes);
            Marshal.Copy(buffer, 0, framebuffer.Address, buffer.Length);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        return bitmap;
    }

    /// <summary>
    ///     Multiplies a color channel by an alpha value, dividing by 255 with rounding.
    /// </summary>
    /// <param name="channel">The color channel.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The premultiplied channel.</returns>
    private static byte Premultiply(byte channel, byte alpha)
    {
        return (byte)(((channel * alpha) + 127) / 255);
    }
}

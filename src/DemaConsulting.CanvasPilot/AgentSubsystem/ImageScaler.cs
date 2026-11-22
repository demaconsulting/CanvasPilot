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

using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Makes a rendered picture smaller so that it fits the picture size that a model can take in, without writing a file.
/// </summary>
/// <remarks>
///     <para>
///         A model reads a picture at a limited size, and a large one costs context for no gain, so the picture that
///         <c>render_image</c> returns is shrunk so that its longer side is at most a fixed number of pixels. A picture
///         that is small enough is returned as it is: a small picture is never enlarged.
///     </para>
///     <para>
///         Each pixel of the smaller picture is the average of the block of pixels it replaces, with the colors weighted
///         by their opacity, so a transparent edge does not turn dark. The positions of the elements of a diagram are not
///         carried over, because they are in the pixels of the larger picture. The class holds no state and is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal static class ImageScaler
{
    /// <summary>
    ///     Returns a picture whose longer side is at most a given number of pixels, keeping its proportions.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <param name="maxEdge">The most pixels that the longer side may have; at least 1.</param>
    /// <returns>The picture itself when it already fits; otherwise a smaller picture with the same source and notes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxEdge" /> is less than 1.</exception>
    internal static RenderedImage FitWithin(RenderedImage image, int maxEdge)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEdge, 1);

        // A picture that fits is not touched, and a small picture is never enlarged
        var longEdge = Math.Max(image.Width, image.Height);
        if (longEdge <= maxEdge)
        {
            return image;
        }

        // The longer side becomes the limit and the shorter side keeps the proportion, at least one pixel
        var width = Math.Max(1, (int)Math.Round((double)image.Width * maxEdge / longEdge));
        var height = Math.Max(1, (int)Math.Round((double)image.Height * maxEdge / longEdge));
        var source = image.RgbaPixels.Span;
        var pixels = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            // The rows of the source that this row of the result replaces: at least one, rounded outward so none is left out
            var top = (int)((long)y * image.Height / height);
            var bottom = (int)Math.Min(image.Height, Math.Max(top + 1, (((long)(y + 1) * image.Height) + height - 1) / height));
            for (var x = 0; x < width; x++)
            {
                var left = (int)((long)x * image.Width / width);
                var right = (int)Math.Min(image.Width, Math.Max(left + 1, (((long)(x + 1) * image.Width) + width - 1) / width));
                Average(source, image.Width, left, right, top, bottom, pixels.AsSpan(((y * width) + x) * 4, 4));
            }
        }

        return new RenderedImage(
            image.SourcePath,
            image.Format,
            width,
            height,
            pixels,
            image.Diagnostics,
            image.SourceFileHash);
    }

    /// <summary>
    ///     Averages a block of pixels into one pixel, weighting the colors by opacity.
    /// </summary>
    /// <param name="source">The pixels of the picture: four bytes each (red, green, blue, alpha), straight alpha.</param>
    /// <param name="stride">The number of pixels in a row of the picture.</param>
    /// <param name="left">The first column of the block.</param>
    /// <param name="right">The column after the last one of the block.</param>
    /// <param name="top">The first row of the block.</param>
    /// <param name="bottom">The row after the last one of the block.</param>
    /// <param name="target">The four bytes that receive the averaged pixel.</param>
    private static void Average(ReadOnlySpan<byte> source, int stride, int left, int right, int top, int bottom, Span<byte> target)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;
        var count = (long)(right - left) * (bottom - top);

        // Sum the colors multiplied by their opacity, so that clear pixels do not tint the result
        for (var row = top; row < bottom; row++)
        {
            for (var column = left; column < right; column++)
            {
                var index = ((row * stride) + column) * 4;
                long a = source[index + 3];
                red += source[index] * a;
                green += source[index + 1] * a;
                blue += source[index + 2] * a;
                alpha += a;
            }
        }

        // A block with no opacity at all is clear
        if (alpha == 0)
        {
            target.Clear();
            return;
        }

        target[0] = (byte)((red + (alpha / 2)) / alpha);
        target[1] = (byte)((green + (alpha / 2)) / alpha);
        target[2] = (byte)((blue + (alpha / 2)) / alpha);
        target[3] = (byte)((alpha + (count / 2)) / count);
    }
}

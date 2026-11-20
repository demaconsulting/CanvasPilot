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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the pixel conversion of the preview pane, from CanvasNet's straight-alpha RGBA to Avalonia's
///     premultiplied BGRA. They need no Avalonia platform; the bitmap creation is covered by the headless UI tests.
/// </summary>
public sealed class RenderedImageBitmapTests
{
    /// <summary>
    ///     Test that opaque pixels keep their color and only change channel order from RGBA to BGRA.
    /// </summary>
    [Fact]
    public void RenderedImageBitmap_Convert_OpaquePixels_SwapsRedAndBlueOnly()
    {
        // Arrange: two opaque pixels, red-ish and blue-ish
        byte[] rgba = [200, 100, 50, 255, 1, 2, 3, 255];
        var bgra = new byte[8];

        // Act: convert
        RenderedImageBitmap.ConvertToPremultipliedBgra(rgba, 2, 1, bgra, 8);

        // Assert: blue, green, red, alpha order with the colors unchanged
        Assert.Equal([50, 100, 200, 255, 3, 2, 1, 255], bgra);
    }

    /// <summary>
    ///     Test that straight color is multiplied by alpha, rounding to the nearest value.
    /// </summary>
    [Theory]
    [InlineData(255, 128, 128)]
    [InlineData(255, 64, 64)]
    [InlineData(200, 128, 100)]
    [InlineData(100, 1, 0)]
    [InlineData(255, 0, 0)]
    [InlineData(0, 200, 0)]
    public void RenderedImageBitmap_Convert_SemiTransparentPixel_MultipliesColorByAlpha(int channel, int alpha, int expected)
    {
        // Arrange: one pixel whose three color channels are the same
        byte[] rgba = [(byte)channel, (byte)channel, (byte)channel, (byte)alpha];
        var bgra = new byte[4];

        // Act: convert
        RenderedImageBitmap.ConvertToPremultipliedBgra(rgba, 1, 1, bgra, 4);

        // Assert: every color channel is premultiplied and the alpha is unchanged
        Assert.Equal([expected, expected, expected, alpha], bgra.Select(b => (int)b));
    }

    /// <summary>
    ///     Test that a fully transparent pixel becomes all zero, whatever color it had.
    /// </summary>
    [Fact]
    public void RenderedImageBitmap_Convert_TransparentPixel_BecomesZero()
    {
        // Arrange: a transparent pixel with a color
        byte[] rgba = [255, 128, 64, 0];
        var bgra = new byte[4];

        // Act: convert
        RenderedImageBitmap.ConvertToPremultipliedBgra(rgba, 1, 1, bgra, 4);

        // Assert: all zero
        Assert.Equal([0, 0, 0, 0], bgra);
    }

    /// <summary>
    ///     Test that rows are written at the destination's row size, leaving the padding after each row untouched.
    /// </summary>
    [Fact]
    public void RenderedImageBitmap_Convert_WiderDestinationRows_UsesStrideAndKeepsPadding()
    {
        // Arrange: a 1 x 2 picture and a destination whose rows are 8 bytes long, prefilled with a marker
        byte[] rgba = [10, 20, 30, 255, 40, 50, 60, 255];
        var bgra = new byte[12];
        Array.Fill(bgra, (byte)0xEE);

        // Act: convert
        RenderedImageBitmap.ConvertToPremultipliedBgra(rgba, 1, 2, bgra, 8);

        // Assert: the second row starts at byte 8 and the padding is untouched
        Assert.Equal([30, 20, 10, 255, 0xEE, 0xEE, 0xEE, 0xEE, 60, 50, 40, 255], bgra);
    }

    /// <summary>
    ///     Test that sizes and buffers that do not fit are refused.
    /// </summary>
    [Fact]
    public void RenderedImageBitmap_Convert_InvalidArguments_Throw()
    {
        // Act / Assert: empty sizes, a short stride, and short buffers are refused
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderedImageBitmap.ConvertToPremultipliedBgra(new byte[4], 0, 1, new byte[4], 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderedImageBitmap.ConvertToPremultipliedBgra(new byte[4], 1, 0, new byte[4], 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderedImageBitmap.ConvertToPremultipliedBgra(new byte[8], 2, 1, new byte[8], 4));
        Assert.Throws<ArgumentException>(() => RenderedImageBitmap.ConvertToPremultipliedBgra(new byte[7], 2, 1, new byte[8], 8));
        Assert.Throws<ArgumentException>(() => RenderedImageBitmap.ConvertToPremultipliedBgra(new byte[8], 2, 1, new byte[7], 8));
        Assert.Throws<ArgumentNullException>(() => RenderedImageBitmap.Create(null!));
    }
}

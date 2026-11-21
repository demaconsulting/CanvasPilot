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

using System.Buffers.Binary;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for <see cref="PngPhysicalSize" />, which writes the resolution of a PNG file as a <c>pHYs</c> chunk after
///     the header chunk, so that a picture that is exported for print says how large it is.
/// </summary>
public sealed class PngPhysicalSizeTests
{
    /// <summary>
    ///     Validates that the chunk is placed directly after the header chunk, has the pixels per meter of the
    ///     resolution on both axes and the meter as its unit, has the correct checksum, and that the file still decodes to
    ///     the same pixels.
    /// </summary>
    [Fact]
    public void PngPhysicalSize_Insert_Png_AddsCorrectChunkAfterHeaderAndKeepsPixels()
    {
        // Arrange: a PNG of 3 x 2 pixels from the codec of CanvasNet, which writes no resolution
        var original = MakePng(out var pixels);
        var copyOfOriginal = original.ToArray();

        // Act
        var result = PngPhysicalSize.Insert(original, 150);

        // Assert: the header chunk is the first chunk, and the chunk of the size follows it
        Assert.Equal(copyOfOriginal, original);
        Assert.Equal(original.Length + 21, result.Length);
        Assert.Equal(original[..33], result[..33]);
        Assert.Equal(9u, BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(33)));
        Assert.Equal("pHYs", Encoding.ASCII.GetString(result, 37, 4));
        Assert.Equal(5906u, BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(41)));
        Assert.Equal(5906u, BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(45)));
        Assert.Equal(1, result[49]);
        Assert.Equal(ReferenceCrc(result.AsSpan(37, 13)), BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(50)));
        Assert.Equal(original[33..], result[54..]);

        // Assert: the file decodes to the same pixels
        using var stream = new MemoryStream(result);
        using var decoded = PngCodec.Load(stream);
        Assert.Equal((3, 2), (decoded.Width, decoded.Height));
        var decodedPixels = new byte[pixels.Length];
        for (var y = 0; y < decoded.Height; y++)
        {
            decoded.GetRowSpanBytes(y).CopyTo(decodedPixels.AsSpan(y * 12, 12));
        }

        Assert.Equal(pixels, decodedPixels);
    }

    /// <summary>
    ///     Validates the conversion of the resolution to pixels per meter for common values.
    /// </summary>
    /// <param name="dpi">The dots per inch.</param>
    /// <param name="expected">The pixels per meter, rounded.</param>
    [Theory]
    [InlineData(72, 2835u)]
    [InlineData(96, 3780u)]
    [InlineData(150, 5906u)]
    [InlineData(300, 11811u)]
    [InlineData(600, 23622u)]
    public void PngPhysicalSize_Insert_CommonResolutions_ArePixelsPerMeterRounded(double dpi, uint expected)
    {
        // Arrange
        var png = MakePng(out _);

        // Act
        var result = PngPhysicalSize.Insert(png, dpi);

        // Assert
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(41)));
    }

    /// <summary>
    ///     Validates that a PNG that already has a size has the chunk replaced and not repeated.
    /// </summary>
    [Fact]
    public void PngPhysicalSize_Insert_PngWithSizeAlready_ReplacesTheChunk()
    {
        // Arrange: a PNG that has a size of 72 dots per inch
        var withSize = PngPhysicalSize.Insert(MakePng(out _), 72);

        // Act
        var result = PngPhysicalSize.Insert(withSize, 300);

        // Assert: the same length, one chunk, and the new value
        Assert.Equal(withSize.Length, result.Length);
        Assert.Equal(1, Count(Encoding.ASCII.GetString(result), "pHYs"));
        Assert.Equal(11811u, BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(41)));
        Assert.Equal(ReferenceCrc(result.AsSpan(37, 13)), BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(50)));
    }

    /// <summary>
    ///     Validates that bytes that are not a PNG with a header chunk, a resolution that cannot be written, and a missing
    ///     array are refused.
    /// </summary>
    [Fact]
    public void PngPhysicalSize_Insert_InvalidArguments_Throw()
    {
        // Arrange
        var png = MakePng(out _);
        var badSignature = png.ToArray();
        badSignature[1] = (byte)'X';
        var noHeader = png.ToArray();
        noHeader[12] = (byte)'X';

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PngPhysicalSize.Insert(null!, 150));
        Assert.Throws<ArgumentException>(() => PngPhysicalSize.Insert([1, 2, 3], 150));
        Assert.Throws<ArgumentException>(() => PngPhysicalSize.Insert(badSignature, 150));
        Assert.Throws<ArgumentException>(() => PngPhysicalSize.Insert(noHeader, 150));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngPhysicalSize.Insert(png, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngPhysicalSize.Insert(png, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngPhysicalSize.Insert(png, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngPhysicalSize.Insert(png, 2400.01));
        Assert.NotEmpty(PngPhysicalSize.Insert(png, 2400));
    }

    /// <summary>
    ///     Makes the bytes of a PNG of 3 x 2 pixels with the codec of CanvasNet.
    /// </summary>
    /// <param name="pixels">Receives the pixels that the picture has.</param>
    /// <returns>The bytes of the file.</returns>
    private static byte[] MakePng(out byte[] pixels)
    {
        pixels = new byte[3 * 2 * 4];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)((index * 10) + 1);
        }

        using var surface = new Surface(3, 2);
        for (var y = 0; y < 2; y++)
        {
            pixels.AsSpan(y * 12, 12).CopyTo(surface.GetRowSpanBytes(y));
        }

        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream);
        return stream.ToArray();
    }

    /// <summary>
    ///     Computes the CRC-32 of PNG bit by bit, apart from the table that the unit uses, as a reference.
    /// </summary>
    /// <param name="data">The bytes to checksum.</param>
    /// <returns>The checksum.</returns>
    private static uint ReferenceCrc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    /// <summary>
    ///     Counts the non-overlapping occurrences of a text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <param name="part">The text to find.</param>
    /// <returns>The number of occurrences.</returns>
    private static int Count(string text, string part)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(part, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += part.Length;
        }

        return count;
    }
}

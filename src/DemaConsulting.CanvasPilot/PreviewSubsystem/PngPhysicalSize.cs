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

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Writes the physical size of a PNG file, the <c>pHYs</c> chunk, into the bytes of a PNG that has none.
/// </summary>
/// <remarks>
///     The PNG codec of CanvasNet writes no resolution, so a picture that is exported for print would open at 72 or 96
///     dots per inch whatever its maker meant. A chunk that is added after the header leaves the rest of the file as it is,
///     so the picture decodes to the same pixels. Stateless and safe to call from any thread.
/// </remarks>
internal static class PngPhysicalSize
{
    /// <summary>
    ///     The number of meters in an inch, which turns dots per inch into pixels per meter.
    /// </summary>
    private const double MetersPerInch = 0.0254;

    /// <summary>
    ///     The eight bytes that start every PNG file.
    /// </summary>
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     The table of the CRC-32 of the PNG specification, made on first use.
    /// </summary>
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>
    ///     Gives the bytes of a PNG with its physical size set to a resolution.
    /// </summary>
    /// <remarks>
    ///     The chunk is placed directly after the header chunk, where the specification wants it before the data. A file
    ///     that already has the chunk has it replaced. The pixels per meter are the resolution divided by 0.0254, rounded.
    /// </remarks>
    /// <param name="png">The bytes of a PNG file. Not changed.</param>
    /// <param name="dpi">The resolution in dots per inch. Must be greater than 0 and at most 2400.</param>
    /// <returns>A new array with the chunk set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="png" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="dpi" /> is not greater than 0 and at most 2400.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="png" /> is not a PNG file that starts with a header chunk.</exception>
    public static byte[] Insert(byte[] png, double dpi)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (!double.IsFinite(dpi) || dpi <= 0 || dpi > RenderSettings.MaxDpi)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, $"The resolution must be greater than 0 and at most {RenderSettings.MaxDpi} dots per inch.");
        }

        // The file must start with the signature and a header chunk, which is where the new chunk goes
        const int headerChunkEnd = 8 + 4 + 4 + 13 + 4;
        if (png.Length < headerChunkEnd || !png.AsSpan(0, 8).SequenceEqual(Signature) || ReadType(png, 8) != "IHDR")
        {
            throw new ArgumentException("The bytes are not a PNG file that starts with a header chunk.", nameof(png));
        }

        // The chunk: nine bytes of data (pixels per meter across and down, and the unit, which is the meter)
        var pixelsPerMeter = (uint)Math.Round(dpi / MetersPerInch);
        var chunk = new byte[4 + 4 + 9 + 4];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, 9);
        Encoding.ASCII.GetBytes("pHYs", chunk.AsSpan(4));
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8), pixelsPerMeter);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(12), pixelsPerMeter);
        chunk[16] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(17), Crc(chunk.AsSpan(4, 4 + 9)));

        // A file that has the chunk already (not made by the codec of CanvasNet) has it replaced, so there is only one
        var existing = FindChunk(png, "pHYs");
        var head = existing >= 0 ? existing : headerChunkEnd;
        var tail = existing >= 0 ? existing + 4 + 4 + (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(existing)) + 4 : headerChunkEnd;
        var result = new byte[head + chunk.Length + (png.Length - tail)];
        png.AsSpan(0, head).CopyTo(result);
        chunk.CopyTo(result, head);
        png.AsSpan(tail).CopyTo(result.AsSpan(head + chunk.Length));
        return result;
    }

    /// <summary>
    ///     Finds a chunk of a type before the image data starts.
    /// </summary>
    /// <param name="png">The bytes of the PNG file.</param>
    /// <param name="type">The four-letter type of the chunk.</param>
    /// <returns>The offset of the length field of the chunk, or -1 when there is none before the data.</returns>
    private static int FindChunk(byte[] png, string type)
    {
        var offset = 8;
        while (offset + 12 <= png.Length)
        {
            var found = ReadType(png, offset);
            if (found == type)
            {
                return offset;
            }

            if (found is "IDAT" or "IEND")
            {
                return -1;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));
            if (length > int.MaxValue || offset + 12L + length > png.Length)
            {
                return -1;
            }

            offset += 12 + (int)length;
        }

        return -1;
    }

    /// <summary>
    ///     Reads the four-letter type of the chunk at an offset.
    /// </summary>
    /// <param name="png">The bytes of the PNG file.</param>
    /// <param name="offset">The offset of the length field of the chunk.</param>
    /// <returns>The type, or an empty text when the bytes end first.</returns>
    private static string ReadType(byte[] png, int offset) =>
        offset + 8 <= png.Length ? Encoding.ASCII.GetString(png, offset + 4, 4) : string.Empty;

    /// <summary>
    ///     Computes the CRC-32 that PNG puts at the end of each chunk, over the type and the data.
    /// </summary>
    /// <param name="data">The type and data bytes.</param>
    /// <returns>The checksum.</returns>
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>
    ///     Makes the table of the CRC-32 with the polynomial of the PNG specification.
    /// </summary>
    /// <returns>The 256 entries of the table.</returns>
    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var bit = 0; bit < 8; bit++)
            {
                // The reflected polynomial 0xEDB88320 is applied when the lowest bit is set
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}

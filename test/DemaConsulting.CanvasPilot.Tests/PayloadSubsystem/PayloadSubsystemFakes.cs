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

using System.Collections.Immutable;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A composer of mark pictures that draws nothing: it returns small, recognizable bytes and records what it was
///     asked to draw, so that the builder of the payload can be tested without drawing.
/// </summary>
internal sealed class FakeMarkImageComposer : IMarkImageComposer
{
    /// <summary>
    ///     Gets the pictures that were composed, in order.
    /// </summary>
    public List<RenderedImage> Pictures { get; } = [];

    /// <summary>
    ///     Gets the numbers of the marks of each call, in order.
    /// </summary>
    public List<int[]> MarkNumbers { get; } = [];

    /// <summary>
    ///     Gets or sets the number of areas to return for a call, or <see langword="null" /> for one for each mark.
    /// </summary>
    public int? CropCount { get; set; }

    /// <inheritdoc />
    public MarkedPictureImages Compose(RenderedImage picture, IReadOnlyList<Mark> marks)
    {
        Pictures.Add(picture);
        MarkNumbers.Add([.. marks.Select(mark => mark.Number)]);
        var crops = Enumerable.Range(0, CropCount ?? marks.Count)
            .Select(i => new MarkCrop(i < marks.Count ? marks[i].Number : 0, AnnotatedBytesFor(picture, i + 1), new RenderBox(i, i, 64, 64)))
            .ToImmutableArray();
        return new MarkedPictureImages(AnnotatedBytesFor(picture, 0), crops);
    }

    /// <summary>
    ///     Gets the bytes that the fake returns for a picture and an area.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="area">Zero for the whole picture, otherwise the one-based index of the area.</param>
    /// <returns>The bytes.</returns>
    public static byte[] AnnotatedBytesFor(RenderedImage picture, int area)
    {
        return [0x89, 0x50, 0x4E, 0x47, (byte)area, (byte)(picture.Width % 256), (byte)(picture.Height % 256)];
    }
}

/// <summary>
///     Helpers for the tests of the payload subsystem.
/// </summary>
internal static class PayloadTestSupport
{
    /// <summary>
    ///     Decodes a PNG file.
    /// </summary>
    /// <param name="png">The bytes of the file.</param>
    /// <returns>The picture, which the caller disposes.</returns>
    public static Surface Decode(ReadOnlyMemory<byte> png)
    {
        using var stream = new MemoryStream(png.ToArray());
        return PngCodec.Load(stream);
    }

    /// <summary>
    ///     Gets the opaque color of a hexadecimal color such as <c>#e53935</c>.
    /// </summary>
    /// <param name="hex">The color as <c>#rrggbb</c>.</param>
    /// <returns>The color.</returns>
    public static Rgba32 ColorOf(string hex)
    {
        return new Rgba32(
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16),
            255);
    }

    /// <summary>
    ///     Makes the staged form of a file attachment, as the attachment store returns it for a file that was copied into
    ///     the project.
    /// </summary>
    /// <param name="chip">The chip of the attachment.</param>
    /// <param name="relativePath">The path in the project.</param>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The staged attachment.</returns>
    public static StagedAttachment Staged(AttachmentChip chip, string relativePath, long size = 3)
    {
        var kind = chip switch
        {
            FolderChip => AttachmentKind.Folder,
            ImageChip => AttachmentKind.Image,
            _ => AttachmentKind.File
        };
        return new StagedAttachment(chip, chip.FullPath, relativePath, kind, Path.GetFileName(relativePath), size, kind == AttachmentKind.Folder ? 2 : 1, true, 0);
    }
}

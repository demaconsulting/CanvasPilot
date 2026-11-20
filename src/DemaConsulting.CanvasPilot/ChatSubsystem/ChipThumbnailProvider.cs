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

using Avalonia.Media.Imaging;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Gives the chips of the chat their small pictures: the preview of an attached picture file, and the detail of the
///     picture around a mark. It is the provider that the composer's editing control and the transcript both use.
/// </summary>
/// <remarks>
///     <para>
///         The composer knows a mark only as a chip with a number, so what a mark looks like comes from the caller as a
///         function from the chip to the PNG bytes of the area around the mark; the chat panel gives one that cuts the
///         area from the picture the mark was drawn on, and a transcript entry gives one that takes the cut-out that the
///         payload already holds. Attached picture files are previewed by <see cref="ImageChipThumbnailProvider" />, which
///         only reads picture files up to a size limit; every other chip keeps its icon.
///     </para>
///     <para>
///         A picture is made on a thread-pool thread, so that composing a crop or decoding a file never holds up the
///         interface, and is kept (by mark identity) until the provider is disposed. A function or decoder that fails
///         leaves the chip with its icon: the provider never throws. Safe to call from the user interface thread.
///     </para>
/// </remarks>
public sealed class ChipThumbnailProvider : IChipThumbnailProvider, IDisposable
{
    /// <summary>
    ///     The width, in pixels, that the pictures of marks are decoded to; the chip shows them far smaller.
    /// </summary>
    public const int PreviewWidth = ImageChipThumbnailProvider.PreviewWidth;

    /// <summary>
    ///     Gives the PNG bytes of the area around a mark, or <see langword="null" /> when there are none.
    /// </summary>
    private readonly Func<MarkChip, byte[]?> _markPng;

    /// <summary>
    ///     Previews the attached picture files.
    /// </summary>
    private readonly IChipThumbnailProvider _files;

    /// <summary>
    ///     Whether <see cref="_files" /> was made here and is released here.
    /// </summary>
    private readonly bool _ownsFiles;

    /// <summary>
    ///     The pictures of marks that were made, by the identity of the mark; a <see langword="null" /> value is the
    ///     remembered result of a mark that has none.
    /// </summary>
    private readonly Dictionary<Guid, Bitmap?> _marks = [];

    /// <summary>
    ///     Guards <see cref="_marks" />.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChipThumbnailProvider" /> class.
    /// </summary>
    /// <param name="markPng">
    ///     Gives the PNG bytes of the area around a mark, or <see langword="null" /> when there are none. It is called on
    ///     a thread-pool thread, and may be slow.
    /// </param>
    /// <param name="files">
    ///     Previews attached picture files, or <see langword="null" /> for an <see cref="ImageChipThumbnailProvider" />
    ///     that this provider owns and disposes.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markPng" /> is <see langword="null" />.</exception>
    public ChipThumbnailProvider(Func<MarkChip, byte[]?> markPng, IChipThumbnailProvider? files = null)
    {
        ArgumentNullException.ThrowIfNull(markPng);

        _markPng = markPng;
        _ownsFiles = files is null;
        _files = files ?? new ImageChipThumbnailProvider();
    }

    /// <inheritdoc />
    public Task<Bitmap?> GetThumbnailAsync(ChipSegment chip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chip);

        // Only a mark is made here; picture files are the business of the file provider, and the rest has no picture
        if (chip is not MarkChip mark)
        {
            return _files.GetThumbnailAsync(chip, cancellationToken);
        }

        return cancellationToken.IsCancellationRequested
            ? Task.FromResult<Bitmap?>(null)
            : Task.Run(() => MakeMarkPicture(mark), cancellationToken);
    }

    /// <summary>
    ///     Releases the pictures that were kept, and the file provider when this provider made it.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var bitmap in _marks.Values)
            {
                bitmap?.Dispose();
            }

            _marks.Clear();
        }

        if (_ownsFiles && _files is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    ///     Makes the picture of a mark, or finds the one that was made before.
    /// </summary>
    /// <param name="mark">The chip of the mark.</param>
    /// <returns>The picture, or <see langword="null" /> when the mark has none.</returns>
    private Bitmap? MakeMarkPicture(MarkChip mark)
    {
        lock (_gate)
        {
            if (_marks.TryGetValue(mark.MarkId, out var cached))
            {
                return cached;
            }
        }

        // Cut the area out and decode it small; any failure leaves the chip with its icon
        Bitmap? bitmap;
        try
        {
            var png = _markPng(mark);
            if (png is null || png.Length == 0)
            {
                bitmap = null;
            }
            else
            {
                using var stream = new MemoryStream(png);
                bitmap = Bitmap.DecodeToWidth(stream, PreviewWidth);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            bitmap = null;
        }

        // Remember the result, also when there is none, so that a failing mark is not tried on every draw
        lock (_gate)
        {
            if (_marks.TryGetValue(mark.MarkId, out var earlier))
            {
                bitmap?.Dispose();
                return earlier;
            }

            _marks[mark.MarkId] = bitmap;
            return bitmap;
        }
    }
}

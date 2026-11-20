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

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     Gives the chips of attached picture files a small preview of the picture, which is the only kind of chip it knows
///     how to show.
/// </summary>
/// <remarks>
///     <para>
///         <b>Dropped content is data.</b> The provider only decodes the picture bytes of a file the user attached, and
///         only when the file is not larger than <see cref="MaxFileBytes" />; it decodes straight to the size of the
///         preview, so a picture with a huge number of pixels never exists in memory at its full size. Every failure of
///         the decoder, the file system, or a file that is not a picture the decoder knows, such as an SVG, gives no
///         preview and the chip keeps its icon. Nothing is run or opened for any other purpose.
///     </para>
///     <para>
///         The work runs on a background thread. The last previews are kept, keyed by the path and the time the file was
///         last written, so that the lines of the message that are drawn again do not read the files again; a changed
///         file gets a new preview. The class is safe for concurrent use.
///     </para>
/// </remarks>
public sealed class ImageChipThumbnailProvider : IChipThumbnailProvider, IDisposable
{
    /// <summary>
    ///     The largest picture file, in bytes, that is decoded for a preview.
    /// </summary>
    public const long MaxFileBytes = 20L * 1024 * 1024;

    /// <summary>
    ///     The width, in pixels, that previews are decoded to; the chip shows them at a small fraction of this.
    /// </summary>
    public const int PreviewWidth = 64;

    /// <summary>
    ///     The most previews that are kept for reuse.
    /// </summary>
    private const int CacheLimit = 64;

    /// <summary>
    ///     The previews that were made, keyed by path and time of the last write; a <see langword="null" /> value is
    ///     the remembered result of a file that has no preview.
    /// </summary>
    private readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Guards <see cref="_cache" />.
    /// </summary>
    private readonly object _gate = new();

    /// <inheritdoc />
    public Task<Bitmap?> GetThumbnailAsync(ChipSegment chip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chip);

        // Only a picture file has a preview; every other chip keeps its icon
        if (chip is not ImageChip imageChip || cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<Bitmap?>(null);
        }

        return Task.Run(() => Decode(imageChip.FullPath), cancellationToken);
    }

    /// <summary>
    ///     Releases the previews that were kept.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var bitmap in _cache.Values)
            {
                bitmap?.Dispose();
            }

            _cache.Clear();
        }
    }

    /// <summary>
    ///     Makes the preview of a picture file, or finds the one that was made for the same version of the file.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns>The preview, or <see langword="null" /> when the file cannot be shown as a picture.</returns>
    private Bitmap? Decode(string path)
    {
        try
        {
            // Look at the file without opening it, and refuse what is too large or not there
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            var key = $"{path}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
            lock (_gate)
            {
                if (_cache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }

            // Decode to the size of the preview, and remember the result, also when there is none
            Bitmap? bitmap;
            try
            {
                using var stream = File.OpenRead(path);
                bitmap = Bitmap.DecodeToWidth(stream, PreviewWidth);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A file that is not a picture the decoder knows, or one that is damaged, keeps its icon
                bitmap = null;
            }

            lock (_gate)
            {
                if (_cache.Count >= CacheLimit)
                {
                    _cache.Clear();
                }

                _cache[key] = bitmap;
            }

            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // The file cannot be read now; the chip keeps its icon
            return null;
        }
    }
}

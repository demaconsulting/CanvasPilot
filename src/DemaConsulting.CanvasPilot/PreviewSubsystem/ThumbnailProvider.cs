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
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Makes the thumbnails of the explorer: it has the render service draw a file at the size of a thumbnail, keeps the
///     pixels of the latest ones in a small cache of a fixed size, and gives each caller a bitmap of its own.
/// </summary>
/// <remarks>
///     <para>
///         <b>Off the user interface thread.</b> The render service draws on a background thread, and at most
///         <see cref="MaximumConcurrentRenders" /> files are drawn at once; the rest wait their turn. A file that changed on
///         disk (its time or size is not what it was) is drawn again, because the cache is keyed by both.
///     </para>
///     <para>
///         <b>Bounded.</b> The cache holds the pixels of at most the number of thumbnails that the constructor is given
///         (the least recently used goes first), so a project of thousands of files holds only some thousands of bytes of
///         each. Bitmaps are made for the caller from the cached pixels and are the caller's to dispose, so evicting an entry
///         never pulls a bitmap from under a row that shows it.
///     </para>
/// </remarks>
public sealed class ThumbnailProvider : IThumbnailSource, IDisposable
{
    /// <summary>
    ///     The width of a thumbnail in pixels, twice the 28 units that a row shows so that it is sharp on a dense display.
    /// </summary>
    public const int ThumbnailWidth = 56;

    /// <summary>
    ///     The height of a thumbnail in pixels, twice the 18 units that a row shows.
    /// </summary>
    public const int ThumbnailHeight = 36;

    /// <summary>
    ///     How many files are drawn at the same time.
    /// </summary>
    public const int MaximumConcurrentRenders = 2;

    /// <summary>
    ///     The color that a thumbnail is flattened over, so that a picture with transparent parts is not drawn on the color of
    ///     the row.
    /// </summary>
    private const string BackgroundHex = "#FFFFFF";

    /// <summary>
    ///     Draws the files.
    /// </summary>
    private readonly IImageRenderService _renderer;

    /// <summary>
    ///     The most thumbnails that the cache holds.
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    ///     Lets only some files be drawn at once.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(MaximumConcurrentRenders);

    /// <summary>
    ///     Guards the cache.
    /// </summary>
    private readonly Lock _syncRoot = new();

    /// <summary>
    ///     The cached thumbnails by key.
    /// </summary>
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _entries = new(StringComparer.Ordinal);

    /// <summary>
    ///     The cached thumbnails, the most recently used first.
    /// </summary>
    private readonly LinkedList<CacheEntry> _order = new();

    /// <summary>
    ///     Whether the provider was disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Creates the provider.
    /// </summary>
    /// <param name="renderer">Draws the files.</param>
    /// <param name="capacity">How many thumbnails the cache holds, at least 1.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderer" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity" /> is below 1.</exception>
    public ThumbnailProvider(IImageRenderService renderer, int capacity = 256)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _renderer = renderer;
        _capacity = capacity;
    }

    /// <summary>
    ///     Gets how many thumbnails the cache holds now.
    /// </summary>
    public int CachedCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    ///     Gets how many files were drawn, which is how many times the cache did not have the thumbnail.
    /// </summary>
    public int RenderCount { get; private set; }

    /// <inheritdoc />
    public Task<Bitmap?> CreateAsync(string filePath, string projectFolder, CancellationToken cancellationToken) =>
        CreateAsync(filePath, projectFolder, ThumbnailWidth, ThumbnailHeight, cancellationToken);

    /// <inheritdoc />
    public async Task<Bitmap?> CreateAsync(string filePath, string projectFolder, int width, int height, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        var info = new FileInfo(filePath);
        if (!info.Exists)
        {
            return null;
        }

        var key = $"{info.FullName}|{info.LastWriteTimeUtc.Ticks}|{info.Length}|{width}x{height}";
        var image = TryGetCached(key);
        if (image is null)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                image = TryGetCached(key) ?? await DrawAsync(key, info.FullName, projectFolder, width, height, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        return image is null ? null : RenderedImageBitmap.Create(image);
    }

    /// <summary>
    ///     Forgets every cached thumbnail. The bitmaps that were given out are the callers' and are not touched.
    /// </summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
            _order.Clear();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();

        // The gate is not disposed: it never makes a wait handle, so it holds nothing to free, and a call that is still
        // running releases it when it ends, which would fail on a disposed gate
        // (the explorer cancels its calls when it is disposed, but the provider may outlive it)
    }

    /// <summary>
    ///     Draws a file at the size of a thumbnail and keeps the pixels.
    /// </summary>
    /// <param name="key">The key of the file.</param>
    /// <param name="filePath">The full path of the file.</param>
    /// <param name="projectFolder">The project folder.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The thumbnail, or <see langword="null" /> when the file could not be drawn.</returns>
    private async Task<RenderedImage?> DrawAsync(string key, string filePath, string projectFolder, int width, int height, CancellationToken cancellationToken)
    {
        var settings = new RenderSettings(width, height, BackgroundHex: BackgroundHex);
        var result = await _renderer.RenderAsync(filePath, projectFolder, settings, cancellationToken);
        if (result.Image is not { } drawn)
        {
            return null;
        }

        // Only the pixels are kept, not the positions and notes that a full render carries
        var thumbnail = new RenderedImage(filePath, "thumbnail", drawn.Width, drawn.Height, drawn.RgbaPixels.ToArray());
        lock (_syncRoot)
        {
            RenderCount++;
            if (_entries.Remove(key, out var existing))
            {
                _order.Remove(existing);
            }

            _entries[key] = _order.AddFirst(new CacheEntry(key, thumbnail));
            while (_entries.Count > _capacity)
            {
                var oldest = _order.Last!;
                _order.RemoveLast();
                _entries.Remove(oldest.Value.Key);
            }
        }

        return thumbnail;
    }

    /// <summary>
    ///     Finds a thumbnail in the cache and makes it the most recently used.
    /// </summary>
    /// <param name="key">The key of the file.</param>
    /// <returns>The thumbnail, or <see langword="null" /> when the cache has none.</returns>
    private RenderedImage? TryGetCached(string key)
    {
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(key, out var node))
            {
                return null;
            }

            _order.Remove(node);
            _order.AddFirst(node);
            return node.Value.Image;
        }
    }

    /// <summary>
    ///     A thumbnail in the cache.
    /// </summary>
    /// <param name="Key">The key of the file: its path, time, and size.</param>
    /// <param name="Image">The pixels.</param>
    private sealed record CacheEntry(string Key, RenderedImage Image);
}

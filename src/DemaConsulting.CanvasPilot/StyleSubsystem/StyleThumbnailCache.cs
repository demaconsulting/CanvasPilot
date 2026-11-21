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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Draws the example images of kits as small pictures for the Style Kits window and remembers them, so that choosing a
///     kit again does not draw its examples again.
/// </summary>
/// <remarks>
///     <para>
///         A picture is drawn 240 pixels wide by the render service, which draws on a worker thread, and the file may read
///         other files only from the folder of its own kit, because the files of a shared kit or of a zip file are not
///         trusted. A picture is remembered by the full path, the size, and the time of the last write of its file, so a
///         file that is changed is drawn again, and a file that is asked for twice at once is drawn once. At most
///         <see cref="MaxEntries" /> pictures are kept; the one that was asked for least recently goes first.
///     </para>
///     <para>
///         The drawing belongs to the cache and not to the caller that asked: a caller that stops waiting does not cancel
///         it, so the next caller gets the picture. Disposing the cache cancels what is still being drawn. Safe for
///         concurrent use.
///     </para>
/// </remarks>
public sealed class StyleThumbnailCache : IDisposable
{
    /// <summary>
    ///     The width of a small picture in pixels.
    /// </summary>
    public const int ThumbnailWidth = 240;

    /// <summary>
    ///     The number of pictures that are kept.
    /// </summary>
    public const int MaxEntries = 64;

    /// <summary>
    ///     Draws the files.
    /// </summary>
    private readonly IImageRenderService _renderer;

    /// <summary>
    ///     The number of pictures that are kept.
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    ///     The pictures, by file and state of the file.
    /// </summary>
    private readonly Dictionary<Key, Entry> _entries = [];

    /// <summary>
    ///     Cancels the drawing that is still going on when the cache is disposed.
    /// </summary>
    private readonly CancellationTokenSource _disposed = new();

    /// <summary>
    ///     Guards the dictionary and the counters.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    ///     The number of times a file was asked to be drawn.
    /// </summary>
    private int _renderCount;

    /// <summary>
    ///     Counts the use of the entries, so that the one that was used longest ago can be found.
    /// </summary>
    private long _clock;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleThumbnailCache" /> class.
    /// </summary>
    /// <param name="renderer">Draws the files.</param>
    /// <param name="capacity">The number of pictures to keep, at least 1.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderer" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity" /> is less than 1.</exception>
    public StyleThumbnailCache(IImageRenderService renderer, int capacity = MaxEntries)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _renderer = renderer;
        _capacity = capacity;
    }

    /// <summary>
    ///     Gets the number of pictures that are kept now.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    ///     Gets how many times a file was drawn, which tests use to see that a picture was not drawn twice.
    /// </summary>
    public int RenderCount => Volatile.Read(ref _renderCount);

    /// <summary>
    ///     Gets the small picture of a file, drawing it when it is not kept or the file has changed.
    /// </summary>
    /// <param name="filePath">The full path of the example file.</param>
    /// <param name="kitFolder">The folder of the kit, which is the only folder that the file may read other files from.</param>
    /// <param name="cancellationToken">Stops the wait of this caller; the drawing goes on for the next caller.</param>
    /// <returns>The picture, or the reason there is none. Never throws for a file that cannot be drawn.</returns>
    /// <exception cref="ArgumentException">Thrown when an argument is empty or blank.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled while waiting.</exception>
    public async Task<StyleThumbnail> GetAsync(string filePath, string kitFolder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(kitFolder);

        var info = new FileInfo(filePath);
        if (!info.Exists)
        {
            return new StyleThumbnail(null, "The file is not there.");
        }

        var key = new Key(info.FullName, info.Length, info.LastWriteTimeUtc.Ticks);
        Task<StyleThumbnail> task;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.LastUsed = ++_clock;
                task = entry.Task;
            }
            else
            {
                task = RenderAsync(info.FullName, kitFolder);
                _entries[key] = new Entry(task, ++_clock);
                Evict();
            }
        }

        var result = await task.WaitAsync(cancellationToken).ConfigureAwait(false);

        // A drawing that was cancelled or that failed to start is not kept, so that the next request draws again
        if (result.IsTransient)
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(key, out var entry) && ReferenceEquals(entry.Task, task))
                {
                    _entries.Remove(key);
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Forgets every picture.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed.Cancel();
        _disposed.Dispose();
        Clear();
    }

    /// <summary>
    ///     Draws one file and turns the outcome into a value; it never throws for a file that cannot be drawn.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="kitFolder">The folder of the kit.</param>
    /// <returns>The picture or the reason there is none.</returns>
    private async Task<StyleThumbnail> RenderAsync(string path, string kitFolder)
    {
        Interlocked.Increment(ref _renderCount);
        try
        {
            // Start on a worker so that a render service that does work before its first await never holds the caller
            var result = await Task.Run(
                () => _renderer.RenderAsync(path, kitFolder, new RenderSettings(Width: ThumbnailWidth), _disposed.Token),
                CancellationToken.None).ConfigureAwait(false);
            if (result.Image is { } image)
            {
                return new StyleThumbnail(image, null);
            }

            return result.IsCancelled
                ? new StyleThumbnail(null, "The drawing was cancelled.", IsTransient: true)
                : new StyleThumbnail(null, result.Error?.Message ?? "The file could not be drawn.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The render service reports a file that cannot be drawn as a result; anything else is a problem of the drawing
            return new StyleThumbnail(null, ex.Message, IsTransient: true);
        }
    }

    /// <summary>
    ///     Removes the entries that were used longest ago while there are more than the capacity. The lock is held.
    /// </summary>
    private void Evict()
    {
        while (_entries.Count > _capacity)
        {
            var oldest = _entries.MinBy(pair => pair.Value.LastUsed).Key;
            _entries.Remove(oldest);
        }
    }

    /// <summary>
    ///     The identity of a picture: the file and its state.
    /// </summary>
    /// <param name="Path">The full path.</param>
    /// <param name="Length">The size in bytes.</param>
    /// <param name="Ticks">The time of the last write.</param>
    private readonly record struct Key(string Path, long Length, long Ticks);

    /// <summary>
    ///     A kept picture and when it was last asked for.
    /// </summary>
    private sealed class Entry(Task<StyleThumbnail> task, long lastUsed)
    {
        /// <summary>
        ///     Gets the drawing, which may still be going on.
        /// </summary>
        public Task<StyleThumbnail> Task { get; } = task;

        /// <summary>
        ///     Gets or sets the value of the clock when the entry was last asked for.
        /// </summary>
        public long LastUsed { get; set; } = lastUsed;
    }
}

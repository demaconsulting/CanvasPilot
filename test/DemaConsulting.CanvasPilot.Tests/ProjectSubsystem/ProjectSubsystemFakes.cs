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

using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Hand-written scheduler for tests: scheduled callbacks wait until a test runs them, so debouncing can be
///     checked without real delays.
/// </summary>
public sealed class FakeRefreshScheduler : IRefreshScheduler
{
    /// <summary>
    ///     The callbacks that were scheduled and not cancelled or run.
    /// </summary>
    private readonly List<Entry> _pending = [];

    /// <summary>
    ///     Every callback ever scheduled, including cancelled ones, for tests of late callbacks.
    /// </summary>
    private readonly List<Entry> _all = [];

    /// <summary>
    ///     Gets how many callbacks were scheduled in total.
    /// </summary>
    public int ScheduledCount { get; private set; }

    /// <summary>
    ///     Gets how many callbacks are waiting to run.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (_pending)
            {
                return _pending.Count;
            }
        }
    }

    /// <summary>
    ///     Gets the delay of the most recent request.
    /// </summary>
    public TimeSpan? LastDelay { get; private set; }

    /// <inheritdoc />
    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        var entry = new Entry(callback, this);
        lock (_pending)
        {
            ScheduledCount++;
            LastDelay = delay;
            _pending.Add(entry);
            _all.Add(entry);
        }

        return entry;
    }

    /// <summary>
    ///     Runs every waiting callback, as if the delay had passed.
    /// </summary>
    public void RunPending()
    {
        List<Entry> due;
        lock (_pending)
        {
            due = _pending.ToList();
            _pending.Clear();
        }

        foreach (var entry in due)
        {
            entry.Callback();
        }
    }

    /// <summary>
    ///     Runs one callback by the order it was scheduled in (zero-based), even if it was cancelled, as a callback
    ///     that was already queued on the user interface thread when it was cancelled would run.
    /// </summary>
    /// <param name="index">The zero-based position of the request.</param>
    public void RunScheduled(int index)
    {
        Entry entry;
        lock (_pending)
        {
            entry = _all[index];
            _pending.Remove(entry);
        }

        entry.Callback();
    }

    /// <summary>
    ///     One scheduled callback.
    /// </summary>
    /// <param name="callback">The callback.</param>
    /// <param name="owner">The scheduler that holds the entry.</param>
    private sealed class Entry(Action callback, FakeRefreshScheduler owner) : IDisposable
    {
        /// <summary>
        ///     Gets the callback.
        /// </summary>
        public Action Callback { get; } = callback;

        /// <inheritdoc />
        public void Dispose()
        {
            lock (owner._pending)
            {
                owner._pending.Remove(this);
            }
        }
    }
}

/// <summary>
///     Hand-written image source filter for tests: a file is an image source when it has one of the given
///     extensions.
/// </summary>
/// <param name="extensions">The accepted extensions, with the leading dot.</param>
public sealed class FakeImageSourceFilter(params string[] extensions) : IImageSourceFilter
{
    /// <inheritdoc />
    public bool IsImageSource(string fileName)
    {
        return extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
    }
}

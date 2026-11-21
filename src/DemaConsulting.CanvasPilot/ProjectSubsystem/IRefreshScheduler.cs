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

using Avalonia.Threading;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     Runs a refresh of the project explorer after a delay. The explorer asks for a new refresh each time the
///     file system changes, and cancels the earlier one, so a burst of changes causes one refresh.
/// </summary>
public interface IRefreshScheduler
{
    /// <summary>
    ///     Schedules a callback.
    /// </summary>
    /// <param name="delay">How long to wait before running the callback.</param>
    /// <param name="callback">
    ///     The callback. It runs on the thread that owns the explorer's data (the user interface thread), unless
    ///     the returned object was disposed first.
    /// </param>
    /// <returns>An object that cancels the callback when disposed. It may be called from any thread.</returns>
    IDisposable Schedule(TimeSpan delay, Action callback);
}

/// <summary>
///     Waits on the thread pool, then runs the callback on the Avalonia user interface thread.
/// </summary>
public sealed class DispatcherRefreshScheduler : IRefreshScheduler
{
    /// <inheritdoc />
    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var source = new CancellationTokenSource();
        _ = RunAsync(delay, callback, source.Token);
        return new Cancellation(source);
    }

    /// <summary>
    ///     Waits, then posts the callback to the user interface thread unless it was cancelled.
    /// </summary>
    /// <param name="delay">How long to wait.</param>
    /// <param name="callback">The callback.</param>
    /// <param name="cancellationToken">Cancels the callback.</param>
    /// <returns>A task that completes when the callback was posted or cancelled.</returns>
    private static async Task RunAsync(TimeSpan delay, Action callback, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                callback();
            }
        });
    }

    /// <summary>
    ///     Cancels a scheduled callback when disposed.
    /// </summary>
    /// <param name="source">The source that cancels the callback.</param>
    private sealed class Cancellation(CancellationTokenSource source) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            source.Cancel();
            source.Dispose();
        }
    }
}

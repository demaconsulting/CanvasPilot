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

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Hands the events of a turn from the thread that reads the provider to the thread that shows them, and keeps a
///     consumer that lags from falling behind: events that say the same thing again are merged while they wait.
/// </summary>
/// <remarks>
///     <para>
///         <b>What is merged.</b> Only events that are next to each other in the queue, so the order never changes: the
///         pieces of text are joined, a progress event replaces the progress event of the same call before it, a
///         <see cref="AgentEvent.ModelWorking" /> replaces the one before it, and a <see cref="AgentEvent.Thinking" /> after
///         a <see cref="AgentEvent.Thinking" /> is dropped. Every other event (a tool that started or finished, a file that
///         was written, the usage, the end of the turn) is kept, in order.
///     </para>
///     <para>
///         <b>Ending.</b> <see cref="Complete" /> ends the stream after the events that wait. A fault that is given to it is
///         thrown to the reader once the events before it were read. One thread adds and one reads; the class guards what
///         they share.
///     </para>
/// </remarks>
internal sealed class AgentEventCoalescer
{
    /// <summary>
    ///     Guards the queue, the end of the stream, and the signal.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    ///     The events that wait, oldest first.
    /// </summary>
    private readonly LinkedList<AgentEvent> _queue = new();

    /// <summary>
    ///     Completes when something was added or the stream ended, while the reader waits; otherwise <see langword="null" />.
    /// </summary>
    private TaskCompletionSource? _signal;

    /// <summary>
    ///     Whether the stream has ended.
    /// </summary>
    private bool _completed;

    /// <summary>
    ///     What ended the stream, or <see langword="null" /> when it ended normally.
    /// </summary>
    private Exception? _fault;

    /// <summary>
    ///     The number of events that were merged into another or dropped.
    /// </summary>
    private long _merged;

    /// <summary>
    ///     Gets the number of events that were merged into another or dropped because the reader had not taken the one
    ///     before yet.
    /// </summary>
    internal long Merged
    {
        get
        {
            lock (_gate)
            {
                return _merged;
            }
        }
    }

    /// <summary>
    ///     Adds an event, merging it into the event at the end of the queue when they say the same thing. Nothing is added
    ///     after the stream ended.
    /// </summary>
    /// <param name="agentEvent">The event.</param>
    /// <exception cref="ArgumentNullException">Thrown when the event is <see langword="null" />.</exception>
    internal void Add(AgentEvent agentEvent)
    {
        ArgumentNullException.ThrowIfNull(agentEvent);

        TaskCompletionSource? wake;
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            var tail = _queue.Last;
            switch (agentEvent)
            {
                case AgentEvent.TextDelta text when tail?.Value is AgentEvent.TextDelta before:
                    tail.Value = new AgentEvent.TextDelta(before.Text + text.Text);
                    _merged++;
                    break;
                case AgentEvent.ToolProgress progress when tail?.Value is AgentEvent.ToolProgress before
                    && string.Equals(before.CallId, progress.CallId, StringComparison.Ordinal):
                    tail.Value = progress;
                    _merged++;
                    break;
                case AgentEvent.ModelWorking when tail?.Value is AgentEvent.ModelWorking:
                    tail.Value = agentEvent;
                    _merged++;
                    break;
                case AgentEvent.Thinking when tail?.Value is AgentEvent.Thinking:
                    _merged++;
                    break;
                default:
                    _queue.AddLast(agentEvent);
                    break;
            }

            wake = _signal;
            _signal = null;
        }

        wake?.TrySetResult();
    }

    /// <summary>
    ///     Ends the stream: the reader gets the events that wait and then the end, or the fault.
    /// </summary>
    /// <param name="fault">What ended the stream; <see langword="null" /> for a normal end.</param>
    internal void Complete(Exception? fault = null)
    {
        TaskCompletionSource? wake;
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _fault = fault;
            wake = _signal;
            _signal = null;
        }

        wake?.TrySetResult();
    }

    /// <summary>
    ///     Reads the events in order until the stream ends.
    /// </summary>
    /// <param name="cancellationToken">A token that ends the wait for the next event.</param>
    /// <returns>The events.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled while waiting.</exception>
    internal async IAsyncEnumerable<AgentEvent> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            AgentEvent? next = null;
            Task? wait = null;
            Exception? fault = null;
            var ended = false;
            var canceled = false;
            lock (_gate)
            {
                if (_queue.First is { } first)
                {
                    next = first.Value;
                    _queue.RemoveFirst();
                }
                else if (_completed)
                {
                    ended = true;
                    fault = _fault;
                }
                else if (cancellationToken.IsCancellationRequested)
                {
                    // Only a reader that has nothing left to take is canceled: what waits is always handed over first
                    canceled = true;
                }
                else
                {
                    _signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    wait = _signal.Task;
                }
            }

            if (next is not null)
            {
                yield return next;
                continue;
            }

            if (ended)
            {
                if (fault is not null)
                {
                    ExceptionDispatchInfo.Throw(fault);
                }

                yield break;
            }

            if (canceled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            try
            {
                await wait!.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Look at the queue once more: an event may have arrived at the same moment, and it is not lost
            }
        }
    }
}

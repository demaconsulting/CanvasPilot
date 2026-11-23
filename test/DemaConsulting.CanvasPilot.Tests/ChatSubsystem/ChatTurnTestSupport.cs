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
using System.Threading.Channels;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     An AI copilot for the chat tests that answers a turn with the events that the test gives it, when the test gives
///     them, so that a test can look at the transcript between two events.
/// </summary>
internal sealed class ControlledAgentService : IAgentService
{
    /// <summary>
    ///     The events of the turn that runs, which the test writes.
    /// </summary>
    private Channel<AgentEvent> _events = Channel.CreateUnbounded<AgentEvent>(new UnboundedChannelOptions { AllowSynchronousContinuations = true });

    /// <inheritdoc />
    public event EventHandler<FileWrittenEventArgs>? FileWritten;

    /// <summary>
    ///     Gets the requests that were made, in order.
    /// </summary>
    public List<AgentTurnRequest> Requests { get; } = [];

    /// <summary>
    ///     Raises <see cref="FileWritten" />, as the real service does when a tool wrote a file.
    /// </summary>
    /// <param name="e">What the event carries.</param>
    public void RaiseFileWritten(FileWrittenEventArgs e) => FileWritten?.Invoke(this, e);

    /// <summary>
    ///     Gets or sets the cancellation token of the latest turn.
    /// </summary>
    public CancellationToken LatestToken { get; private set; }

    /// <summary>
    ///     Gets or sets a value indicating whether a stream whose token is canceled just ends, as the real service does,
    ///     and does not throw <see cref="OperationCanceledException" />.
    /// </summary>
    public bool EndQuietlyOnCancellation { get; set; } = true;

    /// <summary>
    ///     Gets the number of times the conversation was reset.
    /// </summary>
    public int ResetCount { get; private set; }

    /// <summary>
    ///     Gives events to the turn that runs.
    /// </summary>
    /// <param name="events">The events, in order.</param>
    public void Emit(params AgentEvent[] events)
    {
        foreach (var agentEvent in events)
        {
            Assert.True(_events.Writer.TryWrite(agentEvent));
        }
    }

    /// <summary>
    ///     Ends the stream of the turn that runs without an event that ends the turn.
    /// </summary>
    public void EndStream() => _events.Writer.TryComplete();

    /// <summary>
    ///     Makes the stream of the turn that runs throw an exception after the events that were given.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public void Fault(Exception exception) => _events.Writer.TryComplete(exception);

    /// <summary>
    ///     Gets or sets what the readiness check finds.
    /// </summary>
    public AgentReadinessReport Readiness { get; set; } = new(AgentReadinessStatus.Ready, "Ready.");

    /// <summary>
    ///     Gets or sets an exception that the readiness check throws, or <see langword="null" /> for none.
    /// </summary>
    public Exception? ReadinessFailure { get; set; }

    /// <summary>
    ///     Gets the number of readiness checks that were made.
    /// </summary>
    public int ReadinessCalls { get; private set; }

    /// <inheritdoc />
    public Task<AgentReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        ReadinessCalls++;
        return ReadinessFailure is null ? Task.FromResult(Readiness) : Task.FromException<AgentReadinessReport>(ReadinessFailure);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<AgentEvent> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        LatestToken = cancellationToken;
        _events = Channel.CreateUnbounded<AgentEvent>(new UnboundedChannelOptions { AllowSynchronousContinuations = true });
        return Read(_events, cancellationToken);
    }

    /// <inheritdoc />
    public void ResetConversation() => ResetCount++;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    ///     Reads the events of a turn as they are given.
    /// </summary>
    /// <param name="channel">The events of the turn.</param>
    /// <param name="cancellationToken">The token of the turn.</param>
    /// <returns>The events.</returns>
    private async IAsyncEnumerable<AgentEvent> Read(
        Channel<AgentEvent> channel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            var more = false;
            try
            {
                more = await channel.Reader.WaitToReadAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (EndQuietlyOnCancellation)
            {
                // The real service ends the stream of a canceled turn without an event
            }

            if (!more)
            {
                yield break;
            }

            while (channel.Reader.TryRead(out var agentEvent))
            {
                yield return agentEvent;
            }
        }
    }
}

/// <summary>
///     A clock with timers for the chat tests that only moves when the test says so.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    /// <summary>
    ///     The timers that exist.
    /// </summary>
    private readonly List<ManualTimer> _timers = [];

    /// <summary>
    ///     The time in milliseconds.
    /// </summary>
    private long _now;

    /// <inheritdoc />
    public override long TimestampFrequency => 1000;

    /// <summary>
    ///     Gets the number of timers that are set and not disposed.
    /// </summary>
    public int ActiveTimers => _timers.Count(timer => timer.IsActive);

    /// <inheritdoc />
    public override long GetTimestamp() => _now;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, this);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>
    ///     Moves the time forward and runs the timers that fall due, in the order of their time.
    /// </summary>
    /// <param name="amount">How far to move.</param>
    public void Advance(TimeSpan amount)
    {
        var target = _now + (long)amount.TotalMilliseconds;
        while (true)
        {
            var next = _timers.Where(timer => timer.IsActive && timer.DueAt <= target).OrderBy(timer => timer.DueAt).FirstOrDefault();
            if (next is null)
            {
                break;
            }

            _now = Math.Max(_now, next.DueAt);
            next.Fire();
        }

        _now = target;
    }

    /// <summary>
    ///     Gives the time as the timers count it.
    /// </summary>
    /// <returns>The time in milliseconds.</returns>
    private long Now() => _now;

    /// <summary>
    ///     A timer that fires when the test moves the time.
    /// </summary>
    private sealed class ManualTimer : ITimer
    {
        /// <summary>
        ///     The method to call.
        /// </summary>
        private readonly TimerCallback _callback;

        /// <summary>
        ///     What the callback gets.
        /// </summary>
        private readonly object? _state;

        /// <summary>
        ///     The clock the timer counts on.
        /// </summary>
        private readonly ManualTimeProvider _owner;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ManualTimer" /> class.
        /// </summary>
        /// <param name="callback">The method to call.</param>
        /// <param name="state">What the callback gets.</param>
        /// <param name="owner">The clock.</param>
        public ManualTimer(TimerCallback callback, object? state, ManualTimeProvider owner)
        {
            _callback = callback;
            _state = state;
            _owner = owner;
        }

        /// <summary>
        ///     Gets the time at which the timer fires.
        /// </summary>
        public long DueAt { get; private set; }

        /// <summary>
        ///     The period in milliseconds, or zero for a timer that fires once.
        /// </summary>
        private long _period;

        /// <summary>
        ///     Gets a value indicating whether the timer is set and not disposed.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <inheritdoc />
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            IsActive = dueTime != Timeout.InfiniteTimeSpan;
            DueAt = _owner.Now() + (long)dueTime.TotalMilliseconds;
            _period = period == Timeout.InfiniteTimeSpan ? 0 : (long)period.TotalMilliseconds;
            return true;
        }

        /// <summary>
        ///     Fires the timer; a timer with a period is set to fire again after it.
        /// </summary>
        public void Fire()
        {
            if (_period > 0)
            {
                DueAt += _period;
            }
            else
            {
                IsActive = false;
            }

            _callback(_state);
        }

        /// <inheritdoc />
        public void Dispose() => IsActive = false;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
///     A synchronization context for the chat tests that runs what is posted to it at once, on the posting thread, and
///     counts it.
/// </summary>
internal sealed class CountingInlineContext : SynchronizationContext
{
    /// <summary>
    ///     Gets the number of items that were posted.
    /// </summary>
    public int PostCount { get; private set; }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state)
    {
        PostCount++;
        d(state);
    }
}

/// <summary>
///     Makes sent messages for the chat tests.
/// </summary>
internal static class ChatTestMessages
{
    /// <summary>
    ///     Makes a sent message with a payload.
    /// </summary>
    /// <param name="payload">The payload; any object, to test a message that has no payload that can be sent.</param>
    /// <returns>The message.</returns>
    public static SentMessage Sent(object payload) =>
        new(new ComposerDocument().Snapshot(), [], [], payload, FakeMessageBuilder.Now);

    /// <summary>
    ///     Makes a sent message of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The message.</returns>
    public static SentMessage SentText(string text) => Sent(ChatTestPayloads.OfText(text));
}

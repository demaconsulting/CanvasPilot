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

using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A dictation session that the test controls: it says words when told to, ends with a text the test chose, and
///     counts how often it was ended and disposed.
/// </summary>
internal sealed class FakeDictationSession : IDictationSession
{
    /// <inheritdoc />
    public event EventHandler? PartialTextChanged;

    /// <inheritdoc />
    public string PartialText { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string Problem { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the text that <see cref="EndAsync" /> returns.
    /// </summary>
    public string FinalText { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the exception that <see cref="EndAsync" /> throws, or <see langword="null" /> for none.
    /// </summary>
    public Exception? EndFailure { get; set; }

    /// <summary>
    ///     Gets the number of times the session was ended.
    /// </summary>
    public int EndCount { get; private set; }

    /// <summary>
    ///     Gets the number of times the session was disposed.
    /// </summary>
    public int DisposeCount { get; private set; }

    /// <summary>
    ///     Makes the recognizer hear words, as the session reports it.
    /// </summary>
    /// <param name="text">The words recognized so far.</param>
    public void Say(string text)
    {
        PartialText = text;
        PartialTextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<string> EndAsync(CancellationToken cancellationToken = default)
    {
        EndCount++;
        return EndFailure is null ? Task.FromResult(FinalText) : Task.FromException<string>(EndFailure);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     A dictation service that the test controls: it says whether dictation is available, can hold back the start until
///     the test lets it go, and hands out the session the test made.
/// </summary>
internal sealed class FakeDictationService : IDictationService
{
    /// <summary>
    ///     Gets or sets what <see cref="CheckAvailability" /> says.
    /// </summary>
    public DictationAvailability Availability { get; set; } = DictationAvailability.Ready;

    /// <summary>
    ///     Gets the session that <see cref="BeginAsync" /> starts.
    /// </summary>
    public FakeDictationSession Session { get; } = new();

    /// <summary>
    ///     Gets or sets the outcome that <see cref="BeginAsync" /> returns, or <see langword="null" /> for listening with
    ///     <see cref="Session" />.
    /// </summary>
    public DictationStart? Start { get; set; }

    /// <summary>
    ///     Gets or sets the exception that <see cref="BeginAsync" /> throws, or <see langword="null" /> for none.
    /// </summary>
    public Exception? BeginFailure { get; set; }

    /// <summary>
    ///     Gets or sets a gate that holds back the start until the test completes it, or <see langword="null" /> for no gate.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the start keeps waiting for the gate when it is cancelled, as a
    ///     start that has already got its session does when the user lets go at that very moment.
    /// </summary>
    public bool IgnoreCancellation { get; set; }

    /// <summary>
    ///     Gets the number of times listening was requested.
    /// </summary>
    public int BeginCount { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the request for listening was cancelled while it waited.
    /// </summary>
    public bool WasCanceled { get; private set; }

    /// <inheritdoc />
    public DictationAvailability CheckAvailability() => Availability;

    /// <inheritdoc />
    public Task<DictationAvailability> PrepareAsync(CancellationToken cancellationToken = default)
    {
        PrepareCount++;
        return PrepareFailure is null ? Task.FromResult(Availability) : Task.FromException<DictationAvailability>(PrepareFailure);
    }

    /// <summary>
    ///     Gets the number of times the engine was prepared ahead of time.
    /// </summary>
    public int PrepareCount { get; private set; }

    /// <summary>
    ///     Gets or sets the exception that <see cref="PrepareAsync" /> fails with, or <see langword="null" /> for none.
    /// </summary>
    public Exception? PrepareFailure { get; set; }

    /// <inheritdoc />
    public async Task<DictationStart> BeginAsync(CancellationToken cancellationToken = default)
    {
        BeginCount++;
        if (BeginFailure is not null)
        {
            throw BeginFailure;
        }

        if (Gate is not null)
        {
            try
            {
                await (IgnoreCancellation ? Gate.Task : Gate.Task.WaitAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                WasCanceled = true;
                throw;
            }
        }

        return Start ?? new DictationStart(Session, DictationAvailability.Ready);
    }
}

/// <summary>
///     A builder of sent messages that the test controls: it records what it was given, can be held back or made to fail,
///     and returns a message with a text as its payload.
/// </summary>
internal sealed class FakeMessageBuilder : IMessageBuilder
{
    /// <summary>
    ///     Gets the time every message is said to be sent at.
    /// </summary>
    public static DateTimeOffset Now { get; } = new(2026, 11, 20, 16, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     Gets or sets a gate that holds the build back until the test completes it, or <see langword="null" /> for none.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>
    ///     Gets or sets the exception the build fails with, or <see langword="null" /> for none.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    ///     Gets the messages the builder was given, in order.
    /// </summary>
    public List<ComposerSnapshot> Messages { get; } = [];

    /// <summary>
    ///     Gets the marks the builder was given at each call, in order.
    /// </summary>
    public List<IReadOnlyList<Mark>> Marks { get; } = [];

    /// <summary>
    ///     Gets the attachments the builder was given at each call, in order.
    /// </summary>
    public List<IReadOnlyList<StagedAttachment>> Attachments { get; } = [];

    /// <inheritdoc />
    public async Task<SentMessage> BuildAsync(
        ComposerSnapshot message,
        IReadOnlyList<Mark> marks,
        IReadOnlyList<StagedAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        Marks.Add(marks);
        Attachments.Add(attachments);
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        return new SentMessage(message, marks, attachments, "payload of: " + message.FlatText, Now);
    }
}

/// <summary>
///     A synchronization context that only collects the work that is posted to it, so that a test can run the work at
///     the moment it chooses, as the user interface thread would.
/// </summary>
internal sealed class QueueingSynchronizationContext : SynchronizationContext
{
    /// <summary>
    ///     The work that was posted and has not run.
    /// </summary>
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

    /// <summary>
    ///     Gets the number of items that were posted and have not run.
    /// </summary>
    public int Pending
    {
        get
        {
            lock (_posted)
            {
                return _posted.Count;
            }
        }
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_posted)
        {
            _posted.Enqueue((d, state));
        }
    }

    /// <summary>
    ///     Runs everything that was posted, in order.
    /// </summary>
    public void RunAll()
    {
        while (true)
        {
            (SendOrPostCallback Callback, object? State) item;
            lock (_posted)
            {
                if (_posted.Count == 0)
                {
                    return;
                }

                item = _posted.Dequeue();
            }

            item.Callback(item.State);
        }
    }
}

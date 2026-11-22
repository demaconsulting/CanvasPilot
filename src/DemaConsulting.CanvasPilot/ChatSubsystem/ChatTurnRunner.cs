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

using System.Text;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The logic of one turn of the conversation, without a window: it gives the message that was sent to the AI copilot,
///     reads the events of the turn, and keeps the transcript rows that show what happens, so that the panel only has to
///     show them and to follow <see cref="IsBusy" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>One turn at a time.</b> <see cref="RunTurnAsync(SentMessage, IReadOnlyList{AgentHistoryTurn}?)" /> starts a
///         turn and completes when it has ended. While it runs <see cref="IsBusy" /> is <see langword="true" />, and
///         <see cref="BusyChanged" /> is raised when it starts and when it ends, so that the panel can block the composer.
///         A turn that is asked for meanwhile is refused (<see cref="ChatTurnOutcome.Refused" />) and touches nothing.
///     </para>
///     <para>
///         <b>Rows.</b> The runner adds rows to the transcript that it was given, after the row of the message that was
///         sent. The text of the copilot is a <see cref="AssistantReplyViewModel" /> that streams; a tool call is a
///         <see cref="ToolActivityRowViewModel" />, which says <c>Created `x.svg`</c> or <c>Replaced `x.svg`</c> when the
///         tool wrote a file; a failure is a <see cref="NoticeRowViewModel" /> with what to do and whether the settings
///         can help. A reply ends when the copilot calls a tool, and the text after the tool starts a new reply, so the
///         transcript reads in the order things happened.
///     </para>
///     <para>
///         <b>Coalescing.</b> The copilot streams many small pieces of text. The runner collects them and appends them to
///         the reply at most once for every <see cref="UpdateInterval" />: the first piece at once, the pieces that arrive
///         within the interval together when it has passed, by a timer when the stream pauses before the next piece. The
///         clock and the timer are those of the <see cref="TimeProvider" /> that the runner is given. Any other event first
///         adds the text that is waiting, so the rows stay in order.
///     </para>
///     <para>
///         <b>Stop and failure.</b> <see cref="Stop" /> cancels the turn. The text written until then is kept and the reply
///         is marked as stopped, tool calls without a result are marked as stopped, and the outcome is
///         <see cref="ChatTurnOutcome.Stopped" />. A failure that the service reports, or an exception that the stream
///         throws, ends the turn with a failed reply and an error notice; nothing is thrown to the caller.
///     </para>
///     <para>
///         The class is owned by the user interface thread: it is called on it, and the rows are changed on it, because
///         the continuations of the turn and the pieces that the timer adds run on the synchronization context that the
///         turn started on. Without a context (tests) they run where they occur, and the runner guards what they share.
///         The logger gets facts only (the kind of the failure and the type of an exception), never the words of a
///         message or an answer.
///     </para>
/// </remarks>
public sealed class ChatTurnRunner : IDisposable
{
    /// <summary>
    ///     The name of the tool that writes files of the project, whose row says that a file was created or replaced.
    /// </summary>
    private const string WriteToolName = "project_write";

    /// <summary>
    ///     The shortest time between two updates of the text of a reply.
    /// </summary>
    public static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    ///     The copilot that answers.
    /// </summary>
    private readonly IAgentService _agent;

    /// <summary>
    ///     The transcript that the rows are added to.
    /// </summary>
    private readonly ICollection<TranscriptRowViewModel> _transcript;

    /// <summary>
    ///     The clock and the timers.
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Guards the text that waits and the rows that it is added to, which a timer and the turn can both reach.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    ///     The turn that runs, or <see langword="null" /> when none does.
    /// </summary>
    private Turn? _turn;

    /// <summary>
    ///     Whether the runner was disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatTurnRunner" /> class.
    /// </summary>
    /// <param name="agent">The AI copilot that answers.</param>
    /// <param name="transcript">The transcript that the rows of a turn are added to.</param>
    /// <param name="clock">The clock and timers, or <see langword="null" /> for the system's.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when the agent or the transcript is <see langword="null" />.</exception>
    public ChatTurnRunner(
        IAgentService agent,
        ICollection<TranscriptRowViewModel> transcript,
        TimeProvider? clock = null,
        RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(transcript);

        _agent = agent;
        _transcript = transcript;
        _clock = clock ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>
    ///     Raised when <see cref="IsBusy" /> changed, after it did.
    /// </summary>
    public event EventHandler? BusyChanged;

    /// <summary>
    ///     Gets a value indicating whether a turn runs.
    /// </summary>
    public bool IsBusy => _turn is not null;

    /// <summary>
    ///     Runs one turn for a message that was sent, and completes when the turn has ended.
    /// </summary>
    /// <param name="message">The message that was sent.</param>
    /// <param name="restoredHistory">The text of earlier turns for a new provider session, or <see langword="null" /> for none.</param>
    /// <returns>How the turn ended; <see cref="ChatTurnOutcome.Refused" /> when it was not started.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message" /> is <see langword="null" />.</exception>
    public Task<ChatTurnOutcome> RunTurnAsync(SentMessage message, IReadOnlyList<AgentHistoryTurn>? restoredHistory = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Payload is not MessagePayload payload)
        {
            // A message without a payload cannot be given to the copilot; say so and leave a running turn alone
            Add(new NoticeRowViewModel(NoticeSeverity.Error, "This message has no content that the copilot can be given."));
            return Task.FromResult(ChatTurnOutcome.Refused);
        }

        return RunTurnAsync(new AgentTurnRequest(payload) { RestoredHistory = restoredHistory ?? [] });
    }

    /// <summary>
    ///     Runs one turn for a request, and completes when the turn has ended. It never throws for what the copilot or its
    ///     provider does; a failure is a row in the transcript and the outcome.
    /// </summary>
    /// <param name="request">The message and the restored history.</param>
    /// <returns>How the turn ended; <see cref="ChatTurnOutcome.Refused" /> when it was not started.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request" /> is <see langword="null" />.</exception>
    public async Task<ChatTurnOutcome> RunTurnAsync(AgentTurnRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_turn is not null || _disposed)
        {
            return ChatTurnOutcome.Refused;
        }

        // The turn is busy from here on, before the first await, so that the panel can block the composer at once
        var turn = new Turn(SynchronizationContext.Current);
        _turn = turn;
        BusyChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            return await RunAsync(turn, request);
        }
        finally
        {
            EndTurn(turn);
            BusyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Stops the turn that runs, if any. The text written so far is kept and marked as stopped; the turn completes soon
    ///     after.
    /// </summary>
    public void Stop()
    {
        var turn = _turn;
        if (turn is null)
        {
            return;
        }

        turn.StopRequested = true;
        try
        {
            turn.Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The turn ended at the same moment; there is nothing left to stop
        }
    }

    /// <summary>
    ///     Stops the turn that runs and refuses new ones.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        Stop();
    }

    /// <summary>
    ///     Reads the events of the turn and shows them, and decides how the turn ended.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="request">What to ask.</param>
    /// <returns>How the turn ended.</returns>
    private async Task<ChatTurnOutcome> RunAsync(Turn turn, AgentTurnRequest request)
    {
        var token = turn.Cancellation.Token;
        ChatTurnOutcome? outcome = null;
        try
        {
            await foreach (var agentEvent in _agent.RunTurnAsync(request, token).WithCancellation(token))
            {
                outcome = Apply(turn, agentEvent);
                if (outcome is not null)
                {
                    break;
                }
            }

            outcome ??= turn.StopRequested ? FinishStopped(turn) : FinishFailed(turn, AgentFailureKind.Unexpected, "The copilot's answer ended before it was whole.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            outcome = FinishStopped(turn);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The service reports failures as events; this is a safeguard so that nothing ever reaches the interface
            _logger?.Log(LogLevel.Error, $"The turn of the copilot threw {ex.GetType().Name}.");
            outcome = FinishFailed(turn, AgentFailureKind.Unexpected, $"The copilot failed unexpectedly: {ex.Message}");
        }

        return outcome.Value;
    }

    /// <summary>
    ///     Shows one event in the transcript.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="agentEvent">The event.</param>
    /// <returns>How the turn ended when the event is the end of it, otherwise <see langword="null" />.</returns>
    private ChatTurnOutcome? Apply(Turn turn, AgentEvent agentEvent)
    {
        switch (agentEvent)
        {
            case AgentEvent.TextDelta delta:
                QueueText(turn, delta.Text);
                return null;

            case AgentEvent.Usage:
                // Counts of tokens are not shown
                return null;

            case AgentEvent.ToolStarted started:
                FlushText(turn);
                EndReply(turn);
                StartTool(turn, started);
                return null;

            case AgentEvent.ToolFinished finished:
                FlushText(turn);
                FinishTool(turn, finished);
                return null;

            case AgentEvent.FileWritten written:
                FlushText(turn);
                ReportFile(turn, written);
                return null;

            case AgentEvent.Completed:
                return FinishCompleted(turn);

            case AgentEvent.Failed failed:
                _logger?.Log(LogLevel.Warning, $"The turn of the copilot failed: {failed.Kind}.");
                return FinishFailed(turn, failed.Kind, failed.Message);

            default:
                return null;
        }
    }

    /// <summary>
    ///     Collects a piece of text and adds it to the reply now or later, so that the reply changes at most once for each
    ///     <see cref="UpdateInterval" />.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="text">The piece of text.</param>
    private void QueueText(Turn turn, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            turn.Pending.Append(text);
            var now = _clock.GetTimestamp();
            var waited = turn.LastUpdate is { } last ? _clock.GetElapsedTime(last, now) : UpdateInterval;
            if (waited >= UpdateInterval)
            {
                AppendPending(turn);
            }
            else if (turn.Timer is null)
            {
                // The stream may pause now, so a timer adds the text when the interval has passed
                turn.Timer = _clock.CreateTimer(_ => OnTimer(turn), null, UpdateInterval - waited, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>
    ///     Adds the text that waits to the reply at once, before an event that must come after it.
    /// </summary>
    /// <param name="turn">The turn.</param>
    private void FlushText(Turn turn)
    {
        lock (_gate)
        {
            AppendPending(turn);
        }
    }

    /// <summary>
    ///     Runs when the timer of the interval fires, on a thread of the timer, and hands the work to the thread of the
    ///     turn.
    /// </summary>
    /// <param name="turn">The turn that set the timer.</param>
    private void OnTimer(Turn turn)
    {
        if (turn.Context is { } context)
        {
            context.Post(_ => FlushText(turn), null);
        }
        else
        {
            FlushText(turn);
        }
    }

    /// <summary>
    ///     Adds the text that waits to the reply, starting the reply when the turn has none, and ends the timer. The caller
    ///     holds the lock.
    /// </summary>
    /// <param name="turn">The turn.</param>
    private void AppendPending(Turn turn)
    {
        turn.Timer?.Dispose();
        turn.Timer = null;
        if (turn.Pending.Length == 0 || turn.Ended)
        {
            return;
        }

        var text = turn.Pending.ToString();
        turn.Pending.Clear();
        turn.LastUpdate = _clock.GetTimestamp();
        if (turn.Reply is null)
        {
            turn.Reply = new AssistantReplyViewModel();
            _transcript.Add(turn.Reply);
        }

        turn.Reply.AppendText(text);
    }

    /// <summary>
    ///     Ends the reply that is being written, if any, because a tool is called: its text is whole.
    /// </summary>
    /// <param name="turn">The turn.</param>
    private static void EndReply(Turn turn)
    {
        turn.Reply?.Complete();
        turn.Reply = null;
    }

    /// <summary>
    ///     Adds the row of a tool call that started.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="started">The event.</param>
    private void StartTool(Turn turn, AgentEvent.ToolStarted started)
    {
        var row = new ToolActivityRowViewModel(
            started.CallId,
            started.Name,
            started.ArgumentSummary,
            ToolActivityText.Started(started.Name, started.ArgumentSummary));
        turn.Tools[started.CallId] = row;
        turn.ToolOrder.Add(row);
        Add(row);
    }

    /// <summary>
    ///     Ends the row of a tool call, making it when the start was not seen.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="finished">The event.</param>
    private void FinishTool(Turn turn, AgentEvent.ToolFinished finished)
    {
        if (!turn.Tools.TryGetValue(finished.CallId, out var row))
        {
            row = new ToolActivityRowViewModel(finished.CallId, finished.Name, string.Empty, ToolActivityText.Started(finished.Name, string.Empty));
            turn.Tools[finished.CallId] = row;
            turn.ToolOrder.Add(row);
            Add(row);
        }

        row.Finish(
            ToolActivityText.Finished(finished.Name, row.ArgumentSummary, finished.Succeeded, finished.Summary),
            finished.Succeeded,
            finished.HasImage);
    }

    /// <summary>
    ///     Says on the row of the latest write tool that a file was created or replaced, or adds a row for it when no write
    ///     tool is waiting for the report.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="written">The event.</param>
    private void ReportFile(Turn turn, AgentEvent.FileWritten written)
    {
        var sentence = ToolActivityText.Written(written.RelativePath, written.Replaced);
        var row = turn.ToolOrder.LastOrDefault(candidate =>
            string.Equals(candidate.ToolName, WriteToolName, StringComparison.Ordinal) && !candidate.HasFileReport);
        if (row is null)
        {
            row = new ToolActivityRowViewModel(WriteToolName, sentence, succeeded: true);
            turn.ToolOrder.Add(row);
            Add(row);
        }

        row.ReportFile(sentence);
    }

    /// <summary>
    ///     Ends a turn that finished its answer.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The outcome.</returns>
    private ChatTurnOutcome FinishCompleted(Turn turn)
    {
        FlushText(turn);
        EndReply(turn);
        AbandonTools(turn, stopped: false);
        return ChatTurnOutcome.Completed;
    }

    /// <summary>
    ///     Ends a turn that the user stopped: the text is kept and marked, and a notice is added when there is no reply to
    ///     mark.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <returns>The outcome.</returns>
    private ChatTurnOutcome FinishStopped(Turn turn)
    {
        FlushText(turn);
        if (turn.Reply is { IsStreaming: true } reply)
        {
            reply.Stop();
        }
        else
        {
            Add(new NoticeRowViewModel(NoticeSeverity.Information, "The copilot was stopped."));
        }

        turn.Reply = null;
        AbandonTools(turn, stopped: true);
        return ChatTurnOutcome.Stopped;
    }

    /// <summary>
    ///     Ends a turn that failed: the text is kept and marked, and an error notice says what happened and what to do.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="kind">Why the turn failed.</param>
    /// <param name="message">What happened, for the user.</param>
    /// <returns>The outcome.</returns>
    private ChatTurnOutcome FinishFailed(Turn turn, AgentFailureKind kind, string message)
    {
        FlushText(turn);
        turn.Reply?.Fail();
        turn.Reply = null;
        AbandonTools(turn, stopped: false);
        Add(new NoticeRowViewModel(
            NoticeSeverity.Error,
            string.IsNullOrWhiteSpace(message) ? "The copilot failed." : message,
            ToolActivityText.RemedyFor(kind),
            ToolActivityText.SettingsCanHelp(kind)));
        return ChatTurnOutcome.Failed;
    }

    /// <summary>
    ///     Ends the rows of the tool calls that have no result.
    /// </summary>
    /// <param name="turn">The turn.</param>
    /// <param name="stopped"><see langword="true" /> when the user stopped the turn.</param>
    private static void AbandonTools(Turn turn, bool stopped)
    {
        foreach (var row in turn.ToolOrder.Where(candidate => candidate.IsRunning))
        {
            row.Abandon(ToolActivityText.Abandoned(row.Summary, stopped));
        }
    }

    /// <summary>
    ///     Ends the turn: no more text is added, and the timer and the cancellation are released.
    /// </summary>
    /// <param name="turn">The turn.</param>
    private void EndTurn(Turn turn)
    {
        lock (_gate)
        {
            turn.Ended = true;
            turn.Pending.Clear();
            turn.Timer?.Dispose();
            turn.Timer = null;
        }

        _turn = null;
        turn.Cancellation.Dispose();
    }

    /// <summary>
    ///     Adds a row to the transcript.
    /// </summary>
    /// <param name="row">The row.</param>
    private void Add(TranscriptRowViewModel row)
    {
        lock (_gate)
        {
            _transcript.Add(row);
        }
    }

    /// <summary>
    ///     What belongs to one turn.
    /// </summary>
    private sealed class Turn
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Turn" /> class.
        /// </summary>
        /// <param name="context">The synchronization context that the turn started on, or <see langword="null" />.</param>
        internal Turn(SynchronizationContext? context)
        {
            Context = context;
        }

        /// <summary>Gets the synchronization context that the turn started on.</summary>
        internal SynchronizationContext? Context { get; }

        /// <summary>Gets what stops the turn.</summary>
        internal CancellationTokenSource Cancellation { get; } = new();

        /// <summary>Gets the text that waits to be added to the reply.</summary>
        internal StringBuilder Pending { get; } = new();

        /// <summary>Gets the rows of the tool calls by the id of the call.</summary>
        internal Dictionary<string, ToolActivityRowViewModel> Tools { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the rows of the tool calls in the order they were added.</summary>
        internal List<ToolActivityRowViewModel> ToolOrder { get; } = [];

        /// <summary>Gets or sets the reply that is being written, or <see langword="null" />.</summary>
        internal AssistantReplyViewModel? Reply { get; set; }

        /// <summary>Gets or sets the time of the last update of the reply, or <see langword="null" /> before the first.</summary>
        internal long? LastUpdate { get; set; }

        /// <summary>Gets or sets the timer that adds the waiting text, or <see langword="null" />.</summary>
        internal ITimer? Timer { get; set; }

        /// <summary>Gets or sets a value indicating whether <see cref="ChatTurnRunner.Stop" /> was called.</summary>
        internal bool StopRequested { get; set; }

        /// <summary>Gets or sets a value indicating whether the turn has ended.</summary>
        internal bool Ended { get; set; }
    }
}

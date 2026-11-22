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

using System.ComponentModel;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of the <see cref="ChatTurnRunner" /> unit: one turn of the conversation without a window, over a copilot that
///     answers with the events the test gives it and a clock that moves when the test says so.
/// </summary>
public sealed class ChatTurnRunnerTests : IDisposable
{
    /// <summary>
    ///     The copilot that answers.
    /// </summary>
    private readonly ControlledAgentService _agent = new();

    /// <summary>
    ///     The transcript that the rows are added to.
    /// </summary>
    private readonly List<TranscriptRowViewModel> _transcript = [];

    /// <summary>
    ///     The clock of the runner.
    /// </summary>
    private readonly ManualTimeProvider _clock = new();

    /// <summary>
    ///     The runner under test.
    /// </summary>
    private readonly ChatTurnRunner _runner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatTurnRunnerTests" /> class.
    /// </summary>
    public ChatTurnRunnerTests()
    {
        _runner = new ChatTurnRunner(_agent, _transcript, _clock);
    }

    /// <inheritdoc />
    public void Dispose() => _runner.Dispose();

    /// <summary>
    ///     Test that a turn gives the copilot the payload of the message that was sent and the restored history, and
    ///     answers with the outcome of the turn.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_GivesTheCopilotThePayloadAndTheRestoredHistory()
    {
        // Arrange: a message and the text of an earlier conversation
        var payload = ChatTestPayloads.OfText("Draw a box");
        AgentHistoryTurn[] history = [new(AgentHistoryRole.User, "Hello"), new(AgentHistoryRole.Assistant, "Hi")];

        // Act: run a turn that ends at once
        var run = _runner.RunTurnAsync(ChatTestMessages.Sent(payload), history);
        _agent.Emit(new AgentEvent.Completed());
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the copilot got exactly the payload and the history, and the turn completed
        var request = Assert.Single(_agent.Requests);
        Assert.Same(payload, request.Payload);
        Assert.Equal(history, request.RestoredHistory);
        Assert.Equal(ChatTurnOutcome.Completed, outcome);
        Assert.Empty(_transcript);
    }

    /// <summary>
    ///     Test that the text of the copilot is shown in one reply that streams, and is complete when the turn ends.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_StreamedText_GrowsOneReplyAndCompletesIt()
    {
        // Arrange: a turn that runs
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw a box"));

        // Act: two pieces of text more than the interval apart
        _agent.Emit(new AgentEvent.TextDelta("Hello"));
        var reply = Assert.IsType<AssistantReplyViewModel>(Assert.Single(_transcript));
        var duringStreaming = (reply.Text, reply.State, reply.IsComplete);
        _clock.Advance(ChatTurnRunner.UpdateInterval);
        _agent.Emit(new AgentEvent.TextDelta(" world"));
        var afterSecondPiece = reply.Text;
        _agent.Emit(new AgentEvent.Completed());
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the text grew in the one reply, which was plain while streaming and is complete now
        Assert.Equal(("Hello", AssistantReplyState.Streaming, false), duringStreaming);
        Assert.Equal("Hello world", afterSecondPiece);
        Assert.Same(reply, Assert.Single(_transcript));
        Assert.Equal("Hello world", reply.Text);
        Assert.Equal(AssistantReplyState.Completed, reply.State);
        Assert.True(reply.IsComplete);
        Assert.Equal(ChatTurnOutcome.Completed, outcome);
    }

    /// <summary>
    ///     Test that pieces of text that arrive within the interval are added to the reply together, at most once for
    ///     each interval, and that a pause of the stream does not hold the text back.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_ManySmallPieces_UpdateTheReplyAtMostOncePerInterval()
    {
        // Arrange: a turn, and a count of the updates of the text of the reply
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw a box"));
        _agent.Emit(new AgentEvent.TextDelta("a"));
        var reply = Assert.IsType<AssistantReplyViewModel>(Assert.Single(_transcript));
        var updates = new List<string>();
        reply.PropertyChanged += (_, e) => RecordText(updates, reply, e);

        // Act: nineteen more pieces, one millisecond apart, which is less than the interval in all
        for (var i = 0; i < 19; i++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(1));
            _agent.Emit(new AgentEvent.TextDelta("b"));
        }

        var beforeTheInterval = (reply.Text, updates.Count, _clock.ActiveTimers);

        // Act: the stream pauses and the interval passes
        _clock.Advance(TimeSpan.FromMilliseconds(40));
        var afterTheInterval = (reply.Text, updates.Count, _clock.ActiveTimers);

        // Act: a piece after a long pause is added at once
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        _agent.Emit(new AgentEvent.TextDelta("c"));
        var afterLongPause = (reply.Text, updates.Count);
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: nothing was added within the interval, and the waiting pieces were added together by the timer
        Assert.Equal(("a", 0, 1), beforeTheInterval);
        Assert.Equal(("a" + new string('b', 19), 1, 0), afterTheInterval);
        Assert.Equal(("a" + new string('b', 19) + "c", 2), afterLongPause);
    }

    /// <summary>
    ///     Test that the text that waits is added before a tool call is shown, so that the rows stay in the order in which
    ///     things happened, and that the text after a tool starts a new reply.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_TextToolText_AreThreeRowsInOrder()
    {
        // Arrange: a turn and text that waits for the interval
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw a box"));
        _agent.Emit(new AgentEvent.TextDelta("Let me look."));
        _clock.Advance(TimeSpan.FromMilliseconds(10));
        _agent.Emit(new AgentEvent.TextDelta(" Checking."));

        // Act: a tool is called and returns, and more text follows
        _agent.Emit(
            new AgentEvent.ToolStarted("call-1", "project_list", string.Empty),
            new AgentEvent.ToolFinished("call-1", "project_list", true, "2 files", false),
            new AgentEvent.TextDelta("Done."),
            new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: reply, tool, reply; the first reply is whole and holds all its text
        Assert.Equal([TranscriptRowKind.Assistant, TranscriptRowKind.Tool, TranscriptRowKind.Assistant], _transcript.Select(row => row.Kind));
        var first = Assert.IsType<AssistantReplyViewModel>(_transcript[0]);
        Assert.Equal("Let me look. Checking.", first.Text);
        Assert.True(first.IsComplete);
        var tool = Assert.IsType<ToolActivityRowViewModel>(_transcript[1]);
        Assert.Equal("Listed the project files", tool.Summary);
        Assert.Equal("Done.", Assert.IsType<AssistantReplyViewModel>(_transcript[2]).Text);
    }

    /// <summary>
    ///     Test that a tool row says what is under way while the call runs and what it did when the result arrives, for
    ///     each tool of the application.
    /// </summary>
    [Theory]
    [InlineData("render_image", "path=cover.svg", "Rendering `cover.svg`", true, "Rendered `cover.svg`")]
    [InlineData("project_read", "path=data/sales.csv", "Reading `data/sales.csv`", true, "Read `data/sales.csv`")]
    [InlineData("project_write", "path=cover.svg, content=(1,204 characters)", "Writing `cover.svg`", true, "Wrote `cover.svg`")]
    [InlineData("project_list", "", "Listing the project files", true, "Listed the project files")]
    [InlineData("project_list", "folder=drawings", "Listing `drawings`", true, "Listed `drawings`")]
    [InlineData("canvasnet_help", "topic=svg", "Looking up CanvasNet help", true, "Looked up CanvasNet help")]
    [InlineData("style_get", "", "Reading the style guidance", true, "Read the style guidance")]
    [InlineData("style_check", "path=cover.svg", "Checking the style", true, "Checked the style")]
    [InlineData("other_tool", "a=1", "Running other_tool", true, "Ran other_tool")]
    public async Task ChatTurnRunner_RunTurn_ToolCall_RowSaysWhatIsUnderWayAndThenWhatWasDone(
        string tool,
        string arguments,
        string started,
        bool succeeded,
        string finished)
    {
        // Arrange: a turn
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));

        // Act: the call starts and then ends
        _agent.Emit(new AgentEvent.ToolStarted("c1", tool, arguments));
        var row = Assert.IsType<ToolActivityRowViewModel>(Assert.Single(_transcript));
        var whileRunning = (row.Summary, row.IsRunning);
        _agent.Emit(new AgentEvent.ToolFinished("c1", tool, succeeded, "ok", false), new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the row was made at the start and ended with the result
        Assert.Equal((started, true), whileRunning);
        Assert.Equal(finished, row.Summary);
        Assert.False(row.IsRunning);
        Assert.True(row.Succeeded);
        Assert.Equal("c1", row.CallId);
        Assert.Equal(tool, row.ToolName);
    }

    /// <summary>
    ///     Test that a tool that failed says what could not be done and why, and that a result with a picture is marked.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_ToolFailureAndPicture_AreShownOnTheRow()
    {
        // Arrange: a turn
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));

        // Act: one call fails, one returns a picture, and one result has no start
        _agent.Emit(
            new AgentEvent.ToolStarted("c1", "render_image", "path=bad.svg"),
            new AgentEvent.ToolFinished("c1", "render_image", false, "SVG: unexpected end", false),
            new AgentEvent.ToolStarted("c2", "render_image", "path=good.svg"),
            new AgentEvent.ToolFinished("c2", "render_image", true, "A picture.", true),
            new AgentEvent.ToolFinished("c3", "project_read", false, string.Empty, false),
            new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the failure has its reason, the picture is marked, and the result without a start still has a row
        var rows = _transcript.OfType<ToolActivityRowViewModel>().ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal("Could not render `bad.svg`: SVG: unexpected end", rows[0].Summary);
        Assert.False(rows[0].Succeeded);
        Assert.Equal("Rendered `good.svg`", rows[1].Summary);
        Assert.True(rows[1].HasImage);
        Assert.Equal("Could not read a file.", rows[2].Summary);
        Assert.False(rows[2].Succeeded);
    }

    /// <summary>
    ///     Test that a file that was written is shown as created or replaced on the row of the write tool, whichever of the
    ///     tool result and the file event comes first.
    /// </summary>
    [Theory]
    [InlineData(false, true, "Created `x.svg`")]
    [InlineData(true, true, "Replaced `x.svg`")]
    [InlineData(false, false, "Created `x.svg`")]
    [InlineData(true, false, "Replaced `x.svg`")]
    public async Task ChatTurnRunner_RunTurn_FileWritten_RowSaysCreatedOrReplaced(bool replaced, bool fileEventFirst, string expected)
    {
        // Arrange: a turn with a write call
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.ToolStarted("c1", "project_write", "path=x.svg, content=(40 characters)"));

        // Act: the file event and the tool result in either order
        AgentEvent file = new AgentEvent.FileWritten("x.svg", replaced);
        AgentEvent result = new AgentEvent.ToolFinished("c1", "project_write", true, "File 'x.svg' written.", false);
        AgentEvent[] events = fileEventFirst ? [file, result] : [result, file];
        _agent.Emit(events);
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: one row, which says what happened to the file
        var row = Assert.IsType<ToolActivityRowViewModel>(Assert.Single(_transcript));
        Assert.Equal(expected, row.Summary);
        Assert.True(row.Succeeded);
        Assert.False(row.IsRunning);
    }

    /// <summary>
    ///     Test that a file event with no write call waiting gets a row of its own, and that a write that failed is not
    ///     hidden by the report of an earlier file.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_FileWrittenWithoutCallAndFailedWrite_AreHandled()
    {
        // Arrange: a turn
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));

        // Act: a file event with no call, and a write call whose result says it failed
        _agent.Emit(
            new AgentEvent.FileWritten("a.svg", false),
            new AgentEvent.ToolStarted("c1", "project_write", "path=b.svg"),
            new AgentEvent.ToolFinished("c1", "project_write", false, "Refused: not allowed", false),
            new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the first row is its own, and the second shows the refusal
        var rows = _transcript.OfType<ToolActivityRowViewModel>().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Created `a.svg`", rows[0].Summary);
        Assert.Equal("Could not write `b.svg`: Refused: not allowed", rows[1].Summary);
        Assert.False(rows[1].Succeeded);
    }

    /// <summary>
    ///     Test that the busy state is set before the turn is asked, is announced when it starts and when it ends, and that
    ///     a second turn that is asked for meanwhile is refused and touches nothing.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_IsBusyWhileRunningAndRefusesASecondTurn()
    {
        // Arrange: a runner that tells when it is busy
        var states = new List<bool>();
        _runner.BusyChanged += (_, _) => states.Add(_runner.IsBusy);
        Assert.False(_runner.IsBusy);

        // Act: start a turn, ask for another one, and end the first
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("first"));
        var busyDuring = _runner.IsBusy;
        var second = await _runner.RunTurnAsync(ChatTestMessages.SentText("second")).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        _agent.Emit(new AgentEvent.TextDelta("answer"), new AgentEvent.Completed());
        var first = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: busy exactly during the first turn, announced twice, the second turn was refused without a request
        Assert.True(busyDuring);
        Assert.False(_runner.IsBusy);
        Assert.Equal([true, false], states);
        Assert.Equal(ChatTurnOutcome.Refused, second);
        Assert.Equal(ChatTurnOutcome.Completed, first);
        Assert.Single(_agent.Requests);
        Assert.Single(_transcript);

        // Act: a turn after the first one works
        var next = _runner.RunTurnAsync(ChatTestMessages.SentText("third"));
        _agent.Emit(new AgentEvent.Completed());

        // Assert: the runner is usable again
        Assert.Equal(ChatTurnOutcome.Completed, await next.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(2, _agent.Requests.Count);
    }

    /// <summary>
    ///     Test that stopping keeps the text written so far, including text that waited for the interval, marks the reply
    ///     as stopped, and makes the runner idle again, whether the stream ends quietly or throws the cancellation.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChatTurnRunner_Stop_KeepsPartialTextAndMarksTheReplyStopped(bool streamEndsQuietly)
    {
        // Arrange: a turn with text that was shown and text that waits
        _agent.EndQuietlyOnCancellation = streamEndsQuietly;
        var states = new List<bool>();
        _runner.BusyChanged += (_, _) => states.Add(_runner.IsBusy);
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.TextDelta("Partial"));
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        _agent.Emit(new AgentEvent.TextDelta(" answer"));

        // Act: stop
        _runner.Stop();
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the whole text is kept in a stopped reply, the copilot was told to stop, and the runner is idle
        var reply = Assert.IsType<AssistantReplyViewModel>(Assert.Single(_transcript));
        Assert.Equal("Partial answer", reply.Text);
        Assert.Equal(AssistantReplyState.Stopped, reply.State);
        Assert.Equal("Stopped", reply.StatusLabel);
        Assert.False(reply.IsComplete);
        Assert.Equal(ChatTurnOutcome.Stopped, outcome);
        Assert.True(_agent.LatestToken.IsCancellationRequested);
        Assert.False(_runner.IsBusy);
        Assert.Equal([true, false], states);
        Assert.Equal(0, _clock.ActiveTimers);
    }

    /// <summary>
    ///     Test that stopping before any text was written, or while a tool runs, says so in the transcript and ends the
    ///     rows of the calls that have no result.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Stop_WithoutTextAndWithRunningTool_AddsNoticeAndEndsTheToolRow()
    {
        // Arrange: a turn that is in the middle of a tool call
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.ToolStarted("c1", "render_image", "path=cover.svg"));

        // Act: stop
        _runner.Stop();
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the tool row was ended as stopped and a notice says that the copilot was stopped
        Assert.Equal(ChatTurnOutcome.Stopped, outcome);
        var tool = Assert.IsType<ToolActivityRowViewModel>(_transcript[0]);
        Assert.Equal("Rendering `cover.svg` (stopped)", tool.Summary);
        Assert.False(tool.IsRunning);
        Assert.False(tool.Succeeded);
        var notice = Assert.IsType<NoticeRowViewModel>(_transcript[1]);
        Assert.Equal("The copilot was stopped.", notice.Message);
        Assert.False(notice.IsError);
    }

    /// <summary>
    ///     Test that stopping when no turn runs does nothing, and that a turn that ended on its own before the stop reached
    ///     it stays completed.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Stop_WhenIdle_DoesNothingAndCompletedTurnStaysCompleted()
    {
        // Arrange / Act: stop with nothing running
        _runner.Stop();
        Assert.False(_runner.IsBusy);

        // Act: a turn that completes, and a stop after it
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.TextDelta("done"), new AgentEvent.Completed());
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        _runner.Stop();

        // Assert: nothing changed
        Assert.Equal(ChatTurnOutcome.Completed, outcome);
        Assert.Equal(AssistantReplyState.Completed, Assert.IsType<AssistantReplyViewModel>(Assert.Single(_transcript)).State);
    }

    /// <summary>
    ///     Test that a failure that the copilot reports keeps the partial text in a failed reply and adds an error notice
    ///     with the message, a remedy, and the offer to open the settings exactly for the causes that the settings can fix.
    /// </summary>
    [Theory]
    [InlineData(AgentFailureKind.NotReady, true, "Check the AI provider in Settings.")]
    [InlineData(AgentFailureKind.Authentication, true, "Check the sign-in of the AI provider in Settings.")]
    [InlineData(AgentFailureKind.ProtocolMismatch, true, "Update the AI provider, or choose another one in Settings.")]
    [InlineData(AgentFailureKind.ModelNotFound, true, "Choose a model that the AI provider has in Settings.")]
    [InlineData(AgentFailureKind.ConnectionLost, true, "Check that the AI provider is running and its address in Settings, then send the message again.")]
    [InlineData(AgentFailureKind.Busy, false, "Wait for the running turn to end, or press Stop.")]
    [InlineData(AgentFailureKind.DataInsideProject, false, "Open a project folder that does not contain the data folders of CanvasPilot.")]
    [InlineData(AgentFailureKind.Unexpected, false, "Send the message again. If it keeps failing, look at the application log.")]
    public async Task ChatTurnRunner_RunTurn_Failure_KeepsPartialTextAndAddsErrorNoticeWithRemedy(AgentFailureKind kind, bool canOpenSettings, string remedy)
    {
        // Arrange: a turn with some text
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.TextDelta("Starting"));

        // Act: the turn fails
        _agent.Emit(new AgentEvent.Failed(kind, "The provider said no."));
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the text is kept in a failed reply, followed by the error notice
        Assert.Equal(ChatTurnOutcome.Failed, outcome);
        var reply = Assert.IsType<AssistantReplyViewModel>(_transcript[0]);
        Assert.Equal(("Starting", AssistantReplyState.Failed, "Failed"), (reply.Text, reply.State, reply.StatusLabel));
        var notice = Assert.IsType<NoticeRowViewModel>(_transcript[1]);
        Assert.True(notice.IsError);
        Assert.Equal("The provider said no.", notice.Message);
        Assert.Equal(remedy, notice.Remedy);
        Assert.True(notice.HasRemedy);
        Assert.Equal(canOpenSettings, notice.CanOpenSettings);
        Assert.False(_runner.IsBusy);
    }

    /// <summary>
    ///     Test that an exception of the stream, and a stream that ends without saying how the turn ended, are failures in
    ///     the transcript and never reach the caller, and that the exception is not logged with its text.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_StreamThrowsOrEndsEarly_IsAFailureRowAndNeverThrows()
    {
        // Arrange: a turn whose stream throws
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.TextDelta("Begin"));

        // Act: the stream throws
        _agent.Fault(new InvalidOperationException("boom"));
        var thrown = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: a failed reply and a notice that names the problem, and the runner is idle
        Assert.Equal(ChatTurnOutcome.Failed, thrown);
        Assert.Equal(AssistantReplyState.Failed, Assert.IsType<AssistantReplyViewModel>(_transcript[0]).State);
        var notice = Assert.IsType<NoticeRowViewModel>(_transcript[1]);
        Assert.Contains("boom", notice.Message, StringComparison.Ordinal);
        Assert.False(notice.CanOpenSettings);
        Assert.False(_runner.IsBusy);

        // Act: a stream that ends with no end event
        _transcript.Clear();
        var second = _runner.RunTurnAsync(ChatTestMessages.SentText("again"));
        _agent.EndStream();
        var early = await second.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: that is a failure too
        Assert.Equal(ChatTurnOutcome.Failed, early);
        Assert.True(Assert.IsType<NoticeRowViewModel>(Assert.Single(_transcript)).IsError);
    }

    /// <summary>
    ///     Test that a message whose payload is not the payload of the application is refused with an error notice and
    ///     does not start a turn, and that the arguments are checked.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_MessageWithoutPayload_IsRefusedWithANotice()
    {
        // Act: run a turn for a message with some other payload
        var outcome = await _runner.RunTurnAsync(ChatTestMessages.Sent("not a payload")).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: refused, with an error row, and the copilot was never asked
        Assert.Equal(ChatTurnOutcome.Refused, outcome);
        Assert.Empty(_agent.Requests);
        Assert.True(Assert.IsType<NoticeRowViewModel>(Assert.Single(_transcript)).IsError);
        Assert.False(_runner.IsBusy);
        Assert.Throws<ArgumentNullException>(() => new ChatTurnRunner(null!, _transcript));
        Assert.Throws<ArgumentNullException>(() => new ChatTurnRunner(_agent, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _runner.RunTurnAsync((SentMessage)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _runner.RunTurnAsync((AgentTurnRequest)null!));
    }

    /// <summary>
    ///     Test that a turn that ends while text still waits for the interval adds the text and leaves no timer, and that a
    ///     call that has no result when the turn ends is ended as without a result.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_CompletedWhileTextWaits_AddsTheTextAndLeavesNoTimer()
    {
        // Arrange: text that waits
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.TextDelta("a"));
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        _agent.Emit(new AgentEvent.TextDelta("b"), new AgentEvent.Usage(10, 20));
        var reply = Assert.IsType<AssistantReplyViewModel>(Assert.Single(_transcript));
        Assert.Equal("a", reply.Text);

        // Act: the turn completes before the timer fired
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Assert: the waiting text was added once and the reply is complete; no timer is left
        Assert.Equal("ab", reply.Text);
        Assert.True(reply.IsComplete);
        Assert.Equal(0, _clock.ActiveTimers);
        Assert.Single(_transcript);
    }

    /// <summary>
    ///     Test that a call that has no result when the turn completes is ended as without a result.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_CompletedWithRunningTool_EndsTheToolRowWithoutResult()
    {
        // Arrange: a call that never returns
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.ToolStarted("c1", "render_image", "path=cover.svg"));

        // Act: the turn completes
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the row is ended and says so
        var row = Assert.IsType<ToolActivityRowViewModel>(Assert.Single(_transcript));
        Assert.Equal("Rendering `cover.svg` (no result)", row.Summary);
        Assert.False(row.IsRunning);
        Assert.False(row.Succeeded);
    }

    /// <summary>
    ///     Test that the text that waits for the interval is added on the synchronization context that the turn started
    ///     on, and not on the thread of the timer.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_RunTurn_TimerFlush_RunsOnTheContextOfTheTurn()
    {
        // Arrange: start the turn on a context that counts what is posted to it
        var context = new CountingInlineContext();
        var previous = SynchronizationContext.Current;
        Task<ChatTurnOutcome> run;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        _agent.Emit(new AgentEvent.TextDelta("a"));
        var reply = Assert.IsType<AssistantReplyViewModel>(Assert.Single(_transcript));
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        _agent.Emit(new AgentEvent.TextDelta("b"));
        var postsBefore = context.PostCount;

        // Act: the timer fires
        _clock.Advance(TimeSpan.FromMilliseconds(100));

        // Assert: the text was added through the context
        Assert.Equal("ab", reply.Text);
        Assert.True(context.PostCount > postsBefore);
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that disposing the runner stops the turn that runs and that a disposed runner refuses turns.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Dispose_StopsTheRunningTurnAndRefusesNewOnes()
    {
        // Arrange: a turn that runs
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("go"));
        _agent.Emit(new AgentEvent.TextDelta("partial"));

        // Act: dispose
        _runner.Dispose();
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var after = await _runner.RunTurnAsync(ChatTestMessages.SentText("late")).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the turn was stopped and no new turn started
        Assert.Equal(ChatTurnOutcome.Stopped, outcome);
        Assert.Equal(ChatTurnOutcome.Refused, after);
        Assert.Single(_agent.Requests);
    }

    /// <summary>
    ///     Records the text of a reply when it changed.
    /// </summary>
    /// <param name="updates">Where the texts are recorded.</param>
    /// <param name="reply">The reply.</param>
    /// <param name="e">The change.</param>
    private static void RecordText(List<string> updates, AssistantReplyViewModel reply, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssistantReplyViewModel.Text))
        {
            updates.Add(reply.Text);
        }
    }
}

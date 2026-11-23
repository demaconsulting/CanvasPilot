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

using System.Globalization;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of the live status of a turn in <see cref="ChatTurnRunner" />: the phase that the copilot reports, the
///     elapsed time that a timer of the clock ticks, the row that shows the progress of a file that is being written, and
///     the hint about a long wait, on a clock that moves when the test says so.
/// </summary>
public sealed class ChatTurnRunnerStatusTests : IDisposable
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
    ///     How many times the status changed.
    /// </summary>
    private int _changes;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatTurnRunnerStatusTests" /> class.
    /// </summary>
    public ChatTurnRunnerStatusTests()
    {
        _runner = new ChatTurnRunner(_agent, _transcript, _clock);
        _runner.StatusChanged += (_, _) => _changes++;
    }

    /// <inheritdoc />
    public void Dispose() => _runner.Dispose();

    /// <summary>
    ///     Test that a turn starts with the working text and no time, that the timer ticks the elapsed time once a second
    ///     and tells the observers, and that the status is empty and the timers are gone when the turn has ended.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Status_Elapsed_IsTickedOnceASecondAndClearedAtTheEnd()
    {
        // Arrange and act: a turn starts
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw"));
        var atStart = (_runner.StatusText, _runner.ElapsedText, _runner.HintText, _changes);

        // Act: less than a second, then one second, then more than a minute
        _clock.Advance(TimeSpan.FromMilliseconds(900));
        var beforeTheTick = (_runner.ElapsedText, _changes);
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        var afterOneSecond = (_runner.ElapsedText, _changes);
        _clock.Advance(TimeSpan.FromSeconds(65));
        var afterAMinute = _runner.ElapsedText;
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((ChatTurnRunner.WorkingText, "0:00", string.Empty, 0), atStart);
        Assert.Equal(("0:00", 0), beforeTheTick);
        Assert.Equal(("0:01", 1), afterOneSecond);
        Assert.Equal("1:06", afterAMinute);
        Assert.Equal((string.Empty, string.Empty, string.Empty), (_runner.StatusText, _runner.ElapsedText, _runner.HintText));
        Assert.Equal(0, _clock.ActiveTimers);
    }

    /// <summary>
    ///     Test that the elapsed time is written as hours, minutes and seconds from an hour on.
    /// </summary>
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(59, "0:59")]
    [InlineData(60, "1:00")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(7384, "2:03:04")]
    public void ChatTurnRunner_FormatElapsed_WritesMinutesAndSeconds(int seconds, string expected)
    {
        // Act and assert
        Assert.Equal(expected, ChatTurnRunner.FormatElapsed(TimeSpan.FromSeconds(seconds)));
    }

    /// <summary>
    ///     Test that the status follows what the copilot reports: thinking, the size of the response, the progress of a
    ///     file that is written (with the bytes so far), the tool that runs, the wait for the model, and the answer.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Status_FollowsWhatTheCopilotReports()
    {
        // Arrange
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw"));
        var phases = new List<string>();

        // Act: every kind of report, one after the other
        _agent.Emit(new AgentEvent.Thinking());
        phases.Add(_runner.StatusText);
        _agent.Emit(new AgentEvent.ModelWorking(2048));
        phases.Add(_runner.StatusText);
        _agent.Emit(new AgentEvent.ToolProgress("c1", "project_write", 10 * 1024, "art/flow.svg"));
        phases.Add(_runner.StatusText);
        _agent.Emit(new AgentEvent.ToolStarted("c1", "project_write", "path=art/flow.svg, content=(10,240 characters)"));
        phases.Add(_runner.StatusText);
        _agent.Emit(new AgentEvent.ToolFinished("c1", "project_write", true, "Created art/flow.svg", false));
        phases.Add(_runner.StatusText);
        _agent.Emit(new AgentEvent.TextDelta("Done."));
        phases.Add(_runner.StatusText);
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        var size = (double bytes) => string.Format(CultureInfo.CurrentCulture, "{0:0.0} KB", bytes / 1024);
        Assert.Equal(
            [
                ChatTurnRunner.ThinkingText,
                $"{ChatTurnRunner.ModelWritingText} {size(2048)}",
                $"Writing `art/flow.svg`... {size(10 * 1024)}",
                "Writing `art/flow.svg`...",
                ChatTurnRunner.WaitingText,
                ChatTurnRunner.AnsweringText,
            ],
            phases);
        Assert.True(_changes >= phases.Count);
    }

    /// <summary>
    ///     Test that the progress of the arguments of a call makes one running row that the later progress and the start of
    ///     the call update and the end of the call finishes, and that the rows stay in order with the text before them.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Progress_MakesOneRowThatTheCallUpdatesAndFinishes()
    {
        // Arrange: text, then the model starts to write a file
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw"));
        _agent.Emit(new AgentEvent.TextDelta("I will draw it."));

        // Act: progress twice, the start of the call, its end
        _agent.Emit(new AgentEvent.ToolProgress("c1", "project_write", 100, null));
        var afterFirst = (_transcript.Count, ((ToolActivityRowViewModel)_transcript[1]).Summary, ((ToolActivityRowViewModel)_transcript[1]).IsRunning);
        _agent.Emit(new AgentEvent.ToolProgress("c1", "project_write", 5000, "a.svg"));
        var row = Assert.IsType<ToolActivityRowViewModel>(_transcript[1]);
        var afterSecond = (_transcript.Count, row.Summary);
        _agent.Emit(new AgentEvent.ToolStarted("c1", "project_write", "path=a.svg, content=(5,000 characters)"));
        var afterStart = (_transcript.Count, row.Summary, row.ArgumentSummary, row.IsRunning);
        _agent.Emit(new AgentEvent.ToolFinished("c1", "project_write", true, "Created a.svg", false));
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((2, "Writing a file... 100 B", true), afterFirst);
        Assert.Equal((2, $"Writing `a.svg`... {ToolActivityText_FormatSize(5000)}"), afterSecond);
        Assert.Equal((2, "Writing `a.svg`", "path=a.svg, content=(5,000 characters)", true), afterStart);
        Assert.False(row.IsRunning);
        Assert.True(row.Succeeded);
        Assert.Equal("Wrote `a.svg`", row.Summary);
        Assert.IsType<AssistantReplyViewModel>(_transcript[0]);
        Assert.Equal(2, _transcript.Count);
    }

    /// <summary>
    ///     Test that the hint about a long wait appears after the delay without any event and no tool running, that an event
    ///     takes it away, and that it is not shown while a tool runs.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Hint_AppearsAfterALongSilenceAndIsClearedByAnEvent()
    {
        // Arrange
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw"));

        // Act: nearly the delay, then the delay, then an event
        _clock.Advance(ChatTurnRunner.HintDelay - TimeSpan.FromSeconds(1));
        var before = _runner.HintText;
        _clock.Advance(TimeSpan.FromSeconds(1));
        var shown = _runner.HintText;
        _agent.Emit(new AgentEvent.Thinking());
        var cleared = _runner.HintText;

        // Act: a tool runs for longer than the delay
        _agent.Emit(new AgentEvent.ToolStarted("c1", "render_image", "path=a.svg"));
        _clock.Advance(ChatTurnRunner.HintDelay * 2);
        var whileATool = _runner.HintText;
        _agent.Emit(new AgentEvent.ToolFinished("c1", "render_image", true, "ok", true));
        _clock.Advance(ChatTurnRunner.HintDelay);
        var afterTheTool = _runner.HintText;
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(string.Empty, before);
        Assert.Equal(ChatTurnRunner.LongWaitHint, shown);
        Assert.Equal(string.Empty, cleared);
        Assert.Equal(string.Empty, whileATool);
        Assert.Equal(ChatTurnRunner.LongWaitHint, afterTheTool);
    }

    /// <summary>
    ///     Test that a failure and a stop end the status the same way as a completion: nothing is left, no timer runs.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Status_FailureAndStop_ClearTheStatus()
    {
        // Arrange and act: a turn that fails
        var failing = _runner.RunTurnAsync(ChatTestMessages.SentText("One"));
        _agent.Emit(new AgentEvent.Failed(AgentFailureKind.Unexpected, "It broke."));
        await failing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var afterFailure = (_runner.StatusText, _clock.ActiveTimers);

        // Act: a turn that is stopped
        var stopped = _runner.RunTurnAsync(ChatTestMessages.SentText("Two"));
        _runner.Stop();
        await stopped.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var afterStop = (_runner.StatusText, _clock.ActiveTimers);

        // Assert
        Assert.Equal((string.Empty, 0), afterFailure);
        Assert.Equal((string.Empty, 0), afterStop);
    }

    /// <summary>
    ///     Test that a Stop shows the stopping state at once, while the provider is still ending its stream, keeps it
    ///     whatever the copilot still reports and without a hint, ignores a second Stop, and ends the turn as stopped with an
    ///     empty status when the stream ends.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Stop_ShowsStoppingUntilTheStreamEnds()
    {
        // Arrange: a copilot that is slow to stop, and a turn that has written some text
        _agent.IgnoreCancellation = true;
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw"));
        _agent.Emit(new AgentEvent.TextDelta("Let me"));
        var changesBefore = _changes;

        // Act: Stop, twice, while the copilot still reports and a long time passes
        _runner.Stop();
        _runner.Stop();
        var stopping = (_runner.IsBusy, _runner.IsStopping, _runner.StatusText, _changes > changesBefore);
        _agent.Emit(new AgentEvent.Thinking());
        _clock.Advance(ChatTurnRunner.HintDelay * 2);
        var later = (_runner.StatusText, _runner.HintText, _runner.IsStopping);
        _agent.EndStream();
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((true, true, ChatTurnRunner.StoppingText, true), stopping);
        Assert.Equal((ChatTurnRunner.StoppingText, string.Empty, true), later);
        Assert.Equal(ChatTurnOutcome.Stopped, outcome);
        Assert.Equal((false, false, string.Empty), (_runner.IsBusy, _runner.IsStopping, _runner.StatusText));
        Assert.Equal(AssistantReplyState.Stopped, Assert.IsType<AssistantReplyViewModel>(Assert.Single(_transcript)).State);
    }

    /// <summary>
    ///     Writes a count of bytes the way the rows do.
    /// </summary>
    /// <param name="bytes">The count.</param>
    /// <returns>The text.</returns>
    private static string ToolActivityText_FormatSize(long bytes)
    {
        return string.Format(CultureInfo.CurrentCulture, "{0:0.0} KB", bytes / 1024.0);
    }
}

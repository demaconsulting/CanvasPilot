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

using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of the usage caption of a turn: the figures of <see cref="TurnUsage" /> and the line that
///     <see cref="ChatTurnRunner" /> puts under the last reply of the turn, on a clock that moves when the test says so.
/// </summary>
public sealed class ChatTurnRunnerUsageTests : IDisposable
{
    /// <summary>The copilot that answers.</summary>
    private readonly ControlledAgentService _agent = new();

    /// <summary>The transcript that the rows are added to.</summary>
    private readonly List<TranscriptRowViewModel> _transcript = [];

    /// <summary>The clock of the runner.</summary>
    private readonly ManualTimeProvider _clock = new();

    /// <summary>The runner under test.</summary>
    private readonly ChatTurnRunner _runner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatTurnRunnerUsageTests" /> class.
    /// </summary>
    public ChatTurnRunnerUsageTests()
    {
        _runner = new ChatTurnRunner(_agent, _transcript, _clock);
    }

    /// <inheritdoc />
    public void Dispose() => _runner.Dispose();

    /// <summary>
    ///     Test that the tokens of the model calls are added up, that the multiplier is the largest one and not a sum, and
    ///     that the caption says what was reported and the time.
    /// </summary>
    [Fact]
    public void TurnUsage_Format_SumsTokensKeepsTheLargestMultiplier()
    {
        // Arrange
        var usage = new TurnUsage();
        usage.Add(new AgentEvent.Usage(8000, 1200, 100, 15, "claude-opus-5.5"));
        usage.Add(new AgentEvent.Usage(4400, 6000, 300, 15, "claude-opus-5.5"));

        // Act
        var caption = usage.Format(TimeSpan.FromSeconds(205), stopped: false);

        // Assert
        Assert.Equal("12.4k in, 7.2k out, 400 reasoning, model x15 premium, 3:25", caption);
    }

    /// <summary>
    ///     Test that the context size that was asked for is named in the caption of the turn, after the multiplier, and that a
    ///     turn without a chosen size says nothing about it.
    /// </summary>
    [Fact]
    public void TurnUsage_Format_ContextSize_IsNamedAfterTheMultiplier()
    {
        // Arrange: a turn with the larger size on both calls, and one without a size
        var sized = new TurnUsage();
        sized.Add(new AgentEvent.Usage(8000, 1200, null, 15, "claude-opus-5.5", "long context"));
        sized.Add(new AgentEvent.Usage(4400, 6000, null, 15, "claude-opus-5.5", "long context"));
        var plain = new TurnUsage();
        plain.Add(new AgentEvent.Usage(8000, 1200, null, 15, "claude-opus-5.5"));

        // Act and assert
        Assert.Equal("long context", sized.ContextSize);
        Assert.Equal("12.4k in, 7.2k out, model x15 premium, long context, 3:25", sized.Format(TimeSpan.FromSeconds(205), stopped: false));
        Assert.Null(plain.ContextSize);
        Assert.Equal("8k in, 1.2k out, model x15 premium, 0:05", plain.Format(TimeSpan.FromSeconds(5), stopped: false));
    }

    /// <summary>
    ///     Test that a figure that was not reported is left out, that nothing reported means no caption, that a model that
    ///     costs no premium requests says so, and that a stopped turn always says that the request may still count.
    /// </summary>
    [Fact]
    public void TurnUsage_Format_LeavesOutWhatWasNotReportedAndNotesAStop()
    {
        // Arrange
        var none = new TurnUsage();
        var free = new TurnUsage();
        free.Add(new AgentEvent.Usage(null, 500, null, 0));

        // Act and assert
        Assert.Equal(string.Empty, none.Format(TimeSpan.FromSeconds(5), stopped: false));
        Assert.Equal("0:05, " + TurnUsage.StoppedNote, none.Format(TimeSpan.FromSeconds(5), stopped: true));
        Assert.Equal("500 out, no premium requests, 0:05", free.Format(TimeSpan.FromSeconds(5), stopped: false));
    }

    /// <summary>
    ///     Test that a completed turn puts the caption on its last reply, after the tool rows, and not on an earlier one.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Completed_PutsTheUsageCaptionOnTheLastReply()
    {
        // Arrange
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw"));

        // Act
        _agent.Emit(
            new AgentEvent.TextDelta("First."),
            new AgentEvent.Usage(1000, 100, null, 3, "m"),
            new AgentEvent.ToolStarted("c1", "project_write", "path=a.svg"),
            new AgentEvent.ToolFinished("c1", "project_write", true, "ok", false),
            new AgentEvent.TextDelta("Done."),
            new AgentEvent.Usage(2000, 200, null, 3, "m"));
        _clock.Advance(TimeSpan.FromSeconds(65));
        _agent.Emit(new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        var replies = _transcript.OfType<AssistantReplyViewModel>().ToList();
        Assert.Equal(2, replies.Count);
        Assert.Equal(string.Empty, replies[0].UsageCaption);
        Assert.Equal("3k in, 300 out, model x3 premium, 1:05", replies[1].UsageCaption);
        Assert.True(replies[1].HasUsageCaption);
    }

    /// <summary>
    ///     Test that a turn that the user stopped says so in the caption, and that a stopped turn without a reply puts the
    ///     caption into an information notice.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_Stopped_SaysThatTheRequestMayStillCount()
    {
        // Arrange
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Draw"));
        _agent.Emit(new AgentEvent.Usage(900, null, null, 15, "m"));

        // Act
        _runner.Stop();
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ChatTurnOutcome.Stopped, outcome);
        var notices = _transcript.OfType<NoticeRowViewModel>().ToList();
        Assert.Contains(notices, notice => notice.Message == "900 in, model x15 premium, 0:00, " + TurnUsage.StoppedNote);
    }

    /// <summary>
    ///     Test that a completed turn without any reported usage has no caption and adds no row.
    /// </summary>
    [Fact]
    public async Task ChatTurnRunner_CompletedWithoutUsage_HasNoCaption()
    {
        // Arrange
        var run = _runner.RunTurnAsync(ChatTestMessages.SentText("Hi"));

        // Act
        _agent.Emit(new AgentEvent.TextDelta("Hello."), new AgentEvent.Completed());
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        var reply = Assert.Single(_transcript.OfType<AssistantReplyViewModel>());
        Assert.False(reply.HasUsageCaption);
        Assert.Empty(_transcript.OfType<NoticeRowViewModel>());
    }
}

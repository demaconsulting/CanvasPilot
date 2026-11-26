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
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the part of <see cref="ChatPanelViewModel" /> that joins the AI copilot: a sent message starts a
///     turn, the rows of the answer follow the entry of the message, the composer waits while the copilot works, the
///     turn can be stopped, a failure has a notice that opens the settings, and the banner says whether the copilot can be
///     used. The composer, the registry, and the annotation layer are the real units over a temporary project; the
///     copilot is a controlled fake that answers when the test says so and the clock is manual.
/// </summary>
public sealed class ChatPanelCopilotTests : IDisposable
{
    /// <summary>
    ///     How long a test waits for a turn that should end.
    /// </summary>
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Temporary folder that holds the projects.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-chat-copilot-tests-").FullName;

    /// <summary>
    ///     The copilot, which answers when the test says so.
    /// </summary>
    private readonly ControlledAgentService _agent = new();

    /// <summary>
    ///     The composer of the message.
    /// </summary>
    private readonly ComposerViewModel _composer;

    /// <summary>
    ///     The annotation layer.
    /// </summary>
    private readonly AnnotationOverlayViewModel _annotation;

    /// <summary>
    ///     The chat panel under test.
    /// </summary>
    private readonly ChatPanelViewModel _panel;

    /// <summary>
    ///     The clock of the panel and its turns, which moves when a test says so.
    /// </summary>
    private readonly ManualTimeProvider _clock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatPanelCopilotTests" /> class with a chat panel that has a copilot,
    ///     in an open project.
    /// </summary>
    public ChatPanelCopilotTests()
    {
        var marks = new MarkRegistry();
        _composer = new ComposerViewModel(new PayloadTextBuilder(), marks, new FakeDictationService());
        _annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
        _panel = new ChatPanelViewModel(
            _composer,
            marks,
            _annotation,
            Substitute.For<IMarkImageComposer>(),
            agent: _agent,
            clock: _clock);
        _composer.SetProject(Directory.CreateDirectory(Path.Combine(_root, "project")).FullName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _panel.Dispose();
        _annotation.Dispose();
        _composer.Dispose();
        _agent.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the busy row says that the copilot is stopping from the moment of the Stop until the turn has ended, and
    ///     that Stop cannot be pressed again meanwhile.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Stop_ShowsStoppingAndDisablesStopUntilTheTurnEnds()
    {
        // Arrange: a turn on a copilot that is slow to stop
        var token = TestContext.Current.CancellationToken;
        _agent.IgnoreCancellation = true;
        var announced = new List<string?>();
        _panel.PropertyChanged += (_, e) => announced.Add(e.PropertyName);
        _composer.InsertTyped("Draw");
        await _panel.SendCommand.ExecuteAsync(null);
        var before = (_panel.IsStopping, _panel.StopCommand.CanExecute(null));

        // Act: Stop
        _panel.StopCommand.Execute(null);
        var during = (_panel.IsCopilotBusy, _panel.IsStopping, _panel.BusyText, _panel.StopCommand.CanExecute(null));
        _agent.EndStream();
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert
        Assert.Equal((false, true), before);
        Assert.Equal((true, true, ChatTurnRunner.StoppingText, false), during);
        Assert.Contains(nameof(ChatPanelViewModel.IsStopping), announced);
        Assert.Equal((false, false, false), (_panel.IsCopilotBusy, _panel.IsStopping, _panel.StopCommand.CanExecute(null)));
    }

    /// <summary>
    ///     Test that the busy row says what the copilot is doing, how long the turn has lasted, and, after a long silence,
    ///     the hint about a long wait, that the panel announces each change, and that all of it is empty when the turn
    ///     has ended.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TurnRuns_BusyRowShowsPhaseElapsedTimeAndHint()
    {
        // Arrange: the changes that the panel announces
        var token = TestContext.Current.CancellationToken;
        var announced = new List<string?>();
        _panel.PropertyChanged += (_, e) => announced.Add(e.PropertyName);
        _composer.InsertTyped("Draw");

        // Act: send, let the time pass, and let the copilot report that it thinks
        await _panel.SendCommand.ExecuteAsync(null);
        var atStart = (_panel.BusyText, _panel.BusyElapsedText, _panel.HasBusyHint);
        _clock.Advance(ChatTurnRunner.HintDelay);
        var silent = (_panel.BusyElapsedText, _panel.BusyHintText, _panel.HasBusyHint);
        _agent.Emit(new AgentEvent.Thinking());
        var thinking = (_panel.BusyText, _panel.HasBusyHint);
        _agent.Emit(new AgentEvent.Completed());
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert
        Assert.Equal((ChatPanelViewModel.WorkingText, "0:00", false), atStart);
        Assert.Equal(("0:30", ChatTurnRunner.LongWaitHint, true), silent);
        Assert.Equal((ChatTurnRunner.ThinkingText, false), thinking);
        Assert.Contains(nameof(ChatPanelViewModel.BusyText), announced);
        Assert.Contains(nameof(ChatPanelViewModel.BusyElapsedText), announced);
        Assert.Contains(nameof(ChatPanelViewModel.BusyHintText), announced);
        Assert.Contains(nameof(ChatPanelViewModel.HasBusyHint), announced);
        Assert.Equal((string.Empty, string.Empty, string.Empty, false), (_panel.BusyText, _panel.BusyElapsedText, _panel.BusyHintText, _panel.HasBusyHint));
    }

    /// <summary>
    ///     Test that a sent message starts a turn with the payload of the message, that the reply streams, that the text is
    ///     plain until the turn completes and Markdown afterwards, and that the rows follow the entry of the message.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Send_WithCopilot_StartsTurnStreamsReplyAndMakesMarkdownWhenComplete()
    {
        // Arrange: a message
        var token = TestContext.Current.CancellationToken;
        _composer.InsertTyped("Draw a box");

        // Act: send it and let the copilot answer in pieces
        await _panel.SendCommand.ExecuteAsync(null);
        _agent.Emit(new AgentEvent.TextDelta("Here is **the box**"));
        var reply = await WaitForReplyAsync(token);
        var duringStream = (reply.Text, reply.State, reply.Blocks);
        _agent.Emit(new AgentEvent.Completed());
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert: the copilot got the payload, the rows are the entry and the reply, and the reply is final Markdown now
        var request = Assert.Single(_agent.Requests);
        Assert.Same(Assert.Single(_panel.SentEntries).Payload, request.Payload);
        Assert.Equal([TranscriptRowKind.User, TranscriptRowKind.Assistant], _panel.Transcript.Select(row => row.Kind));
        Assert.Equal(("Here is **the box**", AssistantReplyState.Streaming, null), (duringStream.Text, duringStream.State, duringStream.Blocks));
        Assert.True(reply.IsComplete);
        var blocks = Assert.IsAssignableFrom<IReadOnlyList<MarkdownBlock>>(reply.Blocks);
        Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
    }

    /// <summary>
    ///     Test that the composer cannot send and the panel says the copilot is busy while a turn runs, that the message can
    ///     still be written, and that everything is back when the turn has ended.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TurnRuns_ComposerCannotSendAndBusyIsAnnouncedUntilTheTurnEnds()
    {
        // Arrange: the changes that the panel announces
        var token = TestContext.Current.CancellationToken;
        var announced = new List<string?>();
        _panel.PropertyChanged += (_, e) => announced.Add(e.PropertyName);
        _composer.InsertTyped("one");

        // Act: send, and write the next message meanwhile
        await _panel.SendCommand.ExecuteAsync(null);
        _composer.InsertTyped("two");
        var during = (_panel.IsCopilotBusy, _panel.CanSend, _composer.SendBlockedReason, _panel.StopCommand.CanExecute(null));
        _agent.Emit(new AgentEvent.Completed());
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert: blocked with the reason and stoppable during the turn, free and not stoppable afterwards
        Assert.Equal((true, false, ComposerViewModel.CopilotWorkingText, true), during);
        Assert.Contains(nameof(ChatPanelViewModel.IsCopilotBusy), announced);
        Assert.False(_panel.IsCopilotBusy);
        Assert.True(_panel.CanSend);
        Assert.False(_panel.StopCommand.CanExecute(null));
        Assert.Equal("two", _composer.Document.FlatText);
    }

    /// <summary>
    ///     Test that stopping keeps the text that was written and marks the reply stopped, ends the turn, and lets the
    ///     composer send again.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Stop_KeepsPartialTextMarksItStoppedAndFreesTheComposer()
    {
        // Arrange: a turn that has written some text
        var token = TestContext.Current.CancellationToken;
        _composer.InsertTyped("Draw a box");
        await _panel.SendCommand.ExecuteAsync(null);
        _agent.Emit(new AgentEvent.TextDelta("Let me think"));
        var reply = await WaitForReplyAsync(token);

        // Act: stop
        _panel.StopCommand.Execute(null);
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert: the text is kept in a stopped reply, the turn was canceled, and sending is possible again
        Assert.Equal(("Let me think", AssistantReplyState.Stopped, "Stopped"), (reply.Text, reply.State, reply.StatusLabel));
        Assert.Null(reply.Blocks);
        Assert.True(_agent.LatestToken.IsCancellationRequested);
        Assert.False(_panel.IsCopilotBusy);
        _composer.InsertTyped("next");
        Assert.True(_panel.CanSend);
    }

    /// <summary>
    ///     Test that a tool that the copilot calls is a row after the reply, that says what is under way and then what was
    ///     done.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ToolCalled_AddsToolRowInOrderWithItsResult()
    {
        // Arrange / Act: send, and let the copilot talk, write a file, and finish
        var token = TestContext.Current.CancellationToken;
        _composer.InsertTyped("Draw a box");
        await _panel.SendCommand.ExecuteAsync(null);
        _agent.Emit(
            new AgentEvent.TextDelta("Drawing."),
            new AgentEvent.ToolStarted("c1", "project_write", "path=box.svg, content=(80 characters)"),
            new AgentEvent.FileWritten("box.svg", true),
            new AgentEvent.ToolFinished("c1", "project_write", true, "File 'box.svg' replaced.", false),
            new AgentEvent.Completed());
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert: the rows are the entry, the reply, and the tool line that says the file was replaced
        Assert.Equal([TranscriptRowKind.User, TranscriptRowKind.Assistant, TranscriptRowKind.Tool], _panel.Transcript.Select(row => row.Kind));
        var tool = Assert.IsType<ToolActivityRowViewModel>(_panel.Transcript[2]);
        Assert.Equal(("Replaced `box.svg`", false, true), (tool.Summary, tool.IsRunning, tool.Succeeded));
    }

    /// <summary>
    ///     Test that a failure of the copilot is an error notice with the remedy and the offer of the settings, and that
    ///     choosing it raises the request that the shell handles.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TurnFails_ShowsNoticeWithRemedyAndOpenSettingsRaisesTheRequest()
    {
        // Arrange: a request for the settings is counted
        var token = TestContext.Current.CancellationToken;
        var requests = 0;
        _panel.OpenSettingsRequested += (_, _) => requests++;
        _composer.InsertTyped("Draw a box");

        // Act: the provider fails
        await _panel.SendCommand.ExecuteAsync(null);
        _agent.Emit(new AgentEvent.Failed(AgentFailureKind.ModelNotFound, "The model 'm' was not found."));
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert: the notice says what happened, what to do, and offers the settings; the composer is free
        var notice = Assert.IsType<NoticeRowViewModel>(_panel.Transcript[^1]);
        Assert.Equal(("The model 'm' was not found.", true, true, true), (notice.Message, notice.HasRemedy, notice.CanOpenSettings, notice.IsError));
        Assert.False(_composer.IsSendBlocked);

        // Act: choose "Open Settings" on the notice and on the panel
        notice.OpenSettingsCommand.Execute(null);
        _panel.OpenSettingsCommand.Execute(null);

        // Assert: the shell was asked twice
        Assert.Equal(2, requests);
    }

    /// <summary>
    ///     Test that a project change while a turn runs stops the turn, drops its rows even when they arrive later, and
    ///     frees the composer for the new project.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ProjectChangesWhileTurnRuns_StopsItAndDropsWhatItStillSays()
    {
        // Arrange: a turn that has written some text
        var token = TestContext.Current.CancellationToken;
        _composer.InsertTyped("Draw a box");
        await _panel.SendCommand.ExecuteAsync(null);
        _agent.Emit(new AgentEvent.TextDelta("Thinking"));
        await WaitForReplyAsync(token);

        // Act: open another project
        _composer.SetProject(Directory.CreateDirectory(Path.Combine(_root, "other")).FullName);
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert: nothing of the old turn is shown, the turn was canceled, and the new project can send
        Assert.Empty(_panel.Transcript);
        Assert.True(_agent.LatestToken.IsCancellationRequested);
        Assert.False(_panel.IsCopilotBusy);
        Assert.False(_composer.IsSendBlocked);
        _composer.InsertTyped("again");
        Assert.True(_panel.CanSend);
    }

    /// <summary>
    ///     Test that disposing the panel stops a turn that runs and adds nothing afterwards.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Dispose_WhileTurnRuns_StopsTheTurn()
    {
        // Arrange: a turn that runs
        var token = TestContext.Current.CancellationToken;
        _composer.InsertTyped("Draw a box");
        await _panel.SendCommand.ExecuteAsync(null);
        var turn = _panel.WaitForTurnAsync();

        // Act: dispose the panel
        _panel.Dispose();
        await turn.WaitAsync(TurnTimeout, token);

        // Assert: the turn was canceled and the transcript stays empty
        Assert.True(_agent.LatestToken.IsCancellationRequested);
        Assert.Empty(_panel.Transcript);
    }

    /// <summary>
    ///     Test that a panel without a copilot behaves as it did before: a sent message is only added to the transcript,
    ///     nothing is blocked, and stopping and the readiness check do nothing.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_NoCopilot_OnlyAppendsTheEntryAndBlocksNothing()
    {
        // Arrange: a panel with no copilot
        using var composer = new ComposerViewModel(new FakeMessageBuilder(), new MarkRegistry(), new FakeDictationService());
        using var annotation = new AnnotationOverlayViewModel(new MarkRegistry(), new FakeRenderedImageSource(), _ => { });
        using var panel = new ChatPanelViewModel(composer, new MarkRegistry(), annotation, Substitute.For<IMarkImageComposer>());
        composer.SetProject(Directory.CreateDirectory(Path.Combine(_root, "plain")).FullName);
        composer.InsertTyped("hello");

        // Act: send, stop, and check the readiness
        await panel.SendCommand.ExecuteAsync(null);
        panel.Stop();
        await panel.RefreshReadinessAsync();
        await panel.WaitForTurnAsync();

        // Assert: one entry, no busy state, no banner, nothing blocked
        Assert.Equal([TranscriptRowKind.User], panel.Transcript.Select(row => row.Kind));
        Assert.False(panel.IsCopilotBusy);
        Assert.False(panel.HasReadinessIssue);
        Assert.False(composer.IsSendBlocked);
        Assert.False(panel.StopCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that the banner shows the sentence of a provider that cannot be used, including the sentence of GitHub
    ///     Copilot that is not signed in, shows a warning that does not block, and is hidden when everything is ready.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_RefreshReadiness_ShowsTheActionableSentenceAndHidesItWhenReady()
    {
        // Arrange: the changes that the panel announces
        var announced = new List<string?>();
        _panel.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        // Act / Assert: ready, the banner is hidden
        await _panel.RefreshReadinessAsync();
        Assert.False(_panel.HasReadinessIssue);
        Assert.Equal(string.Empty, _panel.ReadinessMessage);

        // Act / Assert: the Copilot that is not signed in says what to do
        _agent.Readiness = new AgentReadinessReport(
            AgentReadinessStatus.CopilotNotSignedIn,
            CopilotRuntimeStarter.NotSignedInMessage);
        await _panel.RefreshReadinessAsync();
        Assert.True(_panel.HasReadinessIssue);
        Assert.Equal(CopilotRuntimeStarter.NotSignedInMessage, _panel.ReadinessMessage);
        Assert.Contains(nameof(ChatPanelViewModel.HasReadinessIssue), announced);

        // Act / Assert: a warning is shown, and sending is not blocked by it
        _agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.OllamaContextTooSmall, "The context window (8192 tokens) is below 16,384.");
        await _panel.RefreshReadinessAsync();
        Assert.True(_panel.HasReadinessIssue);
        _composer.InsertTyped("hello");
        Assert.True(_panel.CanSend);

        // Act / Assert: settings changed and the check is repeated by the command of the banner; the banner goes away
        _agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.Ready, "Ready.");
        var calls = _agent.ReadinessCalls;
        await _panel.CheckAgainCommand.ExecuteAsync(null);
        Assert.Equal(calls + 1, _agent.ReadinessCalls);
        Assert.False(_panel.HasReadinessIssue);
        Assert.Equal(string.Empty, _panel.ReadinessMessage);
    }

    /// <summary>
    ///     Test that a readiness check that throws is shown in the banner and never thrown.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_RefreshReadiness_CheckThrows_ShowsTheProblemInTheBanner()
    {
        // Arrange: a check that fails
        _agent.ReadinessFailure = new InvalidOperationException("the probe broke");

        // Act
        await _panel.RefreshReadinessAsync();

        // Assert: the banner says the copilot could not be checked
        Assert.True(_panel.HasReadinessIssue);
        Assert.Contains("could not be checked", _panel.ReadinessMessage, StringComparison.Ordinal);
        Assert.Contains("the probe broke", _panel.ReadinessMessage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Waits until the transcript has a reply of the copilot.
    /// </summary>
    /// <param name="token">Cancels the wait.</param>
    /// <returns>The reply.</returns>
    private async Task<AssistantReplyViewModel> WaitForReplyAsync(CancellationToken token)
    {
        var deadline = DateTime.UtcNow + TurnTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_panel.Transcript.OfType<AssistantReplyViewModel>().FirstOrDefault() is { } reply)
            {
                return reply;
            }

            await Task.Delay(10, token);
        }

        throw new TimeoutException("The copilot did not write a reply.");
    }

    /// <summary>
    ///     A builder of sent messages whose payload is a real payload with the text of the message, as the copilot needs.
    /// </summary>
    private sealed class PayloadTextBuilder : IMessageBuilder
    {
        /// <inheritdoc />
        public Task<SentMessage> BuildAsync(
            ComposerSnapshot message,
            IReadOnlyList<Mark> marks,
            IReadOnlyList<StagedAttachment> attachments,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SentMessage(message, marks, attachments, ChatTestPayloads.OfText(message.FlatText), FakeMessageBuilder.Now));
    }
}

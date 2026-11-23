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

using System.Net.Http;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of what a Stop does to a turn of <see cref="AgentService" /> and to its conversation, on a scripted provider
///     that blocks in the middle of a tool call, before it says anything, or without ever looking at the token: the turn
///     ends without a failure, the conversation stays usable or is dropped for a new session, the cancel source is logged,
///     and a conversation that a Stop left unusable is replaced once.
/// </summary>
public sealed class AgentServiceStopTests
{
    /// <summary>
    ///     The words of the earlier conversation that the digest carries.
    /// </summary>
    private const string EarlierWords = "EARLIER-WORDS-7c21";

    /// <summary>
    ///     Makes a request with the text of an earlier conversation.
    /// </summary>
    /// <param name="text">The text of the message.</param>
    /// <returns>The request.</returns>
    private static AgentTurnRequest Request(string text)
    {
        return new AgentTurnRequest(AgentSubsystemTests.CreatePayload(text))
        {
            RestoredHistory = [new AgentHistoryTurn(AgentHistoryRole.User, EarlierWords)],
        };
    }

    /// <summary>
    ///     Counts how many of the messages of a request to the model hold the words of the earlier conversation.
    /// </summary>
    /// <param name="messages">The messages.</param>
    /// <returns>The count.</returns>
    private static int CountEarlierWords(IEnumerable<ChatMessage> messages)
    {
        return messages.Count(message => message.Text.Contains(EarlierWords, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Joins the text of the events of a turn.
    /// </summary>
    /// <param name="events">The events.</param>
    /// <returns>The text.</returns>
    private static string TextOf(IEnumerable<AgentEvent> events)
    {
        return string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text));
    }

    /// <summary>
    ///     Test that a Stop in the middle of a tool call (after the tool ran, while the model writes what comes next) ends
    ///     the turn with the events so far, no failure, and no completion, logs who canceled it, and that the next turn is
    ///     answered on the same conversation, without a new agent.
    /// </summary>
    [Fact]
    public async Task AgentService_Stop_MidToolCall_EndsTheTurnAndTheNextTurnAnswersOnTheSameConversation()
    {
        // Arrange: a model that calls a tool, and then, with the result of the tool, waits
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((number, ct) => number switch
        {
            1 => ProgrammableChatClient.CallTool("c1", "project_list"),
            2 => ProgrammableChatClient.Hang(entered, never.Task, "late", ct),
            _ => ProgrammableChatClient.Say("after"),
        });
        using var rig = new AgentServiceRig(client);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var turn = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("One"), stop.Token), token);
        await entered.Task.WaitAsync(token);

        // Act: Stop, then ask again
        await stop.CancelAsync();
        var stopped = await turn.WaitAsync(TimeSpan.FromSeconds(30), token);
        var next = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("Two"), token), token);

        // Assert: the tool call is there, nothing ends the turn, the next turn answers on the same conversation
        Assert.Contains(stopped, item => item is AgentEvent.ToolStarted { Name: "project_list" });
        Assert.Contains(stopped, item => item is AgentEvent.ToolFinished { Name: "project_list" });
        Assert.DoesNotContain(stopped, item => item is AgentEvent.Failed or AgentEvent.Completed);
        Assert.Equal("after", TextOf(next));
        Assert.IsType<AgentEvent.Completed>(next[^1]);
        Assert.Single(rig.Factory.Requests);
        Assert.Empty(rig.Factory.DeletedSessions);
        Assert.Contains("cancelSource=User", rig.ReadLog(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a Stop that arrives before the provider said anything drops the conversation, so that the next turn
    ///     starts on a new provider session that gets the digest of the history once, and not twice.
    /// </summary>
    [Fact]
    public async Task AgentService_Stop_BeforeTheFirstUpdate_DropsTheSessionAndTheNextTurnGetsTheDigestOnce()
    {
        // Arrange: a model that says nothing until it is released
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((number, ct) => number == 1
            ? ProgrammableChatClient.HangBeforeFirstUpdate(entered, never.Task, "late", ct)
            : ProgrammableChatClient.Say("after"));
        using var rig = new AgentServiceRig(client);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var turn = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("One"), stop.Token), token);
        await entered.Task.WaitAsync(token);

        // Act: Stop, wait for the session to go, and ask again
        await stop.CancelAsync();
        var stopped = await turn.WaitAsync(TimeSpan.FromSeconds(30), token);
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);
        var next = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("Two"), token), token);

        // Assert: the stopped turn said nothing, the next turn is on a new agent with the digest in one message only
        Assert.Empty(stopped);
        Assert.Equal(2, rig.Factory.Requests.Count);
        Assert.Equal("after", TextOf(next));
        Assert.Equal(1, CountEarlierWords(client.Requests[1]));
        Assert.Contains("reason=stopped-early", rig.ReadLog(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a provider that never looks at the token cannot hold the caller: after the grace the turn ends without
    ///     a failure, the conversation is dropped, and the next turn runs on a new session while the old stream is still
    ///     blocked.
    /// </summary>
    [Fact]
    public async Task AgentService_Stop_ProviderIgnoresTheToken_EndsAfterTheGraceAndDropsTheConversation()
    {
        // Arrange: a provider that says something and then waits for ever, ignoring the token
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((number, _) => number == 1
            ? ProgrammableChatClient.HangIgnoringCancellation(entered, release.Task)
            : ProgrammableChatClient.Say("after"));
        using var rig = new AgentServiceRig(client, clock: clock);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var turn = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("One"), stop.Token), token);
        await entered.Task.WaitAsync(token);

        // Act: Stop; the turn is still held before the grace has passed, and ends after it
        await stop.CancelAsync();
        var heldBeforeTheGrace = !turn.IsCompleted;
        clock.Advance(AgentService.StopGrace);
        var stopped = await turn.WaitAsync(TimeSpan.FromSeconds(30), token);
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);
        var next = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("Two"), token), token);
        release.SetResult();

        // Assert
        Assert.True(heldBeforeTheGrace);
        Assert.Equal("working ", TextOf(stopped));
        Assert.DoesNotContain(stopped, item => item is AgentEvent.Failed or AgentEvent.Completed);
        Assert.Equal(2, rig.Factory.Requests.Count);
        Assert.Equal("after", TextOf(next));
        Assert.Contains("did not stop", rig.ReadLog(), StringComparison.Ordinal);
        Assert.Contains("reason=stop-timeout", rig.ReadLog(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a conversation that a Stop left unusable is replaced once: the next turn fails before it says anything
    ///     with a lost connection, so a new conversation gets the message with the digest, the failure is not shown, and the
    ///     recovery is logged.
    /// </summary>
    [Fact]
    public async Task AgentService_NextTurnAfterStop_FailsBeforeAnyUpdate_IsRecoveredOnceOnANewConversation()
    {
        // Arrange: a stopped turn, then a session that cannot be used, then a model that works
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((number, ct) => number switch
        {
            1 => ProgrammableChatClient.Hang(entered, never.Task, "late", ct),
            2 => ProgrammableChatClient.Fail(new HttpRequestException("The connection was closed.")),
            _ => ProgrammableChatClient.Say("recovered"),
        });
        using var rig = new AgentServiceRig(client);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var first = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("One"), stop.Token), token);
        await entered.Task.WaitAsync(token);
        await stop.CancelAsync();
        _ = await first.WaitAsync(TimeSpan.FromSeconds(30), token);

        // Act: the next turn
        var next = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("Two"), token), token);
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);

        // Assert: no failure was shown, the answer came from a new conversation, and the old session was deleted
        Assert.DoesNotContain(next, item => item is AgentEvent.Failed);
        Assert.Equal("recovered", TextOf(next));
        Assert.IsType<AgentEvent.Completed>(next[^1]);
        Assert.Equal(2, rig.Factory.Requests.Count);
        Assert.Equal(3, client.Requests.Count);
        Assert.Equal(1, CountEarlierWords(client.Requests[2]));
        Assert.Contains("Session recovered: turn=2 reason=ConnectionLost", rig.ReadLog(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the recovery is tried once and not for a failure of another turn: when the new conversation fails too,
    ///     the failure is shown and nothing is sent a third time, and a failure of a conversation that nobody stopped is
    ///     shown at once.
    /// </summary>
    [Fact]
    public async Task AgentService_Recovery_IsOnlyOnceAndOnlyAfterAStop()
    {
        // Arrange: a stopped turn, and then a provider that fails every time
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stoppedClient = new ProgrammableChatClient((number, ct) => number == 1
            ? ProgrammableChatClient.Hang(entered, never.Task, "late", ct)
            : ProgrammableChatClient.Fail(new HttpRequestException("The connection was closed.")));
        using var stoppedRig = new AgentServiceRig(stoppedClient);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var first = AgentServiceRig.CollectAsync(stoppedRig.Service.RunTurnAsync(Request("One"), stop.Token), token);
        await entered.Task.WaitAsync(token);
        await stop.CancelAsync();
        _ = await first.WaitAsync(TimeSpan.FromSeconds(30), token);

        // Act: the next turn on the stopped conversation, and a first turn on a conversation that nobody stopped
        var afterStop = await AgentServiceRig.CollectAsync(stoppedRig.Service.RunTurnAsync(Request("Two"), token), token);
        var untouchedClient = new ProgrammableChatClient((_, _) => ProgrammableChatClient.Fail(new HttpRequestException("The connection was closed.")));
        using var untouchedRig = new AgentServiceRig(untouchedClient);
        var untouched = await AgentServiceRig.CollectAsync(untouchedRig.Service.RunTurnAsync(Request("Three"), token), token);

        // Assert: one failure each, the stopped conversation was tried twice after the stop and no more, the other once
        Assert.Equal(AgentFailureKind.ConnectionLost, Assert.IsType<AgentEvent.Failed>(Assert.Single(afterStop)).Kind);
        Assert.Equal(3, stoppedClient.Requests.Count);
        Assert.Equal(AgentFailureKind.ConnectionLost, Assert.IsType<AgentEvent.Failed>(Assert.Single(untouched)).Kind);
        Assert.Single(untouchedClient.Requests);
    }

    /// <summary>
    ///     Test that the source of a cancellation that did not come from the caller is logged: a reset and a change of the
    ///     project end the running turn with their own source, and a turn that the caller stops is the user's.
    /// </summary>
    [Theory]
    [InlineData("Reset")]
    [InlineData("Project")]
    public async Task AgentService_Cancel_ByAResetOrAProjectChange_LogsItsSource(string source)
    {
        // Arrange: a turn that waits
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((_, ct) => ProgrammableChatClient.Hang(entered, never.Task, "late", ct));
        using var rig = new AgentServiceRig(client);
        var turn = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(Request("One"), token), token);
        await entered.Task.WaitAsync(token);

        // Act
        if (source == "Reset")
        {
            rig.Service.ResetConversation();
        }
        else
        {
            rig.SwitchProject(null);
        }

        var events = await turn.WaitAsync(TimeSpan.FromSeconds(30), token);

        // Assert
        Assert.DoesNotContain(events, item => item is AgentEvent.Failed or AgentEvent.Completed);
        Assert.Contains($"cancelSource={source}", rig.ReadLog(), StringComparison.Ordinal);
    }
}

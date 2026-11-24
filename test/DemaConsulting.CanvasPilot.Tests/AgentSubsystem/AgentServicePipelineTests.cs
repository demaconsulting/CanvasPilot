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
using DemaConsulting.CanvasPilot.AgentSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of the event pipeline of <see cref="AgentService" />: the provider is read on its own thread and its events
///     are merged for a consumer that lags, and a consumer that goes away ends the provider's stream before the next turn.
/// </summary>
public sealed class AgentServicePipelineTests
{
    /// <summary>
    ///     The number of pieces of text that the burst of the model has.
    /// </summary>
    private const int BurstPieces = 2000;

    /// <summary>
    ///     Makes the answer of a model that sends many pieces of text at once and tells when it has sent them all.
    /// </summary>
    /// <param name="sentAll">Completed when the last piece was taken by the reader.</param>
    /// <returns>The updates.</returns>
    private static async IAsyncEnumerable<ChatResponseUpdate> Burst(TaskCompletionSource sentAll)
    {
        for (var i = 0; i < BurstPieces; i++)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, $"p{i} ");
        }

        await Task.Yield();
        sentAll.TrySetResult();
    }

    /// <summary>
    ///     Test that a consumer that lags does not hold the provider back: the provider's whole burst is read while the
    ///     consumer has taken one event, and the consumer then gets the same text in far fewer, ordered events and the end
    ///     of the turn.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_SlowConsumer_TheProviderIsReadToItsEndAndTheTextIsMerged()
    {
        // Arrange: a model that sends a burst and a consumer that takes only the first event
        var token = TestContext.Current.CancellationToken;
        var sentAll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var rig = new AgentServiceRig(new ProgrammableChatClient((_, _) => Burst(sentAll)));
        await using var enumerator = rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Go")), token).GetAsyncEnumerator(token);
        Assert.True(await enumerator.MoveNextAsync());
        var events = new List<AgentEvent> { enumerator.Current };

        // Act: the provider finishes while the consumer waits, then the consumer reads the rest
        await sentAll.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        while (await enumerator.MoveNextAsync())
        {
            events.Add(enumerator.Current);
        }

        // Assert: all the text in order, in far fewer events than pieces, and the turn completed
        var expected = string.Concat(Enumerable.Range(0, BurstPieces).Select(i => $"p{i} "));
        var texts = events.OfType<AgentEvent.TextDelta>().ToList();
        Assert.Equal(expected, string.Concat(texts.Select(text => text.Text)));
        Assert.True(texts.Count < BurstPieces / 2, $"{texts.Count} text events for {BurstPieces} pieces");
        Assert.IsType<AgentEvent.Completed>(events[^1]);
        Assert.Contains("merged=", rig.ReadLog(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a consumer that stops reading and disposes its enumerator ends the provider's stream, and that the next
    ///     turn is not refused as busy: it begins only after the provider's stream was disposed.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_ConsumerDisposed_EndsTheProviderStreamBeforeTheNextTurn()
    {
        // Arrange: a model that never finishes by itself, and a consumer that reads its first event
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((number, ct) => number == 1
            ? ProgrammableChatClient.Hang(entered, never.Task, "late", ct)
            : ProgrammableChatClient.Say("after"));
        using var rig = new AgentServiceRig(client);
        var enumerator = rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token).GetAsyncEnumerator(token);
        Assert.True(await enumerator.MoveNextAsync());
        await entered.Task.WaitAsync(token);

        // Act: the consumer goes away, and the next turn starts at once
        await enumerator.DisposeAsync();
        var next = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Two")), token), token);

        // Assert: the next turn was not busy and completed
        Assert.DoesNotContain(next, item => item is AgentEvent.Failed);
        Assert.IsType<AgentEvent.Completed>(next[^1]);
        Assert.Equal(2, client.Requests.Count);
    }
}

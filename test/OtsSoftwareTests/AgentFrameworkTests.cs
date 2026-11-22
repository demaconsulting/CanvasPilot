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

using DemaConsulting.AgentKit.Agents.ChatClient;
using DemaConsulting.AgentKit.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the Microsoft Agent Framework off-the-shelf software item (Microsoft.Agents.AI and
///     Microsoft.Agents.AI.Abstractions): the agent, session, and streaming types that CanvasPilot's chat runs a turn
///     with. They use a scripted chat client and no language model.
/// </summary>
public class AgentFrameworkTests
{
    /// <summary>
    ///     Test that an agent streams the text of an answer as updates, in the order the model sent them, and that
    ///     the updates join to the whole answer.
    /// </summary>
    [Fact]
    public async Task AgentFramework_ChatClientAgent_RunStreaming_YieldsTextUpdatesInOrder()
    {
        // Arrange: an agent over a client that answers in three pieces
        var client = new ScriptedChatClient(
            [
                new ChatResponseUpdate(ChatRole.Assistant, "A "),
                new ChatResponseUpdate(ChatRole.Assistant, "streamed "),
                new ChatResponseUpdate(ChatRole.Assistant, "answer.")
            ]);
        var agent = CreateAgent(client);
        var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

        // Act: run one streamed turn and collect the text of each update
        var pieces = new List<string>();
        await foreach (var update in agent.RunStreamingAsync("Hello.", session, cancellationToken: TestContext.Current.CancellationToken))
        {
            pieces.AddRange(update.Contents.OfType<TextContent>().Select(content => content.Text));
        }

        // Assert: the pieces arrived one by one and in order
        Assert.Equal(["A ", "streamed ", "answer."], pieces);
    }

    /// <summary>
    ///     Test that two turns on one session send the earlier conversation to the model with the second turn, and
    ///     that a new session starts without it.
    /// </summary>
    [Fact]
    public async Task AgentFramework_AgentSession_SecondTurn_SendsEarlierConversation()
    {
        // Arrange: an agent over a client that answers three turns, and one session
        var client = new ScriptedChatClient(
            [new ChatResponseUpdate(ChatRole.Assistant, "First answer.")],
            [new ChatResponseUpdate(ChatRole.Assistant, "Second answer.")],
            [new ChatResponseUpdate(ChatRole.Assistant, "Third answer.")]);
        var agent = CreateAgent(client);
        var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

        // Act: run two turns on the session and one turn on a new session
        await Drain(agent.RunStreamingAsync("First question.", session, cancellationToken: TestContext.Current.CancellationToken));
        await Drain(agent.RunStreamingAsync("Second question.", session, cancellationToken: TestContext.Current.CancellationToken));
        var fresh = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
        await Drain(agent.RunStreamingAsync("Another question.", fresh, cancellationToken: TestContext.Current.CancellationToken));

        // Assert: the second request holds the first question and answer, and the third request holds neither
        var second = Texts(client.Requests[1]);
        Assert.Contains("First question.", second);
        Assert.Contains("First answer.", second);
        Assert.Contains("Second question.", second);
        var third = Texts(client.Requests[2]);
        Assert.DoesNotContain("First question.", third);
        Assert.Contains("Another question.", third);
    }

    /// <summary>
    ///     Test that canceling the token of a streamed turn stops it with an operation-cancelled exception, which is
    ///     what the Stop command of the chat relies on.
    /// </summary>
    [Fact]
    public async Task AgentFramework_ChatClientAgent_RunStreaming_CancelledMidStream_ThrowsOperationCanceled()
    {
        // Arrange: an agent over a client with a long answer, and a token that is cancelled after the first piece
        var client = new ScriptedChatClient(
            [.. Enumerable.Range(0, 20).Select(index => new ChatResponseUpdate(ChatRole.Assistant, $"piece {index} "))]);
        var agent = CreateAgent(client);
        var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var received = 0;

        // Act: read the stream and cancel after the first update
        var stopped = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in agent.RunStreamingAsync("Go on.", session, cancellationToken: cancel.Token))
            {
                received++;
                await cancel.CancelAsync();
            }
        });

        // Assert: the stream stopped early
        Assert.NotNull(stopped);
        Assert.InRange(received, 1, 19);
    }

    /// <summary>
    ///     Creates an agent over a chat client with one tool, as CanvasPilot does.
    /// </summary>
    /// <param name="client">The scripted chat client.</param>
    /// <returns>The agent.</returns>
    private static AIAgent CreateAgent(ScriptedChatClient client)
    {
        var tool = GuardedToolFactory.Create(() => ToolResult.Text("pong"), "demo_ping", "Answers a ping.");
        return ChatClientAgentFactory.Create(client, [tool], "You are a test.", "ots-agent");
    }

    /// <summary>
    ///     Reads an update stream to its end.
    /// </summary>
    /// <param name="updates">The stream.</param>
    /// <returns>A task that completes when the stream ends.</returns>
    private static async Task Drain(IAsyncEnumerable<AgentResponseUpdate> updates)
    {
        await foreach (var _ in updates)
        {
            // Only the requests that the model got are of interest.
        }
    }

    /// <summary>
    ///     Gets the text of every message of a request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The texts.</returns>
    private static string[] Texts(List<ChatMessage> request)
    {
        return [.. request.SelectMany(message => message.Contents).OfType<TextContent>().Select(content => content.Text)];
    }
}

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

using System.Net;
using System.Text;
using DemaConsulting.AgentKit.Agents.ChatClient;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     An agent factory that makes the agent of every request on one scripted chat client, which is how the tests of the
///     service and the chat put an agent in place of a real provider.
/// </summary>
internal sealed class ScriptedAgentFactory : IAgentFactory
{
    /// <summary>The chat client that every agent talks to.</summary>
    private readonly IChatClient _client;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptedAgentFactory" /> class.
    /// </summary>
    /// <param name="client">The scripted chat client.</param>
    internal ScriptedAgentFactory(IChatClient client)
    {
        _client = client;
    }

    /// <summary>
    ///     Gets the requests that were made, in order.
    /// </summary>
    internal List<AgentCreationRequest> Requests { get; } = [];

    /// <summary>
    ///     Gets the sessions that were deleted, in order.
    /// </summary>
    internal List<AgentSession> DeletedSessions { get; } = [];

    /// <summary>
    ///     Gets the number of times that the factory was disposed.
    /// </summary>
    internal int DisposeCount { get; private set; }

    /// <summary>
    ///     Gets or sets the failure that every request gets instead of an agent, or <see langword="null" /> for agents.
    /// </summary>
    internal AgentReadinessReport? Failure { get; set; }

    /// <inheritdoc />
    public Task<AgentCreationResult> CreateAsync(AgentCreationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Failure is not null)
        {
            return Task.FromResult(AgentCreationResult.Unavailable(Failure));
        }

        var agent = ChatClientAgentFactory.Create(_client, [.. request.Tools], request.Instructions, AgentProviderFactory.AgentName);
        return Task.FromResult(AgentCreationResult.Created(agent));
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        DeletedSessions.Add(session);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     A started Copilot runtime that makes agents on a scripted chat client, records what it was asked, and records
///     that it was disposed, which stands in for the runtime of the AI library in the tests of the Copilot branch.
/// </summary>
internal sealed class FakeCopilotAgentSource : ICopilotAgentSource
{
    /// <summary>The chat client that the agents talk to.</summary>
    private readonly IChatClient _client;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeCopilotAgentSource" /> class.
    /// </summary>
    /// <param name="client">The scripted chat client.</param>
    internal FakeCopilotAgentSource(IChatClient client)
    {
        _client = client;
    }

    /// <summary>
    ///     Gets the requests for agents, in order.
    /// </summary>
    internal List<CopilotAgentRequest> Requests { get; } = [];

    /// <summary>
    ///     Gets the sessions that were deleted, in order.
    /// </summary>
    internal List<string> DeletedSessions { get; } = [];

    /// <summary>
    ///     Gets the number of times that the source was disposed.
    /// </summary>
    internal int DisposeCount { get; private set; }

    /// <inheritdoc />
    public AIAgent CreateAgent(CopilotAgentRequest request)
    {
        Requests.Add(request);
        return ChatClientAgentFactory.Create(_client, [.. request.Tools], request.Instructions, request.Name);
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        DeletedSessions.Add(sessionId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     An HTTP handler that stands in for an Ollama server: it answers the chat requests from a queue of bodies, and records
///     the address and the body of every request.
/// </summary>
internal sealed class QueuedOllamaHandler : HttpMessageHandler
{
    /// <summary>The bodies to answer with, one per request.</summary>
    private readonly Queue<string> _bodies;

    /// <summary>
    ///     Initializes a new instance of the <see cref="QueuedOllamaHandler" /> class.
    /// </summary>
    /// <param name="bodies">The newline-delimited JSON of the first answer, the second answer, and so on.</param>
    internal QueuedOllamaHandler(params string[] bodies)
    {
        _bodies = new Queue<string>(bodies);
    }

    /// <summary>
    ///     Gets the address and the body of each request, in order.
    /// </summary>
    internal List<(Uri Address, string Body)> Requests { get; } = [];

    /// <summary>
    ///     Gets or sets the status code of every answer.
    /// </summary>
    internal HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!, body));
        var answer = _bodies.TryDequeue(out var next) ? next : "{}";
        return new HttpResponseMessage(Status)
        {
            Content = new StringContent(answer, Encoding.UTF8, "application/x-ndjson"),
        };
    }
}

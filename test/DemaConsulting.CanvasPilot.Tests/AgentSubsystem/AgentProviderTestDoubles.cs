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
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A catalog of models that the test fills, which stands in for the catalog of a provider: it lists and finds the models
///     it was given, counts the refreshes, and can fail the way a provider that cannot be asked does.
/// </summary>
internal sealed class FakeModelCatalog : IModelCatalog
{
    /// <summary>The models that the catalog holds.</summary>
    private readonly List<ModelInfo> _models;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeModelCatalog" /> class.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="models">The models that the catalog holds.</param>
    internal FakeModelCatalog(string providerName, params ModelInfo[] models)
    {
        ProviderName = providerName;
        _models = [.. models];
    }

    /// <inheritdoc />
    public string ProviderName { get; }

    /// <summary>
    ///     Gets or sets the failure that every call throws, or <see langword="null" /> for none.
    /// </summary>
    internal Exception? Failure { get; set; }

    /// <summary>
    ///     Gets the number of times that the catalog was refreshed.
    /// </summary>
    internal int Refreshes { get; private set; }

    /// <summary>
    ///     Gets the number of times that the catalog was listed.
    /// </summary>
    internal int Lists { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<ModelInfo>> ListAsync(ModelQuery? query = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Lists++;
        if (Failure is not null)
        {
            return Task.FromException<IReadOnlyList<ModelInfo>>(Failure);
        }

        var filter = query ?? new ModelQuery();
        return Task.FromResult<IReadOnlyList<ModelInfo>>([.. _models.Where(filter.Matches)]);
    }

    /// <inheritdoc />
    public Task<ModelInfo?> GetAsync(string modelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            return Task.FromException<ModelInfo?>(Failure);
        }

        // A name without a tag stands for the latest tag, as it does for Ollama
        var tagged = modelId.Contains(':', StringComparison.Ordinal) ? modelId : modelId + ":latest";
        return Task.FromResult(_models.FirstOrDefault(model =>
            string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(model.Id, tagged, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Refreshes++;
        return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}

/// <summary>
///     Makes the models that the tests put in a catalog.
/// </summary>
internal static class TestModels
{
    /// <summary>
    ///     Makes a model of a provider.
    /// </summary>
    /// <param name="provider">The id of the provider, <c>copilot</c> or <c>ollama</c>.</param>
    /// <param name="id">The id of the model.</param>
    /// <param name="toolCalls">Whether the model can call tools.</param>
    /// <param name="vision">Whether the model accepts pictures.</param>
    /// <param name="efforts">The ids of the reasoning efforts that the model offers; none when empty.</param>
    /// <param name="windowTokens">The largest context window of the model, or <see langword="null" /> when not known.</param>
    /// <param name="usable">Whether the model can be used.</param>
    /// <param name="displayName">The name to show; the id when <see langword="null" />.</param>
    /// <returns>The model.</returns>
    internal static ModelInfo Make(
        string provider,
        string id,
        bool toolCalls = true,
        bool vision = true,
        string[]? efforts = null,
        int? windowTokens = 256_000,
        bool usable = true,
        string? displayName = null)
    {
        var offered = efforts ?? [];
        var capabilities = ModelCapabilities.None;
        if (toolCalls)
        {
            capabilities |= ModelCapabilities.ToolCalls;
        }

        if (vision)
        {
            capabilities |= ModelCapabilities.Vision;
        }

        if (offered.Length > 0)
        {
            capabilities |= ModelCapabilities.Reasoning;
        }

        var adjustments = ModelAdjustments.ContextWindow;
        if (offered.Length > 0)
        {
            adjustments |= ModelAdjustments.ReasoningEffort;
        }

        return new ModelInfo
        {
            Provider = provider,
            Id = id,
            DisplayName = displayName ?? id,
            Capabilities = capabilities,
            Adjustments = adjustments,
            IsUsable = usable,
            UnusableReason = usable ? null : "The model is disabled by your organization.",
            ContextWindowTokens = windowTokens,
            ReasoningEfforts = [.. offered.Select(effort => new ModelReasoningEffort(effort, char.ToUpperInvariant(effort[0]) + effort[1..], null))],
        };
    }
}

/// <summary>
///     Gives one catalog for every address of an Ollama server and records the addresses that were asked for.
/// </summary>
/// <param name="catalog">The catalog that every address gets.</param>
internal sealed class FakeOllamaCatalogs(IModelCatalog catalog) : IOllamaModelCatalogs
{
    /// <summary>Gets the addresses that were asked for, in order.</summary>
    internal List<Uri> Endpoints { get; } = [];

    /// <inheritdoc />
    public IModelCatalog For(Uri endpoint)
    {
        Endpoints.Add(endpoint);
        return catalog;
    }
}

/// <summary>
///     An agent factory that makes the agent of every request on one scripted chat client, which is how the tests of the
///     service and the chat put an agent in place of a real provider.
/// </summary>
internal sealed class ScriptedAgentFactory : IAgentFactory
{
    /// <summary>The chat client that every agent talks to.</summary>
    private readonly IChatClient _client;

    /// <summary>The lock that guards the records, because requests may arrive from several threads.</summary>
    private readonly Lock _lock = new();

    /// <summary>The requests that were made.</summary>
    private readonly List<AgentCreationRequest> _requests = [];

    /// <summary>The sessions that were deleted.</summary>
    private readonly List<AgentSession> _deletedSessions = [];

    /// <summary>The number of times that the factory was disposed.</summary>
    private int _disposeCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptedAgentFactory" /> class.
    /// </summary>
    /// <param name="client">The scripted chat client.</param>
    internal ScriptedAgentFactory(IChatClient client)
    {
        _client = client;
    }

    /// <summary>
    ///     Gets a snapshot of the requests that were made, in order.
    /// </summary>
    internal List<AgentCreationRequest> Requests
    {
        get
        {
            lock (_lock)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>
    ///     Gets a snapshot of the sessions that were deleted, in order.
    /// </summary>
    internal List<AgentSession> DeletedSessions
    {
        get
        {
            lock (_lock)
            {
                return [.. _deletedSessions];
            }
        }
    }

    /// <summary>
    ///     Gets the number of times that the factory was disposed.
    /// </summary>
    internal int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>
    ///     Gets or sets the failure that every request gets instead of an agent, or <see langword="null" /> for agents.
    /// </summary>
    internal AgentReadinessReport? Failure { get; set; }

    /// <summary>
    ///     Gets or sets how the factory describes the session of a conversation, or <see langword="null" /> for a provider
    ///     that has no session of its own.
    /// </summary>
    internal Func<AgentSession, AgentSessionInfo>? Describe { get; set; }

    /// <inheritdoc />
    public AgentSessionInfo DescribeSession(AgentSession session)
    {
        return Describe?.Invoke(session) ?? AgentSessionInfo.None;
    }

    /// <inheritdoc />
    public Task<AgentCreationResult> CreateAsync(AgentCreationRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _requests.Add(request);
        }

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
        lock (_lock)
        {
            _deletedSessions.Add(session);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
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

    /// <summary>The lock that guards the records, because requests may arrive from several threads.</summary>
    private readonly Lock _lock = new();

    /// <summary>The requests for agents.</summary>
    private readonly List<CopilotAgentRequest> _requests = [];

    /// <summary>The sessions that were deleted.</summary>
    private readonly List<string> _deletedSessions = [];

    /// <summary>The number of times that the source was disposed.</summary>
    private int _disposeCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeCopilotAgentSource" /> class.
    /// </summary>
    /// <param name="client">The scripted chat client.</param>
    /// <param name="models">The catalog of the models that the runtime offers; a catalog without models when <see langword="null" />.</param>
    internal FakeCopilotAgentSource(IChatClient client, FakeModelCatalog? models = null)
    {
        _client = client;
        Catalog = models ?? new FakeModelCatalog("copilot");
    }

    /// <summary>
    ///     Gets the catalog of the models that the runtime offers.
    /// </summary>
    internal FakeModelCatalog Catalog { get; }

    /// <inheritdoc />
    public IModelCatalog Models => Catalog;

    /// <summary>
    ///     Gets or sets how the runtime signed in, which is unknown (<see langword="null" />) until a test sets it.
    /// </summary>
    public CopilotSignInInfo? SignIn { get; set; }

    /// <summary>
    ///     Gets a snapshot of the requests for agents, in order.
    /// </summary>
    internal List<CopilotAgentRequest> Requests
    {
        get
        {
            lock (_lock)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>
    ///     Gets a snapshot of the sessions that were deleted, in order.
    /// </summary>
    internal List<string> DeletedSessions
    {
        get
        {
            lock (_lock)
            {
                return [.. _deletedSessions];
            }
        }
    }

    /// <summary>
    ///     Gets the number of times that the source was disposed.
    /// </summary>
    internal int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <inheritdoc />
    public AIAgent CreateAgent(CopilotAgentRequest request)
    {
        lock (_lock)
        {
            _requests.Add(request);
        }

        return ChatClientAgentFactory.Create(_client, [.. request.Tools], request.Instructions, request.Name);
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _deletedSessions.Add(sessionId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
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

    /// <summary>
    ///     Gets or sets the answer to <c>/api/tags</c>, the list of the models; it never takes a body from the queue.
    /// </summary>
    internal string Tags { get; set; } = """{"models":[]}""";

    /// <summary>
    ///     Gets or sets the answer to <c>/api/show</c>, the description of one model; it never takes a body from the queue.
    /// </summary>
    internal string Show { get; set; } = "{}";

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!, body));

        // The questions about models are answered from their own bodies, so that they do not use up the chat answers
        var path = request.RequestUri!.AbsolutePath;
        var answer = path switch
        {
            "/api/tags" => Tags,
            "/api/show" => Show,
            _ => _bodies.TryDequeue(out var next) ? next : "{}",
        };
        return new HttpResponseMessage(Status)
        {
            Content = new StringContent(answer, Encoding.UTF8, path.StartsWith("/api/chat", StringComparison.Ordinal) ? "application/x-ndjson" : "application/json"),
        };
    }
}

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

using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     What the factory needs to make the agent of one project.
/// </summary>
/// <param name="Settings">The provider choices: the provider, the model, the Ollama address, and the context window.</param>
/// <param name="Tools">The tools that the agent may call and nothing else; confined to the project by the registry.</param>
/// <param name="Instructions">The instructions of the agent.</param>
/// <param name="ProjectFolder">The full path of the project folder, which GitHub Copilot gets as its working directory.</param>
public sealed record AgentCreationRequest(
    AiProviderSettings Settings,
    IReadOnlyList<AIFunction> Tools,
    string Instructions,
    string ProjectFolder)
{
    /// <summary>
    ///     Gets the full path of the folder where the Copilot runtime keeps its own data, which the factory passes to the
    ///     starter of the runtime when it starts the runtime; <see langword="null" /> for none. It is chosen by the agent
    ///     service and is never inside a project.
    /// </summary>
    public string? CopilotDataDirectory { get; init; }

    /// <summary>
    ///     Gets the logger that the Copilot runtime writes to, which records metadata only; <see langword="null" /> for none.
    /// </summary>
    public ILogger? CopilotLogger { get; init; }
}

/// <summary>
///     The answer of the factory: an agent together with what must be disposed when the agent is no longer used, or the
///     reason that no agent could be made.
/// </summary>
public sealed class AgentCreationResult : IAsyncDisposable
{
    /// <summary>
    ///     What the agent owns and disposes with it, or <see langword="null" /> for nothing.
    /// </summary>
    private readonly IAsyncDisposable? _owned;

    /// <summary>
    ///     Whether the owned resources were disposed already.
    /// </summary>
    private int _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentCreationResult" /> class.
    /// </summary>
    /// <param name="agent">The agent, or <see langword="null" /> when none could be made.</param>
    /// <param name="failure">Why no agent could be made; <see langword="null" /> when there is an agent.</param>
    /// <param name="owned">What the agent owns and disposes with it.</param>
    private AgentCreationResult(AIAgent? agent, AgentReadinessReport? failure, IAsyncDisposable? owned)
    {
        Agent = agent;
        Failure = failure;
        _owned = owned;
    }

    /// <summary>
    ///     Gets the agent, or <see langword="null" /> when none could be made.
    /// </summary>
    public AIAgent? Agent { get; }

    /// <summary>
    ///     Gets why no agent could be made, in the terms of the readiness check, or <see langword="null" /> when there is an
    ///     agent.
    /// </summary>
    public AgentReadinessReport? Failure { get; }

    /// <summary>
    ///     Gets a value indicating whether there is an agent.
    /// </summary>
    public bool IsCreated => Agent is not null;

    /// <summary>
    ///     Makes the result of an agent that was made.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="owned">What the agent owns and the result disposes; <see langword="null" /> for nothing.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the agent is <see langword="null" />.</exception>
    public static AgentCreationResult Created(AIAgent agent, IAsyncDisposable? owned = null)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return new AgentCreationResult(agent, null, owned);
    }

    /// <summary>
    ///     Makes the result of an agent that could not be made.
    /// </summary>
    /// <param name="failure">Why; the status and the sentence that the readiness check uses for the same cause.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the failure is <see langword="null" />.</exception>
    public static AgentCreationResult Unavailable(AgentReadinessReport failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new AgentCreationResult(null, failure, null);
    }

    /// <summary>
    ///     Disposes what the agent owns. Calling it again does nothing.
    /// </summary>
    /// <returns>A task that completes when everything is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _owned is not null)
        {
            await _owned.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
///     Makes the agent that a conversation talks to, from the settings, the tools, and the instructions. The interface is
///     the seam where tests put an agent on a scripted chat client in place of a real provider.
/// </summary>
/// <remarks>
///     The factory owns what it started and shares between agents (the Copilot runtime) and disposes it when it is
///     disposed; each result owns what belongs to its agent alone (the connection to Ollama).
/// </remarks>
public interface IAgentFactory : IAsyncDisposable
{
    /// <summary>
    ///     Makes an agent.
    /// </summary>
    /// <param name="request">The settings, tools, instructions, and project folder.</param>
    /// <param name="cancellationToken">A token that cancels the start of a runtime.</param>
    /// <returns>The agent, or the reason that none could be made; a provider that cannot be used is a result, not an exception.</returns>
    Task<AgentCreationResult> CreateAsync(AgentCreationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes what the provider kept for a conversation (for GitHub Copilot, the session that its runtime persists in
    ///     its data folder) so that conversations do not accumulate there. A conversation on a provider that keeps nothing,
    ///     and a session of a runtime that was never started, are left alone.
    /// </summary>
    /// <param name="session">The session of the conversation that ends.</param>
    /// <param name="cancellationToken">A token that cancels the deletion.</param>
    /// <returns>A task that completes when the session is deleted.</returns>
    Task DeleteSessionAsync(AgentSession session, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Tells what the provider calls the session of a conversation and where it keeps what it recorded of it, for the log
    ///     and for the notice of an error. A provider that has no session of its own, and a session that has not run yet,
    ///     give <see cref="AgentSessionInfo.None" />.
    /// </summary>
    /// <param name="session">The session of the conversation.</param>
    /// <returns>The identifier of the session at the provider and the file where the provider records its events.</returns>
    AgentSessionInfo DescribeSession(AgentSession session) => AgentSessionInfo.None;
}

/// <summary>
///     What the provider calls the session of a conversation, which ties the lines of the log to what the provider recorded.
/// </summary>
/// <param name="Id">The identifier of the session at the provider, or <see langword="null" /> when it has none (yet).</param>
/// <param name="EventsFile">
///     The full path of the file where the provider records the events of the session, or <see langword="null" /> when it
///     records none; the file exists until the session is deleted, which is when the conversation is dropped.
/// </param>
public sealed record AgentSessionInfo(string? Id, string? EventsFile)
{
    /// <summary>
    ///     Gets the description of a session that has no identity at the provider.
    /// </summary>
    public static AgentSessionInfo None { get; } = new(null, null);
}

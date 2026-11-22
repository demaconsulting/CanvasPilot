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

using DemaConsulting.AgentKit.Agents.Copilot;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     What the factory asks of a started Copilot runtime to make one agent.
/// </summary>
/// <param name="Tools">The tools that the agent may call and nothing else.</param>
/// <param name="Instructions">The instructions of the agent.</param>
/// <param name="Name">The name of the agent.</param>
/// <param name="Model">The Copilot model; <see langword="null" /> for the default model of the runtime.</param>
/// <param name="WorkingDirectory">The full path of the project folder; it grants nothing, the tools decide what may be touched.</param>
public sealed record CopilotAgentRequest(
    IReadOnlyList<AIFunction> Tools,
    string Instructions,
    string Name,
    string? Model,
    string WorkingDirectory);

/// <summary>
///     A started Copilot runtime, as the factory uses it: it makes agents that run on the runtime, deletes the sessions
///     that the runtime keeps, and stops the runtime when disposed. The interface exists because the runtime of the AI
///     library is a sealed class that only starts when a Copilot CLI can be run, so tests stand in for it.
/// </summary>
public interface ICopilotAgentSource : IAsyncDisposable
{
    /// <summary>
    ///     Makes an agent that runs on the runtime with exactly the given tools. The permission handler is the default of
    ///     the AI library, which approves the given tools by name and refuses everything else.
    /// </summary>
    /// <param name="request">The tools, instructions, name, model, and working directory.</param>
    /// <returns>The agent. The runtime is not owned by the agent; the source disposes it.</returns>
    AIAgent CreateAgent(CopilotAgentRequest request);

    /// <summary>
    ///     Deletes a conversation that the runtime kept, with everything it persisted.
    /// </summary>
    /// <param name="sessionId">The id of the session.</param>
    /// <param name="cancellationToken">A token that cancels the deletion.</param>
    /// <returns>A task that completes when the session is deleted.</returns>
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
///     The <see cref="ICopilotAgentSource" /> on a real <see cref="CopilotRuntime" /> of the AI library. The class is the
///     one place that calls <see cref="CopilotAgentFactory" />; a starter of the runtime that works returns one of these.
/// </summary>
/// <remarks>
///     The runtime is started by the starter and disposed here, because the source is what the factory keeps and disposes.
///     No permission handler is passed, so the library installs its default-deny handler. The class holds no state besides
///     the runtime and is safe for concurrent use as far as the runtime is.
/// </remarks>
public sealed class CopilotRuntimeAgentSource : ICopilotAgentSource
{
    /// <summary>
    ///     The runtime that the agents run on.
    /// </summary>
    private readonly CopilotRuntime _runtime;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CopilotRuntimeAgentSource" /> class.
    /// </summary>
    /// <param name="runtime">The started runtime; the source disposes it.</param>
    /// <exception cref="ArgumentNullException">Thrown when the runtime is <see langword="null" />.</exception>
    public CopilotRuntimeAgentSource(CopilotRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        _runtime = runtime;
    }

    /// <inheritdoc />
    public AIAgent CreateAgent(CopilotAgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // onPermissionRequest stays null: the library then approves exactly the supplied tools and refuses everything else
        return CopilotAgentFactory.Create(
            _runtime,
            [.. request.Tools],
            request.Instructions,
            onPermissionRequest: null,
            name: request.Name,
            model: request.Model,
            workingDirectory: request.WorkingDirectory);
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        return _runtime.DeleteSessionAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _runtime.DisposeAsync();
    }
}

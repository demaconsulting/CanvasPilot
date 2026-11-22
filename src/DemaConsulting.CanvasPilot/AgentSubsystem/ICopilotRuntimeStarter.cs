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

using Microsoft.Extensions.Logging;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     What came of asking for the GitHub Copilot runtime.
/// </summary>
public enum CopilotStartStatus
{
    /// <summary>The runtime can be started, or was started.</summary>
    Started,

    /// <summary>
    ///     The Copilot CLI is older than the oldest version that works with the AI library, or speaks another protocol than
    ///     the library does.
    /// </summary>
    CliTooOld,

    /// <summary>
    ///     The runtime started but has no credentials that it can use: the Copilot CLI's own <c>/login</c> session is not
    ///     used by the AI library.
    /// </summary>
    NotSignedIn,

    /// <summary>The runtime could not be started for a reason that the result names.</summary>
    Failed,
}

/// <summary>
///     What the application asks of the starter of the Copilot runtime.
/// </summary>
/// <param name="CliPath">The full path of the Copilot CLI that the locator found.</param>
/// <param name="CliVersion">The version that the CLI reported.</param>
public sealed record CopilotStartRequest(string CliPath, Version CliVersion)
{
    /// <summary>
    ///     Gets the full path of the folder where the runtime keeps its own data (its home and its sessions), or
    ///     <see langword="null" /> for the folder that the starter was made with. The folder is always one below the local
    ///     data of the user and never one inside a project.
    /// </summary>
    public string? DataDirectory { get; init; }

    /// <summary>
    ///     Gets the logger that a started runtime writes to, which records metadata only, or <see langword="null" /> for none.
    /// </summary>
    public ILogger? Logger { get; init; }
}

/// <summary>
///     The answer of the starter of the Copilot runtime.
/// </summary>
/// <param name="Status">What came of the request.</param>
/// <param name="Message">One sentence for the user that says what happened and, when the runtime cannot be used, what to do.</param>
/// <param name="Agents">
///     The running runtime as a source of agents when <paramref name="Status" /> is <see cref="CopilotStartStatus.Started" />;
///     otherwise <see langword="null" />. The runtime is shared by everyone who asks and belongs to the starter, so whoever
///     receives the source must not dispose it.
/// </param>
public sealed record CopilotStartResult(CopilotStartStatus Status, string Message, ICopilotAgentSource? Agents = null)
{
    /// <summary>
    ///     Gets the status of the readiness check that stands for the same cause as <see cref="Status" />.
    /// </summary>
    public AgentReadinessStatus ReadinessStatus => Status switch
    {
        CopilotStartStatus.Started => AgentReadinessStatus.Ready,
        CopilotStartStatus.CliTooOld => AgentReadinessStatus.CopilotCliTooOld,
        CopilotStartStatus.NotSignedIn => AgentReadinessStatus.CopilotNotSignedIn,
        _ => AgentReadinessStatus.CopilotRuntimeUnavailable,
    };
}

/// <summary>
///     Starts, and shares, the GitHub Copilot runtime of the AI library on the Copilot CLI that the user installed. One
///     runtime serves the agents of the conversations and the readiness check, because the runtime keeps its data in one
///     folder that two runtimes must not share.
/// </summary>
public interface ICopilotRuntimeStarter
{
    /// <summary>
    ///     Gives the running runtime on a CLI, starting it when none runs on that CLI yet.
    /// </summary>
    /// <param name="request">Which CLI to use.</param>
    /// <param name="cancellationToken">A token that cancels the start.</param>
    /// <returns>The answer; a runtime that cannot be started is a result, not an exception.</returns>
    Task<CopilotStartResult> StartAsync(CopilotStartRequest request, CancellationToken cancellationToken = default);
}

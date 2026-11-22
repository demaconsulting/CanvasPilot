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
    ///     This build of the application cannot start the runtime because the AI library does not yet let the application
    ///     choose the Copilot CLI to run.
    /// </summary>
    NeedsAgentKitUpdate,

    /// <summary>The runtime could not be started for a reason that the result names.</summary>
    Failed,
}

/// <summary>
///     What the application asks of the starter of the Copilot runtime.
/// </summary>
/// <param name="CliPath">The full path of the Copilot CLI that the locator found.</param>
/// <param name="CliVersion">The version that the CLI reported.</param>
/// <param name="CheckOnly">
///     <see langword="true" /> to learn whether the runtime could be started without starting it, which is what a
///     readiness check asks.
/// </param>
public sealed record CopilotStartRequest(string CliPath, Version CliVersion, bool CheckOnly)
{
    /// <summary>
    ///     Gets the full path of the folder where the runtime keeps its own data (its home and its sessions), or
    ///     <see langword="null" /> for none. The agent service always names a folder below the local data of the user and
    ///     never one inside a project, and a starter must pass it on instead of using the default of the library.
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
///     The started runtime as a source of agents when <paramref name="Status" /> is <see cref="CopilotStartStatus.Started" />
///     and the request was not a check only; otherwise <see langword="null" />. Whoever receives it disposes it, which stops
///     the runtime.
/// </param>
public sealed record CopilotStartResult(CopilotStartStatus Status, string Message, ICopilotAgentSource? Agents = null);

/// <summary>
///     Starts the GitHub Copilot runtime of the AI library on the Copilot CLI that the user installed. The interface is
///     the one place where the application depends on a feature that the AI library does not have yet (a way to name the
///     CLI to run), so that the rest of the Copilot support is built and tested against it now.
/// </summary>
public interface ICopilotRuntimeStarter
{
    /// <summary>
    ///     Starts the runtime on a CLI, or says whether it could be started.
    /// </summary>
    /// <param name="request">Which CLI to use and whether to only check.</param>
    /// <param name="cancellationToken">A token that cancels the start.</param>
    /// <returns>The answer; a runtime that cannot be started is a result, not an exception.</returns>
    Task<CopilotStartResult> StartAsync(CopilotStartRequest request, CancellationToken cancellationToken = default);
}

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

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The starter of the Copilot runtime that this build ships: it always answers that an update of the AI library is
///     needed, and never starts anything.
/// </summary>
/// <remarks>
///     The AI library of this build can only run the Copilot CLI that it bundles, and the application must not bundle or
///     download the CLI (the user installs it), so the library cannot be pointed to the installed CLI yet. The starter
///     therefore does not touch the library at all. The day the library can be pointed to a CLI, this class is replaced
///     by one that does, and nothing else changes. The class holds no state and is safe for concurrent use.
/// </remarks>
public sealed class GatedCopilotRuntimeStarter : ICopilotRuntimeStarter
{
    /// <summary>
    ///     The sentence that the readiness check shows while the runtime cannot be started.
    /// </summary>
    public const string NeedsUpdateMessage = "Copilot support needs an AgentKit update; use Ollama meanwhile.";

    /// <inheritdoc />
    public Task<CopilotStartResult> StartAsync(CopilotStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new CopilotStartResult(CopilotStartStatus.NeedsAgentKitUpdate, NeedsUpdateMessage));
    }
}

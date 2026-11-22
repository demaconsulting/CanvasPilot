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
///     The AI copilot as the chat sees it: a readiness check, one turn at a time that answers a message with a stream
///     of events, and a reset of the conversation. Nothing in this interface names an AI provider.
/// </summary>
public interface IAgentService : IDisposable
{
    /// <summary>
    ///     Checks whether the AI provider that the current settings choose can be used.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>The report; a provider that cannot be used is a report, not an exception.</returns>
    Task<AgentReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Runs one turn: sends the message and streams what happens until the turn ends. The stream ends with
    ///     <see cref="AgentEvent.Completed" /> or <see cref="AgentEvent.Failed" />, or simply ends when the caller
    ///     cancels. Only one turn runs at a time; a request made while a turn runs gets a
    ///     <see cref="AgentEvent.Failed" /> of kind <see cref="AgentFailureKind.Busy" />.
    /// </summary>
    /// <param name="request">The message and the restored history, if any.</param>
    /// <param name="cancellationToken">A token that stops the turn.</param>
    /// <returns>The events of the turn, in order.</returns>
    IAsyncEnumerable<AgentEvent> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Forgets the conversation so that the next turn starts a new provider session.
    /// </summary>
    void ResetConversation();
}

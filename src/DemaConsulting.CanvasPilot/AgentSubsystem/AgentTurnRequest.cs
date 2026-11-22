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

using DemaConsulting.CanvasPilot.PayloadSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Who said a turn of an earlier conversation.
/// </summary>
public enum AgentHistoryRole
{
    /// <summary>The user wrote the turn.</summary>
    User,

    /// <summary>The AI copilot wrote the turn.</summary>
    Assistant,
}

/// <summary>
///     One text turn of an earlier conversation, restored for a new provider session. It holds the words only: no
///     pictures and no payload.
/// </summary>
/// <param name="Role">Who said the turn.</param>
/// <param name="Text">The words of the turn.</param>
public sealed record AgentHistoryTurn(AgentHistoryRole Role, string Text);

/// <summary>
///     One request to run a turn: the message that the user sent and, for a provider session that starts after the
///     conversation was restored, the text of the earlier turns.
/// </summary>
public sealed record AgentTurnRequest
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentTurnRequest" /> record.
    /// </summary>
    /// <param name="payload">The message that the user sent.</param>
    /// <exception cref="ArgumentNullException">Thrown when the payload is <see langword="null" />.</exception>
    public AgentTurnRequest(MessagePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        Payload = payload;
    }

    /// <summary>
    ///     Gets the message that the user sent.
    /// </summary>
    public MessagePayload Payload { get; }

    /// <summary>
    ///     Gets the text of the earlier turns, oldest first, that the first turn of a new provider session carries so
    ///     that the conversation can go on after the application was restarted. Empty when there is nothing to restore.
    /// </summary>
    public IReadOnlyList<AgentHistoryTurn> RestoredHistory { get; init; } = [];
}

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

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A chat client that answers each request with the next scripted list of updates and records what it was asked, so
///     that a whole tool-calling turn runs without a language model.
/// </summary>
internal sealed class ScriptedChatClient : IChatClient
{
    /// <summary>The scripted answers, one per request, in order.</summary>
    private readonly Queue<ChatResponseUpdate[]> _answers;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptedChatClient" /> class.
    /// </summary>
    /// <param name="answers">The updates to return for the first request, the second request, and so on.</param>
    internal ScriptedChatClient(params ChatResponseUpdate[][] answers)
    {
        _answers = new Queue<ChatResponseUpdate[]>(answers);
    }

    /// <summary>
    ///     Gets a copy of the conversation of each request, in the order the requests were made.
    /// </summary>
    internal List<List<ChatMessage>> Requests { get; } = [];

    /// <summary>
    ///     Gets a value indicating whether the client was disposed.
    /// </summary>
    internal bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
        }

        return updates.ToChatResponse();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
        if (!_answers.TryDequeue(out var updates))
        {
            throw new InvalidOperationException("The scripted chat client has no further answer.");
        }

        foreach (var update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return update;
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IsDisposed = true;
    }
}

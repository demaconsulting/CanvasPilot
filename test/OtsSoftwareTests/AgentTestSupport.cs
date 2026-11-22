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
using Microsoft.Extensions.AI;

namespace OtsSoftwareTests;

/// <summary>
///     A chat client that answers each request with the next scripted list of updates and records what it was asked,
///     so the tests of the AI libraries run a whole tool-calling turn without a language model.
/// </summary>
internal sealed class ScriptedChatClient : IChatClient
{
    /// <summary>
    ///     The scripted answers, one per request, in order.
    /// </summary>
    private readonly Queue<ChatResponseUpdate[]> _answers;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptedChatClient"/> class.
    /// </summary>
    /// <param name="answers">The updates to return for the first request, the second request, and so on.</param>
    public ScriptedChatClient(params ChatResponseUpdate[][] answers)
    {
        _answers = new Queue<ChatResponseUpdate[]>(answers);
    }

    /// <summary>
    ///     Gets a copy of the conversation of each request, in the order the requests were made.
    /// </summary>
    public List<List<ChatMessage>> Requests { get; } = [];

    /// <summary>
    ///     Gets the options of each request, in the order the requests were made.
    /// </summary>
    public List<ChatOptions?> Options { get; } = [];

    /// <summary>
    ///     Returns the next scripted answer as one response.
    /// </summary>
    /// <param name="messages">The conversation.</param>
    /// <param name="options">The request options.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The scripted answer.</returns>
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

    /// <summary>
    ///     Returns the next scripted answer as updates.
    /// </summary>
    /// <param name="messages">The conversation.</param>
    /// <param name="options">The request options.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The scripted updates.</returns>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
        Options.Add(options);

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

    /// <summary>
    ///     Returns no service.
    /// </summary>
    /// <param name="serviceType">The requested service type.</param>
    /// <param name="serviceKey">The requested service key.</param>
    /// <returns>Always <see langword="null"/>.</returns>
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <summary>
    ///     Releases nothing; the client holds no resources.
    /// </summary>
    public void Dispose()
    {
    }
}

/// <summary>
///     An HTTP handler that answers requests from a function and records each request with its body, so the tests of
///     the Ollama client run against a stand-in server without a network.
/// </summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
{
    /// <summary>
    ///     Gets the method, path, and body of each request, in the order the requests were made.
    /// </summary>
    public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = [];

    /// <summary>
    ///     Builds a response with a body and a status code.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="body">The body text, which is sent as newline-delimited JSON.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage Reply(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson")
        };
    }

    /// <summary>
    ///     Records the request and answers it from the function.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The response.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri?.AbsolutePath ?? string.Empty, body));
        return respond(request, body);
    }
}

/// <summary>
///     A folder that exists for the length of one test.
/// </summary>
internal sealed class TestFolder : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TestFolder"/> class and creates the folder.
    /// </summary>
    public TestFolder()
    {
        // The real path is used, so that a temp folder that is itself behind a link compares equal to what the
        // libraries report.
        var created = Directory.CreateTempSubdirectory("CanvasPilotOts-");
        Path = created.LinkTarget is null ? created.FullName : created.ResolveLinkTarget(true)!.FullName;
    }

    /// <summary>
    ///     Gets the full path of the folder.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Deletes the folder with everything in it.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A folder that cannot be removed is left to the operating system's temp cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}

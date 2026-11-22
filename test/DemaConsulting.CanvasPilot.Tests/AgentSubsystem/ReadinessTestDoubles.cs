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
using DemaConsulting.CanvasPilot.AgentSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A starter of the Copilot runtime that answers with a scripted result and records every request.
/// </summary>
internal sealed class FakeCopilotRuntimeStarter : ICopilotRuntimeStarter
{
    /// <summary>The answer to give.</summary>
    private readonly CopilotStartResult _result;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeCopilotRuntimeStarter" /> class.
    /// </summary>
    /// <param name="result">The answer to give to every request.</param>
    internal FakeCopilotRuntimeStarter(CopilotStartResult result)
    {
        _result = result;
    }

    /// <summary>
    ///     Gets the requests that were made, in order.
    /// </summary>
    internal List<CopilotStartRequest> Requests { get; } = [];

    /// <inheritdoc />
    public Task<CopilotStartResult> StartAsync(CopilotStartRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(_result);
    }
}

/// <summary>
///     An Ollama probe that answers with a scripted list of models and context length, and records every question.
/// </summary>
internal sealed class FakeOllamaProbe : IOllamaProbe
{
    /// <summary>
    ///     Gets or sets the answer to the question for the models.
    /// </summary>
    internal OllamaModelList Models { get; set; } = OllamaModelList.Unreachable;

    /// <summary>
    ///     Gets or sets the answer to the question for the context length.
    /// </summary>
    internal int? ContextLength { get; set; }

    /// <summary>
    ///     Gets the questions for the models: the address and the time limit.
    /// </summary>
    internal List<(Uri Endpoint, TimeSpan Timeout)> ListQuestions { get; } = [];

    /// <summary>
    ///     Gets the questions for the context length: the address, the model, and the time limit.
    /// </summary>
    internal List<(Uri Endpoint, string Model, TimeSpan Timeout)> ContextQuestions { get; } = [];

    /// <inheritdoc />
    public Task<OllamaModelList> ListModelsAsync(Uri endpoint, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ListQuestions.Add((endpoint, timeout));
        return Task.FromResult(Models);
    }

    /// <inheritdoc />
    public Task<int?> GetContextLengthAsync(Uri endpoint, string model, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ContextQuestions.Add((endpoint, model, timeout));
        return Task.FromResult(ContextLength);
    }
}

/// <summary>
///     An HTTP handler that answers requests from a function and records the method and path of each, which stands in for
///     an Ollama server in the tests of the real probe.
/// </summary>
internal sealed class StubOllamaHandler : HttpMessageHandler
{
    /// <summary>The function that makes the answer from the path of the request.</summary>
    private readonly Func<string, HttpResponseMessage> _respond;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StubOllamaHandler" /> class.
    /// </summary>
    /// <param name="respond">Makes the answer from the path of the request; it may throw to simulate a failure.</param>
    internal StubOllamaHandler(Func<string, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    /// <summary>
    ///     Gets the method and path of each request, in order.
    /// </summary>
    internal List<(HttpMethod Method, string Path)> Requests { get; } = [];

    /// <summary>
    ///     Makes an answer with a JSON body.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="body">The body.</param>
    /// <returns>The answer.</returns>
    internal static HttpResponseMessage Reply(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        Requests.Add((request.Method, path));
        return Task.FromResult(_respond(path));
    }
}

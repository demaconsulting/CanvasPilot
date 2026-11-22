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
using DemaConsulting.AgentKit.Agents.Ollama;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the OllamaSharp off-the-shelf software item: the client of the Ollama server that
///     CanvasPilot's readiness check and Ollama provider are built on. They run against a stand-in for the server, an
///     HTTP handler that answers from the test, so they need no Ollama installation and no model.
/// </summary>
public class OllamaSharpTests
{
    /// <summary>
    ///     The address that the stand-in server pretends to have.
    /// </summary>
    private static readonly Uri ServerAddress = new("http://ollama.test:11434");

    /// <summary>
    ///     Test that listing the local models asks the server for its tags and returns the names of the models that the
    ///     server answers with.
    /// </summary>
    [Fact]
    public async Task OllamaSharp_ListLocalModels_AsksForTagsAndReturnsModelNames()
    {
        // Arrange: a stand-in server that has two models
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Reply(
            HttpStatusCode.OK,
            """{"models":[{"name":"vision-model:7b","model":"vision-model:7b","size":1},{"name":"gemma3:latest","model":"gemma3:latest","size":2}]}"""));
        using var http = new HttpClient(handler) { BaseAddress = ServerAddress };
        var client = new OllamaApiClient(http, "gemma3:latest");

        // Act: list the models
        var models = (await client.ListLocalModelsAsync(TestContext.Current.CancellationToken)).Select(model => model.Name).ToArray();

        // Assert: the tags were asked for with a GET, and both names came back
        Assert.Equal((HttpMethod.Get, "/api/tags"), (handler.Requests[0].Method, handler.Requests[0].Path));
        Assert.Equal(["vision-model:7b", "gemma3:latest"], models);
    }

    /// <summary>
    ///     Test that a server that cannot be reached shows as an exception from the call, so that a readiness check can
    ///     tell the user that the server is not running.
    /// </summary>
    [Fact]
    public async Task OllamaSharp_ListLocalModels_ServerNotReachable_ThrowsHttpRequestException()
    {
        // Arrange: a stand-in that refuses the connection
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("Connection refused."));
        using var http = new HttpClient(handler) { BaseAddress = ServerAddress };
        var client = new OllamaApiClient(http, "gemma3:latest");

        // Act / Assert: listing the models fails with the exception of the connection
        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListLocalModelsAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a chat with a model that the server does not have fails with an HTTP error whose status code is
    ///     "not found", which is how the application tells a missing model from a server that is not running; the
    ///     message of the server is not part of the exception, so the application names the model itself.
    /// </summary>
    [Fact]
    public async Task OllamaSharp_ChatClient_UnknownModel_FailsWithNotFoundStatus()
    {
        // Arrange: a stand-in that answers every chat with the error of a missing model
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Reply(
            HttpStatusCode.NotFound,
            """{"error":"model \"nope:1b\" not found, try pulling it first"}"""));
        using var http = new HttpClient(handler) { BaseAddress = ServerAddress };
        IChatClient client = new OllamaApiClient(http, "nope:1b");

        // Act: ask for a streamed response, which is how the chat runs a turn
        var failure = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Hi.")],
                null,
                TestContext.Current.CancellationToken))
            {
                // The error comes before any update.
            }
        });

        // Assert: the status code says that the model was not found, and the request named the model
        Assert.Equal(HttpStatusCode.NotFound, failure.StatusCode);
        Assert.Contains("\"model\":\"nope:1b\"", handler.Requests.Single(item => item.Path == "/api/chat").Body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a streamed chat offers the tools to the server and returns the text pieces and the tool call that
    ///     the server streams, with the call's arguments by name.
    /// </summary>
    [Fact]
    public async Task OllamaSharp_ChatClient_StreamingResponse_ReturnsTextAndToolCall()
    {
        // Arrange: a stand-in that streams a piece of text, a tool call, and the end of the answer
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Reply(
            HttpStatusCode.OK,
            string.Join(
                "\n",
                """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"Let me "},"done":false}""",
                """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"demo_echo","arguments":{"text":"hi"}}}]},"done":false}""",
                """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","eval_count":5,"prompt_eval_count":7}""")));
        using var http = new HttpClient(handler) { BaseAddress = ServerAddress };
        IChatClient client = new OllamaApiClient(http, "m");
        var tool = AIFunctionFactory.Create((string text) => text, "demo_echo", "Echoes the text.");

        // Act: stream a response with the tool offered
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Echo hi.")],
            new ChatOptions { Tools = [tool] },
            TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        // Assert: the text piece and the call came back, and the request asked for a stream and named the tool
        Assert.Equal("Let me ", string.Concat(updates.SelectMany(update => update.Contents).OfType<TextContent>().Select(text => text.Text)));
        var call = updates.SelectMany(update => update.Contents).OfType<FunctionCallContent>().Single();
        Assert.Equal("demo_echo", call.Name);
        Assert.Equal("hi", call.Arguments?["text"]?.ToString());
        var request = handler.Requests.Single(item => item.Path == "/api/chat");
        Assert.Contains("\"stream\":true", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"demo_echo\"", request.Body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that AgentKit's context sizing client puts the context length on the wire as the Ollama option
    ///     <c>num_ctx</c> of every chat request, which is what keeps the server from loading the model with its
    ///     small default window.
    /// </summary>
    [Fact]
    public async Task OllamaSharp_ChatClient_WithContextSizing_SendsNumCtxOption()
    {
        // Arrange: a stand-in that answers with one piece of text, and a sizing client over the Ollama client
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Reply(
            HttpStatusCode.OK,
            """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"Hello."},"done":true,"done_reason":"stop"}"""));
        using var http = new HttpClient(handler) { BaseAddress = ServerAddress };
        using var client = new OllamaContextSizingChatClient(new OllamaApiClient(http, "m"), 32768);

        // Act: ask for a response
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi.")], null, TestContext.Current.CancellationToken);

        // Assert: the answer came back, and the request carried the context length in its options
        Assert.Equal("Hello.", response.Text);
        var request = handler.Requests.Single(item => item.Path == "/api/chat");
        Assert.Contains("\"num_ctx\":32768", request.Body, StringComparison.Ordinal);
    }
}

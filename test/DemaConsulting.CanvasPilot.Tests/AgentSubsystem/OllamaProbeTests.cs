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
using DemaConsulting.CanvasPilot.AgentSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="OllamaProbe" />, the real <see cref="IOllamaProbe" />, against a stand-in HTTP handler
///     that plays the Ollama server: the models from <c>/api/tags</c> and the context length from <c>/api/show</c>.
/// </summary>
public sealed class OllamaProbeTests
{
    /// <summary>The address that the stand-in server pretends to have.</summary>
    private static readonly Uri Server = new("http://ollama.test:11434");

    /// <summary>The time limit that the tests give, long enough not to matter.</summary>
    private static readonly TimeSpan Patient = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Test that the models of the server are listed from its tags, in the order that the server gives them.
    /// </summary>
    [Fact]
    public async Task OllamaProbe_ListModels_ServerAnswers_ReturnsTheNamesFromTags()
    {
        // Arrange: a stand-in server with two models
        var handler = new StubOllamaHandler(_ => StubOllamaHandler.Reply(
            HttpStatusCode.OK,
            """{"models":[{"name":"gemma3:latest","model":"gemma3:latest","size":1},{"name":"vision-model:7b","model":"vision-model:7b","size":2}]}"""));
        var probe = new OllamaProbe(handler);

        // Act
        var list = await probe.ListModelsAsync(Server, Patient, TestContext.Current.CancellationToken);

        // Assert: reachable, both names, and one GET of the tags
        Assert.True(list.Reachable);
        Assert.Equal(["gemma3:latest", "vision-model:7b"], list.Models);
        Assert.Equal((HttpMethod.Get, "/api/tags"), Assert.Single(handler.Requests));
    }

    /// <summary>
    ///     Test that a server without models is reachable with an empty list.
    /// </summary>
    [Fact]
    public async Task OllamaProbe_ListModels_NoModels_IsReachableAndEmpty()
    {
        // Arrange
        var probe = new OllamaProbe(new StubOllamaHandler(_ => StubOllamaHandler.Reply(HttpStatusCode.OK, """{"models":[]}""")));

        // Act
        var list = await probe.ListModelsAsync(Server, Patient, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(list.Reachable);
        Assert.Empty(list.Models);
    }

    /// <summary>
    ///     Test that a refused connection, an error status, and an answer that is not JSON are each an unreachable
    ///     server and not an exception.
    /// </summary>
    /// <param name="failure">How the stand-in server fails.</param>
    [Theory]
    [InlineData("refused")]
    [InlineData("server-error")]
    [InlineData("not-json")]
    public async Task OllamaProbe_ListModels_Failure_IsAnUnreachableServer(string failure)
    {
        // Arrange: a stand-in that fails the way the theory says
        var handler = new StubOllamaHandler(_ => failure switch
        {
            "refused" => throw new HttpRequestException("Connection refused."),
            "server-error" => StubOllamaHandler.Reply(HttpStatusCode.InternalServerError, """{"error":"boom"}"""),
            _ => StubOllamaHandler.Reply(HttpStatusCode.OK, "<html>not ollama</html>"),
        });
        var probe = new OllamaProbe(handler);

        // Act
        var list = await probe.ListModelsAsync(Server, Patient, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(list.Reachable);
        Assert.Empty(list.Models);
    }

    /// <summary>
    ///     Test that a server that does not answer within the time limit is an unreachable server, not an exception, and
    ///     that the wait ends at the limit.
    /// </summary>
    [Fact]
    public async Task OllamaProbe_ListModels_NoAnswerInTime_IsAnUnreachableServer()
    {
        // Arrange: a stand-in that never answers
        var probe = new OllamaProbe(new HangingHandler());

        // Act
        var list = await probe.ListModelsAsync(Server, TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(list.Reachable);
    }

    /// <summary>
    ///     Test that a cancellation by the caller is passed on and is not taken for an unreachable server, for both
    ///     questions.
    /// </summary>
    [Fact]
    public async Task OllamaProbe_Cancelled_ThrowsOperationCanceled()
    {
        // Arrange: a stand-in that never answers and a token that is cancelled
        var probe = new OllamaProbe(new HangingHandler());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.ListModelsAsync(Server, Patient, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.GetContextLengthAsync(Server, "m", Patient, cancelled.Token));
    }

    /// <summary>
    ///     Test that the context length is read from the entry of the model information whose key ends in
    ///     <c>.context_length</c>, whatever the family of the model is called, and that the request names the model.
    /// </summary>
    /// <param name="family">The family part of the key.</param>
    [Theory]
    [InlineData("llama")]
    [InlineData("gemma3")]
    [InlineData("custom")]
    public async Task OllamaProbe_GetContextLength_ModelInfo_ReadsTheContextLengthEntry(string family)
    {
        // Arrange: a stand-in whose answer has other entries and the context length of the family
        var handler = new StubOllamaHandler(_ => StubOllamaHandler.Reply(
            HttpStatusCode.OK,
            $$$"""{"details":{"family":"{{{family}}}"},"model_info":{"general.architecture":"{{{family}}}","{{{family}}}.block_count":32,"{{{family}}}.context_length":131072,"{{{family}}}.embedding_length":4096}}"""));
        var probe = new OllamaProbe(handler);

        // Act
        var length = await probe.GetContextLengthAsync(Server, "gemma3:latest", Patient, TestContext.Current.CancellationToken);

        // Assert: the length, and one POST of show
        Assert.Equal(131072, length);
        Assert.Equal((HttpMethod.Post, "/api/show"), Assert.Single(handler.Requests));
    }

    /// <summary>
    ///     Test that a length that is not there, is not a number, is zero, or is too large for an integer is not an error:
    ///     the length is unknown, or is limited to the largest integer.
    /// </summary>
    /// <param name="body">The answer of the stand-in server.</param>
    /// <param name="expected">The expected length; <see langword="null" /> for unknown.</param>
    [Theory]
    [InlineData("""{"model_info":{"general.architecture":"llama"}}""", null)]
    [InlineData("""{"model_info":{"llama.context_length":"long"}}""", null)]
    [InlineData("""{"model_info":{"llama.context_length":0}}""", null)]
    [InlineData("""{"model_info":{"llama.context_length":4096.5}}""", null)]
    [InlineData("""{"model_info":{"llama.context_length":99999999999}}""", int.MaxValue)]
    [InlineData("""{"details":{}}""", null)]
    [InlineData("""{}""", null)]
    public async Task OllamaProbe_GetContextLength_UnusableAnswer_IsUnknownNotAnError(string body, int? expected)
    {
        // Arrange
        var probe = new OllamaProbe(new StubOllamaHandler(_ => StubOllamaHandler.Reply(HttpStatusCode.OK, body)));

        // Act
        var length = await probe.GetContextLengthAsync(Server, "m", Patient, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, length);
    }

    /// <summary>
    ///     Test that a server that fails or does not answer in time gives an unknown length and not an exception.
    /// </summary>
    [Fact]
    public async Task OllamaProbe_GetContextLength_Failure_IsUnknownNotAnError()
    {
        // Arrange: a model that the server does not know, and a server that does not answer
        var missing = new OllamaProbe(new StubOllamaHandler(_ => StubOllamaHandler.Reply(HttpStatusCode.NotFound, """{"error":"model not found"}""")));
        var hanging = new OllamaProbe(new HangingHandler());

        // Act
        var fromMissing = await missing.GetContextLengthAsync(Server, "nope", Patient, TestContext.Current.CancellationToken);
        var fromHanging = await hanging.GetContextLengthAsync(Server, "m", TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(fromMissing);
        Assert.Null(fromHanging);
    }

    /// <summary>
    ///     Test that missing arguments are refused.
    /// </summary>
    [Fact]
    public async Task OllamaProbe_Arguments_Invalid_AreRefused()
    {
        // Arrange
        var probe = new OllamaProbe(new HangingHandler());

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => probe.ListModelsAsync(null!, Patient, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => probe.GetContextLengthAsync(null!, "m", Patient, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => probe.GetContextLengthAsync(Server, " ", Patient, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     An HTTP handler that never answers until the request is cancelled.
    /// </summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable: the delay never ends without cancellation.");
        }
    }
}

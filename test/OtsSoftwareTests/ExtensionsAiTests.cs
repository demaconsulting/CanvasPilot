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

using System.Text.Json;
using Microsoft.Extensions.AI;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the Microsoft.Extensions.AI off-the-shelf software item (the abstractions and the
///     function-invocation pipeline): the types that carry CanvasPilot's tools, messages, pictures, and chat clients.
///     They run without a language model.
/// </summary>
public class ExtensionsAiTests
{
    /// <summary>
    ///     Test that a function made from a delegate binds its arguments by name from JSON values, as a model's tool
    ///     call supplies them, and describes its parameters in a schema that lists the required ones.
    /// </summary>
    [Fact]
    public async Task ExtensionsAi_AIFunction_Invoke_BindsNamedArgumentsFromJsonValues()
    {
        // Arrange: a function with a required text and an optional count, and arguments that are JSON elements
        var function = AIFunctionFactory.Create(
            (string text, int count) => $"{text} x{count}",
            "demo_repeat",
            "Repeats the text.");
        var arguments = new AIFunctionArguments
        {
            ["text"] = JsonDocument.Parse("\"hi\"").RootElement,
            ["count"] = JsonDocument.Parse("3").RootElement
        };

        // Act: invoke it and read its schema
        var result = await function.InvokeAsync(arguments, TestContext.Current.CancellationToken);
        var properties = function.JsonSchema.GetProperty("properties");
        var required = function.JsonSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();

        // Assert: the arguments were bound by name, and the schema names both parameters as required
        Assert.Equal("hi x3", ((JsonElement)result!).GetString());
        Assert.True(properties.TryGetProperty("text", out _));
        Assert.True(properties.TryGetProperty("count", out _));
        Assert.Contains("text", required);
        Assert.Contains("count", required);
    }

    /// <summary>
    ///     Test that the function-invocation client runs a function that is a class of its own, which is how the
    ///     tools of CanvasPilot's existing descriptors are carried, with the arguments of the model's call, and gives
    ///     the function's result to the model in the next request.
    /// </summary>
    [Fact]
    public async Task ExtensionsAi_FunctionInvokingChatClient_RunsSubclassedFunctionAndReturnsItsResult()
    {
        // Arrange: a function class, a client that calls it once and then answers, and the invoking pipeline
        var function = new EchoFunction();
        var inner = new ScriptedChatClient(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "demo_echo", new Dictionary<string, object?> { ["text"] = "hello" })])],
            [new ChatResponseUpdate(ChatRole.Assistant, "Done.")]);
        using var client = new FunctionInvokingChatClient(inner);

        // Act: ask for a response with the function offered
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Echo hello.")],
            new ChatOptions { Tools = [function] },
            TestContext.Current.CancellationToken);

        // Assert: the function ran once with the argument, its result went back to the model, and the answer came last
        Assert.Equal(["hello"], function.Received);
        var toolResult = inner.Requests[1]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        Assert.Equal("echo hello", toolResult.Result?.ToString());
        Assert.Equal("Done.", response.Text);
    }

    /// <summary>
    ///     Test that picture content keeps its bytes and media type, reports its top-level media type, and reads back
    ///     from a data address.
    /// </summary>
    [Fact]
    public void ExtensionsAi_DataContent_KeepsBytesAndMediaType()
    {
        // Arrange: a few bytes and the data address that spells them
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47];
        var address = "data:image/png;base64," + Convert.ToBase64String(bytes);

        // Act: make the content from the bytes and from the address
        var fromBytes = new DataContent(bytes, "image/png");
        var fromAddress = new DataContent(address);

        // Assert: both hold the same bytes and media type, and the content is recognized as an image
        Assert.Equal("image/png", fromBytes.MediaType);
        Assert.True(fromBytes.HasTopLevelMediaType("image"));
        Assert.False(fromBytes.HasTopLevelMediaType("audio"));
        Assert.Equal(bytes, fromBytes.Data.ToArray());
        Assert.Equal("image/png", fromAddress.MediaType);
        Assert.Equal(bytes, fromAddress.Data.ToArray());
        Assert.StartsWith("data:image/png;base64,", fromBytes.Uri, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that streamed updates join into one response whose text is the pieces in order and whose message holds
    ///     the function call that came between them.
    /// </summary>
    [Fact]
    public void ExtensionsAi_ChatResponseUpdates_ToChatResponse_JoinsTextAndKeepsFunctionCalls()
    {
        // Arrange: updates that hold two pieces of text with a function call between them
        ChatResponseUpdate[] updates =
        [
            new(ChatRole.Assistant, "Let me "),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "demo_echo", new Dictionary<string, object?> { ["text"] = "x" })]),
            new(ChatRole.Assistant, "check.")
        ];

        // Act: join them
        var response = updates.ToChatResponse();

        // Assert: the text is the pieces in order and the call is kept
        Assert.Equal("Let me check.", response.Text);
        Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
    }

    /// <summary>
    ///     A function that is a class of its own, as the tools of the application's descriptors are, that echoes its
    ///     text argument and records what it received.
    /// </summary>
    private sealed class EchoFunction : AIFunction
    {
        /// <summary>
        ///     The schema of the one parameter.
        /// </summary>
        private static readonly JsonElement Schema = JsonDocument.Parse(
            """{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}""").RootElement.Clone();

        /// <summary>
        ///     Gets the texts that the function received, in order.
        /// </summary>
        public List<string> Received { get; } = [];

        /// <summary>
        ///     Gets the name of the function.
        /// </summary>
        public override string Name => "demo_echo";

        /// <summary>
        ///     Gets the description of the function.
        /// </summary>
        public override string Description => "Echoes the text.";

        /// <summary>
        ///     Gets the schema of the parameters.
        /// </summary>
        public override JsonElement JsonSchema => Schema;

        /// <summary>
        ///     Echoes the text argument.
        /// </summary>
        /// <param name="arguments">The arguments of the call.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The echo.</returns>
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var text = arguments["text"]?.ToString() ?? string.Empty;
            Received.Add(text);
            return new ValueTask<object?>("echo " + text);
        }
    }
}

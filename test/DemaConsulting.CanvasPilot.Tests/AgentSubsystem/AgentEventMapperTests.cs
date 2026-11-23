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
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DemaConsulting.AgentKit.Agents.ChatClient;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AgentEventMapper" />: every kind of content of an update, the pairing of a call with its
///     result by id, the summaries that the chat shows, the failure of every cause with its sentence, the rule that a
///     canceled turn is not a failure, and the rule that the log gets metadata only. The streams are scripted; one test runs
///     a real agent on a scripted chat client.
/// </summary>
public sealed class AgentEventMapperTests
{
    /// <summary>A text that stands for the words of a user or a model, which must never reach the log.</summary>
    private const string Sentinel = "Sentinel-Words-Of-The-User-4711";

    /// <summary>
    ///     Makes the mapper of a turn on a provider.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="model">The model of the settings.</param>
    /// <param name="logger">The log, or <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">The token of the turn; the token of the test when <see langword="null" />.</param>
    /// <returns>The mapper.</returns>
    private static AgentEventMapper Create(
        AiProviderKind provider = AiProviderKind.Ollama,
        string model = "gemma3",
        RollingFileLogger? logger = null,
        CancellationToken? cancellationToken = null)
    {
        return new AgentEventMapper(provider, model, logger, cancellationToken ?? TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Makes an update of the assistant with some content.
    /// </summary>
    /// <param name="contents">The content.</param>
    /// <returns>The update.</returns>
    private static AgentResponseUpdate Update(params AIContent[] contents)
    {
        return new AgentResponseUpdate(ChatRole.Assistant, [.. contents]);
    }

    /// <summary>
    ///     Streams updates, optionally followed by an exception, the way an agent does.
    /// </summary>
    /// <param name="updates">The updates.</param>
    /// <param name="thenThrow">The exception to throw after the last update; <see langword="null" /> for none.</param>
    /// <returns>The stream.</returns>
    private static IAsyncEnumerable<AgentResponseUpdate> Stream(IEnumerable<AgentResponseUpdate> updates, Exception? thenThrow = null)
    {
        return StreamCore(updates, thenThrow, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Streams updates, optionally followed by an exception; the iterator behind <see cref="Stream" />, which stops when the
    ///     token of the consumer or the token of the test is canceled.
    /// </summary>
    /// <param name="updates">The updates.</param>
    /// <param name="thenThrow">The exception to throw after the last update; <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">The token of the stream.</param>
    /// <returns>The stream.</returns>
    private static async IAsyncEnumerable<AgentResponseUpdate> StreamCore(
        IEnumerable<AgentResponseUpdate> updates,
        Exception? thenThrow,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return update;
        }

        if (thenThrow is not null)
        {
            throw thenThrow;
        }
    }

    /// <summary>
    ///     Reads all events of a stream.
    /// </summary>
    /// <param name="mapper">The mapper.</param>
    /// <param name="updates">The stream.</param>
    /// <returns>The events.</returns>
    private static async Task<List<AgentEvent>> Collect(AgentEventMapper mapper, IAsyncEnumerable<AgentResponseUpdate> updates)
    {
        var events = new List<AgentEvent>();
        await foreach (var mapped in mapper.MapAsync(updates))
        {
            events.Add(mapped);
        }

        return events;
    }

    /// <summary>
    ///     Test that the constants of the unit are the documented values.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Constants_Rule_AreTheDocumentedValues()
    {
        // Assert: the prefix of a refusal of the AI library, the stand-in name, and the length of an argument that is shown
        Assert.Equal("Denied (", AgentEventMapper.RefusalPrefix);
        Assert.Equal("tool", AgentEventMapper.UnknownToolName);
        Assert.Equal(60, AgentEventMapper.MaxArgumentTextLength);
        Assert.Equal(AgentReadiness.NotSignedInMessage, AgentEventMapper.CopilotAuthenticationMessage);

        // The prefix is what the library really puts in front of a refusal, which the mapper relies on
        Assert.StartsWith(
            AgentEventMapper.RefusalPrefix,
            (string)ToolResult.Denied(DenialReason.InvalidRequest, "x"),
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that missing arguments are refused.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Arguments_Null_AreRefused()
    {
        // Arrange
        var mapper = Create();

        // Act and assert: each entry point refuses null
        Assert.Throws<ArgumentNullException>(() => mapper.Map(null!));
        Assert.Throws<ArgumentNullException>(() => mapper.MapAsync(null!));
        Assert.Throws<ArgumentNullException>(() => mapper.MapFailure(null!));
    }

    /// <summary>
    ///     Test that the pieces of text of an update are joined into one event, that an update without text gives none, and
    ///     that a piece of white space is kept because it may sit between two words.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_TextContent_JoinsPiecesAndDropsEmptyUpdates()
    {
        // Arrange
        var mapper = Create();

        // Act: an update of two pieces, an update of nothing, an update of empty text, and a space
        var joined = mapper.Map(Update(new TextContent("Hel"), new TextContent("lo")));
        var nothing = mapper.Map(Update());
        var empty = mapper.Map(Update(new TextContent(string.Empty), new TextContent(null)));
        var space = mapper.Map(Update(new TextContent(" ")));

        // Assert
        Assert.Equal("Hello", Assert.IsType<AgentEvent.TextDelta>(Assert.Single(joined)).Text);
        Assert.Empty(nothing);
        Assert.Empty(empty);
        Assert.Equal(" ", Assert.IsType<AgentEvent.TextDelta>(Assert.Single(space)).Text);
    }

    /// <summary>
    ///     Test that text before and after a call keeps its place around the call.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_TextAroundACall_KeepsTheOrder()
    {
        // Arrange
        var mapper = Create();

        // Act
        var events = mapper.Map(Update(
            new TextContent("Before. "),
            new FunctionCallContent("c1", "project_list", new Dictionary<string, object?>()),
            new TextContent("After.")));

        // Assert
        Assert.Collection(
            events,
            first => Assert.Equal("Before. ", Assert.IsType<AgentEvent.TextDelta>(first).Text),
            second => Assert.Equal("project_list", Assert.IsType<AgentEvent.ToolStarted>(second).Name),
            third => Assert.Equal("After.", Assert.IsType<AgentEvent.TextDelta>(third).Text));
    }

    /// <summary>
    ///     Test that a call and its result are tied by the id, that the name comes from the call, and that a long text
    ///     argument is shown by its length and not in full.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_CallAndResult_AreTiedByIdWithSummaries()
    {
        // Arrange
        var mapper = Create();
        var arguments = new Dictionary<string, object?>
        {
            ["path"] = "cover.svg",
            ["content"] = new string('x', 1234),
            ["count"] = 3,
            ["flag"] = true,
            ["nothing"] = null,
            ["element"] = JsonDocument.Parse("\"short\"").RootElement,
            ["object"] = JsonDocument.Parse("{\"a\":1}").RootElement,
        };

        // Act: the call, then its result in another update
        var started = Assert.IsType<AgentEvent.ToolStarted>(Assert.Single(mapper.Map(Update(new FunctionCallContent("c1", "project_write", arguments)))));
        var finished = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(mapper.Map(Update(new FunctionResultContent(
            "c1",
            "File 'cover.svg' created (1,234 bytes). Call render_image to check the result.")))));

        // Assert: the long text is shown by its length and the rest by value; the result names the tool of the call
        Assert.Equal("c1", started.CallId);
        Assert.Equal(
            "path=cover.svg, content=(1,234 characters), count=3, flag=True, nothing=null, element=short, object={\"a\":1}",
            started.ArgumentSummary);
        Assert.Equal("c1", finished.CallId);
        Assert.Equal("project_write", finished.Name);
        Assert.True(finished.Succeeded);
        Assert.False(finished.HasImage);
        Assert.StartsWith("File 'cover.svg' created (1,234 bytes).", finished.Summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the answer of project_write that a file was replaced reaches the summary, shortened to the limit.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_ReplacedFileAnswer_SummaryShowsReplacedAndIsShort()
    {
        // Arrange: a call, and a result whose text is longer than a summary may be
        var mapper = Create();
        mapper.Map(Update(new FunctionCallContent("c1", "project_write", null)));
        var longTail = new string('y', 400);

        // Act
        var finished = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(mapper.Map(Update(
            new FunctionResultContent("c1", $"File 'cover.svg' replaced (6 bytes -> 7 bytes). {longTail}")))));

        // Assert
        Assert.StartsWith("File 'cover.svg' replaced (6 bytes -> 7 bytes).", finished.Summary, StringComparison.Ordinal);
        Assert.Equal(AgentEvent.MaxSummaryLength, finished.Summary.Length);
        Assert.EndsWith("...", finished.Summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the summary of the arguments never exceeds the limit, however many arguments there are.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_ManyArguments_SummaryIsShortened()
    {
        // Arrange: thirty arguments of moderate length
        var mapper = Create();
        var arguments = Enumerable.Range(0, 30).ToDictionary(index => $"argument{index}", index => (object?)$"value-{index}");

        // Act
        var started = Assert.IsType<AgentEvent.ToolStarted>(Assert.Single(mapper.Map(Update(new FunctionCallContent("c1", "tool_many", arguments)))));

        // Assert
        Assert.Equal(AgentEvent.MaxSummaryLength, started.ArgumentSummary.Length);
        Assert.StartsWith("argument0=value-0, argument1=value-1", started.ArgumentSummary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a refusal, and a result that carries an exception, are not successes.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_RefusalAndException_AreNotSucceeded()
    {
        // Arrange
        var mapper = Create();
        mapper.Map(Update(
            new FunctionCallContent("c1", "project_read", null),
            new FunctionCallContent("c2", "render_image", null)));

        // Act
        var refused = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(mapper.Map(Update(new FunctionResultContent(
            "c1",
            ToolResult.Denied(DenialReason.PathNotPermitted, "The path is outside the project."))))));
        var failed = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(mapper.Map(Update(
            new FunctionResultContent("c2", null) { Exception = new InvalidOperationException("Render\nfailed.") }))));

        // Assert: the reason of the refusal is the summary, and the message of the exception is on one line
        Assert.False(refused.Succeeded);
        Assert.Contains("The path is outside the project.", refused.Summary, StringComparison.Ordinal);
        Assert.False(failed.Succeeded);
        Assert.Equal("The tool failed: Render failed.", failed.Summary);
    }

    /// <summary>
    ///     Test that a picture in a result sets the flag, with a caption as the summary or a stand-in when there is none.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_ImageResult_SetsHasImage()
    {
        // Arrange
        var mapper = Create();
        mapper.Map(Update(
            new FunctionCallContent("c1", "render_image", null),
            new FunctionCallContent("c2", "render_image", null),
            new FunctionCallContent("c3", "render_image", null)));
        var png = new byte[] { 1, 2, 3 };

        // Act: a result with a caption, a result of the picture alone, and a result of a list of content
        var captioned = mapper.Map(Update(new FunctionResultContent("c1", ToolResult.Image(png, "image/png", "Drawn: 640 x 480 px."))));
        var alone = mapper.Map(Update(new FunctionResultContent("c2", ToolResult.Image(png, "image/png"))));
        var listed = mapper.Map(Update(new FunctionResultContent("c3", new List<AIContent> { new TextContent("One."), new DataContent(png, "image/png") })));

        // Assert
        var first = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(captioned));
        Assert.True(first.HasImage);
        Assert.Equal("Drawn: 640 x 480 px.", first.Summary);
        var second = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(alone));
        Assert.True(second.HasImage);
        Assert.Equal("A picture.", second.Summary);
        var third = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(listed));
        Assert.True(third.HasImage);
        Assert.Equal("One.", third.Summary);
    }

    /// <summary>
    ///     Test that results of other shapes are read: JSON text, a structured value, a number, and nothing.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_OtherResultShapes_AreSummarized()
    {
        // Arrange
        var mapper = Create();
        mapper.Map(Update(
            new FunctionCallContent("c1", "a_tool", null),
            new FunctionCallContent("c2", "b_tool", null),
            new FunctionCallContent("c3", "c_tool", null),
            new FunctionCallContent("c4", "d_tool", null)));

        // Act
        var json = mapper.Map(Update(new FunctionResultContent("c1", JsonDocument.Parse("\"text answer\"").RootElement)));
        var structured = mapper.Map(Update(new FunctionResultContent("c2", JsonDocument.Parse("{\"count\":3}").RootElement)));
        var number = mapper.Map(Update(new FunctionResultContent("c3", 42)));
        var none = mapper.Map(Update(new FunctionResultContent("c4", null)));

        // Assert
        Assert.Equal("text answer", Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(json)).Summary);
        Assert.Equal("{\"count\":3}", Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(structured)).Summary);
        Assert.Equal("42", Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(number)).Summary);
        var empty = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(none));
        Assert.Equal(string.Empty, empty.Summary);
        Assert.True(empty.Succeeded);
    }

    /// <summary>
    ///     Test that a call that is reported twice is shown once, and that a result of a call that was never seen still
    ///     gets an event with the stand-in name.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_DuplicateCallAndUnknownResult_AreHandled()
    {
        // Arrange
        var mapper = Create();

        // Act
        var first = mapper.Map(Update(new FunctionCallContent("c1", "project_list", null)));
        var again = mapper.Map(Update(new FunctionCallContent("c1", "project_list", null)));
        var nameless = mapper.Map(Update(new FunctionCallContent("c9", string.Empty, null)));
        var unknown = mapper.Map(Update(new FunctionResultContent("zz", "answer")));

        // Assert
        Assert.Single(first);
        Assert.Empty(again);
        Assert.Empty(nameless);
        var finished = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(unknown));
        Assert.Equal("zz", finished.CallId);
        Assert.Equal(AgentEventMapper.UnknownToolName, finished.Name);
    }

    /// <summary>
    ///     Test that calls without an id get ids that the mapper makes, and that results without an id are paired with them
    ///     in the order of the calls.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_CallsWithoutIds_GetMadeIdsAndPairInOrder()
    {
        // Arrange
        var mapper = Create();

        // Act: two calls without ids, then their two results without ids
        var started = mapper.Map(Update(
            new FunctionCallContent(string.Empty, "project_list", null),
            new FunctionCallContent(string.Empty, "style_get", null)));
        var firstResult = mapper.Map(Update(new FunctionResultContent(string.Empty, "listing")));
        var secondResult = mapper.Map(Update(new FunctionResultContent(string.Empty, "style")));

        // Assert
        var one = Assert.IsType<AgentEvent.ToolStarted>(started[0]);
        var two = Assert.IsType<AgentEvent.ToolStarted>(started[1]);
        Assert.NotEqual(one.CallId, two.CallId);
        var finishedOne = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(firstResult));
        var finishedTwo = Assert.IsType<AgentEvent.ToolFinished>(Assert.Single(secondResult));
        Assert.Equal((one.CallId, "project_list"), (finishedOne.CallId, finishedOne.Name));
        Assert.Equal((two.CallId, "style_get"), (finishedTwo.CallId, finishedTwo.Name));
    }

    /// <summary>
    ///     Test that usage is mapped with the counts that the provider reported, and that reasoning and pictures outside a
    ///     tool result are not shown.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_UsageAndOtherContent_UsageIsMappedOthersAreIgnored()
    {
        // Arrange
        var mapper = Create();

        // Act
        var events = mapper.Map(Update(
            new TextReasoningContent("thinking"),
            new DataContent(new byte[] { 1 }, "image/png"),
            new UsageContent(new UsageDetails { InputTokenCount = 120, OutputTokenCount = 30 }),
            new UsageContent(new UsageDetails())));

        // Assert
        Assert.Collection(
            events,
            first => Assert.Equal(new AgentEvent.Usage(120, 30), first),
            second => Assert.Equal(new AgentEvent.Usage(null, null), second));
    }

    /// <summary>
    ///     Test that an error that the provider sent as content ends the update with a failure, and that nothing after it
    ///     is mapped.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_ErrorContent_BecomesFailedAndStopsTheUpdate()
    {
        // Arrange
        var mapper = Create(AiProviderKind.GitHubCopilot, string.Empty);

        // Act
        var events = mapper.Map(Update(
            new TextContent("Partial."),
            new ErrorContent("SDK protocol version mismatch: SDK supports versions 4-4, but server reports version 3"),
            new TextContent("Never shown.")));

        // Assert: the text before the error, then the failure, and nothing after it
        Assert.Equal(2, events.Count);
        Assert.IsType<AgentEvent.TextDelta>(events[0]);
        var failed = Assert.IsType<AgentEvent.Failed>(events[1]);
        Assert.Equal(AgentFailureKind.ProtocolMismatch, failed.Kind);
    }

    /// <summary>
    ///     Test that a stream that ends is followed by Completed, and that an empty stream is only that.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_MapAsync_StreamEnds_EndsWithCompleted()
    {
        // Arrange
        var mapper = Create();

        // Act
        var events = await Collect(mapper, Stream([Update(new TextContent("Hi.")), Update(new UsageContent(new UsageDetails { InputTokenCount = 1 }))]));
        var empty = await Collect(Create(), Stream([]));

        // Assert
        Assert.Collection(
            events,
            first => Assert.Equal("Hi.", Assert.IsType<AgentEvent.TextDelta>(first).Text),
            second => Assert.IsType<AgentEvent.Usage>(second),
            third => Assert.IsType<AgentEvent.Completed>(third));
        Assert.IsType<AgentEvent.Completed>(Assert.Single(empty));
    }

    /// <summary>
    ///     Test that a turn that the caller cancels ends without a failure and without Completed.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_MapAsync_CancelledByCaller_EndsWithoutFailureOrCompletion()
    {
        // Arrange: a long stream and a token that the consumer cancels after the first event
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var mapper = Create(cancellationToken: cancel.Token);
        var updates = Stream(Enumerable.Range(0, 20).Select(index => Update(new TextContent($"piece {index} "))));
        var events = new List<AgentEvent>();

        // Act
        await foreach (var mapped in mapper.MapAsync(updates))
        {
            events.Add(mapped);
            await cancel.CancelAsync();
        }

        // Assert: only what came before the stop
        Assert.Single(events);
        Assert.IsType<AgentEvent.TextDelta>(events[0]);
    }

    /// <summary>
    ///     Test that any exception after the caller canceled is not a failure, and that an operation that was canceled
    ///     without the caller is not a failure and not a completion.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_MapAsync_CancelLikeExceptions_AreNotFailures()
    {
        // Arrange: a token that is already canceled with an exception of another type, and an operation canceled by itself
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var byCaller = await Collect(Create(cancellationToken: cancel.Token), Stream([], new IOException("The stream was closed.")));
        var byItself = await Collect(Create(), Stream([], new OperationCanceledException()));

        // Assert
        Assert.Empty(byCaller);
        Assert.Empty(byItself);
    }

    /// <summary>
    ///     Test that an exception after some text gives the text and then the failure, and ends the stream.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_MapAsync_ExceptionAfterText_YieldsTextThenFailed()
    {
        // Arrange
        var mapper = Create();

        // Act
        var events = await Collect(
            mapper,
            Stream([Update(new TextContent("Start."))], new HttpRequestException("Connection refused.")));

        // Assert
        Assert.Equal(2, events.Count);
        Assert.IsType<AgentEvent.TextDelta>(events[0]);
        Assert.Equal(AgentFailureKind.ConnectionLost, Assert.IsType<AgentEvent.Failed>(events[1]).Kind);
    }

    /// <summary>
    ///     Test that an error in the content stops the stream with the failure and without Completed.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_MapAsync_ErrorContent_EndsWithFailedOnly()
    {
        // Arrange
        var mapper = Create();

        // Act
        var events = await Collect(
            mapper,
            Stream([Update(new ErrorContent("Boom.")), Update(new TextContent("After."))]));

        // Assert
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
        Assert.Equal(AgentFailureKind.Unexpected, failed.Kind);
        Assert.Contains("Boom.", failed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a model that Ollama does not have, which OllamaSharp reports only as a 404 status, is worded with the
    ///     <c>ollama pull</c> command and the model name.
    /// </summary>
    [Fact]
    public void AgentEventMapper_MapFailure_Ollama404_IsModelNotFoundWithPullCommand()
    {
        // Arrange
        var mapper = Create(AiProviderKind.Ollama, "gemma3:12b");

        // Act
        var failed = mapper.MapFailure(new HttpRequestException("Response status code does not indicate success: 404 (Not Found).", null, HttpStatusCode.NotFound));

        // Assert
        Assert.NotNull(failed);
        Assert.Equal(AgentFailureKind.ModelNotFound, failed.Kind);
        Assert.Equal(
            "The model 'gemma3:12b' was not found on the Ollama server. Run `ollama pull gemma3:12b` or choose another model in Settings.",
            failed.Message);
    }

    /// <summary>
    ///     Test the failures of Ollama by their causes: sign-in, server errors, refused connection, broken socket.
    /// </summary>
    [Fact]
    public void AgentEventMapper_MapFailure_OllamaCauses_AreClassified()
    {
        // Arrange
        var mapper = Create(AiProviderKind.Ollama, "gemma3");

        // Act
        var unauthorized = mapper.MapFailure(new HttpRequestException("401", null, HttpStatusCode.Unauthorized));
        var forbidden = mapper.MapFailure(new HttpRequestException("403", null, HttpStatusCode.Forbidden));
        var server = mapper.MapFailure(new HttpRequestException("Server error.", null, HttpStatusCode.InternalServerError));
        var refused = mapper.MapFailure(new HttpRequestException("No connection could be made because the target machine actively refused it."));
        var socket = mapper.MapFailure(new InvalidOperationException("Outer.", new HttpRequestException("Inner.", new SocketException())));
        var io = mapper.MapFailure(new IOException("The response ended prematurely."));
        var timeout = mapper.MapFailure(new TaskCanceledException("The request was canceled due to the configured timeout.", new TimeoutException()));

        // Assert
        Assert.Equal((AgentFailureKind.Authentication, AgentEventMapper.OllamaAuthenticationMessage), (unauthorized!.Kind, unauthorized.Message));
        Assert.Equal(AgentFailureKind.Authentication, forbidden!.Kind);
        Assert.Equal(AgentFailureKind.Unexpected, server!.Kind);
        Assert.StartsWith("The AI provider answered with an error (HTTP 500): ", server.Message, StringComparison.Ordinal);
        Assert.Equal((AgentFailureKind.ConnectionLost, AgentEventMapper.OllamaConnectionLostMessage), (refused!.Kind, refused.Message));
        Assert.Equal(AgentFailureKind.ConnectionLost, socket!.Kind);
        Assert.Equal(AgentFailureKind.ConnectionLost, io!.Kind);
        Assert.Equal(AgentFailureKind.ConnectionLost, timeout!.Kind);
    }

    /// <summary>
    ///     Test the sentences of the failures of GitHub Copilot that the SDK reports only as text.
    /// </summary>
    [Theory]
    [InlineData("SDK protocol version mismatch: SDK supports versions 4-4, but server reports version 3", AgentFailureKind.ProtocolMismatch)]
    [InlineData("No GitHub OAuth token or Copilot HMAC key provided", AgentFailureKind.Authentication)]
    [InlineData("Authentication failed: bad credentials", AgentFailureKind.Authentication)]
    [InlineData("Model 'foo' is not available", AgentFailureKind.ModelNotFound)]
    [InlineData("The requested model does not exist", AgentFailureKind.ModelNotFound)]
    [InlineData("The JSON-RPC connection was closed", AgentFailureKind.ConnectionLost)]
    [InlineData("The Copilot process has exited unexpectedly", AgentFailureKind.ConnectionLost)]
    [InlineData("Something odd happened", AgentFailureKind.Unexpected)]
    public void AgentEventMapper_MapFailure_CopilotMessages_AreClassifiedByTheirWords(string message, AgentFailureKind expected)
    {
        // Arrange
        var mapper = Create(AiProviderKind.GitHubCopilot, "foo");

        // Act
        var failed = mapper.MapFailure(new InvalidOperationException(message));

        // Assert
        Assert.NotNull(failed);
        Assert.Equal(expected, failed.Kind);
        Assert.False(string.IsNullOrWhiteSpace(failed.Message));
    }

    /// <summary>
    ///     Test the exact guidance of a Copilot that is not signed in, a protocol mismatch, and a missing model.
    /// </summary>
    [Fact]
    public void AgentEventMapper_MapFailure_CopilotGuidance_NamesTheRemedy()
    {
        // Arrange
        var named = Create(AiProviderKind.GitHubCopilot, "foo");
        var unnamed = Create(AiProviderKind.GitHubCopilot, string.Empty);

        // Act
        var auth = named.MapFailure(new InvalidOperationException("No GitHub OAuth token or Copilot HMAC key provided"));
        var protocol = named.MapFailure(new InvalidOperationException("SDK protocol version mismatch: SDK supports versions 4-4, but server reports version 3"));
        var missing = named.MapFailure(new InvalidOperationException("Model 'foo' is not available"));
        var missingDefault = unnamed.MapFailure(new InvalidOperationException("model not found"));
        var lost = named.MapFailure(new IOException("The pipe is being closed."));

        // Assert
        Assert.Contains("`copilot`", auth!.Message, StringComparison.Ordinal);
        Assert.Contains("`/login`", auth.Message, StringComparison.Ordinal);
        Assert.Contains("`gh auth login`", auth.Message, StringComparison.Ordinal);
        Assert.Contains("COPILOT_GITHUB_TOKEN", auth.Message, StringComparison.Ordinal);
        Assert.Equal(AgentEventMapper.ProtocolMismatchMessage, protocol!.Message);
        Assert.Contains("winget upgrade GitHub.Copilot", protocol.Message, StringComparison.Ordinal);
        Assert.Contains("The model 'foo' is not available to your Copilot account.", missing!.Message, StringComparison.Ordinal);
        Assert.StartsWith("GitHub Copilot did not accept the model.", missingDefault!.Message, StringComparison.Ordinal);
        Assert.Equal(AgentEventMapper.CopilotConnectionLostMessage, lost!.Message);
    }

    /// <summary>
    ///     Test that Ollama without a model name still gets a sentence for a missing model, and an authentication failure
    ///     is not worded with the GitHub Copilot guidance.
    /// </summary>
    [Fact]
    public void AgentEventMapper_MapFailure_OllamaWithoutModelName_StillGivesASentence()
    {
        // Arrange
        var mapper = Create(AiProviderKind.Ollama, "  ");

        // Act
        var missing = mapper.MapFailure(new HttpRequestException("404", null, HttpStatusCode.NotFound));
        var auth = mapper.MapFailure(new InvalidOperationException("Unauthorized"));

        // Assert
        Assert.Contains("`ollama list`", missing!.Message, StringComparison.Ordinal);
        Assert.Equal(AgentEventMapper.OllamaAuthenticationMessage, auth!.Message);
    }

    /// <summary>
    ///     Test that the cause is found below wrapping exceptions, and that an aggregate is searched.
    /// </summary>
    [Fact]
    public void AgentEventMapper_MapFailure_WrappedCause_IsFound()
    {
        // Arrange
        var mapper = Create(AiProviderKind.Ollama, "gemma3");
        var wrapped = new InvalidOperationException("The run failed.", new HttpRequestException("404", null, HttpStatusCode.NotFound));
        var aggregate = new AggregateException(new ArgumentException("First."), new HttpRequestException("Refused."));

        // Act
        var fromWrapped = mapper.MapFailure(wrapped);
        var fromAggregate = mapper.MapFailure(aggregate);

        // Assert
        Assert.Equal(AgentFailureKind.ModelNotFound, fromWrapped!.Kind);
        Assert.Equal(AgentFailureKind.ConnectionLost, fromAggregate!.Kind);
    }

    /// <summary>
    ///     Test that an exception without a message still gives a sentence that is not empty.
    /// </summary>
    [Fact]
    public void AgentEventMapper_MapFailure_ExceptionWithoutMessage_GivesTheTypeName()
    {
        // Arrange
        var mapper = Create();

        // Act
        var failed = mapper.MapFailure(new ArgumentException(string.Empty));

        // Assert: the type name stands in for the message, which for this type is never really empty
        Assert.NotNull(failed);
        Assert.Equal(AgentFailureKind.Unexpected, failed.Kind);
        Assert.StartsWith("The AI provider reported a problem: ", failed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a cancellation is not a failure, with the token canceled and with an operation that was canceled.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_MapFailure_Cancellation_IsNull()
    {
        // Arrange
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var mapper = Create();
        var canceledTurn = Create(cancellationToken: cancel.Token);

        // Act and assert
        Assert.Null(mapper.MapFailure(new OperationCanceledException()));
        Assert.Null(mapper.MapFailure(new TaskCanceledException()));
        Assert.Null(canceledTurn.MapFailure(new IOException("closed")));
    }

    /// <summary>
    ///     Test that the log of a turn holds metadata only: the provider, the kind of the failure, and the type of the
    ///     exception, and never the text of a message, an argument, a result, or an exception.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_Logging_Turn_HoldsMetadataOnly()
    {
        // Arrange: a log in a temp folder, and a turn that holds the sentinel in every kind of text
        var folder = Directory.CreateTempSubdirectory("CanvasPilotMapperLog-");
        try
        {
            var logger = new RollingFileLogger(folder.FullName);
            var completed = Create(AiProviderKind.Ollama, "gemma3", logger);
            var failing = Create(AiProviderKind.GitHubCopilot, "foo", logger);

            // Act: a completed turn with the sentinel in text, an argument, and a result; a failed turn; a canceled turn
            await Collect(
                completed,
                Stream(
                [
                    Update(new TextContent(Sentinel), new FunctionCallContent("c1", "project_write", new Dictionary<string, object?> { ["content"] = Sentinel })),
                    Update(new FunctionResultContent("c1", Sentinel)),
                ]));
            await Collect(
                failing,
                Stream([Update(new TextContent(Sentinel))], new InvalidOperationException($"The server said {Sentinel}.")));
            using var cancel = new CancellationTokenSource();
            await cancel.CancelAsync();
            await Collect(Create(AiProviderKind.Ollama, "gemma3", logger, cancel.Token), Stream([], new IOException(Sentinel)));

            // Assert: the lines name the provider, the kind, and the type, and not a single word of the turn
            var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(Sentinel, log, StringComparison.Ordinal);
            Assert.Contains("Turn completed: provider=Ollama textEvents=1 toolCalls=1", log, StringComparison.Ordinal);
            Assert.Contains("Turn failed: provider=GitHubCopilot kind=Unexpected source=InvalidOperationException", log, StringComparison.Ordinal);
            Assert.Contains("Turn canceled: provider=Ollama", log, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>
    ///     Test that a real agent on a scripted chat client, which streams a sentence, calls a tool, and answers again, gives
    ///     the events in order: text, the start and end of the call, text, and Completed.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_MapAsync_RealAgentToolTurn_GivesTheEventsInOrder()
    {
        // Arrange: an agent with one tool over a client that first asks for the tool and then answers
        var client = new ScriptedChatClient(
            [
                new ChatResponseUpdate(ChatRole.Assistant, "Let me check. "),
                new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "demo_ping", new Dictionary<string, object?> { ["note"] = "hello" })]),
            ],
            [new ChatResponseUpdate(ChatRole.Assistant, "All good.")]);
        var tool = GuardedToolFactory.Create(() => ToolResult.Text("pong"), "demo_ping", "Answers a ping.");
        var agent = ChatClientAgentFactory.Create(client, [tool], "You are a test.", "test-agent");
        var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

        // Act
        var events = await Collect(
            Create(),
            agent.RunStreamingAsync("Ping?", session, cancellationToken: TestContext.Current.CancellationToken));

        // Assert: the answer text comes in pieces, the call is started and finished with its id, and the turn completes
        Assert.Equal("Let me check. All good.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
        var started = Assert.Single(events.OfType<AgentEvent.ToolStarted>());
        Assert.Equal(("call-1", "demo_ping"), (started.CallId, started.Name));
        var finished = Assert.Single(events.OfType<AgentEvent.ToolFinished>());
        Assert.Equal(("call-1", "demo_ping", true, "pong"), (finished.CallId, finished.Name, finished.Succeeded, finished.Summary));
        Assert.True(events.IndexOf(started) < events.IndexOf(finished));
        Assert.IsType<AgentEvent.Completed>(events[^1]);
        Assert.DoesNotContain(events, mapped => mapped is AgentEvent.Failed);
    }
}

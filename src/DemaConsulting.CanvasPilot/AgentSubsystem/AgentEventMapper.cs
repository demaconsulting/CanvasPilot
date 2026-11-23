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

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Turns the stream of updates that an AI agent produces for one turn into the events of the turn, which do not depend
///     on the AI provider, and turns the exception of a failed turn into a failure with a sentence that says what to do.
/// </summary>
/// <remarks>
///     <para>
///         GitHub Copilot and Ollama both produce the same kinds of content, so one mapping serves both. Text becomes
///         <see cref="AgentEvent.TextDelta" />: the pieces of one update are joined, and an update without text is dropped.
///         A call of a tool becomes <see cref="AgentEvent.ToolStarted" /> with a short summary of the arguments (a long
///         text is shown by its length, never in full). The result of the call becomes <see cref="AgentEvent.ToolFinished" />
///         with the tool name that the mapper remembers by the id of the call, whether the tool did what was asked (a result
///         that is a refusal, or that carries an exception, did not), a summary of its text (so the answer of
///         <c>project_write</c> that says a file was created or replaced reaches the chat), and whether it carried a
///         picture. Usage becomes <see cref="AgentEvent.Usage" />. A stream that ends is followed by
///         <see cref="AgentEvent.Completed" />.
///     </para>
///     <para>
///         An exception ends the turn with <see cref="AgentEvent.Failed" /> of a kind that the cause tells: a refused sign-in
///         (the sentence tells the user to run <c>copilot</c> and type <c>/login</c>, and names <c>gh auth login</c> and
///         <c>COPILOT_GITHUB_TOKEN</c> as alternatives, for Copilot), a protocol version that the
///         Copilot CLI and the AI library do not share, a model that the provider does not have (OllamaSharp carries only a
///         404 status, so the sentence names <c>ollama pull</c>), or a connection that was lost. A turn that the caller
///         cancels is not a failure: the stream simply ends. The exception chain is searched from the outside in, by type
///         first and by the words of the message second, because the Copilot SDK reports its problems only as text.
///     </para>
///     <para>
///         The log gets metadata only: the provider, the kind of the failure, the type of the exception, and counts. The
///         text of the messages, the arguments and results of tools, and the text of an exception are never logged.
///         A mapper is made for one turn because it remembers the calls of the turn; it is not safe for concurrent use.
///     </para>
/// </remarks>
public sealed class AgentEventMapper
{
    /// <summary>
    ///     The words that a refusal of a tool starts with, which the AI library puts in front of the reason.
    /// </summary>
    public const string RefusalPrefix = "Denied (";

    /// <summary>
    ///     The name that a result gets when its call was never seen.
    /// </summary>
    public const string UnknownToolName = "tool";

    /// <summary>
    ///     The longest text that an argument is shown by; a longer text is shown by its length.
    /// </summary>
    public const int MaxArgumentTextLength = 60;

    /// <summary>
    ///     The sentence of a sign-in that the provider refused, for GitHub Copilot.
    /// </summary>
    public const string CopilotAuthenticationMessage = AgentReadiness.NotSignedInMessage;

    /// <summary>
    ///     The sentence of a sign-in that the provider refused, for Ollama.
    /// </summary>
    public const string OllamaAuthenticationMessage =
        "The Ollama server refused the request. Check the address in Settings and any gateway or proxy in front of the server, then send the message again.";

    /// <summary>
    ///     The sentence of a protocol version that the Copilot CLI and the AI library do not share.
    /// </summary>
    public const string ProtocolMismatchMessage =
        "The Copilot CLI and CanvasPilot's AI library do not speak the same protocol version. Run `winget upgrade GitHub.Copilot`, then choose Check connection.";

    /// <summary>
    ///     The sentence of a connection that was lost, for GitHub Copilot.
    /// </summary>
    public const string CopilotConnectionLostMessage =
        "The connection to GitHub Copilot was lost. Check your network and that the Copilot CLI still runs, then send the message again.";

    /// <summary>
    ///     The sentence of a connection that was lost, for Ollama.
    /// </summary>
    public const string OllamaConnectionLostMessage =
        "The connection to the Ollama server was lost or could not be made. Check that Ollama is running and that the address in Settings is right, then send the message again.";

    /// <summary>
    ///     The deepest level of the chain of inner exceptions that is searched.
    /// </summary>
    private const int MaxExceptionDepth = 8;

    /// <summary>
    ///     The start of an id that the mapper makes for a call that the provider sent without one.
    /// </summary>
    private const string MadeIdPrefix = "call-";

    /// <summary>
    ///     The words of a message of the Copilot SDK that say that the protocol versions differ.
    /// </summary>
    private static readonly string[] ProtocolWords = ["protocol version", "protocol mismatch"];

    /// <summary>
    ///     The words of a message that say that the sign-in was refused or missing.
    /// </summary>
    private static readonly string[] AuthenticationWords =
    [
        "oauth token", "hmac key", "not authenticated", "unauthorized", "authentication", "bad credentials", "not signed in",
        "login required",
    ];

    /// <summary>
    ///     The words that, together with the word model, say that the model is not there.
    /// </summary>
    private static readonly string[] ModelMissingWords =
        ["not found", "not available", "unknown", "invalid", "not supported", "does not exist"];

    /// <summary>
    ///     The words of a message that say that the connection broke.
    /// </summary>
    private static readonly string[] ConnectionWords =
        ["connection", "disconnected", "broken pipe", "pipe is being closed", "stream closed", "has exited", "timed out", "timeout"];

    /// <summary>
    ///     The provider that the turn runs on.
    /// </summary>
    private readonly AiProviderKind _provider;

    /// <summary>
    ///     The model of the settings, which a failure may name.
    /// </summary>
    private readonly string _modelName;

    /// <summary>
    ///     Receives the metadata lines, or <see langword="null" /> for none.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The token that the caller cancels the turn with.
    /// </summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    ///     The names of the tools called so far in the turn, by the id of the call.
    /// </summary>
    private readonly Dictionary<string, string> _calls = new(StringComparer.Ordinal);

    /// <summary>
    ///     The ids of the calls whose result was already mapped.
    /// </summary>
    private readonly HashSet<string> _finished = new(StringComparer.Ordinal);

    /// <summary>
    ///     The number of text events made.
    /// </summary>
    private int _textEvents;

    /// <summary>
    ///     The number of ids that the mapper had to make because the provider sent none.
    /// </summary>
    private int _madeIds;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentEventMapper" /> class.
    /// </summary>
    /// <param name="provider">The provider that the turn runs on, which decides the wording of a failure.</param>
    /// <param name="modelName">The model of the settings, which a failure may name; may be empty.</param>
    /// <param name="logger">Receives the metadata lines; <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">
    ///     The token that the caller cancels the turn with. An exception after it was canceled is the stop of the turn and
    ///     not a failure.
    /// </param>
    public AgentEventMapper(
        AiProviderKind provider,
        string? modelName = null,
        RollingFileLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        _provider = provider;
        _modelName = modelName?.Trim() ?? string.Empty;
        _logger = logger;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    ///     Maps one update to the events that it holds, in the order of its content.
    /// </summary>
    /// <param name="update">The update.</param>
    /// <returns>
    ///     The events; empty for an update without anything to show. An error in the update ends the list with
    ///     <see cref="AgentEvent.Failed" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the update is <see langword="null" />.</exception>
    public IReadOnlyList<AgentEvent> Map(AgentResponseUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var events = new List<AgentEvent>();
        var text = new StringBuilder();
        foreach (var content in update.Contents)
        {
            if (content is TextContent piece)
            {
                text.Append(piece.Text);
                continue;
            }

            // The text before another kind of content is shown before it
            FlushText(text, events);
            switch (content)
            {
                case FunctionCallContent call:
                    MapCall(call, events);
                    break;
                case FunctionResultContent result:
                    events.Add(MapResult(result));
                    break;
                case UsageContent usage:
                    events.Add(new AgentEvent.Usage(usage.Details?.InputTokenCount, usage.Details?.OutputTokenCount));
                    break;
                case ErrorContent error:
                    events.Add(FailureFromText(error.Message));
                    break;
                default:
                    // Reasoning, pictures outside a tool result, and the like are not shown in the chat
                    break;
            }

            if (events.Count > 0 && events[^1] is AgentEvent.Failed)
            {
                return events;
            }
        }

        FlushText(text, events);
        return events;
    }

    /// <summary>
    ///     Maps the whole stream of a turn. The stream of events ends with <see cref="AgentEvent.Completed" /> when the
    ///     updates end, with <see cref="AgentEvent.Failed" /> when an exception or an error ends them, and without either
    ///     when the turn is canceled.
    /// </summary>
    /// <param name="updates">The updates of the agent.</param>
    /// <returns>The events of the turn.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the updates are <see langword="null" />.</exception>
    public IAsyncEnumerable<AgentEvent> MapAsync(IAsyncEnumerable<AgentResponseUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);

        return MapUpdatesAsync(updates);
    }

    /// <summary>
    ///     Maps the updates of a turn one by one; the iterator behind <see cref="MapAsync" />.
    /// </summary>
    /// <param name="updates">The updates of the agent.</param>
    /// <returns>The events of the turn.</returns>
    private async IAsyncEnumerable<AgentEvent> MapUpdatesAsync(IAsyncEnumerable<AgentResponseUpdate> updates)
    {
        await using var enumerator = updates.GetAsyncEnumerator(_cancellationToken);
        while (true)
        {
            AgentResponseUpdate? current = null;
            AgentEvent.Failed? failure = null;
            var ended = false;
            var faulted = false;
            try
            {
                if (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    current = enumerator.Current;
                }
                else
                {
                    ended = true;
                }
            }
            catch (Exception ex)
            {
                // Every failure of the provider is a result of the turn, never an exception into the chat
                failure = MapFailure(ex);
                faulted = true;
            }

            if (current is not null)
            {
                foreach (var mapped in Map(current))
                {
                    yield return mapped;
                    if (mapped is AgentEvent.Failed erroneous)
                    {
                        Log(erroneous.Kind, "ErrorContent");
                        yield break;
                    }
                }

                continue;
            }

            if (failure is not null)
            {
                yield return failure;
            }
            else if (ended && !faulted && !_cancellationToken.IsCancellationRequested)
            {
                Write(LogLevel.Info, $"Turn completed: provider={_provider} textEvents={_textEvents} toolCalls={_calls.Count}");
                yield return new AgentEvent.Completed();
            }

            yield break;
        }
    }

    /// <summary>
    ///     Turns the exception of a failed turn into a failure.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The failure, or <see langword="null" /> when the turn was canceled, which is not a failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the exception is <see langword="null" />.</exception>
    public AgentEvent.Failed? MapFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (IsCanceled(exception, _cancellationToken))
        {
            Write(LogLevel.Info, $"Turn canceled: provider={_provider}");
            return null;
        }

        var (kind, detail) = Classify(exception);
        Log(kind, exception.GetType().Name);
        return new AgentEvent.Failed(kind, Describe(kind, detail, exception));
    }

    /// <summary>
    ///     Makes the text of an argument or a result short enough to show on one line of the chat.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with each run of white space made one space.</returns>
    private static string Flatten(string text)
    {
        var flat = new StringBuilder(text.Length);
        var space = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                space = flat.Length > 0;
                continue;
            }

            if (space)
            {
                flat.Append(' ');
                space = false;
            }

            flat.Append(character);
        }

        return flat.ToString();
    }

    /// <summary>
    ///     Tells whether an exception is the end of a turn that the caller canceled, or an operation that was canceled.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="cancellationToken">The token of the caller.</param>
    /// <returns><see langword="true" /> when the turn was canceled.</returns>
    private static bool IsCanceled(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return true;
        }

        // A request that timed out is also reported as canceled by the HTTP client; that is a lost connection
        return exception is OperationCanceledException && exception.InnerException is not TimeoutException;
    }

    /// <summary>
    ///     Lists an exception and its inner exceptions, outermost first, to a limited depth.
    /// </summary>
    /// <param name="exception">The outermost exception.</param>
    /// <returns>The exceptions.</returns>
    private static IEnumerable<Exception> Chain(Exception exception)
    {
        var pending = new Queue<(Exception Exception, int Depth)>();
        pending.Enqueue((exception, 0));
        while (pending.Count > 0)
        {
            var (current, depth) = pending.Dequeue();
            yield return current;
            if (depth >= MaxExceptionDepth)
            {
                continue;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    pending.Enqueue((inner, depth + 1));
                }
            }
            else if (current.InnerException is { } next)
            {
                pending.Enqueue((next, depth + 1));
            }
        }
    }

    /// <summary>
    ///     Tells whether a message holds one of the words, ignoring case.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="words">The words.</param>
    /// <returns><see langword="true" /> when the message holds a word.</returns>
    private static bool HasAny(string message, string[] words)
    {
        return words.Any(word => message.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Classifies a message of an exception by its words.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The kind, or <see langword="null" /> when the words do not tell.</returns>
    private static AgentFailureKind? ClassifyText(string message)
    {
        if (HasAny(message, ProtocolWords))
        {
            return AgentFailureKind.ProtocolMismatch;
        }

        if (HasAny(message, AuthenticationWords))
        {
            return AgentFailureKind.Authentication;
        }

        if (message.Contains("model", StringComparison.OrdinalIgnoreCase) && HasAny(message, ModelMissingWords))
        {
            return AgentFailureKind.ModelNotFound;
        }

        return HasAny(message, ConnectionWords) ? AgentFailureKind.ConnectionLost : null;
    }

    /// <summary>
    ///     Tells the kind of a failure from an exception, and a detail that the sentence of the failure may use.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The kind and the detail (the HTTP status of a failed request; empty for none).</returns>
    private static (AgentFailureKind Kind, string Detail) Classify(Exception exception)
    {
        foreach (var current in Chain(exception))
        {
            switch (current)
            {
                case HttpRequestException { StatusCode: HttpStatusCode.NotFound }:
                    return (AgentFailureKind.ModelNotFound, string.Empty);
                case HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden }:
                    return (AgentFailureKind.Authentication, string.Empty);
                case HttpRequestException { StatusCode: { } status }:
                    return (AgentFailureKind.Unexpected, string.Create(CultureInfo.InvariantCulture, $"HTTP {(int)status}"));
                case HttpRequestException or SocketException or IOException or TimeoutException or OperationCanceledException:
                    return (AgentFailureKind.ConnectionLost, string.Empty);
            }

            if (ClassifyText(current.Message) is { } kind)
            {
                return (kind, string.Empty);
            }
        }

        return (AgentFailureKind.Unexpected, string.Empty);
    }

    /// <summary>
    ///     Words a failure for the user.
    /// </summary>
    /// <param name="kind">The kind of the failure.</param>
    /// <param name="detail">The detail that <see cref="Classify" /> found.</param>
    /// <param name="exception">The exception, whose message is used only for a failure of no known kind.</param>
    /// <returns>The sentence, never empty.</returns>
    private string Describe(AgentFailureKind kind, string detail, Exception exception)
    {
        var copilot = _provider == AiProviderKind.GitHubCopilot;
        return kind switch
        {
            AgentFailureKind.Authentication => copilot ? CopilotAuthenticationMessage : OllamaAuthenticationMessage,
            AgentFailureKind.ProtocolMismatch => ProtocolMismatchMessage,
            AgentFailureKind.ModelNotFound => DescribeMissingModel(copilot),
            AgentFailureKind.ConnectionLost => copilot ? CopilotConnectionLostMessage : OllamaConnectionLostMessage,
            _ => DescribeUnexpected(detail, exception),
        };
    }

    /// <summary>
    ///     Words the failure of a model that the provider does not have.
    /// </summary>
    /// <param name="copilot"><see langword="true" /> for GitHub Copilot.</param>
    /// <returns>The sentence.</returns>
    private string DescribeMissingModel(bool copilot)
    {
        if (copilot)
        {
            return _modelName.Length == 0
                ? "GitHub Copilot did not accept the model. Clear the model name in Settings to use the default model, then send the message again."
                : $"The model '{_modelName}' is not available to your Copilot account. Clear the model name in Settings to use the default model, or choose another one.";
        }

        return _modelName.Length == 0
            ? "The Ollama server does not have the model. Choose a model in Settings; `ollama list` shows the models that the server has."
            : $"The model '{_modelName}' was not found on the Ollama server. Run `ollama pull {_modelName}` or choose another model in Settings.";
    }

    /// <summary>
    ///     Words a failure of no known kind.
    /// </summary>
    /// <param name="detail">The HTTP status, or empty.</param>
    /// <param name="exception">The exception.</param>
    /// <returns>The sentence.</returns>
    private static string DescribeUnexpected(string detail, Exception exception)
    {
        var reported = AgentEvent.Summarize(Flatten(exception.Message));
        if (reported.Length == 0)
        {
            reported = exception.GetType().Name;
        }

        return detail.Length == 0
            ? $"The AI provider reported a problem: {reported}"
            : $"The AI provider answered with an error ({detail}): {reported}";
    }

    /// <summary>
    ///     Makes a failure from the text of an error that the provider sent as content.
    /// </summary>
    /// <param name="message">The text of the error; may be empty.</param>
    /// <returns>The failure.</returns>
    private AgentEvent.Failed FailureFromText(string? message)
    {
        var text = message ?? string.Empty;
        var kind = ClassifyText(text) ?? AgentFailureKind.Unexpected;
        return new AgentEvent.Failed(kind, Describe(kind, string.Empty, new InvalidOperationException(text)));
    }

    /// <summary>
    ///     Moves the collected text, when there is any, into an event.
    /// </summary>
    /// <param name="text">The collected text; it is cleared.</param>
    /// <param name="events">The events to add to.</param>
    private void FlushText(StringBuilder text, List<AgentEvent> events)
    {
        if (text.Length == 0)
        {
            return;
        }

        events.Add(new AgentEvent.TextDelta(text.ToString()));
        text.Clear();
        _textEvents++;
    }

    /// <summary>
    ///     Maps the start of a call. A call that was seen already is not reported twice.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="events">The events to add to.</param>
    private void MapCall(FunctionCallContent call, List<AgentEvent> events)
    {
        var id = IdOfCall(call.CallId);
        if (string.IsNullOrEmpty(call.Name) || _calls.ContainsKey(id))
        {
            return;
        }

        _calls[id] = call.Name;
        events.Add(new AgentEvent.ToolStarted(id, call.Name, SummarizeArguments(call.Arguments)));
    }

    /// <summary>
    ///     Maps the end of a call.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The event.</returns>
    private AgentEvent.ToolFinished MapResult(FunctionResultContent result)
    {
        var id = IdOfResult(result.CallId);
        var name = _calls.GetValueOrDefault(id) ?? UnknownToolName;
        _finished.Add(id);

        var (text, hasImage) = ReadResult(result.Result);
        var succeeded = result.Exception is null && !text.StartsWith(RefusalPrefix, StringComparison.Ordinal);
        if (result.Exception is not null)
        {
            text = $"The tool failed: {Flatten(result.Exception.Message)}";
        }
        else if (text.Length == 0 && hasImage)
        {
            text = "A picture.";
        }

        return new AgentEvent.ToolFinished(id, name, succeeded, text, hasImage);
    }

    /// <summary>
    ///     Gives the id of a call that starts, making one when the provider sent none so that the events stay tied together.
    /// </summary>
    /// <param name="callId">The id that the provider sent; may be empty.</param>
    /// <returns>The id, never empty.</returns>
    private string IdOfCall(string? callId)
    {
        if (!string.IsNullOrEmpty(callId))
        {
            return callId;
        }

        _madeIds++;
        return string.Create(CultureInfo.InvariantCulture, $"{MadeIdPrefix}{_madeIds}");
    }

    /// <summary>
    ///     Gives the id of a result. A result without an id belongs to the oldest call with a made id that has no result yet.
    /// </summary>
    /// <param name="callId">The id that the provider sent; may be empty.</param>
    /// <returns>The id, never empty.</returns>
    private string IdOfResult(string? callId)
    {
        if (!string.IsNullOrEmpty(callId))
        {
            return callId;
        }

        return _calls.Keys.FirstOrDefault(key => key.StartsWith(MadeIdPrefix, StringComparison.Ordinal) && !_finished.Contains(key))
            ?? IdOfCall(null);
    }

    /// <summary>
    ///     Describes the arguments of a call on one short line: the name and the value of each, where a long text is shown
    ///     by its length.
    /// </summary>
    /// <param name="arguments">The arguments; may be <see langword="null" />.</param>
    /// <returns>The description.</returns>
    private static string SummarizeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", arguments.Select(pair => $"{pair.Key}={DescribeArgument(pair.Value)}"));
    }

    /// <summary>
    ///     Describes the value of one argument.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value itself when it is short, otherwise its length.</returns>
    private static string DescribeArgument(object? value)
    {
        var text = value switch
        {
            null => "null",
            string plain => plain,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
            JsonElement element => element.GetRawText(),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        var flat = Flatten(text);
        return flat.Length <= MaxArgumentTextLength
            ? flat
            : string.Create(CultureInfo.InvariantCulture, $"({text.Length:N0} characters)");
    }

    /// <summary>
    ///     Reads the text and the pictures of the result of a tool, whatever shape the AI library gave it.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The text on one line, and whether a picture was part of it.</returns>
    private static (string Text, bool HasImage) ReadResult(object? result)
    {
        var pieces = new List<string>();
        var hasImage = false;
        Collect(result, pieces, ref hasImage, 0);
        return (Flatten(string.Join(' ', pieces.Where(piece => piece.Length > 0))), hasImage);
    }

    /// <summary>
    ///     Collects the text and finds the pictures of a result.
    /// </summary>
    /// <param name="result">The result or a part of it.</param>
    /// <param name="pieces">The pieces of text found.</param>
    /// <param name="hasImage">Set when a picture is found.</param>
    /// <param name="depth">The depth of the part in the result.</param>
    private static void Collect(object? result, List<string> pieces, ref bool hasImage, int depth)
    {
        switch (result)
        {
            case null:
                return;
            case string text:
                pieces.Add(text);
                return;
            case TextContent content:
                pieces.Add(content.Text);
                return;
            case DataContent data:
                hasImage |= data.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                return;
            case UriContent uri:
                hasImage |= uri.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                return;
            case JsonElement { ValueKind: JsonValueKind.String } element:
                pieces.Add(element.GetString() ?? string.Empty);
                return;
            case JsonElement element:
                pieces.Add(element.GetRawText());
                return;
            case System.Collections.IEnumerable parts when depth < 2:
                foreach (var part in parts)
                {
                    Collect(part, pieces, ref hasImage, depth + 1);
                }

                return;
            default:
                pieces.Add(Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty);
                return;
        }
    }

    /// <summary>
    ///     Writes the metadata line of a failure.
    /// </summary>
    /// <param name="kind">The kind of the failure.</param>
    /// <param name="exceptionType">The type of the exception, or a name for the source of the failure.</param>
    private void Log(AgentFailureKind kind, string exceptionType)
    {
        Write(LogLevel.Warning, $"Turn failed: provider={_provider} kind={kind} source={exceptionType}");
    }

    /// <summary>
    ///     Writes a line of the log, if there is a log. A log that cannot be written never fails the turn.
    /// </summary>
    /// <param name="level">The level of the line.</param>
    /// <param name="message">The line; it holds metadata only.</param>
    private void Write(LogLevel level, string message)
    {
        if (_logger is null)
        {
            return;
        }

        try
        {
            _logger.Log(level, message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The turn has happened; a log that cannot be written is not a reason to fail it
        }
    }
}

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
using OllamaSharp;
using OllamaSharp.Models;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The <see cref="IOllamaProbe" /> that asks a real Ollama server, through the OllamaSharp client: the list of models
///     from <c>/api/tags</c> and the context length of a model from <c>/api/show</c>.
/// </summary>
/// <remarks>
///     Every failure of the conversation (the connection is refused, the time runs out, the answer is not what Ollama
///     sends) is data: an unreachable server for the list and an unknown length for the context. Only a cancellation by
///     the caller is an exception. Nothing the server sends is logged or kept. The probe holds no state and is safe for
///     concurrent use.
/// </remarks>
public sealed class OllamaProbe : IOllamaProbe
{
    /// <summary>The ending of the key of the context length in the model information, after the name of the model family.</summary>
    private const string ContextLengthKeySuffix = ".context_length";

    /// <summary>The handler that carries the requests; <see langword="null" /> for the real network.</summary>
    private readonly HttpMessageHandler? _handler;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OllamaProbe" /> class.
    /// </summary>
    /// <param name="handler">
    ///     The handler that carries the requests, which lets a test stand in for the server; the real network when
    ///     <see langword="null" />. The probe never disposes it.
    /// </param>
    public OllamaProbe(HttpMessageHandler? handler = null)
    {
        _handler = handler;
    }

    /// <inheritdoc />
    public async Task<OllamaModelList> ListModelsAsync(Uri endpoint, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            using var http = CreateHttpClient(endpoint);
            var client = new OllamaApiClient(http);
            var models = await client.ListLocalModelsAsync(limit.Token).ConfigureAwait(false);
            return new OllamaModelList(true, [.. models.Select(model => model.Name).Where(name => !string.IsNullOrWhiteSpace(name))]);
        }
        catch (Exception ex) when (IsProbeFailure(ex, cancellationToken))
        {
            // The server is not there, is too slow, or does not speak the protocol of Ollama
            return OllamaModelList.Unreachable;
        }
    }

    /// <inheritdoc />
    public async Task<int?> GetContextLengthAsync(
        Uri endpoint,
        string model,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            using var http = CreateHttpClient(endpoint);
            var client = new OllamaApiClient(http);
            var response = await client.ShowModelAsync(new ShowModelRequest { Model = model }, limit.Token).ConfigureAwait(false);
            return ReadContextLength(response?.Info?.ExtraInfo);
        }
        catch (Exception ex) when (IsProbeFailure(ex, cancellationToken))
        {
            // The answer is optional, so a failure only means that the length is not known
            return null;
        }
    }

    /// <summary>
    ///     Reads the context length from the extra model information, where it is the entry whose key ends in
    ///     <c>.context_length</c> (the key starts with the family of the model, such as <c>llama.context_length</c>).
    /// </summary>
    /// <param name="information">The extra information; may be <see langword="null" />.</param>
    /// <returns>The length in tokens, or <see langword="null" /> when there is no usable entry.</returns>
    private static int? ReadContextLength(IDictionary<string, object>? information)
    {
        if (information is null)
        {
            return null;
        }

        foreach (var (key, value) in information)
        {
            if (!key.EndsWith(ContextLengthKeySuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var length = ToLength(value);
            if (length is > 0)
            {
                return (int)Math.Min(length.Value, int.MaxValue);
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads a number from a value of the extra information, which the client hands over as the JSON that came on the
    ///     wire or as a number that it has already read.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The number, or <see langword="null" /> when the value is not a whole number.</returns>
    private static long? ToLength(object? value)
    {
        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt64(out var number) => number,
            int number => number,
            long number => number,
            _ => null,
        };
    }

    /// <summary>
    ///     Tells whether an exception is a failure of the conversation with the server, which the probe turns into data,
    ///     and not a cancellation by the caller, which it passes on.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="cancellationToken">The token of the caller.</param>
    /// <returns><see langword="true" /> for a failure of the conversation.</returns>
    private static bool IsProbeFailure(Exception exception, CancellationToken cancellationToken)
    {
        return exception switch
        {
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            HttpRequestException or JsonException or InvalidOperationException or NotSupportedException => true,
            _ => false,
        };
    }

    /// <summary>
    ///     Makes the HTTP client for one request to a server.
    /// </summary>
    /// <param name="endpoint">The address of the server.</param>
    /// <returns>The client; the caller disposes it.</returns>
    private HttpClient CreateHttpClient(Uri endpoint)
    {
        var http = _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
        http.BaseAddress = endpoint;
        return http;
    }
}

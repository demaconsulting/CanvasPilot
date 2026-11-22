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

using DemaConsulting.AgentKit.Agents.Ollama;
using DemaConsulting.AgentKit.Core;
using OllamaSharp;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Gives the catalog of the models of the Ollama server at an address. The interface is the seam that lets the
///     readiness check, the factory of agents, and the model list be tested without a server.
/// </summary>
public interface IOllamaModelCatalogs
{
    /// <summary>
    ///     Gets the catalog of the server at the address.
    /// </summary>
    /// <param name="endpoint">The address of the server.</param>
    /// <returns>
    ///     The catalog, which caches what the server said for a few minutes and fails with the exception of the network
    ///     (<see cref="HttpRequestException" />) when the server cannot be asked.
    /// </returns>
    IModelCatalog For(Uri endpoint);
}

/// <summary>
///     The <see cref="IOllamaModelCatalogs" /> over the real network: one <see cref="OllamaModelCatalog" /> for the address
///     that was asked for last, so that the readiness check, the factory, and the settings page share what the server said
///     instead of describing every model again.
/// </summary>
/// <remarks>
///     The HTTP client has no time limit of its own, because the callers bound each request with their own token (a
///     list is short, a model that is loading is not). A request for another address replaces the catalog; the old HTTP
///     client is left to the finalizer so that a request that is still running on it is not cut off. Safe for concurrent
///     use.
/// </remarks>
public sealed class OllamaModelCatalogs : IOllamaModelCatalogs
{
    /// <summary>
    ///     Guards the catalog of the last address.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    ///     The handler that carries the requests; <see langword="null" /> for the real network.
    /// </summary>
    private readonly HttpMessageHandler? _handler;

    /// <summary>
    ///     The address of the catalog that was made last, in the form that compares equal for the same server.
    /// </summary>
    private string? _key;

    /// <summary>
    ///     The catalog that was made last.
    /// </summary>
    private IModelCatalog? _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OllamaModelCatalogs" /> class.
    /// </summary>
    /// <param name="handler">
    ///     The handler that carries the requests, which lets a test stand in for the server; the real network when
    ///     <see langword="null" />. It is never disposed here.
    /// </param>
    public OllamaModelCatalogs(HttpMessageHandler? handler = null)
    {
        _handler = handler;
    }

    /// <inheritdoc />
    public IModelCatalog For(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var key = endpoint.AbsoluteUri;
        lock (_gate)
        {
            if (_catalog is not null && string.Equals(_key, key, StringComparison.Ordinal))
            {
                return _catalog;
            }

            var http = _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
            http.BaseAddress = endpoint;
            http.Timeout = Timeout.InfiniteTimeSpan;
            _catalog = new OllamaModelCatalog(new OllamaApiClient(http));
            _key = key;
            return _catalog;
        }
    }
}

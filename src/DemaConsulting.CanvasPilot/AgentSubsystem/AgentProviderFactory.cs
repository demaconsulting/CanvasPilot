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
using DemaConsulting.AgentKit.Agents.ChatClient;
using DemaConsulting.AgentKit.Agents.Copilot;
using DemaConsulting.AgentKit.Agents.Ollama;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Agents.AI;
using OllamaSharp;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Makes the agent of a conversation for the provider that the settings choose: Ollama through an
///     <see cref="OllamaApiClient" /> that is told the context window on every request, or GitHub Copilot through the
///     starter of the Copilot runtime.
/// </summary>
/// <remarks>
///     <para>
///         For Ollama the factory builds an HTTP client on the address of the settings (without a time limit, because a
///         local model may take minutes to load and the Stop button is what ends a turn), an Ollama client for the model,
///         and an <see cref="OllamaContextSizingChatClient" /> around it so that <c>num_ctx</c> of the settings is sent on
///         every request. The agent is made by <see cref="ChatClientAgentFactory" />, which adds the decorator that lets
///         the model see pictures that a tool returned. The result owns the clients and disposes them with the agent.
///     </para>
///     <para>
///         For GitHub Copilot the factory finds the Copilot CLI with the locator, asks the starter of the runtime to start
///         the runtime on it once, and makes agents on that runtime with the model of the settings (the default model of
///         the runtime when the name is empty), the project folder as the working directory, and the default-deny
///         permission handler. The starter of this build is the gated one, which starts nothing, so the factory returns
///         the typed answer <see cref="AgentReadinessStatus.CopilotRuntimeUnavailable" /> and no agent. The factory keeps
///         the started runtime for later agents and disposes it when it is disposed.
///     </para>
///     <para>
///         Settings that do not describe a usable provider (a missing model, an invalid address) are answered with
///         <see cref="AgentReadinessStatus.NotConfigured" />. The log gets metadata only: the provider, the model name, the
///         number of tools, and the context window; never the instructions. The factory is safe for concurrent use.
///     </para>
/// </remarks>
public sealed class AgentProviderFactory : IAgentFactory
{
    /// <summary>
    ///     The name that the agent of CanvasPilot carries.
    /// </summary>
    public const string AgentName = "CanvasPilot";

    /// <summary>
    ///     The sentence for settings of Ollama without an address that can be used.
    /// </summary>
    public const string InvalidEndpointMessage =
        "Enter the address of the Ollama server in Settings, for example http://localhost:11434.";

    /// <summary>
    ///     The sentence for settings of Ollama without a model.
    /// </summary>
    public const string MissingModelMessage = "Enter the name of an Ollama model in Settings, for example gemma3.";

    /// <summary>
    ///     The sentence for a context window that is out of range.
    /// </summary>
    public const string InvalidContextWindowMessage = "Enter a context window of the Ollama model in Settings between {0} and {1} tokens.";

    /// <summary>
    ///     The sentence for a runtime that started but gave no way to make agents.
    /// </summary>
    public const string NoAgentSourceMessage = "The Copilot runtime started but cannot make an agent. Choose Check connection, or use Ollama meanwhile.";

    /// <summary>
    ///     Finds the Copilot CLI.
    /// </summary>
    private readonly CopilotCliLocator _locator;

    /// <summary>
    ///     Starts the Copilot runtime.
    /// </summary>
    private readonly ICopilotRuntimeStarter _starter;

    /// <summary>
    ///     The handler that carries the requests to Ollama; <see langword="null" /> for the real network.
    /// </summary>
    private readonly HttpMessageHandler? _handler;

    /// <summary>
    ///     Receives the metadata lines, or <see langword="null" /> for none.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Serializes the start of the runtime.
    /// </summary>
    private readonly SemaphoreSlim _startGate = new(1, 1);

    /// <summary>
    ///     The started Copilot runtime, or <see langword="null" /> when none was started.
    /// </summary>
    private ICopilotAgentSource? _copilot;

    /// <summary>
    ///     Whether the factory was disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentProviderFactory" /> class.
    /// </summary>
    /// <param name="locator">Finds the Copilot CLI; a locator on the real system when <see langword="null" />.</param>
    /// <param name="starter">The starter of the Copilot runtime; the gated starter when <see langword="null" />.</param>
    /// <param name="handler">
    ///     The handler that carries the requests to Ollama, which lets a test stand in for the server; the real network when
    ///     <see langword="null" />. The factory never disposes it.
    /// </param>
    /// <param name="logger">Receives the metadata lines; <see langword="null" /> for none.</param>
    public AgentProviderFactory(
        CopilotCliLocator? locator = null,
        ICopilotRuntimeStarter? starter = null,
        HttpMessageHandler? handler = null,
        RollingFileLogger? logger = null)
    {
        _locator = locator ?? new CopilotCliLocator();
        _starter = starter ?? new GatedCopilotRuntimeStarter();
        _handler = handler;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<AgentCreationResult> CreateAsync(AgentCreationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Settings);
        ArgumentNullException.ThrowIfNull(request.Tools);
        ArgumentNullException.ThrowIfNull(request.Instructions);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectFolder);

        return request.Settings.Provider == AiProviderKind.Ollama
            ? Task.FromResult(CreateOllama(request))
            : CreateCopilotAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Only a Copilot session leaves anything behind (in the data folder of the runtime); the id exists once a run began
        if (session is not CopilotAgentSession copilot || string.IsNullOrEmpty(copilot.SessionId))
        {
            return;
        }

        ICopilotAgentSource? source;
        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            source = _copilot;
        }
        finally
        {
            _startGate.Release();
        }

        if (source is not null)
        {
            await source.DeleteSessionAsync(copilot.SessionId, cancellationToken).ConfigureAwait(false);
            Write("Copilot session deleted");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        ICopilotAgentSource? source;
        await _startGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _disposed = true;
            source = _copilot;
            _copilot = null;
        }
        finally
        {
            _startGate.Release();
        }

        if (source is not null)
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Makes the Ollama agent.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The agent, or the reason that settings cannot be used.</returns>
    private AgentCreationResult CreateOllama(AgentCreationRequest request)
    {
        var settings = request.Settings;
        var endpoint = settings.OllamaEndpoint?.Trim();
        var model = settings.ModelName?.Trim() ?? string.Empty;

        if (!AiProviderSettings.IsValidOllamaEndpoint(endpoint))
        {
            return NotConfigured(InvalidEndpointMessage);
        }

        if (model.Length == 0)
        {
            return NotConfigured(MissingModelMessage);
        }

        if (!AiProviderSettings.IsValidContextWindow(settings.ContextWindowTokens))
        {
            return NotConfigured(string.Format(
                CultureInfo.CurrentCulture,
                InvalidContextWindowMessage,
                AiProviderSettings.MinContextWindowTokens.ToString("N0", CultureInfo.CurrentCulture),
                AiProviderSettings.MaxContextWindowTokens.ToString("N0", CultureInfo.CurrentCulture)));
        }

        var http = _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
        OllamaContextSizingChatClient? sized = null;
        try
        {
            http.BaseAddress = new Uri(endpoint!, UriKind.Absolute);
            http.Timeout = Timeout.InfiniteTimeSpan;
            sized = new OllamaContextSizingChatClient(new OllamaApiClient(http, model), settings.ContextWindowTokens);
            var agent = ChatClientAgentFactory.Create(sized, [.. request.Tools], request.Instructions, AgentName);

            Write($"Agent created: provider=Ollama model={model} tools={request.Tools.Count} contextTokens={settings.ContextWindowTokens}");
            return AgentCreationResult.Created(agent, new OwnedResources(sized, http));
        }
        catch
        {
            // Nothing owns the clients yet, so they are released before the exception goes on
            sized?.Dispose();
            http.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Makes the Copilot agent, or says why that is not possible.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token that cancels the search and the start.</param>
    /// <returns>The agent, or the reason that none could be made.</returns>
    private async Task<AgentCreationResult> CreateCopilotAsync(AgentCreationRequest request, CancellationToken cancellationToken)
    {
        var settings = request.Settings;
        var cli = await _locator.LocateAsync(settings.CopilotCliPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!cli.IsFound || cli.Path is null || cli.Version is null)
        {
            var status = cli.Status == CopilotCliStatus.TooOld ? AgentReadinessStatus.CopilotCliTooOld : AgentReadinessStatus.CopilotCliMissing;
            return Unavailable(status, cli.Message);
        }

        var source = await GetCopilotSourceAsync(
                new CopilotStartRequest(cli.Path, cli.Version, CheckOnly: false)
                {
                    DataDirectory = request.CopilotDataDirectory,
                    Logger = request.CopilotLogger,
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (source.Agents is null)
        {
            return Unavailable(AgentReadinessStatus.CopilotRuntimeUnavailable, source.Message);
        }

        var model = string.IsNullOrWhiteSpace(settings.ModelName) ? null : settings.ModelName.Trim();
        var agent = source.Agents.CreateAgent(new CopilotAgentRequest(request.Tools, request.Instructions, AgentName, model, request.ProjectFolder));
        Write($"Agent created: provider=GitHubCopilot model={model ?? "(default)"} tools={request.Tools.Count}");

        // The runtime belongs to the factory and is shared by the agents, so the result owns nothing
        return AgentCreationResult.Created(agent);
    }

    /// <summary>
    ///     Gives the started Copilot runtime, starting it when it is not running yet.
    /// </summary>
    /// <param name="start">The request for the starter.</param>
    /// <param name="cancellationToken">A token that cancels the start.</param>
    /// <returns>The runtime as a source of agents, or the message of the starter when it could not be started.</returns>
    private async Task<(ICopilotAgentSource? Agents, string Message)> GetCopilotSourceAsync(
        CopilotStartRequest start,
        CancellationToken cancellationToken)
    {
        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_copilot is not null)
            {
                return (_copilot, string.Empty);
            }

            var started = await _starter.StartAsync(start, cancellationToken).ConfigureAwait(false);
            if (started.Status != CopilotStartStatus.Started)
            {
                return (null, started.Message);
            }

            if (started.Agents is null)
            {
                return (null, NoAgentSourceMessage);
            }

            _copilot = started.Agents;
            return (_copilot, string.Empty);
        }
        finally
        {
            _startGate.Release();
        }
    }

    /// <summary>
    ///     Makes the answer for settings that cannot be used.
    /// </summary>
    /// <param name="message">The sentence that says what to enter.</param>
    /// <returns>The result.</returns>
    private static AgentCreationResult NotConfigured(string message)
    {
        return AgentCreationResult.Unavailable(new AgentReadinessReport(AgentReadinessStatus.NotConfigured, message));
    }

    /// <summary>
    ///     Makes the answer for a provider that cannot start, and logs it.
    /// </summary>
    /// <param name="status">The status of the readiness check for the same cause.</param>
    /// <param name="message">The sentence for the user.</param>
    /// <returns>The result.</returns>
    private AgentCreationResult Unavailable(AgentReadinessStatus status, string message)
    {
        Write($"Agent not created: provider=GitHubCopilot status={status}");
        return AgentCreationResult.Unavailable(new AgentReadinessReport(status, message));
    }

    /// <summary>
    ///     Writes a line of the log, if there is a log.
    /// </summary>
    /// <param name="message">The line; it holds metadata only.</param>
    private void Write(string message)
    {
        if (_logger is null)
        {
            return;
        }

        try
        {
            _logger.Log(LogLevel.Info, message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A log that cannot be written is not a reason to refuse an agent
        }
    }

    /// <summary>
    ///     The clients of one Ollama agent, disposed together.
    /// </summary>
    private sealed class OwnedResources : IAsyncDisposable
    {
        /// <summary>The chat client that wraps the Ollama client; disposing it disposes the Ollama client.</summary>
        private readonly OllamaContextSizingChatClient _client;

        /// <summary>The HTTP client of the Ollama client.</summary>
        private readonly HttpClient _http;

        /// <summary>
        ///     Initializes a new instance of the <see cref="OwnedResources" /> class.
        /// </summary>
        /// <param name="client">The chat client.</param>
        /// <param name="http">The HTTP client.</param>
        internal OwnedResources(OllamaContextSizingChatClient client, HttpClient http)
        {
            _client = client;
            _http = http;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _client.Dispose();
            _http.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

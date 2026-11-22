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
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Lists the models of the AI provider that the settings choose, with what each can do, in the types of CanvasPilot.
///     It is the implementation of <see cref="IProviderModelSource" /> that the settings page uses.
/// </summary>
/// <remarks>
///     <para>
///         For GitHub Copilot the catalog is the one of the shared runtime: the Copilot CLI is located (the setting of the
///         user first), the starter starts or reuses the one runtime, and the catalog of that runtime lists the models the
///         signed-in user may use. No second runtime is ever started for the list. For Ollama the catalog asks the server
///         at the address of the settings (<c>/api/tags</c> and <c>/api/show</c>) through <see cref="IOllamaModelCatalogs" />,
///         with a time limit, because a settings page must not wait on a server that is not there.
///     </para>
///     <para>
///         Every model the provider lists is returned, usable or not, with its capabilities: whether it calls tools (which
///         CanvasPilot needs), takes pictures, reasons, and the efforts it offers. Deciding what a missing capability means is
///         left to the page. A provider that cannot be asked (no CLI, not signed in, a server that does not answer) is a
///         list with no models and one sentence, never an exception, so that the page can offer typing the name. Only a
///         cancellation by the caller is an exception. The class holds no state of its own and is safe for concurrent use.
///     </para>
/// </remarks>
public sealed class ProviderModelCatalog : IProviderModelSource
{
    /// <summary>
    ///     The longest time that the Ollama server may take to list and describe its models.
    /// </summary>
    public static readonly TimeSpan OllamaListTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     The sentence for an Ollama server that has no model.
    /// </summary>
    public const string NoOllamaModelsMessage =
        "Ollama has no models yet; run `ollama pull <model>` for a model that can call tools, then choose Refresh models.";

    /// <summary>
    ///     The sentence for settings of Ollama without an address that can be used; the factory of the agents answers with it
    ///     as well.
    /// </summary>
    public const string InvalidEndpointMessage =
        "Enter the address of the Ollama server in Settings, for example http://localhost:11434.";

    /// <summary>
    ///     The sentence for a runtime that started but gave no way to make agents; the factory of the agents answers with it
    ///     as well.
    /// </summary>
    public const string NoAgentSourceMessage = "The Copilot runtime started but cannot make an agent. Choose Check connection to try again.";

    /// <summary>
    ///     Finds the Copilot CLI.
    /// </summary>
    private readonly CopilotCliLocator _locator;

    /// <summary>
    ///     Starts, or reuses, the Copilot runtime.
    /// </summary>
    private readonly ICopilotRuntimeStarter _starter;

    /// <summary>
    ///     Gives the catalog of an Ollama server.
    /// </summary>
    private readonly IOllamaModelCatalogs _ollama;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProviderModelCatalog" /> class.
    /// </summary>
    /// <param name="locator">Finds the Copilot CLI; a locator on the real system when <see langword="null" />.</param>
    /// <param name="starter">
    ///     The starter of the Copilot runtime, which must be the one that the readiness check and the factory of agents use,
    ///     so that one runtime serves all; a starter of the real runtime when <see langword="null" />.
    /// </param>
    /// <param name="ollama">Gives the catalog of the Ollama server; the real network when <see langword="null" />.</param>
    public ProviderModelCatalog(
        CopilotCliLocator? locator = null,
        ICopilotRuntimeStarter? starter = null,
        IOllamaModelCatalogs? ollama = null)
    {
        _locator = locator ?? new CopilotCliLocator();
        _starter = starter ?? new CopilotRuntimeStarter();
        _ollama = ollama ?? new OllamaModelCatalogs();
    }

    /// <inheritdoc />
    public Task<ProviderModelList> ListModelsAsync(
        AiProviderSettings settings,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.Provider == AiProviderKind.Ollama
            ? ListOllamaAsync(settings, refresh, cancellationToken)
            : ListCopilotAsync(settings, refresh, cancellationToken);
    }

    /// <summary>
    ///     Maps a model of the AI library to the model of the settings.
    /// </summary>
    /// <param name="model">The model that a catalog described.</param>
    /// <returns>The same facts in the types of CanvasPilot.</returns>
    public static ProviderModelInfo Map(ModelInfo model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // The efforts are offered only when the model says that the effort can be adjusted
        var efforts = model.Adjustments.HasFlag(ModelAdjustments.ReasoningEffort)
            ? model.ReasoningEfforts.Select(effort => new ProviderEffortChoice(effort.Id, effort.DisplayName)).ToList()
            : [];

        // Notes that ask for attention travel with the model; the plain information of a catalog is not shown
        var warnings = model.Notices
            .Where(notice => notice.Kind != ModelNoticeKind.Info && !string.IsNullOrWhiteSpace(notice.Message))
            .Select(notice => notice.Message)
            .ToList();

        return new ProviderModelInfo(
            model.Id,
            string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName,
            model.Supports(ModelCapabilities.ToolCalls),
            model.Supports(ModelCapabilities.Vision),
            model.Supports(ModelCapabilities.Reasoning),
            model.ContextWindowTokens,
            efforts,
            model.DefaultReasoningEffort,
            model.IsUsable,
            model.UnusableReason,
            warnings);
    }

    /// <summary>
    ///     Lists the models of GitHub Copilot through the shared runtime.
    /// </summary>
    /// <param name="settings">The provider choices.</param>
    /// <param name="refresh">Whether to ask the CLI and the service again.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The models, or no models and the sentence that says why.</returns>
    private async Task<ProviderModelList> ListCopilotAsync(AiProviderSettings settings, bool refresh, CancellationToken cancellationToken)
    {
        // The CLI must be there and be new enough, as for the agents
        var cli = await _locator.LocateAsync(settings.CopilotCliPath, refresh, cancellationToken).ConfigureAwait(false);
        if (!cli.IsFound || cli.Path is null || cli.Version is null)
        {
            return ProviderModelList.Failed(cli.Message);
        }

        // The one shared runtime; a runtime that cannot start gives its own sentence
        var started = await _starter.StartAsync(new CopilotStartRequest(cli.Path, cli.Version), cancellationToken).ConfigureAwait(false);
        if (started.Status != CopilotStartStatus.Started)
        {
            return ProviderModelList.Failed(started.Message);
        }

        if (started.Agents is null)
        {
            return ProviderModelList.Failed(NoAgentSourceMessage);
        }

        try
        {
            if (refresh)
            {
                await started.Agents.Models.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }

            var models = await started.Agents.Models.ListAsync(new ModelQuery { IncludeUnusable = true }, cancellationToken).ConfigureAwait(false);
            return Sorted(models);
        }
        catch (ObjectDisposedException)
        {
            // The runtime was replaced while the list was read, for example by a changed CLI path
            return ProviderModelList.Failed("The Copilot runtime was stopped while the models were listed; choose Refresh models.");
        }
        catch (InvalidOperationException ex)
        {
            // The library words the cause, such as a runtime that is not signed in
            return ProviderModelList.Failed(ex.Message);
        }
    }

    /// <summary>
    ///     Lists the models of the Ollama server.
    /// </summary>
    /// <param name="settings">The provider choices.</param>
    /// <param name="refresh">Whether to describe the models again.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The models, or no models and the sentence that says why.</returns>
    private async Task<ProviderModelList> ListOllamaAsync(AiProviderSettings settings, bool refresh, CancellationToken cancellationToken)
    {
        if (!AiProviderSettings.IsValidOllamaEndpoint(settings.OllamaEndpoint))
        {
            return ProviderModelList.Failed(InvalidEndpointMessage);
        }

        var address = settings.OllamaEndpoint.Trim();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(OllamaListTimeout);
        try
        {
            var catalog = _ollama.For(new Uri(address));
            if (refresh)
            {
                await catalog.RefreshAsync(limit.Token).ConfigureAwait(false);
            }

            var models = await catalog.ListAsync(new ModelQuery { IncludeUnusable = true }, limit.Token).ConfigureAwait(false);
            return models.Count == 0 ? ProviderModelList.Failed(NoOllamaModelsMessage) : Sorted(models);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or NotSupportedException
                                       || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The server is not there, is too slow, or does not speak the protocol of Ollama
            return ProviderModelList.Failed(
                $"Ollama did not answer at {address}; start Ollama or fix the address, then choose Refresh models. You can still type the name of a model.");
        }
    }

    /// <summary>
    ///     Maps the models and puts the ones that can be used first, each group by name, so that the list reads well.
    /// </summary>
    /// <param name="models">The models of a catalog.</param>
    /// <returns>The list without a message.</returns>
    private static ProviderModelList Sorted(IReadOnlyList<ModelInfo> models)
    {
        var mapped = models
            .Select(Map)
            .OrderBy(model => model.IsUsable ? 0 : 1)
            .ThenBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new ProviderModelList(mapped, string.Empty);
    }
}

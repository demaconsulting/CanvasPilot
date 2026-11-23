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
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Checks whether the AI provider that the settings choose can be used, and says in one sentence what was found and
///     what to do about it.
/// </summary>
/// <remarks>
///     <para>
///         For GitHub Copilot the checks run in order and the first one that fails gives the answer: the Copilot CLI is
///         found (by <see cref="CopilotCliLocator" />) and is new enough, the starter of the runtime starts the runtime, or
///         reuses the one that runs, on that CLI (the way that start fails is the answer, including a runtime that is not
///         signed in), and the model settings are valid for the runtime (the model is offered to the user, can call tools,
///         and offers the reasoning effort that is chosen). A model that cannot take pictures is a warning that does not
///         stop the provider. The check looks for no credentials of its own: the runtime signs in from the first source
///         that applies (a token variable, the sign-in of the Copilot CLI, or the GitHub CLI), so only the start of the
///         runtime can tell whether a sign-in works, and the check passes on what the starter says. When the runtime
///         runs, the sentence of a ready provider names the account and the kind of sign-in that the runtime reports (the
///         login, the host, and the source, never a token), so that the user sees which account the agents use. The
///         runtime is the one that the factory of the agents uses, so the check starts no second runtime.
///     </para>
///     <para>
///         For Ollama the checks are: the address and the model name are set, the server answers, the server has the model,
///         the model settings are valid for the model (it can call tools, the context window does not exceed what it was
///         trained for, and it offers the reasoning effort that is chosen), and the context window is large enough. A window
///         below <see cref="AiProviderSettings.RecommendedMinimumContextTokens" /> (the smaller of the configured window and
///         the one that the model supports, when the server says) and a model that cannot take pictures are warnings that
///         do not stop the provider. When the server answers the list of models but cannot describe them, the model
///         settings are not validated and the first turn is what finds the problem.
///     </para>
///     <para>
///         The class implements <see cref="IProviderCheck" /> of the Settings subsystem, so the settings dialog can offer
///         Check connection without depending on this subsystem. It writes no log. It holds no state besides the cache of
///         the locator and is safe for concurrent use.
///     </para>
/// </remarks>
public sealed class AgentReadiness : IProviderCheck
{
    /// <summary>The longest time that a question to the Ollama server may take.</summary>
    public static readonly TimeSpan OllamaProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The sentence for a Copilot that is not signed in, which says to run <c>copilot</c> and type <c>/login</c>.</summary>
    public const string NotSignedInMessage = CopilotRuntimeStarter.NotSignedInMessage;

    /// <summary>Finds and checks the Copilot CLI.</summary>
    private readonly CopilotCliLocator _locator;

    /// <summary>Starts, or reuses, the Copilot runtime.</summary>
    private readonly ICopilotRuntimeStarter _starter;

    /// <summary>Asks the Ollama server.</summary>
    private readonly IOllamaProbe _ollama;

    /// <summary>Gives the catalog of the Ollama server, which describes what each model can do.</summary>
    private readonly IOllamaModelCatalogs _ollamaCatalogs;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentReadiness" /> class.
    /// </summary>
    /// <param name="locator">Finds the Copilot CLI; a locator on the real system when <see langword="null" />.</param>
    /// <param name="starter">The starter of the Copilot runtime; a starter of the real runtime when <see langword="null" />.</param>
    /// <param name="ollama">Asks the Ollama server; the real probe when <see langword="null" />.</param>
    /// <param name="ollamaCatalogs">Gives the catalog of the Ollama server; the real network when <see langword="null" />.</param>
    public AgentReadiness(
        CopilotCliLocator? locator = null,
        ICopilotRuntimeStarter? starter = null,
        IOllamaProbe? ollama = null,
        IOllamaModelCatalogs? ollamaCatalogs = null)
    {
        _locator = locator ?? new CopilotCliLocator();
        _starter = starter ?? new CopilotRuntimeStarter();
        _ollama = ollama ?? new OllamaProbe();
        _ollamaCatalogs = ollamaCatalogs ?? new OllamaModelCatalogs();
    }

    /// <summary>
    ///     Checks the provider that the settings choose.
    /// </summary>
    /// <param name="settings">The provider choices to check, which may not have been saved yet.</param>
    /// <param name="refresh">
    ///     <see langword="true" /> to search for the Copilot CLI again even when the answer is cached (the connection
    ///     check).
    /// </param>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>The report; a provider that cannot be used is a report, not an exception.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    public Task<AgentReadinessReport> GetReportAsync(
        AiProviderSettings settings,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.Provider == AiProviderKind.Ollama
            ? CheckOllamaAsync(settings, cancellationToken)
            : CheckCopilotAsync(settings, refresh, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProviderCheckResult> CheckAsync(AiProviderSettings settings, CancellationToken cancellationToken = default)
    {
        // Check connection always looks again, so a fix that the user just made is seen
        var report = await GetReportAsync(settings, refresh: true, cancellationToken).ConfigureAwait(false);
        return new ProviderCheckResult(report.IsUsable, report.Message);
    }

    /// <summary>
    ///     Builds the message of a context window that is too small.
    /// </summary>
    /// <param name="configured">The context window of the settings, in tokens.</param>
    /// <param name="detected">The largest window that the model supports; <see langword="null" /> when not known.</param>
    /// <returns>
    ///     The sentence, or <see langword="null" /> when the smaller of the two is at least
    ///     <see cref="AiProviderSettings.RecommendedMinimumContextTokens" />.
    /// </returns>
    public static string? DescribeSmallContext(int configured, int? detected)
    {
        var minimum = AiProviderSettings.RecommendedMinimumContextTokens;
        var minimumText = minimum.ToString("N0", CultureInfo.InvariantCulture);

        // The model's own limit wins when it is the smaller one, because raising the setting cannot help then
        if (detected is { } supported && supported < configured)
        {
            return supported < minimum
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"The model supports only {supported:N0} tokens, below {minimumText}; drawings with images and tools may fail, so choose a model with a larger context window.")
                : null;
        }

        return configured < minimum
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"The context window ({configured:N0} tokens) is below {minimumText}; drawings with images and tools may fail. Raise it in Settings ({AiProviderSettings.DefaultContextWindowTokens:N0} recommended).")
            : null;
    }

    /// <summary>
    ///     Runs the ordered checks of GitHub Copilot.
    /// </summary>
    /// <param name="settings">The provider choices.</param>
    /// <param name="refresh">Whether to search for the CLI again.</param>
    /// <param name="cancellationToken">A token that cancels the checks.</param>
    /// <returns>The report of the first check that fails, or of the starter.</returns>
    private async Task<AgentReadinessReport> CheckCopilotAsync(
        AiProviderSettings settings,
        bool refresh,
        CancellationToken cancellationToken)
    {
        // 1 and 2: the CLI is there and is new enough; the locator checks both and words what it found
        var info = await _locator.LocateAsync(settings.CopilotCliPath, refresh, cancellationToken).ConfigureAwait(false);
        if (!info.IsFound || info.Path is null || info.Version is null)
        {
            var status = info.Status == CopilotCliStatus.TooOld
                ? AgentReadinessStatus.CopilotCliTooOld
                : AgentReadinessStatus.CopilotCliMissing;
            return new AgentReadinessReport(status, info.Message);
        }

        // 3: the runtime, which the starter starts or reuses; it is the real check that the CLI runs and is signed in, and
        // the sentence that it gives for a runtime that is not is the answer
        var started = await _starter
            .StartAsync(new CopilotStartRequest(info.Path, info.Version), cancellationToken)
            .ConfigureAwait(false);
        if (started.Status != CopilotStartStatus.Started)
        {
            return new AgentReadinessReport(started.ReadinessStatus, started.Message);
        }

        // 4: the model settings, validated by the catalog of the runtime exactly as an agent will be made
        return await CheckCopilotModelAsync(settings, info.Version, started.Agents, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Validates the model and the reasoning effort of the settings against the catalog of the running Copilot runtime.
    /// </summary>
    /// <param name="settings">The provider choices.</param>
    /// <param name="version">The version of the Copilot CLI, for the sentence.</param>
    /// <param name="agents">The running runtime.</param>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>Ready, ready with the warning for a model without vision, or the reason that the settings cannot be used.</returns>
    private static async Task<AgentReadinessReport> CheckCopilotModelAsync(
        AiProviderSettings settings,
        Version version,
        ICopilotAgentSource? agents,
        CancellationToken cancellationToken)
    {
        // No model named means the default model of the runtime; there is no choice to validate
        var modelSettings = ModelRequirements.ToModelSettings(settings, withContextWindow: false);
        if (modelSettings is null || agents is null)
        {
            return new AgentReadinessReport(AgentReadinessStatus.Ready, WithSignIn($"GitHub Copilot CLI {version} is ready.", agents));
        }

        ModelSelection selection;
        try
        {
            selection = await agents.Models.SelectAsync(modelSettings, ModelRequirements.Required, cancellationToken).ConfigureAwait(false);
        }
        catch (ModelSettingsException ex)
        {
            return new AgentReadinessReport(AgentReadinessStatus.ModelSettingsInvalid, ModelRequirements.Describe(ex));
        }
        catch (InvalidOperationException ex)
        {
            // The catalog could not be read: the runtime is not connected or was stopped
            return new AgentReadinessReport(AgentReadinessStatus.CopilotRuntimeUnavailable, ex.Message);
        }

        var warning = ModelRequirements.VisionWarning(selection.Model);
        var size = DescribeContextSize(selection);
        return warning is null
            ? new AgentReadinessReport(AgentReadinessStatus.Ready, WithSignIn($"GitHub Copilot CLI {version} is ready with the model {selection.Model.Id}{size}.", agents))
            : new AgentReadinessReport(AgentReadinessStatus.ModelLacksVision, WithSignIn(warning, agents));
    }

    /// <summary>
    ///     Words the context size that the settings chose, for the sentence that says that the provider is ready and for
    ///     the tooltip of the provider pill.
    /// </summary>
    /// <param name="selection">The validated selection.</param>
    /// <returns>
    ///     For example <c> at the Long context size (1,000,000 tokens)</c>; an empty string when no size was chosen.
    /// </returns>
    private static string DescribeContextSize(ModelSelection selection)
    {
        var chosen = selection.Settings.ContextTier;
        if (string.IsNullOrEmpty(chosen))
        {
            return string.Empty;
        }

        var tier = selection.Model.ContextTiers.FirstOrDefault(candidate => string.Equals(candidate.Id, chosen, StringComparison.OrdinalIgnoreCase));
        var name = tier is null || string.IsNullOrWhiteSpace(tier.DisplayName) ? chosen : tier.DisplayName;
        return tier?.WindowTokens is { } window
            ? string.Create(CultureInfo.InvariantCulture, $" at the {name} size ({window:N0} tokens)")
            : $" at the {name} size";
    }

    /// <summary>
    ///     Adds the sentence about how the runtime signed in to a sentence of a provider that can be used, so that the user
    ///     sees which account the agents use, in Check connection and in the tooltip of the provider pill.
    /// </summary>
    /// <param name="message">The sentence of the report, which ends with a full stop.</param>
    /// <param name="agents">The running runtime, or <see langword="null" /> when there is none.</param>
    /// <returns>
    ///     The sentence followed by the sign-in sentence; the sentence alone when the runtime does not say how it signed in.
    ///     The sign-in sentence holds the login, the host, and the kind of sign-in and never a token.
    /// </returns>
    private static string WithSignIn(string message, ICopilotAgentSource? agents)
    {
        var signIn = agents?.SignIn?.Describe();
        return string.IsNullOrEmpty(signIn) ? message : $"{message} {signIn}";
    }

    /// <summary>
    ///     Runs the ordered checks of Ollama.
    /// </summary>
    /// <param name="settings">The provider choices.</param>
    /// <param name="cancellationToken">A token that cancels the checks.</param>
    /// <returns>The report of the first check that fails, the warning for a small context window, or ready.</returns>
    private async Task<AgentReadinessReport> CheckOllamaAsync(AiProviderSettings settings, CancellationToken cancellationToken)
    {
        // The settings must say where the server is and which model to use
        if (!AiProviderSettings.IsValidOllamaEndpoint(settings.OllamaEndpoint))
        {
            return new AgentReadinessReport(
                AgentReadinessStatus.NotConfigured,
                "The Ollama address in Settings is not a valid http or https address; fix it, then choose Check connection.");
        }

        var model = settings.ModelName.Trim();
        if (model.Length == 0)
        {
            return new AgentReadinessReport(
                AgentReadinessStatus.NotConfigured,
                "Enter the name of an Ollama model in Settings, then choose Check connection.");
        }

        // The model settings are validated by the catalog of the server, which knows what the model can do; a catalog
        // that cannot be read leaves the settings unchecked, because the list of models just answered
        var address = settings.OllamaEndpoint.Trim();
        var endpoint = new Uri(address);
        var list = await _ollama.ListModelsAsync(endpoint, OllamaProbeTimeout, cancellationToken).ConfigureAwait(false);
        if (!list.Reachable)
        {
            return new AgentReadinessReport(
                AgentReadinessStatus.OllamaUnreachable,
                $"Ollama did not answer at {address}; start Ollama or fix the address in Settings, then choose Check connection.");
        }

        var listed = FindModel(list.Models, model);
        if (listed is null)
        {
            return new AgentReadinessReport(
                AgentReadinessStatus.OllamaModelMissing,
                $"Ollama does not have the model {model}; run `ollama pull {model}`, then choose Check connection.");
        }

        var (selection, invalid) = await SelectOllamaModelAsync(settings, endpoint, cancellationToken).ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        // The context window is a warning, not an obstacle; the model's own limit is asked for but is optional
        var detected = await _ollama.GetContextLengthAsync(endpoint, listed, OllamaProbeTimeout, cancellationToken).ConfigureAwait(false)
            ?? selection?.Model.ContextWindowTokens;
        var warning = DescribeSmallContext(settings.ContextWindowTokens, detected);
        var visionWarning = selection is null ? null : ModelRequirements.VisionWarning(selection.Model);
        if (warning is not null)
        {
            return new AgentReadinessReport(
                AgentReadinessStatus.OllamaContextTooSmall,
                visionWarning is null ? warning : $"{warning} {visionWarning}");
        }

        if (visionWarning is not null)
        {
            return new AgentReadinessReport(AgentReadinessStatus.ModelLacksVision, visionWarning);
        }

        // Without the catalog nothing is known about pictures, so the sentence says what is true of every model
        return new AgentReadinessReport(
            AgentReadinessStatus.Ready,
            selection is null
                ? $"Ollama is running with the model {model}; pictures work only with a vision-capable model."
                : $"Ollama is running with the model {selection.Model.Id}.");
    }

    /// <summary>
    ///     Validates the model settings against the catalog of the Ollama server.
    /// </summary>
    /// <param name="settings">The provider choices.</param>
    /// <param name="endpoint">The address of the server.</param>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>
    ///     The selection when the settings are valid; the report that says why they are not; or neither when the catalog
    ///     could not be read, which leaves the settings unchecked.
    /// </returns>
    private async Task<(ModelSelection? Selection, AgentReadinessReport? Invalid)> SelectOllamaModelAsync(
        AiProviderSettings settings,
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        var modelSettings = ModelRequirements.ToModelSettings(settings, withContextWindow: true);
        if (modelSettings is null)
        {
            return (null, null);
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ProviderModelCatalog.OllamaListTimeout);
        try
        {
            var selection = await _ollamaCatalogs.For(endpoint).SelectAsync(modelSettings, ModelRequirements.Required, limit.Token).ConfigureAwait(false);
            return (selection, null);
        }
        catch (ModelSettingsException ex)
        {
            return (null, new AgentReadinessReport(AgentReadinessStatus.ModelSettingsInvalid, ModelRequirements.Describe(ex)));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NotSupportedException
                                       || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The server did not describe its models in time; the first turn finds a problem, if there is one
            return (null, null);
        }
    }

    /// <summary>
    ///     Finds a model among the names that the server lists. A name without a tag stands for the <c>latest</c> tag, as
    ///     it does for <c>ollama pull</c>.
    /// </summary>
    /// <param name="listed">The names that the server lists.</param>
    /// <param name="wanted">The name that the settings choose.</param>
    /// <returns>The name as the server lists it, or <see langword="null" /> when the server does not have the model.</returns>
    private static string? FindModel(IReadOnlyList<string> listed, string wanted)
    {
        var withTag = wanted.Contains(':', StringComparison.Ordinal) ? wanted : wanted + ":latest";
        return listed.FirstOrDefault(name => string.Equals(name, withTag, StringComparison.OrdinalIgnoreCase))
            ?? listed.FirstOrDefault(name => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase));
    }
}

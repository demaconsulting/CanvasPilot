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

using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     The "AI Provider" settings page. It edits and validates the persisted AI provider choices, and, when it is given
///     an <see cref="IProviderCheck" />, lets the user check the connection to the provider that the entries describe
///     before saving them.
/// </summary>
/// <remarks>
///     <para>
///         When the page is given an <see cref="IProviderModelSource" /> it lists the models of the selected provider
///         (when the page is shown, when the provider changes, when the endpoint or the Copilot CLI path is edited, and when
///         the user chooses Refresh models) and lets the user pick one. Every model is listed; what a model cannot do is
///         shown with it, as a note in the list and as a warning under it, and a model that the provider says cannot be used
///         is shown disabled. The typed model name always stays, so that a provider that cannot be asked, or a model that
///         is not listed, can still be used; the page says why the list is missing.
///     </para>
///     <para>
///         The reasoning level is a choice among the efforts that the selected model offers and is enabled only while the
///         model offers some; the stored value is the id that the provider uses. The unrestricted agent tools flag is not
///         offered at all because the copilot has no such tools; the value is loaded and applied unchanged. Every entry that
///         belongs to one provider is shown only while that provider is selected: the context window and the endpoint
///         belong to Ollama, and the command-line tool path, the cost warning, and the context size (shown only for a model
///         that offers more than one) belong to GitHub
///         Copilot. The context window warning (a window below
///         <see cref="AiProviderSettings.RecommendedMinimumContextTokens" />) and every problem of such an entry are given
///         only while that provider is selected, so a hidden entry can neither warn nor block the page.
///     </para>
/// </remarks>
public sealed partial class AiProviderSettingsPageViewModel : ObservableObject, ISettingsPage
{
    /// <summary>
    ///     The text of the entry that stands for the default model of the provider.
    /// </summary>
    public const string DefaultModelLabel = "Provider default model";

    /// <summary>
    ///     The text of the entry that stands for the effort that the provider chooses.
    /// </summary>
    public const string DefaultEffortLabel = "Provider default";

    /// <summary>
    ///     The text shown while the models are listed.
    /// </summary>
    public const string LoadingModelsText = "Loading the models...";

    /// <summary>
    ///     The delay, in milliseconds, between the last keystroke in an address or a path and the new list of models, so that
    ///     typing does not ask the provider once per letter.
    /// </summary>
    public const int DefaultTypingDelayMilliseconds = 600;

    /// <summary>
    ///     The note shown under the Copilot command-line tool path.
    /// </summary>
    public const string CopilotCliPathNoteText =
        "CanvasPilot uses the installed copilot.exe, version 1.0.113 or later. A path set here wins over the automatic "
        + "search; launcher shims, such as the one that VS Code puts on the PATH, are ignored.";

    /// <summary>
    ///     The text shown while a connection check runs.
    /// </summary>
    public const string CheckingText = "Checking the connection...";

    /// <summary>
    ///     The text shown when the entries are not valid, so that no check could run.
    /// </summary>
    public const string FixEntriesText = "Correct the entries marked in red first, then check the connection.";

    /// <summary>
    ///     The billing multiplier from which a model counts as large for <see cref="EffortCostWarning" />.
    /// </summary>
    public const double LargeModelMultiplier = 3;

    /// <summary>
    ///     The text of the note under the context size when the size is not the provider's own.
    /// </summary>
    public const string ContextTierCostNoteText =
        "A larger context size is billed and served separately by GitHub Copilot, so a message can cost more premium requests "
        + "and take longer, and a long conversation can use much more of it. CanvasPilot cannot tell the price; the line "
        + "under each answer shows what the turn counted.";

    /// <summary>
    ///     The id of the context size that the provider uses when none is chosen, which the page stores as an empty string.
    /// </summary>
    private const string StandardContextTierId = "default";

    /// <summary>
    ///     The reasoning levels that call for <see cref="EffortCostWarning" /> on a large model.
    /// </summary>
    private static readonly string[] HighEfforts = ["high", "xhigh", "max"];

    /// <summary>
    ///     The price bands of a provider that mark a large model.
    /// </summary>
    private static readonly string[] LargePriceCategories = ["high", "very_high", "premium"];

    /// <summary>
    ///     Characters that cannot appear in a file path on this platform.
    /// </summary>
    private static readonly char[] InvalidPathCharacters = Path.GetInvalidPathChars();

    /// <summary>
    ///     Checks the provider; <see langword="null" /> when the page offers no connection check.
    /// </summary>
    private readonly IProviderCheck? _providerCheck;

    /// <summary>
    ///     Lists the models of the provider; <see langword="null" /> when the page offers no model list.
    /// </summary>
    private readonly IProviderModelSource? _modelSource;

    /// <summary>
    ///     The time to wait after an edit of an address or a path before the models are listed again.
    /// </summary>
    private readonly TimeSpan _typingDelay;

    /// <summary>
    ///     The settings that were loaded last, which the entries are applied to for a connection check.
    /// </summary>
    private AppSettings _loaded = new();

    /// <summary>
    ///     Cancels the check that runs, or <see langword="null" /> when none runs.
    /// </summary>
    private CancellationTokenSource? _checkCancellation;

    /// <summary>
    ///     Counts the checks and the changes of entries, so that the answer of an old check is dropped.
    /// </summary>
    private int _checkVersion;

    /// <summary>
    ///     Cancels the listing of models that runs, or <see langword="null" /> when none runs.
    /// </summary>
    private CancellationTokenSource? _modelCancellation;

    /// <summary>
    ///     Counts the listings of models, so that the answer of an old listing is dropped.
    /// </summary>
    private int _modelVersion;

    /// <summary>
    ///     The listing of models that was started last.
    /// </summary>
    private Task _modelLoad = Task.CompletedTask;

    /// <summary>
    ///     Whether the entries are being loaded, which keeps the changes of the entries from being taken for edits.
    /// </summary>
    private bool _loading;

    /// <summary>
    ///     Whether the page itself is setting the selected model, which keeps that from being taken for a choice of the user.
    /// </summary>
    private bool _syncing;

    /// <summary>
    ///     The id of the chosen reasoning effort; an empty string for the effort that the provider chooses.
    /// </summary>
    private string _effortId = string.Empty;

    /// <summary>
    ///     The id of the chosen context size; an empty string for the size that the provider uses (the standard one).
    /// </summary>
    private string _tierId = string.Empty;

    /// <summary>
    ///     Selected provider.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsOllamaSelected),
        nameof(IsCopilotSelected),
        nameof(OllamaEndpointError),
        nameof(CopilotCliPathError),
        nameof(ContextWindowError),
        nameof(ContextWindowWarning),
        nameof(HasContextTierChoice),
        nameof(HasContextTierNote),
        nameof(ContextTierCostNote),
        nameof(HasContextTierCostNote),
        nameof(IsValid))]
    private SettingsChoice<AiProviderKind> _selectedProvider;

    /// <summary>
    ///     Model name text.
    /// </summary>
    [ObservableProperty]
    private string _modelName = string.Empty;

    /// <summary>
    ///     The models of the selected provider, led by the entry for the default model; empty while none is listed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModelList))]
    private IReadOnlyList<ModelChoice> _models = [];

    /// <summary>
    ///     The entry of the list that the model name stands for, or <see langword="null" /> when the name is not in the list.
    /// </summary>
    [ObservableProperty]
    private ModelChoice? _selectedModel;

    /// <summary>
    ///     Whether the models are being listed.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingModels;

    /// <summary>
    ///     The sentence about the list of models: that it is loading, or why there is none and what to do.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModelListMessage))]
    private string _modelListMessage = string.Empty;

    /// <summary>
    ///     What the chosen model can do, one fact per line; an empty string when nothing is known about it.
    /// </summary>
    [ObservableProperty]
    private string _modelDetails = string.Empty;

    /// <summary>
    ///     What the chosen model cannot do that CanvasPilot wants, one sentence per line; an empty string when nothing is
    ///     missing or nothing is known.
    /// </summary>
    [ObservableProperty]
    private string _modelWarning = string.Empty;

    /// <summary>
    ///     The reasoning efforts that can be chosen: the effort that the provider chooses, then the ones that the chosen
    ///     model offers.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SettingsChoice<string>> _reasoningEfforts = [new SettingsChoice<string>(string.Empty, DefaultEffortLabel)];

    /// <summary>
    ///     The chosen reasoning effort.
    /// </summary>
    [ObservableProperty]
    private SettingsChoice<string>? _selectedReasoningEffort;

    /// <summary>
    ///     Whether the reasoning effort can be chosen: the chosen model offers efforts.
    /// </summary>
    [ObservableProperty]
    private bool _isReasoningEffortEnabled;

    /// <summary>
    ///     The sentence that says why the reasoning effort cannot be chosen, or that the stored one was reset; an empty
    ///     string when there is nothing to say.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReasoningNote))]
    private string _reasoningNote = string.Empty;

    /// <summary>
    ///     The context sizes that can be chosen for the chosen model, standard first; the standard one is the entry with
    ///     the empty id.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasContextTierChoice),
        nameof(HasContextTierNote),
        nameof(ContextTierCostNote),
        nameof(HasContextTierCostNote))]
    private IReadOnlyList<SettingsChoice<string>> _contextTiers = [];

    /// <summary>
    ///     The chosen context size.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextTierCostNote), nameof(HasContextTierCostNote))]
    private SettingsChoice<string>? _selectedContextTier;

    /// <summary>
    ///     The sentence that says that the stored context size was reset because the chosen model does not offer it; an
    ///     empty string when there is nothing to say.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContextTierNote))]
    private string _contextTierNote = string.Empty;

    /// <summary>
    ///     What the provider says about the chosen model, or <see langword="null" /> when the model is not in the list.
    /// </summary>
    private ProviderModelInfo? _modelInfo;

    /// <summary>
    ///     Context window text as typed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextWindowError), nameof(ContextWindowWarning), nameof(IsValid))]
    private string _contextWindowText = AiProviderSettings.DefaultContextWindowTokens.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Ollama endpoint text as typed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OllamaEndpointError), nameof(IsValid))]
    private string _ollamaEndpoint = AiProviderSettings.DefaultOllamaEndpoint;

    /// <summary>
    ///     Whether unrestricted agent tools are enabled. It is kept and applied unchanged; the page does not offer it.
    /// </summary>
    [ObservableProperty]
    private bool _unrestrictedAgentTools;

    /// <summary>
    ///     Copilot command-line tool path text as typed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopilotCliPathError), nameof(IsValid))]
    private string _copilotCliPath = string.Empty;

    /// <summary>
    ///     The answer of the last connection check, or the text that says that one runs, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCheckConnectionResult), nameof(IsCheckConnectionFailed))]
    private string _checkConnectionResult = string.Empty;

    /// <summary>
    ///     Whether the last connection check found the provider usable.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCheckConnectionFailed))]
    private bool _isCheckConnectionOk;

    /// <summary>
    ///     Whether a connection check runs.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckConnection), nameof(IsCheckConnectionFailed))]
    private bool _isCheckingConnection;

    /// <summary>
    ///     Creates the page showing the default AI provider settings.
    /// </summary>
    /// <param name="providerCheck">
    ///     Checks the provider that the entries describe; <see langword="null" /> for a page without the Check connection
    ///     button.
    /// </param>
    /// <param name="modelSource">
    ///     Lists the models of the provider that the entries describe; <see langword="null" /> for a page without a model
    ///     list, which leaves the model name to be typed.
    /// </param>
    /// <param name="typingDelay">
    ///     The time to wait after an edit of the address or the path before the models are listed again; the default of
    ///     <see cref="DefaultTypingDelayMilliseconds" /> milliseconds when <see langword="null" />.
    /// </param>
    public AiProviderSettingsPageViewModel(
        IProviderCheck? providerCheck = null,
        IProviderModelSource? modelSource = null,
        TimeSpan? typingDelay = null)
    {
        _providerCheck = providerCheck;
        _modelSource = modelSource;
        _typingDelay = typingDelay ?? TimeSpan.FromMilliseconds(DefaultTypingDelayMilliseconds);
        Providers =
        [
            new SettingsChoice<AiProviderKind>(AiProviderKind.GitHubCopilot, "GitHub Copilot"),
            new SettingsChoice<AiProviderKind>(AiProviderKind.Ollama, "Ollama"),
        ];
        _selectedProvider = Providers[0];
        _selectedReasoningEffort = _reasoningEfforts[0];
    }

    /// <inheritdoc />
    public string Title => "AI Provider";

    /// <inheritdoc />
    public string Description => "Choose which AI provider and model the copilot uses, and check that it can be reached.";

    /// <inheritdoc />
    public string IconKey => "CpIconSparkle";

    /// <summary>
    ///     Gets the providers that can be chosen.
    /// </summary>
    public IReadOnlyList<SettingsChoice<AiProviderKind>> Providers { get; }

    /// <summary>
    ///     Gets a value indicating whether the page offers the list of models and the Refresh models button.
    /// </summary>
    public bool HasModelSource => _modelSource is not null;

    /// <summary>
    ///     Gets a value indicating whether the provider listed models, which makes the list of models the way to choose one.
    /// </summary>
    public bool HasModelList => Models.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether there is a sentence about the list of models to show.
    /// </summary>
    public bool HasModelListMessage => ModelListMessage.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether there is a note about the reasoning effort to show.
    /// </summary>
    public bool HasReasoningNote => ReasoningNote.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether the context size can be chosen: GitHub Copilot is selected and the chosen model,
    ///     as the provider listed it, offers more than one size. The entry is hidden otherwise.
    /// </summary>
    public bool HasContextTierChoice => IsCopilotSelected && ContextTiers.Count > 1;

    /// <summary>
    ///     Gets a value indicating whether there is a note about the context size to show.
    /// </summary>
    public bool HasContextTierNote => HasContextTierChoice && ContextTierNote.Length > 0;

    /// <summary>
    ///     Gets the note that a context size other than the standard one is billed and served separately, or an empty string
    ///     while the standard size, or no choice, is shown.
    /// </summary>
    public string ContextTierCostNote =>
        HasContextTierChoice && SelectedContextTier is { Value.Length: > 0 } ? ContextTierCostNoteText : string.Empty;

    /// <summary>
    ///     Gets a value indicating whether there is a <see cref="ContextTierCostNote" /> to show.
    /// </summary>
    public bool HasContextTierCostNote => ContextTierCostNote.Length > 0;

    /// <summary>
    ///     Gets the warning that a high reasoning level on a large model is slow and costly, or an empty string when the
    ///     chosen model and level do not call for it. It is for the model and level that are chosen now, and is shown for
    ///     GitHub Copilot only, whose models are billed in premium requests.
    /// </summary>
    public string EffortCostWarning => IsCopilotSelected ? BuildEffortCostWarning(_modelInfo, _effortId) : string.Empty;

    /// <summary>
    ///     Gets a value indicating whether there is an <see cref="EffortCostWarning" /> to show.
    /// </summary>
    public bool HasEffortCostWarning => EffortCostWarning.Length > 0;

    /// <summary>
    ///     Gets a task that completes when the listing of models that was started last is over, which lets a test or a
    ///     caller wait for the list that follows a change of the entries.
    /// </summary>
    public Task ModelListLoaded => _modelLoad;

    /// <summary>
    ///     Gets a value indicating whether Ollama is the selected provider, which shows the entries that belong to Ollama
    ///     (the context window and the endpoint) and hides the entries of GitHub Copilot.
    /// </summary>
    public bool IsOllamaSelected => SelectedProvider.Value == AiProviderKind.Ollama;

    /// <summary>
    ///     Gets a value indicating whether GitHub Copilot is the selected provider, which shows the entries that belong to
    ///     it (the command-line tool path and the cost warning) and hides the entries of Ollama.
    /// </summary>
    public bool IsCopilotSelected => SelectedProvider.Value == AiProviderKind.GitHubCopilot;

    /// <summary>
    ///     Gets the context window warning, or an empty string. It is a warning, not an error: the page stays valid. It is
    ///     given while Ollama is selected and the valid context window is below
    ///     <see cref="AiProviderSettings.RecommendedMinimumContextTokens" />.
    /// </summary>
    public string ContextWindowWarning =>
        IsOllamaSelected && TryParseContextWindow(out var tokens) && tokens < AiProviderSettings.RecommendedMinimumContextTokens
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"A context window below {AiProviderSettings.RecommendedMinimumContextTokens:N0} tokens may make drawings with images and tools fail. {AiProviderSettings.DefaultContextWindowTokens:N0} is recommended.")
            : string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the page offers the Check connection button.
    /// </summary>
    public bool HasConnectionCheck => _providerCheck is not null;

    /// <summary>
    ///     Gets a value indicating whether there is something to show about the connection check.
    /// </summary>
    public bool HasCheckConnectionResult => CheckConnectionResult.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether the answer shown is that the provider cannot be used, which the page shows in red.
    /// </summary>
    public bool IsCheckConnectionFailed => HasCheckConnectionResult && !IsCheckConnectionOk && !IsCheckingConnection;

    /// <summary>
    ///     Gets a value indicating whether the connection can be checked now: the page has a check, the entries are valid,
    ///     and no check runs.
    /// </summary>
    public bool CanCheckConnection => _providerCheck is not null && IsValid && !IsCheckingConnection;

    /// <summary>
    ///     Gets the context window problem, or an empty string when the entry is valid or Ollama is not selected, which is
    ///     the only provider that the entry belongs to; a hidden entry never blocks the page.
    /// </summary>
    public string ContextWindowError =>
        !IsOllamaSelected || TryParseContextWindow(out _)
            ? string.Empty
            : $"Enter a whole number from {AiProviderSettings.MinContextWindowTokens} to " +
              $"{AiProviderSettings.MaxContextWindowTokens}.";

    /// <summary>
    ///     Gets the Ollama endpoint problem, or an empty string when the entry is valid or Ollama is not selected.
    /// </summary>
    public string OllamaEndpointError =>
        !IsOllamaSelected || AiProviderSettings.IsValidOllamaEndpoint(OllamaEndpoint)
            ? string.Empty
            : "Enter an absolute http:// or https:// URL.";

    /// <summary>
    ///     Gets the Copilot tool path problem, or an empty string when the entry is valid or Copilot is not selected.
    /// </summary>
    public string CopilotCliPathError =>
        !IsCopilotSelected || IsCopilotCliPathText(CopilotCliPath)
            ? string.Empty
            : "The path contains characters that are not allowed in a file path.";

    /// <inheritdoc />
    public bool IsValid =>
        ContextWindowError.Length == 0 && OllamaEndpointError.Length == 0 && CopilotCliPathError.Length == 0;

    /// <inheritdoc />
    public void Load(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _loaded = settings;
        var ai = settings.AiProvider;

        // The entries are set one by one; none of the changes is an edit of the user, and the list is asked for once, last
        _loading = true;
        try
        {
            Models = [];
            ModelListMessage = string.Empty;
            SelectedProvider = Providers.First(choice => choice.Value == ai.Provider);
            ModelName = ai.ModelName;
            _effortId = ai.ReasoningEffort ?? string.Empty;
            _tierId = NormalizeTierId(ai.ContextTier);
            ContextWindowText = ai.ContextWindowTokens.ToString(CultureInfo.InvariantCulture);
            OllamaEndpoint = ai.OllamaEndpoint;
            UnrestrictedAgentTools = ai.UnrestrictedAgentTools;
            CopilotCliPath = ai.CopilotCliPath;
            UpdateModelPresentation(reconcile: false);
        }
        finally
        {
            _loading = false;
        }

        StartModelLoad(refresh: false, debounce: false);
    }

    /// <inheritdoc />
    public AppSettings Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValid)
        {
            throw new InvalidOperationException("The AI provider settings are not valid.");
        }

        // An entry belonging to the provider that is not selected is kept only when it is itself valid, so a
        // half-typed value for an unused provider never replaces a stored one.
        var contextWindow = TryParseContextWindow(out var tokens) ? tokens : settings.AiProvider.ContextWindowTokens;
        var endpoint = AiProviderSettings.IsValidOllamaEndpoint(OllamaEndpoint)
            ? OllamaEndpoint.Trim()
            : settings.AiProvider.OllamaEndpoint;
        var cliPath = IsCopilotCliPathText(CopilotCliPath)
            ? CopilotCliPath.Trim()
            : settings.AiProvider.CopilotCliPath;

        return settings with
        {
            AiProvider = settings.AiProvider with
            {
                Provider = SelectedProvider.Value,
                ModelName = ModelName.Trim(),
                ContextWindowTokens = contextWindow,
                ReasoningEffort = _effortId,
                ContextTier = _tierId,
                OllamaEndpoint = endpoint,
                UnrestrictedAgentTools = UnrestrictedAgentTools,
                CopilotCliPath = cliPath,
            },
        };
    }

    /// <inheritdoc />
    public void OnClosed()
    {
        // A check or a listing that still runs has nobody to tell
        ForgetCheck();
        _checkCancellation?.Cancel();
        _modelVersion++;
        _modelCancellation?.Cancel();
        IsLoadingModels = false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // The button follows the validity of the entries and the running check
        if (e.PropertyName is nameof(IsValid) or nameof(CanCheckConnection))
        {
            CheckConnectionCommand.NotifyCanExecuteChanged();
        }

        // An answer about other entries is not an answer about these
        if (e.PropertyName is nameof(SelectedProvider) or nameof(ModelName) or nameof(ContextWindowText)
            or nameof(OllamaEndpoint) or nameof(CopilotCliPath) or nameof(SelectedReasoningEffort)
            or nameof(SelectedContextTier))
        {
            ForgetCheck();
        }

        // Entries that decide which models exist ask for a new list; what the user typed is not asked for letter by letter
        if (!_loading)
        {
            if (e.PropertyName is nameof(SelectedProvider))
            {
                // A model id of one provider means nothing to the other, so the choice starts again
                _effortId = string.Empty;
                _tierId = string.Empty;
                ModelName = string.Empty;
                Models = [];
                ModelListMessage = string.Empty;
                UpdateModelPresentation(reconcile: false);
                StartModelLoad(refresh: false, debounce: false);
            }
            else if ((e.PropertyName is nameof(OllamaEndpoint) && IsOllamaSelected)
                     || (e.PropertyName is nameof(CopilotCliPath) && IsCopilotSelected))
            {
                StartModelLoad(refresh: false, debounce: true);
            }
        }
    }

    /// <summary>
    ///     Keeps the choice in the list and the reasoning efforts in step with the text of the model name.
    /// </summary>
    /// <param name="value">The new text.</param>
    partial void OnModelNameChanged(string value)
    {
        if (!_loading)
        {
            UpdateModelPresentation(reconcile: true);
        }
    }

    /// <summary>
    ///     Takes the model that the user chose in the list as the model name.
    /// </summary>
    /// <param name="value">The chosen entry; <see langword="null" /> when the list was replaced, which is not a choice.</param>
    partial void OnSelectedModelChanged(ModelChoice? value)
    {
        if (_syncing || value is null || !value.IsSelectable)
        {
            return;
        }

        ModelName = value.Id;
    }

    /// <summary>
    ///     Takes the effort that the user chose as the effort to store.
    /// </summary>
    /// <param name="value">The chosen entry; <see langword="null" /> when the list was replaced, which is not a choice.</param>
    partial void OnSelectedReasoningEffortChanged(SettingsChoice<string>? value)
    {
        if (_syncing || value is null)
        {
            return;
        }

        _effortId = value.Value;
        ReasoningNote = string.Empty;
        OnPropertyChanged(nameof(EffortCostWarning));
        OnPropertyChanged(nameof(HasEffortCostWarning));
    }

    /// <summary>
    ///     Takes the context size that the user chose as the size to store.
    /// </summary>
    /// <param name="value">The chosen entry; <see langword="null" /> when the list was replaced, which is not a choice.</param>
    partial void OnSelectedContextTierChanged(SettingsChoice<string>? value)
    {
        if (_syncing || value is null)
        {
            return;
        }

        _tierId = value.Value;
        ContextTierNote = string.Empty;
    }

    /// <summary>
    ///     Lists the models of the provider again, asking the provider and not the answer of a short while ago.
    /// </summary>
    /// <returns>A task that completes when the list is shown.</returns>
    [RelayCommand(CanExecute = nameof(HasModelSource))]
    private Task RefreshModelsAsync()
    {
        return StartModelLoad(refresh: true, debounce: false);
    }

    /// <summary>
    ///     Starts the listing of the models, which ends the one that runs.
    /// </summary>
    /// <param name="refresh">Whether to ask the provider again.</param>
    /// <param name="debounce">Whether to wait a moment first, because the user may still be typing.</param>
    /// <returns>A task that completes when the answer is shown or dropped.</returns>
    private Task StartModelLoad(bool refresh, bool debounce)
    {
        if (_modelSource is null)
        {
            return Task.CompletedTask;
        }

        // A listing that runs is about older entries
        _modelCancellation?.Cancel();
        var version = ++_modelVersion;

        // Entries that are not valid cannot be asked about; the list that is shown stays, and so does an answer that
        // belongs to it, but the loading text of the listing that was just ended must not stay
        if (!IsValid)
        {
            IsLoadingModels = false;
            if (ModelListMessage == LoadingModelsText)
            {
                ModelListMessage = string.Empty;
            }

            return _modelLoad = Task.CompletedTask;
        }

        var cancellation = new CancellationTokenSource();
        _modelCancellation = cancellation;
        var settings = _loaded.AiProvider with
        {
            Provider = SelectedProvider.Value,
            OllamaEndpoint = OllamaEndpoint,
            CopilotCliPath = CopilotCliPath.Trim(),
        };
        IsLoadingModels = true;
        ModelListMessage = LoadingModelsText;
        return _modelLoad = LoadModelsAsync(_modelSource, settings, refresh, debounce, version, cancellation);
    }

    /// <summary>
    ///     Asks the source for the models and shows the answer, unless the entries changed meanwhile.
    /// </summary>
    /// <param name="source">The source of the models.</param>
    /// <param name="settings">The settings that describe the provider.</param>
    /// <param name="refresh">Whether to ask the provider again.</param>
    /// <param name="debounce">Whether to wait a moment first.</param>
    /// <param name="version">The number of this listing.</param>
    /// <param name="cancellation">Cancels this listing; it is not disposed, because a later listing may still cancel it.</param>
    /// <returns>A task that completes when the answer is shown or dropped.</returns>
    private async Task LoadModelsAsync(
        IProviderModelSource source,
        AiProviderSettings settings,
        bool refresh,
        bool debounce,
        int version,
        CancellationTokenSource cancellation)
    {
        ProviderModelList answer;
        try
        {
            if (debounce && _typingDelay > TimeSpan.Zero)
            {
                await Task.Delay(_typingDelay, cancellation.Token);
            }

            answer = await source.ListModelsAsync(settings, refresh, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Another listing or the closing of the page ended this one, and whoever did that shows what is needed
            return;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            answer = ProviderModelList.Failed($"The models could not be listed: {ex.Message}");
        }

        // The entries changed meanwhile: the answer is about another provider or address
        if (version != _modelVersion)
        {
            return;
        }

        var choices = new List<ModelChoice>();
        if (answer.Models.Count > 0)
        {
            choices.Add(new ModelChoice(string.Empty, DefaultModelLabel, null));
            choices.AddRange(answer.Models.Select(model => new ModelChoice(model.Id, LabelOf(model), model)));
        }

        Models = choices;
        ModelListMessage = answer.Message;
        IsLoadingModels = false;
        UpdateModelPresentation(reconcile: true);
    }

    /// <summary>
    ///     Words the entry of the list for a model: its name, and in a few words what it cannot do.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The text of the entry.</returns>
    private static string LabelOf(ProviderModelInfo model)
    {
        var name = string.Equals(model.DisplayName, model.Id, StringComparison.Ordinal)
            ? model.Id
            : $"{model.DisplayName} ({model.Id})";
        var missing = new List<string>();
        if (!model.IsUsable)
        {
            missing.Add("unavailable");
        }
        else
        {
            if (!model.SupportsToolCalls)
            {
                missing.Add("no tool calling");
            }

            if (!model.SupportsVision)
            {
                missing.Add("no pictures");
            }
        }

        return missing.Count == 0 ? name : $"{name} - {string.Join(", ", missing)}";
    }

    /// <summary>
    ///     Finds the entry of the list that the model name stands for. A name without a tag stands for the <c>latest</c> tag,
    ///     as it does for Ollama, and an empty name for the default entry.
    /// </summary>
    /// <returns>The entry, or <see langword="null" /> when the name is not in the list.</returns>
    private ModelChoice? FindChoice()
    {
        var name = ModelName?.Trim() ?? string.Empty;
        var withTag = name.Length > 0 && !name.Contains(':', StringComparison.Ordinal) ? name + ":latest" : name;
        return Models.FirstOrDefault(choice => string.Equals(choice.Id, name, StringComparison.OrdinalIgnoreCase))
               ?? Models.FirstOrDefault(choice => choice.Id.Length > 0 && string.Equals(choice.Id, withTag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Shows what is known about the model that the name stands for: the choice in the list, what the model can do and
    ///     lacks, and the efforts that it offers.
    /// </summary>
    /// <param name="reconcile">
    ///     <see langword="true" /> to reset a stored effort that the known model does not offer; <see langword="false" />
    ///     while the entries are loaded, when the stored values are shown as they are.
    /// </param>
    private void UpdateModelPresentation(bool reconcile)
    {
        var choice = FindChoice();
        var info = choice?.Info;
        var name = ModelName?.Trim() ?? string.Empty;

        // The choice in the list follows the text, without that being taken for a choice of the user
        _syncing = true;
        try
        {
            SelectedModel = choice;
        }
        finally
        {
            _syncing = false;
        }

        ModelDetails = DescribeModel(info, choice, name);
        ModelWarning = WarnAbout(info);
        _modelInfo = info;
        UpdateReasoningEfforts(info, name, reconcile);
        UpdateContextTiers(info, reconcile);
        OnPropertyChanged(nameof(EffortCostWarning));
        OnPropertyChanged(nameof(HasEffortCostWarning));
    }

    /// <summary>
    ///     Warns that a high reasoning effort on a large model is slow and costly, for the model and the effort that are
    ///     chosen now.
    /// </summary>
    /// <param name="info">What the provider says about the model; <see langword="null" /> when nothing is known.</param>
    /// <param name="effortId">The chosen effort; an empty string for the effort that the provider chooses.</param>
    /// <returns>The sentence, or an empty string when the choice is not a large model with a high effort.</returns>
    public static string BuildEffortCostWarning(ProviderModelInfo? info, string effortId)
    {
        if (info is null)
        {
            return string.Empty;
        }

        // The effort that the provider chooses is what counts when none was chosen
        var effort = effortId.Length > 0 ? effortId : info.DefaultReasoningEffort ?? string.Empty;
        var highEffort = HighEfforts.Contains(effort, StringComparer.OrdinalIgnoreCase);
        var largeModel = info.BillingMultiplier >= LargeModelMultiplier
            || string.Equals(info.Category, "powerful", StringComparison.OrdinalIgnoreCase)
            || LargePriceCategories.Contains(info.PriceCategory ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        if (!highEffort || !largeModel)
        {
            return string.Empty;
        }

        var cost = info.BillingMultiplier is { } multiplier
            ? $"each message counts {multiplier.ToString("0.##", CultureInfo.InvariantCulture)} premium requests"
            : "each message counts several premium requests";
        return $"A high reasoning level on a large model is slow (a big drawing can take minutes) and costly: {cost}, "
            + "and stopping a turn does not give the request back. Choose a lower level for everyday work.";
    }

    /// <summary>
    ///     Words what a model can do, one fact per line.
    /// </summary>
    /// <param name="info">What the provider says about the model; <see langword="null" /> when it is not in the list.</param>
    /// <param name="choice">The entry of the list; <see langword="null" /> when the name is not in the list.</param>
    /// <param name="name">The model name as typed.</param>
    /// <returns>The text, or an empty string when there is nothing to say.</returns>
    private string DescribeModel(ProviderModelInfo? info, ModelChoice? choice, string name)
    {
        if (info is null)
        {
            if (choice is not null)
            {
                return "The default model of the provider is used; what it can do is not listed.";
            }

            return name.Length > 0 && HasModelList
                ? $"{name} is not in the list; CanvasPilot tries it as typed and the first message tells whether it works."
                : string.Empty;
        }

        string efforts;
        if (info.ReasoningEfforts.Count > 0)
        {
            efforts = string.Join(", ", info.ReasoningEfforts.Select(effort => effort.Id));
        }
        else
        {
            efforts = info.SupportsReasoning ? "reasons, no level to choose" : "none";
        }

        var lines = new List<string>
        {
            $"Tool calling: {(info.SupportsToolCalls ? "yes" : "no")}",
            $"Pictures: {(info.SupportsVision ? "yes" : "no")}",
            $"Reasoning levels: {efforts}",
        };
        if (info.ContextWindowTokens is { } window)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Context window: up to {window:N0} tokens"));
        }

        if (info.ContextTiers is { Count: > 1 } tiers)
        {
            lines.Add($"Context sizes: {string.Join(", ", tiers.Select(LabelOfTier))}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    ///     Words what a model lacks that CanvasPilot needs or wants, one sentence per line.
    /// </summary>
    /// <param name="info">What the provider says about the model; <see langword="null" /> when nothing is known.</param>
    /// <returns>The text, or an empty string when nothing is missing.</returns>
    private static string WarnAbout(ProviderModelInfo? info)
    {
        if (info is null)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        if (!info.IsUsable)
        {
            lines.Add($"{info.DisplayName} cannot be used: {info.UnusableReason ?? "the provider says it is unavailable."}");
        }

        if (!info.SupportsToolCalls)
        {
            lines.Add($"{info.DisplayName} cannot call tools, and CanvasPilot needs tool calling to draw; it cannot use this model.");
        }

        if (!info.SupportsVision)
        {
            lines.Add($"{info.DisplayName} does not accept pictures; it cannot see your marks or its own drawings.");
        }

        lines.AddRange(info.Warnings);
        return string.Join('\n', lines);
    }

    /// <summary>
    ///     Offers the efforts that the model offers, enables the choice only when it offers some, and resets a stored effort
    ///     that it does not offer.
    /// </summary>
    /// <param name="info">What the provider says about the model; <see langword="null" /> when nothing is known.</param>
    /// <param name="name">The model name as typed.</param>
    /// <param name="reconcile">Whether to reset a stored effort that the known model does not offer.</param>
    private void UpdateReasoningEfforts(ProviderModelInfo? info, string name, bool reconcile)
    {
        var choices = new List<SettingsChoice<string>> { new(string.Empty, DefaultEffortLabel) };
        var note = string.Empty;
        var enabled = false;

        if (info is { ReasoningEfforts.Count: > 0 })
        {
            // The model offers efforts: its ids are what the provider accepts
            enabled = true;
            choices.AddRange(info.ReasoningEfforts.Select(effort => new SettingsChoice<string>(effort.Id, effort.DisplayName)));
            if (reconcile && _effortId.Length > 0 && !choices.Any(choice => string.Equals(choice.Value, _effortId, StringComparison.OrdinalIgnoreCase)))
            {
                note = $"{info.DisplayName} does not offer the reasoning level {_effortId}; the provider chooses instead.";
                _effortId = string.Empty;
            }
        }
        else if (info is not null)
        {
            // A model that is listed and offers none has no effort to keep
            note = "This model has no reasoning levels to choose.";
            if (reconcile)
            {
                _effortId = string.Empty;
            }
        }
        else if (name.Length == 0)
        {
            note = "Choose a model to set its reasoning level.";
            if (reconcile)
            {
                _effortId = string.Empty;
            }
        }
        else
        {
            // The model is not known (no list, or not in it): the stored level is kept as it is and cannot be changed
            note = "The reasoning levels of this model are not known, so the level cannot be changed here.";
        }

        // A stored effort that is not offered is still shown, so that the page does not hide what is stored
        if (_effortId.Length > 0 && !choices.Any(choice => string.Equals(choice.Value, _effortId, StringComparison.OrdinalIgnoreCase)))
        {
            choices.Add(new SettingsChoice<string>(_effortId, _effortId));
        }

        var selected = choices.FirstOrDefault(choice => string.Equals(choice.Value, _effortId, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
        _syncing = true;
        try
        {
            ReasoningEfforts = choices;
            SelectedReasoningEffort = selected;
        }
        finally
        {
            _syncing = false;
        }

        IsReasoningEffortEnabled = enabled;
        ReasoningNote = note;
    }

    /// <summary>
    ///     Offers the context sizes that the chosen model offers (only a model with more than one has a choice), and resets a
    ///     stored size that the known model does not offer.
    /// </summary>
    /// <param name="info">What the provider says about the model; <see langword="null" /> when nothing is known.</param>
    /// <param name="reconcile">Whether to reset a stored size that the known model does not offer.</param>
    private void UpdateContextTiers(ProviderModelInfo? info, bool reconcile)
    {
        var choices = new List<SettingsChoice<string>>();
        var note = string.Empty;

        if (info?.ContextTiers is { Count: > 1 } tiers)
        {
            // The standard size is the provider's own, which the settings store as no choice at all
            var standardId = info.DefaultContextTier ?? tiers[0].Id;
            foreach (var tier in tiers)
            {
                var value = string.Equals(tier.Id, standardId, StringComparison.OrdinalIgnoreCase) ? string.Empty : tier.Id;
                choices.Add(new SettingsChoice<string>(value, LabelOfTier(tier)));
            }

            if (_tierId.Length > 0 && !choices.Any(choice => string.Equals(choice.Value, _tierId, StringComparison.OrdinalIgnoreCase)))
            {
                if (reconcile)
                {
                    note = $"{info.DisplayName} does not offer the context size {_tierId}; the standard size is used instead.";
                    _tierId = string.Empty;
                }
                else
                {
                    // A stored size that is not offered is still shown, so that the page does not hide what is stored
                    choices.Add(new SettingsChoice<string>(_tierId, _tierId));
                }
            }
        }
        else if (reconcile && (info is not null || string.IsNullOrWhiteSpace(ModelName)))
        {
            // A listed model that offers one size or none, and the default model of the provider, have no size to keep; while
            // nothing is known about a typed model the stored size stays as it is
            _tierId = string.Empty;
        }

        var selected = choices.FirstOrDefault(choice => string.Equals(choice.Value, _tierId, StringComparison.OrdinalIgnoreCase)) ?? choices.FirstOrDefault();
        _syncing = true;
        try
        {
            ContextTiers = choices;
            SelectedContextTier = selected;
        }
        finally
        {
            _syncing = false;
        }

        ContextTierNote = note;
    }

    /// <summary>
    ///     Words the entry of the list of context sizes: the name of the size and, when the provider says it, its window.
    /// </summary>
    /// <param name="tier">The size.</param>
    /// <returns>The text, for example <c>Long context (1M tokens)</c>.</returns>
    private static string LabelOfTier(ProviderContextTier tier)
    {
        var name = string.IsNullOrWhiteSpace(tier.DisplayName) ? tier.Id : tier.DisplayName;
        return tier.WindowTokens is { } window ? $"{name} ({FormatWindow(window)})" : name;
    }

    /// <summary>
    ///     Writes a context window for a person: whole thousands, or millions.
    /// </summary>
    /// <param name="tokens">The window in tokens.</param>
    /// <returns>The text, for example <c>228K tokens</c> or <c>1M tokens</c>.</returns>
    private static string FormatWindow(int tokens)
    {
        return tokens >= 1_000_000
            ? string.Create(CultureInfo.InvariantCulture, $"{tokens / 1_000_000.0:0.##}M tokens")
            : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(tokens / 1000.0):0}K tokens");
    }

    /// <summary>
    ///     Normalizes a stored context size: trimmed, and the standard size, which is the provider's own, as no choice.
    /// </summary>
    /// <param name="stored">The stored id; may be <see langword="null" />.</param>
    /// <returns>The id to edit.</returns>
    private static string NormalizeTierId(string? stored)
    {
        var id = stored?.Trim() ?? string.Empty;
        return string.Equals(id, StandardContextTierId, StringComparison.OrdinalIgnoreCase) ? string.Empty : id;
    }

    /// <summary>
    ///     Checks the provider that the entries describe, without saving them, and shows the answer.
    /// </summary>
    /// <returns>A task that completes when the answer is shown.</returns>
    [RelayCommand(CanExecute = nameof(CanCheckConnection))]
    private async Task CheckConnectionAsync()
    {
        if (_providerCheck is null)
        {
            return;
        }

        // The entries are merged into the settings that were loaded, as OK would, but nothing is stored
        AiProviderSettings settings;
        try
        {
            settings = Apply(_loaded).AiProvider;
        }
        catch (InvalidOperationException)
        {
            IsCheckConnectionOk = false;
            CheckConnectionResult = FixEntriesText;
            return;
        }

        ForgetCheck();
        var version = _checkVersion;
        using var cancellation = new CancellationTokenSource();
        _checkCancellation = cancellation;
        IsCheckingConnection = true;
        CheckConnectionResult = CheckingText;
        ProviderCheckResult result;
        try
        {
            result = await _providerCheck.CheckAsync(settings, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            result = new ProviderCheckResult(false, string.Empty);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result = new ProviderCheckResult(false, $"The connection could not be checked: {ex.Message}");
        }
        finally
        {
            _checkCancellation = null;
            IsCheckingConnection = false;
        }

        // The user changed an entry or closed the dialog meanwhile: the answer is about something else
        if (version != _checkVersion)
        {
            return;
        }

        IsCheckConnectionOk = result.IsUsable;
        CheckConnectionResult = result.Message;
    }

    /// <summary>
    ///     Drops the answer of the last connection check and ends the one that runs.
    /// </summary>
    private void ForgetCheck()
    {
        _checkVersion++;
        if (CheckConnectionResult.Length > 0)
        {
            IsCheckConnectionOk = false;
            CheckConnectionResult = string.Empty;
        }
    }

    /// <summary>
    ///     Determines whether a text can be a file path.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns><see langword="true" /> when the text has no characters that are invalid in a path.</returns>
    private static bool IsCopilotCliPathText(string text)
    {
        return text.IndexOfAny(InvalidPathCharacters) < 0;
    }

    /// <summary>
    ///     Parses the context window text.
    /// </summary>
    /// <param name="tokens">The parsed size when the text is valid.</param>
    /// <returns><see langword="true" /> when the text is a whole number within the accepted bounds.</returns>
    private bool TryParseContextWindow(out int tokens)
    {
        return int.TryParse(ContextWindowText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out tokens)
               && AiProviderSettings.IsValidContextWindow(tokens);
    }
}

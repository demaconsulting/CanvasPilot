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
///     The reasoning level is shown disabled with a note, because the AI library does not expose it yet, and the
///     unrestricted agent tools flag is not offered at all because the copilot has no such tools; both values are loaded
///     and applied unchanged. The context window belongs to Ollama only, and its warning (a window below
///     <see cref="AiProviderSettings.RecommendedMinimumContextTokens" />) is shown only while Ollama is selected.
/// </remarks>
public sealed partial class AiProviderSettingsPageViewModel : ObservableObject, ISettingsPage
{
    /// <summary>
    ///     The note shown beside the disabled reasoning level.
    /// </summary>
    public const string ReasoningLevelNoteText = "Not available yet: the AI library does not expose it";

    /// <summary>
    ///     The note shown under the context window while GitHub Copilot is selected.
    /// </summary>
    public const string ContextWindowCopilotNoteText = "The context window applies to Ollama only; GitHub Copilot sets its own.";

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
    ///     Characters that cannot appear in a file path on this platform.
    /// </summary>
    private static readonly char[] InvalidPathCharacters = Path.GetInvalidPathChars();

    /// <summary>
    ///     Checks the provider; <see langword="null" /> when the page offers no connection check.
    /// </summary>
    private readonly IProviderCheck? _providerCheck;

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
    ///     Selected provider.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsOllamaSelected),
        nameof(IsCopilotSelected),
        nameof(OllamaEndpointError),
        nameof(CopilotCliPathError),
        nameof(ContextWindowWarning),
        nameof(ContextWindowNote),
        nameof(IsValid))]
    private SettingsChoice<AiProviderKind> _selectedProvider;

    /// <summary>
    ///     Model name text.
    /// </summary>
    [ObservableProperty]
    private string _modelName = string.Empty;

    /// <summary>
    ///     Context window text as typed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextWindowError), nameof(ContextWindowWarning), nameof(IsValid))]
    private string _contextWindowText = AiProviderSettings.DefaultContextWindowTokens.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Selected reasoning level. It is kept and applied unchanged; the combo box that shows it is disabled.
    /// </summary>
    [ObservableProperty]
    private SettingsChoice<ReasoningLevel> _selectedReasoningLevel;

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
    public AiProviderSettingsPageViewModel(IProviderCheck? providerCheck = null)
    {
        _providerCheck = providerCheck;
        Providers =
        [
            new SettingsChoice<AiProviderKind>(AiProviderKind.GitHubCopilot, "GitHub Copilot"),
            new SettingsChoice<AiProviderKind>(AiProviderKind.Ollama, "Ollama"),
        ];
        ReasoningLevels =
        [
            new SettingsChoice<ReasoningLevel>(ReasoningLevel.Auto, "Auto"),
            new SettingsChoice<ReasoningLevel>(ReasoningLevel.Low, "Low"),
            new SettingsChoice<ReasoningLevel>(ReasoningLevel.Medium, "Medium"),
            new SettingsChoice<ReasoningLevel>(ReasoningLevel.High, "High"),
        ];
        _selectedProvider = Providers[0];
        _selectedReasoningLevel = ReasoningLevels[0];
    }

    /// <inheritdoc />
    public string Title => "AI Provider";

    /// <summary>
    ///     Gets the providers that can be chosen.
    /// </summary>
    public IReadOnlyList<SettingsChoice<AiProviderKind>> Providers { get; }

    /// <summary>
    ///     Gets the reasoning levels that can be chosen.
    /// </summary>
    public IReadOnlyList<SettingsChoice<ReasoningLevel>> ReasoningLevels { get; }

    /// <summary>
    ///     Gets a value indicating whether Ollama is the selected provider, which enables the endpoint entry.
    /// </summary>
    public bool IsOllamaSelected => SelectedProvider.Value == AiProviderKind.Ollama;

    /// <summary>
    ///     Gets a value indicating whether GitHub Copilot is the selected provider, which enables the command-line
    ///     tool path entry.
    /// </summary>
    public bool IsCopilotSelected => SelectedProvider.Value == AiProviderKind.GitHubCopilot;

    /// <summary>
    ///     Gets the note under the context window while GitHub Copilot is selected, or an empty string while Ollama is
    ///     selected, which is what the window belongs to.
    /// </summary>
    public string ContextWindowNote => IsCopilotSelected ? ContextWindowCopilotNoteText : string.Empty;

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
    ///     Gets the context window problem, or an empty string when the entry is valid.
    /// </summary>
    public string ContextWindowError =>
        TryParseContextWindow(out _)
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
        SelectedProvider = Providers.First(choice => choice.Value == ai.Provider);
        ModelName = ai.ModelName;
        ContextWindowText = ai.ContextWindowTokens.ToString(CultureInfo.InvariantCulture);
        SelectedReasoningLevel = ReasoningLevels.First(choice => choice.Value == ai.ReasoningLevel);
        OllamaEndpoint = ai.OllamaEndpoint;
        UnrestrictedAgentTools = ai.UnrestrictedAgentTools;
        CopilotCliPath = ai.CopilotCliPath;
    }

    /// <inheritdoc />
    public AppSettings Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValid || !TryParseContextWindow(out var contextWindow))
        {
            throw new InvalidOperationException("The AI provider settings are not valid.");
        }

        // An entry belonging to the provider that is not selected is kept only when it is itself valid, so a
        // half-typed value for an unused provider never replaces a stored one.
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
                ReasoningLevel = SelectedReasoningLevel.Value,
                OllamaEndpoint = endpoint,
                UnrestrictedAgentTools = UnrestrictedAgentTools,
                CopilotCliPath = cliPath,
            },
        };
    }

    /// <inheritdoc />
    public void OnClosed()
    {
        // A check that still runs has nobody to tell
        ForgetCheck();
        _checkCancellation?.Cancel();
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
            or nameof(OllamaEndpoint) or nameof(CopilotCliPath))
        {
            ForgetCheck();
        }
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

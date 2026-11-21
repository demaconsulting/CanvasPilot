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
using CommunityToolkit.Mvvm.ComponentModel;

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     The "AI Provider" settings page. It edits and validates the persisted AI provider choices only; nothing in
///     the application consumes them yet.
/// </summary>
public sealed partial class AiProviderSettingsPageViewModel : ObservableObject, ISettingsPage
{
    /// <summary>
    ///     Characters that cannot appear in a file path on this platform.
    /// </summary>
    private static readonly char[] InvalidPathCharacters = Path.GetInvalidPathChars();

    /// <summary>
    ///     Selected provider.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsOllamaSelected),
        nameof(IsCopilotSelected),
        nameof(OllamaEndpointError),
        nameof(CopilotCliPathError),
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
    [NotifyPropertyChangedFor(nameof(ContextWindowError), nameof(IsValid))]
    private string _contextWindowText = AiProviderSettings.DefaultContextWindowTokens.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Selected reasoning level.
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
    ///     Whether unrestricted agent tools are enabled.
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
    ///     Creates the page showing the default AI provider settings.
    /// </summary>
    public AiProviderSettingsPageViewModel()
    {
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
        // Nothing to release: the page starts no background work.
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

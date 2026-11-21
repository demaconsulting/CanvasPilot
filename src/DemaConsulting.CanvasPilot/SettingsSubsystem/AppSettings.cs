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

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     Language model provider the application is configured to use.
/// </summary>
public enum AiProviderKind
{
    /// <summary>The GitHub Copilot service, reached through the Copilot command-line tool.</summary>
    GitHubCopilot,

    /// <summary>A local or remote Ollama server.</summary>
    Ollama,
}

/// <summary>
///     How much reasoning effort the language model is asked to spend.
/// </summary>
public enum ReasoningLevel
{
    /// <summary>Let the provider choose.</summary>
    Auto,

    /// <summary>Minimal reasoning effort.</summary>
    Low,

    /// <summary>Balanced reasoning effort.</summary>
    Medium,

    /// <summary>Maximum reasoning effort.</summary>
    High,
}

/// <summary>
///     Persisted AI provider choices. These values are stored only; no component consumes them yet.
/// </summary>
public sealed record AiProviderSettings
{
    /// <summary>
    ///     The smallest accepted context window, in tokens.
    /// </summary>
    public const int MinContextWindowTokens = 512;

    /// <summary>
    ///     The largest accepted context window, in tokens.
    /// </summary>
    public const int MaxContextWindowTokens = 2_000_000;

    /// <summary>
    ///     The default context window, in tokens.
    /// </summary>
    public const int DefaultContextWindowTokens = 8192;

    /// <summary>
    ///     The default Ollama server endpoint.
    /// </summary>
    public const string DefaultOllamaEndpoint = "http://localhost:11434";

    /// <summary>
    ///     Gets the selected provider.
    /// </summary>
    public AiProviderKind Provider { get; init; } = AiProviderKind.GitHubCopilot;

    /// <summary>
    ///     Gets the model name, or an empty string to use the provider's default model.
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the context window size, in tokens.
    /// </summary>
    public int ContextWindowTokens { get; init; } = DefaultContextWindowTokens;

    /// <summary>
    ///     Gets the requested reasoning effort.
    /// </summary>
    public ReasoningLevel ReasoningLevel { get; init; } = ReasoningLevel.Auto;

    /// <summary>
    ///     Gets the Ollama server endpoint.
    /// </summary>
    public string OllamaEndpoint { get; init; } = DefaultOllamaEndpoint;

    /// <summary>
    ///     Gets a value indicating whether the user has opted in to unrestricted agent tools. Off by default.
    /// </summary>
    public bool UnrestrictedAgentTools { get; init; }

    /// <summary>
    ///     Gets the path of the Copilot command-line tool, or an empty string to locate it automatically.
    /// </summary>
    public string CopilotCliPath { get; init; } = string.Empty;

    /// <summary>
    ///     Determines whether a text is an absolute <c>http</c> or <c>https</c> URL usable as an Ollama endpoint.
    /// </summary>
    /// <param name="endpoint">The text to check.</param>
    /// <returns><see langword="true" /> when the text is an absolute HTTP or HTTPS URL.</returns>
    public static bool IsValidOllamaEndpoint(string? endpoint)
    {
        return Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && !string.IsNullOrEmpty(uri.Host);
    }

    /// <summary>
    ///     Determines whether a context window size is within the accepted bounds.
    /// </summary>
    /// <param name="tokens">The size, in tokens.</param>
    /// <returns><see langword="true" /> when the size is within bounds.</returns>
    public static bool IsValidContextWindow(int tokens)
    {
        return tokens is >= MinContextWindowTokens and <= MaxContextWindowTokens;
    }
}

/// <summary>
///     Persisted speech choices. Empty strings mean "use the default".
/// </summary>
public sealed record SpeechSettings
{
    /// <summary>
    ///     Gets the capture (microphone) device name.
    /// </summary>
    public string CaptureDevice { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the playback (speaker) device name.
    /// </summary>
    public string PlaybackDevice { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the selected speech-to-text model identifier.
    /// </summary>
    public string SttModelId { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the selected text-to-speech model identifier.
    /// </summary>
    public string TtsModelId { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the per-model parameter values: model identifier, then parameter identifier, then the value as an
    ///     invariant-culture string.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ModelParameters { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>();
}

/// <summary>
///     Persisted choices about style kits: the shared folders in which kits are found, and the kit that a new project
///     starts with.
/// </summary>
public sealed record StyleSettings
{
    /// <summary>
    ///     Gets the folders, in order of precedence (an earlier folder wins over a later one), that hold kits that a team
    ///     shares. Every entry is a full path; a folder that cannot be reached is kept and is reported where the kits are
    ///     listed.
    /// </summary>
    public IReadOnlyList<string> SharedKitFolders { get; init; } = [];

    /// <summary>
    ///     Gets the id of the kit that a new project starts with, or an empty string for no style.
    /// </summary>
    public string DefaultKitId { get; init; } = string.Empty;

    /// <summary>
    ///     Determines whether a text can be the id of a style kit: 1 to 48 lower-case letters, digits, and dashes that start
    ///     with a letter or a digit.
    /// </summary>
    /// <remarks>
    ///     The settings subsystem does not depend on the style subsystem, to which the kits belong, so the rule of the id is
    ///     kept here as a plain check and the id is only text.
    /// </remarks>
    /// <param name="id">The text to check.</param>
    /// <returns><see langword="true" /> when the text can be the id of a style kit.</returns>
    public static bool IsValidKitId(string? id)
    {
        return id is { Length: >= 1 and <= 48 }
               && id[0] != '-'
               && id.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
    }
}

/// <summary>
///     The complete persisted application settings document (<c>settings.json</c>).
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    ///     The schema version written by this build.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    ///     Gets the schema version of the document.
    /// </summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    ///     Gets the recently opened project files, most recent first.
    /// </summary>
    public IReadOnlyList<string> RecentProjects { get; init; } = [];

    /// <summary>
    ///     Gets the AI provider choices.
    /// </summary>
    public AiProviderSettings AiProvider { get; init; } = new();

    /// <summary>
    ///     Gets the speech choices.
    /// </summary>
    public SpeechSettings Speech { get; init; } = new();

    /// <summary>
    ///     Gets the style kit choices.
    /// </summary>
    public StyleSettings Styles { get; init; } = new();
}

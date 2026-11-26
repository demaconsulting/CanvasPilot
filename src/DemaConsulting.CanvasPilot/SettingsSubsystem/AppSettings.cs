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
///     Persisted AI provider choices. The Agent subsystem reads the provider, model, reasoning effort, endpoint, context
///     window, and Copilot CLI path from them. The unrestricted tools flag is stored and kept as it is, but the dialog does
///     not offer it.
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
    ///     The context window, in tokens, of settings that do not state one. A value that is already stored is kept even
    ///     when it is smaller.
    /// </summary>
    public const int DefaultContextWindowTokens = 32768;

    /// <summary>
    ///     The smallest context window, in tokens, that drawings with pictures and tools can be expected to fit in; a
    ///     smaller value is accepted but gets a warning.
    /// </summary>
    public const int RecommendedMinimumContextTokens = 16384;

    /// <summary>
    ///     The default Ollama server endpoint.
    /// </summary>
    public const string DefaultOllamaEndpoint = "http://localhost:11434";

    /// <summary>
    ///     Gets the selected provider.
    /// </summary>
    public AiProviderKind Provider { get; init; } = AiProviderKind.GitHubCopilot;

    /// <summary>
    ///     Gets the id of the model as the provider names it (for example <c>claude-haiku-5.5</c> or <c>gemma3:latest</c>),
    ///     or an empty string to use the provider's default model.
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the context window size, in tokens.
    /// </summary>
    public int ContextWindowTokens { get; init; } = DefaultContextWindowTokens;

    /// <summary>
    ///     Gets the id of the reasoning effort that the chosen model is asked to spend, as the provider spells it (for
    ///     example <c>none</c>, <c>low</c>, <c>medium</c>, <c>high</c>, <c>xhigh</c>, or <c>max</c>), or an empty string to
    ///     leave the choice to the provider.
    /// </summary>
    /// <remarks>
    ///     Which ids exist depends on the model, so the value is text and not an enumeration. Files written by earlier
    ///     builds stored the choice as <c>reasoningLevel</c>; the settings service reads that value into this one.
    /// </remarks>
    public string ReasoningEffort { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the id of the context size (context tier) that a GitHub Copilot model is asked to run with, as the provider
    ///     spells it (for example <c>default</c> or <c>long_context</c>), or an empty string to leave the choice to the
    ///     provider, which is the standard size.
    /// </summary>
    /// <remarks>
    ///     Only some Copilot models offer more than one size, and which ids exist depends on the model, so the value is
    ///     text. The settings page offers the choice only for such a model and resets a value that the chosen model does not
    ///     offer. The value is additive: a file without it, and Ollama, get the provider's own size.
    /// </remarks>
    public string ContextTier { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the Ollama server endpoint.
    /// </summary>
    public string OllamaEndpoint { get; init; } = DefaultOllamaEndpoint;

    /// <summary>
    ///     Gets a value indicating whether the user has opted in to unrestricted agent tools. Off by default. The
    ///     copilot has no unrestricted tools, so the settings page does not offer the choice and keeps the stored value.
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
    ///     Gets the key, or the chord of modifiers and a key, that is held to dictate a message, written as
    ///     <c>F9</c> or <c>Ctrl+Shift+Space</c>. An empty string means the default (<c>F9</c>); a value that is not a safe
    ///     binding (including the old default <c>F8</c>) is replaced by an empty string when the settings are loaded.
    /// </summary>
    public string PushToTalkKey { get; init; } = string.Empty;

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
///     Which theme variant the application is shown in.
/// </summary>
public enum ThemeChoice
{
    /// <summary>The application follows the dark or light theme of the operating system.</summary>
    FollowSystem,

    /// <summary>The application is always dark.</summary>
    Dark,

    /// <summary>The application is always light.</summary>
    Light,
}

/// <summary>
///     Persisted choices about the look of the application: the theme variant and the density of lists.
/// </summary>
public sealed record AppearanceSettings
{
    /// <summary>
    ///     Gets the theme variant. The default follows the operating system.
    /// </summary>
    public ThemeChoice Theme { get; init; } = ThemeChoice.FollowSystem;

    /// <summary>
    ///     Gets a value indicating whether the project explorer shows a small picture beside each file. On by default.
    /// </summary>
    public bool ShowThumbnails { get; init; } = true;

    /// <summary>
    ///     Gets a value indicating whether the rows of the project explorer are taller. Off by default (compact rows).
    /// </summary>
    public bool ComfortableRows { get; init; }
}

/// <summary>
///     The state in which the main window was last closed.
/// </summary>
public enum SavedWindowState
{
    /// <summary>The window has the size and place that are stored.</summary>
    Normal,

    /// <summary>The window was maximized; the stored size and place are those it returns to.</summary>
    Maximized,
}

/// <summary>
///     Persisted layout of the main window: its size, place, and state, and the widths of the explorer and the chat
///     column, so the window opens as the user left it.
/// </summary>
/// <remarks>
///     The values are kept within the documented bounds by the settings service. The place is only a remembered
///     position: the window clamps it to the screens that are visible when it restores the window, because a monitor
///     may have been removed since.
/// </remarks>
public sealed record WindowSettings
{
    /// <summary>The width of a window that has no stored width, in device-independent pixels.</summary>
    public const int DefaultWidth = 1440;

    /// <summary>The height of a window that has no stored height, in device-independent pixels.</summary>
    public const int DefaultHeight = 900;

    /// <summary>The smallest accepted width of the window.</summary>
    public const int MinimumWidth = 960;

    /// <summary>The smallest accepted height of the window.</summary>
    public const int MinimumHeight = 600;

    /// <summary>The largest accepted width or height of the window, which no screen is expected to exceed.</summary>
    public const int MaximumSize = 16384;

    /// <summary>The largest accepted distance of the place of the window from the origin, in either direction.</summary>
    public const int MaximumCoordinate = 32767;

    /// <summary>The width of the explorer column when none is stored.</summary>
    public const int DefaultExplorerWidth = 264;

    /// <summary>The smallest accepted width of the explorer column.</summary>
    public const int MinimumExplorerWidth = 180;

    /// <summary>The largest accepted width of the explorer column.</summary>
    public const int MaximumExplorerWidth = 420;

    /// <summary>The width of the chat column when none is stored.</summary>
    public const int DefaultChatWidth = 400;

    /// <summary>The smallest accepted width of the chat column.</summary>
    public const int MinimumChatWidth = 320;

    /// <summary>The largest accepted width of the chat column.</summary>
    public const int MaximumChatWidth = 640;

    /// <summary>
    ///     Gets the width of the window in its normal state.
    /// </summary>
    public int Width { get; init; } = DefaultWidth;

    /// <summary>
    ///     Gets the height of the window in its normal state.
    /// </summary>
    public int Height { get; init; } = DefaultHeight;

    /// <summary>
    ///     Gets the horizontal place of the left edge of the window, or <see langword="null" /> to let the system place it.
    /// </summary>
    public int? Left { get; init; }

    /// <summary>
    ///     Gets the vertical place of the top edge of the window, or <see langword="null" /> to let the system place it.
    /// </summary>
    public int? Top { get; init; }

    /// <summary>
    ///     Gets the state the window was in when it was closed.
    /// </summary>
    public SavedWindowState State { get; init; } = SavedWindowState.Normal;

    /// <summary>
    ///     Gets the width of the explorer column.
    /// </summary>
    public int ExplorerWidth { get; init; } = DefaultExplorerWidth;

    /// <summary>
    ///     Gets the width of the chat column.
    /// </summary>
    public int ChatWidth { get; init; } = DefaultChatWidth;
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
    ///     The separators of the parts of a picture path: both, so a path written on another system is judged the same way.
    /// </summary>
    private static readonly char[] PictureSeparators = ['/', '\\'];

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

    /// <summary>
    ///     Gets the look of the application: the theme variant and the density of lists.
    /// </summary>
    public AppearanceSettings Appearance { get; init; } = new();

    /// <summary>
    ///     Gets the layout of the main window.
    /// </summary>
    public WindowSettings Window { get; init; } = new();

    /// <summary>
    ///     Gets the time at which each recently opened project was last opened, by the same path as in
    ///     <see cref="RecentProjects" />. A project that has no time (for example one that was opened by an earlier
    ///     build) is shown without one.
    /// </summary>
    /// <remarks>
    ///     This is an additive part of the document: an earlier build ignores it and a file of an earlier build has none,
    ///     so the schema version does not change.
    /// </remarks>
    public IReadOnlyDictionary<string, DateTimeOffset> RecentProjectOpened { get; init; } =
        new Dictionary<string, DateTimeOffset>();

    /// <summary>
    ///     Gets the picture that was last shown in each recently opened project, by the same path as in
    ///     <see cref="RecentProjects" />, as a path relative to the folder of the project (for example
    ///     <c>diagrams/flow.svg</c>). A project that has none opens with nothing selected.
    /// </summary>
    /// <remarks>
    ///     Which picture a person looked at is that person's own view state, and a project folder is copied, shared, and
    ///     put under source control, so it is kept here, on this computer, and never in the project file. The path is
    ///     relative so that it is the same for every place the folder is opened from; see
    ///     <see cref="IsValidShownPicture" /> for the paths that are kept. This is an additive part of the document, like
    ///     <see cref="RecentProjectOpened" />, so the schema version does not change.
    /// </remarks>
    public IReadOnlyDictionary<string, string> RecentProjectPicture { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    ///     Tells whether a path can be kept as the picture last shown in a project: a path relative to the folder of the
    ///     project that stays inside it.
    /// </summary>
    /// <remarks>
    ///     A rooted path would name a file outside the project, or on one computer only, and a <c>..</c> part leaves the
    ///     folder, so neither is a picture of the project. The check is on the text only and does not look at the disk:
    ///     a picture that was deleted is still a valid path, and the project simply opens with nothing selected.
    /// </remarks>
    /// <param name="picture">The path to check; may be <see langword="null" />.</param>
    /// <returns>
    ///     <see langword="true" /> when the path is not blank, has no character that a path cannot hold, is not rooted,
    ///     and has no <c>..</c> part.
    /// </returns>
    public static bool IsValidShownPicture(string? picture)
    {
        if (string.IsNullOrWhiteSpace(picture)
            || picture.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || Path.IsPathRooted(picture))
        {
            return false;
        }

        // No part may climb out of the folder of the project
        return !picture.Split(PictureSeparators).Any(part => part == "..");
    }
}

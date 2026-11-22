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

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     SettingsService keeps the application settings in one versioned JSON file in the per-user application data
///     folder. Missing, unreadable, corrupt, or newer files never stop the application: defaults are used and the
///     condition is logged. Writes are atomic so a failed write cannot damage the existing file.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    /// <summary>
    ///     Name of the settings file.
    /// </summary>
    public const string FileName = "settings.json";

    /// <summary>
    ///     Stable name of the application folder created under the per-user application data folder.
    /// </summary>
    private const string ApplicationFolderName = "CanvasPilot";

    /// <summary>
    ///     JSON options shared by every read and write: indented camel-case output, case-insensitive and tolerant
    ///     input, and enumerations that fall back to their first value when the text is not recognized.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new TolerantEnumConverter<AiProviderKind>(),
        },
    };

    /// <summary>
    ///     Guards the settings in effect and the file operations.
    /// </summary>
    private readonly Lock _syncRoot = new();

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The settings in effect.
    /// </summary>
    private AppSettings _current = new();

    /// <summary>
    ///     Creates a service that stores its file in <paramref name="settingsFolder" />. No file access happens
    ///     until <see cref="Load" /> or <see cref="Save" /> is called.
    /// </summary>
    /// <param name="settingsFolder">Folder holding <c>settings.json</c>.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="settingsFolder" /> is null or whitespace.</exception>
    public SettingsService(string settingsFolder, RollingFileLogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFolder);
        SettingsFolder = settingsFolder;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>
    ///     Gets the default per-user settings folder: <c>%APPDATA%\CanvasPilot</c> on Windows, and the equivalent
    ///     application data location on other platforms. When the platform reports no application data folder, the
    ///     user profile folder is used as the root instead, and the current directory when no user profile is
    ///     reported either.
    /// </summary>
    public static string DefaultSettingsFolder
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                root = ".";
            }

            return PathHelpers.SafePathCombine(root, ApplicationFolderName);
        }
    }

    /// <summary>
    ///     Gets the folder holding the settings file.
    /// </summary>
    public string SettingsFolder { get; }

    /// <inheritdoc />
    public string SettingsFilePath => PathHelpers.SafePathCombine(SettingsFolder, FileName);

    /// <inheritdoc />
    public AppSettings Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    /// <summary>
    ///     Creates a service for <see cref="DefaultSettingsFolder" />.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A service using the per-user settings folder.</returns>
    public static SettingsService CreateDefault(RollingFileLogger? logger = null) => new(DefaultSettingsFolder, logger);

    /// <inheritdoc />
    public AppSettings Load()
    {
        AppSettings loaded;
        lock (_syncRoot)
        {
            loaded = ReadSettingsFile();
            _current = loaded;
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(loaded));
        return loaded;
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AppSettings saved;
        lock (_syncRoot)
        {
            saved = WriteSettingsFile(settings);
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(saved));
    }

    /// <inheritdoc />
    public AppSettings Update(Func<AppSettings, AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        AppSettings saved;
        lock (_syncRoot)
        {
            saved = WriteSettingsFile(update(_current));
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(saved));
        return saved;
    }

    /// <summary>
    ///     Replaces missing or out-of-range values with defaults so the rest of the application can rely on them.
    /// </summary>
    /// <param name="settings">The settings as read or supplied; individual members may be null after parsing.</param>
    /// <returns>Settings that satisfy every documented bound, at the current schema version.</returns>
    private AppSettings Normalize(AppSettings? settings)
    {
        var source = settings ?? new AppSettings();
        var ai = source.AiProvider ?? new AiProviderSettings();
        var speech = source.Speech ?? new SpeechSettings();
        var styles = source.Styles ?? new StyleSettings();

        var parameters = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (modelId, values) in speech.ModelParameters ?? new Dictionary<string, IReadOnlyDictionary<string, string>>())
        {
            if (values is null)
            {
                continue;
            }

            parameters[modelId] = values
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        return new AppSettings
        {
            Version = AppSettings.CurrentVersion,
            RecentProjects = (source.RecentProjects ?? [])
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList(),
            AiProvider = ai with
            {
                ModelName = ai.ModelName?.Trim() ?? string.Empty,
                ReasoningEffort = ai.ReasoningEffort?.Trim() ?? string.Empty,
                ContextWindowTokens = AiProviderSettings.IsValidContextWindow(ai.ContextWindowTokens)
                    ? ai.ContextWindowTokens
                    : AiProviderSettings.DefaultContextWindowTokens,
                OllamaEndpoint = AiProviderSettings.IsValidOllamaEndpoint(ai.OllamaEndpoint)
                    ? ai.OllamaEndpoint.Trim()
                    : AiProviderSettings.DefaultOllamaEndpoint,
                CopilotCliPath = ai.CopilotCliPath?.Trim() ?? string.Empty,
            },
            Speech = speech with
            {
                CaptureDevice = speech.CaptureDevice ?? string.Empty,
                PlaybackDevice = speech.PlaybackDevice ?? string.Empty,
                SttModelId = speech.SttModelId ?? string.Empty,
                TtsModelId = speech.TtsModelId ?? string.Empty,
                ModelParameters = parameters,
            },
            Styles = styles with
            {
                SharedKitFolders = NormalizeSharedFolders(styles.SharedKitFolders),
                DefaultKitId = StyleSettings.IsValidKitId(styles.DefaultKitId?.Trim()) ? styles.DefaultKitId!.Trim() : string.Empty,
            },
        };
    }

    /// <summary>
    ///     Cleans the list of shared style kit folders: each entry is trimmed, must be a full path, and appears once; the
    ///     order is kept.
    /// </summary>
    /// <remarks>
    ///     A folder that does not exist or cannot be reached is kept, because a network folder may be down now and back
    ///     later and the list of kits says which folders cannot be reached. A relative path or a blank entry cannot mean the
    ///     same folder on every machine, so it is dropped and the drop is logged.
    /// </remarks>
    /// <param name="folders">The folders as read or supplied; may be <see langword="null" /> after parsing.</param>
    /// <returns>The kept folders, in their order.</returns>
    private List<string> NormalizeSharedFolders(IReadOnlyList<string>? folders)
    {
        var kept = new List<string>();
        foreach (var entry in folders ?? [])
        {
            var folder = entry?.Trim() ?? string.Empty;
            if (folder.Length == 0 || folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathFullyQualified(folder))
            {
                Log(LogLevel.Warning, $"The shared style kit folder '{entry}' is not a full path and was dropped from the settings.");
            }
            else if (!kept.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                kept.Add(folder);
            }
        }

        return kept;
    }

    /// <summary>
    ///     Reads and normalizes the settings file, never throwing for a bad file.
    /// </summary>
    /// <returns>The settings read, or defaults when the file is missing, unreadable, or corrupt.</returns>
    private AppSettings ReadSettingsFile()
    {
        var path = SettingsFilePath;
        if (!File.Exists(path))
        {
            Log(LogLevel.Info, $"No settings file at '{path}'; using default settings.");
            return Normalize(null);
        }

        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Error, $"Settings file '{path}' could not be read; using default settings.", ex);
            return Normalize(null);
        }

        AppSettings? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AppSettings>(text, JsonOptions);
        }
        catch (JsonException ex)
        {
            QuarantineCorruptFile(path, ex);
            return Normalize(null);
        }

        if (parsed is null)
        {
            QuarantineCorruptFile(path, null);
            return Normalize(null);
        }

        if (parsed.Version > AppSettings.CurrentVersion)
        {
            Log(
                LogLevel.Warning,
                $"Settings file '{path}' has newer schema version {parsed.Version} than the supported version " +
                $"{AppSettings.CurrentVersion}; unknown values are ignored.");
        }

        return Normalize(MigrateLegacyReasoningLevel(parsed, text));
    }

    /// <summary>
    ///     Reads the reasoning level that files of earlier builds stored under <c>aiProvider.reasoningLevel</c> into the
    ///     reasoning effort, so that a file keeps its meaning without a change of the schema version.
    /// </summary>
    /// <remarks>
    ///     The old values were <c>auto</c> (the provider chooses, which is an empty effort), <c>low</c>, <c>medium</c>, and
    ///     <c>high</c> (the same ids as efforts). A file that states a reasoning effort of its own is not changed, and a
    ///     value that is none of these is ignored. The old key is not written again.
    /// </remarks>
    /// <param name="parsed">The settings as parsed.</param>
    /// <param name="text">The text of the file that they were parsed from.</param>
    /// <returns>The settings with the effort of the old key when the file had no effort.</returns>
    private static AppSettings MigrateLegacyReasoningLevel(AppSettings parsed, string text)
    {
        if (!string.IsNullOrWhiteSpace(parsed.AiProvider?.ReasoningEffort))
        {
            return parsed;
        }

        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var provider = document.RootElement.EnumerateObject()
                .FirstOrDefault(property => string.Equals(property.Name, "aiProvider", StringComparison.OrdinalIgnoreCase))
                .Value;
            if (provider.ValueKind != JsonValueKind.Object)
            {
                return parsed;
            }

            var level = provider.EnumerateObject()
                .FirstOrDefault(property => string.Equals(property.Name, "reasoningLevel", StringComparison.OrdinalIgnoreCase))
                .Value;
            var effort = level.ValueKind == JsonValueKind.String ? level.GetString()?.Trim().ToLowerInvariant() : null;
            return effort is "low" or "medium" or "high"
                ? parsed with { AiProvider = (parsed.AiProvider ?? new AiProviderSettings()) with { ReasoningEffort = effort } }
                : parsed;
        }
        catch (JsonException)
        {
            // The text was parsed once already, so this cannot happen; the settings stay as they are if it ever does
            return parsed;
        }
    }

    /// <summary>
    ///     Renames a corrupt settings file to <c>settings.json.bad</c> so it is kept for diagnosis but no longer
    ///     read, replacing any earlier <c>.bad</c> file.
    /// </summary>
    /// <param name="path">The corrupt file.</param>
    /// <param name="cause">The parse failure, when there is one.</param>
    private void QuarantineCorruptFile(string path, Exception? cause)
    {
        var badPath = path + ".bad";
        try
        {
            File.Move(path, badPath, overwrite: true);
            Log(LogLevel.Error, $"Settings file '{path}' is corrupt; moved to '{badPath}' and using default settings.", cause);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Error, $"Settings file '{path}' is corrupt and could not be moved aside; using default settings.", ex);
        }
    }

    /// <summary>
    ///     Normalizes the settings, writes them atomically, and makes them the settings in effect.
    /// </summary>
    /// <param name="settings">The settings to store.</param>
    /// <returns>The normalized settings now in effect.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    private AppSettings WriteSettingsFile(AppSettings settings)
    {
        var normalized = Normalize(settings);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        var path = SettingsFilePath;
        string? tempPath = null;

        try
        {
            Directory.CreateDirectory(SettingsFolder);
            tempPath = PathHelpers.SafePathCombine(SettingsFolder, $"{FileName}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(tempPath);
            Log(LogLevel.Error, $"Settings could not be written to '{path}'.", ex);
            if (ex is IOException)
            {
                throw;
            }

            throw new IOException($"Settings could not be written to '{path}': {ex.Message}", ex);
        }

        _current = normalized;
        return normalized;
    }

    /// <summary>
    ///     Deletes a leftover temporary file, ignoring failures.
    /// </summary>
    /// <param name="path">The file to delete, or null.</param>
    private static void DeleteQuietly(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file does not affect the settings file.
        }
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks settings access.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    /// <param name="exception">Optional failure context.</param>
    private void Log(LogLevel level, string message, Exception? exception = null)
    {
        try
        {
            _logger?.Log(level, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a settings problem worse.
        }
    }

    /// <summary>
    ///     Reads an enumeration from its name, using the first (default) member for unrecognized or non-text values
    ///     so a newer file with an unknown choice still loads.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type.</typeparam>
    private sealed class TolerantEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        /// <inheritdoc />
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<TEnum>(reader.GetString(), ignoreCase: true, out var value)
                && Enum.IsDefined(value))
            {
                return value;
            }

            reader.Skip();
            return default;
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

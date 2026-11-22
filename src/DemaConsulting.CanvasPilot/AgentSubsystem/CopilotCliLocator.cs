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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Finds the GitHub Copilot CLI that the user installed, tells it from the launchers (shims) that editors put on the
///     <c>PATH</c>, and checks that it is new enough.
/// </summary>
/// <remarks>
///     <para>
///         The rule is documented in the design of the unit and is, in order: a path chosen in the settings is the only
///         candidate and is not filtered; otherwise the candidates are the WinGet package folders, the WinGet
///         <c>Links</c> entry, and every <c>copilot.exe</c> on the <c>PATH</c>, each followed through links and listed
///         once; candidates that are not <c>.exe</c> files, lie in the folder of an editor shim, or are smaller than any
///         real CLI are rejected; the first remaining candidate that answers <c>--version</c> with a version that is
///         new enough is used.
///     </para>
///     <para>
///         The reason for the three independent filters (extension, location, size) is that the VS Code shim is also called
///         <c>copilot.exe</c>, is on the <c>PATH</c> of every machine that has VS Code, and runs, but is a launcher and
///         not the CLI that the AI library is pointed to.
///     </para>
///     <para>
///         The rule is written for Windows paths and works on them whatever platform runs the code, so that it can be tested
///         everywhere; on other platforms no <c>copilot.exe</c> exists, so the search reports that the CLI was not found.
///         Nothing is logged here and no program output is kept beyond the version number. The locator caches its answer
///         until the setting changes or a refresh is asked for. It is safe for concurrent use.
///     </para>
/// </remarks>
public sealed partial class CopilotCliLocator
{
    /// <summary>
    ///     The oldest Copilot CLI version that works with the protocol of the AI library (protocol 4). Evidence is in the
    ///     design of the unit: version 1.0.106 reports protocol 3 and fails, version 1.0.114 works.
    /// </summary>
    public static readonly Version MinimumCliVersion = new(1, 0, 113);

    /// <summary>
    ///     The smallest size, in bytes, of a real Copilot CLI (20 MiB). The real CLI measured 196,113,184 bytes and the VS Code
    ///     shim 791,904 bytes, so the limit separates them with a wide margin on both sides.
    /// </summary>
    public const long MinimumCliBytes = 20L * 1024 * 1024;

    /// <summary>
    ///     The longest time that <c>--version</c> may take before the candidate is given up.
    /// </summary>
    public static readonly TimeSpan VersionProbeTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The names of the folders that mark an editor shim: the shim folder of VS Code, the VS Code user folders, and the
    ///     folder of the Copilot Chat extension. A new editor shim is one more entry.
    /// </summary>
    public static readonly IReadOnlyList<string> ShimDirectoryNames =
        ["copilot-shim", ".vscode", ".vscode-insiders", "github.copilot-chat"];

    /// <summary>
    ///     The start of the name of an installation folder of VS Code (<c>Microsoft VS Code</c>, <c>Microsoft VS Code
    ///     Insiders</c>), which also marks a shim.
    /// </summary>
    public const string ShimDirectoryPrefix = "Microsoft VS Code";

    /// <summary>The file name of the Copilot CLI on Windows.</summary>
    private const string ExecutableName = "copilot.exe";

    /// <summary>The pattern of the folder names of the WinGet packages of the Copilot CLI.</summary>
    private const string PackageFolderPattern = "GitHub.Copilot_*";

    /// <summary>
    ///     The arguments that make the CLI print the version of the executable itself: with <c>--no-auto-update</c> the
    ///     CLI does not switch to a newer version that it cached, which is the version that the AI library runs it with.
    /// </summary>
    private static readonly string[] VersionArguments = ["--no-auto-update", "--version"];

    /// <summary>The seam that runs <c>--version</c>.</summary>
    private readonly IProcessRunner _processRunner;

    /// <summary>The seam that asks the file system.</summary>
    private readonly ICliFileSystem _fileSystem;

    /// <summary>The seam that reads <c>PATH</c> and <c>LOCALAPPDATA</c>.</summary>
    private readonly IEnvironmentReader _environment;

    /// <summary>Guards the cached answer.</summary>
    private readonly Lock _cacheLock = new();

    /// <summary>The setting that the cached answer was found for.</summary>
    private string? _cachedKey;

    /// <summary>The cached answer, or <see langword="null" /> before the first search.</summary>
    private CopilotCliInfo? _cached;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CopilotCliLocator" /> class.
    /// </summary>
    /// <param name="processRunner">Runs <c>--version</c>; the real runner when <see langword="null" />.</param>
    /// <param name="fileSystem">Asks the file system; the real one when <see langword="null" />.</param>
    /// <param name="environment">Reads the environment; the real one when <see langword="null" />.</param>
    public CopilotCliLocator(
        IProcessRunner? processRunner = null,
        ICliFileSystem? fileSystem = null,
        IEnvironmentReader? environment = null)
    {
        _processRunner = processRunner ?? new ProcessRunner();
        _fileSystem = fileSystem ?? new CliFileSystem();
        _environment = environment ?? new SystemEnvironmentReader();
    }

    /// <summary>
    ///     Finds the Copilot CLI and checks its version.
    /// </summary>
    /// <param name="overridePath">
    ///     The path chosen in the settings; <see langword="null" />, empty, or white space for automatic detection. A path
    ///     that is set is the only candidate and the search never falls back to automatic detection.
    /// </param>
    /// <param name="refresh">
    ///     <see langword="true" /> to search again even when the answer for this setting is cached (the connection check).
    /// </param>
    /// <param name="cancellationToken">A token that cancels the search and stops the running check.</param>
    /// <returns>What was found; a CLI that cannot be used is a result with a status and advice, not an exception.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled; the cache is left as it was.</exception>
    public async Task<CopilotCliInfo> LocateAsync(
        string? overridePath = null,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        // A path in quotes is how Windows shows a path with spaces, so the quotes are not part of the path
        var key = overridePath?.Trim().Trim('"').Trim() ?? string.Empty;

        // Answer from the cache when the setting is the same and no refresh was asked for
        if (!refresh)
        {
            lock (_cacheLock)
            {
                if (_cached is not null && _cachedKey == key)
                {
                    return _cached;
                }
            }
        }

        // Search, off the calling thread because the PATH may name slow network folders, and remember the answer
        var info = key.Length > 0
            ? await LocateOverrideAsync(key, cancellationToken).ConfigureAwait(false)
            : await LocateAutomaticAsync(cancellationToken).ConfigureAwait(false);
        lock (_cacheLock)
        {
            _cached = info;
            _cachedKey = key;
        }

        return info;
    }

    /// <summary>
    ///     Reads the version from the text that <c>copilot --version</c> prints, for example
    ///     <c>GitHub Copilot CLI 1.0.114.</c>.
    /// </summary>
    /// <remarks>
    ///     The text may continue with a line that suggests an update, and the version may carry a pre-release suffix; both
    ///     are ignored, so only the three numbers count.
    /// </remarks>
    /// <param name="output">The text that the program printed; may be <see langword="null" /> or empty.</param>
    /// <param name="version">The version that was read; <see langword="null" /> when the text has none.</param>
    /// <returns><see langword="true" /> when a version was read.</returns>
    public static bool TryParseVersion(string? output, [NotNullWhen(true)] out Version? version)
    {
        version = null;
        if (string.IsNullOrEmpty(output))
        {
            return false;
        }

        // The three numbers follow the product name; anything else in the text is not a version of this product
        var match = VersionPattern().Match(output);
        if (!match.Success)
        {
            return false;
        }

        // The numbers can be too large for an integer in a hand-made text, which is not a version either
        if (!int.TryParse(match.Groups[1].ValueSpan, out var major)
            || !int.TryParse(match.Groups[2].ValueSpan, out var minor)
            || !int.TryParse(match.Groups[3].ValueSpan, out var build))
        {
            return false;
        }

        version = new Version(major, minor, build);
        return true;
    }

    /// <summary>
    ///     Tells whether a version is new enough, that is, not older than <see cref="MinimumCliVersion" />.
    /// </summary>
    /// <param name="version">The version that the CLI reported.</param>
    /// <returns><see langword="true" /> when the CLI can be used.</returns>
    public static bool IsSupportedVersion(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return version >= MinimumCliVersion;
    }

    /// <summary>
    ///     Tells whether a path lies in the folder of an editor shim.
    /// </summary>
    /// <remarks>
    ///     Only the folders above the file count, not the file name. Both kinds of separators are understood and the
    ///     comparison ignores case.
    /// </remarks>
    /// <param name="path">The full path of the file.</param>
    /// <returns><see langword="true" /> when a folder of the path is a shim folder.</returns>
    public static bool IsShimPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Every segment but the last is a folder; the last is the file name
        var segments = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (segment.StartsWith(ShimDirectoryPrefix, StringComparison.OrdinalIgnoreCase)
                || ShimDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets the pattern that reads the version from the output of <c>--version</c>.
    /// </summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(@"GitHub Copilot CLI (\d+)\.(\d+)\.(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    /// <summary>
    ///     Joins two parts of a Windows path.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="name">The name that follows it.</param>
    /// <returns>The path with exactly one backslash between the parts.</returns>
    private static string Combine(string folder, string name)
    {
        return folder.TrimEnd('\\', '/') + "\\" + name;
    }

    /// <summary>
    ///     Writes a path in one form, so that the same file is always the same text.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The path with backslashes and without a trailing separator.</returns>
    private static string Normalize(string path)
    {
        return path.Replace('/', '\\').TrimEnd('\\');
    }

    /// <summary>
    ///     Gets the last part of a path, its file or folder name.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The text after the last separator.</returns>
    private static string LastSegment(string path)
    {
        var separator = path.LastIndexOfAny(['\\', '/']);
        return separator < 0 ? path : path[(separator + 1)..];
    }

    /// <summary>
    ///     Tells whether a path names an <c>.exe</c> file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true" /> when the file name ends in <c>.exe</c>, in any case.</returns>
    private static bool IsExe(string path)
    {
        return LastSegment(path).EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Checks the one path that the settings name.
    /// </summary>
    /// <param name="path">The path of the settings.</param>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>The result; the path is accepted when it is an existing <c>.exe</c> that reports a new enough version.</returns>
    private async Task<CopilotCliInfo> LocateOverrideAsync(string path, CancellationToken cancellationToken)
    {
        // The settings are the user's explicit choice: shims, folders, and sizes are not judged, but the file must be a
        // program that exists, and the failure is reported as it is, with no fallback to automatic detection
        if (!IsExe(path))
        {
            return Info(
                CopilotCliStatus.NotAnExecutable,
                path,
                null,
                [],
                $"The Copilot CLI path in Settings ({path}) is not an .exe file. Choose the installed copilot.exe, or clear the path to search automatically.");
        }

        if (!_fileSystem.FileExists(path))
        {
            return Info(
                CopilotCliStatus.NotFound,
                path,
                null,
                [],
                $"The Copilot CLI path in Settings ({path}) does not exist. Choose the installed copilot.exe, or clear the path to search automatically.");
        }

        // Validate with --version, like every automatic candidate
        var probe = await ProbeAsync(path, cancellationToken).ConfigureAwait(false);
        if (probe.Version is null)
        {
            return Info(
                CopilotCliStatus.Unreadable,
                path,
                null,
                [new RejectedCliCandidate(path, CopilotCliRejection.Unreadable, probe.Detail)],
                $"The Copilot CLI at {path} did not report its version ({probe.Detail}). Choose another copilot.exe in Settings, or reinstall it with `winget install GitHub.Copilot`.");
        }

        if (!IsSupportedVersion(probe.Version))
        {
            return Info(
                CopilotCliStatus.TooOld,
                path,
                probe.Version,
                [new RejectedCliCandidate(path, CopilotCliRejection.TooOld, $"version {probe.Version}")],
                TooOldMessage(probe.Version));
        }

        return Info(CopilotCliStatus.Found, path, probe.Version, [], FoundMessage(path, probe.Version));
    }

    /// <summary>
    ///     Searches for the CLI by the automatic rule.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the search.</param>
    /// <returns>The result of the search.</returns>
    private async Task<CopilotCliInfo> LocateAutomaticAsync(CancellationToken cancellationToken)
    {
        // Gather and judge the candidates without a program run, on a worker thread
        var rejected = new List<RejectedCliCandidate>();
        var survivors = await Task.Run(() => GatherSurvivors(rejected), cancellationToken).ConfigureAwait(false);

        // Validate the survivors in order; the first that reports a new enough version wins. A failed survivor is kept for
        // the result in case none passes: an old version is a better explanation than an unreadable one
        string? tooOldPath = null;
        Version? tooOldVersion = null;
        string? unreadablePath = null;
        string unreadableDetail = string.Empty;
        foreach (var path in survivors)
        {
            var probe = await ProbeAsync(path, cancellationToken).ConfigureAwait(false);
            if (probe.Version is null)
            {
                rejected.Add(new RejectedCliCandidate(path, CopilotCliRejection.Unreadable, probe.Detail));
                if (unreadablePath is null)
                {
                    unreadablePath = path;
                    unreadableDetail = probe.Detail;
                }
            }
            else if (!IsSupportedVersion(probe.Version))
            {
                rejected.Add(new RejectedCliCandidate(path, CopilotCliRejection.TooOld, $"version {probe.Version}"));
                if (tooOldPath is null)
                {
                    tooOldPath = path;
                    tooOldVersion = probe.Version;
                }
            }
            else
            {
                return Info(CopilotCliStatus.Found, path, probe.Version, rejected, FoundMessage(path, probe.Version));
            }
        }

        // Nothing passed: report the most useful failure
        if (tooOldPath is not null && tooOldVersion is not null)
        {
            return Info(CopilotCliStatus.TooOld, tooOldPath, tooOldVersion, rejected, TooOldMessage(tooOldVersion));
        }

        if (unreadablePath is not null)
        {
            return Info(
                CopilotCliStatus.Unreadable,
                unreadablePath,
                null,
                rejected,
                $"The Copilot CLI at {unreadablePath} did not report its version ({unreadableDetail}). Reinstall it with `winget install GitHub.Copilot` or choose another copilot.exe in Settings.");
        }

        return Info(CopilotCliStatus.NotFound, null, null, rejected, NotFoundMessage(rejected));
    }

    /// <summary>
    ///     Lists the candidates in the order of the rule, follows their links, drops duplicates, and rejects those that are
    ///     not the real CLI.
    /// </summary>
    /// <param name="rejected">Receives every candidate that was rejected, with the reason.</param>
    /// <returns>The full paths of the candidates that were not rejected, in the order to validate them.</returns>
    private List<string> GatherSurvivors(List<RejectedCliCandidate> rejected)
    {
        var survivors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateCandidates())
        {
            // Only a file that is there is a candidate; a PATH folder without the file is not worth a diagnostic
            if (!_fileSystem.FileExists(candidate))
            {
                continue;
            }

            // Judge the file that really runs, and judge each such file once
            var resolved = Normalize(_fileSystem.ResolveLinkTarget(candidate));
            if (!seen.Add(resolved))
            {
                continue;
            }

            var rejection = Classify(resolved, out var detail);
            if (rejection is null)
            {
                survivors.Add(resolved);
            }
            else
            {
                rejected.Add(new RejectedCliCandidate(resolved, rejection.Value, detail));
            }
        }

        return survivors;
    }

    /// <summary>
    ///     Lists the paths where a Copilot CLI may be, in the order of the rule: the WinGet package folders, the WinGet
    ///     <c>Links</c> entry, and every folder of the <c>PATH</c>.
    /// </summary>
    /// <returns>The paths of the candidate files; the files may not exist.</returns>
    private IEnumerable<string> EnumerateCandidates()
    {
        // WinGet installs the real CLI in a package folder and puts a link to it in Links
        var localAppData = _environment.GetVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var winGet = Combine(Combine(localAppData, "Microsoft"), "WinGet");
            var packageFolders = _fileSystem.EnumerateDirectories(Combine(winGet, "Packages"), PackageFolderPattern)
                .OrderByDescending(LastSegment, StringComparer.OrdinalIgnoreCase);
            foreach (var folder in packageFolders)
            {
                yield return Combine(folder, ExecutableName);
            }

            yield return Combine(Combine(winGet, "Links"), ExecutableName);
        }

        // Every folder of the PATH, in order, may hold a copilot.exe; other names (copilot.cmd, copilot.ps1, and a file
        // without an extension) are never programs that can be started directly
        var path = _environment.GetVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var entry in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var folder = entry.Trim('"');
                if (folder.Length > 0)
                {
                    yield return Combine(folder, ExecutableName);
                }
            }
        }
    }

    /// <summary>
    ///     Decides whether a file is the real CLI by what it is, where it is, and how large it is.
    /// </summary>
    /// <param name="resolved">The full path of the file, after links were followed.</param>
    /// <param name="detail">The fact behind the rejection; empty when the file is accepted.</param>
    /// <returns>The reason to reject the file, or <see langword="null" /> when it is not rejected.</returns>
    private CopilotCliRejection? Classify(string resolved, out string detail)
    {
        detail = string.Empty;

        // A link may lead nowhere
        if (!_fileSystem.FileExists(resolved))
        {
            detail = "the file does not exist";
            return CopilotCliRejection.Missing;
        }

        // Only an .exe can be started without a shell
        if (!IsExe(resolved))
        {
            detail = "the file is not an .exe";
            return CopilotCliRejection.NotExe;
        }

        // A file in the folder of an editor is that editor's launcher
        if (IsShimPath(resolved))
        {
            detail = "the file is in the folder of an editor shim";
            return CopilotCliRejection.Shim;
        }

        // The real CLI is a large program; a small file is a launcher whatever it is called
        var length = _fileSystem.GetLength(resolved);
        if (length < MinimumCliBytes)
        {
            detail = string.Create(CultureInfo.InvariantCulture, $"the file is {length:N0} bytes, below {MinimumCliBytes:N0}");
            return CopilotCliRejection.TooSmall;
        }

        return null;
    }

    /// <summary>
    ///     Runs <c>--version</c> on a candidate and reads the version.
    /// </summary>
    /// <param name="path">The full path of the candidate.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>The version, or the reason that none was read.</returns>
    private async Task<VersionProbe> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(path, VersionArguments, VersionProbeTimeout, cancellationToken).ConfigureAwait(false);

        // The reasons say what happened and never repeat what the program printed
        if (!result.Started)
        {
            return new VersionProbe(null, "it could not be started");
        }

        if (result.TimedOut)
        {
            return new VersionProbe(null, $"it did not answer within {VersionProbeTimeout.TotalSeconds:0} seconds");
        }

        return TryParseVersion(result.Output, out var version)
            ? new VersionProbe(version, string.Empty)
            : new VersionProbe(null, "its answer has no version");
    }

    /// <summary>
    ///     Makes a result.
    /// </summary>
    /// <param name="status">What was found.</param>
    /// <param name="path">The path of the CLI or of the failed candidate.</param>
    /// <param name="version">The reported version.</param>
    /// <param name="rejected">The rejected candidates; copied so that the result never changes afterwards.</param>
    /// <param name="message">The sentence for the user.</param>
    /// <returns>The result.</returns>
    private static CopilotCliInfo Info(
        CopilotCliStatus status,
        string? path,
        Version? version,
        IReadOnlyList<RejectedCliCandidate> rejected,
        string message)
    {
        return new CopilotCliInfo(status, path, version, [.. rejected], message);
    }

    /// <summary>
    ///     Makes the sentence for a CLI that can be used.
    /// </summary>
    /// <param name="path">The path of the CLI.</param>
    /// <param name="version">The version that it reported.</param>
    /// <returns>The sentence.</returns>
    private static string FoundMessage(string path, Version version)
    {
        return $"GitHub Copilot CLI {version} found at {path}.";
    }

    /// <summary>
    ///     Makes the sentence for a CLI that is too old.
    /// </summary>
    /// <param name="version">The version that it reported.</param>
    /// <returns>The sentence, with the way to upgrade.</returns>
    private static string TooOldMessage(Version version)
    {
        return $"Copilot CLI {version} is older than {MinimumCliVersion}. Run `winget upgrade GitHub.Copilot`.";
    }

    /// <summary>
    ///     Makes the sentence for a search that found nothing, which names the editor shim when that is all there was.
    /// </summary>
    /// <param name="rejected">The rejected candidates.</param>
    /// <returns>The sentence, with the way to install the CLI.</returns>
    private static string NotFoundMessage(List<RejectedCliCandidate> rejected)
    {
        const string message = "The GitHub Copilot CLI was not found. Install it with `winget install GitHub.Copilot` or set its path in Settings.";
        var shim = rejected.FirstOrDefault(r => r.Reason == CopilotCliRejection.Shim);
        return shim is null ? message : $"{message} Only the VS Code copilot shim was found at {shim.Path}.";
    }

    /// <summary>
    ///     What running <c>--version</c> on a candidate gave.
    /// </summary>
    /// <param name="Version">The version, or <see langword="null" /> when none was read.</param>
    /// <param name="Detail">Why no version was read; empty when one was.</param>
    private sealed record VersionProbe(Version? Version, string Detail);
}

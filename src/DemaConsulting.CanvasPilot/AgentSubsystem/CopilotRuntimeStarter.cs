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

using System.Text.RegularExpressions;
using DemaConsulting.AgentKit.Agents.Copilot;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Starts the GitHub Copilot runtime of the AI library on the Copilot CLI that the user installed, shares that one
///     runtime among all who ask, and turns every way that the start can fail into a sentence for the user.
/// </summary>
/// <remarks>
///     <para>
///         The runtime is started with the installed CLI as its source and the full path that the locator found (the
///         setting of the user first) as the CLI to run, so that nothing is bundled or downloaded. The folder for its data
///         is the one of the request, or the one that the starter was made with, and is never a folder of a project. The
///         starter passes no token and leaves the other sign-in options at the defaults of the AI library, so the runtime
///         signs in from the first source that applies: a token variable of the environment of the application
///         (<c>COPILOT_GITHUB_TOKEN</c>, <c>GH_TOKEN</c>, or <c>GITHUB_TOKEN</c>), the sign-in of the Copilot CLI itself
///         (run <c>copilot</c>, then <c>/login</c>), a sign-in stored in the data folder, or a signed-in GitHub CLI
///         (<c>gh auth login</c>). No host, token provider, or switch is passed. No token is read, kept, or logged here.
///     </para>
///     <para>
///         One runtime is shared, because two runtimes must not use one data folder. A start while the runtime runs on the
///         same CLI and folder returns it; a start for another CLI or folder stops the old runtime and starts a new one;
///         concurrent starts wait for each other, so a runtime is started once. A start that fails is not remembered, so the
///         next request tries again. The way a runtime is started is a function that the constructor accepts, so that a
///         test can stand in for the processes; the default starts a real runtime.
///     </para>
///     <para>
///         The class is safe for concurrent use. Disposing it stops the runtime, and a start after that throws
///         <see cref="ObjectDisposedException" />. The sources it hands out are owned by it and must not be disposed by
///         those who receive them.
///     </para>
/// </remarks>
public sealed partial class CopilotRuntimeStarter : ICopilotRuntimeStarter, IAsyncDisposable
{
    /// <summary>
    ///     The sentence for a runtime that is not signed in. It tells the user to sign in the way that the Copilot CLI
    ///     does (<c>copilot</c>, then <c>/login</c>), which is the way that a user who installed the CLI expects, and names
    ///     the GitHub CLI and the token variables only as alternatives.
    /// </summary>
    public const string NotSignedInMessage =
        "Copilot is not signed in for CanvasPilot. Run `copilot`, then type `/login` to sign in (or sign in with `gh auth login`, or set the COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then choose Check connection.";

    /// <summary>
    ///     The words that introduce the digest of what the AI library found when it looked for a sign-in.
    /// </summary>
    private const string FoundPrefix = " What was found: ";

    /// <summary>
    ///     The beginning of the sentence for a CLI that cannot be used, which the cause follows.
    /// </summary>
    public const string CliPathMessage = "The Copilot CLI path in Settings cannot be used: ";

    /// <summary>
    ///     The sentence for a runtime that could not be started for a reason that the library did not explain.
    /// </summary>
    public const string StartFailedMessage = "The Copilot runtime could not be started. Choose Check connection to try again.";

    /// <summary>
    ///     Starts a runtime from the options and gives it as a source of agents.
    /// </summary>
    private readonly Func<CopilotRuntimeOptions, CancellationToken, Task<ICopilotAgentSource>> _start;

    /// <summary>
    ///     The folder for the data of the runtime when a request names none.
    /// </summary>
    private readonly string? _defaultDataDirectory;

    /// <summary>
    ///     Serializes starts and the disposal.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    ///     The running runtime, or <see langword="null" /> when none runs.
    /// </summary>
    private ICopilotAgentSource? _source;

    /// <summary>
    ///     The CLI and the folder of the running runtime, which tell whether a request is served by it.
    /// </summary>
    private (string CliPath, string DataDirectory)? _key;

    /// <summary>
    ///     Whether the starter was disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CopilotRuntimeStarter" /> class.
    /// </summary>
    /// <param name="defaultDataDirectory">
    ///     The full path of the folder for the data of the runtime when a request names none, or <see langword="null" /> for
    ///     the folder that <see cref="AgentService.GetCopilotDataDirectory" /> gives.
    /// </param>
    /// <param name="start">
    ///     Starts a runtime from the options and gives it as a source of agents; the function that starts a real runtime of
    ///     the AI library when <see langword="null" />.
    /// </param>
    public CopilotRuntimeStarter(
        string? defaultDataDirectory = null,
        Func<CopilotRuntimeOptions, CancellationToken, Task<ICopilotAgentSource>>? start = null)
    {
        _defaultDataDirectory = defaultDataDirectory;
        _start = start ?? StartRealRuntimeAsync;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Thrown when the starter was disposed.</exception>
    public async Task<CopilotStartResult> StartAsync(CopilotStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CliPath);

        var dataDirectory = request.DataDirectory ?? _defaultDataDirectory ?? AgentService.GetCopilotDataDirectory();

        // The options come first, so a path that the library refuses is told before anything is stopped or started
        CopilotRuntimeOptions options;
        try
        {
            options = new CopilotRuntimeOptions(
                dataDirectory,
                gitHubToken: null,
                logger: request.Logger,
                runtimeSource: CopilotRuntimeSource.InstalledCli,
                cliPath: request.CliPath);
        }
        catch (ArgumentException ex)
        {
            return new CopilotStartResult(CopilotStartStatus.Failed, CliPathMessage + FirstSentenceOf(ex));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var key = (options.CliPath ?? request.CliPath, options.DataDirectory);
            if (_source is not null && _key == key)
            {
                return new CopilotStartResult(CopilotStartStatus.Started, StartedMessage(request), _source);
            }

            // A runtime on another CLI or folder is replaced; only one runtime may use the data folder at a time
            await StopAsync().ConfigureAwait(false);

            try
            {
                _source = await _start(options, cancellationToken).ConfigureAwait(false);
                _key = key;
                return new CopilotStartResult(CopilotStartStatus.Started, StartedMessage(request), _source);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or NotSupportedException)
            {
                return Describe(ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Starts a real runtime of the AI library.
    /// </summary>
    /// <param name="options">The options of the runtime.</param>
    /// <param name="cancellationToken">A token that cancels the start.</param>
    /// <returns>The runtime as a source of agents.</returns>
    private static async Task<ICopilotAgentSource> StartRealRuntimeAsync(CopilotRuntimeOptions options, CancellationToken cancellationToken)
    {
        var runtime = await CopilotRuntime.StartAsync(options, cancellationToken).ConfigureAwait(false);
        return new CopilotRuntimeAgentSource(runtime);
    }

    /// <summary>
    ///     Words what the start found, from the exception that the library threw.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The result, which names no source.</returns>
    private static CopilotStartResult Describe(Exception exception)
    {
        var message = exception.Message;

        // The library explains each cause in its own words and has no type for them, so the cause is told by the words
        if (message.Contains("is not signed in", StringComparison.OrdinalIgnoreCase))
        {
            return new CopilotStartResult(CopilotStartStatus.NotSignedIn, NotSignedInMessage + DescribeFindings(message));
        }

        if (message.Contains("too old", StringComparison.OrdinalIgnoreCase)
            || message.Contains("protocol version", StringComparison.OrdinalIgnoreCase))
        {
            return new CopilotStartResult(CopilotStartStatus.CliTooOld, message);
        }

        return new CopilotStartResult(
            CopilotStartStatus.Failed,
            string.IsNullOrWhiteSpace(message) ? StartFailedMessage : message);
    }

    /// <summary>
    ///     Words, in one line, what the AI library found at each place where it looked for a sign-in.
    /// </summary>
    /// <remarks>
    ///     The library lists its findings as indented, numbered lines, one per place it looked, after a line that says it
    ///     tried these places in order, and ends with its own advice on how to sign in. The advice is left out because the
    ///     sentence of the starter gives it, and so is the first place (the token that a host passes), which this
    ///     application never uses. A report message is one line that ends with a full stop, so the findings are joined
    ///     with semicolons. The library has no typed form of the findings, so the lines are recognized by their shape, and
    ///     a text that has none leaves the sentence alone. The library has redacted every token from its text already.
    /// </remarks>
    /// <param name="message">The message of the exception of the library.</param>
    /// <returns>The words that follow the sentence, which begin with a space; an empty string when no findings were found.</returns>
    private static string DescribeFindings(string message)
    {
        // Find the numbered lines, skipping the first place, which is never the one in use here
        var findings = new List<string>();
        foreach (var line in message.Split('\n', StringSplitOptions.TrimEntries))
        {
            var match = FindingLine().Match(line);
            if (match.Success && match.Groups[1].Value != "1")
            {
                var finding = match.Groups[2].Value.Trim().TrimEnd('.', ' ');
                if (finding.Length > 0)
                {
                    findings.Add(finding);
                }
            }
        }

        return findings.Count == 0 ? string.Empty : FoundPrefix + string.Join("; ", findings) + ".";
    }

    /// <summary>
    ///     Words the answer for a runtime that runs.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The sentence.</returns>
    private static string StartedMessage(CopilotStartRequest request)
    {
        return $"GitHub Copilot CLI {request.CliVersion} is running.";
    }

    /// <summary>
    ///     Gets the message of an exception of the library without the part that names the parameter.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The message.</returns>
    private static string FirstSentenceOf(ArgumentException exception)
    {
        return ParameterSuffix().Replace(exception.Message, string.Empty).Trim();
    }

    /// <summary>
    ///     Stops the running runtime, if there is one.
    /// </summary>
    /// <returns>A task that completes when the runtime is stopped.</returns>
    private async Task StopAsync()
    {
        var source = _source;
        _source = null;
        _key = null;
        if (source is not null)
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Matches the part of the message of an argument exception that names the parameter.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\s*\(Parameter '[^']*'\)\s*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ParameterSuffix();

    /// <summary>
    ///     Matches a numbered line of the findings of the AI library, such as <c>3. Copilot CLI sign-in: no configuration</c>.
    /// </summary>
    /// <returns>The pattern; the first group is the number and the second is the finding.</returns>
    [GeneratedRegex(@"^(\d{1,2})\.\s+(\S.*)$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FindingLine();
}

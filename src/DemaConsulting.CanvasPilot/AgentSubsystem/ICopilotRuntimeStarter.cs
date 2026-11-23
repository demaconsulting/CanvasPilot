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

using DemaConsulting.AgentKit.Agents.Copilot;
using Microsoft.Extensions.Logging;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     How a started Copilot runtime signed in to GitHub: where the sign-in came from, as whom, and on which host. The
///     record holds no token, nor any other secret, so it may be shown to the user and written to a log.
/// </summary>
/// <remarks>
///     The values are the ones that the runtime reports. A value that the runtime did not report is
///     <see langword="null" /> (or <see cref="CopilotSignInSource.Unknown" />) and is left out of the sentence that
///     <see cref="Describe" /> words, so that the sentence never claims more than is known.
/// </remarks>
/// <param name="Source">Where the sign-in came from, or <see cref="CopilotSignInSource.Unknown" />.</param>
/// <param name="Login">The GitHub login that the runtime signed in as, or <see langword="null" /> when not known.</param>
/// <param name="Host">
///     The GitHub host that the runtime signed in to as the runtime reports it, for example <c>https://github.com</c>, or
///     <see langword="null" /> when not known.
/// </param>
public sealed record CopilotSignInInfo(CopilotSignInSource Source, string? Login, string? Host)
{
    /// <summary>
    ///     Words how the runtime signed in as one sentence for the user, such as
    ///     <c>Signed in as octocat on github.com (Copilot CLI sign-in).</c>
    /// </summary>
    /// <remarks>
    ///     The user needs to know which account and which kind of sign-in is in use, because a machine may offer several
    ///     (a token variable, the Copilot CLI, the GitHub CLI) and they may be different accounts. The host is shown without
    ///     its scheme, because a name such as <c>github.com</c> is what the user knows. Whatever is not known is left out;
    ///     the method never throws.
    /// </remarks>
    /// <returns>
    ///     The sentence, which ends with a full stop; an empty string when the source, the login, and the host are all
    ///     unknown, which leaves nothing to say.
    /// </returns>
    public string Describe()
    {
        // Each part is added only when known, so the sentence says no more than the runtime reported
        var login = string.IsNullOrWhiteSpace(Login) ? null : Login.Trim();
        var host = HostName(Host);
        var source = SourceName(Source);
        if (login is null && host is null && source is null)
        {
            return string.Empty;
        }

        var sentence = "Signed in";
        if (login is not null)
        {
            sentence += " as " + login;
        }

        if (host is not null)
        {
            sentence += " on " + host;
        }

        if (source is not null)
        {
            sentence += " (" + source + ")";
        }

        return sentence + ".";
    }

    /// <summary>
    ///     Gets the name of a host without its scheme and without a closing slash.
    /// </summary>
    /// <param name="host">The host as the runtime reports it, or <see langword="null" />.</param>
    /// <returns>The name, or <see langword="null" /> when the host is blank.</returns>
    private static string? HostName(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        // The runtime reports a URL such as https://github.com; the user knows the name
        var name = host.Trim();
        var schemeEnd = name.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
        {
            name = name[(schemeEnd + 3)..];
        }

        name = name.TrimEnd('/');
        return name.Length == 0 ? null : name;
    }

    /// <summary>
    ///     Gets the words for a kind of sign-in.
    /// </summary>
    /// <param name="source">The kind of sign-in.</param>
    /// <returns>The words, or <see langword="null" /> for a kind that is not known.</returns>
    private static string? SourceName(CopilotSignInSource source)
    {
        return source switch
        {
            CopilotSignInSource.GitHubToken or CopilotSignInSource.GitHubTokenProvider => "GitHub token",
            CopilotSignInSource.EnvironmentVariable => "token variable",
            CopilotSignInSource.CopilotCli => "Copilot CLI sign-in",
            CopilotSignInSource.DataDirectory => "stored sign-in",
            CopilotSignInSource.GitHubCli => "GitHub CLI sign-in",
            _ => null,
        };
    }
}

/// <summary>
///     What came of asking for the GitHub Copilot runtime.
/// </summary>
public enum CopilotStartStatus
{
    /// <summary>The runtime can be started, or was started.</summary>
    Started,

    /// <summary>
    ///     The Copilot CLI is older than the oldest version that works with the AI library, or speaks another protocol than
    ///     the library does.
    /// </summary>
    CliTooOld,

    /// <summary>
    ///     The runtime started but is not signed in to GitHub: it found no token variable, no sign-in of the Copilot CLI
    ///     (run <c>copilot</c>, then <c>/login</c>), no stored sign-in, and no signed-in GitHub CLI that it could use.
    /// </summary>
    NotSignedIn,

    /// <summary>The runtime could not be started for a reason that the result names.</summary>
    Failed,
}

/// <summary>
///     What the application asks of the starter of the Copilot runtime.
/// </summary>
/// <param name="CliPath">The full path of the Copilot CLI that the locator found.</param>
/// <param name="CliVersion">The version that the CLI reported.</param>
public sealed record CopilotStartRequest(string CliPath, Version CliVersion)
{
    /// <summary>
    ///     Gets the full path of the folder where the runtime keeps its own data (its home and its sessions), or
    ///     <see langword="null" /> for the folder that the starter was made with. The folder is always one below the local
    ///     data of the user and never one inside a project.
    /// </summary>
    public string? DataDirectory { get; init; }

    /// <summary>
    ///     Gets the logger that a started runtime writes to, which records metadata only, or <see langword="null" /> for none.
    /// </summary>
    public ILogger? Logger { get; init; }
}

/// <summary>
///     The answer of the starter of the Copilot runtime.
/// </summary>
/// <param name="Status">What came of the request.</param>
/// <param name="Message">One sentence for the user that says what happened and, when the runtime cannot be used, what to do.</param>
/// <param name="Agents">
///     The running runtime as a source of agents when <paramref name="Status" /> is <see cref="CopilotStartStatus.Started" />;
///     otherwise <see langword="null" />. The runtime is shared by everyone who asks and belongs to the starter, so whoever
///     receives the source must not dispose it.
/// </param>
public sealed record CopilotStartResult(CopilotStartStatus Status, string Message, ICopilotAgentSource? Agents = null)
{
    /// <summary>
    ///     Gets the status of the readiness check that stands for the same cause as <see cref="Status" />.
    /// </summary>
    public AgentReadinessStatus ReadinessStatus => Status switch
    {
        CopilotStartStatus.Started => AgentReadinessStatus.Ready,
        CopilotStartStatus.CliTooOld => AgentReadinessStatus.CopilotCliTooOld,
        CopilotStartStatus.NotSignedIn => AgentReadinessStatus.CopilotNotSignedIn,
        _ => AgentReadinessStatus.CopilotRuntimeUnavailable,
    };
}

/// <summary>
///     Starts, and shares, the GitHub Copilot runtime of the AI library on the Copilot CLI that the user installed. One
///     runtime serves the agents of the conversations and the readiness check, because the runtime keeps its data in one
///     folder that two runtimes must not share.
/// </summary>
public interface ICopilotRuntimeStarter
{
    /// <summary>
    ///     Gives the running runtime on a CLI, starting it when none runs on that CLI yet.
    /// </summary>
    /// <param name="request">Which CLI to use.</param>
    /// <param name="cancellationToken">A token that cancels the start.</param>
    /// <returns>The answer; a runtime that cannot be started is a result, not an exception.</returns>
    Task<CopilotStartResult> StartAsync(CopilotStartRequest request, CancellationToken cancellationToken = default);
}

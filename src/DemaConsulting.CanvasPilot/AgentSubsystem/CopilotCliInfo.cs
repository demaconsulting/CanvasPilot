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

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     What the search for the GitHub Copilot CLI found.
/// </summary>
public enum CopilotCliStatus
{
    /// <summary>A Copilot CLI was found, reports a version, and is new enough.</summary>
    Found,

    /// <summary>No usable Copilot CLI was found, or the path that the settings name does not exist.</summary>
    NotFound,

    /// <summary>The path that the settings name is not an <c>.exe</c> file.</summary>
    NotAnExecutable,

    /// <summary>A Copilot CLI was found but is older than the oldest version that works, and no newer one was found.</summary>
    TooOld,

    /// <summary>
    ///     A candidate was found but could not be started, did not answer in time, or did not report a version, and no other
    ///     candidate was usable.
    /// </summary>
    Unreadable,
}

/// <summary>
///     Why the search did not use a candidate for the Copilot CLI.
/// </summary>
public enum CopilotCliRejection
{
    /// <summary>The path is not a file (a link that points nowhere, for example).</summary>
    Missing,

    /// <summary>The file is not an <c>.exe</c> (the target of a link, for example, is a script).</summary>
    NotExe,

    /// <summary>The file is in a folder of an editor shim that forwards to the real CLI, not in an installation of it.</summary>
    Shim,

    /// <summary>The file is smaller than any real Copilot CLI, so it is a launcher and not the CLI itself.</summary>
    TooSmall,

    /// <summary>The file could not be started, did not answer in time, or did not report a version.</summary>
    Unreadable,

    /// <summary>The file reports a version older than the oldest that works.</summary>
    TooOld,
}

/// <summary>
///     A candidate that the search did not use, and why, for the diagnostics of a failed search.
/// </summary>
/// <param name="Path">The full path of the candidate, after links were followed.</param>
/// <param name="Reason">Why the candidate was not used.</param>
/// <param name="Detail">One short sentence with the facts behind the reason (a size or a version); never program output.</param>
public sealed record RejectedCliCandidate(string Path, CopilotCliRejection Reason, string Detail);

/// <summary>
///     The result of the search for the GitHub Copilot CLI: what was found, and the advice for the user when it cannot be
///     used.
/// </summary>
public sealed record CopilotCliInfo
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CopilotCliInfo" /> record.
    /// </summary>
    /// <param name="status">What the search found.</param>
    /// <param name="path">The full path of the CLI, or of the candidate that failed; <see langword="null" /> when none.</param>
    /// <param name="version">The version that the CLI reported; <see langword="null" /> when it did not report one.</param>
    /// <param name="rejected">The candidates that were not used, in the order that they were considered; may be empty.</param>
    /// <param name="message">One sentence for the user that says what was found and, when it is not usable, what to do.</param>
    /// <exception cref="ArgumentException">Thrown when the message is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the message or the list of rejected candidates is <see langword="null" />.</exception>
    public CopilotCliInfo(
        CopilotCliStatus status,
        string? path,
        Version? version,
        IReadOnlyList<RejectedCliCandidate> rejected,
        string message)
    {
        ArgumentNullException.ThrowIfNull(rejected);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Status = status;
        Path = path;
        Version = version;
        Rejected = rejected;
        Message = message;
    }

    /// <summary>
    ///     Gets what the search found.
    /// </summary>
    public CopilotCliStatus Status { get; }

    /// <summary>
    ///     Gets the full path of the CLI that was found, or of the candidate that failed the check; <see langword="null" />
    ///     when there was none.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    ///     Gets the version that the CLI reported; <see langword="null" /> when it did not report one.
    /// </summary>
    public Version? Version { get; }

    /// <summary>
    ///     Gets the candidates that were not used, in the order that they were considered, with the reason for each.
    /// </summary>
    public IReadOnlyList<RejectedCliCandidate> Rejected { get; }

    /// <summary>
    ///     Gets one sentence for the user that says what was found and, when the CLI cannot be used, what to do.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets a value indicating whether a Copilot CLI that can be used was found.
    /// </summary>
    public bool IsFound => Status == CopilotCliStatus.Found;
}

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
///     What a readiness check found about the AI provider that the settings choose.
/// </summary>
public enum AgentReadinessStatus
{
    /// <summary>The provider can be used.</summary>
    Ready,

    /// <summary>The settings do not yet say enough to use a provider.</summary>
    NotConfigured,

    /// <summary>The GitHub Copilot CLI was not found.</summary>
    CopilotCliMissing,

    /// <summary>The GitHub Copilot CLI is older than the oldest version that works.</summary>
    CopilotCliTooOld,

    /// <summary>No credentials that the AI library can use were found for GitHub Copilot.</summary>
    CopilotNotSignedIn,

    /// <summary>The GitHub Copilot runtime could not be started.</summary>
    CopilotRuntimeUnavailable,

    /// <summary>The Ollama server did not answer.</summary>
    OllamaUnreachable,

    /// <summary>The Ollama server does not have the chosen model.</summary>
    OllamaModelMissing,

    /// <summary>The Ollama context window is smaller than recommended; the provider can still be used.</summary>
    OllamaContextTooSmall,

    /// <summary>
    ///     The model or the reasoning effort of the settings cannot be used, for example because the model cannot call
    ///     tools, is not offered to the user, or does not offer the effort; the provider cannot be used until the settings
    ///     change.
    /// </summary>
    ModelSettingsInvalid,

    /// <summary>
    ///     The chosen model does not accept pictures; the provider can still be used, but the model cannot see the drawings.
    /// </summary>
    ModelLacksVision,
}

/// <summary>
///     The result of a readiness check: a status and one sentence for the user that says what was found and, when the
///     provider cannot be used, what to do about it.
/// </summary>
public sealed record AgentReadinessReport
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentReadinessReport" /> record.
    /// </summary>
    /// <param name="status">What the check found.</param>
    /// <param name="message">One sentence for the user; never empty.</param>
    /// <exception cref="ArgumentException">Thrown when the message is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the message is <see langword="null" />.</exception>
    public AgentReadinessReport(AgentReadinessStatus status, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Status = status;
        Message = message;
    }

    /// <summary>
    ///     Gets what the check found.
    /// </summary>
    public AgentReadinessStatus Status { get; }

    /// <summary>
    ///     Gets one sentence for the user that says what was found and what to do about it.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets a value indicating whether a turn can be run: the provider is ready, or it is ready with a warning that
    ///     does not stop it.
    /// </summary>
    public bool IsUsable => Status is AgentReadinessStatus.Ready or AgentReadinessStatus.OllamaContextTooSmall
        or AgentReadinessStatus.ModelLacksVision;
}

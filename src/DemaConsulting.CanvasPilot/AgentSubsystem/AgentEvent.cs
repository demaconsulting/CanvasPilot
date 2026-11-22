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
///     Why a turn failed, in terms that do not depend on the AI provider.
/// </summary>
public enum AgentFailureKind
{
    /// <summary>The provider is not ready; the readiness report says why.</summary>
    NotReady,

    /// <summary>The provider refused the credentials.</summary>
    Authentication,

    /// <summary>The runtime and the library do not speak the same protocol version.</summary>
    ProtocolMismatch,

    /// <summary>The chosen model does not exist on the provider.</summary>
    ModelNotFound,

    /// <summary>The connection to the provider was lost or could not be made.</summary>
    ConnectionLost,

    /// <summary>Another turn is still running; only one turn runs at a time.</summary>
    Busy,

    /// <summary>Anything else; the message holds what is known.</summary>
    Unexpected,
}

/// <summary>
///     One thing that happened during a turn, in terms that do not depend on the AI provider. A turn is a stream of
///     these events that ends with <see cref="Completed" /> or <see cref="Failed" />; a turn that the caller cancels
///     simply ends.
/// </summary>
public abstract record AgentEvent
{
    /// <summary>
    ///     The longest summary, in characters, that a tool event carries.
    /// </summary>
    public const int MaxSummaryLength = 200;

    /// <summary>
    ///     Prevents events from outside this subsystem from deriving from this record.
    /// </summary>
    private protected AgentEvent()
    {
    }

    /// <summary>
    ///     Shortens a text to one that a tool event may carry: trimmed, and cut to <see cref="MaxSummaryLength" />
    ///     characters with a trailing <c>...</c> when it was longer.
    /// </summary>
    /// <param name="text">The text, or <see langword="null" /> for none.</param>
    /// <returns>The text for a summary; empty when there is none.</returns>
    public static string Summarize(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        return trimmed.Length <= MaxSummaryLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, MaxSummaryLength - 3), "...");
    }

    /// <summary>
    ///     A piece of the answer text, to be appended to the answer so far.
    /// </summary>
    public sealed record TextDelta : AgentEvent
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TextDelta" /> record.
        /// </summary>
        /// <param name="text">The piece of text.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text is <see langword="null" />.</exception>
        public TextDelta(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            Text = text;
        }

        /// <summary>
        ///     Gets the piece of text.
        /// </summary>
        public string Text { get; }
    }

    /// <summary>
    ///     The copilot started to call a tool.
    /// </summary>
    public sealed record ToolStarted : AgentEvent
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ToolStarted" /> record.
        /// </summary>
        /// <param name="callId">The id that ties this event to the <see cref="ToolFinished" /> of the same call.</param>
        /// <param name="name">The name of the tool.</param>
        /// <param name="argumentSummary">A short description of the arguments; shortened to <see cref="MaxSummaryLength" />.</param>
        /// <exception cref="ArgumentException">Thrown when the call id or the name is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the call id or the name is <see langword="null" />.</exception>
        public ToolStarted(string callId, string name, string? argumentSummary)
        {
            ArgumentException.ThrowIfNullOrEmpty(callId);
            ArgumentException.ThrowIfNullOrEmpty(name);

            CallId = callId;
            Name = name;
            ArgumentSummary = Summarize(argumentSummary);
        }

        /// <summary>
        ///     Gets the id of the call.
        /// </summary>
        public string CallId { get; }

        /// <summary>
        ///     Gets the name of the tool.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets a short description of the arguments, at most <see cref="MaxSummaryLength" /> characters.
        /// </summary>
        public string ArgumentSummary { get; }
    }

    /// <summary>
    ///     A tool call ended.
    /// </summary>
    public sealed record ToolFinished : AgentEvent
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ToolFinished" /> record.
        /// </summary>
        /// <param name="callId">The id of the call, the same as that of its <see cref="ToolStarted" />.</param>
        /// <param name="name">The name of the tool.</param>
        /// <param name="succeeded"><see langword="true" /> when the tool did what was asked.</param>
        /// <param name="summary">A short description of the result; shortened to <see cref="MaxSummaryLength" />.</param>
        /// <param name="hasImage"><see langword="true" /> when the result carried a picture.</param>
        /// <exception cref="ArgumentException">Thrown when the call id or the name is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the call id or the name is <see langword="null" />.</exception>
        public ToolFinished(string callId, string name, bool succeeded, string? summary, bool hasImage)
        {
            ArgumentException.ThrowIfNullOrEmpty(callId);
            ArgumentException.ThrowIfNullOrEmpty(name);

            CallId = callId;
            Name = name;
            Succeeded = succeeded;
            Summary = Summarize(summary);
            HasImage = hasImage;
        }

        /// <summary>
        ///     Gets the id of the call.
        /// </summary>
        public string CallId { get; }

        /// <summary>
        ///     Gets the name of the tool.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets a value indicating whether the tool did what was asked.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        ///     Gets a short description of the result, at most <see cref="MaxSummaryLength" /> characters.
        /// </summary>
        public string Summary { get; }

        /// <summary>
        ///     Gets a value indicating whether the result carried a picture.
        /// </summary>
        public bool HasImage { get; }
    }

    /// <summary>
    ///     The copilot created or replaced a file of the project.
    /// </summary>
    /// <param name="RelativePath">The path of the file, relative to the project folder.</param>
    /// <param name="Replaced"><see langword="true" /> when the file existed and was replaced; otherwise it was created.</param>
    public sealed record FileWritten(string RelativePath, bool Replaced) : AgentEvent;

    /// <summary>
    ///     How much the provider counted for the turn so far.
    /// </summary>
    /// <param name="InputTokens">The tokens sent to the model, or <see langword="null" /> when not reported.</param>
    /// <param name="OutputTokens">The tokens the model wrote, or <see langword="null" /> when not reported.</param>
    public sealed record Usage(long? InputTokens, long? OutputTokens) : AgentEvent;

    /// <summary>
    ///     The turn ended normally; no event follows.
    /// </summary>
    public sealed record Completed : AgentEvent;

    /// <summary>
    ///     The turn failed; no event follows.
    /// </summary>
    public sealed record Failed : AgentEvent
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Failed" /> record.
        /// </summary>
        /// <param name="kind">Why the turn failed.</param>
        /// <param name="message">What happened and what to do about it, for the user; never empty.</param>
        /// <exception cref="ArgumentException">Thrown when the message is empty or white space.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the message is <see langword="null" />.</exception>
        public Failed(AgentFailureKind kind, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            Kind = kind;
            Message = message;
        }

        /// <summary>
        ///     Gets why the turn failed.
        /// </summary>
        public AgentFailureKind Kind { get; }

        /// <summary>
        ///     Gets what happened and what to do about it, for the user.
        /// </summary>
        public string Message { get; }
    }
}

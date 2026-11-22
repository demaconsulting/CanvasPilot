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

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     How serious a notice in the transcript is.
/// </summary>
public enum NoticeSeverity
{
    /// <summary>The application tells the user something; nothing went wrong.</summary>
    Information,

    /// <summary>Something went wrong; the notice says what and what to do.</summary>
    Error,
}

/// <summary>
///     A notice or an error that the application says in the conversation: what happened, what to do about it, and
///     whether the Settings window can help.
/// </summary>
/// <remarks>
///     The row is immutable. It is not a message of the user or of the copilot, so it is not part of the text that
///     restores a conversation.
/// </remarks>
public sealed class NoticeRowViewModel : TranscriptRowViewModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NoticeRowViewModel" /> class.
    /// </summary>
    /// <param name="severity">How serious the notice is.</param>
    /// <param name="message">What happened, which is not empty.</param>
    /// <param name="remedy">What to do about it, or an empty string for none.</param>
    /// <param name="canOpenSettings">Whether the row offers to open the settings.</param>
    /// <exception cref="ArgumentException">Thrown when the message is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the message or the remedy is <see langword="null" />.</exception>
    public NoticeRowViewModel(NoticeSeverity severity, string message, string remedy = "", bool canOpenSettings = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(remedy);

        Severity = severity;
        Message = message;
        Remedy = remedy;
        CanOpenSettings = canOpenSettings;
    }

    /// <inheritdoc />
    public override TranscriptRowKind Kind => TranscriptRowKind.Notice;

    /// <summary>
    ///     Gets how serious the notice is.
    /// </summary>
    public NoticeSeverity Severity { get; }

    /// <summary>
    ///     Gets a value indicating whether something went wrong.
    /// </summary>
    public bool IsError => Severity == NoticeSeverity.Error;

    /// <summary>
    ///     Gets what happened.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets what to do about it, or an empty string.
    /// </summary>
    public string Remedy { get; }

    /// <summary>
    ///     Gets a value indicating whether there is something to do about it.
    /// </summary>
    public bool HasRemedy => Remedy.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether the row offers to open the settings.
    /// </summary>
    public bool CanOpenSettings { get; }
}

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

using System.Globalization;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     A line of the transcript about one tool that the AI copilot called, such as <c>Created `cover.svg`</c> or
///     <c>Rendered `cover.svg`</c>: the name of the tool, one sentence about what it did, and whether it worked.
/// </summary>
/// <remarks>
///     The row is made when the call starts and says what is under way; it is finished when the result arrives, or
///     abandoned when the turn ends before the result does. The sentence is made from the name of the tool and its
///     arguments by the chat turn runner and holds no file contents.
/// </remarks>
public sealed class ToolActivityRowViewModel : TranscriptRowViewModel
{
    /// <summary>
    ///     The shortest time that is shown, because a call that took less says nothing.
    /// </summary>
    private static readonly TimeSpan MinimumShownElapsed = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///     The sentence about what the tool did.
    /// </summary>
    private string _summary;

    /// <summary>
    ///     Whether the call has no result yet.
    /// </summary>
    private bool _isRunning;

    /// <summary>
    ///     Whether the tool did what was asked.
    /// </summary>
    private bool _succeeded;

    /// <summary>
    ///     Whether the result carried a picture.
    /// </summary>
    private bool _hasImage;

    /// <summary>
    ///     The clock that times the call, or <see langword="null" /> for a row that was restored.
    /// </summary>
    private readonly TimeProvider? _clock;

    /// <summary>
    ///     The time stamp of the start of the call.
    /// </summary>
    private readonly long _started;

    /// <summary>
    ///     How long the call took, or <see cref="TimeSpan.Zero" /> while it is not known.
    /// </summary>
    private TimeSpan _elapsed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ToolActivityRowViewModel" /> class for a call that has started.
    /// </summary>
    /// <param name="callId">The id of the call, which ties the row to the result.</param>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="argumentSummary">The short description of the arguments that the call came with.</param>
    /// <param name="summary">The sentence about what is under way.</param>
    /// <param name="clock">The clock that times the call, or <see langword="null" /> for the system clock.</param>
    /// <exception cref="ArgumentException">Thrown when the call id or the name is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public ToolActivityRowViewModel(string callId, string toolName, string argumentSummary, string summary, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentNullException.ThrowIfNull(argumentSummary);
        ArgumentNullException.ThrowIfNull(summary);

        CallId = callId;
        ToolName = toolName;
        ArgumentSummary = argumentSummary;
        _summary = summary;
        _isRunning = true;
        _succeeded = true;
        _clock = clock ?? TimeProvider.System;
        _started = _clock.GetTimestamp();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ToolActivityRowViewModel" /> class for a call that has ended, such
    ///     as one that was restored.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="summary">The sentence about what the tool did.</param>
    /// <param name="succeeded">Whether the tool did what was asked.</param>
    /// <exception cref="ArgumentException">Thrown when the name is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public ToolActivityRowViewModel(string toolName, string summary, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentNullException.ThrowIfNull(summary);

        CallId = string.Empty;
        ToolName = toolName;
        ArgumentSummary = string.Empty;
        _summary = summary;
        _isRunning = false;
        _succeeded = succeeded;
    }

    /// <inheritdoc />
    public override TranscriptRowKind Kind => TranscriptRowKind.Tool;

    /// <summary>
    ///     Gets the id of the call, or an empty string for a row that was restored.
    /// </summary>
    public string CallId { get; }

    /// <summary>
    ///     Gets the name of the tool.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    ///     Gets the short description of the arguments that the call came with, or an empty string.
    /// </summary>
    public string ArgumentSummary { get; }

    /// <summary>
    ///     Gets the sentence about what the tool did, or what is under way while <see cref="IsRunning" />.
    /// </summary>
    public string Summary => _summary;

    /// <summary>
    ///     Gets a value indicating whether the call has no result yet.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    ///     Gets a value indicating whether the tool did what was asked. A call that has not ended counts as working.
    /// </summary>
    public bool Succeeded => _succeeded;

    /// <summary>
    ///     Gets a value indicating whether the call has ended and did what was asked, which the row shows as a check.
    /// </summary>
    public bool ShowsSuccess => !_isRunning && _succeeded;

    /// <summary>
    ///     Gets a value indicating whether the call has ended and did not do what was asked, which the row shows as a cross.
    /// </summary>
    public bool ShowsFailure => !_isRunning && !_succeeded;

    /// <summary>
    ///     Gets a value indicating whether the result carried a picture.
    /// </summary>
    public bool HasImage => _hasImage;

    /// <summary>
    ///     Gets how long the call took in seconds with one decimal, such as <c>0.8 s</c>, or an empty string for a call that is
    ///     under way, that was restored, that was abandoned, or that took less than a tenth of a second.
    /// </summary>
    public string ElapsedText => _elapsed >= MinimumShownElapsed ? string.Format(CultureInfo.CurrentCulture, "{0:0.0} s", _elapsed.TotalSeconds) : string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the report that a file was written has been made for this call.
    /// </summary>
    public bool HasFileReport { get; private set; }

    /// <summary>
    ///     Ends the call with its result.
    /// </summary>
    /// <param name="summary">The sentence about what the tool did. A report of a written file that was made before is kept.</param>
    /// <param name="succeeded">Whether the tool did what was asked.</param>
    /// <param name="hasImage">Whether the result carried a picture.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="summary" /> is <see langword="null" />.</exception>
    public void Finish(string summary, bool succeeded, bool hasImage)
    {
        ArgumentNullException.ThrowIfNull(summary);

        // The answer of a write that failed must not hide behind the earlier report of the file that was written
        if (!HasFileReport || !succeeded)
        {
            SetSummary(summary);
        }

        _hasImage = hasImage;
        _succeeded = succeeded;
        _isRunning = false;
        _elapsed = _clock is null ? TimeSpan.Zero : _clock.GetElapsedTime(_started);
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(Succeeded));
        OnPropertyChanged(nameof(IsRunning));
        OnShapeChanged();
    }

    /// <summary>
    ///     Says that the tool wrote a file, so that the row reads <c>Created `x.svg`</c> or <c>Replaced `x.svg`</c>. The
    ///     report counts as success.
    /// </summary>
    /// <param name="summary">The sentence about the file.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="summary" /> is <see langword="null" />.</exception>
    public void ReportFile(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        HasFileReport = true;
        SetSummary(summary);
        _succeeded = true;
        OnPropertyChanged(nameof(HasFileReport));
        OnPropertyChanged(nameof(Succeeded));
        OnShapeChanged();
    }

    /// <summary>
    ///     Ends a call that has no result because the turn ended first. Nothing happens when the call has ended.
    /// </summary>
    /// <param name="summary">The sentence about what happened.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="summary" /> is <see langword="null" />.</exception>
    public void Abandon(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (!_isRunning)
        {
            return;
        }

        SetSummary(summary);
        _succeeded = false;
        _isRunning = false;
        OnPropertyChanged(nameof(Succeeded));
        OnPropertyChanged(nameof(IsRunning));
        OnShapeChanged();
    }

    /// <summary>
    ///     Tells the observers that the check and the cross may have changed.
    /// </summary>
    private void OnShapeChanged()
    {
        OnPropertyChanged(nameof(ShowsSuccess));
        OnPropertyChanged(nameof(ShowsFailure));
    }

    /// <summary>
    ///     Sets the sentence and tells the observers.
    /// </summary>
    /// <param name="summary">The sentence.</param>
    private void SetSummary(string summary)
    {
        _summary = summary;
        OnPropertyChanged(nameof(Summary));
    }
}

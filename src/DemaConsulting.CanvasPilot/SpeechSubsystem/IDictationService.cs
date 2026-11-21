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

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     Why dictation can or cannot be used right now. Each reason has its own user-readable message, because what
///     the user must do about it differs.
/// </summary>
public enum DictationAvailabilityReason
{
    /// <summary>Dictation can be used.</summary>
    Available,

    /// <summary>No speech-to-text model is chosen on the Speech page of the settings.</summary>
    NoModelChosen,

    /// <summary>The chosen speech-to-text model is not installed, is unknown, or cannot be listed.</summary>
    ModelNotInstalled,

    /// <summary>The application is missing the native files that run speech recognition.</summary>
    SpeechRuntimeMissing,

    /// <summary>No microphone can be used.</summary>
    NoCaptureDevice,

    /// <summary>The model is installed, but its speech engine could not be started.</summary>
    EngineUnavailable,
}

/// <summary>
///     Whether dictation can be used, and when it cannot, why and what the user can do about it.
/// </summary>
/// <param name="Reason">Why dictation can or cannot be used.</param>
/// <param name="Message">
///     The explanation for the user, which names the next step; an empty string when dictation is available.
/// </param>
public sealed record DictationAvailability(DictationAvailabilityReason Reason, string Message = "")
{
    /// <summary>
    ///     Gets a value indicating whether dictation can be used.
    /// </summary>
    public bool IsAvailable => Reason == DictationAvailabilityReason.Available;

    /// <summary>
    ///     Gets the availability of dictation that can be used.
    /// </summary>
    public static DictationAvailability Ready { get; } = new(DictationAvailabilityReason.Available);
}

/// <summary>
///     The outcome of asking dictation to start listening.
/// </summary>
/// <param name="Session">
///     The session that is listening, which the caller must end, or <see langword="null" /> when listening did not
///     start.
/// </param>
/// <param name="Availability">
///     Why listening did not start when <paramref name="Session" /> is <see langword="null" />. It says that
///     dictation is available when the request was ignored because the user is already dictating.
/// </param>
public sealed record DictationStart(IDictationSession? Session, DictationAvailability Availability)
{
    /// <summary>
    ///     Gets a value indicating whether listening started.
    /// </summary>
    public bool IsListening => Session is not null;
}

/// <summary>
///     One hold of a talk button or key: from the moment the application starts listening to the moment the user
///     lets go. A session is used once.
/// </summary>
/// <remarks>
///     Not thread-safe for concurrent <see cref="EndAsync" /> calls from different threads, although a second call
///     after the first has finished is allowed and returns the same text.
/// </remarks>
public interface IDictationSession : IAsyncDisposable
{
    /// <summary>
    ///     Raised when <see cref="PartialText" /> has changed. It is raised on a background thread, so a handler
    ///     that updates the user interface must marshal to the interface thread.
    /// </summary>
    event EventHandler? PartialTextChanged;

    /// <summary>
    ///     Gets the words recognized so far: the finished sentences followed by the sentence that is still being
    ///     recognized, which may change as more is heard.
    /// </summary>
    string PartialText { get; }

    /// <summary>
    ///     Gets the explanation of a failure of the recognition while it was listening, or an empty string. The text
    ///     recognized before the failure is still returned by <see cref="EndAsync" />.
    /// </summary>
    string Problem { get; }

    /// <summary>
    ///     Stops listening, lets the recognizer finish the audio it has already heard, releases the microphone and
    ///     the recognition session, and returns the final text.
    /// </summary>
    /// <param name="cancellationToken">Gives up waiting for the recognizer to finish; the text so far is returned.</param>
    /// <returns>
    ///     The recognized text with the finished sentences joined by a single space and trimmed, or an empty string
    ///     when nothing was recognized.
    /// </returns>
    Task<string> EndAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Dictation: turns what the user says while holding a talk button or key into text. The speech engine is loaded
///     once and reused, because loading it is the expensive step. Every state in which dictation cannot work, such as
///     no model, no microphone, or missing native speech files, is a result with an explanation, never an exception.
/// </summary>
public interface IDictationService
{
    /// <summary>
    ///     Says whether dictation can be used with the speech settings in effect, without loading the speech engine.
    ///     It checks, in this order, that a speech-to-text model is chosen and installed, that the native speech
    ///     files are present, and that a microphone can be used.
    /// </summary>
    /// <returns>The availability, with an explanation when dictation cannot be used.</returns>
    DictationAvailability CheckAvailability();

    /// <summary>
    ///     Loads the speech engine ahead of time, so that the first press of the talk button does not wait for it.
    ///     Nothing is loaded when dictation is not available, and a failure is returned, never thrown.
    /// </summary>
    /// <param name="cancellationToken">Cancels the preparation.</param>
    /// <returns>Whether dictation can be used after the preparation, with the reason when it cannot.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken" /> is cancelled.</exception>
    Task<DictationAvailability> PrepareAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Starts listening. A request while a session is already listening is ignored.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Gives up the start, for example because the user already let go while the engine was loading.
    /// </param>
    /// <returns>The listening session, or why there is none.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken" /> is cancelled.</exception>
    Task<DictationStart> BeginAsync(CancellationToken cancellationToken = default);
}

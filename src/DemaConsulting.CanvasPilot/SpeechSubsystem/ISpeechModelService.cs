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

using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     The speech models found by one enumeration, and why the catalog could not be read when it could not.
/// </summary>
/// <param name="Models">Every model the catalog knows, with its current install state.</param>
/// <param name="Problem">The explanation when the catalog could not be read, or <see langword="null" />.</param>
public sealed record SpeechModelList(IReadOnlyList<SpeechModelDescriptor> Models, string? Problem = null)
{
    /// <summary>
    ///     Gets a value indicating whether the catalog could be read.
    /// </summary>
    public bool IsAvailable => Problem is null;

    /// <summary>
    ///     Creates a list for a catalog that could not be read.
    /// </summary>
    /// <param name="problem">The explanation.</param>
    /// <returns>A list with no models and the explanation.</returns>
    public static SpeechModelList Unavailable(string problem)
    {
        return new SpeechModelList([], problem);
    }
}

/// <summary>
///     How a model download ended.
/// </summary>
public enum SpeechModelDownloadStatus
{
    /// <summary>The model was downloaded, verified, and installed.</summary>
    Installed,

    /// <summary>The caller cancelled the download; nothing was installed.</summary>
    Canceled,

    /// <summary>The download did not install the model; the report carries the reason.</summary>
    Failed,
}

/// <summary>
///     The end of a model download, as a result that the user interface can show.
/// </summary>
/// <param name="Status">How the download ended.</param>
/// <param name="Message">The explanation of a failure or cancellation, or an empty string after an install.</param>
public sealed record SpeechModelDownloadReport(SpeechModelDownloadStatus Status, string Message = "")
{
    /// <summary>
    ///     Gets a value indicating whether the model was installed.
    /// </summary>
    public bool IsInstalled => Status == SpeechModelDownloadStatus.Installed;
}

/// <summary>
///     Why a speech recognizer could not be loaded.
/// </summary>
public enum RecognizerLoadStatus
{
    /// <summary>The recognizer is loaded and can create sessions.</summary>
    Loaded,

    /// <summary>
    ///     The model is not in the catalog, is not a speech-to-text model, or is not installed in the model store.
    /// </summary>
    ModelNotInstalled,

    /// <summary>
    ///     The model is installed but its speech engine could not be started, for example because the speech runtime
    ///     failed to load.
    /// </summary>
    EngineUnavailable,
}

/// <summary>
///     The outcome of loading a speech recognizer: the engine, or the reason there is none.
/// </summary>
/// <param name="Status">How the load ended.</param>
/// <param name="Engine">
///     The loaded engine, which the caller owns and must dispose, or <see langword="null" /> when
///     <paramref name="Status" /> is not <see cref="RecognizerLoadStatus.Loaded" />.
/// </param>
/// <param name="Problem">The explanation when there is no engine, or an empty string.</param>
public sealed record RecognizerLoadResult(RecognizerLoadStatus Status, ISpeechRecognizerEngine? Engine, string Problem = "")
{
    /// <summary>
    ///     Gets a value indicating whether an engine was loaded.
    /// </summary>
    public bool IsLoaded => Status == RecognizerLoadStatus.Loaded;

    /// <summary>
    ///     Creates a result for a loaded engine.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The result.</returns>
    public static RecognizerLoadResult Success(ISpeechRecognizerEngine engine)
    {
        return new RecognizerLoadResult(RecognizerLoadStatus.Loaded, engine);
    }

    /// <summary>
    ///     Creates a result for an engine that could not be loaded.
    /// </summary>
    /// <param name="status">Why; not <see cref="RecognizerLoadStatus.Loaded" />.</param>
    /// <param name="problem">The explanation.</param>
    /// <returns>The result.</returns>
    public static RecognizerLoadResult Failure(RecognizerLoadStatus status, string problem)
    {
        return new RecognizerLoadResult(status, null, problem);
    }
}

/// <summary>
///     Lists the speech models, downloads them into the shared per-user model store, and loads the installed
///     speech-to-text models. Failures are results and messages, never exceptions, so a blocked network, a damaged
///     store, or a missing speech runtime cannot crash the application.
/// </summary>
public interface ISpeechModelService
{
    /// <summary>
    ///     Loads the speech engine of an installed speech-to-text model. Loading is the expensive step of speech
    ///     recognition, so the caller keeps the engine for as long as it may recognize speech.
    /// </summary>
    /// <param name="modelId">The identifier of the speech-to-text model.</param>
    /// <param name="storedParameters">
    ///     The stored values of the model's tunable settings as invariant-culture strings, by parameter identifier, or
    ///     <see langword="null" /> for the model's defaults. A value that is not a value of the parameter is ignored.
    /// </param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The engine, or no engine with the reason; a cancelled token ends in an exception.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken" /> is cancelled.</exception>
    Task<RecognizerLoadResult> LoadRecognizerAsync(
        string modelId,
        IReadOnlyDictionary<string, string>? storedParameters,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Lists the models the catalog knows with their current install state. This reads the local store only and
    ///     does not use the network.
    /// </summary>
    /// <returns>The models, or an empty list with the problem when the catalog cannot be read.</returns>
    SpeechModelList Enumerate();

    /// <summary>
    ///     Downloads, verifies, and installs one model.
    /// </summary>
    /// <param name="modelId">The identifier of a model in the catalog.</param>
    /// <param name="progress">Receives progress as the files transfer, or <see langword="null" /> to ignore it.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>How the download ended; a cancelled token gives <see cref="SpeechModelDownloadStatus.Canceled" />.</returns>
    Task<SpeechModelDownloadReport> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken);
}

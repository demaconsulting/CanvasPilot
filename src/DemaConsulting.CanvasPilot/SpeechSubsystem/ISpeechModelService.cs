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
///     Lists the speech models and downloads them into the shared per-user model store. Failures are results and
///     messages, never exceptions, so a blocked network or a damaged store cannot crash the application.
/// </summary>
public interface ISpeechModelService
{
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

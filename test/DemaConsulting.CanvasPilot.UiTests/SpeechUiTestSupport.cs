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

using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     An audio device service for UI tests that reports the snapshot a test sets and counts refreshes.
/// </summary>
internal sealed class FakeAudioDeviceService : IAudioDeviceService
{
    /// <summary>
    ///     Gets or sets the snapshot that both operations return.
    /// </summary>
    public AudioDeviceSnapshot Snapshot { get; set; } = new([], []);

    /// <summary>
    ///     Gets how many refreshes were requested.
    /// </summary>
    public int Refreshes { get; private set; }

    /// <inheritdoc />
    public AudioDeviceSnapshot Enumerate()
    {
        return Snapshot;
    }

    /// <inheritdoc />
    public AudioDeviceSnapshot Refresh()
    {
        Refreshes++;
        return Snapshot;
    }
}

/// <summary>
///     A speech model whose identity and parameters a test chooses.
/// </summary>
/// <param name="id">The model identifier.</param>
/// <param name="role">The model's role.</param>
/// <param name="parameters">The tunable parameters.</param>
internal sealed class FakeSpeechModel(string id, SpeechModelRole role, params ISpeechModelParameter[] parameters) : ISpeechModel
{
    /// <inheritdoc />
    public string Id { get; } = id;

    /// <inheritdoc />
    public string DisplayName { get; } = $"Model {id}";

    /// <inheritdoc />
    public SpeechModelRole Role { get; } = role;

    /// <inheritdoc />
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } = parameters;

    /// <inheritdoc />
    public SpeechModelAudioTagSupport AudioTagSupport => default;

    /// <inheritdoc />
    public SpeechModelDownloadDescriptor DownloadDescriptor => default!;
}

/// <summary>
///     A speech model service for UI tests. Its download waits until a test finishes it or the token is cancelled,
///     and remembers the progress sink and token it was given.
/// </summary>
internal sealed class FakeSpeechModelService : ISpeechModelService
{
    /// <summary>
    ///     The result of the running download.
    /// </summary>
    private TaskCompletionSource<SpeechModelDownloadReport> _pending = new();

    /// <summary>
    ///     Gets the models that <see cref="Enumerate" /> lists.
    /// </summary>
    public List<SpeechModelDescriptor> Models { get; } = [];

    /// <summary>
    ///     Gets or sets the problem that <see cref="Enumerate" /> reports, or <see langword="null" />.
    /// </summary>
    public string? Problem { get; set; }

    /// <summary>
    ///     Gets the progress sink of the last download.
    /// </summary>
    public IProgress<SpeechModelDownloadProgress>? LastProgress { get; private set; }

    /// <summary>
    ///     Gets the cancellation token of the last download.
    /// </summary>
    public CancellationToken LastToken { get; private set; }

    /// <summary>
    ///     Gets how many downloads were started.
    /// </summary>
    public int Downloads { get; private set; }

    /// <inheritdoc />
    public SpeechModelList Enumerate()
    {
        return new SpeechModelList(Models.ToArray(), Problem);
    }

    /// <inheritdoc />
    public Task<SpeechModelDownloadReport> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Downloads++;
        LastProgress = progress;
        LastToken = cancellationToken;
        var pending = new TaskCompletionSource<SpeechModelDownloadReport>();
        _pending = pending;
        cancellationToken.Register(() => pending.TrySetResult(new SpeechModelDownloadReport(SpeechModelDownloadStatus.Canceled, "Download canceled.")));
        return pending.Task;
    }

    /// <summary>
    ///     Ends the running download with a report.
    /// </summary>
    /// <param name="report">The report.</param>
    public void Complete(SpeechModelDownloadReport report)
    {
        _pending.TrySetResult(report);
    }

    /// <summary>
    ///     Adds a model to the list.
    /// </summary>
    /// <param name="id">The model identifier.</param>
    /// <param name="role">The model's role.</param>
    /// <param name="state">The install state.</param>
    /// <param name="parameters">The tunable parameters.</param>
    public void AddModel(string id, SpeechModelRole role, SpeechModelState state = SpeechModelState.NotDownloaded, params ISpeechModelParameter[] parameters)
    {
        Models.Add(new SpeechModelDescriptor(new FakeSpeechModel(id, role, parameters), state));
    }
}

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

using System.Threading.Channels;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A recognition session that a test drives by hand: results are emitted when the test says, stopping it
///     completes the result stream after emitting any results that the test queued as "drained on stop", and every
///     call is counted.
/// </summary>
internal sealed class FakeRecognitionSession : IRecognitionSession
{
    /// <summary>
    ///     The results waiting for the consumer.
    /// </summary>
    private readonly Channel<SpeechRecognitionEvent> _events = Channel.CreateUnbounded<SpeechRecognitionEvent>();

    /// <summary>
    ///     Gets or sets a value indicating whether the session claims to be usable.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    ///     Gets the current lifecycle state.
    /// </summary>
    public RecognitionSessionState State { get; private set; } = RecognitionSessionState.Created;

    /// <summary>
    ///     Gets or sets the exception that <see cref="StartAsync" /> throws, or <see langword="null" />.
    /// </summary>
    public Exception? StartFailure { get; set; }

    /// <summary>
    ///     Gets the results that <see cref="StopAsync" /> emits before it completes the stream, which is how the real
    ///     recognizer finishes the audio it has already heard.
    /// </summary>
    public List<SpeechRecognitionResult> ResultsOnStop { get; } = [];

    /// <summary>
    ///     Gets how many times the session was started.
    /// </summary>
    public int Starts { get; private set; }

    /// <summary>
    ///     Gets how many times the session was stopped.
    /// </summary>
    public int Stops { get; private set; }

    /// <summary>
    ///     Gets how many times the session was disposed.
    /// </summary>
    public int Disposals { get; private set; }

    /// <summary>
    ///     Never raised by this fake; the accessors are empty so that the unused event is not a warning.
    /// </summary>
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged
    {
        add
        {
            // The fake never changes state observably, so there is nothing to subscribe to.
        }

        remove
        {
            // See the add accessor.
        }
    }

    /// <summary>
    ///     Emits one result to the consumer.
    /// </summary>
    /// <param name="text">The recognized text.</param>
    /// <param name="isFinal">Whether the result is final.</param>
    public void Emit(string text, bool isFinal)
    {
        _events.Writer.TryWrite(new SpeechRecognitionEvent(new SpeechRecognitionResult(text, isFinal)));
    }

    /// <summary>
    ///     Ends the result stream with a failure, as a faulted recognizer does.
    /// </summary>
    /// <param name="failure">The failure.</param>
    public void Fault(Exception failure)
    {
        _events.Writer.TryComplete(failure);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Starts++;
        if (StartFailure is not null)
        {
            throw StartFailure;
        }

        State = RecognitionSessionState.Running;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Stops++;
        foreach (var result in ResultsOnStop)
        {
            _events.Writer.TryWrite(new SpeechRecognitionEvent(result));
        }

        _events.Writer.TryComplete();
        State = RecognitionSessionState.Stopped;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<SpeechRecognitionEvent> GetResultsAsync(CancellationToken cancellationToken = default)
    {
        return _events.Reader.ReadAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Disposals++;
        _events.Writer.TryComplete();
        State = RecognitionSessionState.Disposed;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     A recognizer engine that creates <see cref="FakeRecognitionSession" />s and remembers them.
/// </summary>
internal sealed class FakeRecognizerEngine : ISpeechRecognizerEngine
{
    /// <summary>
    ///     Gets or sets a value indicating whether the engine claims to be usable.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    ///     Gets the sessions created so far, in order.
    /// </summary>
    public List<FakeRecognitionSession> Sessions { get; } = [];

    /// <summary>
    ///     Gets or sets what the next <see cref="CreateSessionAsync" /> does with the new session before it is returned,
    ///     for example queueing results to be drained on stop.
    /// </summary>
    public Action<FakeRecognitionSession>? ConfigureSession { get; set; }

    /// <summary>
    ///     Gets or sets the exception that <see cref="CreateSessionAsync" /> throws, or <see langword="null" />.
    /// </summary>
    public Exception? CreateFailure { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the engine was disposed.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc />
    public Task<IRecognitionSession> CreateSessionAsync(IAudioCaptureDevice device, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CreateFailure is not null)
        {
            throw CreateFailure;
        }

        var session = new FakeRecognitionSession();
        ConfigureSession?.Invoke(session);
        Sessions.Add(session);
        return Task.FromResult<IRecognitionSession>(session);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     A capture device that does nothing but remember that it was disposed.
/// </summary>
internal sealed class FakeCaptureDevice : IAudioCaptureDevice, IDisposable
{
    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public int SampleRate => 16000;

    /// <inheritdoc />
    public int ChannelCount => 1;

    /// <summary>
    ///     Gets a value indicating whether the device was disposed.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <summary>
    ///     Never raised by this fake; the accessors are empty so that the unused event is not a warning.
    /// </summary>
    public event EventHandler<AudioCaptureFrameEventArgs>? FrameCaptured
    {
        add
        {
            // The fake produces no audio, so there is nothing to subscribe to.
        }

        remove
        {
            // See the add accessor.
        }
    }

    /// <inheritdoc />
    public void Start()
    {
        // The fake produces no audio.
    }

    /// <inheritdoc />
    public void Stop()
    {
        // The fake produces no audio.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Disposed = true;
    }
}

/// <summary>
///     A settings service whose current settings a test changes directly.
/// </summary>
internal sealed class FakeSettingsService : ISettingsService
{
    /// <inheritdoc />
    public event EventHandler<SettingsChangedEventArgs>? Changed
    {
        add
        {
            // Tests change the settings directly and read them again, so nothing needs to be notified.
        }

        remove
        {
            // See the add accessor.
        }
    }

    /// <inheritdoc />
    public AppSettings Current { get; set; } = new();

    /// <inheritdoc />
    public string SettingsFilePath => string.Empty;

    /// <inheritdoc />
    public AppSettings Load()
    {
        return Current;
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        Current = settings;
    }

    /// <inheritdoc />
    public AppSettings Update(Func<AppSettings, AppSettings> update)
    {
        Current = update(Current);
        return Current;
    }
}

/// <summary>
///     A recognition backend written in managed code, so that the speech library's real engine and session pipeline
///     can run without a native speech runtime or a downloaded model: the first provisional results are reported
///     as audio arrives and the final result is reported when the session finishes the audio it has heard.
/// </summary>
/// <param name="provisional">The provisional results, reported one per decode after audio has arrived.</param>
/// <param name="final">The final result, reported when the session flushes at its end.</param>
internal sealed class FakeRecognitionBackend(IEnumerable<SpeechRecognitionResult> provisional, SpeechRecognitionResult final) : IRecognitionBackend
{
    /// <summary>
    ///     The provisional results not yet reported.
    /// </summary>
    private readonly Queue<SpeechRecognitionResult> _pending = new(provisional);

    /// <summary>
    ///     Whether the final result was reported.
    /// </summary>
    private bool _flushed;

    /// <summary>
    ///     Gets how many samples the backend was given.
    /// </summary>
    public long SamplesReceived { get; private set; }

    /// <inheritdoc />
    public void AcceptSamples(ReadOnlySpan<float> monoSamples)
    {
        SamplesReceived += monoSamples.Length;
    }

    /// <inheritdoc />
    public void Reset()
    {
        // The fake keeps no utterance state beyond its script.
    }

    /// <inheritdoc />
    public bool TryDecode(out SpeechRecognitionResult result)
    {
        if (SamplesReceived > 0 && _pending.Count > 0)
        {
            result = _pending.Dequeue();
            return true;
        }

        result = null!;
        return false;
    }

    /// <inheritdoc />
    public bool TryFlush(out SpeechRecognitionResult result)
    {
        if (!_flushed)
        {
            _flushed = true;
            result = final;
            return true;
        }

        result = null!;
        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The fake holds nothing to release.
    }
}

/// <summary>
///     A speech-to-text model written in managed code whose backend is the scripted <see cref="FakeRecognitionBackend" />.
/// </summary>
/// <param name="id">The model identifier.</param>
/// <param name="backendFactory">Creates the backend when the engine is loaded.</param>
/// <param name="parameters">The tunable parameters the model declares, or <see langword="null" /> for none.</param>
internal sealed class FakeRecognitionModel(
    string id,
    Func<IRecognitionBackend> backendFactory,
    IReadOnlyList<ISpeechModelParameter>? parameters = null) : IRecognitionModel
{
    /// <inheritdoc />
    public string Id { get; } = id;

    /// <inheritdoc />
    public string DisplayName { get; } = $"Fake model {id}";

    /// <inheritdoc />
    public SpeechModelRole Role => SpeechModelRole.Recognition;

    /// <inheritdoc />
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } = parameters ?? [];

    /// <inheritdoc />
    public SpeechModelAudioTagSupport AudioTagSupport => default;

    /// <inheritdoc />
    public SpeechModelDownloadDescriptor DownloadDescriptor => default!;

    /// <inheritdoc />
    public AudioFormat AudioFormat => AudioFormat.Mono(16000);

    /// <inheritdoc />
    public IRecognitionBackend CreateBackend(string installedModelDirectory)
    {
        return backendFactory();
    }
}

/// <summary>
///     Builders for the files that the dictation tests need on disk.
/// </summary>
internal static class DictationTestFiles
{
    /// <summary>
    ///     Writes a mono 16-bit PCM WAVE file of silence, which is all the scripted backend needs because it ignores
    ///     what the audio contains.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="seconds">The length of the audio.</param>
    /// <param name="sampleRate">The sample rate in Hz.</param>
    public static void WriteSilentWave(string path, int seconds, int sampleRate)
    {
        var dataBytes = seconds * sampleRate * 2;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        writer.Write(new byte[dataBytes]);
    }

    /// <summary>
    ///     Marks a model as installed in a model store folder, the way the store's own installer does: a manifest
    ///     next to a <c>current</c> folder that holds the model's files.
    /// </summary>
    /// <param name="storeRoot">The root of the model store.</param>
    /// <param name="modelId">The model identifier.</param>
    public static void InstallModel(string storeRoot, string modelId)
    {
        var modelFolder = Path.Combine(storeRoot, modelId);
        Directory.CreateDirectory(Path.Combine(modelFolder, "current"));
        File.WriteAllText(Path.Combine(modelFolder, "current", "model.bin"), "x");
        File.WriteAllText(
            Path.Combine(modelFolder, "install-manifest.json"),
            "{\"TotalSizeBytes\":1,\"InstalledAtUtc\":\"2026-01-01T00:00:00+00:00\"}");
    }
}

/// <summary>
///     Everything a dictation test needs, wired together over substitutes: a settings service with a chosen
///     model, a model service that lists it as installed and loads <see cref="FakeRecognizerEngine" />s, an audio
///     service with one microphone, and the native speech files reported present. A test changes what it needs and
///     creates the service with <see cref="CreateService" />.
/// </summary>
internal sealed class DictationHarness
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DictationHarness" /> class with everything available.
    /// </summary>
    public DictationHarness()
    {
        Settings.Current = new AppSettings { Speech = new SpeechSettings { SttModelId = "stt-one", CaptureDevice = "Mic A" } };
        ListInstalled();

        Models.LoadRecognizerAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Loads++;
                LastParameters = call.ArgAt<IReadOnlyDictionary<string, string>?>(1);
                if (LoadFailure is not null)
                {
                    return Task.FromResult(LoadFailure);
                }

                var engine = new FakeRecognizerEngine();
                ConfigureEngine?.Invoke(engine);
                Engines.Add(engine);
                return Task.FromResult(RecognizerLoadResult.Success(engine));
            });

        Audio.Enumerate().Returns(_ => new AudioDeviceSnapshot([SpeechTestData.Capture("Mic A")], []));
        Audio.CreateCaptureDevice(Arg.Any<string?>()).Returns(_ =>
        {
            Requested.Add(Device);
            return DeviceResult ?? new CaptureDeviceResult(Device);
        });
    }

    /// <summary>
    ///     Gets the settings service.
    /// </summary>
    public FakeSettingsService Settings { get; } = new();

    /// <summary>
    ///     Gets the model service substitute.
    /// </summary>
    public ISpeechModelService Models { get; } = Substitute.For<ISpeechModelService>();

    /// <summary>
    ///     Gets the audio service substitute.
    /// </summary>
    public IAudioDeviceService Audio { get; } = Substitute.For<IAudioDeviceService>();

    /// <summary>
    ///     Gets the microphone that <see cref="Audio" /> creates.
    /// </summary>
    public FakeCaptureDevice Device { get; } = new();

    /// <summary>
    ///     Gets the microphones that were asked for, in order.
    /// </summary>
    public List<FakeCaptureDevice> Requested { get; } = [];

    /// <summary>
    ///     Gets or sets the result that <see cref="Audio" /> gives for a capture device instead of the working
    ///     microphone, or <see langword="null" />.
    /// </summary>
    public CaptureDeviceResult? DeviceResult { get; set; }

    /// <summary>
    ///     Gets the engines that were loaded, in order.
    /// </summary>
    public List<FakeRecognizerEngine> Engines { get; } = [];

    /// <summary>
    ///     Gets or sets what is done to each engine before it is handed out.
    /// </summary>
    public Action<FakeRecognizerEngine>? ConfigureEngine { get; set; }

    /// <summary>
    ///     Gets or sets the failure that every load reports instead of an engine, or <see langword="null" />.
    /// </summary>
    public RecognizerLoadResult? LoadFailure { get; set; }

    /// <summary>
    ///     Gets how many times an engine was asked for.
    /// </summary>
    public int Loads { get; private set; }

    /// <summary>
    ///     Gets the stored parameters that the last load was given.
    /// </summary>
    public IReadOnlyDictionary<string, string>? LastParameters { get; private set; }

    /// <summary>
    ///     Gets or sets the native files that the service finds missing.
    /// </summary>
    public IReadOnlyList<string> MissingRuntimeFiles { get; set; } = [];

    /// <summary>
    ///     Makes the model service list the chosen model with the given state.
    /// </summary>
    /// <param name="state">The install state.</param>
    /// <param name="role">The model's role.</param>
    public void ListInstalled(SpeechModelState state = SpeechModelState.Downloaded, SpeechModelRole role = SpeechModelRole.Recognition)
    {
        var list = new SpeechModelList([SpeechTestData.Descriptor("stt-one", role, state)]);
        Models.Enumerate().Returns(list);
    }

    /// <summary>
    ///     Creates the service over the wired substitutes.
    /// </summary>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    /// <param name="releaseTail">How long the session keeps listening after the end is requested; none by default.</param>
    /// <returns>The service.</returns>
    public DictationService CreateService(RollingFileLogger? logger = null, TimeSpan? releaseTail = null)
    {
        return new DictationService(Settings, Models, Audio, logger, () => MissingRuntimeFiles, releaseTail ?? TimeSpan.Zero);
    }
}

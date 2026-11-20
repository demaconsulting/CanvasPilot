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

using System.Diagnostics;
using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Sherpa;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the DemaConsulting.Speech off-the-shelf software item (the
///     <c>DemaConsulting.Speech</c> library and its <c>DemaConsulting.Speech.Sherpa</c> model package). They
///     exercise the library directly with the same calls the application uses. They never download a model: the
///     model store is a temporary folder, and downloads are only requested in ways the library refuses before any
///     transfer. They accept whatever audio hardware the machine has, including none.
/// </summary>
public sealed class SpeechTests : IDisposable
{
    /// <summary>
    ///     Temporary folder that holds the model store, removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ots-speech-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the catalog filled with the Sherpa models lists speech-to-text and text-to-speech models with
    ///     identifiers, names, and an install state, from an empty store and without the network.
    /// </summary>
    [Fact]
    public void Speech_SherpaCatalog_Enumerate_ListsRecognitionAndSynthesisModels()
    {
        // Arrange: a catalog over an empty store, filled as the application fills it
        using var catalog = CreateCatalog();

        // Act: enumerate
        var models = catalog.Enumerate();

        // Assert: both roles are present, identifiers are unique, and nothing is installed
        Assert.Contains(models, model => model.Role == SpeechModelRole.Recognition);
        Assert.Contains(models, model => model.Role == SpeechModelRole.Synthesis);
        Assert.All(models, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Id));
            Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
            Assert.Equal(SpeechModelState.NotDownloaded, model.State);
        });
        Assert.Equal(models.Count, models.Select(model => model.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     Test that the Sherpa models declare their tunable parameters as typed descriptors with usable bounds,
    ///     defaults, and options.
    /// </summary>
    [Fact]
    public void Speech_SherpaCatalog_ModelParameters_AreTypedWithUsableBoundsAndDefaults()
    {
        // Arrange: the Sherpa models
        using var catalog = CreateCatalog();

        // Act: collect every declared parameter
        var parameters = catalog.Enumerate().SelectMany(model => model.Model.Parameters).ToArray();

        // Assert: at least one numeric and one choice parameter exist, and each is internally consistent
        Assert.Contains(parameters, parameter => parameter is NumericParameter);
        Assert.Contains(parameters, parameter => parameter is ChoiceParameter);
        foreach (var parameter in parameters)
        {
            Assert.False(string.IsNullOrWhiteSpace(parameter.Id));
            switch (parameter)
            {
                case NumericParameter numeric:
                    Assert.InRange(numeric.Default, numeric.Minimum, numeric.Maximum);
                    Assert.True(numeric.Step > 0);
                    break;
                case ChoiceParameter choice:
                    Assert.Contains(choice.Options, option => option.Value == choice.Default);
                    break;
                default:
                    Assert.IsType<BooleanParameter>(parameter);
                    break;
            }
        }
    }

    /// <summary>
    ///     Test that the catalog refuses a download of an unknown model, and honors a cancelled token for a known
    ///     model before anything is transferred, so downloads can be started and stopped by the application.
    /// </summary>
    [Fact]
    public async Task Speech_SherpaCatalog_Download_UnknownModelOrCancelledToken_IsRefusedWithoutTransfer()
    {
        // Arrange: a catalog over an empty store, a known model identifier, and a token that is already cancelled
        using var catalog = CreateCatalog();
        var known = catalog.Enumerate()[0].Id;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act: request the downloads
        var unknown = await Assert.ThrowsAsync<ArgumentException>(() => catalog.DownloadAsync("no-such-model", null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.DownloadAsync(known, null, cancelled.Token));

        // Assert: the unknown model was named in the refusal and the known model is still not downloaded
        Assert.Contains("no-such-model", unknown.Message, StringComparison.Ordinal);
        Assert.Equal(SpeechModelState.NotDownloaded, catalog.GetState(known));
    }

    /// <summary>
    ///     Test that listing capture and playback devices works on a machine with or without audio hardware: the
    ///     answer is a list, possibly empty, of named devices and never an exception.
    /// </summary>
    [Fact]
    public void Speech_AudioDeviceFactory_Enumerate_ReturnsNamedDevicesOrAnEmptyList()
    {
        // Arrange: the factory the application uses
        var factory = new AudioDeviceFactory();

        // Act: list both directions
        var capture = factory.CaptureProbe.Enumerate();
        var playback = factory.PlaybackProbe.Enumerate();

        // Assert: the lists exist and every device has a name and the right direction
        Assert.NotNull(capture);
        Assert.NotNull(playback);
        Assert.All(capture, device => Assert.False(string.IsNullOrWhiteSpace(device.Name)));
        Assert.All(capture, device => Assert.Equal(AudioDeviceDirection.Capture, device.Direction));
        Assert.All(playback, device => Assert.Equal(AudioDeviceDirection.Playback, device.Direction));
    }

    /// <summary>
    ///     Test that asking for the default capture and playback devices, and for a new scan of the hardware, does not
    ///     throw on a machine without audio hardware, and that an unusable device says so instead.
    /// </summary>
    [Fact]
    public void Speech_AudioDeviceFactory_DefaultDevicesAndRefresh_ToleratesMissingHardware()
    {
        // Arrange: the factory the application uses
        var factory = new AudioDeviceFactory();

        // Act: create the default devices and scan again with no stream open
        var capture = factory.CreateCaptureDevice(null);
        var playback = factory.CreatePlaybackDevice(null);
        var refresh = Record.Exception(factory.RefreshDevices);
        var afterRefresh = factory.CaptureProbe.Enumerate();

        // Assert: the devices exist and report their availability, the new scan worked, and listing still works
        Assert.NotNull(capture);
        Assert.NotNull(playback);
        Assert.Null(refresh);
        Assert.NotNull(afterRefresh);
        (capture as IDisposable)?.Dispose();
        (playback as IDisposable)?.Dispose();
    }

    /// <summary>
    ///     Test that a capture device can be asked for by the name of a microphone that does not exist, in the mono
    ///     format of speech models, and that the answer is a device that says whether it is usable and never an
    ///     exception, on a machine with or without audio hardware.
    /// </summary>
    [Fact]
    public void Speech_AudioDeviceFactory_CreateCaptureDevice_UnknownNameInMonoFormat_ReturnsDeviceWithoutThrowing()
    {
        // Arrange: the factory the application uses, a microphone name that no machine has, and the system default
        var factory = new AudioDeviceFactory();
        var unknown = new AudioDeviceSelection("No such microphone, DemaConsulting test");

        // Act: ask for both in the format that speech models take
        var named = factory.CreateCaptureDevice(unknown, AudioFormat.Mono(16000));
        var systemDefault = factory.CreateCaptureDevice(AudioDeviceSelection.SystemDefault, AudioFormat.Mono(16000));

        // Assert: both are devices, a usable one has a sample rate and channels, and an unusable one has none
        foreach (var device in new[] { named, systemDefault })
        {
            Assert.NotNull(device);
            if (device.IsAvailable)
            {
                Assert.True(device.SampleRate > 0);
                Assert.True(device.ChannelCount > 0);
            }
            else
            {
                Assert.Equal(0, device.SampleRate);
            }

            (device as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    ///     Test that the native speech runtime that the library's packages bring along is delivered to the output
    ///     folder of a build and loads from there on 64-bit Windows: the inference runtime, the Sherpa C interface, and
    ///     the audio library. The inference runtime must be the one of the application, because Windows has an
    ///     older one in its system folder that crashes the process when it is loaded instead.
    /// </summary>
    [Fact]
    public void Speech_NativeRuntime_WindowsLibraries_AreDeliveredAndLoadFromTheApplicationFolder()
    {
        // Arrange: only 64-bit Windows is delivered with an installer, so the check is made there
        Assert.SkipUnless(
            OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64,
            "The native speech runtime is delivered and verified for 64-bit Windows only.");
        var folder = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
        var libraries = new[] { "onnxruntime.dll", "sherpa-onnx-c-api.dll", "portaudio.dll" };

        // Act: load each library from the folder, the inference runtime first because the Sherpa interface needs it
        var handles = new List<nint>();
        try
        {
            foreach (var library in libraries)
            {
                var path = Path.Combine(folder, library);
                Assert.True(File.Exists(path), $"{library} was not delivered to {folder}.");
                Assert.True(NativeLibrary.TryLoad(path, out var handle), $"{library} did not load.");
                handles.Add(handle);
            }

            // Assert: the modules that the process holds are the ones from the application folder, not from Windows
            var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().ToArray();
            foreach (var library in libraries)
            {
                var module = Assert.Single(modules, candidate => string.Equals(candidate.ModuleName, library, StringComparison.OrdinalIgnoreCase));
                Assert.StartsWith(AppContext.BaseDirectory, module.FileName, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            foreach (var handle in handles)
            {
                NativeLibrary.Free(handle);
            }
        }
    }

    /// <summary>
    ///     Test that asking for a recognizer of a model that is not installed gives an unavailable engine and an
    ///     unavailable session instead of an exception, without loading any native speech runtime.
    /// </summary>
    [Fact]
    public async Task Speech_RecognizerFactory_ModelNotInstalled_ReturnsUnavailableEngineWithoutThrowing()
    {
        // Arrange: a catalog over an empty store, and a speech-to-text model from it
        using var catalog = CreateCatalog();
        var model = (IRecognitionModel)catalog.Enumerate().First(descriptor => descriptor.Model is IRecognitionModel).Model;

        // Act: load the recognizer, and ask it for a session
        var engine = await SpeechRecognizerFactory.LoadAsync(model, catalog, null, null, TestContext.Current.CancellationToken);
        await using var session = await engine.CreateSessionAsync(UnavailableAudioCaptureDevice.Instance, TestContext.Current.CancellationToken);

        // Assert: both say they are unavailable, and nothing was thrown
        Assert.False(engine.IsAvailable);
        Assert.False(session.IsAvailable);
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Test that the library's own recognition pipeline, run with a recognition backend written in managed
    ///     code and a sound file for a microphone, streams provisional results that a final result replaces, finishes
    ///     the audio it has already received when it is stopped, and then ends the stream.
    /// </summary>
    [Fact]
    public async Task Speech_RecognizerFactory_ManagedBackend_StreamsProvisionalThenFinalResultsAndDrainsOnStop()
    {
        // Arrange: a model whose backend reports two provisional results and then a final one, installed in a store
        // in the temporary folder, and one second of silence as the sound file
        var modelFolder = Path.Combine(_root, "models", "managed");
        Directory.CreateDirectory(Path.Combine(modelFolder, "current"));
        await File.WriteAllTextAsync(Path.Combine(modelFolder, "current", "model.bin"), "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(modelFolder, "install-manifest.json"),
            "{\"TotalSizeBytes\":1,\"InstalledAtUtc\":\"2026-01-01T00:00:00+00:00\"}",
            TestContext.Current.CancellationToken);
        var wave = Path.Combine(_root, "silence.wav");
        WriteSilentWave(wave, sampleRate: 16000);
        var model = new ManagedRecognitionModel();
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "models") }).AddModels(model);
        var engine = await SpeechRecognizerFactory.LoadAsync(model, catalog, null, null, TestContext.Current.CancellationToken);
        await using var session = await engine.CreateSessionAsync(new WavFileAudioCaptureDevice(wave, 1600), TestContext.Current.CancellationToken);
        var events = new System.Collections.Concurrent.ConcurrentQueue<(string Text, bool IsFinal)>();

        // Act: begin reading the results before the sound file plays, because the recognizer keeps no results for a
        // reader that is not yet reading: the first step of the enumeration is taken here, on this thread, which
        // subscribes the reader before anything is started; then start, wait for the words, stop, and wait for the end
        var enumerator = session.GetResultsAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var firstStep = enumerator.MoveNextAsync().AsTask();
        var pump = Task.Run(
            async () =>
            {
                for (var more = await firstStep; more; more = await enumerator.MoveNextAsync())
                {
                    events.Enqueue((enumerator.Current.Result.Text, enumerator.Current.Result.IsFinal));
                }

                await enumerator.DisposeAsync();
            },
            TestContext.Current.CancellationToken);
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);
        await pump.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var results = events.ToArray();

        // Assert: the engine was available, the provisional results came first and were not final, the last result
        // is the final one, and the stream ended
        Assert.True(engine.IsAvailable);
        Assert.NotEmpty(results);
        Assert.Equal(("Hello world.", true), results[^1]);
        Assert.All(results.Take(results.Length - 1), recognized => Assert.False(recognized.IsFinal));
        Assert.Contains(results, recognized => recognized.Text.StartsWith("hello", StringComparison.Ordinal));
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Opt-in integration test: with a speech-to-text model and a text-to-speech model downloaded, the real
    ///     native engine recognizes a sentence that the other model speaks into a sound file, which is fed through the
    ///     sound-file capture device. It runs only when the environment variable
    ///     <c>CANVASPILOT_SPEECH_INTEGRATION</c> is <c>1</c> and the models are installed, because it needs large
    ///     downloaded files and loads the native runtime; it is manual evidence before a release, not an automated
    ///     requirement.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Speech_RecognizerFactory_WavFileDevice_WithInstalledModel_ProducesText()
    {
        // Arrange: skip unless the test was asked for, the natives are beside the test host, and both models exist
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("CANVASPILOT_SPEECH_INTEGRATION") == "1",
            "Set CANVASPILOT_SPEECH_INTEGRATION=1 to run the speech integration test.");
        Assert.SkipUnless(
            OperatingSystem.IsWindows() && File.Exists(Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "onnxruntime.dll")),
            "The native speech runtime is not beside the test host.");
        using var catalog = new SpeechModelCatalog().AddSherpaModels();
        var installed = catalog.Enumerate().Where(descriptor => descriptor.State == SpeechModelState.Downloaded).ToArray();
        var recognizer = installed.FirstOrDefault(descriptor => descriptor.Model is IRecognitionModel);
        var speaker = installed.FirstOrDefault(descriptor => descriptor.Model is ISynthesisModel && descriptor.Id.Contains("piper", StringComparison.OrdinalIgnoreCase))
            ?? installed.FirstOrDefault(descriptor => descriptor.Model is ISynthesisModel);
        Assert.SkipUnless(recognizer is not null && speaker is not null, "A speech-to-text and a text-to-speech model must be downloaded.");
        var wave = Path.Combine(_root, "spoken.wav");

        // Act: speak a sentence into a sound file, then recognize the sound file
        var synthesizer = await SpeechSynthesizerFactory.LoadAsync((ISynthesisModel)speaker.Model, catalog, null, null, TestContext.Current.CancellationToken);
        var format = ((ISynthesisModel)speaker.Model).PreferredAudioFormat;
        using (var playback = new WavFileAudioPlaybackDevice(wave, format.SampleRate, format.ChannelCount))
        {
            await synthesizer.SpeakAsync(playback, "Please make the second box blue.", TestContext.Current.CancellationToken);
        }

        await synthesizer.DisposeAsync();
        var engine = await SpeechRecognizerFactory.LoadAsync((IRecognitionModel)recognizer.Model, catalog, null, null, TestContext.Current.CancellationToken);
        Assert.True(engine.IsAvailable, "The native speech engine did not load.");
        var text = new System.Text.StringBuilder();
        await using (var session = await engine.CreateSessionAsync(new WavFileAudioCaptureDevice(wave, 1600), TestContext.Current.CancellationToken))
        {
            // The results are read from before the start, with the first step of the enumeration taken here, because the
            // recognizer keeps none for a reader that is not yet reading and the sound file is delivered at once.
            var enumerator = session.GetResultsAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
            var firstStep = enumerator.MoveNextAsync().AsTask();
            var pump = Task.Run(
                async () =>
                {
                    for (var more = await firstStep; more; more = await enumerator.MoveNextAsync())
                    {
                        if (enumerator.Current.Result.IsFinal)
                        {
                            text.Append(' ').Append(enumerator.Current.Result.Text);
                        }
                    }

                    await enumerator.DisposeAsync();
                },
                TestContext.Current.CancellationToken);
            await session.StartAsync(TestContext.Current.CancellationToken);

            // The sound file is delivered at once and the recognizer decodes it on its own thread, so it is given a
            // moment to catch up before the release, as a person's pause before letting go would.
            await Task.Delay(2000, TestContext.Current.CancellationToken);
            await session.StopAsync(TestContext.Current.CancellationToken);
            await pump.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        }

        await engine.DisposeAsync();

        // Assert: the first words of the sentence came back; the very end can be cut off by a recognizer that is
        // stopped abruptly, so the test does not demand it
        Assert.True(text.ToString().Contains("make the second", StringComparison.OrdinalIgnoreCase), $"Recognized: '{text}'");
    }

    /// <summary>
    ///     Writes a mono 16-bit PCM WAVE file of one second of silence.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="sampleRate">The sample rate in Hz.</param>
    private static void WriteSilentWave(string path, int sampleRate)
    {
        var dataBytes = sampleRate * 2;
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
    ///     Creates a catalog over a store in the temporary folder, filled with the Sherpa models.
    /// </summary>
    /// <returns>The catalog.</returns>
    private SpeechModelCatalog CreateCatalog()
    {
        return new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "models") }).AddSherpaModels();
    }

    /// <summary>
    ///     A speech-to-text model written in managed code, so that the library's own recognition pipeline can be
    ///     tested without a native speech runtime or a downloaded model.
    /// </summary>
    private sealed class ManagedRecognitionModel : IRecognitionModel
    {
        /// <inheritdoc />
        public string Id => "managed";

        /// <inheritdoc />
        public string DisplayName => "Managed test model";

        /// <inheritdoc />
        public SpeechModelRole Role => SpeechModelRole.Recognition;

        /// <inheritdoc />
        public IReadOnlyList<ISpeechModelParameter> Parameters { get; } = [];

        /// <inheritdoc />
        public SpeechModelAudioTagSupport AudioTagSupport => default;

        /// <inheritdoc />
        public SpeechModelDownloadDescriptor DownloadDescriptor => default!;

        /// <inheritdoc />
        public AudioFormat AudioFormat => AudioFormat.Mono(16000);

        /// <inheritdoc />
        public IRecognitionBackend CreateBackend(string installedModelDirectory)
        {
            return new ManagedRecognitionBackend();
        }
    }

    /// <summary>
    ///     A recognition backend that reports two provisional results once audio has arrived and a final result when
    ///     the session flushes at its end.
    /// </summary>
    private sealed class ManagedRecognitionBackend : IRecognitionBackend
    {
        /// <summary>
        ///     The provisional results not yet reported.
        /// </summary>
        private readonly Queue<SpeechRecognitionResult> _pending = new([new SpeechRecognitionResult("hello", false), new SpeechRecognitionResult("hello wor", false)]);

        /// <summary>
        ///     Whether any audio has arrived.
        /// </summary>
        private bool _heardAudio;

        /// <summary>
        ///     Whether the final result was reported.
        /// </summary>
        private bool _flushed;

        /// <inheritdoc />
        public void AcceptSamples(ReadOnlySpan<float> monoSamples)
        {
            _heardAudio |= monoSamples.Length > 0;
        }

        /// <inheritdoc />
        public void Reset()
        {
            // The scripted backend keeps no utterance state.
        }

        /// <inheritdoc />
        public bool TryDecode(out SpeechRecognitionResult result)
        {
            if (_heardAudio && _pending.Count > 0)
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
                result = new SpeechRecognitionResult("Hello world.", true);
                return true;
            }

            result = null!;
            return false;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // The scripted backend holds nothing to release.
        }
    }
}

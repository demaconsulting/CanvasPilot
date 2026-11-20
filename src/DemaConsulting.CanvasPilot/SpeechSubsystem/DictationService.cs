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
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     Turns what the user says while holding a talk button or key into text, with the speech model, parameters, and
///     microphone chosen in the settings. The speech engine is loaded once per model and parameter combination and
///     reused, because loading it is the expensive step; a session is created for every hold.
/// </summary>
/// <remarks>
///     <para>
///         The service is the one place that loads a speech engine, so it is also the one place that guarantees the
///         native speech runtime is there first. Loading the engine when <c>onnxruntime</c> is missing next to the
///         application does not fail cleanly: Windows then loads an older <c>onnxruntime.dll</c> from its system
///         folder and the process crashes inside the native library, which cannot be caught. Every path that loads
///         an engine therefore first checks that the native files exist, and reports that the speech runtime is
///         missing instead of loading when they do not.
///     </para>
///     <para>
///         Recognized text is never written to the log. The service is safe for concurrent use; only one dictation
///         session listens at a time and a request for a second one is ignored.
///     </para>
/// </remarks>
public sealed class DictationService : IDictationService, IDisposable
{
    /// <summary>
    ///     The native libraries, by library name without prefix or extension, that speech recognition cannot start
    ///     safely without: the inference runtime and the Sherpa C interface to it.
    /// </summary>
    private static readonly string[] RequiredNativeLibraries = ["onnxruntime", "sherpa-onnx-c-api"];

    /// <summary>
    ///     The sentence that points the user at the application log from messages about failures that have details
    ///     only the log holds.
    /// </summary>
    private const string SeeLogHint = "The application log has more details.";

    /// <summary>
    ///     How long the microphone keeps listening after the user lets go. The audio system delivers audio in blocks
    ///     and the recognizer decodes on its own thread, so audio of the last word is still on its way at the moment
    ///     of release; stopping at once would cut that word off.
    /// </summary>
    private static readonly TimeSpan DefaultReleaseTail = TimeSpan.FromMilliseconds(300);

    /// <summary>
    ///     How long the microphone keeps listening after the user lets go.
    /// </summary>
    private readonly TimeSpan _releaseTail;

    /// <summary>
    ///     The settings that hold the model, its parameters, and the microphone.
    /// </summary>
    private readonly ISettingsService _settings;

    /// <summary>
    ///     Lists the models and loads the speech engine.
    /// </summary>
    private readonly ISpeechModelService _models;

    /// <summary>
    ///     Creates the capture device and lists the microphones.
    /// </summary>
    private readonly IAudioDeviceService _audio;

    /// <summary>
    ///     Names the native libraries that are missing from the application.
    /// </summary>
    private readonly Func<IReadOnlyList<string>> _findMissingRuntimeFiles;

    /// <summary>
    ///     The application log, or <see langword="null" /> for none.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Serializes loading, replacing, and releasing the cached engine.
    /// </summary>
    private readonly SemaphoreSlim _engineGate = new(1, 1);

    /// <summary>
    ///     Guards <see cref="_listening" />, <see cref="_active" />, and <see cref="_disposed" />.
    /// </summary>
    private readonly Lock _stateLock = new();

    /// <summary>
    ///     The loaded engine, or <see langword="null" /> when none is loaded.
    /// </summary>
    private ISpeechRecognizerEngine? _engine;

    /// <summary>
    ///     The model and parameters that <see cref="_engine" /> was loaded with.
    /// </summary>
    private string? _engineKey;

    /// <summary>
    ///     Whether a session is listening or being started.
    /// </summary>
    private bool _listening;

    /// <summary>
    ///     The session that is listening, or <see langword="null" />.
    /// </summary>
    private DictationSession? _active;

    /// <summary>
    ///     Whether the service has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Creates a service over the settings in effect, the speech models, and the audio devices, which checks the
    ///     native speech files beside the running application.
    /// </summary>
    /// <param name="settings">The settings service; its current speech settings are read for every request.</param>
    /// <param name="models">Lists the speech models and loads the speech engine.</param>
    /// <param name="audio">Lists the microphones and creates the capture device.</param>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings" />, <paramref name="models" />, or <paramref name="audio" /> is <see langword="null" />.</exception>
    public DictationService(
        ISettingsService settings,
        ISpeechModelService models,
        IAudioDeviceService audio,
        RollingFileLogger? logger = null)
        : this(settings, models, audio, logger, null)
    {
    }

    /// <summary>
    ///     Creates a service whose check of the native speech files can be substituted, so that tests need neither
    ///     the files nor an engine.
    /// </summary>
    /// <param name="settings">The settings service.</param>
    /// <param name="models">Lists the speech models and loads the speech engine.</param>
    /// <param name="audio">Lists the microphones and creates the capture device.</param>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    /// <param name="findMissingRuntimeFiles">
    ///     Names the native files that are missing, an empty list when none is, or <see langword="null" /> for the
    ///     check of the files beside the running application.
    /// </param>
    /// <param name="releaseTail">
    ///     How long the microphone keeps listening after the user lets go, or <see langword="null" /> for the
    ///     default. Tests pass <see cref="TimeSpan.Zero" /> to end sessions at once.
    /// </param>
    internal DictationService(
        ISettingsService settings,
        ISpeechModelService models,
        IAudioDeviceService audio,
        RollingFileLogger? logger,
        Func<IReadOnlyList<string>>? findMissingRuntimeFiles,
        TimeSpan? releaseTail = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(audio);

        _settings = settings;
        _models = models;
        _audio = audio;
        _logger = logger;
        _findMissingRuntimeFiles = findMissingRuntimeFiles ?? (() => FindMissingNativeLibraries(AppContext.BaseDirectory));
        _releaseTail = releaseTail ?? DefaultReleaseTail;
    }

    /// <summary>
    ///     Gets the file name that a native library has on the running platform.
    /// </summary>
    /// <param name="libraryName">The library name without prefix or extension.</param>
    /// <returns>The file name, for example <c>onnxruntime.dll</c> on Windows.</returns>
    internal static string NativeFileName(string libraryName)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"{libraryName}.dll";
        }

        return OperatingSystem.IsMacOS() ? $"lib{libraryName}.dylib" : $"lib{libraryName}.so";
    }

    /// <summary>
    ///     Names the native speech libraries that are missing from an application folder. A library counts as present
    ///     when its file is in the folder itself, which is where the published application keeps it, or in the
    ///     <c>runtimes/{rid}/native</c> folder below it, which is where a build for development keeps it.
    /// </summary>
    /// <param name="baseDirectory">The folder of the application.</param>
    /// <returns>The file names that were not found, in a fixed order; empty when all are present.</returns>
    internal static IReadOnlyList<string> FindMissingNativeLibraries(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        // The runtime identifier is built from the platform instead of read from the runtime, which reports
        // distribution-specific identifiers that the native package folders do not use.
        string system;
        if (OperatingSystem.IsWindows())
        {
            system = "win";
        }
        else
        {
            system = OperatingSystem.IsMacOS() ? "osx" : "linux";
        }
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var developmentFolder = Path.Combine(baseDirectory, "runtimes", $"{system}-{architecture}", "native");

        var missing = new List<string>();
        foreach (var library in RequiredNativeLibraries)
        {
            var fileName = NativeFileName(library);
            if (!File.Exists(Path.Combine(baseDirectory, fileName))
                && !File.Exists(Path.Combine(developmentFolder, fileName)))
            {
                missing.Add(fileName);
            }
        }

        return missing;
    }

    /// <inheritdoc />
    public DictationAvailability CheckAvailability()
    {
        try
        {
            var failure = CheckModelAndRuntime(_settings.Current.Speech);
            if (failure is not null)
            {
                return failure;
            }

            // The microphones are listed last because starting the audio system is the slowest of the checks.
            var devices = _audio.Enumerate();
            if (!devices.IsAvailable)
            {
                return NoMicrophone(devices.Problem);
            }

            return devices.CaptureDevices.Count == 0
                ? NoMicrophone("No microphone was found.")
                : DictationAvailability.Ready;
        }
        catch (Exception ex)
        {
            // The services answer with results, but this check runs on the user interface thread when the chat
            // panel opens, so even a defect in one of them must end as a message and not as a crash.
            LogProblem("Checking whether dictation is available failed.", ex);
            return new DictationAvailability(
                DictationAvailabilityReason.EngineUnavailable,
                $"Speech recognition could not be checked. {SeeLogHint}");
        }
    }

    /// <inheritdoc />
    public async Task<DictationAvailability> PrepareAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var availability = CheckAvailability();
            if (!availability.IsAvailable)
            {
                return availability;
            }

            var lookup = await GetEngineAsync(_settings.Current.Speech, forSession: false, cancellationToken).ConfigureAwait(false);
            return lookup.Failure ?? DictationAvailability.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Preparing is a convenience that runs in the background; if it fails, the failure is logged and the
            // user finds out, with the reason, when they press the talk button.
            LogProblem("Preparing the speech engine failed.", ex);
            return new DictationAvailability(
                DictationAvailabilityReason.EngineUnavailable,
                $"Speech recognition could not be prepared. {SeeLogHint}");
        }
    }

    /// <inheritdoc />
    public async Task<DictationStart> BeginAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: claim the single listening slot. A second request while the user is already dictating is the
        // key repeat or a double press, and is ignored rather than reported as a problem.
        lock (_stateLock)
        {
            if (_disposed)
            {
                return new DictationStart(
                    null,
                    new DictationAvailability(DictationAvailabilityReason.EngineUnavailable, "Speech recognition has been shut down."));
            }

            if (_listening)
            {
                return new DictationStart(null, DictationAvailability.Ready);
            }

            _listening = true;
        }

        // Everything below must give the slot back unless the session took it over, which is why the slot is
        // released in a finally block and not in each of the many early returns.
        var handedOver = false;
        try
        {
            var start = await StartSessionAsync(cancellationToken).ConfigureAwait(false);
            handedOver = start.IsListening;
            return start;
        }
        finally
        {
            if (!handedOver)
            {
                ReleaseSlot();
            }
        }
    }

    /// <summary>
    ///     Stops a session that is still listening, releases the engine, and refuses further requests.
    /// </summary>
    public void Dispose()
    {
        DictationSession? active;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            active = _active;
        }

        // The session and the engine are released on a worker thread and waited for here, because the interface of
        // disposing the application is synchronous and the release is asynchronous; the worker keeps this from
        // deadlocking on the user interface thread.
        Task.Run(async () =>
        {
            if (active is not null)
            {
                await active.EndAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await _engineGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisposeEngineAsync().ConfigureAwait(false);
            }
            finally
            {
                _engineGate.Release();
            }
        }).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Does the work of <see cref="BeginAsync" /> after the listening slot is claimed.
    /// </summary>
    /// <param name="cancellationToken">Gives up the start.</param>
    /// <returns>The listening session, or why there is none.</returns>
    private async Task<DictationStart> StartSessionAsync(CancellationToken cancellationToken)
    {
        // Step 2: check the model and the native files before anything is loaded.
        var speech = _settings.Current.Speech;
        var failure = CheckModelAndRuntime(speech);
        if (failure is not null)
        {
            return new DictationStart(null, failure);
        }

        // Step 3: get the engine, which is loaded only once.
        var lookup = await GetEngineAsync(speech, forSession: true, cancellationToken).ConfigureAwait(false);
        if (lookup.Engine is null)
        {
            return new DictationStart(null, lookup.Failure ?? DictationAvailability.Ready);
        }

        // Step 4: create the microphone for this hold.
        var capture = _audio.CreateCaptureDevice(speech.CaptureDevice);
        if (capture.Device is null)
        {
            return new DictationStart(null, NoMicrophone(capture.Problem));
        }

        // Step 5: bind a recognition session to the microphone and start listening. The results are read from before
        // the session starts, because the recognizer keeps no results for a reader that arrives late and the first
        // words would otherwise be lost when the microphone delivers audio quickly.
        IRecognitionSession? session = null;
        DictationSession? dictation = null;
        try
        {
            session = await lookup.Engine.CreateSessionAsync(capture.Device, cancellationToken).ConfigureAwait(false);
            if (!session.IsAvailable)
            {
                await AbandonAsync(dictation, session, capture.Device).ConfigureAwait(false);
                return new DictationStart(null, EngineUnavailable("The speech engine could not create a session."));
            }

            dictation = new DictationSession(session, capture.Device, ReleaseSlot, _logger, _releaseTail);
            await session.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await AbandonAsync(dictation, session, capture.Device).ConfigureAwait(false);
            throw;
        }
        catch (RecognitionEngineBusyException ex)
        {
            await AbandonAsync(dictation, session, capture.Device).ConfigureAwait(false);
            LogProblem("The speech engine was still busy with an earlier session.", ex);
            return new DictationStart(null, EngineUnavailable("Speech recognition is still finishing the last request. Try again."));
        }
        catch (Exception ex) when (ex is AudioDeviceUnavailableException or AudioDeviceInUseException)
        {
            await AbandonAsync(dictation, session, capture.Device).ConfigureAwait(false);
            LogProblem("The microphone could not be started.", ex);
            return new DictationStart(null, NoMicrophone(ex.Message));
        }
        catch (Exception ex)
        {
            // A native failure while starting recognition must end as a message; the session and the microphone
            // are released first so that the next press starts clean.
            await AbandonAsync(dictation, session, capture.Device).ConfigureAwait(false);
            LogProblem("Starting speech recognition failed.", ex);
            return new DictationStart(null, EngineUnavailable(ex.Message));
        }

        // Step 6: hand the running session to the caller, who holds it until the user lets go.
        lock (_stateLock)
        {
            _active = dictation;
        }

        WriteLog(_logger, LogLevel.Info, "Dictation started.");
        return new DictationStart(dictation, DictationAvailability.Ready);
    }

    /// <summary>
    ///     Checks that a model is chosen and installed and that the native speech files are present, which must hold
    ///     before an engine is loaded.
    /// </summary>
    /// <param name="speech">The speech settings in effect.</param>
    /// <returns>Why dictation cannot be used, or <see langword="null" /> when these checks pass.</returns>
    private DictationAvailability? CheckModelAndRuntime(SpeechSettings speech)
    {
        // Step 1: a model must be chosen.
        if (string.IsNullOrWhiteSpace(speech.SttModelId))
        {
            return new DictationAvailability(
                DictationAvailabilityReason.NoModelChosen,
                "No speech-to-text model is chosen. Choose and download one on the Speech page of the settings.");
        }

        // Step 2: the chosen model must be a speech-to-text model that is installed.
        var list = _models.Enumerate();
        if (!list.IsAvailable)
        {
            return new DictationAvailability(
                DictationAvailabilityReason.ModelNotInstalled,
                $"The speech models could not be listed. {list.Problem}");
        }

        var descriptor = list.Models.FirstOrDefault(model => string.Equals(model.Id, speech.SttModelId, StringComparison.Ordinal));
        if (descriptor is null || descriptor.Role != SpeechModelRole.Recognition || descriptor.State != SpeechModelState.Downloaded)
        {
            var name = descriptor?.DisplayName ?? speech.SttModelId;
            return new DictationAvailability(
                DictationAvailabilityReason.ModelNotInstalled,
                $"The speech-to-text model '{name}' is not downloaded. Download it on the Speech page of the settings.");
        }

        // Step 3: the native files must be present, because loading the engine without them can crash the process.
        var missing = _findMissingRuntimeFiles();
        if (missing.Count > 0)
        {
            return new DictationAvailability(
                DictationAvailabilityReason.SpeechRuntimeMissing,
                $"The speech runtime is missing from this installation ({string.Join(", ", missing)}). "
                + "Reinstall CanvasPilot to use dictation.");
        }

        return null;
    }

    /// <summary>
    ///     Gets the engine for the chosen model and parameters, loading it when none is loaded or when the choice
    ///     changed since it was loaded.
    /// </summary>
    /// <param name="speech">The speech settings in effect.</param>
    /// <param name="forSession">
    ///     <see langword="true" /> when the caller is about to listen, so a changed engine may be replaced;
    ///     <see langword="false" /> for a preparation, which leaves the engine alone while a session is listening
    ///     because the session is using it.
    /// </param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The engine, or why there is none.</returns>
    private async Task<EngineLookup> GetEngineAsync(SpeechSettings speech, bool forSession, CancellationToken cancellationToken)
    {
        await _engineGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return new EngineLookup(null, EngineUnavailable("Speech recognition has been shut down."));
                }
            }

            speech.ModelParameters.TryGetValue(speech.SttModelId, out var stored);
            var key = BuildEngineKey(speech.SttModelId, stored);

            if (_engine is not null && string.Equals(key, _engineKey, StringComparison.Ordinal))
            {
                return new EngineLookup(_engine, null);
            }

            bool listening;
            lock (_stateLock)
            {
                listening = _listening;
            }

            if (!forSession && listening)
            {
                // A listening session is using the engine that is loaded; the next press loads the new one.
                return new EngineLookup(_engine, null);
            }

            // The model or its parameters changed, so the loaded engine is released before the new one is loaded.
            await DisposeEngineAsync().ConfigureAwait(false);

            var stopwatch = Stopwatch.StartNew();
            var result = await Task.Run(
                () => _models.LoadRecognizerAsync(speech.SttModelId, stored, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            if (result.Engine is null || !result.IsLoaded)
            {
                var reason = result.Status == RecognizerLoadStatus.ModelNotInstalled
                    ? DictationAvailabilityReason.ModelNotInstalled
                    : DictationAvailabilityReason.EngineUnavailable;
                return new EngineLookup(null, new DictationAvailability(reason, DescribeLoadFailure(result)));
            }

            _engine = result.Engine;
            _engineKey = key;
            WriteLog(_logger, LogLevel.Info, $"Speech engine loaded in {stopwatch.ElapsedMilliseconds} ms.");
            return new EngineLookup(_engine, null);
        }
        finally
        {
            _engineGate.Release();
        }
    }

    /// <summary>
    ///     Builds the text that identifies the engine to load: the model and the stored values of its parameters in a
    ///     fixed order, so that the same choice gives the same text.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <param name="stored">The stored parameter values, or <see langword="null" />.</param>
    /// <returns>The key.</returns>
    private static string BuildEngineKey(string modelId, IReadOnlyDictionary<string, string>? stored)
    {
        if (stored is null || stored.Count == 0)
        {
            return modelId;
        }

        var pairs = stored
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}");
        return $"{modelId}\n{string.Join('\n', pairs)}";
    }

    /// <summary>
    ///     Builds the message for an engine that could not be loaded.
    /// </summary>
    /// <param name="result">The failed load.</param>
    /// <returns>The message.</returns>
    private static string DescribeLoadFailure(RecognizerLoadResult result)
    {
        return result.Status == RecognizerLoadStatus.ModelNotInstalled
            ? $"{result.Problem} Download it on the Speech page of the settings."
            : $"{result.Problem} {SeeLogHint}";
    }

    /// <summary>
    ///     Creates the availability for an engine that cannot be used.
    /// </summary>
    /// <param name="detail">What went wrong.</param>
    /// <returns>The availability.</returns>
    private static DictationAvailability EngineUnavailable(string detail)
    {
        return new DictationAvailability(
            DictationAvailabilityReason.EngineUnavailable,
            $"Speech recognition could not be started. {detail}".TrimEnd());
    }

    /// <summary>
    ///     Creates the availability for a microphone that cannot be used.
    /// </summary>
    /// <param name="detail">What went wrong, or <see langword="null" />.</param>
    /// <returns>The availability.</returns>
    private static DictationAvailability NoMicrophone(string? detail)
    {
        var message = string.IsNullOrWhiteSpace(detail) ? "No microphone could be used." : detail.Trim();
        return new DictationAvailability(
            DictationAvailabilityReason.NoCaptureDevice,
            $"{message} Connect a microphone, choose it on the Speech page of the settings, and check that "
            + "the operating system allows desktop applications to use the microphone.");
    }

    /// <summary>
    ///     Releases the loaded engine, if there is one. The caller holds <see cref="_engineGate" />.
    /// </summary>
    /// <returns>A task that completes when the engine is released.</returns>
    private async Task DisposeEngineAsync()
    {
        var engine = _engine;
        _engine = null;
        _engineKey = null;
        if (engine is null)
        {
            return;
        }

        try
        {
            await engine.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The engine is being replaced or the application is closing; a failure to free native memory is
            // worth a log entry but not worth stopping for.
            LogProblem("Releasing the speech engine failed.", ex);
        }
    }

    /// <summary>
    ///     Gives the listening slot back, so that the next press can start a session.
    /// </summary>
    private void ReleaseSlot()
    {
        lock (_stateLock)
        {
            _listening = false;
            _active = null;
        }
    }

    /// <summary>
    ///     Releases a session and the microphone that were not handed on, and stops the reading of the results when
    ///     it has begun.
    /// </summary>
    /// <param name="dictation">The object that reads the results, or <see langword="null" /> when it was not created.</param>
    /// <param name="session">The session, or <see langword="null" /> when none was created.</param>
    /// <param name="device">The microphone.</param>
    /// <returns>A task that completes when everything is released.</returns>
    private async Task AbandonAsync(DictationSession? dictation, IRecognitionSession? session, IAudioCaptureDevice device)
    {
        if (dictation is not null)
        {
            await dictation.AbandonAsync().ConfigureAwait(false);
            return;
        }

        await DictationSession.ReleaseResourcesAsync(session, device, _logger).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes a warning to the log when there is one; logging never fails the caller.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="exception">The failure.</param>
    private void LogProblem(string message, Exception exception)
    {
        WriteLog(_logger, LogLevel.Warning, message, exception);
    }

    /// <summary>
    ///     Writes a log entry when there is a log; logging never fails the caller.
    /// </summary>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    /// <param name="level">The severity.</param>
    /// <param name="message">The message. It never contains recognized text.</param>
    /// <param name="exception">The failure, or <see langword="null" />.</param>
    private static void WriteLog(RollingFileLogger? logger, LogLevel level, string message, Exception? exception = null)
    {
        try
        {
            logger?.Log(level, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a speech problem worse.
        }
    }

    /// <summary>
    ///     The result of looking for an engine: the engine, or why there is none.
    /// </summary>
    /// <param name="Engine">The engine, or <see langword="null" />.</param>
    /// <param name="Failure">Why there is no engine, or <see langword="null" />.</param>
    private readonly record struct EngineLookup(ISpeechRecognizerEngine? Engine, DictationAvailability? Failure);

    /// <summary>
    ///     One hold of the talk button: a running recognition session over a microphone, with the words it has
    ///     recognized so far.
    /// </summary>
    private sealed class DictationSession : IDictationSession
    {
        /// <summary>
        ///     The recognition session.
        /// </summary>
        private readonly IRecognitionSession _session;

        /// <summary>
        ///     The microphone the session listens to.
        /// </summary>
        private readonly IAudioCaptureDevice _device;

        /// <summary>
        ///     Gives the service's listening slot back when the session has ended.
        /// </summary>
        private readonly Action _onEnded;

        /// <summary>
        ///     The application log, or <see langword="null" /> for none.
        /// </summary>
        private readonly RollingFileLogger? _logger;

        /// <summary>
        ///     How long to keep listening after the end is requested.
        /// </summary>
        private readonly TimeSpan _releaseTail;

        /// <summary>
        ///     Stops the enumeration of results when the caller gives up waiting.
        /// </summary>
        private readonly CancellationTokenSource _giveUp = new();

        /// <summary>
        ///     Guards the recognized text and the end of the session.
        /// </summary>
        private readonly Lock _lock = new();

        /// <summary>
        ///     The finished sentences, in the order they were recognized.
        /// </summary>
        private readonly List<string> _finals = [];

        /// <summary>
        ///     The task that reads the results of the recognition session.
        /// </summary>
        private readonly Task _pump;

        /// <summary>
        ///     The sentence that is still being recognized, or an empty string.
        /// </summary>
        private string _provisional = string.Empty;

        /// <summary>
        ///     The text that <see cref="PartialText" /> returns.
        /// </summary>
        private string _partial = string.Empty;

        /// <summary>
        ///     The explanation of a failure of the recognition, or an empty string.
        /// </summary>
        private string _problem = string.Empty;

        /// <summary>
        ///     The task that ends the session, once ending has begun.
        /// </summary>
        private Task<string>? _ending;

        /// <summary>
        ///     Starts reading the results of a recognition session. The first step of the enumeration is taken here, on
        ///     the calling thread, which subscribes the reader before the session is started, because the recognizer
        ///     keeps no results for a reader that is not yet reading and a microphone that delivers audio quickly would
        ///     otherwise lose the first words. The caller starts the session right after this object is created.
        /// </summary>
        /// <param name="session">The recognition session, which is started right after this object is created.</param>
        /// <param name="device">The microphone the session listens to.</param>
        /// <param name="onEnded">Gives the service's listening slot back.</param>
        /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
        /// <param name="releaseTail">How long to keep listening after the end is requested.</param>
        public DictationSession(IRecognitionSession session, IAudioCaptureDevice device, Action onEnded, RollingFileLogger? logger, TimeSpan releaseTail)
        {
            _session = session;
            _device = device;
            _onEnded = onEnded;
            _logger = logger;
            _releaseTail = releaseTail;

            // The reader is subscribed by taking the first step of the enumeration at once; a failure to do so is a
            // problem of the session, which then simply has no results.
            IAsyncEnumerator<SpeechRecognitionEvent>? results = null;
            Task<bool> firstStep;
            try
            {
                results = session.GetResultsAsync(_giveUp.Token).GetAsyncEnumerator(_giveUp.Token);
                firstStep = results.MoveNextAsync().AsTask();
            }
            catch (Exception ex)
            {
                SetProblem("Speech recognition could not start reading results.", ex);
                firstStep = Task.FromResult(false);
            }

            _pump = Task.Run(() => PumpAsync(results, firstStep), CancellationToken.None);
        }

        /// <inheritdoc />
        public event EventHandler? PartialTextChanged;

        /// <inheritdoc />
        public string PartialText
        {
            get
            {
                lock (_lock)
                {
                    return _partial;
                }
            }
        }

        /// <inheritdoc />
        public string Problem
        {
            get
            {
                lock (_lock)
                {
                    return _problem;
                }
            }
        }

        /// <summary>
        ///     Releases a recognition session and its microphone, ignoring failures: this runs when a session ends or
        ///     could not start, and there is nothing more the caller could do about a failure to release.
        /// </summary>
        /// <param name="session">The session, or <see langword="null" />.</param>
        /// <param name="device">The microphone; disposed when it is disposable.</param>
        /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
        /// <returns>A task that completes when both are released.</returns>
        public static async Task ReleaseResourcesAsync(IRecognitionSession? session, IAudioCaptureDevice device, RollingFileLogger? logger)
        {
            try
            {
                if (session is not null)
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }

                (device as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                WriteLog(logger, LogLevel.Warning, "Releasing the dictation session failed.", ex);
            }
        }

        /// <summary>
        ///     Gives up a session that never started listening: stops reading its results and releases the session and
        ///     the microphone, without asking the recognizer to stop. It does not give the listening slot back; the
        ///     caller of the start does.
        /// </summary>
        /// <returns>A task that completes when everything is released.</returns>
        public async Task AbandonAsync()
        {
            await _giveUp.CancelAsync().ConfigureAwait(false);
            try
            {
                await _pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The reading was cancelled before it began, which is the expected end of an abandoned session.
            }

            await ReleaseResourcesAsync(_session, _device, _logger).ConfigureAwait(false);
            _giveUp.Dispose();
        }

        /// <inheritdoc />
        public Task<string> EndAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                // The first call does the work, on its own thread so that nothing runs under the lock; later calls,
                // including the one from DisposeAsync, get the same text.
                return _ending ??= Task.Run(() => FinishAsync(cancellationToken), CancellationToken.None);
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            // Letting go of a session without ending it, for example when the window closes, still stops the
            // microphone; the text is no longer wanted.
            await EndAsync(CancellationToken.None).ConfigureAwait(false);
            _giveUp.Dispose();
        }

        /// <summary>
        ///     Stops the recognition, waits for it to finish the audio it has heard, releases everything, and gives
        ///     the slot back.
        /// </summary>
        /// <param name="cancellationToken">Gives up waiting.</param>
        /// <returns>The final text.</returns>
        private async Task<string> FinishAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Step 1: keep listening for a moment so that the last word is not cut off, then stop the microphone
                // and let the recognizer finish what it has already heard.
                if (_releaseTail > TimeSpan.Zero)
                {
                    await Task.Delay(_releaseTail, cancellationToken).ConfigureAwait(false);
                }

                await _session.StopAsync(cancellationToken).ConfigureAwait(false);
                await _pump.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The caller gave up waiting; the text recognized so far is what they get.
                await _giveUp.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SetProblem("Speech recognition stopped with an error.", ex);
            }

            // Step 2: release the session and the microphone, and give the slot back, whatever happened above.
            await ReleaseResourcesAsync(_session, _device, _logger).ConfigureAwait(false);
            _onEnded();
            WriteLog(_logger, LogLevel.Info, "Dictation ended.");

            lock (_lock)
            {
                return Compose();
            }
        }

        /// <summary>
        ///     Reads the recognition results until the session ends, keeping the text up to date.
        /// </summary>
        /// <param name="results">The enumerator of the results, which this method disposes, or <see langword="null" /> when it could not be created.</param>
        /// <param name="firstStep">The first step of the enumeration, which the caller took before the session started.</param>
        /// <returns>A task that completes when the results end.</returns>
        private async Task PumpAsync(IAsyncEnumerator<SpeechRecognitionEvent>? results, Task<bool> firstStep)
        {
            try
            {
                for (var more = await firstStep.ConfigureAwait(false); more && results is not null; more = await results.MoveNextAsync().ConfigureAwait(false))
                {
                    Apply(results.Current.Result);
                }
            }
            catch (OperationCanceledException)
            {
                // The caller gave up waiting; the text so far stands.
            }
            catch (Exception ex)
            {
                // A fault inside the native recognizer ends the enumeration; what was recognized before it is kept,
                // and the problem is available to the caller.
                SetProblem("Speech recognition failed while listening.", ex);
            }
            finally
            {
                if (results is not null)
                {
                    await results.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        ///     Updates the text with one result: a provisional result replaces the sentence being recognized and a
        ///     final result finishes it.
        /// </summary>
        /// <param name="result">The result.</param>
        private void Apply(SpeechRecognitionResult result)
        {
            var text = result.Text?.Trim() ?? string.Empty;
            bool changed;
            lock (_lock)
            {
                if (result.IsFinal)
                {
                    if (text.Length > 0)
                    {
                        _finals.Add(text);
                    }

                    _provisional = string.Empty;
                }
                else
                {
                    _provisional = text;
                }

                var composed = Compose();
                changed = !string.Equals(composed, _partial, StringComparison.Ordinal);
                _partial = composed;
            }

            if (!changed)
            {
                return;
            }

            try
            {
                PartialTextChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // A faulty subscriber must not stop the recognition, which runs on a thread that nobody else
                // observes; the failure is logged instead.
                SetProblem("A handler of the dictation text failed.", ex);
            }
        }

        /// <summary>
        ///     Joins the finished sentences and the sentence that is still being recognized with single spaces. The
        ///     caller holds the lock.
        /// </summary>
        /// <returns>The text, trimmed.</returns>
        private string Compose()
        {
            IEnumerable<string> parts = _provisional.Length > 0 ? [.. _finals, _provisional] : _finals;
            return string.Join(' ', parts).Trim();
        }

        /// <summary>
        ///     Records a failure: the first one becomes the problem text, and every one is logged.
        /// </summary>
        /// <param name="message">What failed.</param>
        /// <param name="exception">The failure.</param>
        private void SetProblem(string message, Exception exception)
        {
            lock (_lock)
            {
                if (_problem.Length == 0)
                {
                    _problem = $"{message} {exception.Message}".TrimEnd();
                }
            }

            WriteLog(_logger, LogLevel.Warning, message, exception);
        }
    }
}

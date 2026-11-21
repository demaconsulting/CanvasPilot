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
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Sherpa;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     Lists, downloads, and loads speech models through the DemaConsulting.Speech library's
///     <see cref="SpeechModelCatalog" />, filled with the models of the Sherpa package. The catalog uses the
///     library's default model store, which is shared with other DEMA Consulting applications. The catalog is
///     created on first use and released by <see cref="Dispose" />.
/// </summary>
public sealed class SpeechModelService : ISpeechModelService, IDisposable
{
    /// <summary>
    ///     Creates the catalog on first use.
    /// </summary>
    private readonly Func<SpeechModelCatalog> _catalogFactory;

    /// <summary>
    ///     Loads the engine of an installed speech-to-text model; the library's recognizer factory in the running
    ///     application.
    /// </summary>
    private readonly Func<IRecognitionModel, SpeechModelCatalog, ISpeechDiagnostics, IReadOnlyDictionary<string, object>?, CancellationToken, Task<ISpeechRecognizerEngine>> _recognizerLoader;

    /// <summary>
    ///     The application log, or <see langword="null" /> for none.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The catalog, once created.
    /// </summary>
    private SpeechModelCatalog? _catalog;

    /// <summary>
    ///     Whether the service has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Creates a service over the library's default model store with the Sherpa models.
    /// </summary>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    public SpeechModelService(RollingFileLogger? logger = null)
        : this(CreateDefaultCatalog, logger)
    {
    }

    /// <summary>
    ///     Creates a service over a catalog that is created on first use, so tests can use a store in a temporary
    ///     folder or cause the creation to fail.
    /// </summary>
    /// <param name="catalogFactory">Creates the catalog.</param>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    /// <param name="recognizerLoader">
    ///     Loads the engine of a speech-to-text model, or <see langword="null" /> for the library's recognizer
    ///     factory. Tests substitute it so that no native speech runtime is loaded.
    /// </param>
    internal SpeechModelService(
        Func<SpeechModelCatalog> catalogFactory,
        RollingFileLogger? logger = null,
        Func<IRecognitionModel, SpeechModelCatalog, ISpeechDiagnostics, IReadOnlyDictionary<string, object>?, CancellationToken, Task<ISpeechRecognizerEngine>>? recognizerLoader = null)
    {
        ArgumentNullException.ThrowIfNull(catalogFactory);

        _catalogFactory = catalogFactory;
        _logger = logger;
        _recognizerLoader = recognizerLoader ?? SpeechRecognizerFactory.LoadAsync;
    }

    /// <inheritdoc />
    public SpeechModelList Enumerate()
    {
        try
        {
            return new SpeechModelList(GetCatalog().Enumerate());
        }
        catch (Exception ex)
        {
            // The model store is files in the user's profile and can be unreadable or missing its drive. That must
            // not stop the settings dialog from opening.
            LogProblem("Listing the speech models failed.", ex);
            return SpeechModelList.Unavailable($"The speech models could not be listed. {ex.Message}".TrimEnd());
        }
    }

    /// <inheritdoc />
    public async Task<SpeechModelDownloadReport> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        try
        {
            var result = await GetCatalog().DownloadAsync(modelId, progress, cancellationToken).ConfigureAwait(false);
            return result.Outcome == SpeechModelDownloadOutcome.Installed
                ? new SpeechModelDownloadReport(SpeechModelDownloadStatus.Installed)
                : new SpeechModelDownloadReport(SpeechModelDownloadStatus.Failed, DescribeFailure(result));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Only the caller's own cancellation is a cancellation; a timeout inside the download is a failure.
            return new SpeechModelDownloadReport(SpeechModelDownloadStatus.Canceled, "Download canceled.");
        }
        catch (Exception ex)
        {
            // Downloads cross the network and the file system. Whatever fails, including an unknown model
            // identifier or a store that cannot be created, reaches the user as a message beside the model.
            LogProblem($"Downloading the speech model '{modelId}' failed.", ex);
            return new SpeechModelDownloadReport(SpeechModelDownloadStatus.Failed, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<RecognizerLoadResult> LoadRecognizerAsync(
        string modelId,
        IReadOnlyDictionary<string, string>? storedParameters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: find the model and make sure it is a speech-to-text model that is installed, because the library
        // answers a missing model with an unavailable engine that says nothing about why.
        SpeechModelCatalog catalog;
        SpeechModelDescriptor? descriptor;
        try
        {
            catalog = GetCatalog();
            descriptor = catalog.Enumerate().FirstOrDefault(model => string.Equals(model.Id, modelId, StringComparison.Ordinal));
        }
        catch (Exception ex)
        {
            LogProblem("Listing the speech models to load a recognizer failed.", ex);
            return RecognizerLoadResult.Failure(
                RecognizerLoadStatus.ModelNotInstalled,
                $"The speech models could not be listed. {ex.Message}".TrimEnd());
        }

        if (descriptor is null)
        {
            return RecognizerLoadResult.Failure(
                RecognizerLoadStatus.ModelNotInstalled,
                $"The speech model '{modelId}' is not known.");
        }

        if (descriptor.Model is not IRecognitionModel model)
        {
            return RecognizerLoadResult.Failure(
                RecognizerLoadStatus.ModelNotInstalled,
                $"The speech model '{descriptor.DisplayName}' is not a speech-to-text model.");
        }

        if (descriptor.State != SpeechModelState.Downloaded)
        {
            return RecognizerLoadResult.Failure(
                RecognizerLoadStatus.ModelNotInstalled,
                $"The speech model '{descriptor.DisplayName}' is not downloaded.");
        }

        // Step 2: load the engine. The library reports an absent native runtime as an unavailable engine, but a
        // damaged model or an invalid parameter can still throw, and none of that may reach the user as a crash.
        ISpeechRecognizerEngine engine;
        try
        {
            engine = await _recognizerLoader(
                model,
                catalog,
                new LogSpeechDiagnostics(_logger),
                ToParameterValues(model, storedParameters),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Only the caller's own cancellation is a cancellation; anything else is a failure to load.
            throw;
        }
        catch (Exception ex)
        {
            LogProblem($"Loading the speech model '{modelId}' failed.", ex);
            return RecognizerLoadResult.Failure(
                RecognizerLoadStatus.EngineUnavailable,
                $"The speech engine of '{descriptor.DisplayName}' could not be loaded. {ex.Message}".TrimEnd());
        }

        // Step 3: an engine that says it is unavailable cannot recognize anything, so it is not handed on.
        if (!engine.IsAvailable)
        {
            await DisposeEngineAsync(engine).ConfigureAwait(false);
            LogProblem($"The speech engine of '{modelId}' is unavailable.", null);
            return RecognizerLoadResult.Failure(
                RecognizerLoadStatus.EngineUnavailable,
                $"The speech engine of '{descriptor.DisplayName}' could not be started. "
                + "The speech runtime may be missing or damaged.");
        }

        return RecognizerLoadResult.Success(engine);
    }

    /// <summary>
    ///     Releases the catalog if it was created.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog?.Dispose();
        _catalog = null;
    }

    /// <summary>
    ///     Converts the stored parameter values, which are invariant-culture strings, into the typed values that the
    ///     library takes, by the same rules as the model parameter editor: a number is clamped to its range and
    ///     rounded when it is a whole number, a choice must be one of the options, and a switch must be true or
    ///     false. A value that is none of these is left out, so that the model's default applies.
    /// </summary>
    /// <param name="model">The model, whose declared parameters decide each value's type.</param>
    /// <param name="stored">The stored values by parameter identifier, or <see langword="null" />.</param>
    /// <returns>The typed values, or <see langword="null" /> when there are none.</returns>
    internal static IReadOnlyDictionary<string, object>? ToParameterValues(
        ISpeechModel model,
        IReadOnlyDictionary<string, string>? stored)
    {
        if (stored is null || stored.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var parameter in model.Parameters)
        {
            if (!stored.TryGetValue(parameter.Id, out var text) || text is null)
            {
                continue;
            }

            switch (parameter)
            {
                case NumericParameter numeric
                    when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                         && double.IsFinite(number):
                    var clamped = Math.Clamp(number, numeric.Minimum, numeric.Maximum);
                    values[parameter.Id] = numeric.IsInteger ? Math.Round(clamped, MidpointRounding.AwayFromZero) : clamped;
                    break;
                case ChoiceParameter choice when choice.Options.Any(option => string.Equals(option.Value, text, StringComparison.Ordinal)):
                    values[parameter.Id] = text;
                    break;
                case BooleanParameter when bool.TryParse(text, out var flag):
                    values[parameter.Id] = flag;
                    break;
            }
        }

        return values.Count == 0 ? null : values;
    }

    /// <summary>
    ///     Creates the catalog of the real service: the library's default store plus the Sherpa models.
    /// </summary>
    /// <returns>The catalog.</returns>
    private static SpeechModelCatalog CreateDefaultCatalog()
    {
        var catalog = new SpeechModelCatalog();
        try
        {
            return catalog.AddSherpaModels();
        }
        catch
        {
            catalog.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Builds the text that tells the user why a download did not install the model.
    /// </summary>
    /// <param name="result">The library's result.</param>
    /// <returns>The text.</returns>
    internal static string DescribeFailure(SpeechModelDownloadResult result)
    {
        var detail = result.Error?.Message;
        var reason = result.Outcome switch
        {
            SpeechModelDownloadOutcome.ChecksumMismatch =>
                "The downloaded files did not match their expected checksums and were discarded.",
            SpeechModelDownloadOutcome.NetworkBlocked =>
                "The network blocked the download.",
            SpeechModelDownloadOutcome.HttpError =>
                "The download server reported an error.",
            SpeechModelDownloadOutcome.IoFailure =>
                "The model files could not be written to the model store.",
            _ => "The download could not be completed.",
        };

        return string.IsNullOrWhiteSpace(detail) ? reason : $"{reason} {detail}";
    }

    /// <summary>
    ///     Gets the catalog, creating it on first use.
    /// </summary>
    /// <returns>The catalog.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the service has been disposed.</exception>
    private SpeechModelCatalog GetCatalog()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _catalog ??= _catalogFactory();
    }

    /// <summary>
    ///     Writes a warning to the log when there is one; logging never fails the caller.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="exception">The failure, or <see langword="null" /> when the problem is not an exception.</param>
    private void LogProblem(string message, Exception? exception)
    {
        try
        {
            _logger?.Log(LogLevel.Warning, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a model problem worse.
        }
    }

    /// <summary>
    ///     Releases an engine that is not handed on; a failure to release it is only logged.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task that completes when the engine is released.</returns>
    private async Task DisposeEngineAsync(ISpeechRecognizerEngine engine)
    {
        try
        {
            await engine.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The engine is being thrown away because it cannot work; a second failure adds nothing for the user.
            LogProblem("Releasing an unavailable speech engine failed.", ex);
        }
    }

    /// <summary>
    ///     Forwards the speech library's structural diagnostics to the application log. The library promises never to
    ///     report audio or recognized text through this channel, so it is safe to write to a log that users attach to
    ///     bug reports.
    /// </summary>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    private sealed class LogSpeechDiagnostics(RollingFileLogger? logger) : ISpeechDiagnostics
    {
        /// <inheritdoc />
        public void Report(SpeechDiagnosticLevel level, string category, string message)
        {
            try
            {
                var mapped = level switch
                {
                    SpeechDiagnosticLevel.Error => LogLevel.Error,
                    SpeechDiagnosticLevel.Warning => LogLevel.Warning,
                    _ => LogLevel.Info,
                };
                logger?.Log(mapped, $"Speech library ({category}): {message}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: a full disk must not make speech recognition fail.
            }
        }
    }
}

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

using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     Lists and downloads speech models through the DemaConsulting.Speech library's
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
    internal SpeechModelService(Func<SpeechModelCatalog> catalogFactory, RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalogFactory);

        _catalogFactory = catalogFactory;
        _logger = logger;
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
    /// <param name="exception">The failure.</param>
    private void LogProblem(string message, Exception exception)
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
}

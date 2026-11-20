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
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="SpeechModelService" />. The catalog is the real library catalog over a model store
///     in a temporary folder that holds substitute models, so nothing is downloaded and the shared per-user model
///     store is never touched.
/// </summary>
public sealed class SpeechModelServiceTests : IDisposable
{
    /// <summary>
    ///     Temporary folder for the model store and log, created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the models of the catalog are listed with their role and an install state of not downloaded
    ///     when the store holds nothing.
    /// </summary>
    [Fact]
    public void SpeechModelService_Enumerate_CatalogWithModels_ListsModelsWithState()
    {
        // Arrange: a catalog over an empty store with one model of each role
        using var service = CreateService(
            SpeechTestData.Model("stt-one", SpeechModelRole.Recognition),
            SpeechTestData.Model("tts-one", SpeechModelRole.Synthesis, SpeechTestData.Choice()));

        // Act: enumerate
        var list = service.Enumerate();

        // Assert: both models are listed, not downloaded, and the parameters of the model are reachable
        Assert.True(list.IsAvailable);
        Assert.Equal(["stt-one", "tts-one"], list.Models.Select(model => model.Id));
        Assert.Equal([SpeechModelRole.Recognition, SpeechModelRole.Synthesis], list.Models.Select(model => model.Role));
        Assert.All(list.Models, model => Assert.Equal(SpeechModelState.NotDownloaded, model.State));
        Assert.Single(list.Models[1].Model.Parameters);
    }

    /// <summary>
    ///     Test that a catalog that cannot be created is an unavailable list with a problem, not an exception.
    /// </summary>
    [Fact]
    public void SpeechModelService_Enumerate_CatalogCannotBeCreated_ReturnsUnavailableWithProblem()
    {
        // Arrange: a service whose model store is unusable, and a log
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        using var service = new SpeechModelService(
            () => throw new IOException("The drive is not ready."),
            logger);

        // Act: enumerate
        var list = service.Enumerate();

        // Assert: no models, an explanation, and a log entry
        Assert.Empty(list.Models);
        Assert.False(list.IsAvailable);
        Assert.Contains("The drive is not ready.", list.Problem, StringComparison.Ordinal);
        Assert.Contains("Listing the speech models failed.", File.ReadAllText(logger.ActiveFilePath), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the catalog is created only once, on first use, and that creating the service does not create it.
    /// </summary>
    [Fact]
    public void SpeechModelService_Catalog_IsCreatedOnFirstUseOnly()
    {
        // Arrange: a factory that counts its calls
        var created = 0;
        using var service = new SpeechModelService(() =>
        {
            created++;
            return new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_tempRoot, "models") });
        });
        var afterConstruction = created;

        // Act: enumerate twice
        service.Enumerate();
        service.Enumerate();

        // Assert: nothing was created at construction, and one catalog served both calls
        Assert.Equal(0, afterConstruction);
        Assert.Equal(1, created);
    }

    /// <summary>
    ///     Test that a token that is already cancelled gives a cancelled report without any transfer.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_DownloadAsync_CancelledToken_ReturnsCanceled()
    {
        // Arrange: a catalog with a model, and a token that is already cancelled
        using var service = CreateService(SpeechTestData.Model("stt-one", SpeechModelRole.Recognition));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act: download
        var report = await service.DownloadAsync("stt-one", null, source.Token);

        // Assert: the download was cancelled and nothing was installed
        Assert.Equal(SpeechModelDownloadStatus.Canceled, report.Status);
        Assert.False(report.IsInstalled);
        Assert.Equal("Download canceled.", report.Message);
        Assert.Equal(SpeechModelState.NotDownloaded, service.Enumerate().Models.Single().State);
    }

    /// <summary>
    ///     Test that a model that is not in the catalog is a failed report with the library's message, not an
    ///     exception, and that the failure is logged.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_DownloadAsync_UnknownModel_ReturnsFailedWithMessage()
    {
        // Arrange: a catalog without the requested model, and a log
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        using var service = CreateService(logger, SpeechTestData.Model("stt-one", SpeechModelRole.Recognition));

        // Act: download a model the catalog does not know
        var report = await service.DownloadAsync("missing", null, CancellationToken.None);

        // Assert: failed with a message naming the model, and logged
        Assert.Equal(SpeechModelDownloadStatus.Failed, report.Status);
        Assert.Contains("missing", report.Message, StringComparison.Ordinal);
        Assert.Contains("Downloading the speech model 'missing' failed.", await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every library outcome that is not an install gives a reason, and the library's detail when
    ///     there is one.
    /// </summary>
    [Theory]
    [InlineData(SpeechModelDownloadOutcome.ChecksumMismatch, "did not match their expected checksums")]
    [InlineData(SpeechModelDownloadOutcome.NetworkBlocked, "network blocked")]
    [InlineData(SpeechModelDownloadOutcome.HttpError, "server reported an error")]
    [InlineData(SpeechModelDownloadOutcome.IoFailure, "could not be written")]
    [InlineData(SpeechModelDownloadOutcome.Failed, "could not be completed")]
    public void SpeechModelService_DescribeFailure_EachOutcome_GivesReasonAndDetail(SpeechModelDownloadOutcome outcome, string expectedReason)
    {
        // Arrange: a result with and without an underlying error
        var withError = new SpeechModelDownloadResult(outcome, new InvalidOperationException("detail text"));
        var withoutError = new SpeechModelDownloadResult(outcome);

        // Act: describe both
        var detailed = SpeechModelService.DescribeFailure(withError);
        var plain = SpeechModelService.DescribeFailure(withoutError);

        // Assert: the reason is always given, and the detail only when there is one
        Assert.Contains(expectedReason, detailed, StringComparison.Ordinal);
        Assert.EndsWith("detail text", detailed, StringComparison.Ordinal);
        Assert.Contains(expectedReason, plain, StringComparison.Ordinal);
        Assert.DoesNotContain("detail text", plain, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a report is installed only for the installed status.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadReport_IsInstalled_OnlyForInstalledStatus()
    {
        // Act & Assert: each status
        Assert.True(new SpeechModelDownloadReport(SpeechModelDownloadStatus.Installed).IsInstalled);
        Assert.False(new SpeechModelDownloadReport(SpeechModelDownloadStatus.Canceled).IsInstalled);
        Assert.False(new SpeechModelDownloadReport(SpeechModelDownloadStatus.Failed, "x").IsInstalled);
    }

    /// <summary>
    ///     Test that a disposed service reports unavailable and failed results instead of throwing, and that
    ///     disposing twice is harmless.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_Dispose_RefusesFurtherUseWithResults()
    {
        // Arrange: a service that has been used and disposed
        var service = CreateService(SpeechTestData.Model("stt-one", SpeechModelRole.Recognition));
        service.Enumerate();
        service.Dispose();

        // Act: dispose again, then use it
        service.Dispose();
        var list = service.Enumerate();
        var report = await service.DownloadAsync("stt-one", null, CancellationToken.None);

        // Assert: results, not exceptions
        Assert.False(list.IsAvailable);
        Assert.Equal(SpeechModelDownloadStatus.Failed, report.Status);
    }

    /// <summary>
    ///     Test that missing or blank arguments are refused.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_InvalidArguments_Throw()
    {
        // Arrange: a service
        using var service = CreateService();

        // Act & Assert: a missing factory and a blank model identifier are refused
        Assert.Throws<ArgumentNullException>(() => new SpeechModelService((Func<SpeechModelCatalog>)null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadAsync(" ", null, CancellationToken.None));
    }

    /// <summary>
    ///     Creates a service over a catalog in the temporary store that holds the given models.
    /// </summary>
    /// <param name="models">The models.</param>
    /// <returns>The service.</returns>
    private SpeechModelService CreateService(params ISpeechModel[] models)
    {
        return CreateService(null, models);
    }

    /// <summary>
    ///     Creates a service over a catalog in the temporary store that holds the given models.
    /// </summary>
    /// <param name="logger">The log, or <see langword="null" />.</param>
    /// <param name="models">The models.</param>
    /// <returns>The service.</returns>
    private SpeechModelService CreateService(RollingFileLogger? logger, params ISpeechModel[] models)
    {
        return new SpeechModelService(
            () => new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_tempRoot, "models") }).AddModels(models),
            logger);
    }
}

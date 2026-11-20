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
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

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
    ///     Test that an installed speech-to-text model is loaded through the loader with its stored parameters
    ///     converted to typed values, and that the engine is returned.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_LoadRecognizerAsync_InstalledModel_ReturnsEngineWithTypedParameters()
    {
        // Arrange: an installed model with a whole-number, a choice, and a boolean parameter, and a loader that
        // records what it was given instead of loading a native engine
        var engine = new FakeRecognizerEngine();
        IReadOnlyDictionary<string, object>? received = null;
        var model = new FakeRecognitionModel(
            "stt-one",
            () => new FakeRecognitionBackend([], new SpeechRecognitionResult(string.Empty, IsFinal: true)),
            [SpeechTestData.Numeric("beam", isInteger: true), SpeechTestData.Choice("lang"), SpeechTestData.Boolean("echo")]);
        DictationTestFiles.InstallModel(Path.Combine(_tempRoot, "models"), "stt-one");
        using var service = CreateRecognizerService(
            model,
            (_, _, _, values, _) =>
            {
                received = values;
                return Task.FromResult<ISpeechRecognizerEngine>(engine);
            });

        // Act: load with stored values, one of which is out of range
        var result = await service.LoadRecognizerAsync(
            "stt-one",
            new Dictionary<string, string> { ["beam"] = "99.6", ["lang"] = "c", ["echo"] = "true" },
            TestContext.Current.CancellationToken);

        // Assert: the engine is handed on, and the values are a clamped whole number, the choice, and a boolean
        Assert.True(result.IsLoaded);
        Assert.Same(engine, result.Engine);
        Assert.Equal(10d, received!["beam"]);
        Assert.Equal("c", received["lang"]);
        Assert.True((bool)received["echo"]);
    }

    /// <summary>
    ///     Test that stored values that are not values of their parameter are left out, so that the model's default
    ///     applies, and that no stored values at all is no bag at all.
    /// </summary>
    [Fact]
    public void SpeechModelService_ToParameterValues_InvalidOrMissingValues_AreLeftOut()
    {
        // Arrange: a model with four parameters
        var model = SpeechTestData.Model(
            "stt-one",
            SpeechModelRole.Recognition,
            SpeechTestData.Numeric("speed"),
            SpeechTestData.Numeric("beam", isInteger: true),
            SpeechTestData.Choice("lang"),
            SpeechTestData.Boolean("echo"));

        // Act: convert garbage, a mix, an unknown parameter, and nothing
        var garbage = SpeechModelService.ToParameterValues(
            model,
            new Dictionary<string, string> { ["speed"] = "fast", ["beam"] = "NaN", ["lang"] = "z", ["echo"] = "maybe" });
        var mixed = SpeechModelService.ToParameterValues(
            model,
            new Dictionary<string, string> { ["speed"] = "2.5", ["beam"] = "3.5", ["lang"] = "a", ["echo"] = "false", ["other"] = "1" });
        var none = SpeechModelService.ToParameterValues(model, null);
        var empty = SpeechModelService.ToParameterValues(model, new Dictionary<string, string>());

        // Assert: garbage is dropped, valid values are typed (a whole number is rounded away from zero), unknown
        // parameters are ignored, and nothing stored means no bag
        Assert.Null(garbage);
        Assert.Equal(["speed", "beam", "lang", "echo"], mixed!.Keys);
        Assert.Equal(2.5d, mixed["speed"]);
        Assert.Equal(4d, mixed["beam"]);
        Assert.Equal("a", mixed["lang"]);
        Assert.False((bool)mixed["echo"]);
        Assert.Null(none);
        Assert.Null(empty);
    }

    /// <summary>
    ///     Test that a model that is unknown, is not a speech-to-text model, or is not downloaded is a result
    ///     that names the problem, and that none of them loads an engine.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_LoadRecognizerAsync_UnknownWrongKindOrNotDownloaded_ReturnsModelNotInstalled()
    {
        // Arrange: a loader that would fail the test, and a catalog with a synthesis model and an uninstalled model
        var loaded = false;
        var uninstalled = new FakeRecognitionModel("stt-two", () => throw new InvalidOperationException("must not load"));
        using var service = CreateRecognizerService(
            uninstalled,
            (_, _, _, _, _) =>
            {
                loaded = true;
                return Task.FromResult<ISpeechRecognizerEngine>(new FakeRecognizerEngine());
            },
            null,
            SpeechTestData.Model("tts-one", SpeechModelRole.Synthesis));

        // Act: load each
        var unknown = await service.LoadRecognizerAsync("nope", null, TestContext.Current.CancellationToken);
        var wrongKind = await service.LoadRecognizerAsync("tts-one", null, TestContext.Current.CancellationToken);
        var missing = await service.LoadRecognizerAsync("stt-two", null, TestContext.Current.CancellationToken);

        // Assert: each is not installed with its own explanation, and the loader was never called
        Assert.All([unknown, wrongKind, missing], result =>
        {
            Assert.Equal(RecognizerLoadStatus.ModelNotInstalled, result.Status);
            Assert.Null(result.Engine);
        });
        Assert.Contains("'nope' is not known", unknown.Problem, StringComparison.Ordinal);
        Assert.Contains("not a speech-to-text model", wrongKind.Problem, StringComparison.Ordinal);
        Assert.Contains("not downloaded", missing.Problem, StringComparison.Ordinal);
        Assert.False(loaded);
    }

    /// <summary>
    ///     Test that an engine that reports itself unavailable is released and reported, and that a loader that
    ///     throws is reported and logged, both as an engine that is unavailable.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_LoadRecognizerAsync_EngineUnavailableOrLoaderThrows_ReturnsEngineUnavailable()
    {
        // Arrange: an installed model, and a loader that first returns an unavailable engine and then throws
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var unavailable = new FakeRecognizerEngine { IsAvailable = false };
        var calls = 0;
        var model = new FakeRecognitionModel("stt-one", () => throw new InvalidOperationException("not used"));
        DictationTestFiles.InstallModel(Path.Combine(_tempRoot, "models"), "stt-one");
        using var service = CreateRecognizerService(
            model,
            (_, _, _, _, _) =>
            {
                calls++;
                return calls == 1
                    ? Task.FromResult<ISpeechRecognizerEngine>(unavailable)
                    : throw new BadImageFormatException("wrong architecture");
            },
            logger: logger);

        // Act: load twice
        var first = await service.LoadRecognizerAsync("stt-one", null, TestContext.Current.CancellationToken);
        var second = await service.LoadRecognizerAsync("stt-one", null, TestContext.Current.CancellationToken);

        // Assert: both are unavailable engines with an explanation, the first engine was released, and the throw is
        // in the log
        Assert.Equal(RecognizerLoadStatus.EngineUnavailable, first.Status);
        Assert.Contains("speech runtime", first.Problem, StringComparison.Ordinal);
        Assert.True(unavailable.Disposed);
        Assert.Equal(RecognizerLoadStatus.EngineUnavailable, second.Status);
        Assert.Contains("wrong architecture", second.Problem, StringComparison.Ordinal);
        Assert.Contains("wrong architecture", await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the diagnostics that the speech library reports while loading reach the application log with
    ///     their level and category.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_LoadRecognizerAsync_LibraryDiagnostics_AreWrittenToTheLog()
    {
        // Arrange: a loader that reports through the diagnostics it is given
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var model = new FakeRecognitionModel("stt-one", () => throw new InvalidOperationException("not used"));
        DictationTestFiles.InstallModel(Path.Combine(_tempRoot, "models"), "stt-one");
        using var service = CreateRecognizerService(
            model,
            (_, _, diagnostics, _, _) =>
            {
                diagnostics.Report(SpeechDiagnosticLevel.Warning, "Recognition", "fell back to unavailable engine");
                return Task.FromResult<ISpeechRecognizerEngine>(new FakeRecognizerEngine());
            },
            logger: logger);

        // Act: load
        await service.LoadRecognizerAsync("stt-one", null, TestContext.Current.CancellationToken);

        // Assert: the log has the category and message with the warning level
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("Speech library (Recognition): fell back to unavailable engine", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a cancelled token is a cancellation, not a failed load, and that a catalog that cannot be created
    ///     is a result.
    /// </summary>
    [Fact]
    public async Task SpeechModelService_LoadRecognizerAsync_CancelledOrCatalogBroken_CancelsOrReturnsProblem()
    {
        // Arrange: a service over a working catalog, and a service whose catalog cannot be created
        using var working = CreateService(SpeechTestData.Model("stt-one", SpeechModelRole.Recognition));
        using var broken = new SpeechModelService(() => throw new IOException("No model store."));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act: load with the cancelled token, and from the broken service
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => working.LoadRecognizerAsync("stt-one", null, cancelled.Token));
        var result = await broken.LoadRecognizerAsync("stt-one", null, TestContext.Current.CancellationToken);

        // Assert: the broken catalog is a result with its message
        Assert.Equal(RecognizerLoadStatus.ModelNotInstalled, result.Status);
        Assert.Contains("No model store.", result.Problem, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ArgumentException>(() => working.LoadRecognizerAsync(" ", null, TestContext.Current.CancellationToken));
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

    /// <summary>
    ///     Creates a service over a catalog in the temporary store that holds the given model and any others, with a
    ///     substitute for the library's recognizer factory so that no native speech runtime is ever loaded.
    /// </summary>
    /// <param name="model">The speech-to-text model; a test installs it by writing into the store first.</param>
    /// <param name="loader">Stands in for the library's recognizer factory.</param>
    /// <param name="logger">The log, or <see langword="null" />.</param>
    /// <param name="others">Further models for the catalog.</param>
    /// <returns>The service.</returns>
    private SpeechModelService CreateRecognizerService(
        ISpeechModel model,
        Func<IRecognitionModel, SpeechModelCatalog, ISpeechDiagnostics, IReadOnlyDictionary<string, object>?, CancellationToken, Task<ISpeechRecognizerEngine>> loader,
        RollingFileLogger? logger = null,
        params ISpeechModel[] others)
    {
        return new SpeechModelService(
            () => new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_tempRoot, "models") }).AddModels([model, .. others]),
            logger,
            loader);
    }
}

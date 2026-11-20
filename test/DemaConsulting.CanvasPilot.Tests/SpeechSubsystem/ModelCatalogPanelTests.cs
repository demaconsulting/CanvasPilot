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
using DemaConsulting.Speech.ModelManagementSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ModelCatalogPanelViewModel" /> and <see cref="ModelListItemViewModel" /> with
///     substitute model services. No test downloads anything.
/// </summary>
public sealed class ModelCatalogPanelTests
{
    /// <summary>
    ///     Test that loading splits the catalog by role, selects the stored models, and shows each model's state.
    /// </summary>
    [Fact]
    public void ModelCatalogPanelViewModel_Load_CatalogWithModels_SplitsByRoleAndSelectsStoredModels()
    {
        // Arrange: a catalog with two recognition models and one synthesis model, one of them installed
        var service = SpeechTestData.Models(
            SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition),
            SpeechTestData.Descriptor("stt-b", SpeechModelRole.Recognition, SpeechModelState.Downloaded),
            SpeechTestData.Descriptor("tts-a", SpeechModelRole.Synthesis));
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);

        // Act: load with stored choices
        panel.Load("stt-b", "tts-a");

        // Assert: the lists are split, the stored models are selected, and states read correctly
        Assert.Equal(["stt-a", "stt-b"], panel.SttModels.Select(row => row.Id));
        Assert.Equal(["tts-a"], panel.TtsModels.Select(row => row.Id));
        Assert.Equal("stt-b", panel.SelectedSttModel!.Id);
        Assert.Equal("tts-a", panel.SelectedTtsModel!.Id);
        Assert.Equal("stt-b", panel.SttModelId);
        Assert.Equal("Installed", panel.SttModels[1].StatusText);
        Assert.Equal("Not downloaded", panel.SttModels[0].StatusText);
        Assert.False(panel.HasMessage);
    }

    /// <summary>
    ///     Test that a stored model the catalog does not list leaves nothing selected but keeps the stored identifier,
    ///     so the choice is not erased.
    /// </summary>
    [Fact]
    public void ModelCatalogPanelViewModel_Load_StoredModelNotListed_KeepsStoredIdentifier()
    {
        // Arrange: a catalog without the stored models
        var panel = new ModelCatalogPanelViewModel(
            SpeechTestData.Models(SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition)),
            SpeechTestData.Inline);

        // Act: load with stored models that are not listed
        panel.Load("old-stt", "old-tts");

        // Assert: nothing is selected, and the identifiers are still reported for saving
        Assert.Null(panel.SelectedSttModel);
        Assert.Null(panel.SelectedTtsModel);
        Assert.Equal("old-stt", panel.SttModelId);
        Assert.Equal("old-tts", panel.TtsModelId);
    }

    /// <summary>
    ///     Test that a choice made by the user replaces the stored identifier.
    /// </summary>
    [Fact]
    public void ModelCatalogPanelViewModel_SelectModel_ChangesReportedIdentifier()
    {
        // Arrange: a loaded panel
        var panel = new ModelCatalogPanelViewModel(
            SpeechTestData.Models(
                SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition),
                SpeechTestData.Descriptor("stt-b", SpeechModelRole.Recognition)),
            SpeechTestData.Inline);
        panel.Load("stt-a", string.Empty);

        // Act: the user selects the other model
        panel.SelectedSttModel = panel.SttModels[1];

        // Assert: the identifier follows
        Assert.Equal("stt-b", panel.SttModelId);
    }

    /// <summary>
    ///     Test that an unreadable catalog is explained and the stored identifiers are kept.
    /// </summary>
    [Fact]
    public void ModelCatalogPanelViewModel_Load_CatalogUnavailable_ShowsProblemAndKeepsStoredIdentifiers()
    {
        // Arrange: a service that cannot read the catalog
        var service = Substitute.For<ISpeechModelService>();
        service.Enumerate().Returns(SpeechModelList.Unavailable("The speech models could not be listed. Disk error."));
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);

        // Act: load with stored models
        panel.Load("stt-a", "tts-a");

        // Assert: the problem is shown, the lists are empty, and the choices are kept
        Assert.Equal("The speech models could not be listed. Disk error.", panel.Message);
        Assert.True(panel.HasMessage);
        Assert.Empty(panel.SttModels);
        Assert.Equal("stt-a", panel.SttModelId);
        Assert.Equal("tts-a", panel.TtsModelId);
    }

    /// <summary>
    ///     Test that an empty catalog is explained.
    /// </summary>
    [Fact]
    public void ModelCatalogPanelViewModel_Load_EmptyCatalog_ShowsEmptyMessage()
    {
        // Arrange: a catalog without models
        var panel = new ModelCatalogPanelViewModel(SpeechTestData.Models(), SpeechTestData.Inline);

        // Act: load
        panel.Load(string.Empty, string.Empty);

        // Assert: the message says the catalog is empty
        Assert.Equal(ModelCatalogPanelViewModel.EmptyCatalogMessage, panel.Message);
    }

    /// <summary>
    ///     Test that a download shows its progress while it runs and the installed state when it ends.
    /// </summary>
    [Fact]
    public async Task ModelCatalogPanelViewModel_Download_Succeeds_ShowsProgressThenInstalled()
    {
        // Arrange: a service whose download reports progress and then waits to be completed
        var gate = new TaskCompletionSource<SpeechModelDownloadReport>();
        IProgress<SpeechModelDownloadProgress>? sink = null;
        var service = SpeechTestData.Models(SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition));
        service.DownloadAsync("stt-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                sink = call.ArgAt<IProgress<SpeechModelDownloadProgress>?>(1);
                return gate.Task;
            });
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);
        panel.Load(string.Empty, string.Empty);
        var row = panel.SttModels.Single();

        // Act: start the download, report half of the bytes, then finish it
        var download = panel.DownloadAsync(row);
        var downloadingState = (row.State, row.IsDownloading, row.CanDownload, panel.IsDownloading);
        sink!.Report(new SpeechModelDownloadProgress(1, 1, 50, 100));
        var halfway = row.ProgressFraction;
        gate.SetResult(new SpeechModelDownloadReport(SpeechModelDownloadStatus.Installed));
        await download;

        // Assert: the row showed a download in progress with half progress, and ended installed
        Assert.Equal((SpeechModelState.Downloading, true, false, true), downloadingState);
        Assert.Equal(0.5, halfway);
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.Equal(1.0, row.ProgressFraction);
        Assert.False(row.CanDownload);
        Assert.False(row.HasFailureMessage);
        Assert.False(panel.IsDownloading);
    }

    /// <summary>
    ///     Test that a failed download is explained in its row and can be tried again.
    /// </summary>
    [Fact]
    public async Task ModelCatalogPanelViewModel_Download_Fails_ShowsFailureAndAllowsRetry()
    {
        // Arrange: a service whose download fails with a message
        var service = SpeechTestData.Models(SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition));
        service.DownloadAsync("stt-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new SpeechModelDownloadReport(SpeechModelDownloadStatus.Failed, "The network blocked the download."));
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);
        panel.Load(string.Empty, string.Empty);
        var row = panel.SttModels.Single();

        // Act: download
        await panel.DownloadAsync(row);

        // Assert: the row failed with the message, no progress is left, and a retry is possible
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.Equal("The network blocked the download.", row.FailureMessage);
        Assert.True(row.HasFailureMessage);
        Assert.Null(row.ProgressFraction);
        Assert.True(row.CanDownload);
        Assert.Equal("Failed or corrupt", row.StatusText);
    }

    /// <summary>
    ///     Test that a service that throws anyway is shown as a failed download and does not escape.
    /// </summary>
    [Fact]
    public async Task ModelCatalogPanelViewModel_Download_ServiceThrows_ShowsFailure()
    {
        // Arrange: a service that throws instead of returning a report
        var service = SpeechTestData.Models(SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition));
        service.DownloadAsync("stt-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns<Task<SpeechModelDownloadReport>>(_ => throw new InvalidOperationException("boom"));
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);
        panel.Load(string.Empty, string.Empty);
        var row = panel.SttModels.Single();

        // Act: download
        await panel.DownloadAsync(row);

        // Assert: the row failed with the exception's message
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.Equal("boom", row.FailureMessage);
    }

    /// <summary>
    ///     Test that canceling one download cancels the token the service received and returns the row to
    ///     not downloaded.
    /// </summary>
    [Fact]
    public async Task ModelCatalogPanelViewModel_CancelDownload_RunningDownload_CancelsAndRestoresRow()
    {
        // Arrange: a service whose download waits for its token to be cancelled
        var service = SpeechTestData.Models(SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition));
        service.DownloadAsync("stt-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);
        panel.Load(string.Empty, string.Empty);
        var row = panel.SttModels.Single();
        var download = panel.DownloadAsync(row);

        // Act: cancel from the row's command
        panel.CancelDownloadCommand.Execute(row);
        await download;

        // Assert: the download ended as cancelled and the model can be downloaded again
        Assert.Equal(SpeechModelState.NotDownloaded, row.State);
        Assert.Equal("Download canceled.", row.FailureMessage);
        Assert.True(row.CanDownload);
        Assert.False(panel.IsDownloading);
    }

    /// <summary>
    ///     Test that canceling all downloads stops every running download.
    /// </summary>
    [Fact]
    public async Task ModelCatalogPanelViewModel_CancelAllDownloads_RunningDownloads_CancelsEach()
    {
        // Arrange: two downloads that wait for cancellation
        var service = SpeechTestData.Models(
            SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition),
            SpeechTestData.Descriptor("tts-a", SpeechModelRole.Synthesis));
        service.DownloadAsync(Arg.Any<string>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);
        panel.Load(string.Empty, string.Empty);
        var first = panel.DownloadAsync(panel.SttModels.Single());
        var second = panel.DownloadAsync(panel.TtsModels.Single());
        var running = panel.IsDownloading;

        // Act: cancel everything
        panel.CancelAllDownloads();
        await Task.WhenAll(first, second);

        // Assert: both rows are back to not downloaded and nothing is running
        Assert.True(running);
        Assert.Equal(SpeechModelState.NotDownloaded, panel.SttModels.Single().State);
        Assert.Equal(SpeechModelState.NotDownloaded, panel.TtsModels.Single().State);
        Assert.False(panel.IsDownloading);
    }

    /// <summary>
    ///     Test that a model that is installed, or a missing row, starts no download.
    /// </summary>
    [Fact]
    public async Task ModelCatalogPanelViewModel_Download_InstalledModelOrNoRow_DoesNothing()
    {
        // Arrange: an installed model
        var service = SpeechTestData.Models(SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition, SpeechModelState.Downloaded));
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);
        panel.Load(string.Empty, string.Empty);

        // Act: try to download it, and a missing row
        await panel.DownloadAsync(panel.SttModels.Single());
        await panel.DownloadAsync(null);
        panel.CancelDownload(null);

        // Assert: the service was never asked to download
        _ = service.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Test that a refresh while a download runs is refused, and that otherwise it re-reads the catalog and keeps
    ///     the choice.
    /// </summary>
    [Fact]
    public async Task ModelCatalogPanelViewModel_Refresh_WhileDownloading_IsRefusedOtherwiseRereads()
    {
        // Arrange: a loaded panel with a download that waits for cancellation
        var service = SpeechTestData.Models(SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition));
        service.DownloadAsync("stt-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));
        var panel = new ModelCatalogPanelViewModel(service, SpeechTestData.Inline);
        panel.Load("stt-a", string.Empty);
        var enumeratedAfterLoad = CountEnumerateCalls(service);
        var download = panel.DownloadAsync(panel.SttModels.Single());

        // Act: refresh while downloading, cancel, and refresh again
        panel.RefreshCommand.Execute(null);
        var busyMessage = panel.Message;
        var enumeratedWhileBusy = CountEnumerateCalls(service);
        panel.CancelAllDownloads();
        await download;
        panel.RefreshCommand.Execute(null);

        // Assert: the busy refresh only explained itself; the later one re-read and kept the selection and cleared the message
        Assert.Equal(ModelCatalogPanelViewModel.RefreshBusyMessage, busyMessage);
        Assert.Equal(enumeratedAfterLoad, enumeratedWhileBusy);
        Assert.Equal(enumeratedAfterLoad + 1, CountEnumerateCalls(service));
        Assert.Equal("stt-a", panel.SelectedSttModel!.Id);
        Assert.False(panel.HasMessage);
    }

    /// <summary>
    ///     Test that a missing service is refused.
    /// </summary>
    [Fact]
    public void ModelCatalogPanelViewModel_New_NullService_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ModelCatalogPanelViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new ModelListItemViewModel(null!));
    }

    /// <summary>
    ///     Counts the calls that listed the models.
    /// </summary>
    /// <param name="service">The substitute service.</param>
    /// <returns>The number of calls.</returns>
    private static int CountEnumerateCalls(ISpeechModelService service)
    {
        return service.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ISpeechModelService.Enumerate));
    }

    /// <summary>
    ///     A download that ends as cancelled when its token is cancelled, like the real service.
    /// </summary>
    /// <param name="token">The token to wait for.</param>
    /// <returns>The cancelled report.</returns>
    private static async Task<SpeechModelDownloadReport> WaitForCancellationAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the expected way for this download to end.
        }

        return new SpeechModelDownloadReport(SpeechModelDownloadStatus.Canceled, "Download canceled.");
    }
}

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

using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Speech subsystem: the real <see cref="AudioDeviceService" /> and
///     <see cref="SpeechModelService" /> (over substitute audio operations and a library catalog in a temporary
///     store with substitute models), the Speech settings page, the settings dialog, and the settings file working
///     together. Nothing is downloaded and no audio hardware is needed.
/// </summary>
public sealed class SpeechSubsystemTests : IDisposable
{
    /// <summary>
    ///     Temporary folder for the settings file and the model store, created fresh for each test and removed on
    ///     disposal.
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
    ///     Test that devices, models, and model parameters chosen in the dialog are stored by OK and shown again
    ///     by a dialog of the next run.
    /// </summary>
    [Fact]
    public void SpeechSubsystem_ConfigureSpeech_DialogOk_StoresDevicesModelsAndParameters()
    {
        // Arrange: the real services over a machine with devices and a catalog, and a dialog over the Speech page
        var settingsFolder = Path.Combine(_tempRoot, "settings");
        var service = new SettingsService(settingsFolder);
        using var models = CreateModelService();
        var page = new SpeechSettingsPageViewModel(CreateAudioService(["Mic A", "Mic B"], ["Out 1"]), models, SpeechTestData.Inline);
        var dialog = new SettingsDialogViewModel(service, [page]);

        // Act: the user chooses devices and models, edits a parameter, and chooses OK
        page.Devices.SelectedCaptureDevice = page.Devices.CaptureDevices.Single(device => device.Name == "Mic B");
        page.Devices.SelectedPlaybackDevice = page.Devices.PlaybackDevices.Single(device => device.Name == "Out 1");
        page.Models.SelectedSttModel = page.Models.SttModels.Single();
        page.Models.SelectedTtsModel = page.Models.TtsModels.Single();
        ((ChoiceParameterViewModel)page.TtsParameters.Parameters[0]).SelectedOption = ((ChoiceParameterViewModel)page.TtsParameters.Parameters[0]).Options[2];
        dialog.OkCommand.Execute(null);
        var restored = new SettingsService(settingsFolder).Load().Speech;

        // Assert: the next run has the choices, and a new page shows them
        Assert.Equal("Mic B", restored.CaptureDevice);
        Assert.Equal("Out 1", restored.PlaybackDevice);
        Assert.Equal("stt-one", restored.SttModelId);
        Assert.Equal("tts-one", restored.TtsModelId);
        Assert.Equal("c", restored.ModelParameters["tts-one"]["voice"]);
        var nextPage = new SpeechSettingsPageViewModel(CreateAudioService(["Mic A", "Mic B"], ["Out 1"]), models, SpeechTestData.Inline);
        nextPage.Load(new SettingsService(settingsFolder).Load());
        Assert.Equal("Mic B", nextPage.Devices.CaptureDeviceName);
        Assert.Equal("c", nextPage.TtsParameters.Parameters[0].ValueText);
    }

    /// <summary>
    ///     Test that a machine with no audio system and an unusable model store does not stop the dialog, explains
    ///     what is missing, and keeps the stored choices when the user chooses OK.
    /// </summary>
    [Fact]
    public void SpeechSubsystem_ToleratePoorMachines_NoAudioNoModelStore_DialogStillWorks()
    {
        // Arrange: stored speech choices, an audio backend that fails, and a model store that cannot be created
        var settingsFolder = Path.Combine(_tempRoot, "settings");
        var service = new SettingsService(settingsFolder);
        service.Save(new AppSettings
        {
            Speech = new SpeechSettings { CaptureDevice = "USB mic", SttModelId = "stt-one" },
        });
        var audio = new AudioDeviceService(
            () => throw new DllNotFoundException("audio library is missing"),
            () => throw new DllNotFoundException("audio library is missing"),
            () => throw new DllNotFoundException("audio library is missing"));
        using var models = new SpeechModelService(() => throw new IOException("No model store."));
        var page = new SpeechSettingsPageViewModel(audio, models, SpeechTestData.Inline);
        var dialog = new SettingsDialogViewModel(service, [page]);
        var captureStatus = page.Devices.CaptureStatus;
        var modelsMessage = page.Models.Message;

        // Act: choose OK
        dialog.OkCommand.Execute(null);
        var restored = new SettingsService(settingsFolder).Load().Speech;

        // Assert: both problems were explained, nothing threw, and the stored choices survived
        Assert.Contains("audio library is missing", captureStatus, StringComparison.Ordinal);
        Assert.Contains("No model store.", modelsMessage, StringComparison.Ordinal);
        Assert.Equal(string.Empty, dialog.ErrorMessage);
        Assert.Equal("USB mic", restored.CaptureDevice);
        Assert.Equal("stt-one", restored.SttModelId);
    }

    /// <summary>
    ///     Test that closing the dialog, however it closes, cancels a model download that is still running.
    /// </summary>
    [Fact]
    public async Task SpeechSubsystem_CancelDownloadsOnClose_DialogCancelled_StopsRunningDownload()
    {
        // Arrange: a dialog over the Speech page with a download that runs until it is cancelled
        var models = SpeechTestData.Models(SpeechTestData.Descriptor("stt-one", SpeechModelRole.Recognition));
        CancellationToken received = default;
        models.DownloadAsync("stt-one", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                received = call.ArgAt<CancellationToken>(2);
                try
                {
                    await Task.Delay(Timeout.Infinite, received);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is the expected way for this download to end.
                }

                return new SpeechModelDownloadReport(SpeechModelDownloadStatus.Canceled, "Download canceled.");
            });
        var page = new SpeechSettingsPageViewModel(SpeechTestData.AudioDevices([], []), models, SpeechTestData.Inline);
        var dialog = new SettingsDialogViewModel(new SettingsService(Path.Combine(_tempRoot, "settings")), [page]);
        var download = page.Models.DownloadAsync(page.Models.SttModels.Single());
        var runningBeforeClose = page.Models.IsDownloading;

        // Act: the user cancels the dialog while the download runs
        dialog.CancelCommand.Execute(null);
        await download;

        // Assert: the download was running, was cancelled, and the model is left not downloaded
        Assert.True(runningBeforeClose);
        Assert.True(received.IsCancellationRequested);
        Assert.Equal(SpeechModelState.NotDownloaded, page.Models.SttModels.Single().State);
    }

    /// <summary>
    ///     Creates a speech model service over a library catalog in the temporary store with one recognition model
    ///     and one synthesis model that declares a choice.
    /// </summary>
    /// <returns>The service.</returns>
    private SpeechModelService CreateModelService()
    {
        return new SpeechModelService(
            () => new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_tempRoot, "models") }).AddModels(
                SpeechTestData.Model("stt-one", SpeechModelRole.Recognition),
                SpeechTestData.Model("tts-one", SpeechModelRole.Synthesis, SpeechTestData.Choice())));
    }

    /// <summary>
    ///     Creates the real audio device service over a backend that lists the given devices.
    /// </summary>
    /// <param name="capture">The capture device names.</param>
    /// <param name="playback">The playback device names.</param>
    /// <returns>The service.</returns>
    private static AudioDeviceService CreateAudioService(string[] capture, string[] playback)
    {
        return new AudioDeviceService(
            () => capture.Select(SpeechTestData.Capture).ToArray(),
            () => playback.Select(SpeechTestData.Playback).ToArray(),
            () => { });
    }
}

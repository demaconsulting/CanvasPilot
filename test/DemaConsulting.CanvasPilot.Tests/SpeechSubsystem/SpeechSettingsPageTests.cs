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
///     Unit tests for <see cref="SpeechSettingsPageViewModel" /> with substitute audio device and speech model
///     services. No test downloads anything.
/// </summary>
public sealed class SpeechSettingsPageTests
{
    /// <summary>
    ///     Test that the page is titled "Speech", is always valid, and does not use the audio or model services until
    ///     it is loaded.
    /// </summary>
    [Fact]
    public void SpeechSettingsPageViewModel_New_IsTitledValidAndDoesNotUseServices()
    {
        // Arrange: substitute services
        var audio = Substitute.For<IAudioDeviceService>();
        var models = Substitute.For<ISpeechModelService>();

        // Act: create the page
        var page = new SpeechSettingsPageViewModel(audio, models, SpeechTestData.Inline);

        // Assert: it has its title, accepts any entry, and left the services alone
        Assert.Equal("Speech", page.Title);
        Assert.True(page.IsValid);
        audio.DidNotReceive().Enumerate();
        models.DidNotReceive().Enumerate();
    }

    /// <summary>
    ///     Test that stored settings are shown and applied again unchanged, with devices stored by name.
    /// </summary>
    [Fact]
    public void SpeechSettingsPageViewModel_LoadApply_StoredSettings_RoundTrips()
    {
        // Arrange: a page over a machine with the stored devices and models
        var page = CreatePage();
        var stored = new AppSettings
        {
            Speech = new SpeechSettings
            {
                CaptureDevice = "Mic B",
                PlaybackDevice = "Out 1",
                SttModelId = "stt-a",
                TtsModelId = "tts-a",
                ModelParameters = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["tts-a"] = new Dictionary<string, string> { ["speed"] = "7", ["voice"] = "a", ["echo"] = "true" },
                },
            },
        };

        // Act: load and apply
        page.Load(stored);
        var applied = page.Apply(new AppSettings()).Speech;

        // Assert: the page showed the stored choices and applying them stores the same values
        Assert.Equal("Mic B", page.Devices.CaptureDeviceName);
        Assert.Equal("stt-a", page.Models.SelectedSttModel!.Id);
        Assert.Equal("7", page.TtsParameters.Parameters[0].ValueText);
        Assert.Equal("a", page.TtsParameters.Parameters[1].ValueText);
        Assert.Equal("Mic B", applied.CaptureDevice);
        Assert.Equal("Out 1", applied.PlaybackDevice);
        Assert.Equal("stt-a", applied.SttModelId);
        Assert.Equal("tts-a", applied.TtsModelId);
        Assert.Equal(stored.Speech.ModelParameters["tts-a"], applied.ModelParameters["tts-a"]);
    }

    /// <summary>
    ///     Test that the choices the user makes are what is applied: devices by name, models by identifier, and the
    ///     parameter values of the chosen models.
    /// </summary>
    [Fact]
    public void SpeechSettingsPageViewModel_Apply_UserChoices_AreStored()
    {
        // Arrange: a loaded page with default settings
        var page = CreatePage();
        page.Load(new AppSettings());

        // Act: the user picks devices and models and edits a parameter
        page.Devices.SelectedCaptureDevice = page.Devices.CaptureDevices.Single(device => device.Name == "Mic A");
        page.Devices.SelectedPlaybackDevice = page.Devices.PlaybackDevices.Single(device => device.Name == "Out 2");
        page.Models.SelectedSttModel = page.Models.SttModels.Single(row => row.Id == "stt-a");
        page.Models.SelectedTtsModel = page.Models.TtsModels.Single(row => row.Id == "tts-a");
        ((NumericParameterViewModel)page.TtsParameters.Parameters[0]).Value = 9;
        var applied = page.Apply(new AppSettings()).Speech;

        // Assert: the choices are stored, and the parameters of the chosen model with them
        Assert.Equal("Mic A", applied.CaptureDevice);
        Assert.Equal("Out 2", applied.PlaybackDevice);
        Assert.Equal("stt-a", applied.SttModelId);
        Assert.Equal("tts-a", applied.TtsModelId);
        Assert.Equal("9", applied.ModelParameters["tts-a"]["speed"]);
    }

    /// <summary>
    ///     Test that applying changes only the speech section and keeps everything else.
    /// </summary>
    [Fact]
    public void SpeechSettingsPageViewModel_Apply_OtherSettings_AreUnchanged()
    {
        // Arrange: a loaded page and settings with other values
        var page = CreatePage();
        page.Load(new AppSettings());
        var other = new AppSettings
        {
            RecentProjects = ["C:\\a.canvaspilot"],
            AiProvider = new AiProviderSettings { ModelName = "keep-me" },
        };

        // Act: apply
        var applied = page.Apply(other);

        // Assert: the recent projects and AI provider choices are untouched
        Assert.Equal(["C:\\a.canvaspilot"], applied.RecentProjects);
        Assert.Equal("keep-me", applied.AiProvider.ModelName);
    }

    /// <summary>
    ///     Test that changing the chosen model shows the new model's parameters, and that values edited for the
    ///     first model are remembered when the user switches back.
    /// </summary>
    [Fact]
    public void SpeechSettingsPageViewModel_SelectModel_ShowsItsParametersAndRemembersEdits()
    {
        // Arrange: a loaded page whose two synthesis models have different parameters
        var page = CreatePage();
        page.Load(new AppSettings { Speech = new SpeechSettings { TtsModelId = "tts-a" } });
        ((NumericParameterViewModel)page.TtsParameters.Parameters[0]).Value = 3;

        // Act: switch to the other model and back
        page.Models.SelectedTtsModel = page.Models.TtsModels.Single(row => row.Id == "tts-b");
        var otherParameters = page.TtsParameters.Parameters.Select(parameter => parameter.Id).ToArray();
        page.Models.SelectedTtsModel = page.Models.TtsModels.Single(row => row.Id == "tts-a");

        // Assert: each model showed its own parameters and the edit was remembered
        Assert.Equal(["echo"], otherParameters);
        Assert.Equal("3", page.TtsParameters.Parameters[0].ValueText);
        Assert.Equal("3", page.Apply(new AppSettings()).Speech.ModelParameters["tts-a"]["speed"]);
    }

    /// <summary>
    ///     Test that loading again shows the stored values, not the values edited in an earlier dialog session.
    /// </summary>
    [Fact]
    public void SpeechSettingsPageViewModel_Load_AfterEdits_ShowsStoredValuesAgain()
    {
        // Arrange: a loaded page whose parameter was edited and then discarded by closing the dialog
        var page = CreatePage();
        var stored = new AppSettings
        {
            Speech = new SpeechSettings
            {
                TtsModelId = "tts-a",
                ModelParameters = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["tts-a"] = new Dictionary<string, string> { ["speed"] = "2" },
                },
            },
        };
        page.Load(stored);
        ((NumericParameterViewModel)page.TtsParameters.Parameters[0]).Value = 8;
        page.OnClosed();

        // Act: the dialog opens again
        page.Load(stored);

        // Assert: the stored value is shown
        Assert.Equal("2", page.TtsParameters.Parameters[0].ValueText);
    }

    /// <summary>
    ///     Test that models and devices that cannot be shown are kept in the settings rather than erased.
    /// </summary>
    [Fact]
    public void SpeechSettingsPageViewModel_Apply_StoredChoicesNotShown_AreKept()
    {
        // Arrange: a page over a machine that has no audio system and a catalog that cannot be read
        var audio = Substitute.For<IAudioDeviceService>();
        audio.Enumerate().Returns(AudioDeviceSnapshot.Unavailable("No audio."));
        var models = Substitute.For<ISpeechModelService>();
        models.Enumerate().Returns(SpeechModelList.Unavailable("No catalog."));
        var page = new SpeechSettingsPageViewModel(audio, models, SpeechTestData.Inline);
        var stored = new AppSettings
        {
            Speech = new SpeechSettings { CaptureDevice = "USB mic", PlaybackDevice = "USB out", SttModelId = "stt-x", TtsModelId = "tts-x" },
        };

        // Act: load and apply
        page.Load(stored);
        var applied = page.Apply(new AppSettings()).Speech;

        // Assert: nothing the page could not show was lost
        Assert.Equal("USB mic", applied.CaptureDevice);
        Assert.Equal("USB out", applied.PlaybackDevice);
        Assert.Equal("stt-x", applied.SttModelId);
        Assert.Equal("tts-x", applied.TtsModelId);
    }

    /// <summary>
    ///     Test that closing the dialog cancels a download that is still running.
    /// </summary>
    [Fact]
    public async Task SpeechSettingsPageViewModel_OnClosed_RunningDownload_IsCancelled()
    {
        // Arrange: a page with a download that waits for its token to be cancelled
        var models = SpeechTestData.Models(SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition));
        CancellationToken received = default;
        models.DownloadAsync("stt-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                received = call.ArgAt<CancellationToken>(2);
                return WaitForCancellationAsync(received);
            });
        var page = new SpeechSettingsPageViewModel(SpeechTestData.AudioDevices([], []), models, SpeechTestData.Inline);
        page.Load(new AppSettings());
        var download = page.Models.DownloadAsync(page.Models.SttModels.Single());
        var runningBeforeClose = page.Models.IsDownloading;

        // Act: close the dialog
        page.OnClosed();
        await download;

        // Assert: the download had been running, its token was cancelled, and nothing is running now
        Assert.True(runningBeforeClose);
        Assert.True(received.IsCancellationRequested);
        Assert.False(page.Models.IsDownloading);
        Assert.Equal(SpeechModelState.NotDownloaded, page.Models.SttModels.Single().State);
    }

    /// <summary>
    ///     Test that missing arguments are refused.
    /// </summary>
    [Fact]
    public void SpeechSettingsPageViewModel_InvalidArguments_Throw()
    {
        // Arrange: a page
        var page = CreatePage();

        // Act & Assert: missing services and settings are refused
        Assert.Throws<ArgumentNullException>(() => new SpeechSettingsPageViewModel(null!, SpeechTestData.Models()));
        Assert.Throws<ArgumentNullException>(() => new SpeechSettingsPageViewModel(SpeechTestData.AudioDevices([], []), null!));
        Assert.Throws<ArgumentNullException>(() => page.Load(null!));
        Assert.Throws<ArgumentNullException>(() => page.Apply(null!));
    }

    /// <summary>
    ///     Creates a page over a machine with two microphones, two speakers, two recognition models, and two
    ///     synthesis models, one of which has a number, a choice, and a switch.
    /// </summary>
    /// <returns>The page, not yet loaded.</returns>
    private static SpeechSettingsPageViewModel CreatePage()
    {
        var models = SpeechTestData.Models(
            SpeechTestData.Descriptor("stt-a", SpeechModelRole.Recognition),
            SpeechTestData.Descriptor("stt-b", SpeechModelRole.Recognition),
            SpeechTestData.Descriptor(
                "tts-a",
                SpeechModelRole.Synthesis,
                SpeechModelState.Downloaded,
                SpeechTestData.Numeric(),
                SpeechTestData.Choice(),
                SpeechTestData.Boolean()),
            SpeechTestData.Descriptor("tts-b", SpeechModelRole.Synthesis, SpeechModelState.NotDownloaded, SpeechTestData.Boolean()));
        return new SpeechSettingsPageViewModel(
            SpeechTestData.AudioDevices(["Mic A", "Mic B"], ["Out 1", "Out 2"]),
            models,
            SpeechTestData.Inline);
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

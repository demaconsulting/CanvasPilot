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

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the Speech settings page view, its device pickers, model catalog panel, and parameter
///     editors, and for the page inside the settings dialog. The audio and model services are fakes, so no hardware
///     is needed and nothing is downloaded.
/// </summary>
public sealed class SpeechSettingsPageViewTests : IDisposable
{
    /// <summary>
    ///     Temporary settings folder created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <summary>
    ///     The audio device service the pages use.
    /// </summary>
    private readonly FakeAudioDeviceService _audio = new();

    /// <summary>
    ///     The speech model service the pages use.
    /// </summary>
    private readonly FakeSpeechModelService _models = new();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the page shows the stored devices, the models by role with their state, the parameters of the
    ///     chosen model, and a status text for each device list.
    /// </summary>
    [AvaloniaFact]
    public void SpeechSettingsPageView_LoadedSettings_ShowDevicesModelsAndParameters()
    {
        // Arrange: a machine with one microphone and no speakers, and a catalog with one model of each role
        _audio.Snapshot = new AudioDeviceSnapshot([new AudioDeviceDescription("Desk mic", AudioDeviceDirection.Capture, 1, 16000)], []);
        _models.AddModel("stt-one", SpeechModelRole.Recognition, SpeechModelState.Downloaded);
        _models.AddModel("tts-one", SpeechModelRole.Synthesis, SpeechModelState.NotDownloaded, CreateVoiceParameter());
        var page = new SpeechSettingsPageViewModel(_audio, _models);

        // Act: show the page over stored settings
        var window = ShowPage(page, new AppSettings
        {
            Speech = new SpeechSettings { CaptureDevice = "Desk mic", SttModelId = "stt-one", TtsModelId = "tts-one" },
        });

        // Assert: the controls show the stored choices and the states
        var capture = SettingsUiHelpers.Find<ComboBox>(window, "SpeechCaptureDeviceComboBox");
        Assert.Equal("Desk mic", ((DeviceChoice)capture.SelectedItem!).Name);
        Assert.Equal("1 capture device(s) found.", SettingsUiHelpers.Find<TextBlock>(window, "SpeechCaptureStatus").Text);
        Assert.StartsWith("No playback device was found.", SettingsUiHelpers.Find<TextBlock>(window, "SpeechPlaybackStatus").Text, StringComparison.Ordinal);
        Assert.Equal(1, SettingsUiHelpers.Find<ListBox>(window, "SpeechSttModelList").ItemCount);
        Assert.Equal(1, SettingsUiHelpers.Find<ListBox>(window, "SpeechTtsModelList").ItemCount);
        Assert.Equal("stt-one", ((ModelListItemViewModel)SettingsUiHelpers.Find<ListBox>(window, "SpeechSttModelList").SelectedItem!).Id);
        Assert.Equal(["Installed", "Not downloaded"], StatusTexts(window));
        var voice = SettingsUiHelpers.Find<ComboBox>(window, "SpeechParameterComboBox");
        Assert.Equal("b", ((ChoiceParameterOption)voice.SelectedItem!).Value);

        window.Close();
    }

    /// <summary>
    ///     Test that a machine without devices or models is explained in the page rather than shown as blank lists.
    /// </summary>
    [AvaloniaFact]
    public void SpeechSettingsPageView_NothingAvailable_ShowsExplanations()
    {
        // Arrange: an unusable audio system and a catalog that cannot be read
        _audio.Snapshot = AudioDeviceSnapshot.Unavailable("The audio system could not be used.");
        _models.Problem = "The speech models could not be listed.";
        var page = new SpeechSettingsPageViewModel(_audio, _models);

        // Act: show the page
        var window = ShowPage(page, new AppSettings());

        // Assert: every problem is shown as text, and the parameter editors say that no model is chosen
        Assert.Equal("The audio system could not be used.", SettingsUiHelpers.Find<TextBlock>(window, "SpeechCaptureStatus").Text);
        var message = SettingsUiHelpers.Find<TextBlock>(window, "SpeechModelsMessage");
        Assert.True(message.IsVisible);
        Assert.Equal("The speech models could not be listed.", message.Text);
        Assert.Equal(ModelParameterEditorViewModel.NoModelMessage, SettingsUiHelpers.Find<TextBlock>(window, "SpeechParametersEmptyMessage").Text);

        window.Close();
    }

    /// <summary>
    ///     Test that the Refresh devices button asks the audio service to scan and shows the new device.
    /// </summary>
    [AvaloniaFact]
    public void SpeechSettingsPageView_RefreshDevicesButton_ScansAndShowsNewDevice()
    {
        // Arrange: a shown page over a machine without devices
        var page = new SpeechSettingsPageViewModel(_audio, _models);
        var window = ShowPage(page, new AppSettings());
        _audio.Snapshot = new AudioDeviceSnapshot([], [new AudioDeviceDescription("USB speakers", AudioDeviceDirection.Playback, 2, 48000)]);

        // Act: the user presses Refresh devices
        var button = SettingsUiHelpers.Find<Button>(window, "SpeechRefreshDevicesButton");
        button.Command!.Execute(button.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        // Assert: the service was asked to scan and the new device is offered
        Assert.Equal(1, _audio.Refreshes);
        Assert.Contains(SettingsUiHelpers.Find<ComboBox>(window, "SpeechPlaybackDeviceComboBox").Items.OfType<DeviceChoice>(), choice => choice.Name == "USB speakers");

        window.Close();
    }

    /// <summary>
    ///     Test that the Download button starts a download that shows its progress and, when it ends, the installed
    ///     state.
    /// </summary>
    [AvaloniaFact]
    public async Task SpeechSettingsPageView_DownloadButton_ShowsProgressThenInstalled()
    {
        // Arrange: a shown page with one model that is not downloaded
        _models.AddModel("stt-one", SpeechModelRole.Recognition);
        var page = new SpeechSettingsPageViewModel(_audio, _models);
        var window = ShowPage(page, new AppSettings());
        var download = SettingsUiHelpers.Find<Button>(window, "SpeechModelDownloadButton");

        // Act: press Download, report half of the bytes, and then finish the download
        var running = ((IAsyncRelayCommand)download.Command!).ExecuteAsync(download.CommandParameter);
        Dispatcher.UIThread.RunJobs();
        var progressBar = SettingsUiHelpers.Find<ProgressBar>(window, "SpeechModelProgress");
        var during = (progressBar.IsVisible, download.IsEnabled, StatusTexts(window)[0]);
        _models.LastProgress!.Report(new SpeechModelDownloadProgress(1, 1, 50, 100));
        Dispatcher.UIThread.RunJobs();
        var halfway = progressBar.Value;
        _models.Complete(new SpeechModelDownloadReport(SpeechModelDownloadStatus.Installed));
        await running;
        Dispatcher.UIThread.RunJobs();

        // Assert: the bar showed a running, half-done download, and the row ends installed
        Assert.Equal((true, false, "Downloading"), during);
        Assert.Equal(0.5, halfway);
        Assert.False(progressBar.IsVisible);
        Assert.Equal("Installed", StatusTexts(window)[0]);
        Assert.False(download.IsEnabled);

        window.Close();
    }

    /// <summary>
    ///     Test that the Cancel button stops a running download, leaves the model not downloaded, and says so.
    /// </summary>
    [AvaloniaFact]
    public async Task SpeechSettingsPageView_CancelButton_StopsDownloadAndExplains()
    {
        // Arrange: a shown page with a download in progress
        _models.AddModel("stt-one", SpeechModelRole.Recognition);
        var page = new SpeechSettingsPageViewModel(_audio, _models);
        var window = ShowPage(page, new AppSettings());
        var download = SettingsUiHelpers.Find<Button>(window, "SpeechModelDownloadButton");
        var running = ((IAsyncRelayCommand)download.Command!).ExecuteAsync(download.CommandParameter);
        Dispatcher.UIThread.RunJobs();
        var cancel = SettingsUiHelpers.Find<Button>(window, "SpeechModelCancelButton");
        var cancelShown = cancel.IsVisible;

        // Act: press Cancel
        cancel.Command!.Execute(cancel.CommandParameter);
        await running;
        Dispatcher.UIThread.RunJobs();

        // Assert: the token was cancelled, the row can download again, and it says the download was canceled
        Assert.True(cancelShown);
        Assert.True(_models.LastToken.IsCancellationRequested);
        Assert.True(download.IsEnabled);
        Assert.Equal("Not downloaded", StatusTexts(window)[0]);
        var failure = SettingsUiHelpers.Find<TextBlock>(window, "SpeechModelFailure");
        Assert.True(failure.IsVisible);
        Assert.Equal("Download canceled.", failure.Text);

        window.Close();
    }

    /// <summary>
    ///     Test that a failed download shows its explanation and leaves the Download button available for a retry.
    /// </summary>
    [AvaloniaFact]
    public async Task SpeechSettingsPageView_DownloadFails_ShowsFailureAndAllowsRetry()
    {
        // Arrange: a shown page with a download in progress
        _models.AddModel("stt-one", SpeechModelRole.Recognition);
        var page = new SpeechSettingsPageViewModel(_audio, _models);
        var window = ShowPage(page, new AppSettings());
        var download = SettingsUiHelpers.Find<Button>(window, "SpeechModelDownloadButton");
        var running = ((IAsyncRelayCommand)download.Command!).ExecuteAsync(download.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        // Act: the download fails
        _models.Complete(new SpeechModelDownloadReport(SpeechModelDownloadStatus.Failed, "The network blocked the download."));
        await running;
        Dispatcher.UIThread.RunJobs();

        // Assert: the explanation is shown and a retry is possible
        Assert.Equal("The network blocked the download.", SettingsUiHelpers.Find<TextBlock>(window, "SpeechModelFailure").Text);
        Assert.True(download.IsEnabled);
        Assert.Equal("Failed or corrupt", StatusTexts(window)[0]);

        window.Close();
    }

    /// <summary>
    ///     Test that the settings dialog lists the Speech page after the AI Provider page and shows its content when
    ///     it is chosen.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_SpeechPage_IsListedAndShownWhenChosen()
    {
        // Arrange: a dialog over both standard pages
        var (window, _) = CreateDialog();
        window.Show();
        var list = SettingsUiHelpers.Find<ListBox>(window, "SettingsPageList");
        var titles = list.Items.OfType<ISettingsPage>().Select(page => page.Title).ToArray();

        // Act: choose the Speech page
        list.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        // Assert: both pages are listed and the Speech page replaced the AI page
        Assert.Equal(["AI Provider", "Speech"], titles);
        Assert.NotNull(SettingsUiHelpers.Find<ListBox>(window, "SpeechSttModelList"));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<ComboBox>(), combo => AutomationProperties.GetAutomationId(combo) == "AiProviderComboBox");

        window.Close();
    }

    /// <summary>
    ///     Test that closing the settings dialog window cancels a download that is still running.
    /// </summary>
    [AvaloniaFact]
    public async Task SettingsDialogView_CloseWindow_CancelsRunningDownload()
    {
        // Arrange: a shown dialog on the Speech page with a download in progress
        _models.AddModel("stt-one", SpeechModelRole.Recognition);
        var (window, page) = CreateDialog();
        window.Show();
        SettingsUiHelpers.Find<ListBox>(window, "SettingsPageList").SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        var download = SettingsUiHelpers.Find<Button>(window, "SpeechModelDownloadButton");
        var running = ((IAsyncRelayCommand)download.Command!).ExecuteAsync(download.CommandParameter);
        Dispatcher.UIThread.RunJobs();
        var tokenBeforeClose = _models.LastToken.IsCancellationRequested;

        // Act: close the window without choosing OK or Cancel
        window.Close();
        await running;

        // Assert: the download was running, and closing cancelled it
        Assert.False(tokenBeforeClose);
        Assert.True(_models.LastToken.IsCancellationRequested);
        Assert.False(page.Models.IsDownloading);
    }

    /// <summary>
    ///     Test that choices made on the Speech page and saved with OK are written to the settings file.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_OkButton_SavesSpeechChoices()
    {
        // Arrange: a shown dialog on the Speech page over a machine with two microphones and one model
        _audio.Snapshot = new AudioDeviceSnapshot(
            [new AudioDeviceDescription("Mic A", AudioDeviceDirection.Capture, 1, 16000), new AudioDeviceDescription("Mic B", AudioDeviceDirection.Capture, 1, 16000)],
            []);
        _models.AddModel("stt-one", SpeechModelRole.Recognition, SpeechModelState.Downloaded);
        var (window, _) = CreateDialog();
        window.Show();
        SettingsUiHelpers.Find<ListBox>(window, "SettingsPageList").SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        // Act: choose the second microphone and the model, and click OK
        SettingsUiHelpers.Find<ComboBox>(window, "SpeechCaptureDeviceComboBox").SelectedIndex = 2;
        SettingsUiHelpers.Find<ListBox>(window, "SpeechSttModelList").SelectedIndex = 0;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "SettingsOkButton"));

        // Assert: the choices are in the settings file
        var stored = new SettingsService(_tempRoot).Load().Speech;
        Assert.Equal("Mic B", stored.CaptureDevice);
        Assert.Equal("stt-one", stored.SttModelId);
    }

    /// <summary>
    ///     Creates a voice choice parameter for a synthesis model.
    /// </summary>
    /// <returns>The parameter.</returns>
    private static ChoiceParameter CreateVoiceParameter()
    {
        return new ChoiceParameter(
            "voice",
            "Voice",
            "Which voice speaks.",
            [new ChoiceParameterOption("a", "Anna"), new ChoiceParameterOption("b", "Ben")],
            "b");
    }

    /// <summary>
    ///     Reads the state texts of all model rows that are shown, in the order of the lists.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The texts.</returns>
    private static string[] StatusTexts(Window window)
    {
        return window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => AutomationProperties.GetAutomationId(text) == "SpeechModelStatus")
            .Select(text => text.Text ?? string.Empty)
            .ToArray();
    }

    /// <summary>
    ///     Loads a page with settings and shows it in a window that is large enough to show every control.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="settings">The settings to load.</param>
    /// <returns>The shown window.</returns>
    private static Window ShowPage(SpeechSettingsPageViewModel page, AppSettings settings)
    {
        page.Load(settings);
        var window = new Window
        {
            Width = 700,
            Height = 1600,
            Content = new SpeechSettingsPageView { DataContext = page },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    ///     Creates a settings dialog window over the AI Provider page and the Speech page, as the application
    ///     composes them.
    /// </summary>
    /// <returns>The window and the Speech page.</returns>
    private (SettingsDialogView Window, SpeechSettingsPageViewModel Page) CreateDialog()
    {
        var speech = new SpeechSettingsPageViewModel(_audio, _models);
        var viewModel = new SettingsDialogViewModel(new SettingsService(_tempRoot), [new AiProviderSettingsPageViewModel(), speech]);
        return (new SettingsDialogView(viewModel), speech);
    }
}

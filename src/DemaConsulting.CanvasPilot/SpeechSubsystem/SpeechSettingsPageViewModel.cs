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

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     The "Speech" settings page. It composes the device pickers, the model catalog panel, and the parameter
///     editors of the chosen models, and maps them to and from <see cref="SpeechSettings" />. Devices are stored by
///     name. Model downloads run at once and are not part of OK or Cancel; closing the dialog cancels the downloads
///     that are still running. Nothing in the application consumes the choices yet.
/// </summary>
public sealed class SpeechSettingsPageViewModel : ObservableObject, ISettingsPage
{
    /// <summary>
    ///     The parameter values being edited: model identifier, then parameter identifier, then the value.
    /// </summary>
    private Dictionary<string, Dictionary<string, string>> _parameterValues = new(StringComparer.Ordinal);

    /// <summary>
    ///     Creates the page.
    /// </summary>
    /// <param name="audioDevices">Lists the audio devices.</param>
    /// <param name="speechModels">Lists and downloads the speech models.</param>
    /// <param name="createProgress">
    ///     Creates the sink that receives a download's progress on the user interface thread, or
    ///     <see langword="null" /> for the default.
    /// </param>
    public SpeechSettingsPageViewModel(
        IAudioDeviceService audioDevices,
        ISpeechModelService speechModels,
        Func<Action<SpeechModelDownloadProgress>, IProgress<SpeechModelDownloadProgress>>? createProgress = null)
    {
        ArgumentNullException.ThrowIfNull(audioDevices);
        ArgumentNullException.ThrowIfNull(speechModels);

        Devices = new DeviceSelectionViewModel(audioDevices);
        Models = new ModelCatalogPanelViewModel(speechModels, createProgress);
        SttParameters = new ModelParameterEditorViewModel("Speech-to-text model settings");
        TtsParameters = new ModelParameterEditorViewModel("Text-to-speech model settings");
        Models.PropertyChanged += OnModelsPropertyChanged;
    }

    /// <inheritdoc />
    public string Title => "Speech";

    /// <inheritdoc />
    public bool IsValid => true;

    /// <summary>
    ///     Gets the audio device pickers.
    /// </summary>
    public DeviceSelectionViewModel Devices { get; }

    /// <summary>
    ///     Gets the model catalog panel.
    /// </summary>
    public ModelCatalogPanelViewModel Models { get; }

    /// <summary>
    ///     Gets the editor of the chosen speech-to-text model's parameters.
    /// </summary>
    public ModelParameterEditorViewModel SttParameters { get; }

    /// <summary>
    ///     Gets the editor of the chosen text-to-speech model's parameters.
    /// </summary>
    public ModelParameterEditorViewModel TtsParameters { get; }

    /// <inheritdoc />
    public void Load(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var speech = settings.Speech;

        // Free the editors first so the values of the previous dialog session cannot be captured over the
        // stored ones when the models are chosen again below.
        SttParameters.Model = null;
        TtsParameters.Model = null;
        _parameterValues = speech.ModelParameters.ToDictionary(
            model => model.Key,
            model => new Dictionary<string, string>(model.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);

        Devices.Load(speech.CaptureDevice, speech.PlaybackDevice);
        Models.Load(speech.SttModelId, speech.TtsModelId);
    }

    /// <inheritdoc />
    public AppSettings Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Capture(SttParameters);
        Capture(TtsParameters);
        var parameters = _parameterValues
            .Where(model => model.Value.Count > 0)
            .ToDictionary(
                model => model.Key,
                model => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(model.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);

        return settings with
        {
            Speech = settings.Speech with
            {
                CaptureDevice = Devices.CaptureDeviceName,
                PlaybackDevice = Devices.PlaybackDeviceName,
                SttModelId = Models.SttModelId,
                TtsModelId = Models.TtsModelId,
                ModelParameters = parameters,
            },
        };
    }

    /// <inheritdoc />
    public void OnClosed()
    {
        Models.CancelAllDownloads();
    }

    /// <summary>
    ///     Shows the parameters of a newly chosen model.
    /// </summary>
    /// <param name="sender">The model catalog panel.</param>
    /// <param name="e">The change details.</param>
    private void OnModelsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModelCatalogPanelViewModel.SelectedSttModel))
        {
            Bind(SttParameters, Models.SelectedSttModel);
        }
        else if (e.PropertyName == nameof(ModelCatalogPanelViewModel.SelectedTtsModel))
        {
            Bind(TtsParameters, Models.SelectedTtsModel);
        }
    }

    /// <summary>
    ///     Keeps the values of the model an editor showed and shows the values of the model that replaces it.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="row">The newly chosen model, or <see langword="null" />.</param>
    private void Bind(ModelParameterEditorViewModel editor, ModelListItemViewModel? row)
    {
        Capture(editor);
        editor.Model = row?.Model;
        if (row is not null && _parameterValues.TryGetValue(row.Id, out var values))
        {
            editor.SetValues(values);
        }
    }

    /// <summary>
    ///     Stores the values an editor shows under its model's identifier.
    /// </summary>
    /// <param name="editor">The editor.</param>
    private void Capture(ModelParameterEditorViewModel editor)
    {
        if (editor.Model is { } model && editor.HasParameters)
        {
            _parameterValues[model.Id] = new Dictionary<string, string>(editor.GetValues(), StringComparer.Ordinal);
        }
    }
}

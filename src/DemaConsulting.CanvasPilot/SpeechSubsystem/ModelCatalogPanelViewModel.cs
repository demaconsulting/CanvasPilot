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

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     View model of the model catalog panel: the speech-to-text and text-to-speech models that the catalog knows,
///     the model chosen for each, and downloads that run immediately and can be cancelled. The lists are read only
///     when a page is loaded or refreshed. Failures are shown beside the model, never thrown. Expected to be used
///     from the user interface thread only.
/// </summary>
public sealed partial class ModelCatalogPanelViewModel : ObservableObject
{
    /// <summary>
    ///     Text shown when the catalog lists no model at all.
    /// </summary>
    public const string EmptyCatalogMessage = "The catalog lists no speech models.";

    /// <summary>
    ///     Text shown when a refresh is refused because a download is running.
    /// </summary>
    public const string RefreshBusyMessage =
        "Wait for the downloads to finish, or cancel them, and then refresh the list.";

    /// <summary>
    ///     Lists and downloads the models.
    /// </summary>
    private readonly ISpeechModelService _modelService;

    /// <summary>
    ///     Creates the progress sink of one download; it runs its handler on the user interface thread.
    /// </summary>
    private readonly Func<Action<SpeechModelDownloadProgress>, IProgress<SpeechModelDownloadProgress>> _createProgress;

    /// <summary>
    ///     The cancellation source of each running download, by model identifier. Downloads normally end on the
    ///     user interface thread, but where there is no synchronization context (as in tests) several can end at the
    ///     same time, so the collection tolerates concurrent changes.
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _downloads = new(StringComparer.Ordinal);

    /// <summary>
    ///     The speech-to-text model identifier last chosen or loaded, kept when the list cannot show it.
    /// </summary>
    private string _chosenSttId = string.Empty;

    /// <summary>
    ///     The text-to-speech model identifier last chosen or loaded, kept when the list cannot show it.
    /// </summary>
    private string _chosenTtsId = string.Empty;

    /// <summary>
    ///     Chosen speech-to-text row.
    /// </summary>
    [ObservableProperty]
    private ModelListItemViewModel? _selectedSttModel;

    /// <summary>
    ///     Chosen text-to-speech row.
    /// </summary>
    [ObservableProperty]
    private ModelListItemViewModel? _selectedTtsModel;

    /// <summary>
    ///     Text that explains why the list is empty or could not be read, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = string.Empty;

    /// <summary>
    ///     Creates the panel with empty lists; models are read by <see cref="Load" />.
    /// </summary>
    /// <param name="modelService">Lists and downloads the models.</param>
    /// <param name="createProgress">
    ///     Creates the sink that receives a download's progress and runs its handler on the user interface thread,
    ///     or <see langword="null" /> to use a <see cref="Progress{T}" /> created on the calling thread.
    /// </param>
    public ModelCatalogPanelViewModel(
        ISpeechModelService modelService,
        Func<Action<SpeechModelDownloadProgress>, IProgress<SpeechModelDownloadProgress>>? createProgress = null)
    {
        ArgumentNullException.ThrowIfNull(modelService);

        _modelService = modelService;
        _createProgress = createProgress ?? (handler => new Progress<SpeechModelDownloadProgress>(handler));
    }

    /// <summary>
    ///     Gets the speech-to-text rows.
    /// </summary>
    public ObservableCollection<ModelListItemViewModel> SttModels { get; } = [];

    /// <summary>
    ///     Gets the text-to-speech rows.
    /// </summary>
    public ObservableCollection<ModelListItemViewModel> TtsModels { get; } = [];

    /// <summary>
    ///     Gets a value indicating whether <see cref="Message" /> has text to show.
    /// </summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>
    ///     Gets the identifier of the chosen speech-to-text model, or an empty string. When the list cannot show
    ///     the model that was stored, the stored identifier is kept so a damaged model store does not erase the
    ///     user's choice.
    /// </summary>
    public string SttModelId => SelectedSttModel?.Id ?? _chosenSttId;

    /// <summary>
    ///     Gets the identifier of the chosen text-to-speech model, or an empty string, kept like
    ///     <see cref="SttModelId" />.
    /// </summary>
    public string TtsModelId => SelectedTtsModel?.Id ?? _chosenTtsId;

    /// <summary>
    ///     Gets a value indicating whether a download is running.
    /// </summary>
    public bool IsDownloading => _downloads.Count > 0;

    /// <summary>
    ///     Reads the catalog and chooses the stored models.
    /// </summary>
    /// <param name="sttModelId">The stored speech-to-text model identifier, or an empty string.</param>
    /// <param name="ttsModelId">The stored text-to-speech model identifier, or an empty string.</param>
    public void Load(string sttModelId, string ttsModelId)
    {
        _chosenSttId = sttModelId ?? string.Empty;
        _chosenTtsId = ttsModelId ?? string.Empty;
        CancelAllDownloads();
        Rebuild();
    }

    /// <summary>
    ///     Cancels every running download. The rows return to their earlier state when the downloads end.
    /// </summary>
    public void CancelAllDownloads()
    {
        foreach (var source in _downloads.Values.ToArray())
        {
            source.Cancel();
        }
    }

    /// <summary>
    ///     Downloads one model and shows its progress and outcome in its row. The task ends when the download
    ///     does; it never fails.
    /// </summary>
    /// <param name="model">The row to download, or <see langword="null" /> to do nothing.</param>
    /// <returns>A task that completes when the download has ended and been shown.</returns>
    [RelayCommand]
    public async Task DownloadAsync(ModelListItemViewModel? model)
    {
        if (model is null || !model.CanDownload)
        {
            return;
        }

        using var source = new CancellationTokenSource();
        _downloads[model.Id] = source;
        OnPropertyChanged(nameof(IsDownloading));

        model.FailureMessage = string.Empty;
        model.ProgressFraction = null;
        model.State = SpeechModelState.Downloading;
        var progress = _createProgress(value => model.ProgressFraction = value.FractionComplete);

        SpeechModelDownloadReport report;
        try
        {
            report = await _modelService.DownloadAsync(model.Id, progress, source.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !source.IsCancellationRequested)
        {
            // The service reports failures as results. A service that throws anyway must not take the dialog down.
            report = new SpeechModelDownloadReport(SpeechModelDownloadStatus.Failed, ex.Message);
        }
        catch (OperationCanceledException)
        {
            report = new SpeechModelDownloadReport(SpeechModelDownloadStatus.Canceled, "Download canceled.");
        }

        ShowReport(model, report);
        if (_downloads.TryGetValue(model.Id, out var current) && ReferenceEquals(current, source))
        {
            _downloads.TryRemove(new KeyValuePair<string, CancellationTokenSource>(model.Id, source));
        }

        OnPropertyChanged(nameof(IsDownloading));
    }

    /// <summary>
    ///     Cancels the download of one model.
    /// </summary>
    /// <param name="model">The row whose download to cancel, or <see langword="null" /> to do nothing.</param>
    [RelayCommand]
    public void CancelDownload(ModelListItemViewModel? model)
    {
        if (model is not null && _downloads.TryGetValue(model.Id, out var source))
        {
            source.Cancel();
        }
    }

    /// <summary>
    ///     Reads the catalog again so the install states are current. It is refused while a download runs, because
    ///     the rows that show the downloads must stay.
    /// </summary>
    [RelayCommand]
    private void Refresh()
    {
        if (IsDownloading)
        {
            Message = RefreshBusyMessage;
            return;
        }

        Rebuild();
    }

    /// <summary>
    ///     Remembers the speech-to-text choice made in the list.
    /// </summary>
    /// <param name="value">The chosen row.</param>
    partial void OnSelectedSttModelChanged(ModelListItemViewModel? value)
    {
        if (value is not null)
        {
            _chosenSttId = value.Id;
        }

        OnPropertyChanged(nameof(SttModelId));
    }

    /// <summary>
    ///     Remembers the text-to-speech choice made in the list.
    /// </summary>
    /// <param name="value">The chosen row.</param>
    partial void OnSelectedTtsModelChanged(ModelListItemViewModel? value)
    {
        if (value is not null)
        {
            _chosenTtsId = value.Id;
        }

        OnPropertyChanged(nameof(TtsModelId));
    }

    /// <summary>
    ///     Shows the end of a download in its row.
    /// </summary>
    /// <param name="model">The row.</param>
    /// <param name="report">How the download ended.</param>
    private static void ShowReport(ModelListItemViewModel model, SpeechModelDownloadReport report)
    {
        model.ProgressFraction = report.IsInstalled ? 1.0 : null;
        switch (report.Status)
        {
            case SpeechModelDownloadStatus.Installed:
                model.State = SpeechModelState.Downloaded;
                break;
            case SpeechModelDownloadStatus.Canceled:
                // Nothing was installed, so the model is simply not downloaded.
                model.State = SpeechModelState.NotDownloaded;
                model.FailureMessage = report.Message;
                break;
            default:
                model.State = SpeechModelState.FailedOrCorrupt;
                model.FailureMessage = report.Message;
                break;
        }
    }

    /// <summary>
    ///     Rebuilds both lists from the catalog and chooses the remembered models.
    /// </summary>
    private void Rebuild()
    {
        var list = _modelService.Enumerate();

        SttModels.Clear();
        TtsModels.Clear();
        foreach (var descriptor in list.Models)
        {
            var row = new ModelListItemViewModel(descriptor);
            (descriptor.Role == SpeechModelRole.Recognition ? SttModels : TtsModels).Add(row);
        }

        SelectedSttModel = SttModels.FirstOrDefault(row => string.Equals(row.Id, _chosenSttId, StringComparison.Ordinal));
        SelectedTtsModel = TtsModels.FirstOrDefault(row => string.Equals(row.Id, _chosenTtsId, StringComparison.Ordinal));
        Message = list.Problem ?? (list.Models.Count == 0 ? EmptyCatalogMessage : string.Empty);
    }
}

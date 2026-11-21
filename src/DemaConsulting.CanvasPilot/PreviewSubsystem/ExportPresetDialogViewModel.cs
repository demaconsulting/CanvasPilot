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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Event data for <see cref="ExportPresetDialogViewModel.CloseRequested" />.
/// </summary>
/// <param name="accepted">
///     <see langword="true" /> when the dialog closed through Export; <see langword="false" /> when it was cancelled.
/// </param>
public sealed class ExportPresetCloseRequestedEventArgs(bool accepted) : EventArgs
{
    /// <summary>
    ///     Gets a value indicating whether the dialog closed through Export.
    /// </summary>
    public bool Accepted { get; } = accepted;
}

/// <summary>
///     View model of the Export PNG dialog: the ways that the shown picture can be exported, one of which is selected,
///     and a line that says what the file will be. Export is enabled while a way is selected; the caller reads
///     <see cref="SelectedPreset" /> when the dialog was accepted.
/// </summary>
public sealed partial class ExportPresetDialogViewModel : ObservableObject
{
    /// <summary>
    ///     The line that is selected.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPreset), nameof(ResultText))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private ExportPresetItemViewModel? _selectedItem;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExportPresetDialogViewModel" /> class.
    /// </summary>
    /// <param name="presets">The ways to export, in the order to show them.</param>
    /// <param name="last">The way chosen the last time, which is selected when it is among the presets; otherwise the first is selected.</param>
    public ExportPresetDialogViewModel(IReadOnlyList<ExportPreset> presets, ExportPreset? last = null)
    {
        ArgumentNullException.ThrowIfNull(presets);

        Items = presets.Select(preset => new ExportPresetItemViewModel(preset)).ToArray();

        // Select what was chosen the last time when it is still offered, else the first way (as shown)
        _selectedItem = (last is null ? null : Items.FirstOrDefault(item => item.Preset.Id == last.Id))
                        ?? Items.FirstOrDefault();
    }

    /// <summary>
    ///     Raised when the dialog asks its host to close.
    /// </summary>
    public event EventHandler<ExportPresetCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    ///     Gets the dialog title.
    /// </summary>
    public string Title { get; } = "Export PNG";

    /// <summary>
    ///     Gets the ways to export, in the order they are shown.
    /// </summary>
    public IReadOnlyList<ExportPresetItemViewModel> Items { get; }

    /// <summary>
    ///     Gets a value indicating whether the dialog was closed through Export.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    ///     Gets the preset of the selected line, or <see langword="null" /> when none is selected.
    /// </summary>
    public ExportPreset? SelectedPreset => SelectedItem?.Preset;

    /// <summary>
    ///     Gets a sentence that says what the file will be for the selected line.
    /// </summary>
    public string ResultText
    {
        get
        {
            if (SelectedPreset is not { } preset)
            {
                return string.Empty;
            }

            if (preset.IsAsRendered)
            {
                return "Result: the picture as it is shown, with its own size and background.";
            }

            var text = (preset.Width, preset.Height) switch
            {
                ({ } w, { } h) => $"Result: {w} x {h} px, the picture fitted inside with its proportions kept.",
                ({ } w, null) => $"Result: {w} px wide, as high as the picture needs.",
                (null, { } h) => $"Result: {h} px high, as wide as the picture needs.",
                _ => "Result: the natural size of the picture."
            };

            if (preset.Transparent)
            {
                text += " The background stays transparent.";
            }
            else if (preset.BackgroundHex is { Length: > 0 } background)
            {
                text += $" The background is {background}.";
            }

            return text;
        }
    }

    /// <summary>
    ///     Accepts the selected line and closes the dialog.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        Accepted = true;
        CloseRequested?.Invoke(this, new ExportPresetCloseRequestedEventArgs(accepted: true));
    }

    /// <summary>
    ///     Closes the dialog without accepting.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new ExportPresetCloseRequestedEventArgs(accepted: false));
    }

    /// <summary>
    ///     Gets a value indicating whether a line is selected, which is what Export needs.
    /// </summary>
    /// <returns><see langword="true" /> when a line is selected.</returns>
    private bool CanExport() => SelectedItem is not null;
}

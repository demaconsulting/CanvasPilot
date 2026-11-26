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
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     One row of the model list: a model's identity, which does not change, and its install state, download
///     progress, and last failure, which change while the user watches. Expected to be used from the user interface
///     thread only.
/// </summary>
public sealed partial class ModelListItemViewModel : ObservableObject
{
    /// <summary>
    ///     Install state as last reported or observed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(CanDownload), nameof(IsDownloading))]
    private SpeechModelState _state;

    /// <summary>
    ///     Fraction of the download that is complete, from 0 to 1, or <see langword="null" /> when it cannot be
    ///     measured.
    /// </summary>
    [ObservableProperty]
    private double? _progressFraction;

    /// <summary>
    ///     Explanation of the last download that did not install the model, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailureMessage))]
    private string _failureMessage = string.Empty;

    /// <summary>
    ///     Creates a row from a catalog entry.
    /// </summary>
    /// <param name="descriptor">The catalog entry.</param>
    public ModelListItemViewModel(SpeechModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Model = descriptor.Model;
        Id = descriptor.Id;
        DisplayName = descriptor.DisplayName;
        Role = descriptor.Role;
        _state = descriptor.State;
    }

    /// <summary>
    ///     Gets the model, whose parameters the parameter editor presents.
    /// </summary>
    public ISpeechModel Model { get; }

    /// <summary>
    ///     Gets the identifier that is stored in the settings and used to request the download.
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets the name shown in the list.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    ///     Gets whether the model recognizes or synthesizes speech.
    /// </summary>
    public SpeechModelRole Role { get; }

    /// <summary>
    ///     Gets a value indicating whether a failure explanation is available to show.
    /// </summary>
    public bool HasFailureMessage => FailureMessage.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether a download can be started now. A model that is installed or being
    ///     downloaded cannot, and one whose last attempt failed can, so a transient network failure can be retried.
    /// </summary>
    public bool CanDownload => State is SpeechModelState.NotDownloaded or SpeechModelState.FailedOrCorrupt;

    /// <summary>
    ///     Gets a value indicating whether a download is in progress.
    /// </summary>
    public bool IsDownloading => State == SpeechModelState.Downloading;

    /// <summary>
    ///     Gets the text that describes the install state.
    /// </summary>
    public string StatusText => State switch
    {
        SpeechModelState.NotDownloaded => "Not downloaded",
        SpeechModelState.Downloading => "Downloading",
        SpeechModelState.Downloaded => "Installed",
        SpeechModelState.FailedOrCorrupt => "Failed or corrupt",
        _ => State.ToString(),
    };

    /// <summary>
    ///     Gives the model name shown as the first line of the row, so a screen reader names the row by the model.
    /// </summary>
    /// <remarks>
    ///     A list row whose template is a panel of two lines is named by the text of its item, which would otherwise
    ///     be the name of this type. The install state stays readable as the second line of the row.
    /// </remarks>
    /// <returns>The <see cref="DisplayName" />.</returns>
    public override string ToString() => DisplayName;
}

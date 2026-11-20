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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     One entry of a device list: the stored device name and the text the list shows for it.
/// </summary>
/// <param name="Name">
///     The device name, which is the only stable identity the audio library offers, or an empty string for the
///     system default device.
/// </param>
/// <param name="Label">The text shown in the list.</param>
public sealed record DeviceChoice(string Name, string Label)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
///     View model of the audio device pickers: a capture (microphone) list and a playback (speaker) list, each
///     with the system default as its first entry. The lists are read only when a page is loaded or refreshed, so
///     starting the application never touches the audio system. A machine with no devices or no audio system is
///     reported in a status text beside each list.
/// </summary>
public sealed partial class DeviceSelectionViewModel : ObservableObject
{
    /// <summary>
    ///     The label of the entry that stands for the system default device.
    /// </summary>
    public const string SystemDefaultLabel = "System default";

    /// <summary>
    ///     The entry that stands for the system default device.
    /// </summary>
    private static readonly DeviceChoice SystemDefault = new(string.Empty, SystemDefaultLabel);

    /// <summary>
    ///     Lists the audio devices.
    /// </summary>
    private readonly IAudioDeviceService _deviceService;

    /// <summary>
    ///     Selected capture entry.
    /// </summary>
    [ObservableProperty]
    private DeviceChoice? _selectedCaptureDevice = SystemDefault;

    /// <summary>
    ///     Selected playback entry.
    /// </summary>
    [ObservableProperty]
    private DeviceChoice? _selectedPlaybackDevice = SystemDefault;

    /// <summary>
    ///     Text that describes the capture list.
    /// </summary>
    [ObservableProperty]
    private string _captureStatus = string.Empty;

    /// <summary>
    ///     Text that describes the playback list.
    /// </summary>
    [ObservableProperty]
    private string _playbackStatus = string.Empty;

    /// <summary>
    ///     Creates the view model with only the system default entries; devices are read by <see cref="Load" />.
    /// </summary>
    /// <param name="deviceService">Lists the audio devices.</param>
    public DeviceSelectionViewModel(IAudioDeviceService deviceService)
    {
        ArgumentNullException.ThrowIfNull(deviceService);

        _deviceService = deviceService;
        CaptureDevices.Add(SystemDefault);
        PlaybackDevices.Add(SystemDefault);
    }

    /// <summary>
    ///     Gets the capture entries, starting with the system default.
    /// </summary>
    public ObservableCollection<DeviceChoice> CaptureDevices { get; } = [];

    /// <summary>
    ///     Gets the playback entries, starting with the system default.
    /// </summary>
    public ObservableCollection<DeviceChoice> PlaybackDevices { get; } = [];

    /// <summary>
    ///     Gets the name of the selected capture device, or an empty string for the system default.
    /// </summary>
    public string CaptureDeviceName => SelectedCaptureDevice?.Name ?? string.Empty;

    /// <summary>
    ///     Gets the name of the selected playback device, or an empty string for the system default.
    /// </summary>
    public string PlaybackDeviceName => SelectedPlaybackDevice?.Name ?? string.Empty;

    /// <summary>
    ///     Reads the device lists and selects the stored devices. A stored device that is not connected stays
    ///     selected, marked as not connected, so the choice is not lost silently.
    /// </summary>
    /// <param name="captureDeviceName">The stored capture device name, or an empty string for the default.</param>
    /// <param name="playbackDeviceName">The stored playback device name, or an empty string for the default.</param>
    public void Load(string captureDeviceName, string playbackDeviceName)
    {
        Show(_deviceService.Enumerate(), captureDeviceName ?? string.Empty, playbackDeviceName ?? string.Empty);
    }

    /// <summary>
    ///     Makes the audio system scan for newly attached or removed devices, then reads the lists again. The
    ///     current choices are kept.
    /// </summary>
    [RelayCommand]
    private void Refresh()
    {
        Show(_deviceService.Refresh(), CaptureDeviceName, PlaybackDeviceName);
    }

    /// <summary>
    ///     Replaces both lists from a snapshot and selects the given devices.
    /// </summary>
    /// <param name="snapshot">The devices found.</param>
    /// <param name="captureName">The capture device name to select.</param>
    /// <param name="playbackName">The playback device name to select.</param>
    private void Show(AudioDeviceSnapshot snapshot, string captureName, string playbackName)
    {
        SelectedCaptureDevice = Fill(CaptureDevices, snapshot.CaptureDevices, captureName);
        SelectedPlaybackDevice = Fill(PlaybackDevices, snapshot.PlaybackDevices, playbackName);
        CaptureStatus = Describe(snapshot, snapshot.CaptureDevices.Count, "capture");
        PlaybackStatus = Describe(snapshot, snapshot.PlaybackDevices.Count, "playback");
    }

    /// <summary>
    ///     Replaces the content of a list and finds the entry to select.
    /// </summary>
    /// <param name="target">The list to fill.</param>
    /// <param name="devices">The devices found.</param>
    /// <param name="selectedName">The name to select.</param>
    /// <returns>The entry to select.</returns>
    private static DeviceChoice Fill(
        ObservableCollection<DeviceChoice> target,
        IReadOnlyList<AudioDeviceDescription> devices,
        string selectedName)
    {
        target.Clear();
        target.Add(SystemDefault);
        foreach (var name in devices.Select(device => device.Name).Where(name => name.Length > 0).Distinct(StringComparer.Ordinal))
        {
            target.Add(new DeviceChoice(name, name));
        }

        var selected = target.FirstOrDefault(choice => string.Equals(choice.Name, selectedName, StringComparison.Ordinal));
        if (selected is null)
        {
            selected = new DeviceChoice(selectedName, $"{selectedName} (not connected)");
            target.Add(selected);
        }

        return selected;
    }

    /// <summary>
    ///     Builds the status text of one list.
    /// </summary>
    /// <param name="snapshot">The devices found.</param>
    /// <param name="count">The number of devices in the list.</param>
    /// <param name="direction">The direction as a word.</param>
    /// <returns>The text.</returns>
    private static string Describe(AudioDeviceSnapshot snapshot, int count, string direction)
    {
        if (!snapshot.IsAvailable)
        {
            return snapshot.Problem!;
        }

        return count == 0
            ? $"No {direction} device was found. The system default is used when one is connected."
            : $"{count} {direction} device(s) found.";
    }

    /// <summary>
    ///     Announces the derived device names.
    /// </summary>
    /// <param name="value">The new capture entry.</param>
    partial void OnSelectedCaptureDeviceChanged(DeviceChoice? value)
    {
        OnPropertyChanged(nameof(CaptureDeviceName));
    }

    /// <summary>
    ///     Announces the derived device names.
    /// </summary>
    /// <param name="value">The new playback entry.</param>
    partial void OnSelectedPlaybackDeviceChanged(DeviceChoice? value)
    {
        OnPropertyChanged(nameof(PlaybackDeviceName));
    }
}

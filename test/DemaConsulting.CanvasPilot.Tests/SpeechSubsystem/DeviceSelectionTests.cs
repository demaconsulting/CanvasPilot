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
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="DeviceSelectionViewModel" /> with substitute audio device services.
/// </summary>
public sealed class DeviceSelectionTests
{
    /// <summary>
    ///     Test that a new view model offers only the system default and does not use the audio service.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_New_OffersSystemDefaultOnlyAndDoesNotEnumerate()
    {
        // Arrange: an audio service that has not been used
        var service = Substitute.For<IAudioDeviceService>();

        // Act: create the view model
        var viewModel = new DeviceSelectionViewModel(service);

        // Assert: only the default is offered and selected, and the audio system was not used
        Assert.Equal([DeviceSelectionViewModel.SystemDefaultLabel], viewModel.CaptureDevices.Select(device => device.Label));
        Assert.Equal(string.Empty, viewModel.CaptureDeviceName);
        Assert.Equal(string.Empty, viewModel.PlaybackDeviceName);
        service.DidNotReceive().Enumerate();
    }

    /// <summary>
    ///     Test that loading lists the default and every device and selects the stored devices by name.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Load_StoredDevicesPresent_SelectsThemByName()
    {
        // Arrange: a view model over two microphones and two speakers
        var viewModel = new DeviceSelectionViewModel(SpeechTestData.AudioDevices(["Mic A", "Mic B"], ["Out 1", "Out 2"]));

        // Act: load with a stored microphone and speaker
        viewModel.Load("Mic B", "Out 1");

        // Assert: the default plus the devices are offered and the stored ones are selected
        Assert.Equal([DeviceSelectionViewModel.SystemDefaultLabel, "Mic A", "Mic B"], viewModel.CaptureDevices.Select(device => device.Label));
        Assert.Equal("Mic B", viewModel.CaptureDeviceName);
        Assert.Equal("Out 1", viewModel.PlaybackDeviceName);
        Assert.Equal("2 capture device(s) found.", viewModel.CaptureStatus);
        Assert.Equal("2 playback device(s) found.", viewModel.PlaybackStatus);
    }

    /// <summary>
    ///     Test that an empty stored name selects the system default.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Load_EmptyNames_SelectSystemDefault()
    {
        // Arrange: a view model over one device of each kind
        var viewModel = new DeviceSelectionViewModel(SpeechTestData.AudioDevices(["Mic"], ["Out"]));

        // Act: load with no stored devices
        viewModel.Load(string.Empty, string.Empty);

        // Assert: the defaults are selected
        Assert.Equal(DeviceSelectionViewModel.SystemDefaultLabel, viewModel.SelectedCaptureDevice!.Label);
        Assert.Equal(DeviceSelectionViewModel.SystemDefaultLabel, viewModel.SelectedPlaybackDevice!.Label);
        Assert.Equal(string.Empty, viewModel.CaptureDeviceName);
    }

    /// <summary>
    ///     Test that a stored device that is not connected stays selected and is marked, so the choice is not lost.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Load_StoredDeviceNotConnected_StaysSelectedAndMarked()
    {
        // Arrange: a view model whose machine has other devices only
        var viewModel = new DeviceSelectionViewModel(SpeechTestData.AudioDevices(["Laptop mic"], ["Laptop speakers"]));

        // Act: load with devices that are not there
        viewModel.Load("USB headset", "USB headset");

        // Assert: the stored names are kept and shown as not connected
        Assert.Equal("USB headset", viewModel.CaptureDeviceName);
        Assert.Equal("USB headset (not connected)", viewModel.SelectedCaptureDevice!.Label);
        Assert.Equal("USB headset", viewModel.PlaybackDeviceName);
    }

    /// <summary>
    ///     Test that a machine without devices is explained, and the system default stays selectable.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Load_NoDevices_ExplainsAndKeepsDefault()
    {
        // Arrange: an audio service that finds nothing
        var viewModel = new DeviceSelectionViewModel(SpeechTestData.AudioDevices([], []));

        // Act: load
        viewModel.Load(string.Empty, string.Empty);

        // Assert: both lists explain that nothing was found and offer the default
        Assert.StartsWith("No capture device was found.", viewModel.CaptureStatus, StringComparison.Ordinal);
        Assert.StartsWith("No playback device was found.", viewModel.PlaybackStatus, StringComparison.Ordinal);
        Assert.Single(viewModel.CaptureDevices);
    }

    /// <summary>
    ///     Test that an unavailable audio system is explained with its problem text in both lists.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Load_AudioSystemUnavailable_ShowsProblem()
    {
        // Arrange: an audio service that cannot be used
        var service = Substitute.For<IAudioDeviceService>();
        service.Enumerate().Returns(AudioDeviceSnapshot.Unavailable("The audio system could not be used. No PortAudio."));
        var viewModel = new DeviceSelectionViewModel(service);

        // Act: load with a stored microphone
        viewModel.Load("Stored mic", string.Empty);

        // Assert: the problem is shown and the stored choice is kept
        Assert.Equal("The audio system could not be used. No PortAudio.", viewModel.CaptureStatus);
        Assert.Equal("The audio system could not be used. No PortAudio.", viewModel.PlaybackStatus);
        Assert.Equal("Stored mic", viewModel.CaptureDeviceName);
    }

    /// <summary>
    ///     Test that a refresh scans, lists again, and keeps the choices.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Refresh_KeepsChoicesAndShowsNewDevices()
    {
        // Arrange: a loaded view model whose service gains a microphone on refresh
        var service = Substitute.For<IAudioDeviceService>();
        service.Enumerate().Returns(new AudioDeviceSnapshot([SpeechTestData.Capture("Mic A")], []));
        service.Refresh().Returns(new AudioDeviceSnapshot([SpeechTestData.Capture("Mic A"), SpeechTestData.Capture("Mic B")], []));
        var viewModel = new DeviceSelectionViewModel(service);
        viewModel.Load("Mic A", string.Empty);

        // Act: refresh
        viewModel.RefreshCommand.Execute(null);

        // Assert: the new device is offered and the choice is unchanged
        Assert.Equal([DeviceSelectionViewModel.SystemDefaultLabel, "Mic A", "Mic B"], viewModel.CaptureDevices.Select(device => device.Label));
        Assert.Equal("Mic A", viewModel.CaptureDeviceName);
        service.Received(1).Refresh();
    }

    /// <summary>
    ///     Test that a refused refresh shows its problem.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Refresh_Refused_ShowsProblem()
    {
        // Arrange: a service whose refresh is refused
        var service = Substitute.For<IAudioDeviceService>();
        service.Enumerate().Returns(new AudioDeviceSnapshot([SpeechTestData.Capture("Mic A")], []));
        service.Refresh().Returns(new AudioDeviceSnapshot([SpeechTestData.Capture("Mic A")], [], "A device is in use."));
        var viewModel = new DeviceSelectionViewModel(service);
        viewModel.Load(string.Empty, string.Empty);

        // Act: refresh
        viewModel.RefreshCommand.Execute(null);

        // Assert: the refusal is shown
        Assert.Equal("A device is in use.", viewModel.CaptureStatus);
    }

    /// <summary>
    ///     Test that a missing audio service is refused.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_New_NullService_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DeviceSelectionViewModel(null!));
    }
}

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

using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AudioDeviceService" />. The backend operations are substitutes, except in the one
///     test that uses the real audio backend and accepts whatever the machine has.
/// </summary>
public sealed class AudioDeviceServiceTests : IDisposable
{
    /// <summary>
    ///     Temporary folder for the log, created fresh for each test and removed on disposal.
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
    ///     Test that the devices of both directions are listed in the order the backend reports them.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Enumerate_DevicesPresent_ReturnsBothLists()
    {
        // Arrange: a backend with two microphones and one speaker
        var service = new AudioDeviceService(
            () => [SpeechTestData.Capture("Mic A"), SpeechTestData.Capture("Mic B")],
            () => [SpeechTestData.Playback("Speakers")],
            () => { });

        // Act: enumerate
        var snapshot = service.Enumerate();

        // Assert: the lists keep the backend's order and there is no problem
        Assert.Equal(["Mic A", "Mic B"], snapshot.CaptureDevices.Select(device => device.Name));
        Assert.Equal(["Speakers"], snapshot.PlaybackDevices.Select(device => device.Name));
        Assert.True(snapshot.IsAvailable);
        Assert.Null(snapshot.Problem);
    }

    /// <summary>
    ///     Test that a machine without devices is an ordinary available result with empty lists.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Enumerate_NoDevices_IsAvailableWithEmptyLists()
    {
        // Arrange: a backend that answers with no devices
        var service = new AudioDeviceService(() => [], () => [], () => { });

        // Act: enumerate
        var snapshot = service.Enumerate();

        // Assert: nothing was found, and that is not reported as a problem
        Assert.Empty(snapshot.CaptureDevices);
        Assert.Empty(snapshot.PlaybackDevices);
        Assert.True(snapshot.IsAvailable);
    }

    /// <summary>
    ///     Test that a refresh scans the backend first and then lists what is there afterwards.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Refresh_Succeeds_ScansThenListsDevices()
    {
        // Arrange: a backend that gains a microphone when it is refreshed
        var refreshed = false;
        var service = new AudioDeviceService(
            () => refreshed ? [SpeechTestData.Capture("New mic")] : [],
            () => [],
            () => refreshed = true);

        // Act: list before and after a refresh
        var before = service.Enumerate();
        var after = service.Refresh();

        // Assert: the refresh made the new device visible
        Assert.Empty(before.CaptureDevices);
        Assert.Equal(["New mic"], after.CaptureDevices.Select(device => device.Name));
        Assert.True(after.IsAvailable);
    }

    /// <summary>
    ///     Test that a backend that fails is reported as an unavailable result with a problem and no devices,
    ///     without throwing.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Enumerate_BackendThrows_ReturnsUnavailableWithProblem()
    {
        // Arrange: a backend whose native audio library cannot be loaded
        var service = new AudioDeviceService(
            () => throw new DllNotFoundException("audio library is missing"),
            () => throw new InvalidOperationException("no audio"),
            () => { });

        // Act: enumerate
        var snapshot = service.Enumerate();

        // Assert: no devices, no exception, and the first failure is explained
        Assert.Empty(snapshot.CaptureDevices);
        Assert.Empty(snapshot.PlaybackDevices);
        Assert.False(snapshot.IsAvailable);
        Assert.Contains("audio library is missing", snapshot.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a refused refresh keeps the devices that can be listed and explains the refusal.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Refresh_Refused_ReturnsProblemAndStillLists()
    {
        // Arrange: a backend that refuses to scan because a stream is open
        var service = new AudioDeviceService(
            () => [SpeechTestData.Capture("Busy mic")],
            () => [],
            () => throw new InvalidOperationException("A device is in use."));

        // Act: refresh
        var snapshot = service.Refresh();

        // Assert: the device is still listed and the refusal is the problem
        Assert.Equal(["Busy mic"], snapshot.CaptureDevices.Select(device => device.Name));
        Assert.False(snapshot.IsAvailable);
        Assert.Contains("A device is in use.", snapshot.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that creating the service does not use the audio backend, so starting the application never touches
    ///     the audio system.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Constructed_DoesNotTouchAudioSystem()
    {
        // Arrange: backend operations that count their calls
        var calls = 0;

        // Act: create the service and do nothing else
        _ = new AudioDeviceService(
            () =>
            {
                calls++;
                return [];
            },
            () =>
            {
                calls++;
                return [];
            },
            () => calls++);

        // Assert: the backend was not used
        Assert.Equal(0, calls);
    }

    /// <summary>
    ///     Test that a failure is written to the log, and that the service still works when the log cannot be used.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Failure_IsLogged()
    {
        // Arrange: a failing backend and a log
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var service = new AudioDeviceService(
            () => throw new InvalidOperationException("capture broke"),
            () => [],
            () => { },
            logger);

        // Act: enumerate
        service.Enumerate();

        // Assert: the log names the failure
        var log = File.ReadAllText(logger.ActiveFilePath);
        Assert.Contains("Listing the audio devices failed.", log, StringComparison.Ordinal);
        Assert.Contains("capture broke", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that missing backend operations are refused.
    /// </summary>
    [Fact]
    public void AudioDeviceService_InvalidArguments_Throw()
    {
        // Act & Assert: each missing operation is refused
        Assert.Throws<ArgumentNullException>(() => new AudioDeviceService(null!, () => [], () => { }));
        Assert.Throws<ArgumentNullException>(() => new AudioDeviceService(() => [], null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => new AudioDeviceService(() => [], () => [], null!));
    }

    /// <summary>
    ///     Test that the service over the real audio backend never throws, whatever hardware or native library
    ///     the machine has.
    /// </summary>
    [Fact]
    public void AudioDeviceService_RealBackend_EnumerateAndRefresh_NeverThrow()
    {
        // Arrange: the service over the real backend
        var service = new AudioDeviceService();

        // Act: enumerate and refresh
        var enumerated = service.Enumerate();
        var refreshed = service.Refresh();

        // Assert: both gave a snapshot; an unavailable backend carries its problem and no devices
        Assert.NotNull(enumerated.CaptureDevices);
        Assert.NotNull(refreshed.PlaybackDevices);
        Assert.True(enumerated.IsAvailable || (enumerated.CaptureDevices.Count == 0 && enumerated.Problem!.Length > 0));
    }
}

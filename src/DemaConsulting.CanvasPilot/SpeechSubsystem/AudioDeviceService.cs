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
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     Lists the audio devices through the DemaConsulting.Speech library's <see cref="AudioDeviceFactory" />. The
///     factory is created on first use, so starting the application never touches the audio backend. Every failure
///     of the backend, including a machine without audio hardware or without the native audio library, is returned
///     as an empty list with a problem text and never thrown.
/// </summary>
public sealed class AudioDeviceService : IAudioDeviceService
{
    /// <summary>
    ///     Message used when the backend failed with no usable message of its own.
    /// </summary>
    private const string UnavailableMessage = "The audio system could not be used.";

    /// <summary>
    ///     Message used when the audio system answered but offered no usable microphone.
    /// </summary>
    private const string NoMicrophoneMessage =
        "No microphone could be used. Connect one, and check that the operating system allows desktop applications "
        + "to use the microphone.";

    /// <summary>
    ///     The sample rate, in Hz, that capture devices are asked for. Speech models take 16 kHz mono audio, so asking
    ///     for it lets the audio system do the conversion from the hardware's own format.
    /// </summary>
    private const int CaptureSampleRate = 16000;

    /// <summary>
    ///     Lists the capture devices.
    /// </summary>
    private readonly Func<IReadOnlyList<AudioDeviceDescription>> _captureLister;

    /// <summary>
    ///     Lists the playback devices.
    /// </summary>
    private readonly Func<IReadOnlyList<AudioDeviceDescription>> _playbackLister;

    /// <summary>
    ///     Makes the backend scan for changed hardware.
    /// </summary>
    private readonly Action _refresher;

    /// <summary>
    ///     Creates a capture device for a chosen device name.
    /// </summary>
    private readonly Func<string?, IAudioCaptureDevice> _captureCreator;

    /// <summary>
    ///     The application log, or <see langword="null" /> for none.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The library factory, created on first use.
    /// </summary>
    private AudioDeviceFactory? _factory;

    /// <summary>
    ///     Creates a service over the library's real audio backend.
    /// </summary>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    public AudioDeviceService(RollingFileLogger? logger = null)
    {
        _logger = logger;
        _captureLister = () => Factory.CaptureProbe.Enumerate();
        _playbackLister = () => Factory.PlaybackProbe.Enumerate();
        _refresher = () => Factory.RefreshDevices();
        _captureCreator = CreateLibraryCaptureDevice;
    }

    /// <summary>
    ///     Creates a service over substitute backend operations, so tests can cause backend failures that real
    ///     hardware cannot be made to produce.
    /// </summary>
    /// <param name="captureLister">Lists the capture devices.</param>
    /// <param name="playbackLister">Lists the playback devices.</param>
    /// <param name="refresher">Makes the backend scan for changed hardware.</param>
    /// <param name="logger">The application log, or <see langword="null" /> for none.</param>
    /// <param name="captureCreator">
    ///     Creates a capture device for a device name, or <see langword="null" /> when the test does not create
    ///     capture devices; the service then reports that no capture device can be created.
    /// </param>
    internal AudioDeviceService(
        Func<IReadOnlyList<AudioDeviceDescription>> captureLister,
        Func<IReadOnlyList<AudioDeviceDescription>> playbackLister,
        Action refresher,
        RollingFileLogger? logger = null,
        Func<string?, IAudioCaptureDevice>? captureCreator = null)
    {
        ArgumentNullException.ThrowIfNull(captureLister);
        ArgumentNullException.ThrowIfNull(playbackLister);
        ArgumentNullException.ThrowIfNull(refresher);

        _captureLister = captureLister;
        _playbackLister = playbackLister;
        _refresher = refresher;
        _logger = logger;
        _captureCreator = captureCreator
            ?? (_ => throw new NotSupportedException("This audio backend cannot create capture devices."));
    }

    /// <summary>
    ///     Gets the library factory, creating it on first use.
    /// </summary>
    private AudioDeviceFactory Factory => _factory ??= new AudioDeviceFactory();

    /// <inheritdoc />
    public AudioDeviceSnapshot Enumerate()
    {
        return ListDevices(earlierProblem: null);
    }

    /// <inheritdoc />
    public CaptureDeviceResult CreateCaptureDevice(string? deviceName)
    {
        try
        {
            var device = _captureCreator(deviceName);

            // The library never throws for a missing microphone: it hands back a device that says it is unavailable,
            // and starting such a device would throw. Turning it into a result here keeps that out of the caller.
            if (device is { IsAvailable: true })
            {
                return new CaptureDeviceResult(device);
            }

            return CaptureDeviceResult.Unavailable(NoMicrophoneMessage);
        }
        catch (Exception ex)
        {
            // The native audio library can fail in ways that differ by platform and machine; dictation must still
            // be able to say what is wrong instead of failing.
            LogProblem("Creating the capture device failed.", ex);
            return CaptureDeviceResult.Unavailable(Describe(ex));
        }
    }

    /// <inheritdoc />
    public AudioDeviceSnapshot Refresh()
    {
        string? problem = null;
        try
        {
            _refresher();
        }
        catch (Exception ex)
        {
            // The library refuses a scan while a stream is open and can fail while it restarts its native audio
            // library. Neither may reach the user as a crash, so the refusal becomes the problem text and the
            // devices are listed as they are.
            problem = Describe(ex);
            LogProblem("Refreshing the audio devices failed.", ex);
        }

        return ListDevices(problem);
    }

    /// <summary>
    ///     Creates a capture device through the library factory for the chosen device name.
    /// </summary>
    /// <param name="deviceName">The chosen device name, or <see langword="null" /> or empty for the system default.</param>
    /// <returns>The device, which may report that it is unavailable.</returns>
    private IAudioCaptureDevice CreateLibraryCaptureDevice(string? deviceName)
    {
        // An empty name is how the settings file says "system default", which the library spells as a selection
        // without a name; a name that no device has any more falls back to the default inside the library.
        var selection = string.IsNullOrWhiteSpace(deviceName)
            ? AudioDeviceSelection.SystemDefault
            : new AudioDeviceSelection(deviceName);
        return Factory.CreateCaptureDevice(selection, AudioFormat.Mono(CaptureSampleRate));
    }

    /// <summary>
    ///     Lists both directions, turning a failure of either into the problem text.
    /// </summary>
    /// <param name="earlierProblem">A problem already known, which stays the reported one.</param>
    /// <returns>The snapshot.</returns>
    private AudioDeviceSnapshot ListDevices(string? earlierProblem)
    {
        var problem = earlierProblem;
        var capture = Safely(_captureLister, ref problem);
        var playback = Safely(_playbackLister, ref problem);
        return new AudioDeviceSnapshot(capture, playback, problem);
    }

    /// <summary>
    ///     Runs a lister and returns an empty list when it fails.
    /// </summary>
    /// <param name="lister">The lister to run.</param>
    /// <param name="problem">Receives the failure's description unless a problem is already set.</param>
    /// <returns>The devices, or an empty list.</returns>
    private IReadOnlyList<AudioDeviceDescription> Safely(
        Func<IReadOnlyList<AudioDeviceDescription>> lister,
        ref string? problem)
    {
        try
        {
            return lister() ?? [];
        }
        catch (Exception ex)
        {
            // A machine can lack audio hardware or the native audio library; the application must still run.
            problem ??= Describe(ex);
            LogProblem("Listing the audio devices failed.", ex);
            return [];
        }
    }

    /// <summary>
    ///     Builds the text that tells the user why the audio system could not be used.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The text.</returns>
    private static string Describe(Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? UnavailableMessage
            : $"{UnavailableMessage} {exception.Message}";
    }

    /// <summary>
    ///     Writes a warning to the log when there is one; logging never fails the caller.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="exception">The failure.</param>
    private void LogProblem(string message, Exception exception)
    {
        try
        {
            _logger?.Log(LogLevel.Warning, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make an audio problem worse.
        }
    }
}

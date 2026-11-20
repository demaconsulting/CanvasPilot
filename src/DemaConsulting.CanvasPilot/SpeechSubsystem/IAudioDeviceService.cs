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

using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     The audio devices found by one enumeration, and why enumeration was impossible when it was.
/// </summary>
/// <param name="CaptureDevices">The capture (microphone) devices, in the order the audio backend reports them.</param>
/// <param name="PlaybackDevices">The playback (speaker) devices, in the order the audio backend reports them.</param>
/// <param name="Problem">
///     The explanation when the audio backend could not be used, or <see langword="null" /> when it answered. An
///     answer with no devices is not a problem: the machine simply has none.
/// </param>
public sealed record AudioDeviceSnapshot(
    IReadOnlyList<AudioDeviceDescription> CaptureDevices,
    IReadOnlyList<AudioDeviceDescription> PlaybackDevices,
    string? Problem = null)
{
    /// <summary>
    ///     Gets a value indicating whether the audio backend could be used.
    /// </summary>
    public bool IsAvailable => Problem is null;

    /// <summary>
    ///     Creates a snapshot for an audio backend that could not be used.
    /// </summary>
    /// <param name="problem">The explanation.</param>
    /// <returns>A snapshot with no devices and the explanation.</returns>
    public static AudioDeviceSnapshot Unavailable(string problem)
    {
        return new AudioDeviceSnapshot([], [], problem);
    }
}

/// <summary>
///     The outcome of asking for a capture device: the device, or the reason there is none.
/// </summary>
/// <param name="Device">
///     The capture device, which the caller must stop and, when it is <see cref="IDisposable" />, dispose, or
///     <see langword="null" /> when no usable device could be created.
/// </param>
/// <param name="Problem">The explanation when there is no device, or <see langword="null" />.</param>
public sealed record CaptureDeviceResult(IAudioCaptureDevice? Device, string? Problem = null)
{
    /// <summary>
    ///     Gets a value indicating whether a usable device was created.
    /// </summary>
    public bool IsAvailable => Device is not null;

    /// <summary>
    ///     Creates a result for a device that could not be created.
    /// </summary>
    /// <param name="problem">The explanation.</param>
    /// <returns>A result with no device and the explanation.</returns>
    public static CaptureDeviceResult Unavailable(string problem)
    {
        return new CaptureDeviceResult(null, problem);
    }
}

/// <summary>
///     Lists the machine's audio devices and creates the capture device that dictation listens through. A machine
///     without audio hardware, or without a working audio backend, is an ordinary state that is reported in the
///     result, never thrown.
/// </summary>
public interface IAudioDeviceService
{
    /// <summary>
    ///     Creates a capture device that delivers audio in the mono format that speech recognition needs. The device
    ///     is created, not started: the recognition session that is given the device starts and stops it.
    /// </summary>
    /// <param name="deviceName">
    ///     The name of the chosen microphone, or <see langword="null" /> or an empty string for the system default. A
    ///     name that no device has any more also selects the system default.
    /// </param>
    /// <returns>The device, or no device with the problem when the audio system or the microphone cannot be used.</returns>
    CaptureDeviceResult CreateCaptureDevice(string? deviceName);

    /// <summary>
    ///     Lists the audio devices the audio backend currently knows.
    /// </summary>
    /// <returns>The devices, or an empty snapshot with the problem when the backend cannot be used.</returns>
    AudioDeviceSnapshot Enumerate();

    /// <summary>
    ///     Makes the audio backend scan for newly attached or removed hardware and then lists the devices.
    /// </summary>
    /// <returns>
    ///     The devices, with the problem when the scan was refused or failed; the list is whatever the backend
    ///     reports after the attempt.
    /// </returns>
    AudioDeviceSnapshot Refresh();
}

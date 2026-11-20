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
///     Lists the machine's audio devices. A machine without audio hardware, or without a working audio backend, is
///     an ordinary state that is reported in the result, never thrown.
/// </summary>
public interface IAudioDeviceService
{
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

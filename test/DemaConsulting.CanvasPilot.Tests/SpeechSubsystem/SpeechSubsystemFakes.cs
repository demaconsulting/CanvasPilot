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
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A progress sink that runs its handler at once on the reporting thread, so tests see each report without
///     waiting for a synchronization context.
/// </summary>
/// <typeparam name="T">The report type.</typeparam>
/// <param name="handler">The handler.</param>
internal sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    /// <inheritdoc />
    public void Report(T value)
    {
        handler(value);
    }
}

/// <summary>
///     Builders for the speech library values and substitutes that the Speech tests use.
/// </summary>
internal static class SpeechTestData
{
    /// <summary>
    ///     Creates a progress factory that runs handlers inline.
    /// </summary>
    public static readonly Func<Action<SpeechModelDownloadProgress>, IProgress<SpeechModelDownloadProgress>> Inline =
        handler => new InlineProgress<SpeechModelDownloadProgress>(handler);

    /// <summary>
    ///     Creates a substitute speech model.
    /// </summary>
    /// <param name="id">The model identifier.</param>
    /// <param name="role">The model's role.</param>
    /// <param name="parameters">The tunable parameters the model declares.</param>
    /// <returns>The model.</returns>
    public static ISpeechModel Model(string id, SpeechModelRole role, params ISpeechModelParameter[] parameters)
    {
        var model = Substitute.For<ISpeechModel>();
        model.Id.Returns(id);
        model.DisplayName.Returns($"Model {id}");
        model.Role.Returns(role);
        model.Parameters.Returns(parameters);
        return model;
    }

    /// <summary>
    ///     Creates a catalog descriptor for a new substitute model.
    /// </summary>
    /// <param name="id">The model identifier.</param>
    /// <param name="role">The model's role.</param>
    /// <param name="state">The install state.</param>
    /// <param name="parameters">The tunable parameters the model declares.</param>
    /// <returns>The descriptor.</returns>
    public static SpeechModelDescriptor Descriptor(
        string id,
        SpeechModelRole role,
        SpeechModelState state = SpeechModelState.NotDownloaded,
        params ISpeechModelParameter[] parameters)
    {
        return new SpeechModelDescriptor(Model(id, role, parameters), state);
    }

    /// <summary>
    ///     Creates a capture device description.
    /// </summary>
    /// <param name="name">The device name.</param>
    /// <returns>The description.</returns>
    public static AudioDeviceDescription Capture(string name)
    {
        return new AudioDeviceDescription(name, AudioDeviceDirection.Capture, 1, 16000);
    }

    /// <summary>
    ///     Creates a playback device description.
    /// </summary>
    /// <param name="name">The device name.</param>
    /// <returns>The description.</returns>
    public static AudioDeviceDescription Playback(string name)
    {
        return new AudioDeviceDescription(name, AudioDeviceDirection.Playback, 2, 48000);
    }

    /// <summary>
    ///     Creates a numeric parameter from 0 to 10 with a default of 5.
    /// </summary>
    /// <param name="id">The parameter identifier.</param>
    /// <param name="isInteger">Whether only whole numbers make sense.</param>
    /// <returns>The parameter.</returns>
    public static NumericParameter Numeric(string id = "speed", bool isInteger = false)
    {
        return new NumericParameter(id, "Speed", "How fast.", new NumericParameterBounds(0, 10, 1, 5), "x", isInteger);
    }

    /// <summary>
    ///     Creates a choice parameter with the options <c>a</c>, <c>b</c>, and <c>c</c> and the default <c>b</c>.
    /// </summary>
    /// <param name="id">The parameter identifier.</param>
    /// <returns>The parameter.</returns>
    public static ChoiceParameter Choice(string id = "voice")
    {
        return new ChoiceParameter(
            id,
            "Voice",
            "Which voice.",
            [new ChoiceParameterOption("a", "Anna"), new ChoiceParameterOption("b", "Ben"), new ChoiceParameterOption("c", "Cleo")],
            "b");
    }

    /// <summary>
    ///     Creates a boolean parameter.
    /// </summary>
    /// <param name="id">The parameter identifier.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The parameter.</returns>
    public static BooleanParameter Boolean(string id = "echo", bool defaultValue = false)
    {
        return new BooleanParameter(id, "Echo", "Add an echo.", defaultValue);
    }

    /// <summary>
    ///     Creates an audio device service substitute that reports the given devices.
    /// </summary>
    /// <param name="capture">The capture device names.</param>
    /// <param name="playback">The playback device names.</param>
    /// <returns>The substitute.</returns>
    public static IAudioDeviceService AudioDevices(string[] capture, string[] playback)
    {
        var snapshot = new AudioDeviceSnapshot(
            capture.Select(Capture).ToArray(),
            playback.Select(Playback).ToArray());
        var service = Substitute.For<IAudioDeviceService>();
        service.Enumerate().Returns(snapshot);
        service.Refresh().Returns(snapshot);
        return service;
    }

    /// <summary>
    ///     Creates a speech model service substitute that lists the given models.
    /// </summary>
    /// <param name="descriptors">The models to list.</param>
    /// <returns>The substitute.</returns>
    public static ISpeechModelService Models(params SpeechModelDescriptor[] descriptors)
    {
        var service = Substitute.For<ISpeechModelService>();
        service.Enumerate().Returns(new SpeechModelList(descriptors));
        return service;
    }
}

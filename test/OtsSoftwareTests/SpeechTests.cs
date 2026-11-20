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
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the DemaConsulting.Speech off-the-shelf software item (the
///     <c>DemaConsulting.Speech</c> library and its <c>DemaConsulting.Speech.Sherpa</c> model package). They
///     exercise the library directly with the same calls the application uses. They never download a model: the
///     model store is a temporary folder, and downloads are only requested in ways the library refuses before any
///     transfer. They accept whatever audio hardware the machine has, including none.
/// </summary>
public sealed class SpeechTests : IDisposable
{
    /// <summary>
    ///     Temporary folder that holds the model store, removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ots-speech-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the catalog filled with the Sherpa models lists speech-to-text and text-to-speech models with
    ///     identifiers, names, and an install state, from an empty store and without the network.
    /// </summary>
    [Fact]
    public void Speech_SherpaCatalog_Enumerate_ListsRecognitionAndSynthesisModels()
    {
        // Arrange: a catalog over an empty store, filled as the application fills it
        using var catalog = CreateCatalog();

        // Act: enumerate
        var models = catalog.Enumerate();

        // Assert: both roles are present, identifiers are unique, and nothing is installed
        Assert.Contains(models, model => model.Role == SpeechModelRole.Recognition);
        Assert.Contains(models, model => model.Role == SpeechModelRole.Synthesis);
        Assert.All(models, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Id));
            Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
            Assert.Equal(SpeechModelState.NotDownloaded, model.State);
        });
        Assert.Equal(models.Count, models.Select(model => model.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     Test that the Sherpa models declare their tunable parameters as typed descriptors with usable bounds,
    ///     defaults, and options.
    /// </summary>
    [Fact]
    public void Speech_SherpaCatalog_ModelParameters_AreTypedWithUsableBoundsAndDefaults()
    {
        // Arrange: the Sherpa models
        using var catalog = CreateCatalog();

        // Act: collect every declared parameter
        var parameters = catalog.Enumerate().SelectMany(model => model.Model.Parameters).ToArray();

        // Assert: at least one numeric and one choice parameter exist, and each is internally consistent
        Assert.Contains(parameters, parameter => parameter is NumericParameter);
        Assert.Contains(parameters, parameter => parameter is ChoiceParameter);
        foreach (var parameter in parameters)
        {
            Assert.False(string.IsNullOrWhiteSpace(parameter.Id));
            switch (parameter)
            {
                case NumericParameter numeric:
                    Assert.InRange(numeric.Default, numeric.Minimum, numeric.Maximum);
                    Assert.True(numeric.Step > 0);
                    break;
                case ChoiceParameter choice:
                    Assert.Contains(choice.Options, option => option.Value == choice.Default);
                    break;
                default:
                    Assert.IsType<BooleanParameter>(parameter);
                    break;
            }
        }
    }

    /// <summary>
    ///     Test that the catalog refuses a download of an unknown model, and honors a cancelled token for a known
    ///     model before anything is transferred, so downloads can be started and stopped by the application.
    /// </summary>
    [Fact]
    public async Task Speech_SherpaCatalog_Download_UnknownModelOrCancelledToken_IsRefusedWithoutTransfer()
    {
        // Arrange: a catalog over an empty store, a known model identifier, and a token that is already cancelled
        using var catalog = CreateCatalog();
        var known = catalog.Enumerate()[0].Id;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act: request the downloads
        var unknown = await Assert.ThrowsAsync<ArgumentException>(() => catalog.DownloadAsync("no-such-model", null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.DownloadAsync(known, null, cancelled.Token));

        // Assert: the unknown model was named in the refusal and the known model is still not downloaded
        Assert.Contains("no-such-model", unknown.Message, StringComparison.Ordinal);
        Assert.Equal(SpeechModelState.NotDownloaded, catalog.GetState(known));
    }

    /// <summary>
    ///     Test that listing capture and playback devices works on a machine with or without audio hardware: the
    ///     answer is a list, possibly empty, of named devices and never an exception.
    /// </summary>
    [Fact]
    public void Speech_AudioDeviceFactory_Enumerate_ReturnsNamedDevicesOrAnEmptyList()
    {
        // Arrange: the factory the application uses
        var factory = new AudioDeviceFactory();

        // Act: list both directions
        var capture = factory.CaptureProbe.Enumerate();
        var playback = factory.PlaybackProbe.Enumerate();

        // Assert: the lists exist and every device has a name and the right direction
        Assert.NotNull(capture);
        Assert.NotNull(playback);
        Assert.All(capture, device => Assert.False(string.IsNullOrWhiteSpace(device.Name)));
        Assert.All(capture, device => Assert.Equal(AudioDeviceDirection.Capture, device.Direction));
        Assert.All(playback, device => Assert.Equal(AudioDeviceDirection.Playback, device.Direction));
    }

    /// <summary>
    ///     Test that asking for the default capture and playback devices, and for a new scan of the hardware, does not
    ///     throw on a machine without audio hardware, and that an unusable device says so instead.
    /// </summary>
    [Fact]
    public void Speech_AudioDeviceFactory_DefaultDevicesAndRefresh_ToleratesMissingHardware()
    {
        // Arrange: the factory the application uses
        var factory = new AudioDeviceFactory();

        // Act: create the default devices and scan again with no stream open
        var capture = factory.CreateCaptureDevice(null);
        var playback = factory.CreatePlaybackDevice(null);
        var refresh = Record.Exception(factory.RefreshDevices);
        var afterRefresh = factory.CaptureProbe.Enumerate();

        // Assert: the devices exist and report their availability, the new scan worked, and listing still works
        Assert.NotNull(capture);
        Assert.NotNull(playback);
        Assert.Null(refresh);
        Assert.NotNull(afterRefresh);
        (capture as IDisposable)?.Dispose();
        (playback as IDisposable)?.Dispose();
    }

    /// <summary>
    ///     Creates a catalog over a store in the temporary folder, filled with the Sherpa models.
    /// </summary>
    /// <returns>The catalog.</returns>
    private SpeechModelCatalog CreateCatalog()
    {
        return new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "models") }).AddSherpaModels();
    }
}

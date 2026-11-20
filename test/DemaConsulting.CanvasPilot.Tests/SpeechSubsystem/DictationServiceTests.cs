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
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="DictationService" />. The model service, the audio service, and the speech engine
///     are substitutes, so no model, microphone, or native speech runtime is needed and the native runtime is never
///     loaded; the check for the native files is also a substitute except in the tests of
///     <see cref="DictationService.FindMissingNativeLibraries" />.
/// </summary>
public sealed class DictationServiceTests : IDisposable
{
    /// <summary>
    ///     The longest time a test waits for work on a background thread before it fails.
    /// </summary>
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Temporary folder for the log and for native file layouts, created fresh for each test and removed on
    ///     disposal.
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
    ///     Test that dictation is reported as available when a model is chosen and installed, the native files are
    ///     present, and a microphone exists.
    /// </summary>
    [Fact]
    public void DictationService_CheckAvailability_EverythingPresent_IsAvailable()
    {
        // Arrange: a machine with everything dictation needs
        var harness = new DictationHarness();
        using var service = harness.CreateService();

        // Act: check
        var availability = service.CheckAvailability();

        // Assert: available, with no message, and no engine was loaded just to find out
        Assert.True(availability.IsAvailable);
        Assert.Equal(DictationAvailabilityReason.Available, availability.Reason);
        Assert.Equal(string.Empty, availability.Message);
        Assert.Equal(0, harness.Loads);
    }

    /// <summary>
    ///     Test that every reason why dictation is unavailable is reported with its own reason and its own
    ///     non-empty message, and that none of them loads an engine.
    /// </summary>
    [Fact]
    public void DictationService_CheckAvailability_EachProblem_HasItsOwnReasonAndMessage()
    {
        // Arrange: one harness per problem
        var noModel = new DictationHarness();
        noModel.Settings.Current = new AppSettings();
        var notInstalled = new DictationHarness();
        notInstalled.ListInstalled(SpeechModelState.NotDownloaded);
        var noRuntime = new DictationHarness { MissingRuntimeFiles = ["onnxruntime.dll", "sherpa-onnx-c-api.dll"] };
        var noMicrophone = new DictationHarness();
        noMicrophone.Audio.Enumerate().Returns(new AudioDeviceSnapshot([], []));
        var harnesses = new[] { noModel, notInstalled, noRuntime, noMicrophone };

        // Act: check each
        var results = harnesses.Select(harness =>
        {
            using var service = harness.CreateService();
            return service.CheckAvailability();
        }).ToArray();

        // Assert: the four reasons are the expected ones, the messages are distinct and name the next step, and
        // no engine was loaded
        Assert.Equal(
            [
                DictationAvailabilityReason.NoModelChosen,
                DictationAvailabilityReason.ModelNotInstalled,
                DictationAvailabilityReason.SpeechRuntimeMissing,
                DictationAvailabilityReason.NoCaptureDevice,
            ],
            results.Select(result => result.Reason));
        Assert.All(results, result => Assert.False(result.IsAvailable));
        Assert.Equal(4, results.Select(result => result.Message).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("Speech page", results[0].Message, StringComparison.Ordinal);
        Assert.Contains("Model stt-one", results[1].Message, StringComparison.Ordinal);
        Assert.Contains("onnxruntime.dll", results[2].Message, StringComparison.Ordinal);
        Assert.Contains("microphone", results[3].Message, StringComparison.Ordinal);
        Assert.All(harnesses, harness => Assert.Equal(0, harness.Loads));
    }

    /// <summary>
    ///     Test that a model of the wrong kind, an unknown model, and an unreadable model catalog are all reported as
    ///     a model that is not installed, with the catalog's explanation when there is one.
    /// </summary>
    [Fact]
    public void DictationService_CheckAvailability_WrongOrUnknownOrUnreadableModel_ReportsModelNotInstalled()
    {
        // Arrange: a synthesis model chosen as the speech-to-text model, an unknown model, and an unreadable catalog
        var wrongKind = new DictationHarness();
        wrongKind.ListInstalled(role: SpeechModelRole.Synthesis);
        var unknown = new DictationHarness();
        unknown.Settings.Current = new AppSettings { Speech = new SpeechSettings { SttModelId = "other" } };
        var unreadable = new DictationHarness();
        unreadable.Models.Enumerate().Returns(SpeechModelList.Unavailable("The drive is not ready."));

        // Act: check each
        var results = new[] { wrongKind, unknown, unreadable }.Select(harness =>
        {
            using var service = harness.CreateService();
            return service.CheckAvailability();
        }).ToArray();

        // Assert: all three are a model that is not installed, and the last one carries the explanation
        Assert.All(results, result => Assert.Equal(DictationAvailabilityReason.ModelNotInstalled, result.Reason));
        Assert.Contains("The drive is not ready.", results[2].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an audio system that cannot be used is reported as a missing microphone with the audio system's
    ///     explanation.
    /// </summary>
    [Fact]
    public void DictationService_CheckAvailability_AudioSystemUnusable_ReportsNoCaptureDeviceWithProblem()
    {
        // Arrange: an audio system that failed
        var harness = new DictationHarness();
        harness.Audio.Enumerate().Returns(AudioDeviceSnapshot.Unavailable("The audio library is missing."));
        using var service = harness.CreateService();

        // Act: check
        var availability = service.CheckAvailability();

        // Assert: no microphone, with the explanation
        Assert.Equal(DictationAvailabilityReason.NoCaptureDevice, availability.Reason);
        Assert.Contains("The audio library is missing.", availability.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a failure inside a service that should have answered with a result is still an explanation and
    ///     not an exception.
    /// </summary>
    [Fact]
    public void DictationService_CheckAvailability_ServiceThrows_ReturnsExplanationAndLogsIt()
    {
        // Arrange: a model service that throws, and a log
        var harness = new DictationHarness();
        harness.Models.Enumerate().Returns(_ => throw new InvalidOperationException("catalog defect"));
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        using var service = harness.CreateService(logger);

        // Act: check
        var availability = service.CheckAvailability();

        // Assert: an unavailable result, and the defect is in the log
        Assert.False(availability.IsAvailable);
        Assert.Contains("catalog defect", File.ReadAllText(logger.ActiveFilePath), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that missing native files stop dictation from loading an engine, in a begin as well as in a
    ///     preparation, because loading the engine without them can crash the process.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAndPrepare_NativeFilesMissing_NeverLoadEngine()
    {
        // Arrange: the native files are missing
        var harness = new DictationHarness { MissingRuntimeFiles = ["onnxruntime.dll"] };
        using var service = harness.CreateService();

        // Act: prepare and begin
        var prepared = await service.PrepareAsync(TestContext.Current.CancellationToken);
        var started = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: both say the runtime is missing, and no engine was ever requested
        Assert.Equal(DictationAvailabilityReason.SpeechRuntimeMissing, prepared.Reason);
        Assert.Null(started.Session);
        Assert.Equal(DictationAvailabilityReason.SpeechRuntimeMissing, started.Availability.Reason);
        Assert.Equal(0, harness.Loads);
    }

    /// <summary>
    ///     Test that provisional and final results update the partial text as the accumulated finished sentences
    ///     followed by the sentence being recognized, and raise the change event only when the text changes.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_ProvisionalThenFinalResults_UpdatesPartialText()
    {
        // Arrange: a listening session with a counter on the change event
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        var start = await service.BeginAsync(TestContext.Current.CancellationToken);
        var session = start.Session!;
        var changes = 0;
        session.PartialTextChanged += (_, _) => Interlocked.Increment(ref changes);
        var fake = harness.Engines[0].Sessions[0];

        // Act: the recognizer streams a sentence, finishes it, and starts the next one
        fake.Emit("make the box", isFinal: false);
        await EventuallyAsync(() => session.PartialText == "make the box");
        fake.Emit("make the box blue", isFinal: false);
        await EventuallyAsync(() => session.PartialText == "make the box blue");
        fake.Emit("Make the box blue.", isFinal: true);
        await EventuallyAsync(() => session.PartialText == "Make the box blue.");
        fake.Emit("and move", isFinal: false);
        await EventuallyAsync(() => session.PartialText == "Make the box blue. and move");
        fake.Emit("and move", isFinal: false);
        fake.Emit("and move it", isFinal: false);
        await EventuallyAsync(() => session.PartialText == "Make the box blue. and move it");

        // Assert: one change per distinct text, and the repeated provisional result raised none
        Assert.True(start.IsListening);
        Assert.Equal(5, Volatile.Read(ref changes));
        await session.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that ending a session returns the finished sentences joined by single spaces and trimmed, including
    ///     the sentence that the recognizer finishes while it drains the audio after the release.
    /// </summary>
    [Fact]
    public async Task DictationService_EndAsync_FinalResultsIncludingDrained_ReturnsTrimmedJoinedText()
    {
        // Arrange: a recognizer that finishes one more sentence when it is stopped
        var harness = new DictationHarness
        {
            ConfigureEngine = engine => engine.ConfigureSession = session =>
                session.ResultsOnStop.Add(new SpeechRecognitionResult("  Move the label.  ", IsFinal: true)),
        };
        using var service = harness.CreateService();
        var session = (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!;
        var fake = harness.Engines[0].Sessions[0];
        fake.Emit("  Make the box blue.", isFinal: true);
        fake.Emit("   ", isFinal: true);
        await EventuallyAsync(() => session.PartialText == "Make the box blue.");

        // Act: the user lets go
        var text = await session.EndAsync(TestContext.Current.CancellationToken);

        // Assert: the sentences are joined by one space, the empty one was dropped, and nothing is padded
        Assert.Equal("Make the box blue. Move the label.", text);
        Assert.Equal(string.Empty, session.Problem);
    }

    /// <summary>
    ///     Test that a sentence that was still provisional when the recognizer stopped is kept rather than lost.
    /// </summary>
    [Fact]
    public async Task DictationService_EndAsync_SentenceNeverFinished_KeepsItsLastProvisionalText()
    {
        // Arrange: a session that only produced a provisional result
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        var session = (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!;
        harness.Engines[0].Sessions[0].Emit("hello there", isFinal: false);
        await EventuallyAsync(() => session.PartialText == "hello there");

        // Act: the user lets go
        var text = await session.EndAsync(TestContext.Current.CancellationToken);

        // Assert: the words the user saw are the words they get
        Assert.Equal("hello there", text);
    }

    /// <summary>
    ///     Test that a session in which nothing was said ends with an empty string.
    /// </summary>
    [Fact]
    public async Task DictationService_EndAsync_NothingSaid_ReturnsEmptyString()
    {
        // Arrange: a session without results
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        var session = (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!;

        // Act: the user lets go at once
        var text = await session.EndAsync(TestContext.Current.CancellationToken);

        // Assert: nothing was recognized
        Assert.Equal(string.Empty, text);
        Assert.Equal(string.Empty, session.PartialText);
    }

    /// <summary>
    ///     Test that ending a session stops and disposes the recognition session, releases the microphone, and
    ///     lets the next hold start, and that ending twice returns the same text and stops once.
    /// </summary>
    [Fact]
    public async Task DictationService_EndAsync_ReleasesSessionAndMicrophone_AllowsNextBegin()
    {
        // Arrange: a listening session with one result
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        var session = (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!;
        var fake = harness.Engines[0].Sessions[0];
        fake.Emit("one", isFinal: true);
        await EventuallyAsync(() => session.PartialText == "one");

        // Act: end it twice, then begin again
        var first = await session.EndAsync(TestContext.Current.CancellationToken);
        var second = await session.EndAsync(TestContext.Current.CancellationToken);
        var next = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: one stop, one disposal, the microphone released, the same text twice, and a new session started
        Assert.Equal("one", first);
        Assert.Equal("one", second);
        Assert.Equal(1, fake.Starts);
        Assert.Equal(1, fake.Stops);
        Assert.Equal(1, fake.Disposals);
        Assert.True(harness.Device.Disposed);
        Assert.True(next.IsListening);
        Assert.Equal(2, harness.Engines[0].Sessions.Count);
        await next.Session!.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that a request to start while a session is listening is ignored: no second session, no second
    ///     microphone, and no problem reported.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_WhileListening_IsIgnored()
    {
        // Arrange: a listening session
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        var first = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Act: a second request, as a key repeat would make
        var second = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: it was ignored without a problem, and only one session and microphone exist
        Assert.True(first.IsListening);
        Assert.Null(second.Session);
        Assert.True(second.Availability.IsAvailable);
        Assert.Single(harness.Engines[0].Sessions);
        Assert.Single(harness.Requested);
        await first.Session!.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that the engine is loaded once and reused by every later hold.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_SeveralHolds_ReuseTheLoadedEngine()
    {
        // Arrange: a service
        var harness = new DictationHarness();
        using var service = harness.CreateService();

        // Act: three holds
        for (var hold = 0; hold < 3; hold++)
        {
            var start = await service.BeginAsync(TestContext.Current.CancellationToken);
            await start.Session!.EndAsync(TestContext.Current.CancellationToken);
        }

        // Assert: one engine served all three sessions
        Assert.Equal(1, harness.Loads);
        Assert.Equal(3, harness.Engines[0].Sessions.Count);
        Assert.False(harness.Engines[0].Disposed);
    }

    /// <summary>
    ///     Test that a preparation loads the engine ahead of time and the first hold then loads nothing.
    /// </summary>
    [Fact]
    public async Task DictationService_PrepareAsync_Available_LoadsEngineOnceForLaterHolds()
    {
        // Arrange: a service
        var harness = new DictationHarness();
        using var service = harness.CreateService();

        // Act: prepare twice and begin
        var first = await service.PrepareAsync(TestContext.Current.CancellationToken);
        var second = await service.PrepareAsync(TestContext.Current.CancellationToken);
        var start = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: available, and one load served everything
        Assert.True(first.IsAvailable);
        Assert.True(second.IsAvailable);
        Assert.Equal(1, harness.Loads);
        Assert.True(start.IsListening);
        await start.Session!.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that a preparation that cannot work says why without loading anything and without throwing, and that
    ///     a load that throws is logged and returned as an explanation.
    /// </summary>
    [Fact]
    public async Task DictationService_PrepareAsync_FailureOrUnavailable_IsReturnedAndLoggedNotThrown()
    {
        // Arrange: one service without a model chosen, and one whose model service throws on load
        var noModel = new DictationHarness();
        noModel.Settings.Current = new AppSettings();
        using var noModelService = noModel.CreateService();
        var failing = new DictationHarness();
        failing.Models
            .LoadRecognizerAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns<Task<RecognizerLoadResult>>(_ => throw new InvalidOperationException("native defect"));
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        using var failingService = failing.CreateService(logger);

        // Act: prepare both
        var withoutModel = await noModelService.PrepareAsync(TestContext.Current.CancellationToken);
        var withDefect = await failingService.PrepareAsync(TestContext.Current.CancellationToken);

        // Assert: both are explanations, nothing was loaded for the first, and the defect is in the log
        Assert.Equal(DictationAvailabilityReason.NoModelChosen, withoutModel.Reason);
        Assert.Equal(0, noModel.Loads);
        Assert.Equal(DictationAvailabilityReason.EngineUnavailable, withDefect.Reason);
        Assert.Contains("native defect", await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a changed model or changed parameters replace the engine, and the old engine is disposed.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_SettingsChanged_ReloadsEngineAndDisposesOldOne()
    {
        // Arrange: a first hold on the first engine
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        await (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!.EndAsync(TestContext.Current.CancellationToken);

        // Act: the user changes a model parameter on the Speech page, and holds again
        harness.Settings.Current = new AppSettings
        {
            Speech = new SpeechSettings
            {
                SttModelId = "stt-one",
                CaptureDevice = "Mic A",
                ModelParameters = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["stt-one"] = new Dictionary<string, string> { ["beam"] = "4" },
                },
            },
        };
        var second = await service.BeginAsync(TestContext.Current.CancellationToken);
        await second.Session!.EndAsync(TestContext.Current.CancellationToken);

        // Assert: a second engine was loaded with the stored parameters, and the first was disposed
        Assert.Equal(2, harness.Loads);
        Assert.True(harness.Engines[0].Disposed);
        Assert.False(harness.Engines[1].Disposed);
        Assert.Equal("4", harness.LastParameters!["beam"]);
    }

    /// <summary>
    ///     Test that a preparation while a session is listening does not replace the engine that the session is
    ///     using.
    /// </summary>
    [Fact]
    public async Task DictationService_PrepareAsync_WhileListeningWithChangedSettings_KeepsTheEngineInUse()
    {
        // Arrange: a listening session
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        var start = await service.BeginAsync(TestContext.Current.CancellationToken);
        harness.Settings.Current = new AppSettings
        {
            Speech = new SpeechSettings
            {
                SttModelId = "stt-one",
                ModelParameters = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["stt-one"] = new Dictionary<string, string> { ["beam"] = "8" },
                },
            },
        };

        // Act: the settings change triggers a preparation while the user is still holding
        var prepared = await service.PrepareAsync(TestContext.Current.CancellationToken);

        // Assert: nothing was replaced or disposed while the session used the engine
        Assert.True(prepared.IsAvailable);
        Assert.Equal(1, harness.Loads);
        Assert.False(harness.Engines[0].Disposed);
        await start.Session!.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that an engine that cannot be loaded, because the model is gone or the engine will not start, is
    ///     reported with the model service's explanation and the next press tries again.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_EngineLoadFails_ReportsReasonAndTriesAgainNextTime()
    {
        // Arrange: a model service that fails to load the engine
        var harness = new DictationHarness
        {
            LoadFailure = RecognizerLoadResult.Failure(RecognizerLoadStatus.EngineUnavailable, "The runtime failed to initialize."),
        };
        using var service = harness.CreateService();

        // Act: begin, then begin again after the cause is fixed
        var failed = await service.BeginAsync(TestContext.Current.CancellationToken);
        harness.LoadFailure = null;
        var retried = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: the first said why, and the second loaded an engine and listens
        Assert.Null(failed.Session);
        Assert.Equal(DictationAvailabilityReason.EngineUnavailable, failed.Availability.Reason);
        Assert.Contains("The runtime failed to initialize.", failed.Availability.Message, StringComparison.Ordinal);
        Assert.True(retried.IsListening);
        Assert.Equal(2, harness.Loads);
        await retried.Session!.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that a model that disappeared between the check and the load is reported as not installed.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_ModelDisappeared_ReportsModelNotInstalled()
    {
        // Arrange: a load that reports the model as not installed
        var harness = new DictationHarness
        {
            LoadFailure = RecognizerLoadResult.Failure(RecognizerLoadStatus.ModelNotInstalled, "The speech model 'Model stt-one' is not downloaded."),
        };
        using var service = harness.CreateService();

        // Act: begin
        var start = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: the reason is the model, and the message says where to fix it
        Assert.Equal(DictationAvailabilityReason.ModelNotInstalled, start.Availability.Reason);
        Assert.Contains("Speech page", start.Availability.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a microphone that cannot be created is reported, keeps the loaded engine, and leaves the next
    ///     press able to start once a microphone is there.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_NoMicrophone_ReportsNoCaptureDeviceAndKeepsEngine()
    {
        // Arrange: an audio system without a usable microphone
        var harness = new DictationHarness { DeviceResult = CaptureDeviceResult.Unavailable("Windows blocked the microphone.") };
        using var service = harness.CreateService();

        // Act: begin, then begin again after the microphone works
        var blocked = await service.BeginAsync(TestContext.Current.CancellationToken);
        harness.DeviceResult = null;
        var retried = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: the first named the microphone, and the second reused the engine
        Assert.Null(blocked.Session);
        Assert.Equal(DictationAvailabilityReason.NoCaptureDevice, blocked.Availability.Reason);
        Assert.Contains("Windows blocked the microphone.", blocked.Availability.Message, StringComparison.Ordinal);
        Assert.True(retried.IsListening);
        Assert.Equal(1, harness.Loads);
        await retried.Session!.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that failures while creating or starting the recognition session end as a reason, release the
    ///     microphone, and leave the next press able to start.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_SessionCannotStart_ReportsReasonReleasesMicrophoneAndRecovers()
    {
        // Arrange: first a busy engine, then a microphone in use, then an unexpected native failure
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        await (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!.EndAsync(TestContext.Current.CancellationToken);
        var engine = harness.Engines[0];

        // Act: each failure in turn
        engine.CreateFailure = new RecognitionEngineBusyException();
        var busy = await service.BeginAsync(TestContext.Current.CancellationToken);
        engine.CreateFailure = null;
        engine.ConfigureSession = session => session.StartFailure = new AudioDeviceInUseException("Another program uses the microphone.");
        var inUse = await service.BeginAsync(TestContext.Current.CancellationToken);
        engine.ConfigureSession = session => session.StartFailure = new InvalidOperationException("native failure");
        var native = await service.BeginAsync(TestContext.Current.CancellationToken);
        engine.ConfigureSession = null;
        var recovered = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: each had the right reason, every failed session was disposed, and the last press worked
        Assert.Equal(DictationAvailabilityReason.EngineUnavailable, busy.Availability.Reason);
        Assert.Equal(DictationAvailabilityReason.NoCaptureDevice, inUse.Availability.Reason);
        Assert.Contains("Another program uses the microphone.", inUse.Availability.Message, StringComparison.Ordinal);
        Assert.Equal(DictationAvailabilityReason.EngineUnavailable, native.Availability.Reason);
        Assert.Contains("native failure", native.Availability.Message, StringComparison.Ordinal);
        Assert.All(engine.Sessions.Skip(1).Take(2), session => Assert.Equal(1, session.Disposals));
        Assert.True(recovered.IsListening);
        await recovered.Session!.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that an engine or a session that says it is unavailable is reported, not used.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_SessionUnavailable_ReportsEngineUnavailable()
    {
        // Arrange: an engine whose sessions are unusable
        var harness = new DictationHarness
        {
            ConfigureEngine = engine => engine.ConfigureSession = session => session.IsAvailable = false,
        };
        using var service = harness.CreateService();

        // Act: begin
        var start = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: reported, the session was released, and nothing started
        Assert.Null(start.Session);
        Assert.Equal(DictationAvailabilityReason.EngineUnavailable, start.Availability.Reason);
        Assert.Equal(1, harness.Engines[0].Sessions[0].Disposals);
        Assert.Equal(0, harness.Engines[0].Sessions[0].Starts);
    }

    /// <summary>
    ///     Test that a cancelled token ends the start with a cancellation and gives the listening slot back.
    /// </summary>
    [Fact]
    public async Task DictationService_BeginAsync_Cancelled_ThrowsAndLeavesNextBeginPossible()
    {
        // Arrange: a token that is already cancelled, and a token that is cancelled while the engine loads
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        using var duringLoad = new CancellationTokenSource();
        harness.ConfigureEngine = _ => duringLoad.Cancel();

        // Act: begin with each token, then begin normally
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.BeginAsync(cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.BeginAsync(duringLoad.Token));
        harness.ConfigureEngine = null;
        var start = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: the normal start worked, so the slot was given back both times
        Assert.True(start.IsListening);
        await start.Session!.EndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that a failure of the recognizer while listening is kept as the session's problem and does not lose
    ///     the text that was recognized before it.
    /// </summary>
    [Fact]
    public async Task DictationService_EndAsync_RecognizerFaulted_KeepsTextAndReportsProblem()
    {
        // Arrange: a session that recognizes a sentence and then faults
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        var session = (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!;
        var fake = harness.Engines[0].Sessions[0];
        fake.Emit("before the fault", isFinal: true);
        await EventuallyAsync(() => session.PartialText == "before the fault");
        fake.Fault(new InvalidOperationException("recognizer broke"));
        await EventuallyAsync(() => session.Problem.Length > 0);

        // Act: the user lets go
        var text = await session.EndAsync(TestContext.Current.CancellationToken);

        // Assert: the text survived and the problem names the cause
        Assert.Equal("before the fault", text);
        Assert.Contains("recognizer broke", session.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a handler of the text change event that throws does not stop the recognition.
    /// </summary>
    [Fact]
    public async Task DictationService_PartialTextChanged_HandlerThrows_RecognitionContinues()
    {
        // Arrange: a handler that always throws
        var harness = new DictationHarness();
        using var service = harness.CreateService();
        var session = (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!;
        session.PartialTextChanged += (_, _) => throw new InvalidOperationException("handler defect");
        var fake = harness.Engines[0].Sessions[0];

        // Act: two results
        fake.Emit("first", isFinal: true);
        await EventuallyAsync(() => session.PartialText == "first");
        fake.Emit("second", isFinal: true);
        await EventuallyAsync(() => session.PartialText == "first second");
        var text = await session.EndAsync(TestContext.Current.CancellationToken);

        // Assert: both results arrived, and the handler's failure is the problem
        Assert.Equal("first second", text);
        Assert.Contains("handler defect", session.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that disposing a session without ending it still stops the recognition and releases the microphone,
    ///     and that the text is never written to the log.
    /// </summary>
    [Fact]
    public async Task DictationService_DisposeSession_WithoutEnd_ReleasesEverythingAndLogsNoText()
    {
        // Arrange: a listening session that recognized a secret sentence, and a log
        var harness = new DictationHarness();
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        using var service = harness.CreateService(logger);
        var session = (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!;
        var fake = harness.Engines[0].Sessions[0];
        fake.Emit("the launch code is swordfish", isFinal: true);
        await EventuallyAsync(() => session.PartialText.Length > 0);

        // Act: dispose without ending
        await session.DisposeAsync();

        // Assert: everything was released, and the log has the events but not the words
        Assert.Equal(1, fake.Stops);
        Assert.Equal(1, fake.Disposals);
        Assert.True(harness.Device.Disposed);
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("Dictation started.", log, StringComparison.Ordinal);
        Assert.Contains("Dictation ended.", log, StringComparison.Ordinal);
        Assert.DoesNotContain("swordfish", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that disposing the service ends a session that is still listening, disposes the engine, and refuses
    ///     later requests with an explanation.
    /// </summary>
    [Fact]
    public async Task DictationService_Dispose_WhileListening_EndsSessionAndReleasesEngine()
    {
        // Arrange: a listening session
        var harness = new DictationHarness();
        var service = harness.CreateService();
        var start = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Act: dispose the service twice, and then ask to begin
        service.Dispose();
        service.Dispose();
        var afterwards = await service.BeginAsync(TestContext.Current.CancellationToken);

        // Assert: the session was stopped and the engine freed, and the late request was refused
        Assert.Equal(1, harness.Engines[0].Sessions[0].Stops);
        Assert.True(harness.Engines[0].Disposed);
        Assert.True(harness.Device.Disposed);
        Assert.Null(afterwards.Session);
        Assert.Equal(DictationAvailabilityReason.EngineUnavailable, afterwards.Availability.Reason);
        Assert.NotNull(start.Session);
    }

    /// <summary>
    ///     Test that the microphone keeps listening for the release tail after the end is requested, so that the
    ///     last word, which is still on its way when the user lets go, is part of the text.
    /// </summary>
    [Fact]
    public async Task DictationService_EndAsync_WordsArriveDuringReleaseTail_AreKept()
    {
        // Arrange: a session over a service with a one second release tail, and one sentence before the release
        var harness = new DictationHarness();
        using var service = harness.CreateService(releaseTail: TimeSpan.FromSeconds(1));
        var session = (await service.BeginAsync(TestContext.Current.CancellationToken)).Session!;
        var fake = harness.Engines[0].Sessions[0];
        fake.Emit("make it blue", isFinal: true);
        await EventuallyAsync(() => session.PartialText == "make it blue");

        // Act: the user lets go, and the last word arrives while the microphone is still listening
        var ending = session.EndAsync(TestContext.Current.CancellationToken);
        var stopsWhenReleased = fake.Stops;
        fake.Emit("please", isFinal: true);
        var text = await ending;

        // Assert: the recognizer had not been stopped at the release, and the late word is in the text
        Assert.Equal(0, stopsWhenReleased);
        Assert.Equal("make it blue please", text);
        Assert.Equal(1, fake.Stops);
    }

    /// <summary>
    ///     Test that missing constructor arguments are refused.
    /// </summary>
    [Fact]
    public void DictationService_InvalidArguments_Throw()
    {
        // Arrange: valid substitutes
        var harness = new DictationHarness();

        // Act / Assert: each missing argument is refused
        Assert.Throws<ArgumentNullException>(() => new DictationService(null!, harness.Models, harness.Audio));
        Assert.Throws<ArgumentNullException>(() => new DictationService(harness.Settings, null!, harness.Audio));
        Assert.Throws<ArgumentNullException>(() => new DictationService(harness.Settings, harness.Models, null!));
        Assert.Throws<ArgumentException>(() => DictationService.FindMissingNativeLibraries(" "));
    }

    /// <summary>
    ///     Test that the check of the native files names both required libraries when the folder has neither, and
    ///     only the missing one when it has one.
    /// </summary>
    [Fact]
    public void DictationService_FindMissingNativeLibraries_EmptyOrPartialFolder_NamesWhatIsMissing()
    {
        // Arrange: an empty application folder, then one with only the inference runtime
        var folder = Directory.CreateDirectory(Path.Combine(_tempRoot, "app")).FullName;
        var onnx = DictationService.NativeFileName("onnxruntime");
        var sherpa = DictationService.NativeFileName("sherpa-onnx-c-api");

        // Act: check, add the file, check again
        var empty = DictationService.FindMissingNativeLibraries(folder);
        File.WriteAllBytes(Path.Combine(folder, onnx), [0]);
        var partial = DictationService.FindMissingNativeLibraries(folder);

        // Assert: both were missing at first, and then only the Sherpa interface
        Assert.Equal([onnx, sherpa], empty);
        Assert.Equal([sherpa], partial);
    }

    /// <summary>
    ///     Test that the native files count as present both beside the application, as in the published build, and
    ///     in the runtime-specific folder, as in a development build.
    /// </summary>
    [Fact]
    public void DictationService_FindMissingNativeLibraries_PublishedAndDevelopmentLayouts_AreBothPresent()
    {
        // Arrange: one folder with the files beside it, and one with them in the runtimes folder
        var published = Directory.CreateDirectory(Path.Combine(_tempRoot, "published")).FullName;
        var development = Directory.CreateDirectory(Path.Combine(_tempRoot, "development")).FullName;
        string system;
        if (OperatingSystem.IsWindows())
        {
            system = "win";
        }
        else
        {
            system = OperatingSystem.IsMacOS() ? "osx" : "linux";
        }

        var architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var runtimeFolder = Directory.CreateDirectory(Path.Combine(development, "runtimes", $"{system}-{architecture}", "native")).FullName;
        foreach (var library in new[] { "onnxruntime", "sherpa-onnx-c-api" })
        {
            File.WriteAllBytes(Path.Combine(published, DictationService.NativeFileName(library)), [0]);
            File.WriteAllBytes(Path.Combine(runtimeFolder, DictationService.NativeFileName(library)), [0]);
        }

        // Act: check both
        var beside = DictationService.FindMissingNativeLibraries(published);
        var below = DictationService.FindMissingNativeLibraries(development);

        // Assert: nothing is missing in either layout
        Assert.Empty(beside);
        Assert.Empty(below);
    }

    /// <summary>
    ///     Waits until a condition that depends on a background thread holds.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    private static async Task EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The condition did not hold within the time limit.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}

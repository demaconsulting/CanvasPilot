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

using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.AgentKit.Agents.Copilot;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Live tests of long turns of GitHub Copilot through the composed application: the real agent service, the real
///     Copilot CLI with the sign-in of the machine, and the real renderer and preview. They check what a person sees
///     during a long drawing turn (live status), that Stop leaves the conversation usable, and that consecutive turns share
///     one provider session. They are skipped unless <c>CANVASPILOT_LIVE_COPILOT=1</c> is set, so that no build needs a
///     Copilot CLI, a sign-in, the network, or premium requests. The model is <c>CANVASPILOT_LIVE_COPILOT_MODEL</c>
///     (default <c>claude-opus-5.5</c>) at the reasoning level <c>CANVASPILOT_LIVE_COPILOT_EFFORT</c> (default
///     <c>high</c>); a full run costs several dozen premium requests on such a model, so a cheaper model may be named for
///     routine runs. Each test records its measurements (timings, counts of what was observed by kind, the longest gap
///     between observations, the tools, the session identifiers, and the usage) as a diagnostic message and, when
///     <c>CANVASPILOT_LIVE_COPILOT_OUT</c> names a file, appends them there. Nothing that is recorded holds a token or the
///     text of a message. The tests belong to no requirement; the manual verification of the system records their results.
/// </summary>
public sealed class LiveCopilotLongTurnTests
{
    /// <summary>The longest time that one turn may take.</summary>
    private static readonly TimeSpan TurnLimit = TimeSpan.FromMinutes(10);

    /// <summary>The longest time that a whole test may take, in milliseconds, which includes several turns.</summary>
    private const int TestTimeoutMilliseconds = 30 * 60 * 1000;

    /// <summary>The marketing request of the first and second test.</summary>
    private const string MarketingRequest =
        "Please make a marketing image for a wind-up toy called Crazy Monkey Robot, 1200 by 900 pixels, as an SVG file named "
        + "crazy-monkey-robot.svg. It should be playful and colorful: a grinning robot monkey with gears, antennae and a wind-up key, "
        + "the name as a bold headline, a short tagline, and a round badge that says Wind me up!";

    /// <summary>
    ///     Test the experience of a long first turn in the composed application with an empty project: a person asks for a
    ///     realistic marketing image and the status of the turn keeps changing while it runs (the elapsed time and the phase,
    ///     whatever the model buffers), the drawing is created in the project, it renders, the preview shows it, and the
    ///     turn shows what it cost.
    /// </summary>
    [Fact(Timeout = TestTimeoutMilliseconds)]
    public async Task LiveCopilotLongTurn_EmptyProject_MarketingImage_ShowsProgressCreatesFileRendersAndPreviews()
    {
        // Arrange: skipped unless asked for; the application over an empty project with the model and level of the record
        SkipUnlessLive();
        var token = TestContext.Current.CancellationToken;
        await using var rig = await LiveRig.CreateAsync(token);
        var report = new StringBuilder().AppendLine($"== Scenario (a): empty project, marketing image ({rig.Header})");
        try
        {
            await rig.AssertReadyAsync();

            // Act: the user asks for the image and waits for the end of the turn; then the preview is waited for
            var window = await rig.RunTurnAsync(MarketingRequest, TurnLimit, token);
            await rig.Composition.Preview.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(60), token);
            report.Append(rig.Summarize("Turn 1", window));

            // Record what the project and the preview hold
            var drawing = Path.Combine(rig.ProjectFolder, "crazy-monkey-robot.svg");
            var preview = rig.Composition.Preview;
            var size = File.Exists(drawing) ? new FileInfo(drawing).Length : -1;
            report.AppendLine($"Drawing: exists={File.Exists(drawing)} bytes={size}; project files=[{string.Join(", ", rig.ProjectFiles())}]");
            report.AppendLine($"Explorer selection: {Path.GetFileName(rig.Composition.Shell.Workspace.Explorer.SelectedFile)}");
            report.AppendLine($"Preview: hasImage={preview.HasImage} hasError={preview.HasError} size={preview.ImageSizeText} file={preview.FileName} {preview.RenderTimeText}");
            report.Append(rig.RuntimeEvents("Runtime events"));
            var sum = window.Sum;

            // Assert: the turn completed without a failure
            Assert.Equal("Completed", rig.OutcomeOf(1));
            Assert.Empty(rig.ErrorNotices());

            // Assert: the status changed at least every 5 s while the turn ran, which the timer guarantees for every model
            Assert.True(sum.MaxGapMilliseconds <= 5000, $"The status was silent for {sum.MaxGapMilliseconds} ms.{Environment.NewLine}{report}");

            // Assert: the drawing was created, it holds a picture, the explorer selected it, and the preview shows it
            Assert.True(size > 0, report.ToString());
            Assert.Contains("<svg", await File.ReadAllTextAsync(drawing, token), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(drawing, rig.Composition.Shell.Workspace.Explorer.SelectedFile, ignoreCase: true);
            Assert.True(preview.HasImage, report.ToString());
            Assert.False(preview.HasError, report.ToString());
            Assert.True(preview.Image!.Width > 0 && preview.Image.Height > 0, report.ToString());

            // Assert: a tool wrote the drawing, an answer ended the conversation, and the turn says what it cost
            Assert.Contains(rig.Chat.Transcript, row => row is ToolActivityRowViewModel { ToolName: "project_write" });
            var reply = Assert.IsType<AssistantReplyViewModel>(rig.Chat.Transcript[^1]);
            Assert.Equal(AssistantReplyState.Completed, reply.State);
            Assert.True(reply.HasUsageCaption, report.ToString());
        }
        finally
        {
            LiveCopilotTurnTests.Record(report.ToString());
        }
    }

    /// <summary>
    ///     Test that Stop in the middle of a long first turn is safe: about a minute into the generation the user stops the
    ///     turn, the panel says that it is stopping and is idle again within seconds, and a follow-up question then gets an
    ///     answer without an error and without a session that is replaced for no reason; the time to the answer is recorded.
    /// </summary>
    [Fact(Timeout = TestTimeoutMilliseconds)]
    public async Task LiveCopilotLongTurn_StopAboutAMinuteIn_FollowUpIsAnswered()
    {
        // Arrange: skipped unless asked for; the application over an empty project
        SkipUnlessLive();
        var token = TestContext.Current.CancellationToken;
        await using var rig = await LiveRig.CreateAsync(token);
        var report = new StringBuilder().AppendLine($"== Scenario (b): Stop about 60 s into the generation, then a follow-up ({rig.Header})");
        try
        {
            await rig.AssertReadyAsync();

            // Act: the request is sent, and a minute into the turn (a timer of the test) the user presses Stop
            var started = await rig.BeginTurnAsync(MarketingRequest);
            await Task.Delay(TimeSpan.FromSeconds(60), token);
            var stillBusy = rig.Chat.IsCopilotBusy;
            report.Append(rig.RuntimeEvents("Runtime events just before Stop"));
            var stopAt = rig.Recorder.Elapsed;
            rig.Chat.Stop();
            var stoppingShown = rig.Chat.IsStopping && rig.Chat.BusyText == ChatTurnRunner.StoppingText;
            rig.Recorder.Add("test", "stop-pressed", string.Empty);
            await rig.Chat.WaitForTurnAsync().WaitAsync(TimeSpan.FromSeconds(60), token);
            var idleAt = rig.Recorder.Elapsed;
            var stopWindow = new LiveWindow(started, idleAt, rig.Recorder.Summarize(started, idleAt));
            report.Append(rig.Summarize("Turn 1 (stopped)", stopWindow));
            report.AppendLine($"Stop: pressed at {stopAt} ms; stopping text shown immediately={stoppingShown}; idle after {(idleAt - stopAt)} ms");

            // The user then asks a question, which must be answered by the conversation that was stopped
            var followUp = await rig.RunTurnAsync(
                "Without drawing or changing anything: in one short sentence, what kind of image did I ask you for?",
                TimeSpan.FromMinutes(3),
                token);
            report.Append(rig.Summarize("Turn 2 (follow-up)", followUp));
            var answeredAfter = followUp.EndMilliseconds - followUp.StartMilliseconds;
            report.AppendLine($"Follow-up: answered after {answeredAfter} ms; first text after {rig.Recorder.FirstAfter("ui", "row:Assistant", followUp.StartMilliseconds) - followUp.StartMilliseconds} ms");
            report.Append(rig.SessionReport());

            // Assert: the turn was long enough to be stopped, the stop was seen at once, and the panel was idle within seconds
            Assert.True(stillBusy, "The turn ended before Stop was pressed, so Stop was not tested; use a longer request." + Environment.NewLine + report);
            Assert.True(stoppingShown, report.ToString());
            Assert.True(idleAt - stopAt <= 10_000, $"Stop took {idleAt - stopAt} ms to end the turn.{Environment.NewLine}{report}");
            Assert.False(rig.Chat.IsCopilotBusy);

            // Assert: the follow-up was answered, in words, without an error, and not after a long wait
            Assert.Empty(rig.ErrorNotices());
            var reply = Assert.IsType<AssistantReplyViewModel>(rig.Chat.Transcript[^1]);
            Assert.Equal(AssistantReplyState.Completed, reply.State);
            Assert.False(string.IsNullOrWhiteSpace(reply.Text), report.ToString());
            Assert.Equal("Completed", rig.OutcomeOf(2));
            Assert.True(answeredAfter <= 120_000, $"The follow-up took {answeredAfter} ms.{Environment.NewLine}{report}");

            // Assert: the follow-up ran on the same provider session, or the product logged why it started a new one
            var first = rig.SessionIdOf(1);
            var second = rig.SessionIdOf(2);
            Assert.NotNull(second);
            Assert.True(
                (first is not null && first == second) || rig.LogContains("Conversation released:") || rig.LogContains("Session recovered:"),
                "The follow-up ran on a new session that the log does not explain." + Environment.NewLine + report);
        }
        finally
        {
            LiveCopilotTurnTests.Record(report.ToString());
        }
    }

    /// <summary>
    ///     Test two consecutive refinement turns in one conversation: the first makes a small drawing and the second changes
    ///     it. Both run on one and the same provider session, which was created once and resumed by the second turn: no new
    ///     session, no replay of the history, and no recovery. The Copilot agent releases its session handle after every run
    ///     by design, so the assertion is that no session is created, recovered, or dropped between the turns.
    /// </summary>
    [Fact(Timeout = TestTimeoutMilliseconds)]
    public async Task LiveCopilotLongTurn_TwoRefinementTurns_ShareOneProviderSessionWithoutRecovery()
    {
        // Arrange: skipped unless asked for; the application over an empty project
        SkipUnlessLive();
        var token = TestContext.Current.CancellationToken;
        await using var rig = await LiveRig.CreateAsync(token);
        var report = new StringBuilder().AppendLine($"== Scenario (c): two refinement turns in one conversation ({rig.Header})");
        try
        {
            await rig.AssertReadyAsync();

            // Act: a first drawing, and a refinement of it
            var first = await rig.RunTurnAsync(
                "Draw a simple 800 by 600 pixel SVG named robot-monkey.svg: a cheerful gray robot monkey face on a yellow background.",
                TurnLimit,
                token);
            var drawing = Path.Combine(rig.ProjectFolder, "robot-monkey.svg");
            var before = File.Exists(drawing) ? await File.ReadAllTextAsync(drawing, token) : string.Empty;
            report.Append(rig.Summarize("Turn 1 (first drawing)", first));
            var second = await rig.RunTurnAsync(
                "Now change robot-monkey.svg: make the background light blue and add a red bow tie on the monkey. Keep the rest as it is.",
                TurnLimit,
                token);
            await rig.Composition.Preview.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(60), token);
            var after = File.Exists(drawing) ? await File.ReadAllTextAsync(drawing, token) : string.Empty;
            report.Append(rig.Summarize("Turn 2 (refinement)", second));
            report.AppendLine($"Drawing: bytes before={before.Length} after={after.Length}; changed={before != after}; preview hasImage={rig.Composition.Preview.HasImage}");
            report.Append(rig.SessionReport());
            report.Append(rig.RuntimeEvents("Runtime events"));

            // Assert: both turns completed without an error, and the second changed the drawing
            Assert.Equal("Completed", rig.OutcomeOf(1));
            Assert.Equal("Completed", rig.OutcomeOf(2));
            Assert.Empty(rig.ErrorNotices());
            Assert.NotEqual(string.Empty, before);
            Assert.NotEqual(before, after);

            // Assert: one conversation and one provider session, created by the first turn and resumed by the second
            var firstId = rig.SessionIdOf(1);
            Assert.NotNull(firstId);
            Assert.Equal(firstId, rig.SessionIdOf(2));
            Assert.Equal(1, rig.LogCount("Conversation started:"));
            Assert.True(rig.LogContains($"turn=1 id={firstId} run=created"), report.ToString());
            Assert.True(rig.LogContains($"turn=2 id={firstId} run=resumed"), report.ToString());

            // Assert: nothing was recovered, dropped, or replaced between the turns
            Assert.Equal(0, rig.LogCount("Session recovered:"));
            Assert.Equal(0, rig.LogCount("Conversation released:"));
            Assert.Equal(0, rig.LogCount("its session is dropped"));
            Assert.Equal(2, rig.LogCount("outcome=Completed cancelSource=None"));
            Assert.Equal(1, rig.StarterRuntimes);
        }
        finally
        {
            LiveCopilotTurnTests.Record(report.ToString());
        }
    }

    /// <summary>
    ///     Skips the test unless the live tests are asked for.
    /// </summary>
    private static void SkipUnlessLive()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT") == "1",
            "Set CANVASPILOT_LIVE_COPILOT=1 to run a real GitHub Copilot turn on the installed Copilot CLI.");
    }

    /// <summary>
    ///     A time span of a turn on the clock of the recorder, with what the recorder counted in it.
    /// </summary>
    /// <param name="StartMilliseconds">When the message was sent.</param>
    /// <param name="EndMilliseconds">When the turn ended.</param>
    /// <param name="Sum">What was counted between the two.</param>
    private sealed record LiveWindow(long StartMilliseconds, long EndMilliseconds, WindowSummary Sum);

    /// <summary>
    ///     What the recorder counted in a window.
    /// </summary>
    /// <param name="Counts">How often each kind of observation was made, by source and kind.</param>
    /// <param name="MaxGapMilliseconds">The longest time without any observation of the user interface, from the send to the end.</param>
    /// <param name="MaxGapWithoutTicksMilliseconds">The same, but without the observations of the elapsed time.</param>
    /// <param name="MaxProviderGapMilliseconds">The longest time between two updates of the provider, or -1 for fewer than two.</param>
    /// <param name="FirstProviderMilliseconds">When the first update of the provider arrived after the send, or -1 for none.</param>
    /// <param name="Phases">The distinct phase texts that were shown, in order.</param>
    private sealed record WindowSummary(
        SortedDictionary<string, int> Counts,
        long MaxGapMilliseconds,
        long MaxGapWithoutTicksMilliseconds,
        long MaxProviderGapMilliseconds,
        long FirstProviderMilliseconds,
        IReadOnlyList<string> Phases);

    /// <summary>
    ///     One observation: when, by which source, what kind, and a short detail that holds no secret.
    /// </summary>
    /// <param name="Milliseconds">The time on the clock of the recorder.</param>
    /// <param name="Source">Who observed it (<c>ui</c>, <c>provider</c>, or <c>test</c>).</param>
    /// <param name="Kind">What kind of observation.</param>
    /// <param name="Detail">A short detail such as the phase text.</param>
    private sealed record Mark(long Milliseconds, string Source, string Kind, string Detail);

    /// <summary>
    ///     Collects the observations of a test with their times, from any thread.
    /// </summary>
    private sealed class LiveRecorder
    {
        /// <summary>The clock of the observations.</summary>
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>The observations, in the order that they were made.</summary>
        private readonly List<Mark> _marks = [];

        /// <summary>Gets the milliseconds since the recorder was made.</summary>
        internal long Elapsed => _clock.ElapsedMilliseconds;

        /// <summary>
        ///     Records one observation now.
        /// </summary>
        /// <param name="source">Who observed it.</param>
        /// <param name="kind">What kind of observation.</param>
        /// <param name="detail">A short detail that holds no secret.</param>
        internal void Add(string source, string kind, string detail)
        {
            var mark = new Mark(_clock.ElapsedMilliseconds, source, kind, detail);
            lock (_marks)
            {
                _marks.Add(mark);
            }
        }

        /// <summary>
        ///     Tells when the first observation of a kind was made at or after a time.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="kind">The kind.</param>
        /// <param name="from">The time to start at.</param>
        /// <returns>The time of the observation, or the time to start at when there is none.</returns>
        internal long FirstAfter(string source, string kind, long from)
        {
            lock (_marks)
            {
                return _marks.FirstOrDefault(mark => mark.Source == source && mark.Kind == kind && mark.Milliseconds >= from)?.Milliseconds ?? from;
            }
        }

        /// <summary>
        ///     Counts what was observed between two times.
        /// </summary>
        /// <param name="from">The start.</param>
        /// <param name="to">The end.</param>
        /// <returns>The counts and gaps.</returns>
        internal WindowSummary Summarize(long from, long to)
        {
            List<Mark> inside;
            lock (_marks)
            {
                inside = [.. _marks.Where(mark => mark.Milliseconds >= from && mark.Milliseconds <= to)];
            }

            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var mark in inside)
            {
                var key = $"{mark.Source}:{mark.Kind}";
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }

            var ui = inside.Where(mark => mark.Source == "ui" && mark.Kind != "busy").ToList();
            var provider = inside.Where(mark => mark.Source == "provider").Select(mark => mark.Milliseconds).ToList();
            var phases = new List<string>();
            foreach (var mark in inside.Where(mark => mark is { Source: "ui", Kind: "phase" }))
            {
                if (phases.Count == 0 || phases[^1] != mark.Detail)
                {
                    phases.Add(mark.Detail);
                }
            }

            return new WindowSummary(
                counts,
                MaxGap(ui.Select(mark => mark.Milliseconds), from, to),
                MaxGap(ui.Where(mark => mark.Kind != "tick").Select(mark => mark.Milliseconds), from, to),
                provider.Count < 2 ? -1 : MaxGap(provider, provider[0], provider[^1]),
                provider.Count == 0 ? -1 : provider[0] - from,
                phases);
        }

        /// <summary>
        ///     Finds the longest time between two neighbors of a series of times, the start and the end included.
        /// </summary>
        /// <param name="times">The times.</param>
        /// <param name="from">The start.</param>
        /// <param name="to">The end.</param>
        /// <returns>The longest gap in milliseconds.</returns>
        private static long MaxGap(IEnumerable<long> times, long from, long to)
        {
            var ordered = times.Append(from).Append(to).Order().ToList();
            long max = 0;
            for (var index = 1; index < ordered.Count; index++)
            {
                max = Math.Max(max, ordered[index] - ordered[index - 1]);
            }

            return max;
        }
    }

    /// <summary>
    ///     A factory of agents that passes on to the real one and records every update that the provider streams, by the
    ///     kind of its contents and when it arrived, which shows what the runtime delivers and when.
    /// </summary>
    private sealed class RecordingAgentFactory : IAgentFactory
    {
        /// <summary>The real factory.</summary>
        private readonly IAgentFactory _inner;

        /// <summary>The recorder.</summary>
        private readonly LiveRecorder _recorder;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RecordingAgentFactory" /> class.
        /// </summary>
        /// <param name="inner">The real factory.</param>
        /// <param name="recorder">The recorder.</param>
        internal RecordingAgentFactory(IAgentFactory inner, LiveRecorder recorder)
        {
            _inner = inner;
            _recorder = recorder;
        }

        /// <inheritdoc />
        public async Task<AgentCreationResult> CreateAsync(AgentCreationRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _inner.CreateAsync(request, cancellationToken);
            return result.Agent is null ? result : AgentCreationResult.Created(new RecordingAgent(result.Agent, _recorder), result);
        }

        /// <inheritdoc />
        public Task DeleteSessionAsync(AgentSession session, CancellationToken cancellationToken = default)
        {
            return _inner.DeleteSessionAsync(session, cancellationToken);
        }

        /// <inheritdoc />
        public AgentSessionInfo DescribeSession(AgentSession session)
        {
            return _inner.DescribeSession(session);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            return _inner.DisposeAsync();
        }
    }

    /// <summary>
    ///     An agent that records the updates of the agent that it wraps and passes them on unchanged.
    /// </summary>
    private sealed class RecordingAgent : DelegatingAIAgent
    {
        /// <summary>The recorder.</summary>
        private readonly LiveRecorder _recorder;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RecordingAgent" /> class.
        /// </summary>
        /// <param name="inner">The agent to wrap.</param>
        /// <param name="recorder">The recorder.</param>
        internal RecordingAgent(AIAgent inner, LiveRecorder recorder)
            : base(inner)
        {
            _recorder = recorder;
        }

        /// <inheritdoc />
        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
            {
                var kinds = update.Contents.Select(content => content.GetType().Name.Replace("Content", string.Empty, StringComparison.Ordinal)).Distinct();
                _recorder.Add("provider", string.Join('+', kinds.DefaultIfEmpty("empty")), string.Empty);
                yield return update;
            }
        }
    }

    /// <summary>
    ///     The composed application over temporary folders with the real Copilot runtime, the settings of the record, and an
    ///     open empty project, and the means to observe it.
    /// </summary>
    private sealed class LiveRig : IAsyncDisposable
    {
        /// <summary>The folder that holds everything of the rig.</summary>
        private readonly string _root;

        /// <summary>The logger of the application.</summary>
        private readonly RollingFileLogger _logger;

        /// <summary>The folder where the Copilot runtime keeps its data.</summary>
        private readonly string _runtimeData;

        /// <summary>The counting starter of the real runtime.</summary>
        private readonly LiveCopilotTurnTests.CountingStarter _starter;

        /// <summary>The model of the record.</summary>
        private readonly string _model;

        /// <summary>The reasoning level of the record.</summary>
        private readonly string _effort;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LiveRig" /> class.
        /// </summary>
        /// <param name="root">The folder that holds everything.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="runtimeData">The data folder of the runtime.</param>
        /// <param name="starter">The starter.</param>
        /// <param name="composition">The composition.</param>
        /// <param name="recorder">The recorder.</param>
        /// <param name="projectFolder">The folder of the open project.</param>
        /// <param name="model">The model.</param>
        /// <param name="effort">The reasoning level.</param>
        private LiveRig(
            string root,
            RollingFileLogger logger,
            string runtimeData,
            LiveCopilotTurnTests.CountingStarter starter,
            ApplicationComposition composition,
            LiveRecorder recorder,
            string projectFolder,
            string model,
            string effort)
        {
            _root = root;
            _logger = logger;
            _runtimeData = runtimeData;
            _starter = starter;
            Composition = composition;
            Recorder = recorder;
            ProjectFolder = projectFolder;
            _model = model;
            _effort = effort;
        }

        /// <summary>Gets the composed application.</summary>
        internal ApplicationComposition Composition { get; }

        /// <summary>Gets the chat panel.</summary>
        internal ChatPanelViewModel Chat => Composition.Chat;

        /// <summary>Gets the recorder.</summary>
        internal LiveRecorder Recorder { get; }

        /// <summary>Gets the folder of the open project.</summary>
        internal string ProjectFolder { get; }

        /// <summary>Gets how many different Copilot runtimes were handed out.</summary>
        internal int StarterRuntimes => _starter.Runtimes;

        /// <summary>Gets the line that heads a record: the model, the level, and the sign-in.</summary>
        internal string Header => $"model={_model} effort={_effort} signIn={LiveCopilotTurnTests.DescribeSignIn(_starter.SignIn)} at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}";

        /// <summary>
        ///     Makes the rig: the application with GitHub Copilot as provider, the model and the level of the record, and an
        ///     empty project.
        /// </summary>
        /// <param name="token">The token of the test.</param>
        /// <returns>The rig.</returns>
        internal static async Task<LiveRig> CreateAsync(CancellationToken token)
        {
            var root = Directory.CreateTempSubdirectory("canvaspilot-live-long-").FullName;
            var model = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_MODEL")?.Trim();
            var effort = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_EFFORT")?.Trim();
            model = string.IsNullOrEmpty(model) ? "claude-opus-5.5" : model;
            effort = string.IsNullOrEmpty(effort) ? "high" : effort;
            var logger = new RollingFileLogger(Path.Combine(root, "logs"));
            var localData = Path.Combine(root, "local");
            var runtimeData = AgentService.GetCopilotDataDirectory(localData);
            var starter = new LiveCopilotTurnTests.CountingStarter(new CopilotRuntimeStarter(runtimeData));
            var settings = new SettingsService(Path.Combine(root, "settings"), logger);
            settings.Update(current => current with
            {
                AiProvider = current.AiProvider with { Provider = AiProviderKind.GitHubCopilot, ModelName = model, ReasoningEffort = effort },
            });
            var recorder = new LiveRecorder();
            var factory = new RecordingAgentFactory(new AgentProviderFactory(logger: logger, starter: starter, ownsStarter: false), recorder);
            var composition = LiveCopilotTurnTests.ComposeLiveApplication(root, logger, localData, starter, settings, factory);
            composition.Shell.AttachDialogService(new FakeDialogService());
            var opened = await composition.Projects.CreateAsync(Path.Combine(root, "Documents"), "Demo", token);
            var rig = new LiveRig(root, logger, runtimeData, starter, composition, recorder, opened.Project!.FolderPath, model, effort);
            rig.Observe();
            return rig;
        }

        /// <summary>
        ///     Checks that the provider is ready (the sign-in, the model, and the level are accepted) before a turn costs
        ///     anything.
        /// </summary>
        /// <returns>A task that completes when the check is done.</returns>
        internal async Task AssertReadyAsync()
        {
            await Chat.RefreshReadinessAsync();
            Assert.False(Chat.HasReadinessIssue, $"The provider is not ready: {Chat.ReadinessMessage}");
        }

        /// <summary>
        ///     Sends a message the way the user does and returns when the message was sent and the turn has started, not when it has ended.
        /// </summary>
        /// <param name="text">The message.</param>
        /// <returns>The time on the clock of the recorder when the message was sent.</returns>
        internal async Task<long> BeginTurnAsync(string text)
        {
            Composition.Composer.InsertTyped(text);
            var started = Recorder.Elapsed;
            Recorder.Add("test", "send", string.Empty);
            await Chat.SendCommand.ExecuteAsync(null);
            return started;
        }

        /// <summary>
        ///     Sends a message and waits for the end of the turn.
        /// </summary>
        /// <param name="text">The message.</param>
        /// <param name="limit">The longest time that the turn may take.</param>
        /// <param name="token">The token of the test.</param>
        /// <returns>When the turn started and ended and what was counted in between.</returns>
        internal async Task<LiveWindow> RunTurnAsync(string text, TimeSpan limit, CancellationToken token)
        {
            var started = await BeginTurnAsync(text);
            await Chat.WaitForTurnAsync().WaitAsync(limit, token);
            var ended = Recorder.Elapsed;
            return new LiveWindow(started, ended, Recorder.Summarize(started, ended));
        }

        /// <summary>
        ///     Lists the files of the project, without its data folder.
        /// </summary>
        /// <returns>The names of the files.</returns>
        internal IEnumerable<string> ProjectFiles()
        {
            return Directory.EnumerateFiles(ProjectFolder, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(ProjectFolder, file))
                .Where(file => !file.StartsWith(ProjectDocument.DataFolderName, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Gets the notices of an error in the conversation.
        /// </summary>
        /// <returns>The messages of the notices.</returns>
        internal IReadOnlyList<string> ErrorNotices()
        {
            return [.. Chat.Transcript.OfType<NoticeRowViewModel>().Where(notice => notice.IsError).Select(notice => notice.Message)];
        }

        /// <summary>
        ///     Words what happened in a turn for the record: the times, what was observed by kind, the longest gaps, the
        ///     phases, the tools, and the usage of the turn.
        /// </summary>
        /// <param name="label">The name of the turn.</param>
        /// <param name="window">The window of the turn.</param>
        /// <returns>The words.</returns>
        internal string Summarize(string label, LiveWindow window)
        {
            var sum = window.Sum;
            var text = new StringBuilder();
            text.AppendLine($"-- {label}: {(window.EndMilliseconds - window.StartMilliseconds) / 1000.0:F1} s wall ({window.StartMilliseconds}..{window.EndMilliseconds} ms); sign-in {LiveCopilotTurnTests.DescribeSignIn(_starter.SignIn)}");
            text.AppendLine($"   Observations by kind: {string.Join(", ", sum.Counts.Select(pair => $"{pair.Key}={pair.Value}"))}");
            text.AppendLine($"   Status silent for at most {sum.MaxGapMilliseconds} ms (without the elapsed-time ticks: {sum.MaxGapWithoutTicksMilliseconds} ms)");
            text.AppendLine($"   Provider updates seen by the service: first after {sum.FirstProviderMilliseconds} ms, at most {sum.MaxProviderGapMilliseconds} ms between two");
            text.AppendLine($"   Phases shown: {string.Join(" > ", sum.Phases.DefaultIfEmpty("(none)"))}");
            var tools = Chat.Transcript.OfType<ToolActivityRowViewModel>().ToList();
            text.AppendLine($"   Tool rows in the conversation so far: {tools.Count} [{string.Join(", ", tools.Select(tool => $"{tool.ToolName}{(tool.ShowsFailure ? "(failed)" : string.Empty)}"))}]");
            var captions = Chat.Transcript.OfType<AssistantReplyViewModel>().Where(reply => reply.HasUsageCaption).Select(reply => reply.UsageCaption).ToList();
            text.AppendLine($"   Usage captions so far: [{string.Join(" | ", captions)}]");
            text.AppendLine($"   Service log: {string.Join(" || ", LogLines("Turn ended:").Select(Detail))}");
            return text.ToString();
        }

        /// <summary>
        ///     Words the session lines of the application log for the record: the conversations, the provider sessions, the
        ///     releases, and the recoveries.
        /// </summary>
        /// <returns>The words.</returns>
        internal string SessionReport()
        {
            var text = new StringBuilder("-- Sessions (from the application log)").AppendLine();
            foreach (var line in LogLines("Conversation started:", "Conversation released:", "Provider session:", "Session recovered:", "its session is dropped", "Turn refused:"))
            {
                text.AppendLine("   " + Detail(line));
            }

            var runtime = LogLines("Copilot runtime:");
            text.AppendLine($"   Runtime log lines: {runtime.Count}; warnings or worse: {runtime.Count(line => line.Contains("level=Warning", StringComparison.Ordinal) || line.Contains("level=Error", StringComparison.Ordinal))}");
            return text.ToString();
        }

        /// <summary>
        ///     Reads what the Copilot runtime recorded of its sessions (the types of its events, when they came, and the
        ///     premium requests that it counted), which shows what the runtime delivered and when. It reads no content.
        /// </summary>
        /// <param name="label">The heading of the record.</param>
        /// <returns>The words.</returns>
        internal string RuntimeEvents(string label)
        {
            var text = new StringBuilder($"-- {label}").AppendLine();
            if (!Directory.Exists(_runtimeData))
            {
                return text.AppendLine("   (no data folder)").ToString();
            }

            foreach (var file in Directory.EnumerateFiles(_runtimeData, "events.jsonl", SearchOption.AllDirectories))
            {
                text.AppendLine($"   Session {Path.GetFileName(Path.GetDirectoryName(file))}: {DescribeEvents(file)}");
            }

            return text.ToString();
        }

        /// <summary>
        ///     Tells the outcome that the log gives for a turn.
        /// </summary>
        /// <param name="turn">The number of the turn, from 1.</param>
        /// <returns>The outcome, such as <c>Completed</c>, or an empty string when the log has none.</returns>
        internal string OutcomeOf(int turn)
        {
            var line = LogLines($"Turn ended: turn={turn} ").FirstOrDefault();
            var match = line is null ? null : Regex.Match(line, @"outcome=(\S+)");
            return match is { Success: true } ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        ///     Tells the identifier of the provider session that a turn ran on, as the log says.
        /// </summary>
        /// <param name="turn">The number of the turn, from 1.</param>
        /// <returns>The identifier, or <see langword="null" /> when the log has none for the turn.</returns>
        internal string? SessionIdOf(int turn)
        {
            var line = LogLines($"Provider session: turn={turn} ").FirstOrDefault();
            var match = line is null ? null : Regex.Match(line, @"\bid=(\S+)");
            return match is { Success: true } ? match.Groups[1].Value : null;
        }

        /// <summary>
        ///     Tells whether any line of the log holds a text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><see langword="true" /> when a line holds it.</returns>
        internal bool LogContains(string text) => LogCount(text) > 0;

        /// <summary>
        ///     Counts the lines of the log that hold a text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The number of lines.</returns>
        internal int LogCount(string text) => LogLines(text).Count;

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            Composition.Dispose();
            await _starter.DisposeAsync();
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The temporary folder stays when something still holds a file; it is only a temporary folder
            }
        }

        /// <summary>
        ///     Starts to record what the user interface shows: the status of the turn, the rows of the conversation, and
        ///     changes of the rows.
        /// </summary>
        private void Observe()
        {
            var phase = string.Empty;
            var elapsed = string.Empty;
            Chat.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(ChatPanelViewModel.BusyText) when Chat.BusyText != phase:
                        phase = Chat.BusyText;
                        Recorder.Add("ui", "phase", phase);
                        break;
                    case nameof(ChatPanelViewModel.BusyElapsedText) when Chat.BusyElapsedText != elapsed:
                        elapsed = Chat.BusyElapsedText;
                        Recorder.Add("ui", "tick", elapsed);
                        break;
                    case nameof(ChatPanelViewModel.IsCopilotBusy):
                        Recorder.Add("ui", "busy", Chat.IsCopilotBusy.ToString());
                        break;
                    case nameof(ChatPanelViewModel.HasBusyHint):
                        Recorder.Add("ui", "hint", Chat.HasBusyHint.ToString());
                        break;
                }
            };
            Chat.Transcript.CollectionChanged += (_, e) =>
            {
                if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
                {
                    return;
                }

                foreach (TranscriptRowViewModel row in e.NewItems)
                {
                    var kind = row.Kind;
                    Recorder.Add("ui", $"row:{kind}", string.Empty);
                    row.PropertyChanged += (_, _) => Recorder.Add("ui", $"row-change:{kind}", string.Empty);
                }
            };
        }

        /// <summary>
        ///     Reads the lines of the application log that hold any of the texts.
        /// </summary>
        /// <param name="texts">The texts.</param>
        /// <returns>The lines.</returns>
        private List<string> LogLines(params string[] texts)
        {
            if (!File.Exists(_logger.ActiveFilePath))
            {
                return [];
            }

            using var stream = new FileStream(_logger.ActiveFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                if (texts.Any(text => line.Contains(text, StringComparison.Ordinal)))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>
        ///     Cuts a line of the log down to its message and the time before it.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <returns>The shortened line.</returns>
        private static string Detail(string line)
        {
            return line.Length <= 400 ? line : line[..400] + "...";
        }

        /// <summary>
        ///     Describes the events file of a session of the runtime without reading any content: how many events of each
        ///     type, the span, the longest gap between two events, and the premium requests that the runtime counted.
        /// </summary>
        /// <param name="file">The events file.</param>
        /// <returns>The description.</returns>
        private static string DescribeEvents(string file)
        {
            try
            {
                var types = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var times = new List<DateTimeOffset>();
                var premium = new List<double>();
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                while (reader.ReadLine() is { Length: > 0 } line)
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "?" : "?";
                    types[type] = types.GetValueOrDefault(type) + 1;
                    if (root.TryGetProperty("timestamp", out var stamp) && DateTimeOffset.TryParse(stamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                    {
                        times.Add(time);
                    }

                    CollectPremium(root, premium);
                }

                var maxGap = times.Count < 2 ? 0 : times.Zip(times.Skip(1), (first, second) => (second - first).TotalSeconds).Max();
                var span = times.Count < 2 ? 0 : (times[^1] - times[0]).TotalSeconds;
                var counts = string.Join(", ", types.Select(pair => $"{pair.Key}={pair.Value}"));
                return $"{times.Count} events over {span:F1} s, longest gap between events {maxGap:F1} s; premium requests counted: [{string.Join(", ", premium.Distinct())}]; types: {counts}";
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return $"(not readable: {ex.GetType().Name})";
            }
        }

        /// <summary>
        ///     Collects the numbers of every property of an event whose name mentions premium requests.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="found">Receives the numbers.</param>
        private static void CollectPremium(JsonElement element, List<double> found)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Name.Contains("premium", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number)
                        {
                            found.Add(property.Value.GetDouble());
                        }
                        else
                        {
                            CollectPremium(property.Value, found);
                        }
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        CollectPremium(item, found);
                    }

                    break;
            }
        }
    }
}

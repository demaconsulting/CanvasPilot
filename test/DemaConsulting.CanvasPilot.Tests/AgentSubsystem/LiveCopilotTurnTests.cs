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

using System.Text;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A live test of GitHub Copilot through the agent service of the application, on the Copilot CLI that is installed on
///     the machine and the credentials that the machine has. It is skipped unless <c>CANVASPILOT_LIVE_COPILOT=1</c> is set,
///     so that no build needs a Copilot CLI, a sign-in, or the network. The model is the runtime default unless
///     <c>CANVASPILOT_LIVE_COPILOT_MODEL</c> names one. The test belongs to no requirement; the manual verification of the
///     system records its result.
/// </summary>
public sealed class LiveCopilotTurnTests
{
    /// <summary>The longest time that the turn may take.</summary>
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromMinutes(4);

    /// <summary>
    ///     Test that a short real turn through the agent service lists the files of the project and renders a picture with
    ///     the tools of the application, and that the turn completes with words of the model.
    /// </summary>
    [Fact]
    public async Task LiveCopilotTurn_AgentService_ListsProjectFilesAndRendersAPicture()
    {
        // Arrange: skipped unless asked for; the real locator, starter, and factory over a project with one drawing
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT") == "1",
            "Set CANVASPILOT_LIVE_COPILOT=1 to run a real GitHub Copilot turn on the installed Copilot CLI.");
        var token = TestContext.Current.CancellationToken;
        var model = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_MODEL")?.Trim() ?? string.Empty;
        using var rig = new AgentServiceRig(
            new ScriptedChatClient(),
            factory: new AgentProviderFactory(),
            settings: new AiProviderSettings { Provider = AiProviderKind.GitHubCopilot, ModelName = model });
        await File.WriteAllTextAsync(
            Path.Combine(rig.ProjectFolder, "box.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"80\" height=\"40\"><rect width=\"80\" height=\"40\" fill=\"#2060c0\"/></svg>",
            token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TurnTimeout);

        // Act: ask for the two tools in one short turn
        var events = await AgentServiceRig.CollectAsync(
            rig.Service.RunTurnAsync(
                new AgentTurnRequest(AgentSubsystemTests.CreatePayload(
                    "Use the project_list tool to list the files of the project, then use the render_image tool on box.svg, then say in one short sentence which files you found and how large the picture is.")),
                timeout.Token),
            timeout.Token);

        // Record what happened, so that a person can read it
        var summary = new StringBuilder()
            .AppendLine($"Model setting: {(model.Length == 0 ? "(runtime default)" : model)}")
            .AppendLine($"Tools: {string.Join(", ", events.OfType<AgentEvent.ToolStarted>().Select(started => started.Name))}")
            .AppendLine($"Text: {string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text))}")
            .AppendLine($"Last event: {events[^1]}")
            .ToString();
        TestContext.Current.SendDiagnosticMessage(summary);

        // Assert: it completed, both tools ran, and the model said something
        Assert.True(events[^1] is AgentEvent.Completed, summary);
        var tools = events.OfType<AgentEvent.ToolFinished>().Select(finished => finished.Name).ToArray();
        Assert.Contains("project_list", tools);
        Assert.Contains("render_image", tools);
        Assert.NotEmpty(string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)).Trim());
    }

    /// <summary>
    ///     Test the product path: the application is composed as at startup (the real locator, readiness check, factory of
    ///     the providers, and one shared starter of the Copilot runtime) with the default settings, which choose GitHub
    ///     Copilot; the banner is clear, a message asks for a drawing, the answer and the tool rows arrive, the drawing is in
    ///     the project, and the readiness check and the conversation ran on one and the same runtime.
    /// </summary>
    [Fact]
    public async Task LiveCopilotTurn_ComposedApplication_ChatWritesADrawingOnTheSharedRuntime()
    {
        // Arrange: skipped unless asked for
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT") == "1",
            "Set CANVASPILOT_LIVE_COPILOT=1 to run a real GitHub Copilot turn on the installed Copilot CLI.");
        var token = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("canvaspilot-live-").FullName;
        var model = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_MODEL")?.Trim() ?? string.Empty;
        var logger = new RollingFileLogger(Path.Combine(root, "logs"));
        var localData = Path.Combine(root, "local");
        await using var starter = new CountingStarter(new CopilotRuntimeStarter(AgentService.GetCopilotDataDirectory(localData)));
        var settings = new SettingsService(Path.Combine(root, "settings"), logger);
        var dialogs = new FakeDialogService();
        var composition = ApplicationComposition.Create(
            logger,
            settings,
            new FakeRefreshScheduler(),
            defaultProjectsFolder: Directory.CreateDirectory(Path.Combine(root, "Documents")).FullName,
            audioDevices: SpeechTestData.AudioDevices([], []),
            speechModels: SpeechTestData.Models(),
            dragFolder: Path.Combine(root, "drag"),
            styleUserFolder: Path.Combine(root, "styles"),
            localDataRoot: localData,
            copilotStarter: starter);
        try
        {
            composition.Shell.AttachDialogService(dialogs);
            if (model.Length > 0)
            {
                settings.Update(current => current with { AiProvider = current.AiProvider with { ModelName = model } });
            }

            var opened = await composition.Projects.CreateAsync(Path.Combine(root, "Documents"), "Demo", token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TurnTimeout);

            // Act: the banner is checked, as when the panel is shown, and then the user asks for a drawing
            await composition.Chat.RefreshReadinessAsync();
            var banner = (composition.Chat.HasReadinessIssue, composition.Chat.ReadinessMessage);
            composition.Composer.InsertTyped(
                "Write a file box.svg with the project_write tool that holds an SVG of an 80 by 40 blue rectangle, then say in one sentence that it is drawn.");
            await composition.Chat.SendCommand.ExecuteAsync(null);
            await composition.Chat.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

            // Record what happened, so that a person can read it
            var summary = new StringBuilder()
                .AppendLine($"Banner: issue={banner.HasReadinessIssue} message={banner.ReadinessMessage}")
                .AppendLine($"Model setting: {(model.Length == 0 ? "(runtime default)" : model)}")
                .AppendLine($"Rows: {string.Join(" | ", composition.Chat.Transcript.Select(row => row.Kind.ToString()))}")
                .AppendLine($"Starter asks: {starter.Asks}, distinct runtimes: {starter.Runtimes}")
                .ToString();
            TestContext.Current.SendDiagnosticMessage(summary);

            // Assert: no problem in the banner, the drawing exists, a tool row and an answer are in the conversation, and the
            // one runtime served the check and the conversation
            Assert.False(banner.HasReadinessIssue, summary);
            Assert.True(File.Exists(Path.Combine(opened.Project!.FolderPath, "box.svg")), summary);
            Assert.Contains(composition.Chat.Transcript, row => row.Kind == TranscriptRowKind.Tool);
            Assert.Equal(TranscriptRowKind.Assistant, composition.Chat.Transcript[^1].Kind);
            Assert.True(starter.Asks >= 2, summary);
            Assert.Equal(1, starter.Runtimes);
        }
        finally
        {
            composition.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     A starter that passes on to a real one and counts how often it was asked and for how many different runtimes.
    /// </summary>
    private sealed class CountingStarter : ICopilotRuntimeStarter, IAsyncDisposable
    {
        /// <summary>The starter that does the work.</summary>
        private readonly CopilotRuntimeStarter _inner;

        /// <summary>The runtimes that were handed out.</summary>
        private readonly HashSet<ICopilotAgentSource> _runtimes = [];

        /// <summary>
        ///     Initializes a new instance of the <see cref="CountingStarter" /> class.
        /// </summary>
        /// <param name="inner">The starter that does the work.</param>
        internal CountingStarter(CopilotRuntimeStarter inner)
        {
            _inner = inner;
        }

        /// <summary>Gets how often the starter was asked.</summary>
        internal int Asks { get; private set; }

        /// <summary>Gets how many different runtimes were handed out.</summary>
        internal int Runtimes
        {
            get
            {
                lock (_runtimes)
                {
                    return _runtimes.Count;
                }
            }
        }

        /// <inheritdoc />
        public async Task<CopilotStartResult> StartAsync(CopilotStartRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _inner.StartAsync(request, cancellationToken);
            lock (_runtimes)
            {
                Asks++;
                if (result.Agents is not null)
                {
                    _runtimes.Add(result.Agents);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            return _inner.DisposeAsync();
        }
    }
}

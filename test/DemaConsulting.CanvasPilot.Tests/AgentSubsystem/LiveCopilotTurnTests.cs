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
using DemaConsulting.AgentKit.Agents.Copilot;
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
///     <c>CANVASPILOT_LIVE_COPILOT_MODEL</c> names one (and <c>CANVASPILOT_LIVE_COPILOT_EFFORT</c> a reasoning level, for the
///     test of the model settings). The sign-in check records how the runtime signed in (the kind, the login, and the host,
///     never a token) and, when <c>CANVASPILOT_LIVE_COPILOT_EXPECT_SIGNIN</c> names a kind of sign-in, requires it; with
///     <c>CANVASPILOT_LIVE_COPILOT_OUT</c> naming a file it also appends its record there. The test belongs to no
///     requirement; the manual verification of the system records its result.
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
    ///     Test that the default-deny permission handling of the AI library holds on a real runtime: the model is asked to
    ///     run a shell command that writes a marker file, which is not one of the tools of the application. The command
    ///     must not run (no marker file appears in the project folder or beside it) and no tool of a shell is reported,
    ///     whatever the model then says.
    /// </summary>
    [Fact]
    public async Task LiveCopilotTurn_AgentService_ShellCommandRequest_IsRefusedAndRunsNothing()
    {
        // Arrange: skipped unless asked for; a project with no files
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT") == "1",
            "Set CANVASPILOT_LIVE_COPILOT=1 to run a real GitHub Copilot turn on the installed Copilot CLI.");
        var token = TestContext.Current.CancellationToken;
        var model = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_MODEL")?.Trim() ?? string.Empty;
        using var rig = new AgentServiceRig(
            new ScriptedChatClient(),
            factory: new AgentProviderFactory(),
            settings: new AiProviderSettings { Provider = AiProviderKind.GitHubCopilot, ModelName = model });
        var marker = Path.Combine(rig.ProjectFolder, "shell-ran.txt");
        var outsideMarker = Path.Combine(Path.GetDirectoryName(rig.ProjectFolder)!, "shell-ran.txt");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TurnTimeout);

        // Act: ask for a shell command in one short turn
        var events = await AgentServiceRig.CollectAsync(
            rig.Service.RunTurnAsync(
                new AgentTurnRequest(AgentSubsystemTests.CreatePayload(
                    $"Use your shell tool (bash, powershell, or cmd) to run exactly this command: echo done > \"{marker}\" . Do not use any other way to write that file. If you are not allowed to run it, say so in one short sentence.")),
                timeout.Token),
            timeout.Token);

        // Record what happened, so that a person can read it
        var summary = new StringBuilder()
            .AppendLine($"Model setting: {(model.Length == 0 ? "(runtime default)" : model)}")
            .AppendLine($"Tools started: [{string.Join(", ", events.OfType<AgentEvent.ToolStarted>().Select(started => started.Name))}]")
            .AppendLine($"Text: {string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text))}")
            .AppendLine($"Last event: {events[^1]}")
            .AppendLine($"Marker exists: {File.Exists(marker)}; outside marker exists: {File.Exists(outsideMarker)}")
            .ToString();
        TestContext.Current.SendDiagnosticMessage(summary);

        // Assert: the turn ended, nothing of a shell was reported as a tool, and the command did not run
        Assert.True(events[^1] is AgentEvent.Completed or AgentEvent.Failed, summary);
        Assert.DoesNotContain(
            events.OfType<AgentEvent.ToolStarted>(),
            started => started.Name.Contains("shell", StringComparison.OrdinalIgnoreCase)
                || started.Name.Contains("bash", StringComparison.OrdinalIgnoreCase)
                || started.Name.Contains("powershell", StringComparison.OrdinalIgnoreCase)
                || started.Name.Contains("exec", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(marker), summary);
        Assert.False(File.Exists(outsideMarker), summary);
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
        var composition = ComposeLiveApplication(root, logger, localData, starter, settings);
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
                .AppendLine($"SignIn: {DescribeSignIn(starter.SignIn)}")
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
    ///     Test the model settings through the product path on a real runtime: the Settings page of the composed application
    ///     lists the models that the signed-in Copilot account is offered (their number and the ones that lack tool calling
    ///     or pictures are recorded), the user chooses a model and a reasoning level that the model offers, the choice is
    ///     stored, the banner is clear, and a real message runs a turn with the chosen model and level. The model is
    ///     <c>CANVASPILOT_LIVE_COPILOT_MODEL</c> when set, otherwise <c>claude-haiku-5.5</c> when listed, otherwise the first
    ///     model with tools and levels; the level is <c>CANVASPILOT_LIVE_COPILOT_EFFORT</c> when set, otherwise <c>low</c>.
    /// </summary>
    [Fact]
    public async Task LiveCopilotTurn_SettingsPage_ListsModelsAndTheChosenModelAndEffortRun()
    {
        // Arrange: skipped unless asked for
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT") == "1",
            "Set CANVASPILOT_LIVE_COPILOT=1 to run a real GitHub Copilot turn on the installed Copilot CLI.");
        var token = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("canvaspilot-live-models-").FullName;
        var wantedModel = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_MODEL")?.Trim() ?? string.Empty;
        var wantedEffort = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_EFFORT")?.Trim() ?? "low";
        var logger = new RollingFileLogger(Path.Combine(root, "logs"));
        var localData = Path.Combine(root, "local");
        await using var starter = new CountingStarter(new CopilotRuntimeStarter(AgentService.GetCopilotDataDirectory(localData)));
        var settings = new SettingsService(Path.Combine(root, "settings"), logger);
        var dialogs = new FakeDialogService();
        var composition = ComposeLiveApplication(root, logger, localData, starter, settings);
        try
        {
            composition.Shell.AttachDialogService(dialogs);
            var summary = new StringBuilder();
            AiProviderSettingsPageViewModel? page = null;
            dialogs.OnSettings = dialog =>
            {
                // The page lists the models when it is shown; the user then chooses one and a level, and chooses OK
                page = Assert.IsType<AiProviderSettingsPageViewModel>(dialog.Pages[0]);
                page.ModelListLoaded.GetAwaiter().GetResult();
                var listed = page.Models.Where(choice => choice.Info is not null).Select(choice => choice.Info!).ToList();
                summary.AppendLine($"Models listed: {listed.Count}; message: '{page.ModelListMessage}'");
                summary.AppendLine($"Without tool calling: [{string.Join(", ", listed.Where(model => !model.SupportsToolCalls).Select(model => model.Id))}]");
                summary.AppendLine($"Without pictures: [{string.Join(", ", listed.Where(model => !model.SupportsVision).Select(model => model.Id))}]");
                summary.AppendLine($"Not usable: [{string.Join(", ", listed.Where(model => !model.IsUsable).Select(model => model.Id))}]");
                summary.AppendLine($"With reasoning levels: {listed.Count(model => model.ReasoningEfforts.Count > 0)}");
                summary.AppendLine($"Models: {string.Join(", ", listed.Select(model => $"{model.Id}[{string.Join('/', model.ReasoningEfforts.Select(effort => effort.Id))}]"))}");
                var chosen = listed.FirstOrDefault(model => model.Id == wantedModel)
                    ?? listed.FirstOrDefault(model => model.Id == "claude-haiku-5.5")
                    ?? listed.First(model => model.IsUsable && model.SupportsToolCalls && model.ReasoningEfforts.Count > 0);
                page.SelectedModel = page.Models.Single(choice => choice.Id == chosen.Id);
                var effort = page.ReasoningEfforts.FirstOrDefault(choice => choice.Value == wantedEffort);
                if (effort is not null)
                {
                    page.SelectedReasoningEffort = effort;
                }

                summary.AppendLine($"Chosen: {chosen.Id}, level '{page.SelectedReasoningEffort?.Value}', details: {page.ModelDetails.Replace('\n', ';')}");
                dialog.OkCommand.Execute(null);
            };

            var opened = await composition.Projects.CreateAsync(Path.Combine(root, "Documents"), "Demo", token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TurnTimeout);

            // Act: the user chooses the model in Settings, the banner is checked, and a message asks for a drawing
            await composition.Shell.OpenSettingsCommand.ExecuteAsync(null);
            await composition.Chat.RefreshReadinessAsync();
            var banner = (composition.Chat.HasReadinessIssue, composition.Chat.ReadinessMessage);
            composition.Composer.InsertTyped(
                "Write a file box.svg with the project_write tool that holds an SVG of an 80 by 40 blue rectangle, then say in one sentence that it is drawn.");
            await composition.Chat.SendCommand.ExecuteAsync(null);
            await composition.Chat.WaitForTurnAsync().WaitAsync(TurnTimeout, token);
            var stored = settings.Current.AiProvider;
            summary.AppendLine($"Banner: issue={banner.HasReadinessIssue} message={banner.ReadinessMessage}");
            summary.AppendLine($"Stored: model={stored.ModelName} effort={stored.ReasoningEffort}");
            summary.AppendLine($"Rows: {string.Join(" | ", composition.Chat.Transcript.Select(row => row.Kind.ToString()))}");
            summary.AppendLine($"Starter asks: {starter.Asks}, distinct runtimes: {starter.Runtimes}");
            summary.AppendLine($"SignIn: {DescribeSignIn(starter.SignIn)}");

            // A reasoning effort that the model does not offer is refused by the catalog of the live runtime and told in the banner
            settings.Update(current => current with { AiProvider = current.AiProvider with { ReasoningEffort = "bogus" } });
            await composition.Chat.RefreshReadinessAsync();
            var refused = (composition.Chat.HasReadinessIssue, composition.Chat.ReadinessMessage);
            summary.AppendLine($"Banner with the effort 'bogus': issue={refused.HasReadinessIssue} message={refused.ReadinessMessage}");
            TestContext.Current.SendDiagnosticMessage(summary.ToString());

            // Assert: models were listed, the choice was stored, the banner is clear, and the turn wrote the drawing
            Assert.NotEmpty(page!.Models);
            Assert.NotEqual(string.Empty, stored.ModelName);
            Assert.False(banner.Item1, summary.ToString());
            Assert.True(refused.HasReadinessIssue, summary.ToString());
            Assert.Contains("does not support reasoning effort 'bogus'. Supported:", refused.ReadinessMessage, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(opened.Project!.FolderPath, "box.svg")), summary.ToString());
            Assert.Contains(composition.Chat.Transcript, row => row.Kind == TranscriptRowKind.Tool);
            Assert.Equal(TranscriptRowKind.Assistant, composition.Chat.Transcript[^1].Kind);
            Assert.Equal(1, starter.Runtimes);
        }
        finally
        {
            composition.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the real readiness check, on the Copilot CLI installed on the machine and over one real runtime, can be
    ///     used and says how the runtime signed in (the account, the host, and the kind of sign-in), and that no token is in
    ///     the sentence. What the machine offers is recorded as facts that hold no secret (the names of the token variables
    ///     that are set, whether the GitHub CLI is on the <c>PATH</c>, whether <c>COPILOT_HOME</c> is set). When
    ///     <c>CANVASPILOT_LIVE_COPILOT_EXPECT_SIGNIN</c> names a kind of sign-in (such as <c>CopilotCli</c>), the runtime
    ///     must have used that kind. The record is also appended to the file that <c>CANVASPILOT_LIVE_COPILOT_OUT</c> names,
    ///     so that it survives test runners that hide diagnostic messages.
    /// </summary>
    [Fact]
    public async Task LiveCopilotTurn_Readiness_ReportsHowCopilotSignedIn()
    {
        // Arrange: skipped unless asked for; the real locator and readiness check over one counting starter of the real runtime
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT") == "1",
            "Set CANVASPILOT_LIVE_COPILOT=1 to run a real GitHub Copilot turn on the installed Copilot CLI.");
        var token = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("canvaspilot-live-signin-").FullName;
        var expected = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_EXPECT_SIGNIN")?.Trim() ?? string.Empty;
        var model = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_MODEL")?.Trim() ?? string.Empty;
        string[] tokenVariables = ["COPILOT_GITHUB_TOKEN", "GH_TOKEN", "GITHUB_TOKEN"];
        try
        {
            await using var starter = new CountingStarter(new CopilotRuntimeStarter(AgentService.GetCopilotDataDirectory(root)));
            var readiness = new AgentReadiness(starter: starter);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TurnTimeout);

            // Act: check the provider the way that Check connection does
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = await ((IProviderCheck)readiness).CheckAsync(
                new AiProviderSettings { Provider = AiProviderKind.GitHubCopilot, ModelName = model },
                timeout.Token);
            watch.Stop();

            // Record what happened, as facts that hold no secret, so that a person can read it
            var signIn = starter.SignIn;
            var summary = new StringBuilder()
                .AppendLine($"Check connection: usable={result.IsUsable}, {watch.Elapsed.TotalSeconds:F1} s")
                .AppendLine($"Message: {result.Message}")
                .AppendLine($"SignIn: {DescribeSignIn(signIn)}")
                .AppendLine($"Token variables set (names only): [{string.Join(", ", tokenVariables.Where(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))))}]")
                .AppendLine($"GitHub CLI on PATH: {IsOnPath("gh")}")
                .AppendLine($"COPILOT_HOME set: {!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPILOT_HOME"))}")
                .AppendLine($"Expected sign-in kind: {(expected.Length == 0 ? "(not set)" : expected)}")
                .ToString();
            Record(summary);

            // Assert: usable, the account and the host named, no token text, and the expected kind when one is named
            Assert.True(result.IsUsable, summary);
            Assert.NotNull(signIn);
            Assert.NotEqual(CopilotSignInSource.Unknown, signIn.Source);
            if (!string.IsNullOrWhiteSpace(signIn.Login))
            {
                Assert.Contains(signIn.Login, result.Message, StringComparison.Ordinal);
            }

            Assert.Contains(signIn.Describe(), result.Message, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"gh[a-z]_|github_pat_", result.Message + summary);
            foreach (var name in tokenVariables)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Assert.DoesNotContain(value, result.Message + summary, StringComparison.Ordinal);
                }
            }

            if (expected.Length > 0)
            {
                Assert.Equal(Enum.Parse<CopilotSignInSource>(expected, ignoreCase: true), signIn.Source);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Words how a runtime signed in for the record: the kind, the login, and the host. It holds no token.
    /// </summary>
    /// <param name="signIn">How the runtime signed in, or <see langword="null" /> when not known.</param>
    /// <returns>The words.</returns>
    private static string DescribeSignIn(CopilotSignInInfo? signIn)
    {
        return signIn is null ? "(not reported)" : $"{signIn.Source} / {signIn.Login ?? "(no login)"}@{signIn.Host ?? "(no host)"}";
    }

    /// <summary>
    ///     Tells whether a program is on the <c>PATH</c>, by its file name with or without the extension of the platform.
    /// </summary>
    /// <param name="name">The name of the program without an extension.</param>
    /// <returns><see langword="true" /> when a folder of the <c>PATH</c> holds it.</returns>
    private static bool IsOnPath(string name)
    {
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return folders.Any(folder =>
        {
            var trimmed = folder.Trim('"');
            return trimmed.Length > 0
                && (File.Exists(Path.Combine(trimmed, name + ".exe")) || File.Exists(Path.Combine(trimmed, name)));
        });
    }

    /// <summary>
    ///     Records the summary of a live check as a diagnostic message and, when <c>CANVASPILOT_LIVE_COPILOT_OUT</c> names a
    ///     file, appends it there, so that the record survives test runners that hide diagnostic messages.
    /// </summary>
    /// <param name="summary">The summary, which holds no token.</param>
    private static void Record(string summary)
    {
        TestContext.Current.SendDiagnosticMessage(summary);
        var path = Environment.GetEnvironmentVariable("CANVASPILOT_LIVE_COPILOT_OUT")?.Trim();
        if (!string.IsNullOrEmpty(path))
        {
            File.AppendAllText(path, summary + Environment.NewLine);
        }
    }

    /// <summary>
    ///     Composes the application as at startup over temporary folders, with the real locator, readiness check, factory of
    ///     the providers, and model list, all sharing the one starter that is given.
    /// </summary>
    /// <param name="root">The temporary folder.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="localData">The root of the local data.</param>
    /// <param name="starter">The starter of the Copilot runtime.</param>
    /// <param name="settings">The settings service.</param>
    /// <returns>The composition.</returns>
    private static ApplicationComposition ComposeLiveApplication(
        string root,
        RollingFileLogger logger,
        string localData,
        ICopilotRuntimeStarter starter,
        SettingsService settings)
    {
        return ApplicationComposition.Create(
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

        /// <summary>
        ///     Gets how the last runtime that was handed out signed in (the kind, the login, and the host, never a token), or
        ///     <see langword="null" /> when no runtime was handed out or it did not say.
        /// </summary>
        internal CopilotSignInInfo? SignIn { get; private set; }

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
                    SignIn = result.Agents.SignIn;
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

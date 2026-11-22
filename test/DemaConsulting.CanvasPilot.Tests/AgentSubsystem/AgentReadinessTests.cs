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

using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AgentReadiness" />: the ordered checks of GitHub Copilot (CLI, version, credentials,
///     runtime) and of Ollama (address, model name, server, model, context window), the exact sentences, and the rule that
///     no token is kept. Every collaborator is a fake, so no program is run and no server is asked.
/// </summary>
public sealed class AgentReadinessTests
{
    /// <summary>The path of the GitHub CLI in the folder that its installer uses.</summary>
    private const string ProgramFilesGh = @"C:\Program Files\GitHub CLI\gh.exe";

    /// <summary>The path of the GitHub CLI in a folder on the PATH.</summary>
    private const string PathGh = @"C:\Tools\gh.exe";

    /// <summary>The text that stands for a token, which must never appear in a message.</summary>
    private const string SecretToken = "gho_Secret_Sentinel_0123456789";

    /// <summary>The settings for GitHub Copilot with automatic detection.</summary>
    private static readonly AiProviderSettings CopilotSettings = new() { Provider = AiProviderKind.GitHubCopilot };

    /// <summary>
    ///     Makes the settings for Ollama with a model and a context window.
    /// </summary>
    /// <param name="model">The model name.</param>
    /// <param name="contextTokens">The configured context window.</param>
    /// <returns>The settings.</returns>
    private static AiProviderSettings OllamaSettings(string model = "gemma3", int contextTokens = 32768)
    {
        return new AiProviderSettings { Provider = AiProviderKind.Ollama, ModelName = model, ContextWindowTokens = contextTokens };
    }

    /// <summary>
    ///     Makes the readiness check on the development machine, where the real CLI answers with a version.
    /// </summary>
    /// <param name="runner">The process runner; the real CLI answers 1.0.114 when not given.</param>
    /// <param name="environment">The environment; the development machine without tokens when not given.</param>
    /// <param name="fileSystem">The file system; the development machine when not given.</param>
    /// <param name="starter">The starter; one that starts the runtime when <see langword="null" />.</param>
    /// <param name="probe">The Ollama probe; one that finds no server when <see langword="null" />.</param>
    /// <param name="ollamaCatalogs">
    ///     The catalog of the Ollama server; one that cannot be read when <see langword="null" />, which leaves the model
    ///     settings unchecked, as for a server that does not describe its models.
    /// </param>
    /// <returns>The readiness check.</returns>
    private static AgentReadiness Create(
        FakeProcessRunner? runner = null,
        FakeEnvironment? environment = null,
        FakeCliFileSystem? fileSystem = null,
        ICopilotRuntimeStarter? starter = null,
        IOllamaProbe? probe = null,
        IOllamaModelCatalogs? ollamaCatalogs = null)
    {
        if (runner is null)
        {
            runner = new FakeProcessRunner();
            runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        }

        environment ??= DevMachine.CreateEnvironment();
        fileSystem ??= DevMachine.CreateFileSystem();
        return new AgentReadiness(
            new CopilotCliLocator(runner, fileSystem, environment),
            starter ?? new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "started")),
            runner,
            environment,
            fileSystem,
            probe ?? new FakeOllamaProbe(),
            ollamaCatalogs ?? new FakeOllamaCatalogs(new FakeModelCatalog("ollama") { Failure = new HttpRequestException("No server.") }));
    }

    /// <summary>
    ///     Test that the constants of the unit are the documented values.
    /// </summary>
    [Fact]
    public void AgentReadiness_Constants_Rule_AreTheDocumentedValues()
    {
        // Assert: the three token variables in order, and the two time limits
        Assert.Equal(["COPILOT_GITHUB_TOKEN", "GH_TOKEN", "GITHUB_TOKEN"], AgentReadiness.TokenVariableNames);
        Assert.Equal(TimeSpan.FromSeconds(5), AgentReadiness.CredentialProbeTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), AgentReadiness.OllamaProbeTimeout);
    }

    /// <summary>
    ///     Test that a machine without a Copilot CLI gets the install sentence and that nothing after the first check runs.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_NoCli_ReportsMissingWithTheInstallSentence()
    {
        // Arrange: a machine with nothing installed, and a token that must not be looked at yet
        var runner = new FakeProcessRunner();
        var environment = new FakeEnvironment(DevMachine.LocalAppData, @"C:\Windows");
        environment.Set("GH_TOKEN", SecretToken);
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "unused"));
        var readiness = Create(runner, environment, new FakeCliFileSystem(), starter);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the exact sentence, not usable, and no program and no start was attempted
        Assert.Equal(AgentReadinessStatus.CopilotCliMissing, report.Status);
        Assert.Equal(
            "The GitHub Copilot CLI was not found. Install it with `winget install GitHub.Copilot` or set its path in Settings.",
            report.Message);
        Assert.False(report.IsUsable);
        Assert.Empty(runner.Runs);
        Assert.Empty(starter.Requests);
    }

    /// <summary>
    ///     Test that a machine whose only copilot.exe is the VS Code shim gets the install sentence with the shim path.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_OnlyShim_ReportsMissingAndNamesTheShim()
    {
        // Arrange: only the shim exists
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(DevMachine.ShimCli, DevMachine.ShimBytes);
        var readiness = Create(environment: DevMachine.CreateEnvironment(), fileSystem: fileSystem);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: missing, and the sentence ends with the shim hint
        Assert.Equal(AgentReadinessStatus.CopilotCliMissing, report.Status);
        Assert.EndsWith($"Only the VS Code copilot shim was found at {DevMachine.ShimCli}.", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a path in the settings that does not exist is reported as a missing CLI with the sentence about that
    ///     path, and that no other candidate is tried.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_OverridePathMissing_ReportsMissingForThatPath()
    {
        // Arrange: the real CLI exists, but the settings name another path
        var settings = new AiProviderSettings { Provider = AiProviderKind.GitHubCopilot, CopilotCliPath = @"C:\Nowhere\copilot.exe" };
        var readiness = Create();

        // Act
        var report = await readiness.GetReportAsync(settings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.CopilotCliMissing, report.Status);
        Assert.Contains(@"C:\Nowhere\copilot.exe", report.Message, StringComparison.Ordinal);
        Assert.Contains("does not exist", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a CLI that cannot report its version is reported as missing with the sentence about reinstalling it.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_CliDoesNotAnswer_ReportsMissingWithReinstallAdvice()
    {
        // Arrange: the real CLI is there but does not start
        var runner = new FakeProcessRunner();
        var readiness = Create(runner);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.CopilotCliMissing, report.Status);
        Assert.Contains("winget install GitHub.Copilot", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a CLI older than 1.0.113 gets the upgrade sentence with its version, and that credentials are not
    ///     looked at.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_CliTooOld_ReportsTooOldWithTheUpgradeSentence()
    {
        // Arrange: version 1.0.112 and a token that must not be looked at yet
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.112");
        var readiness = Create(runner);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the exact sentence, and only the version was asked for
        Assert.Equal(AgentReadinessStatus.CopilotCliTooOld, report.Status);
        Assert.Equal("Copilot CLI 1.0.112 is older than 1.0.113. Run `winget upgrade GitHub.Copilot`.", report.Message);
        Assert.False(report.IsUsable);
        Assert.Equal([DevMachine.RealCli], runner.Runs.Select(run => run.Path));
    }

    /// <summary>
    ///     Test that version 1.0.113 itself is new enough and goes on to the credentials.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_MinimumVersion_IsAcceptedAndGoesOnToCredentials()
    {
        // Arrange: exactly the oldest version, with no credentials
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.113");
        var readiness = Create(runner);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the check got past the version and stopped at the credentials
        Assert.Equal(AgentReadinessStatus.CopilotNotSignedIn, report.Status);
    }

    /// <summary>
    ///     Test that a CLI without any credentials gets the exact sign-in sentence and that the starter is not asked.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_NoCredentials_ReportsNotSignedInWithTheExactSentence()
    {
        // Arrange: the CLI is fine, there is no token variable and no GitHub CLI
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "unused"));
        var readiness = Create(starter: starter);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the exact sentence names gh auth login, the variables, and that the CLI's own login does not apply
        Assert.Equal(AgentReadinessStatus.CopilotNotSignedIn, report.Status);
        Assert.Equal(
            "Copilot is not signed in for CanvasPilot. Sign in with `gh auth login` (or set the COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then choose Check connection. Signing in to the Copilot CLI with /login does not apply to CanvasPilot.",
            report.Message);
        Assert.False(report.IsUsable);
        Assert.Empty(starter.Requests);
    }

    /// <summary>
    ///     Test that each of the three token variables counts as credentials, that the GitHub CLI is not asked then, and that
    ///     the check goes on to the runtime and reports Copilot as ready.
    /// </summary>
    /// <param name="variable">The name of the variable that is set.</param>
    [Theory]
    [InlineData("COPILOT_GITHUB_TOKEN")]
    [InlineData("GH_TOKEN")]
    [InlineData("GITHUB_TOKEN")]
    public async Task AgentReadiness_Copilot_TokenVariable_CountsAsCredentialsAndReportsReady(string variable)
    {
        // Arrange: one token variable, and a GitHub CLI that would fail if it were asked
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var environment = DevMachine.CreateEnvironment();
        environment.Set(variable, SecretToken);
        environment.Set("ProgramFiles", @"C:\Program Files");
        var fileSystem = DevMachine.CreateFileSystem();
        fileSystem.AddFile(ProgramFilesGh, 1_000);
        var readiness = Create(runner, environment, fileSystem);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: Copilot is ready and usable, and the GitHub CLI was not run
        Assert.Equal(AgentReadinessStatus.Ready, report.Status);
        Assert.Equal("GitHub Copilot CLI 1.0.114 is ready.", report.Message);
        Assert.True(report.IsUsable);
        Assert.DoesNotContain(runner.Runs, run => run.Path == ProgramFilesGh);
    }

    /// <summary>
    ///     Test that an empty or white space token variable is not a credential.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_EmptyTokenVariables_AreNotCredentials()
    {
        // Arrange: the variables exist but have no text
        var environment = DevMachine.CreateEnvironment();
        environment.Set("COPILOT_GITHUB_TOKEN", string.Empty);
        environment.Set("GH_TOKEN", "   ");
        var readiness = Create(environment: environment);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.CopilotNotSignedIn, report.Status);
    }

    /// <summary>
    ///     Test that a GitHub CLI in its folder under the program files that prints a token counts as credentials, that it
    ///     is run with the documented arguments and time limit, and that the token appears in no message.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_GitHubCliSignedIn_CountsAsCredentialsAndNeverKeepsTheToken()
    {
        // Arrange: the GitHub CLI is installed under the program files and prints a token
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        runner.Answer(ProgramFilesGh, new ProcessResult(true, 0, SecretToken + "\n", false));
        var environment = DevMachine.CreateEnvironment();
        environment.Set("ProgramFiles", @"C:\Program Files");
        var fileSystem = DevMachine.CreateFileSystem();
        fileSystem.AddFile(ProgramFilesGh, 1_000);
        var readiness = Create(runner, environment, fileSystem);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: past the credentials, run as `gh auth token` with 5 seconds, and the token is nowhere in the report
        Assert.Equal(AgentReadinessStatus.Ready, report.Status);
        var run = Assert.Single(runner.Runs, item => item.Path == ProgramFilesGh);
        Assert.Equal(["auth", "token"], run.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(5), run.Timeout);
        Assert.DoesNotContain(SecretToken, report.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretToken, report.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a GitHub CLI on the PATH is found before the one under the program files.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_GitHubCliOnPath_IsRunFromThePath()
    {
        // Arrange: gh.exe is in a folder of the PATH and also under the program files
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        runner.Answer(PathGh, new ProcessResult(true, 0, SecretToken, false));
        var environment = DevMachine.CreateEnvironment();
        environment.Set("PATH", environment.GetVariable("PATH") + @";""C:\Tools""");
        environment.Set("ProgramFiles", @"C:\Program Files");
        var fileSystem = DevMachine.CreateFileSystem();
        fileSystem.AddFile(PathGh, 1_000);
        fileSystem.AddFile(ProgramFilesGh, 1_000);
        var readiness = Create(runner, environment, fileSystem);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the one on the PATH was run, and the other was not
        Assert.Equal(AgentReadinessStatus.Ready, report.Status);
        Assert.Contains(runner.Runs, run => run.Path == PathGh);
        Assert.DoesNotContain(runner.Runs, run => run.Path == ProgramFilesGh);
    }

    /// <summary>
    ///     Test that a GitHub CLI that is not signed in (a failed exit, no output, a time out, or no start) is not a
    ///     credential.
    /// </summary>
    /// <param name="started">Whether the program started.</param>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="output">What it printed.</param>
    /// <param name="timedOut">Whether it ran too long.</param>
    [Theory]
    [InlineData(true, 1, "You are not logged into any GitHub hosts.", false)]
    [InlineData(true, 0, "", false)]
    [InlineData(true, 0, "  \n", false)]
    [InlineData(true, -1, "", true)]
    [InlineData(false, -1, "cannot start", false)]
    public async Task AgentReadiness_Copilot_GitHubCliNotSignedIn_IsNotACredential(
        bool started,
        int exitCode,
        string output,
        bool timedOut)
    {
        // Arrange: the GitHub CLI is installed but answers the way the theory says
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        runner.Answer(ProgramFilesGh, new ProcessResult(started, exitCode, output, timedOut));
        var environment = DevMachine.CreateEnvironment();
        environment.Set("ProgramFiles", @"C:\Program Files");
        var fileSystem = DevMachine.CreateFileSystem();
        fileSystem.AddFile(ProgramFilesGh, 1_000);
        var readiness = Create(runner, environment, fileSystem);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the user is told to sign in
        Assert.Equal(AgentReadinessStatus.CopilotNotSignedIn, report.Status);
    }

    /// <summary>
    ///     Test that a starter that reports each way of not being usable makes the report carry the matching status and the
    ///     starter's own sentence, and that the report is not usable.
    /// </summary>
    /// <param name="start">The status that the starter answers.</param>
    /// <param name="expected">The status that the report must have.</param>
    [Theory]
    [InlineData(CopilotStartStatus.CliTooOld, AgentReadinessStatus.CopilotCliTooOld)]
    [InlineData(CopilotStartStatus.NotSignedIn, AgentReadinessStatus.CopilotNotSignedIn)]
    [InlineData(CopilotStartStatus.Failed, AgentReadinessStatus.CopilotRuntimeUnavailable)]
    public async Task AgentReadiness_Copilot_StarterRefuses_ReportsTheMatchingStatusAndSentence(
        CopilotStartStatus start,
        AgentReadinessStatus expected)
    {
        // Arrange: everything is in order up to the runtime, which the starter refuses
        var environment = DevMachine.CreateEnvironment();
        environment.Set("COPILOT_GITHUB_TOKEN", SecretToken);
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(start, "The sentence of the starter."));
        var readiness = Create(environment: environment, starter: starter);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, report.Status);
        Assert.Equal("The sentence of the starter.", report.Message);
        Assert.False(report.IsUsable);
    }

    /// <summary>
    ///     Test that a starter that can start the runtime makes the report ready, and that it is asked with the path and
    ///     version of the CLI that the locator found.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_StarterStarted_ReportsReadyAndAsksWithTheCliThatWasFound()
    {
        // Arrange: a token and a starter that can start
        var environment = DevMachine.CreateEnvironment();
        environment.Set("GITHUB_TOKEN", SecretToken);
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "started"));
        var readiness = Create(environment: environment, starter: starter);

        // Act
        var report = await readiness.GetReportAsync(CopilotSettings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: ready and usable, and the request carried what the locator found
        Assert.Equal(AgentReadinessStatus.Ready, report.Status);
        Assert.True(report.IsUsable);
        Assert.Equal("GitHub Copilot CLI 1.0.114 is ready.", report.Message);
        var request = Assert.Single(starter.Requests);
        Assert.Equal(DevMachine.RealCli, request.CliPath);
        Assert.Equal(new Version(1, 0, 114), request.CliVersion);
    }

    /// <summary>
    ///     Test that the model of the settings is validated by the catalog of the running runtime: a model that can call
    ///     tools and take pictures is ready, and the sentence names it; no model named is ready on the default model.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_ModelOfTheSettings_IsValidatedByTheRuntimeCatalogAndNamedWhenReady()
    {
        // Arrange: a runtime whose catalog offers a model with reasoning efforts
        var environment = DevMachine.CreateEnvironment();
        environment.Set("GITHUB_TOKEN", SecretToken);
        var source = new FakeCopilotAgentSource(new ScriptedChatClient(), new FakeModelCatalog("copilot", TestModels.Make("copilot", "claude-haiku-5.5", efforts: ["low", "xhigh"])));
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "started", source));
        var readiness = Create(environment: environment, starter: starter);
        var token = TestContext.Current.CancellationToken;

        // Act: the model with an effort that it offers, and no model
        var chosen = await readiness.GetReportAsync(new AiProviderSettings { ModelName = "claude-haiku-5.5", ReasoningEffort = "xhigh" }, cancellationToken: token);
        var none = await readiness.GetReportAsync(CopilotSettings, cancellationToken: token);

        // Assert
        Assert.Equal(AgentReadinessStatus.Ready, chosen.Status);
        Assert.Equal("GitHub Copilot CLI 1.0.114 is ready with the model claude-haiku-5.5.", chosen.Message);
        Assert.Equal("GitHub Copilot CLI 1.0.114 is ready.", none.Message);
    }

    /// <summary>
    ///     Test that model settings that the catalog of the runtime refuses are reported as invalid, with the sentence of
    ///     the library, and are not usable.
    /// </summary>
    /// <param name="model">The model of the settings.</param>
    /// <param name="effort">The reasoning effort of the settings.</param>
    /// <param name="sentence">The part of the sentence of the library.</param>
    [Theory]
    [InlineData("no-tools", "", "does not support tool calling")]
    [InlineData("absent", "", "is not available from provider 'copilot'")]
    [InlineData("full", "bogus", "does not support reasoning effort 'bogus'. Supported: low, high.")]
    [InlineData("disabled", "", "cannot be used: The model is disabled by your organization.")]
    public async Task AgentReadiness_Copilot_ModelSettingsTheCatalogRefuses_ReportModelSettingsInvalid(string model, string effort, string sentence)
    {
        // Arrange: a runtime with a few models
        var environment = DevMachine.CreateEnvironment();
        environment.Set("GITHUB_TOKEN", SecretToken);
        var catalog = new FakeModelCatalog(
            "copilot",
            TestModels.Make("copilot", "full", efforts: ["low", "high"]),
            TestModels.Make("copilot", "no-tools", toolCalls: false),
            TestModels.Make("copilot", "disabled", usable: false));
        var source = new FakeCopilotAgentSource(new ScriptedChatClient(), catalog);
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "started", source));
        var readiness = Create(environment: environment, starter: starter);

        // Act
        var report = await readiness.GetReportAsync(new AiProviderSettings { ModelName = model, ReasoningEffort = effort }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.ModelSettingsInvalid, report.Status);
        Assert.False(report.IsUsable);
        Assert.Contains(sentence, report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a model that cannot take pictures is a warning that does not stop the provider, and that a catalog that
    ///     cannot be read is reported as a runtime that cannot be used.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_ModelWithoutVisionAndUnreadableCatalog_WarnsAndReportsTheRuntime()
    {
        // Arrange: a runtime with a model that cannot see
        var environment = DevMachine.CreateEnvironment();
        environment.Set("GITHUB_TOKEN", SecretToken);
        var catalog = new FakeModelCatalog("copilot", TestModels.Make("copilot", "blind", vision: false));
        var source = new FakeCopilotAgentSource(new ScriptedChatClient(), catalog);
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "started", source));
        var readiness = Create(environment: environment, starter: starter);
        var token = TestContext.Current.CancellationToken;

        // Act: the model that cannot see, then a catalog that fails
        var blind = await readiness.GetReportAsync(new AiProviderSettings { ModelName = "blind" }, cancellationToken: token);
        catalog.Failure = new InvalidOperationException("The Copilot runtime could not list models: it is not signed in.");
        var failed = await readiness.GetReportAsync(new AiProviderSettings { ModelName = "blind" }, cancellationToken: token);

        // Assert: a usable warning that says why, and then the runtime problem
        Assert.Equal(AgentReadinessStatus.ModelLacksVision, blind.Status);
        Assert.True(blind.IsUsable);
        Assert.Contains("blind does not accept pictures", blind.Message, StringComparison.Ordinal);
        Assert.Equal(AgentReadinessStatus.CopilotRuntimeUnavailable, failed.Status);
        Assert.Contains("could not list models", failed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the plain check searches again only when asked: the second plain check reuses the answer, and Check
    ///     connection (through the settings interface) asks the CLI again.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Copilot_RefreshAndCheckAsync_SearchAgainOnlyWhenAsked()
    {
        // Arrange
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var readiness = Create(runner);
        var token = TestContext.Current.CancellationToken;

        // Act: two plain checks, then Check connection
        await readiness.GetReportAsync(CopilotSettings, cancellationToken: token);
        await readiness.GetReportAsync(CopilotSettings, cancellationToken: token);
        var afterPlain = runner.Runs.Count(run => run.Path == DevMachine.RealCli);
        IProviderCheck check = readiness;
        var result = await check.CheckAsync(CopilotSettings, token);

        // Assert: one --version for the two plain checks, one more for the connection check, and the result is data
        Assert.Equal(1, afterPlain);
        Assert.Equal(2, runner.Runs.Count(run => run.Path == DevMachine.RealCli));
        Assert.False(result.IsUsable);
        Assert.Equal(AgentReadinessStatus.CopilotNotSignedIn, (await readiness.GetReportAsync(CopilotSettings, cancellationToken: token)).Status);
    }

    /// <summary>
    ///     Test that a cancelled token ends the check with a cancellation and not with a report.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_GetReport_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        var readiness = Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act / Assert: for both providers
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readiness.GetReportAsync(CopilotSettings, cancellationToken: cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readiness.GetReportAsync(OllamaSettings(), cancellationToken: cancelled.Token));
    }

    /// <summary>
    ///     Test that missing settings and a missing argument are refused.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_GetReport_NullSettings_ThrowsArgumentNull()
    {
        // Arrange
        var readiness = Create();

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => readiness.GetReportAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that an Ollama address that is not an http or https address is a settings problem and the server is not
    ///     asked.
    /// </summary>
    /// <param name="endpoint">The address.</param>
    [Theory]
    [InlineData("")]
    [InlineData("localhost:11434")]
    [InlineData("ftp://localhost")]
    public async Task AgentReadiness_Ollama_InvalidEndpoint_ReportsNotConfigured(string endpoint)
    {
        // Arrange
        var probe = new FakeOllamaProbe();
        var readiness = Create(probe: probe);
        var settings = OllamaSettings() with { OllamaEndpoint = endpoint };

        // Act
        var report = await readiness.GetReportAsync(settings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.NotConfigured, report.Status);
        Assert.Equal("The Ollama address in Settings is not a valid http or https address; fix it, then choose Check connection.", report.Message);
        Assert.Empty(probe.ListQuestions);
    }

    /// <summary>
    ///     Test that an Ollama provider without a model name is a settings problem and the server is not asked.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Ollama_NoModelName_ReportsNotConfigured()
    {
        // Arrange
        var probe = new FakeOllamaProbe();
        var readiness = Create(probe: probe);

        // Act
        var report = await readiness.GetReportAsync(OllamaSettings("  "), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.NotConfigured, report.Status);
        Assert.Equal("Enter the name of an Ollama model in Settings, then choose Check connection.", report.Message);
        Assert.Empty(probe.ListQuestions);
    }

    /// <summary>
    ///     Test that a server that does not answer is reported with the address, and that it was asked at that address
    ///     with the limit of five seconds.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Ollama_ServerNotReachable_ReportsUnreachable()
    {
        // Arrange: the probe finds no server
        var probe = new FakeOllamaProbe { Models = OllamaModelList.Unreachable };
        var readiness = Create(probe: probe);

        // Act
        var report = await readiness.GetReportAsync(OllamaSettings(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.OllamaUnreachable, report.Status);
        Assert.Equal(
            "Ollama did not answer at http://localhost:11434; start Ollama or fix the address in Settings, then choose Check connection.",
            report.Message);
        Assert.False(report.IsUsable);
        var question = Assert.Single(probe.ListQuestions);
        Assert.Equal(new Uri("http://localhost:11434"), question.Endpoint);
        Assert.Equal(TimeSpan.FromSeconds(5), question.Timeout);
        Assert.Empty(probe.ContextQuestions);
    }

    /// <summary>
    ///     Test that a server without the model gets the pull command in the sentence.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Ollama_ModelMissing_ReportsTheOllamaPullCommand()
    {
        // Arrange: the server has other models only
        var probe = new FakeOllamaProbe { Models = new OllamaModelList(true, ["llama3:8b", "gemma3n:latest"]) };
        var readiness = Create(probe: probe);

        // Act
        var report = await readiness.GetReportAsync(OllamaSettings("gemma3"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.OllamaModelMissing, report.Status);
        Assert.Equal("Ollama does not have the model gemma3; run `ollama pull gemma3`, then choose Check connection.", report.Message);
        Assert.False(report.IsUsable);
        Assert.Empty(probe.ContextQuestions);
    }

    /// <summary>
    ///     Test that a model name matches the listed name with the implied latest tag, with a tag, and in any letter case,
    ///     and that the context length is asked for under the name that the server lists.
    /// </summary>
    /// <param name="wanted">The name in the settings.</param>
    /// <param name="listed">The name that the server lists.</param>
    [Theory]
    [InlineData("gemma3", "gemma3:latest")]
    [InlineData("Gemma3", "gemma3:latest")]
    [InlineData("gemma3:latest", "gemma3:latest")]
    [InlineData("vision-model:7b", "vision-model:7b")]
    [InlineData("tool-model", "tool-model")]
    public async Task AgentReadiness_Ollama_ModelPresent_ReportsReadyWithTheVisionHint(string wanted, string listed)
    {
        // Arrange: a server that lists the model, with a large context
        var probe = new FakeOllamaProbe { Models = new OllamaModelList(true, ["other:1b", listed]), ContextLength = 131072 };
        var readiness = Create(probe: probe);

        // Act
        var report = await readiness.GetReportAsync(OllamaSettings(wanted), cancellationToken: TestContext.Current.CancellationToken);

        // Assert: ready with the hint, and the context question used the listed name and the limit of five seconds
        Assert.Equal(AgentReadinessStatus.Ready, report.Status);
        Assert.True(report.IsUsable);
        Assert.Equal($"Ollama is running with the model {wanted}; pictures work only with a vision-capable model.", report.Message);
        var question = Assert.Single(probe.ContextQuestions);
        Assert.Equal(listed, question.Model);
        Assert.Equal(TimeSpan.FromSeconds(5), question.Timeout);
    }

    /// <summary>
    ///     Test that a configured window of 16,383 tokens warns and 16,384 does not, that the warning is usable, and that
    ///     the sentence is the exact one.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Ollama_ConfiguredContext_WarnsAt16383AndNotAt16384()
    {
        // Arrange: a server that does not tell the model's limit
        var probe = new FakeOllamaProbe { Models = new OllamaModelList(true, ["gemma3:latest"]), ContextLength = null };
        var readiness = Create(probe: probe);
        var token = TestContext.Current.CancellationToken;

        // Act
        var small = await readiness.GetReportAsync(OllamaSettings(contextTokens: 16383), cancellationToken: token);
        var enough = await readiness.GetReportAsync(OllamaSettings(contextTokens: 16384), cancellationToken: token);

        // Assert
        Assert.Equal(AgentReadinessStatus.OllamaContextTooSmall, small.Status);
        Assert.True(small.IsUsable);
        Assert.Equal(
            "The context window (16,383 tokens) is below 16,384; drawings with images and tools may fail. Raise it in Settings (32,768 recommended).",
            small.Message);
        Assert.Equal(AgentReadinessStatus.Ready, enough.Status);
    }

    /// <summary>
    ///     Test that the smaller of the configured window and the model's own limit decides, with the right sentence.
    /// </summary>
    /// <param name="configured">The configured window.</param>
    /// <param name="detected">The model's limit; <see langword="null" /> when not known.</param>
    /// <param name="expected">The expected status.</param>
    /// <param name="sentence">The start of the expected sentence; empty when no warning.</param>
    [Theory]
    [InlineData(32768, 8192, AgentReadinessStatus.OllamaContextTooSmall, "The model supports only 8,192 tokens, below 16,384; drawings with images and tools may fail, so choose a model with a larger context window.")]
    [InlineData(32768, 16384, AgentReadinessStatus.Ready, "")]
    [InlineData(32768, 16383, AgentReadinessStatus.OllamaContextTooSmall, "The model supports only 16,383 tokens, below 16,384; drawings with images and tools may fail, so choose a model with a larger context window.")]
    [InlineData(8192, 131072, AgentReadinessStatus.OllamaContextTooSmall, "The context window (8,192 tokens) is below 16,384; drawings with images and tools may fail. Raise it in Settings (32,768 recommended).")]
    [InlineData(4096, 2048, AgentReadinessStatus.OllamaContextTooSmall, "The model supports only 2,048 tokens, below 16,384; drawings with images and tools may fail, so choose a model with a larger context window.")]
    [InlineData(32768, null, AgentReadinessStatus.Ready, "")]
    [InlineData(32768, 131072, AgentReadinessStatus.Ready, "")]
    public async Task AgentReadiness_Ollama_DetectedContext_TheSmallerOfConfiguredAndDetectedDecides(
        int configured,
        int? detected,
        AgentReadinessStatus expected,
        string sentence)
    {
        // Arrange
        var probe = new FakeOllamaProbe { Models = new OllamaModelList(true, ["gemma3:latest"]), ContextLength = detected };
        var readiness = Create(probe: probe);

        // Act
        var report = await readiness.GetReportAsync(OllamaSettings(contextTokens: configured), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, report.Status);
        if (sentence.Length > 0)
        {
            Assert.Equal(sentence, report.Message);
        }
    }

    /// <summary>
    ///     Test that the model settings are validated by the catalog of the server: a model that can do everything is ready
    ///     and named by the catalog's id, a model without tool calling and a window above the trained length are invalid,
    ///     and a model that cannot see is a usable warning that is combined with the warning for a small window.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Ollama_ModelSettings_AreValidatedByTheCatalogOfTheServer()
    {
        // Arrange: a server that lists four models, described by the catalog
        var probe = new FakeOllamaProbe
        {
            Models = new OllamaModelList(true, ["full:latest", "no-tools:latest", "small:latest", "blind:latest"]),
            ContextLength = null,
        };
        var catalog = new FakeModelCatalog(
            "ollama",
            TestModels.Make("ollama", "full:latest", windowTokens: 131_072),
            TestModels.Make("ollama", "no-tools:latest", toolCalls: false),
            TestModels.Make("ollama", "small:latest", windowTokens: 8192),
            TestModels.Make("ollama", "blind:latest", vision: false, windowTokens: 131_072));
        var readiness = Create(probe: probe, ollamaCatalogs: new FakeOllamaCatalogs(catalog));
        var token = TestContext.Current.CancellationToken;

        // Act
        var ready = await readiness.GetReportAsync(OllamaSettings("full"), cancellationToken: token);
        var noTools = await readiness.GetReportAsync(OllamaSettings("no-tools"), cancellationToken: token);
        var tooLarge = await readiness.GetReportAsync(OllamaSettings("small", contextTokens: 32768), cancellationToken: token);
        var blind = await readiness.GetReportAsync(OllamaSettings("blind"), cancellationToken: token);
        var blindAndSmall = await readiness.GetReportAsync(OllamaSettings("blind", contextTokens: 4096), cancellationToken: token);

        // Assert
        Assert.Equal((AgentReadinessStatus.Ready, "Ollama is running with the model full:latest."), (ready.Status, ready.Message));
        Assert.Equal(AgentReadinessStatus.ModelSettingsInvalid, noTools.Status);
        Assert.False(noTools.IsUsable);
        Assert.Contains("does not support tool calling", noTools.Message, StringComparison.Ordinal);
        Assert.Equal(AgentReadinessStatus.ModelSettingsInvalid, tooLarge.Status);
        Assert.Contains("supports at most 8,192 tokens; a 32,768-token window is not supported", tooLarge.Message, StringComparison.Ordinal);
        Assert.Equal(AgentReadinessStatus.ModelLacksVision, blind.Status);
        Assert.True(blind.IsUsable);
        Assert.Contains("does not accept pictures", blind.Message, StringComparison.Ordinal);
        Assert.Equal(AgentReadinessStatus.OllamaContextTooSmall, blindAndSmall.Status);
        Assert.Contains("4,096 tokens", blindAndSmall.Message, StringComparison.Ordinal);
        Assert.Contains("does not accept pictures", blindAndSmall.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the catalog's own limit is used for the small-window warning when the server did not answer the
    ///     question about the context, so that a model trained for a small window is warned about.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Ollama_NoContextAnswer_UsesTheTrainedLengthOfTheCatalog()
    {
        // Arrange: a model trained for 16,000 tokens, asked with a window that it can honor
        var probe = new FakeOllamaProbe { Models = new OllamaModelList(true, ["tiny:latest"]), ContextLength = null };
        var catalog = new FakeModelCatalog("ollama", TestModels.Make("ollama", "tiny:latest", windowTokens: 16_000));
        var readiness = Create(probe: probe, ollamaCatalogs: new FakeOllamaCatalogs(catalog));

        // Act
        var report = await readiness.GetReportAsync(OllamaSettings("tiny", contextTokens: 16_000), cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the window is the one the model supports, which is below the recommendation
        Assert.Equal(AgentReadinessStatus.OllamaContextTooSmall, report.Status);
        Assert.Contains("16,000", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the sentence for a small window is built only below the recommended minimum.
    /// </summary>
    [Fact]
    public void AgentReadiness_DescribeSmallContext_Boundaries_AreTheRecommendedMinimum()
    {
        // Assert: the boundary is 16,384 for both the configured window and the model's limit
        Assert.Equal(16384, AiProviderSettings.RecommendedMinimumContextTokens);
        Assert.NotNull(AgentReadiness.DescribeSmallContext(16383, null));
        Assert.Null(AgentReadiness.DescribeSmallContext(16384, null));
        Assert.NotNull(AgentReadiness.DescribeSmallContext(32768, 16383));
        Assert.Null(AgentReadiness.DescribeSmallContext(32768, 16384));
        Assert.NotNull(AgentReadiness.DescribeSmallContext(16383, 16383 + 100));
    }

    /// <summary>
    ///     Test that Check connection for Ollama maps the report to the result of the settings interface for a ready
    ///     server, a window warning, and a missing model.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_CheckAsync_Ollama_MapsUsabilityAndMessage()
    {
        // Arrange
        var probe = new FakeOllamaProbe { Models = new OllamaModelList(true, ["gemma3:latest"]), ContextLength = 131072 };
        IProviderCheck check = Create(probe: probe);
        var token = TestContext.Current.CancellationToken;

        // Act
        var ready = await check.CheckAsync(OllamaSettings(), token);
        var warning = await check.CheckAsync(OllamaSettings(contextTokens: 4096), token);
        var missing = await check.CheckAsync(OllamaSettings("absent"), token);

        // Assert: ready and a warning are usable; a missing model is not; messages are the report's
        Assert.True(ready.IsUsable);
        Assert.Contains("gemma3", ready.Message, StringComparison.Ordinal);
        Assert.True(warning.IsUsable);
        Assert.Contains("4,096", warning.Message, StringComparison.Ordinal);
        Assert.False(missing.IsUsable);
        Assert.Contains("ollama pull absent", missing.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every message of every status is one line and ends with a full stop, so each is one actionable
    ///     sentence for the chat banner.
    /// </summary>
    [Fact]
    public async Task AgentReadiness_Messages_Rule_AreOneLineAndEndWithAFullStop()
    {
        // Arrange: reports for every status that the checks can give
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.112");
        var token = TestContext.Current.CancellationToken;
        var messages = new List<string>
        {
            (await Create(new FakeProcessRunner(), new FakeEnvironment(null, null), new FakeCliFileSystem()).GetReportAsync(CopilotSettings, cancellationToken: token)).Message,
            (await Create(runner).GetReportAsync(CopilotSettings, cancellationToken: token)).Message,
            (await Create().GetReportAsync(CopilotSettings, cancellationToken: token)).Message,
            (await Create().GetReportAsync(OllamaSettings(""), cancellationToken: token)).Message,
            (await Create().GetReportAsync(OllamaSettings(), cancellationToken: token)).Message,
            (await Create(probe: new FakeOllamaProbe { Models = new OllamaModelList(true, []) }).GetReportAsync(OllamaSettings(), cancellationToken: token)).Message,
            (await Create(probe: new FakeOllamaProbe { Models = new OllamaModelList(true, ["gemma3:latest"]) }).GetReportAsync(OllamaSettings(contextTokens: 2048), cancellationToken: token)).Message,
        };

        // Assert
        Assert.All(messages, message =>
        {
            Assert.DoesNotContain('\n', message);
            Assert.EndsWith(".", message, StringComparison.Ordinal);
        });
    }
}

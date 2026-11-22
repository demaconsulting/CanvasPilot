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

using System.Runtime.CompilerServices;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the contract of the Agent subsystem: how a consumer runs a turn through
///     <see cref="IAgentService" /> and reads the readiness report, using a scripted service in place of an AI provider.
/// </summary>
public sealed class AgentSubsystemTests
{
    /// <summary>
    ///     Test that a consumer that runs a turn through the interface receives the events in order, ending with the
    ///     completed event, and that the service receives the message and the restored history that the request carries.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_RunTurn_ScriptedService_StreamsEventsInOrderAndReceivesTheRequest()
    {
        // Arrange: a service that answers with a scripted stream, and a request with restored history
        var token = TestContext.Current.CancellationToken;
        var payload = CreatePayload("Draw a box");
        var history = new[]
        {
            new AgentHistoryTurn(AgentHistoryRole.User, "Hello"),
            new AgentHistoryTurn(AgentHistoryRole.Assistant, "Hi, what shall we draw?"),
        };
        var scripted = new ScriptedAgentService(
            new AgentEvent.TextDelta("I will "),
            new AgentEvent.TextDelta("draw it."),
            new AgentEvent.ToolStarted("call-1", "project_write", "box.svg"),
            new AgentEvent.ToolFinished("call-1", "project_write", true, "created box.svg", false),
            new AgentEvent.FileWritten("box.svg", false),
            new AgentEvent.Usage(120, 45),
            new AgentEvent.Completed());
        IAgentService service = scripted;

        // Act: the consumer runs the turn and collects every event
        var events = new List<AgentEvent>();
        await foreach (var agentEvent in service.RunTurnAsync(new AgentTurnRequest(payload) { RestoredHistory = history }, token))
        {
            events.Add(agentEvent);
        }

        // Assert: the events arrived in order, the turn ended with Completed, and the service saw the request
        Assert.Equal(7, events.Count);
        Assert.Equal("I will draw it.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(e => e.Text)));
        Assert.Equal(
            ["project_write", "project_write"],
            events.OfType<AgentEvent.ToolStarted>().Select(e => e.Name).Concat(events.OfType<AgentEvent.ToolFinished>().Select(e => e.Name)));
        Assert.Equal("box.svg", events.OfType<AgentEvent.FileWritten>().Single().RelativePath);
        Assert.IsType<AgentEvent.Completed>(events[^1]);
        Assert.Same(payload, scripted.LastRequest!.Payload);
        Assert.Equal(history, scripted.LastRequest.RestoredHistory);
    }

    /// <summary>
    ///     Test that a turn that cannot run ends with a failed event that names why, and that no event follows it.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_RunTurn_ScriptedBusyService_EndsWithFailedEventOfKindBusy()
    {
        // Arrange: a service that refuses the turn because another one runs
        var token = TestContext.Current.CancellationToken;
        IAgentService service = new ScriptedAgentService(
            new AgentEvent.Failed(AgentFailureKind.Busy, "The copilot is still working. Press Stop to cancel it."));

        // Act: run a turn
        var events = new List<AgentEvent>();
        await foreach (var agentEvent in service.RunTurnAsync(new AgentTurnRequest(CreatePayload("again")), token))
        {
            events.Add(agentEvent);
        }

        // Assert: the only event is the failure, with the kind and the message for the user
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
        Assert.Equal(AgentFailureKind.Busy, failed.Kind);
        Assert.Contains("Stop", failed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a consumer can tell a provider that can be used, with or without a warning, from one that cannot, and
    ///     that resetting the conversation reaches the service.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_Readiness_ScriptedReports_SeparateUsableFromBlockedProviders()
    {
        // Arrange: a service whose readiness is scripted per call
        var token = TestContext.Current.CancellationToken;
        var scripted = new ScriptedAgentService();
        IAgentService service = scripted;
        var reports = new[]
        {
            new AgentReadinessReport(AgentReadinessStatus.Ready, "Ollama is ready."),
            new AgentReadinessReport(AgentReadinessStatus.OllamaContextTooSmall, "The context window is below 16,384."),
            new AgentReadinessReport(AgentReadinessStatus.CopilotRuntimeUnavailable, "Copilot support needs an AgentKit update; use Ollama meanwhile."),
        };

        // Act: read each report through the interface and reset the conversation
        var received = new List<AgentReadinessReport>();
        foreach (var report in reports)
        {
            scripted.Readiness = report;
            received.Add(await service.GetReadinessAsync(token));
        }

        service.ResetConversation();

        // Assert: the reports arrive unchanged, the first two can run a turn, the third cannot, and the reset arrived
        Assert.Equal(reports, received);
        Assert.Equal([true, true, false], received.Select(r => r.IsUsable));
        Assert.Equal(1, scripted.ResetCount);
    }

    /// <summary>
    ///     Test that the subsystem finds the Copilot CLI that the user installed on a machine that also has the VS Code shim,
    ///     explains a machine that has only the shim, and accepts a path from the settings on that machine, with the file
    ///     system, environment, and programs replaced by the layout of the development machine.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_LocateCopilotCli_MachinesWithShim_FindsRealCliAndExplainsShimOnly()
    {
        // Arrange: the development machine, and a machine that has the shim only; the real CLI answers on both disks
        var token = TestContext.Current.CancellationToken;
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        runner.AnswerVersion(DevMachine.ShimCli, "1.0.114");
        var withCli = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());
        var shimOnly = new CopilotCliLocator(
            runner,
            DevMachine.CreateFileSystem(),
            new FakeEnvironment(DevMachine.LocalAppData + @"\Elsewhere", DevMachine.ShimFolder));

        // Act: locate on both machines, and on the second with the real CLI chosen in the settings
        var found = await withCli.LocateAsync(cancellationToken: token);
        var notFound = await shimOnly.LocateAsync(cancellationToken: token);
        var chosen = await shimOnly.LocateAsync(DevMachine.RealCli, cancellationToken: token);

        // Assert: the real CLI is found and the shim is not; the shim-only machine is told so; the choice is accepted
        Assert.Equal(DevMachine.RealCli, found.Path);
        Assert.Equal(CopilotCliStatus.NotFound, notFound.Status);
        Assert.Contains("Only the VS Code copilot shim was found", notFound.Message, StringComparison.Ordinal);
        Assert.Equal(CopilotCliStatus.Found, chosen.Status);
        Assert.Equal(DevMachine.RealCli, chosen.Path);
    }

    /// <summary>
    ///     Test that the subsystem checks both providers through the interface that the settings dialog owns: GitHub
    ///     Copilot on a machine with a signed-in GitHub CLI is not usable because this build cannot start the runtime,
    ///     and Ollama with a model and a small window is usable with a warning.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_CheckProviderReadiness_BothProviders_ReportThroughTheSettingsInterface()
    {
        // Arrange: the development machine with a GitHub CLI that is signed in, and a server that has one model
        var token = TestContext.Current.CancellationToken;
        const string gh = @"C:\Program Files\GitHub CLI\gh.exe";
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        runner.Answer(gh, new ProcessResult(true, 0, "a-token-text", false));
        var environment = DevMachine.CreateEnvironment();
        environment.Set("ProgramFiles", @"C:\Program Files");
        var fileSystem = DevMachine.CreateFileSystem();
        fileSystem.AddFile(gh, 1_000);
        var probe = new FakeOllamaProbe { Models = new OllamaModelList(true, ["gemma3:latest"]), ContextLength = 131072 };
        IProviderCheck check = new AgentReadiness(
            new CopilotCliLocator(runner, fileSystem, environment),
            null,
            runner,
            environment,
            fileSystem,
            probe);

        // Act: check Copilot, then Ollama with a window that is too small
        var copilot = await check.CheckAsync(new AiProviderSettings { Provider = AiProviderKind.GitHubCopilot }, token);
        var ollama = await check.CheckAsync(
            new AiProviderSettings { Provider = AiProviderKind.Ollama, ModelName = "gemma3", ContextWindowTokens = 8192 },
            token);

        // Assert: Copilot is gated and not usable; Ollama is usable and warns about the window
        Assert.False(copilot.IsUsable);
        Assert.Equal("Copilot support needs an AgentKit update; use Ollama meanwhile.", copilot.Message);
        Assert.True(ollama.IsUsable);
        Assert.Contains("8,192", ollama.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the subsystem builds the tools of a project as the copilot gets them: only the application's own tools
    ///     (the drawing knowledge, the style tools, and the tools of a supplied pack), each pack under the one policy that
    ///     lets a file of the project be read and written and refuses the data folder, the project file, and every place
    ///     outside the project, and that a call of a tool reaches the knowledge of the application.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_ConfineTools_ProjectFolder_OffersOnlyApplicationToolsUnderTheConfiningPolicy()
    {
        // Arrange: a project folder, a registry over the real drawing knowledge, and a project pack that keeps its policy
        var token = TestContext.Current.CancellationToken;
        var project = Directory.CreateTempSubdirectory("canvaspilot-agent-tools-").FullName;
        try
        {
            var pack = new ToolTestDoubles.FakePack("project", [GuardedToolFactory.Create(() => ToolResult.Text("pong"), "project_ping", "Pings.")]);
            var registry = new AgentToolRegistry(
                KnowledgeTools.CreateAll(KnowledgeCatalog.Default),
                [ToolTestDoubles.Plain("style_get")],
                _ => [pack]);

            // Act: build the tools, call the knowledge tool, and ask the policy about the places of the project
            var tools = registry.CreateTools(new AgentToolContext(project), new HashSet<string>());
            var index = await tools[0].InvokeAsync(new AIFunctionArguments(), token);
            var policy = pack.LastPolicy!;
            var inside = policy.TryResolveWrite("drawing.svg", out _, out _);
            var data = policy.TryResolveRead(Path.Combine(".canvaspilot", "chat.json"), out _, out _);
            var projectFile = policy.TryResolveWrite("Brochure.canvaspilot", out _, out _);
            var outside = policy.TryResolveRead(Path.Combine("..", "other.svg"), out _, out _);

            // Assert: only the application's tools are offered, the knowledge tool answers from the real cards, and the
            // policy permits the project's pictures and nothing else
            Assert.Equal(["canvasnet_help", "style_get", "project_ping"], tools.Select(tool => tool.Name).ToArray());
            Assert.Contains(KnowledgeCatalog.Default.Roots[0].Id, index?.ToString(), StringComparison.Ordinal);
            Assert.True(inside);
            Assert.False(data);
            Assert.False(projectFile);
            Assert.False(outside);
        }
        finally
        {
            Directory.Delete(project, true);
        }
    }

    /// <summary>
    ///     Test that the project tools work on a real project as the copilot meets them: a drawing is written, listed, read,
    ///     replaced directly, and drawn by the real render service into a picture; nothing is kept in the project but the
    ///     drawing; every path that leaves the project or reaches the project file is refused; and the shell is told of each
    ///     write with the path of the file relative to the project.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_ProjectTools_ProjectFolder_WriteListReadAndDrawStayInsideTheProject()
    {
        // Arrange: a project folder with a project file, a secret beside it, and a registry supplied by the project tools
        var token = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("canvaspilot-agent-project-tools-").FullName;
        try
        {
            var project = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
            var projectFile = Path.Combine(project, "Brochure.canvaspilot");
            await File.WriteAllTextAsync(projectFile, "PROJECT-FILE", token);
            await File.WriteAllTextAsync(Path.Combine(root, "secret.txt"), "SECRET", token);
            var written = new List<FileWrittenEventArgs>();
            var projectTools = new ProjectTools(new ImageRenderService());
            projectTools.FileWritten += (_, args) => written.Add(args);
            var registry = new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), [], projectTools.CreatePacks);
            var tools = registry.CreateTools(new AgentToolContext(project), new HashSet<string>()).ToDictionary(tool => tool.Name);
            const string drawing = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"20\"><rect width=\"40\" height=\"20\" fill=\"#2563EB\"/></svg>";

            // Act: write a drawing, list, read it, replace it, draw it, and try to leave the project
            var create = await tools["project_write"].InvokeAsync(new AIFunctionArguments { ["path"] = "art/box.svg", ["content"] = drawing }, token);
            var list = await tools["project_list"].InvokeAsync(new AIFunctionArguments(), token);
            var read = await tools["project_read"].InvokeAsync(new AIFunctionArguments { ["path"] = "art/box.svg" }, token);
            var replace = await tools["project_write"].InvokeAsync(new AIFunctionArguments { ["path"] = "art/box.svg", ["content"] = drawing + "\n" }, token);
            var picture = await tools["render_image"].InvokeAsync(new AIFunctionArguments { ["path"] = "art/box.svg" }, token);
            var escape = await tools["project_write"].InvokeAsync(new AIFunctionArguments { ["path"] = "../planted.txt", ["content"] = "x" }, token);
            var overProjectFile = await tools["project_write"].InvokeAsync(new AIFunctionArguments { ["path"] = "Brochure.canvaspilot", ["content"] = "x" }, token);
            var readSecret = await tools["project_read"].InvokeAsync(new AIFunctionArguments { ["path"] = Path.Combine(root, "secret.txt") }, token);

            // Assert: the tools answer as designed, the shell is told of both writes, and nothing else is in the project
            Assert.Equal(
                ["canvasnet_help", "project_list", "project_read", "project_write", "render_image"],
                tools.Keys.ToArray());
            Assert.Contains("created", ToolAnswers.Text(create), StringComparison.Ordinal);
            Assert.Contains("art/box.svg (", ToolAnswers.Text(list), StringComparison.Ordinal);
            Assert.Equal(drawing, ToolAnswers.Text(read));
            Assert.Contains("replaced", ToolAnswers.Text(replace), StringComparison.Ordinal);
            Assert.NotNull(ToolAnswers.Picture(picture));
            Assert.Contains("40 x 20", ToolAnswers.Text(picture), StringComparison.Ordinal);
            Assert.DoesNotContain("created", ToolAnswers.Text(escape) + ToolAnswers.Text(overProjectFile), StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET", ToolAnswers.Text(readSecret), StringComparison.Ordinal);
            Assert.Equal([("art/box.svg", true), ("art/box.svg", false)], written.Select(args => (args.RelativePath, args.Created)).ToArray());
            Assert.False(File.Exists(Path.Combine(root, "planted.txt")));
            Assert.Equal("PROJECT-FILE", await File.ReadAllTextAsync(projectFile, token));
            Assert.Equal(
                ["Brochure.canvaspilot", "art", Path.Combine("art", "box.svg")],
                Directory.EnumerateFileSystemEntries(project, "*", SearchOption.AllDirectories).Select(entry => Path.GetRelativePath(project, entry)).Order(StringComparer.Ordinal).ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    ///     Creates a payload that holds only text.
    /// </summary>
    /// <param name="text">The text of the message.</param>
    /// <returns>The payload.</returns>
    internal static MessagePayload CreatePayload(string text)
    {
        return new MessagePayload(text, [], [], [], DateTimeOffset.UnixEpoch);
    }

    /// <summary>
    ///     An agent service that streams the events it was given and records what it was asked, in place of an AI provider.
    /// </summary>
    /// <param name="events">The events to stream for every turn.</param>
    private sealed class ScriptedAgentService(params AgentEvent[] events) : IAgentService
    {
        /// <summary>
        ///     Gets or sets the report that the next readiness check returns.
        /// </summary>
        public AgentReadinessReport Readiness { get; set; } = new(AgentReadinessStatus.Ready, "Ready.");

        /// <summary>
        ///     Gets the last request that a turn was run for.
        /// </summary>
        public AgentTurnRequest? LastRequest { get; private set; }

        /// <summary>
        ///     Gets how many times the conversation was reset.
        /// </summary>
        public int ResetCount { get; private set; }

        /// <inheritdoc />
        public Task<AgentReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Readiness);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AgentEvent> RunTurnAsync(
            AgentTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            foreach (var agentEvent in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return agentEvent;
            }
        }

        /// <inheritdoc />
        public void ResetConversation()
        {
            ResetCount++;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}

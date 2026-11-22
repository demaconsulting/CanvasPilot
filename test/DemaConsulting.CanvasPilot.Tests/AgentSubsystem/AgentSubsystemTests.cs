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
using DemaConsulting.AgentKit.Agents.ChatClient;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
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
            new AgentReadinessReport(AgentReadinessStatus.CopilotNotSignedIn, CopilotRuntimeStarter.NotSignedInMessage),
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
    ///     Copilot on a machine with a signed-in GitHub CLI is usable because the runtime starts on the installed CLI,
    ///     and Ollama with a model and a small window is usable with a warning.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_CheckProviderReadiness_BothProviders_ReportThroughTheSettingsInterface()
    {
        // Arrange: the development machine with a GitHub CLI that is signed in, a runtime that starts, and a server that has one model
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
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "Started."));
        IProviderCheck check = new AgentReadiness(
            new CopilotCliLocator(runner, fileSystem, environment),
            starter,
            runner,
            environment,
            fileSystem,
            probe);

        // Act: check Copilot, then Ollama with a window that is too small
        var copilot = await check.CheckAsync(new AiProviderSettings { Provider = AiProviderKind.GitHubCopilot }, token);
        var ollama = await check.CheckAsync(
            new AiProviderSettings { Provider = AiProviderKind.Ollama, ModelName = "gemma3", ContextWindowTokens = 8192 },
            token);

        // Assert: Copilot is ready on the CLI that was found; Ollama is usable and warns about the window
        Assert.True(copilot.IsUsable);
        Assert.Equal("GitHub Copilot CLI 1.0.114 is ready.", copilot.Message);
        Assert.Equal(DevMachine.RealCli, Assert.Single(starter.Requests).CliPath);
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
    ///     Test that the instructions and the tools agree as the copilot meets them: for a project with a kit and a few files
    ///     the full tier is chosen for a large window and the compact tier for a small one, the compact text is much smaller,
    ///     the instructions name only tools that the registry really offers, and the registry offers every tool they name.
    /// </summary>
    [Fact]
    public void AgentSubsystem_InstructTheCopilot_ProjectWithKit_InstructionsNameExactlyTheToolsThatAreOffered()
    {
        // Arrange: the real cards, the tools of the registry over them with the real project tools, and a project with a kit
        var project = Directory.CreateTempSubdirectory("canvaspilot-agent-instructions-").FullName;
        try
        {
            var kitFolder = Path.Combine(AppContext.BaseDirectory, StyleKitCatalog.BuiltInFolderName, "clean");
            var kit = StyleKitSerializer.ReadFolder(kitFolder).Kit!;
            var entry = new StyleKitEntry(kit, StyleKitLocation.BuiltIn, kitFolder, null, true, false, null, []);
            var facts = new AgentProjectFacts("Brochure", project, ["cover.svg", "flow/process.mmd"], entry, kit.Id);
            var instructions = new AgentInstructions(new KnowledgePromptBuilder(KnowledgeCatalog.Default));
            var projectTools = new ProjectTools(new ImageRenderService());
            var registry = new AgentToolRegistry(
                KnowledgeTools.CreateAll(KnowledgeCatalog.Default),
                [ToolTestDoubles.Plain("style_get"), ToolTestDoubles.Plain("style_check")],
                projectTools.CreatePacks);

            // Act: choose the tiers the way the service will, write both texts, and build the tools
            var large = AgentInstructions.ChooseTier(AiProviderKind.Ollama, AiProviderSettings.DefaultContextWindowTokens);
            var small = AgentInstructions.ChooseTier(AiProviderKind.Ollama, 8192);
            var full = instructions.Build(facts, large);
            var compact = instructions.Build(facts, small);
            var offered = registry.CreateTools(new AgentToolContext(project), new HashSet<string>()).Select(tool => tool.Name).ToHashSet();
            var named = System.Text.RegularExpressions.Regex.Matches(instructions.RolePrompt, "\\b(?:canvasnet|style|project|render)_[a-z]+\\b")
                .Select(match => match.Value)
                .ToHashSet();

            // Assert: the tiers, the sizes, the facts in both, and the agreement between the role prompt and the tools
            Assert.Equal(InstructionTier.Full, large);
            Assert.Equal(InstructionTier.Compact, small);
            Assert.True(compact.Length < full.Length / 2, $"compact {compact.Length}, full {full.Length}");
            Assert.All([full, compact], text => Assert.Contains("- Name: Brochure", text, StringComparison.Ordinal));
            Assert.All([full, compact], text => Assert.Contains("flow/process.mmd", text, StringComparison.Ordinal));
            Assert.Contains("## Example images", full, StringComparison.Ordinal);
            Assert.DoesNotContain("## Example images", compact, StringComparison.Ordinal);
            Assert.Equal(offered.Order().ToArray(), named.Order().ToArray());
        }
        finally
        {
            Directory.Delete(project, true);
        }
    }

    /// <summary>
    ///     Test that a message with marks, built by the real payload builder from a real picture, is mapped to a user message
    ///     as the model receives it: the text of the user with the block of context that has the picture by its path in the
    ///     project and the numbers of the marks, then the annotated picture and one close-up for each mark, all as real PNG
    ///     files, and no full path of the computer in the text.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_SendMessage_RealPayloadWithMarks_MapsToTextAndPngImagesInOrder()
    {
        // Arrange: a project with a picture, two marks on it, and the payload that the real builder makes from them
        var token = TestContext.Current.CancellationToken;
        var project = Directory.CreateTempSubdirectory("canvaspilot-agent-message-").FullName;
        try
        {
            var file = Path.Combine(project, "art", "logo.svg");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='32'><rect width='64' height='32' fill='#336699'/></svg>", token);
            var renderer = new ImageRenderService();
            var picture = (await renderer.RenderAsync(file, project, token)).Image!;
            var registry = new MarkRegistry();
            var first = registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(4, 4), new RenderPoint(40, 20)]);
            var second = registry.Add(picture, MarkKind.Arrow, [new RenderPoint(50, 5), new RenderPoint(30, 25)]);
            var document = new ComposerDocument();
            document.InsertChip(0, new MarkChip(first.Id, first.Number));
            document.InsertChip(1, new MarkChip(second.Id, second.Number));
            var payload = await new PayloadBuilder(new MarkImageComposer(renderer)).BuildAsync(document.Snapshot(), registry.Marks, [], token);

            // Act
            var message = PayloadMessageMapper.Map(payload, project);

            // Assert: the text, the order of the images, and that every image is a PNG file
            var text = Assert.IsType<TextContent>(message.Contents[0]).Text;
            var images = message.Contents.OfType<DataContent>().ToList();
            Assert.Equal(ChatRole.User, message.Role);
            Assert.StartsWith("[Mark 1][Mark 2]", text, StringComparison.Ordinal);
            Assert.Contains("- Picture 1: art/logo.svg (svg, 64 x 32 px)", text, StringComparison.Ordinal);
            Assert.Contains("  - Mark 1: rectangle; no note; box x=4, y=4, w=36, h=16;", text, StringComparison.Ordinal);
            Assert.Contains("  - Mark 2: arrow; no note;", text, StringComparison.Ordinal);
            Assert.Contains("Source elements: not available (svg provides no element positions)", text, StringComparison.Ordinal);
            Assert.DoesNotContain(project, text, StringComparison.Ordinal);
            Assert.Equal(["picture-1.png", "mark-1.png", "mark-2.png"], images.Select(image => image.Name ?? string.Empty).ToArray());
            Assert.All(images, image => Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, image.Data.Span[..4].ToArray()));
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
    ///     Test that a whole turn of a real agent is reported as events as the chat will meet them: a scripted model writes a
    ///     drawing, looks at it, and tries to write outside the project, with the real project tools. The events carry the text,
    ///     each call with its arguments in brief, the answer that says the file was created, the picture flag of the look, the
    ///     refusal as a call that did not succeed, and the end of the turn, and the drawing is in the project.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_RunTurn_ScriptedModelWritesAndLooksAtADrawing_EventsShowTheCreatedFileThePictureAndTheRefusal()
    {
        // Arrange: a project, the real tools, and an agent on a scripted model that asks for three tool calls and then answers
        var token = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("canvaspilot-agent-events-").FullName;
        try
        {
            var project = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
            var projectTools = new ProjectTools(new ImageRenderService());
            var registry = new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), [], projectTools.CreatePacks);
            var tools = registry.CreateTools(new AgentToolContext(project), new HashSet<string>());
            const string drawing = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"20\"><rect width=\"40\" height=\"20\" fill=\"#2563EB\"/></svg>";
            var model = new ScriptedChatClient(
                [
                    new ChatResponseUpdate(ChatRole.Assistant, "I will draw a box. "),
                    new ChatResponseUpdate(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent("w1", "project_write", new Dictionary<string, object?> { ["path"] = "art/box.svg", ["content"] = drawing }),
                            new FunctionCallContent("r1", "render_image", new Dictionary<string, object?> { ["path"] = "art/box.svg" }),
                            new FunctionCallContent("w2", "project_write", new Dictionary<string, object?> { ["path"] = "../planted.txt", ["content"] = "x" }),
                        ]),
                ],
                [new ChatResponseUpdate(ChatRole.Assistant, "The box is drawn.")]);
            var agent = ChatClientAgentFactory.Create(model, [.. tools], "You draw.", AgentProviderFactory.AgentName);
            var session = await agent.CreateSessionAsync(token);
            var mapper = new AgentEventMapper(AiProviderKind.Ollama, "gemma3", cancellationToken: token);

            // Act: run the turn and collect the events
            var events = new List<AgentEvent>();
            await foreach (var mapped in mapper.MapAsync(agent.RunStreamingAsync("Draw a box.", session, cancellationToken: token)))
            {
                events.Add(mapped);
            }

            // Assert: the text, the three calls tied by id with their summaries, the picture, and the refusal
            Assert.Equal("I will draw a box. The box is drawn.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
            var started = events.OfType<AgentEvent.ToolStarted>().ToDictionary(item => item.CallId);
            var finished = events.OfType<AgentEvent.ToolFinished>().ToDictionary(item => item.CallId);
            Assert.Equal(["r1", "w1", "w2"], started.Keys.Order(StringComparer.Ordinal).ToArray());
            Assert.Equal($"path=art/box.svg, content=({drawing.Length} characters)", started["w1"].ArgumentSummary);
            Assert.True(finished["w1"].Succeeded);
            Assert.StartsWith("File 'art/box.svg' created", finished["w1"].Summary, StringComparison.Ordinal);
            Assert.True(finished["r1"].Succeeded);
            Assert.True(finished["r1"].HasImage);
            Assert.False(finished["w2"].Succeeded);
            Assert.IsType<AgentEvent.Completed>(events[^1]);
            Assert.Equal(drawing, await File.ReadAllTextAsync(Path.Combine(project, "art", "box.svg"), token));
            Assert.False(File.Exists(Path.Combine(root, "planted.txt")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    ///     Test that a conversation with Ollama works end to end as the application will run it: the settings choose the
    ///     provider, the factory makes the agent on a stand-in server, the agent is given the real tools of a project, the
    ///     model on the server asks for a drawing to be written, and the events show the created file; the drawing is in the
    ///     project, the server got the model and the window of the settings, and nothing else is in the project.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_ConverseWithOllama_ScriptedServerAndProjectTools_WritesTheDrawingAndStreamsEvents()
    {
        // Arrange: a project, the real tools, and a stand-in server that asks for the tool and then answers
        var token = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("canvaspilot-agent-ollama-").FullName;
        try
        {
            var projectTools = new ProjectTools(new ImageRenderService());
            var registry = new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), [], projectTools.CreatePacks);
            var tools = registry.CreateTools(new AgentToolContext(root), new HashSet<string>());
            var handler = new QueuedOllamaHandler(
                string.Join(
                    "\n",
                    """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"Drawing it. "},"done":false}""",
                    """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"project_write","arguments":{"path":"box.svg","content":"<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'/>"}}}]},"done":false}""",
                    """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop"}"""),
                """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"Done."},"done":true,"done_reason":"stop"}""");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.Ollama,
                OllamaEndpoint = "http://localhost:11434",
                ModelName = "gemma3:12b",
                ContextWindowTokens = 32768,
            };
            await using var factory = new AgentProviderFactory(handler: handler);

            // Act: make the agent from the settings and run a turn through the mapper
            await using var made = await factory.CreateAsync(new AgentCreationRequest(settings, tools, "You draw.", root), token);
            var session = await made.Agent!.CreateSessionAsync(token);
            var mapper = new AgentEventMapper(settings.Provider, settings.ModelName, cancellationToken: token);
            var events = new List<AgentEvent>();
            await foreach (var mapped in mapper.MapAsync(made.Agent.RunStreamingAsync("Draw a box.", session, cancellationToken: token)))
            {
                events.Add(mapped);
            }

            // Assert: the events show the created file, the drawing is in the project, and the server got the settings
            Assert.Equal("Drawing it. Done.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
            var finished = Assert.Single(events.OfType<AgentEvent.ToolFinished>());
            Assert.Equal(("project_write", true), (finished.Name, finished.Succeeded));
            Assert.StartsWith("File 'box.svg' created", finished.Summary, StringComparison.Ordinal);
            Assert.IsType<AgentEvent.Completed>(events[^1]);
            Assert.Contains("width='40'", await File.ReadAllTextAsync(Path.Combine(root, "box.svg"), token), StringComparison.Ordinal);
            Assert.Equal(["box.svg"], Directory.EnumerateFileSystemEntries(root).Select(entry => Path.GetFileName(entry)).ToArray());
            Assert.All(handler.Requests, request =>
            {
                Assert.Contains("\"model\":\"gemma3:12b\"", request.Body, StringComparison.Ordinal);
                Assert.Contains("\"num_ctx\":32768", request.Body, StringComparison.Ordinal);
            });
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    ///     Test that GitHub Copilot works as the application will meet it: the factory with a starter that refuses the runtime
    ///     gives the typed answer and no agent, while a starter that runs a runtime gives an agent that is made on it with
    ///     exactly the tools of the project and the project folder as its working directory.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_ConverseWithCopilot_RefusingAndRunningStarter_RefusalGivesNoAgentAndRuntimeGivesAnAgentWithTheProjectTools()
    {
        // Arrange: the development machine with the real CLI, the tools of a project, and two factories
        var token = TestContext.Current.CancellationToken;
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());
        var root = Directory.CreateTempSubdirectory("canvaspilot-agent-copilot-").FullName;
        try
        {
            var projectTools = new ProjectTools(new ImageRenderService());
            var registry = new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), [], projectTools.CreatePacks);
            var tools = registry.CreateTools(new AgentToolContext(root), new HashSet<string>());
            var request = new AgentCreationRequest(new AiProviderSettings(), tools, "You draw.", root);
            var source = new FakeCopilotAgentSource(new ScriptedChatClient());
            var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "Started.", source));
            var refusing = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.NotSignedIn, "Sign in first."));
            await using var refused = new AgentProviderFactory(locator, refusing);
            await using var started = new AgentProviderFactory(locator, starter);

            // Act
            var withoutRuntime = await refused.CreateAsync(request, token);
            await using var withRuntime = await started.CreateAsync(request, token);

            // Assert: the refusal gives the typed answer, and the runtime gets exactly the tools and the folder of the project
            Assert.Null(withoutRuntime.Agent);
            Assert.Equal(AgentReadinessStatus.CopilotNotSignedIn, withoutRuntime.Failure!.Status);
            Assert.NotNull(withRuntime.Agent);
            var made = Assert.Single(source.Requests);
            Assert.Equal(tools.Select(tool => tool.Name).ToArray(), made.Tools.Select(tool => tool.Name).ToArray());
            Assert.Equal(root, made.WorkingDirectory);
            Assert.Null(made.Model);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    ///     Test that a conversation with Ollama works end to end through the agent service as the application will run it:
    ///     the service makes the agent of the open project on a stand-in server through the real factory, the model asks for
    ///     a drawing to be written, and the events show the answer, the call, the file, and the end; the drawing is in the
    ///     project, the shell was told, the server got the model and the window of the settings, and the project holds
    ///     nothing besides the drawing and its project file.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_ConverseWithOllama_ServiceOnStandInServer_WritesTheDrawingAndKeepsTheProjectClean()
    {
        // Arrange: the service on the real factory over a stand-in server that asks for the tool and then answers
        var token = TestContext.Current.CancellationToken;
        var handler = new QueuedOllamaHandler(
            string.Join(
                "\n",
                """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"Drawing it. "},"done":false}""",
                """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"project_write","arguments":{"path":"box.svg","content":"<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'/>"}}}]},"done":false}""",
                """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop"}"""),
            """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"Done."},"done":true,"done_reason":"stop"}""");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Ollama,
            OllamaEndpoint = "http://localhost:11434",
            ModelName = "gemma3:12b",
            ContextWindowTokens = 32768,
        };
        using var rig = new AgentServiceRig(new ScriptedChatClient(), factory: new AgentProviderFactory(handler: handler), settings: settings);
        var told = new List<string>();
        rig.Service.FileWritten += (_, args) => told.Add(args.RelativePath);
        var before = AgentServiceRig.Snapshot(rig.ProjectFolder);

        // Act: run a turn through the service
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(CreatePayload("Draw a box.")), token), token);

        // Assert: the events show the answer, the call, the file, and the end, and the shell was told of the file
        Assert.Equal("Drawing it. Done.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
        Assert.Equal(("project_write", true), (Assert.Single(events.OfType<AgentEvent.ToolFinished>()).Name, true));
        Assert.Equal(new AgentEvent.FileWritten("box.svg", false), Assert.Single(events.OfType<AgentEvent.FileWritten>()));
        Assert.IsType<AgentEvent.Completed>(events[^1]);
        Assert.Equal(["box.svg"], told);

        // Assert: the drawing is in the project and nothing else was added to it, and the server got the settings
        Assert.Contains("width='40'", await File.ReadAllTextAsync(Path.Combine(rig.ProjectFolder, "box.svg"), token), StringComparison.Ordinal);
        Assert.Equal(before.Append("box.svg").Order(StringComparer.Ordinal).ToArray(), AgentServiceRig.Snapshot(rig.ProjectFolder));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Contains("\"model\":\"gemma3:12b\"", request.Body, StringComparison.Ordinal);
            Assert.Contains("\"num_ctx\":32768", request.Body, StringComparison.Ordinal);
        });
    }

    /// <summary>
    ///     Test that the service on the starter of a runtime that is not signed in answers a turn for GitHub Copilot with
    ///     one failure that carries the sign-in sentence, without making an agent, and that the log says why without any
    ///     words of the message.
    /// </summary>
    [Fact]
    public async Task AgentSubsystem_ConverseWithCopilot_ServiceWithNotSignedInStarter_TurnFailsWithTheSignInSentence()
    {
        // Arrange: the development machine with a good CLI, the real factory with a starter that is not signed in
        var token = TestContext.Current.CancellationToken;
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());
        var starter = new CopilotRuntimeStarter(
            Path.Combine(Path.GetTempPath(), "canvaspilot-service-data"),
            (_, _) => throw new InvalidOperationException("The Copilot runtime started but is not signed in to GitHub, so AgentKit stopped it."));
        using var rig = new AgentServiceRig(
            new ScriptedChatClient(),
            factory: new AgentProviderFactory(locator, starter, ownsStarter: true),
            settings: new AiProviderSettings { Provider = AiProviderKind.GitHubCopilot });

        // Act
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(CreatePayload("Draw a box.")), token), token);

        // Assert: one typed failure with the sign-in sentence
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
        Assert.Equal((AgentFailureKind.NotReady, CopilotRuntimeStarter.NotSignedInMessage), (failed.Kind, failed.Message));
        Assert.DoesNotContain("Draw a box", rig.ReadLog(), StringComparison.Ordinal);
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
        public event EventHandler<FileWrittenEventArgs>? FileWritten
        {
            add
            {
                // The scripted service never writes a file, so nothing is raised.
            }

            remove
            {
                // See the add accessor.
            }
        }

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

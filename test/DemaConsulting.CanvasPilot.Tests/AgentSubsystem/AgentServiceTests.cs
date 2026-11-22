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
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for <see cref="AgentService" />: the lifecycle of the conversation of a project, the turn with its events, the
///     single running turn, cancellation, reset and disposal, the places where the copilot's data lives, and the log.
/// </summary>
public sealed class AgentServiceTests
{
    /// <summary>
    ///     A sentence of a user that must never reach the log.
    /// </summary>
    private const string SentinelMessage = "SENTINEL-MESSAGE-3f9a";

    /// <summary>
    ///     A sentence of the model that must never reach the log.
    /// </summary>
    private const string SentinelReply = "SENTINEL-REPLY-81c2";

    /// <summary>
    ///     The content of a file that the model writes, which must never reach the log.
    /// </summary>
    private const string SentinelFile = "SENTINEL-FILE-5d7e";

    /// <summary>
    ///     A sentence of the restored history that must never reach the log.
    /// </summary>
    private const string SentinelHistory = "SENTINEL-HISTORY-92b4";

    /// <summary>
    ///     The text of an exception that must never reach the log.
    /// </summary>
    private const string SentinelException = "SENTINEL-EXCEPTION-6a10";

    /// <summary>
    ///     A drawing of a box that the model writes.
    /// </summary>
    private const string Box = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"20\"><rect width=\"40\" height=\"20\"/></svg>";

    /// <summary>
    ///     Test that nothing is made before the first turn: no agent, no tools, no runtime, and that a turn makes exactly one
    ///     agent with the tools, the instructions, and the project folder of the open project.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_FirstTurn_StartsLazilyWithTheToolsAndInstructionsOfTheProject()
    {
        // Arrange: a service on a scripted model
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "Hello.")]));

        // Assert: before a turn the factory was not asked, not even by the readiness check
        _ = await rig.Service.GetReadinessAsync(token);
        Assert.Empty(rig.Factory.Requests);

        // Act: run the first turn
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Hi")), token), token);

        // Assert: one agent was made for the project, with the tools, the instructions that name the project, and no restore
        var request = Assert.Single(rig.Factory.Requests);
        Assert.Equal(rig.ProjectFolder, request.ProjectFolder);
        Assert.Equal(AiProviderKind.Ollama, request.Settings.Provider);
        Assert.Contains(request.Tools, tool => tool.Name == "project_write");
        Assert.Contains(request.Tools, tool => tool.Name == "canvasnet_help");
        Assert.Contains("- Name: Brochure", request.Instructions, StringComparison.Ordinal);
        Assert.Equal(["Hello."], events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text).ToArray());
        Assert.IsType<AgentEvent.Completed>(events[^1]);
    }

    /// <summary>
    ///     Test that a turn on a scripted Ollama-style model runs end to end: the message reaches the model, the answer is
    ///     streamed, the tool call writes the file, the file event comes between the start and the finish of the call, the
    ///     service tells the shell, and the turn completes.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_ScriptedModelWritesAFile_StreamsTextCallFileAndCompletion()
    {
        // Arrange: a model that talks, asks to write a drawing, and then says it is done
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient(
            [
                new ChatResponseUpdate(ChatRole.Assistant, "Drawing a box. "),
                new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new FunctionCallContent("w1", "project_write", new Dictionary<string, object?> { ["path"] = "art/box.svg", ["content"] = Box })]),
            ],
            [new ChatResponseUpdate(ChatRole.Assistant, "Done.")]);
        using var rig = new AgentServiceRig(client);
        var shellEvents = new List<FileWrittenEventArgs>();
        rig.Service.FileWritten += (_, args) => shellEvents.Add(args);

        // Act: run the turn
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Draw a box")), token), token);

        // Assert: the text, then the call, the file, the answer of the call, more text, and completion, in that order
        Assert.Equal("Drawing a box. Done.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
        var started = events.FindIndex(item => item is AgentEvent.ToolStarted);
        var written = events.FindIndex(item => item is AgentEvent.FileWritten);
        var finished = events.FindIndex(item => item is AgentEvent.ToolFinished);
        Assert.True(started >= 0 && started < written && written < finished, $"order {started}, {written}, {finished}");
        Assert.Equal(new AgentEvent.FileWritten("art/box.svg", false), events[written]);
        Assert.True(((AgentEvent.ToolFinished)events[finished]).Succeeded);
        Assert.IsType<AgentEvent.Completed>(events[^1]);
        Assert.Equal(Box, await File.ReadAllTextAsync(Path.Combine(rig.ProjectFolder, "art", "box.svg"), token));
        var told = Assert.Single(shellEvents);
        Assert.Equal(("art/box.svg", true), (told.RelativePath, told.Created));

        // Assert: the message of the user reached the model, after the instructions
        Assert.Contains(client.Requests[0], message => message.Role == ChatRole.User && message.Text.Contains("Draw a box", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that the conversation of a project is kept for the next turns: one agent, and the second request to the model
    ///     carries the first exchange.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_SameProjectTwice_KeepsOneAgentAndTheSession()
    {
        // Arrange: a model that answers twice
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient(
            [new ChatResponseUpdate(ChatRole.Assistant, "First.")],
            [new ChatResponseUpdate(ChatRole.Assistant, "Second.")]);
        using var rig = new AgentServiceRig(client);

        // Act: two turns
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("alpha-question")), token), token);
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("beta-question")), token), token);

        // Assert: one agent, and the second request holds the first question and answer before the second question
        Assert.Single(rig.Factory.Requests);
        Assert.Empty(rig.Factory.DeletedSessions);
        var second = client.Requests[1].Select(message => message.Text).ToList();
        Assert.Contains(second, text => text.Contains("alpha-question", StringComparison.Ordinal));
        Assert.Contains("First.", second);
        Assert.Contains("beta-question", second[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that each open project has its own agent: switching to another project drops the first conversation and
    ///     deletes its session, and the next turn makes an agent for the other project's folder.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_ProjectChanges_MakesAgentPerProjectAndDeletesTheOldSession()
    {
        // Arrange: a turn in the first project
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "A.")], [new ChatResponseUpdate(ChatRole.Assistant, "B.")]);
        using var rig = new AgentServiceRig(client);
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("alpha-question")), token), token);
        var other = rig.CreateOtherProject("Poster");

        // Act: open the other project and run a turn there
        rig.SwitchProject(other);
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("beta-question")), token), token);

        // Assert: two agents, the second for the other folder with its own name in the instructions
        Assert.Equal([rig.ProjectFolder, other.FolderPath], rig.Factory.Requests.Select(request => request.ProjectFolder).ToArray());
        Assert.Contains("- Name: Poster", rig.Factory.Requests[1].Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain(client.Requests[1], message => message.Text.Contains("alpha-question", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that closing the project drops the conversation and that the next turn says that no project is open.
    /// </summary>
    [Fact]
    public async Task AgentService_ProjectClosed_DropsTheConversationAndTheNextTurnNeedsAProject()
    {
        // Arrange: a conversation in an open project
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "A.")]));
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);

        // Act: close the project and ask again
        rig.SwitchProject(null);
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Two")), token), token);

        // Assert: one failure that says so, and no new agent
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
        Assert.Equal((AgentFailureKind.NotReady, AgentService.NoProjectMessage), (failed.Kind, failed.Message));
        Assert.Single(rig.Factory.Requests);
    }

    /// <summary>
    ///     Test that a change of a setting that decides the agent rebuilds the conversation and that a change of any other
    ///     setting does not.
    /// </summary>
    /// <param name="change">The name of the change.</param>
    /// <param name="rebuilds">Whether the agent is made again.</param>
    [Theory]
    [InlineData("provider", true)]
    [InlineData("model", true)]
    [InlineData("endpoint", true)]
    [InlineData("context", true)]
    [InlineData("cli", true)]
    [InlineData("reasoning", false)]
    [InlineData("unrestricted", false)]
    [InlineData("same", false)]
    public async Task AgentService_SettingsChanged_ByKind_RebuildsOnlyForTheSettingsThatDecideTheAgent(string change, bool rebuilds)
    {
        // Arrange: a conversation
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "A.")], [new ChatResponseUpdate(ChatRole.Assistant, "B.")]);
        using var rig = new AgentServiceRig(client);
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);

        // Act: change the setting and run the next turn
        rig.ChangeAiSettings(settings => change switch
        {
            "provider" => settings with { Provider = AiProviderKind.GitHubCopilot },
            "model" => settings with { ModelName = "llama3.2" },
            "endpoint" => settings with { OllamaEndpoint = "http://other:11434" },
            "context" => settings with { ContextWindowTokens = 16384 },
            "cli" => settings with { CopilotCliPath = @"C:\tools\copilot.exe" },
            "reasoning" => settings with { ReasoningLevel = ReasoningLevel.High },
            "unrestricted" => settings with { UnrestrictedAgentTools = true },
            _ => settings,
        });
        if (rebuilds)
        {
            await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);
        }

        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Two")), token), token);

        // Assert: a second agent for the setting that matters, the same agent otherwise
        Assert.Equal(rebuilds ? 2 : 1, rig.Factory.Requests.Count);
        Assert.Equal(rebuilds ? 1 : 0, rig.Factory.DeletedSessions.Count);
    }

    /// <summary>
    ///     Test that a request made while a turn runs gets one failure of the kind busy, that the running turn goes on and
    ///     completes, and that the next turn runs.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_RequestsWhileATurnRuns_FailBusyAndTheRunningTurnCompletes()
    {
        // Arrange: a model that waits until it is released
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((number, ct) => number == 1
            ? ProgrammableChatClient.Hang(entered, release.Task, "finished", ct)
            : ProgrammableChatClient.Say("again"));
        using var rig = new AgentServiceRig(client);
        var first = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);
        await entered.Task.WaitAsync(token);

        // Act: five more requests while the first runs, then release it
        var busy = new List<List<AgentEvent>>();
        for (var i = 0; i < 5; i++)
        {
            busy.Add(await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Two")), token), token));
        }

        release.SetResult();
        var firstEvents = await first;
        var next = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Three")), token), token);

        // Assert: each busy request has one failure and nothing else, the first turn finished, and the next turn works
        Assert.All(busy, events =>
        {
            var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
            Assert.Equal((AgentFailureKind.Busy, AgentService.BusyMessage), (failed.Kind, failed.Message));
        });
        Assert.Equal("working finished", string.Concat(firstEvents.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
        Assert.IsType<AgentEvent.Completed>(firstEvents[^1]);
        Assert.IsType<AgentEvent.Completed>(next[^1]);
        Assert.Equal(2, client.Requests.Count);
    }

    /// <summary>
    ///     Test that canceling the token of a running turn ends the stream with neither a failure nor a completion, and
    ///     that the service takes the next turn.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_TokenCancelled_EndsWithoutFailureOrCompletionAndAllowsTheNextTurn()
    {
        // Arrange: a model that never finishes by itself
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((number, ct) => number == 1
            ? ProgrammableChatClient.Hang(entered, never.Task, "late", ct)
            : ProgrammableChatClient.Say("after"));
        using var rig = new AgentServiceRig(client);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var turn = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), stop.Token), token);
        await entered.Task.WaitAsync(token);

        // Act: stop the turn, then run another
        await stop.CancelAsync();
        var stopped = await turn;
        var next = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Two")), token), token);

        // Assert: the partial text is there, nothing ends the turn, and the next turn is not busy
        Assert.Equal("working ", string.Concat(stopped.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
        Assert.DoesNotContain(stopped, item => item is AgentEvent.Failed or AgentEvent.Completed);
        Assert.IsType<AgentEvent.Completed>(next[^1]);
    }

    /// <summary>
    ///     Test that resetting the conversation deletes its session, and that the next turn starts a new agent that does not
    ///     know the earlier exchange.
    /// </summary>
    [Fact]
    public async Task AgentService_ResetConversation_AfterATurn_DeletesTheSessionAndStartsAFreshAgent()
    {
        // Arrange: a conversation with one exchange
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "A.")], [new ChatResponseUpdate(ChatRole.Assistant, "B.")]);
        using var rig = new AgentServiceRig(client);
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Remember this")), token), token);

        // Act: reset, and ask again
        rig.Service.ResetConversation();
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Next")), token), token);

        // Assert: a second agent, and the second request does not hold the first exchange
        Assert.Equal(2, rig.Factory.Requests.Count);
        Assert.DoesNotContain(client.Requests[1], message => message.Text.Contains("Remember this", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a reset while a turn runs stops that turn without a failure, and that the session is deleted once the
    ///     turn is over.
    /// </summary>
    [Fact]
    public async Task AgentService_ResetConversation_WhileATurnRuns_StopsItAndDeletesTheSession()
    {
        // Arrange: a turn that waits
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((_, ct) => ProgrammableChatClient.Hang(entered, never.Task, "late", ct));
        using var rig = new AgentServiceRig(client);
        var turn = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);
        await entered.Task.WaitAsync(token);

        // Act: reset
        rig.Service.ResetConversation();
        var events = await turn;
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);

        // Assert: no failure and no completion, and one deleted session
        Assert.DoesNotContain(events, item => item is AgentEvent.Failed or AgentEvent.Completed);
        Assert.Single(rig.Factory.DeletedSessions);
    }

    /// <summary>
    ///     Test that disposing the service stops a running turn, deletes the session, disposes the factory once (which stops
    ///     the runtime), ignores a second disposal and later events, and refuses later turns with a failure.
    /// </summary>
    [Fact]
    public async Task AgentService_Dispose_WithARunningTurn_StopsItReleasesTheConversationAndDisposesTheFactoryOnce()
    {
        // Arrange: a turn that waits
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((_, ct) => ProgrammableChatClient.Hang(entered, never.Task, "late", ct));
        using var rig = new AgentServiceRig(client);
        var turn = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);
        await entered.Task.WaitAsync(token);

        // Act: dispose twice, raise the events of the services, and ask again
        rig.Service.Dispose();
        rig.Service.Dispose();
        rig.SwitchProject(null);
        rig.ChangeAiSettings(settings => settings with { ModelName = "other" });
        var events = await turn;
        var late = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Two")), token), token);

        // Assert: the turn ended without a failure, the session was deleted once, the factory was disposed once, and later turns fail
        Assert.DoesNotContain(events, item => item is AgentEvent.Failed or AgentEvent.Completed);
        Assert.Single(rig.Factory.DeletedSessions);
        Assert.Equal(1, rig.Factory.DisposeCount);
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(late));
        Assert.Equal((AgentFailureKind.NotReady, AgentService.ClosingMessage), (failed.Kind, failed.Message));
    }

    /// <summary>
    ///     Test that a provider that cannot make an agent gives one failure of the kind not ready with the sentence of the
    ///     readiness check, and that the next turn asks again so that a fix is noticed.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_FactoryHasNoAgent_FailsNotReadyWithTheSentenceAndAsksAgain()
    {
        // Arrange: a factory that cannot make agents
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "Now it works.")]));
        rig.Factory.Failure = new AgentReadinessReport(AgentReadinessStatus.NotConfigured, "Enter the name of an Ollama model in Settings.");

        // Act: a turn that fails, then a turn after the fix
        var failed = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);
        rig.Factory.Failure = null;
        var fixedEvents = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Two")), token), token);

        // Assert
        var failure = Assert.IsType<AgentEvent.Failed>(Assert.Single(failed));
        Assert.Equal((AgentFailureKind.NotReady, "Enter the name of an Ollama model in Settings."), (failure.Kind, failure.Message));
        Assert.IsType<AgentEvent.Completed>(fixedEvents[^1]);
        Assert.Equal(2, rig.Factory.Requests.Count);
    }

    /// <summary>
    ///     Test that a turn without an open project fails with a sentence that says to open one.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_NoProjectOpen_FailsNotReady()
    {
        // Arrange: no project is open
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient());
        rig.SwitchProject(null);

        // Act
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);

        // Assert: one failure, and nothing was made
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
        Assert.Equal((AgentFailureKind.NotReady, AgentService.NoProjectMessage), (failed.Kind, failed.Message));
        Assert.Empty(rig.Factory.Requests);
    }

    /// <summary>
    ///     Test that the readiness check follows the current settings of the service and reports them as the readiness
    ///     check does.
    /// </summary>
    [Fact]
    public async Task AgentService_GetReadiness_OllamaSettings_ReportsWhatTheProbeSees()
    {
        // Arrange: a server without the model, then with it
        var token = TestContext.Current.CancellationToken;
        var probe = new FakeOllamaProbe { Models = new OllamaModelList(true, ["other:latest"]) };
        using var rig = new AgentServiceRig(new ScriptedChatClient(), probe);

        // Act
        var missing = await rig.Service.GetReadinessAsync(token);
        probe.Models = new OllamaModelList(true, ["gemma3:12b"]);
        var ready = await rig.Service.GetReadinessAsync(token);

        // Assert
        Assert.Equal(AgentReadinessStatus.OllamaModelMissing, missing.Status);
        Assert.Equal(AgentReadinessStatus.Ready, ready.Status);
    }

    /// <summary>
    ///     Test that the restored history reaches the model once, as one block at the start of the first message of a new
    ///     provider session, within its size limit, and that the second turn does not repeat it.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_RestoredHistory_IsPrefixedOnceAsOneBoundedBlock()
    {
        // Arrange: a long restored conversation and two turns that both carry it
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "A.")], [new ChatResponseUpdate(ChatRole.Assistant, "B.")]);
        using var rig = new AgentServiceRig(client);
        var history = Enumerable.Range(1, 120)
            .Select(i => new AgentHistoryTurn(i % 2 == 1 ? AgentHistoryRole.User : AgentHistoryRole.Assistant, $"turn {i} " + new string('x', 400)))
            .ToArray();
        var request = new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Continue")) { RestoredHistory = history };

        // Act
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(request, token), token);
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(request, token), token);

        // Assert: the first user message starts with one block of the digest, the second request has it once, from the first
        var firstUser = client.Requests[0].Last(message => message.Role == ChatRole.User);
        var block = Assert.IsType<TextContent>(firstUser.Contents[0]).Text;
        Assert.StartsWith(RestoredHistoryDigest.Header, block, StringComparison.Ordinal);
        Assert.True(block.TrimEnd().Length <= RestoredHistoryDigest.MaxCharacters, $"length {block.Length}");
        Assert.Contains("turn 120", block, StringComparison.Ordinal);
        Assert.DoesNotContain("turn 1 ", block, StringComparison.Ordinal);
        Assert.Contains("earlier turns were left out", block, StringComparison.Ordinal);
        Assert.Equal(1, client.Requests[1].Count(message => message.Text.Contains(RestoredHistoryDigest.Header, StringComparison.Ordinal)));
        Assert.DoesNotContain(RestoredHistoryDigest.Header, client.Requests[1][^1].Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a first turn that fails before the provider answered does not use up the restored history, so that the
    ///     next turn of the same session carries it, and that a reset makes a new session carry it again.
    /// </summary>
    [Fact]
    public async Task AgentService_RunTurn_FirstTurnFailsBeforeAnswer_RestoredHistoryIsStillPending()
    {
        // Arrange: a model that fails the first request and answers the next ones
        var token = TestContext.Current.CancellationToken;
        var client = new ProgrammableChatClient((number, _) => number == 1 ? FailBeforeAnswering() : ProgrammableChatClient.Say("ok"));
        using var rig = new AgentServiceRig(client);
        var request = new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Go"))
        {
            RestoredHistory = [new AgentHistoryTurn(AgentHistoryRole.User, "earlier words")],
        };

        // Act
        var failed = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(request, token), token);
        var worked = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(request, token), token);

        // Assert: the first turn failed, and the second carried the block to the model that had not answered yet
        Assert.IsType<AgentEvent.Failed>(failed[^1]);
        Assert.IsType<AgentEvent.Completed>(worked[^1]);
        Assert.Contains(client.Requests[1].Where(message => message.Role == ChatRole.User), message => message.Text.Contains("earlier words", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Makes an answer that ends with an exception before it says anything.
    /// </summary>
    /// <returns>The updates.</returns>
    private static async IAsyncEnumerable<ChatResponseUpdate> FailBeforeAnswering()
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, string.Empty);
        await Task.Yield();
        throw new InvalidOperationException("The model could not be reached.");
    }

    /// <summary>
    ///     Test that the data folder of the Copilot runtime that the service passes to the factory lies below the injected
    ///     local data root, has the documented name, and is not inside the project folder; and that the default root is the
    ///     local application data of the user.
    /// </summary>
    [Fact]
    public async Task AgentService_DataResidency_CopilotDataDirectory_IsBelowTheLocalDataRootAndOutsideTheProject()
    {
        // Arrange: a service with an injected root outside the project
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "A.")]));

        // Act
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);

        // Assert: the factory was told the explicit folder, which is outside the project
        var directory = Assert.Single(rig.Factory.Requests).CopilotDataDirectory;
        Assert.Equal(Path.Combine(rig.LocalDataRoot, "CanvasPilot", "copilot"), directory);
        Assert.False(AgentService.IsInside(directory!, rig.ProjectFolder));
        Assert.True(AgentService.IsInside(directory!, rig.LocalDataRoot));

        // Assert: without an injected root the folder is below the local application data of the user, never below the project
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(local, "CanvasPilot", "copilot"), AgentService.GetCopilotDataDirectory());
        Assert.Equal(Path.Combine(local, "CanvasPilot", "history"), AgentService.GetHistoryDirectory());
        Assert.Equal(local, AgentService.GetDefaultLocalDataRoot());
    }

    /// <summary>
    ///     Test that a turn is refused with a typed failure when the folder of the application's data lies inside the
    ///     project, and that nothing is made and nothing is written to the project.
    /// </summary>
    /// <param name="nestedRoot">Whether the local data root is inside the project; otherwise the history root is.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AgentService_DataResidency_DataRootInsideProject_RefusesTheTurnAndWritesNothing(bool nestedRoot)
    {
        // Arrange: a service whose local data root or history root is inside the project folder
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient(), nestLocalDataRoot: nestedRoot);
        using var nestedHistory = nestedRoot ? null : new AgentService(
            rig.Projects,
            rig.Settings,
            new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), []),
            new AgentInstructions(new KnowledgePromptBuilder(KnowledgeCatalog.Default)),
            rig.Factory,
            new AgentReadiness(ollama: rig.Probe),
            localDataRoot: rig.LocalDataRoot,
            historyRoot: Path.Combine(rig.ProjectFolder, "history"));
        var service = nestedHistory ?? rig.Service;
        var before = AgentServiceRig.Snapshot(rig.ProjectFolder);

        // Act
        var events = await AgentServiceRig.CollectAsync(service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);

        // Assert: one typed failure that names the folder, no agent, and the project folder is unchanged
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
        Assert.Equal(AgentFailureKind.DataInsideProject, failed.Kind);
        Assert.Contains(rig.ProjectFolder, failed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(rig.Factory.Requests);
        Assert.Equal(before, AgentServiceRig.Snapshot(rig.ProjectFolder));
    }

    /// <summary>
    ///     Test that a project that contains the whole local data of the user (for example a project in the user profile)
    ///     is refused, because the Copilot data folder would then lie inside it.
    /// </summary>
    [Fact]
    public async Task AgentService_DataResidency_ProjectContainsTheLocalDataRoot_RefusesTheTurn()
    {
        // Arrange: a project folder that is the parent of the local data root
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient());
        rig.SwitchProject(AgentServiceRig.MakeProject(rig.Root, "Everything"));

        // Act
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);

        // Assert
        Assert.Equal(AgentFailureKind.DataInsideProject, Assert.IsType<AgentEvent.Failed>(Assert.Single(events)).Kind);
        Assert.Empty(rig.Factory.Requests);
    }

    /// <summary>
    ///     Test that a whole turn that writes nothing leaves the project folder exactly as it was, and that the service
    ///     itself creates nothing in the local data root.
    /// </summary>
    [Fact]
    public async Task AgentService_DataResidency_TurnThatWritesNothing_LeavesTheProjectFolderUnchanged()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient([new ChatResponseUpdate(ChatRole.Assistant, "Nothing to draw.")]));
        var before = AgentServiceRig.Snapshot(rig.ProjectFolder);

        // Act: a turn, a reset, and a switch of the project
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Hello")) { RestoredHistory = [new AgentHistoryTurn(AgentHistoryRole.User, "Before")] }, token), token);
        rig.Service.ResetConversation();
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);

        // Assert: the project folder is unchanged, and the service created no data of its own
        Assert.Equal(before, AgentServiceRig.Snapshot(rig.ProjectFolder));
        Assert.False(Directory.Exists(rig.LocalDataRoot));
    }

    /// <summary>
    ///     Test that the folders are compared by whole names and without regard to case.
    /// </summary>
    /// <param name="root">The folder that is tested.</param>
    /// <param name="container">The folder that may contain it.</param>
    /// <param name="expected">Whether the first is inside the second.</param>
    [Theory]
    [InlineData(@"C:\work\proj\data", @"C:\work\proj", true)]
    [InlineData(@"C:\work\proj", @"C:\work\proj", true)]
    [InlineData(@"C:\work\proj\", @"C:\work\proj", true)]
    [InlineData(@"C:\WORK\Proj\Data", @"c:\work\proj", true)]
    [InlineData(@"C:\work\proj2", @"C:\work\proj", false)]
    [InlineData(@"C:\work", @"C:\work\proj", false)]
    [InlineData(@"D:\work\proj", @"C:\work\proj", false)]
    [InlineData(@"C:\work\proj\..\other", @"C:\work\proj", false)]
    public void AgentService_IsInside_Paths_ComparesWholeNamesWithoutCase(string root, string container, bool expected)
    {
        // Act and assert
        Assert.Equal(expected, AgentService.IsInside(root, container));
        Assert.Throws<ArgumentException>(() => AgentService.IsInside(" ", container));
        Assert.Throws<ArgumentException>(() => AgentService.IsInside(root, string.Empty));
    }

    /// <summary>
    ///     Test that the log of a whole turn holds metadata only: the lifecycle lines are there, and none of the words of the
    ///     user, the model, the restored history, the tool's arguments, the file's content, or an exception appear in it.
    /// </summary>
    [Fact]
    public async Task AgentService_Logging_TurnWithSentinelTexts_WritesMetadataOnly()
    {
        // Arrange: a model that answers with a sentinel, writes a file with a sentinel, and a user with sentinels
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient(
            [
                new ChatResponseUpdate(ChatRole.Assistant, SentinelReply),
                new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new FunctionCallContent("w1", "project_write", new Dictionary<string, object?> { ["path"] = "note.txt", ["content"] = SentinelFile })]),
            ],
            [new ChatResponseUpdate(ChatRole.Assistant, SentinelReply + " done")]);
        using var rig = new AgentServiceRig(client);
        var request = new AgentTurnRequest(AgentSubsystemTests.CreatePayload(SentinelMessage))
        {
            RestoredHistory = [new AgentHistoryTurn(AgentHistoryRole.User, SentinelHistory)],
        };

        // Act: a turn, then a reset
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(request, token), token);
        rig.Service.ResetConversation();
        await AgentServiceRig.WaitUntilAsync(() => rig.Factory.DeletedSessions.Count == 1, token);
        await AgentServiceRig.WaitUntilAsync(() => rig.ReadLog().Contains("Conversation released", StringComparison.Ordinal), token);
        var log = rig.ReadLog();

        // Assert: the turn really carried the sentinels, the log has the lifecycle, and no sentinel reached the log
        Assert.Contains(events, item => item is AgentEvent.TextDelta { Text: SentinelReply });
        Assert.Equal(SentinelFile, await File.ReadAllTextAsync(Path.Combine(rig.ProjectFolder, "note.txt"), token));
        Assert.IsType<AgentEvent.Completed>(events[^1]);
        Assert.Contains("Conversation started: provider=Ollama", log, StringComparison.Ordinal);
        Assert.Contains("Turn started: provider=Ollama restoredTurns=1 digestAdded=True", log, StringComparison.Ordinal);
        Assert.Contains("Turn ended: outcome=Completed", log, StringComparison.Ordinal);
        Assert.Contains("Conversation released: provider=Ollama reason=reset", log, StringComparison.Ordinal);
        Assert.Contains("note.txt", log, StringComparison.Ordinal);
        Assert.All(
            new[] { SentinelMessage, SentinelReply, SentinelFile, SentinelHistory },
            sentinel => Assert.DoesNotContain(sentinel, log, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that an exception while the conversation is made is reported to the user with its message but logged by its
    ///     type only.
    /// </summary>
    [Fact]
    public async Task AgentService_Logging_ExceptionWhileMakingTheAgent_LogsTheTypeAndNotTheMessage()
    {
        // Arrange: a factory that throws with a sentinel in the message
        var token = TestContext.Current.CancellationToken;
        var factory = new ThrowingFactory(new InvalidOperationException(SentinelException));
        using var rig = new AgentServiceRig(new ScriptedChatClient());
        using var service = new AgentService(
            rig.Projects,
            rig.Settings,
            new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), []),
            new AgentInstructions(new KnowledgePromptBuilder(KnowledgeCatalog.Default)),
            factory,
            new AgentReadiness(ollama: rig.Probe),
            logger: rig.Logger,
            localDataRoot: rig.LocalDataRoot);

        // Act
        var events = await AgentServiceRig.CollectAsync(service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("One")), token), token);
        var log = rig.ReadLog();

        // Assert: the user is told, the log has the type of the exception and not its text
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
        Assert.Equal(AgentFailureKind.Unexpected, failed.Kind);
        Assert.Contains(SentinelException, failed.Message, StringComparison.Ordinal);
        Assert.Contains("Turn could not start: exception=InvalidOperationException", log, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelException, log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the logger that the Copilot runtime gets records the level, the category, the event number, and the type
    ///     of an exception, and never the message, the state, or a scope.
    /// </summary>
    [Fact]
    public void RuntimeLogAdapter_Log_MessageStateAndScope_AreNeverWritten()
    {
        // Arrange: the adapter on a log file
        var folder = Directory.CreateTempSubdirectory("canvaspilot-runtime-log-").FullName;
        try
        {
            var logger = new RollingFileLogger(folder);
            Microsoft.Extensions.Logging.ILogger adapter = new RuntimeLogAdapter(logger, "CopilotRuntime");

            // Act: log with sentinel text in the state, the formatted message, a scope, and the exception message
            using var scope = adapter.BeginScope(SentinelMessage);
            adapter.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                new Microsoft.Extensions.Logging.EventId(42),
                SentinelMessage,
                new InvalidOperationException(SentinelException),
                (state, _) => state + SentinelReply);
            adapter.Log(Microsoft.Extensions.Logging.LogLevel.Debug, default, SentinelMessage, null, (state, _) => state);
            adapter.Log(Microsoft.Extensions.Logging.LogLevel.None, default, SentinelMessage, null, (state, _) => state);
            var log = File.ReadAllText(logger.ActiveFilePath);

            // Assert: only the facts are in the log
            Assert.Null(scope);
            Assert.Contains("Copilot runtime: category=CopilotRuntime level=Error event=42 exception=InvalidOperationException", log, StringComparison.Ordinal);
            Assert.Contains("level=Debug", log, StringComparison.Ordinal);
            Assert.DoesNotContain("level=None", log, StringComparison.Ordinal);
            Assert.All(new[] { SentinelMessage, SentinelReply, SentinelException }, sentinel => Assert.DoesNotContain(sentinel, log, StringComparison.Ordinal));
            Assert.False(adapter.IsEnabled(Microsoft.Extensions.Logging.LogLevel.None));
            Assert.Throws<ArgumentNullException>(() => new RuntimeLogAdapter(null!, "x"));
            Assert.Throws<ArgumentNullException>(() => new RuntimeLogAdapter(logger, null!));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    ///     Test that the constructor and the turn refuse missing arguments, and that the documented constants are what the
    ///     chat and the shell can rely on.
    /// </summary>
    [Fact]
    public void AgentService_Constructor_MissingArguments_AreRefused()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ScriptedChatClient());
        var registry = new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), []);
        var instructions = new AgentInstructions(new KnowledgePromptBuilder(KnowledgeCatalog.Default));
        var readiness = new AgentReadiness(ollama: rig.Probe);

        // Act and assert: every required argument is refused when it is missing
        Assert.Throws<ArgumentNullException>(() => new AgentService(null!, rig.Settings, registry, instructions, rig.Factory, readiness));
        Assert.Throws<ArgumentNullException>(() => new AgentService(rig.Projects, null!, registry, instructions, rig.Factory, readiness));
        Assert.Throws<ArgumentNullException>(() => new AgentService(rig.Projects, rig.Settings, null!, instructions, rig.Factory, readiness));
        Assert.Throws<ArgumentNullException>(() => new AgentService(rig.Projects, rig.Settings, registry, null!, rig.Factory, readiness));
        Assert.Throws<ArgumentNullException>(() => new AgentService(rig.Projects, rig.Settings, registry, instructions, null!, readiness));
        Assert.Throws<ArgumentNullException>(() => new AgentService(rig.Projects, rig.Settings, registry, instructions, rig.Factory, null!));
        Assert.Throws<ArgumentNullException>(() => rig.Service.RunTurnAsync(null!, token));
        Assert.Equal("CanvasPilot", AgentService.ApplicationFolderName);
        Assert.Equal(("copilot", "history"), (AgentService.CopilotFolderName, AgentService.HistoryFolderName));
    }

    /// <summary>
    ///     A factory that throws the exception it was given when it is asked for an agent.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    private sealed class ThrowingFactory(Exception exception) : IAgentFactory
    {
        /// <inheritdoc />
        public Task<AgentCreationResult> CreateAsync(AgentCreationRequest request, CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        /// <inheritdoc />
        public Task DeleteSessionAsync(Microsoft.Agents.AI.AgentSession session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

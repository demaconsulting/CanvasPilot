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

using System.Net;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AgentProviderFactory" />: the Ollama agent on a stand-in server (the address, the model,
///     the context window, the instructions, and the tools reach the wire, and a whole tool turn runs), the settings that
///     cannot be used, the ownership of what the factory starts, and the Copilot branch with a fake starter of the runtime.
/// </summary>
public sealed class AgentProviderFactoryTests
{
    /// <summary>The instructions that every request carries.</summary>
    private const string Instructions = "Test instructions: draw with the tools.";

    /// <summary>The project folder that the Copilot agent gets as its working directory; it need not exist.</summary>
    private static readonly string ProjectFolder = Path.Combine(Path.GetTempPath(), "canvaspilot-factory-project");

    /// <summary>The streamed answer of a model that says a few words and calls the tool.</summary>
    private static readonly string ToolCallAnswer = string.Join(
        "\n",
        """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"Let me "},"done":false}""",
        """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"demo_ping","arguments":{"note":"hi"}}}]},"done":false}""",
        """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","eval_count":5,"prompt_eval_count":7}""");

    /// <summary>The streamed answer of a model that answers in words after the tool.</summary>
    private static readonly string FinalAnswer = string.Join(
        "\n",
        """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":"The tool said pong."},"done":false}""",
        """{"model":"m","created_at":"2026-11-21T00:00:00Z","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","eval_count":6,"prompt_eval_count":9}""");

    /// <summary>
    ///     Makes the tool that the agents are given.
    /// </summary>
    /// <returns>The tool, which answers <c>pong</c>.</returns>
    private static AIFunction Tool()
    {
        return GuardedToolFactory.Create(() => ToolResult.Text("pong"), "demo_ping", "Answers a ping.");
    }

    /// <summary>
    ///     Makes a factory whose Ollama requests go to the stand-in server and whose catalog knows the model of the tests.
    /// </summary>
    /// <param name="handler">The stand-in server for the chat requests.</param>
    /// <param name="catalog">The catalog of the server; one with <c>gemma3:12b</c> when <see langword="null" />.</param>
    /// <returns>The factory.</returns>
    private static AgentProviderFactory OllamaFactory(QueuedOllamaHandler handler, FakeModelCatalog? catalog = null)
    {
        var models = catalog ?? new FakeModelCatalog("ollama", TestModels.Make("ollama", "gemma3:12b", windowTokens: 131_072));
        return new AgentProviderFactory(handler: handler, ollamaCatalogs: new FakeOllamaCatalogs(models));
    }

    /// <summary>
    ///     Makes a factory on the development machine whose Copilot runtime offers the given models.
    /// </summary>
    /// <param name="source">Receives the requests for agents.</param>
    /// <param name="models">The models of the runtime.</param>
    /// <returns>The factory.</returns>
    private static AgentProviderFactory CopilotFactory(out FakeCopilotAgentSource source, params ModelInfo[] models)
    {
        source = new FakeCopilotAgentSource(new ScriptedChatClient(), new FakeModelCatalog("copilot", models));
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "Started.", source));
        return new AgentProviderFactory(Locator(), starter);
    }

    /// <summary>
    ///     Test that Copilot model and reasoning effort of the settings are validated by the catalog of the runtime and
    ///     reach the agent as the selection, and that settings that name no model give the default model and no selection.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Copilot_ModelAndEffort_ReachTheAgentAsTheSelection()
    {
        // Arrange: a runtime that offers a model with reasoning efforts
        var token = TestContext.Current.CancellationToken;
        await using var factory = CopilotFactory(out var source, TestModels.Make("copilot", "claude-haiku-5.5", efforts: ["low", "medium", "xhigh"]));
        var settings = new AiProviderSettings { ModelName = "claude-haiku-5.5", ReasoningEffort = "xhigh" };

        // Act: make an agent for the settings, and one for settings that name an effort but no model
        await using var chosen = await factory.CreateAsync(Request(settings), token);
        await using var unnamed = await factory.CreateAsync(Request(new AiProviderSettings { ReasoningEffort = "low" }), token);

        // Assert: the selection carries the model and the effort, and an effort without a model is not used
        Assert.True(chosen.IsCreated);
        var selection = source.Requests[0].Selection!;
        Assert.Equal("claude-haiku-5.5", selection.Model.Id);
        Assert.Equal("xhigh", selection.Settings.ReasoningEffort);
        Assert.Null(selection.Settings.ContextWindowTokens);
        Assert.True(unnamed.IsCreated);
        Assert.Null(source.Requests[1].Selection);
    }

    /// <summary>
    ///     Test that the Copilot context size of the settings is validated by the catalog of the runtime and reaches the agent
    ///     as the selection, that a size that the model does not offer is refused with the sentence of the library, and that
    ///     a stored size is not used for Ollama, which has no such choice.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Copilot_ContextTier_ReachesTheAgentAsTheSelectionAndIsValidated()
    {
        // Arrange: a runtime that offers a model with two sizes and one with a single size
        var token = TestContext.Current.CancellationToken;
        await using var factory = CopilotFactory(
            out var source,
            TestModels.Make("copilot", "claude-opus-5.5", tiers: [("default", "Standard", 328_000), ("long_context", "Long context", 1_000_000)]),
            TestModels.Make("copilot", "single"));

        // Act: the larger size, a size that the model does not offer, a size for a model with none, and no size
        await using var larger = await factory.CreateAsync(Request(new AiProviderSettings { ModelName = "claude-opus-5.5", ContextTier = "long_context" }), token);
        await using var refused = await factory.CreateAsync(Request(new AiProviderSettings { ModelName = "claude-opus-5.5", ContextTier = "huge" }), token);
        await using var noTiers = await factory.CreateAsync(Request(new AiProviderSettings { ModelName = "single", ContextTier = "long_context" }), token);
        await using var noSize = await factory.CreateAsync(Request(new AiProviderSettings { ModelName = "claude-opus-5.5" }), token);

        // Assert: the size reaches the selection, a size that is not offered is refused, and none is none
        Assert.True(larger.IsCreated);
        Assert.Equal("long_context", source.Requests[0].Selection!.Settings.ContextTier);
        Assert.False(refused.IsCreated);
        Assert.Equal(AgentReadinessStatus.ModelSettingsInvalid, refused.Failure!.Status);
        Assert.Contains("huge", refused.Failure.Message, StringComparison.Ordinal);
        Assert.False(noTiers.IsCreated);
        Assert.Equal(AgentReadinessStatus.ModelSettingsInvalid, noTiers.Failure!.Status);
        Assert.True(noSize.IsCreated);
        Assert.Null(source.Requests[1].Selection!.Settings.ContextTier);
    }

    /// <summary>
    ///     Test that a context size that is stored in the settings is not stated for Ollama, whose model settings the
    ///     catalog of the server would refuse with it.
    /// </summary>
    [Fact]
    public void ModelRequirements_ToModelSettings_ContextTier_IsStatedForCopilotOnly()
    {
        // Arrange: the same choices for both providers
        var copilot = new AiProviderSettings { Provider = AiProviderKind.GitHubCopilot, ModelName = "m", ContextTier = " long_context " };
        var ollama = copilot with { Provider = AiProviderKind.Ollama };
        var unnamed = copilot with { ModelName = string.Empty };

        // Act
        var forCopilot = ModelRequirements.ToModelSettings(copilot, withContextWindow: false);
        var forOllama = ModelRequirements.ToModelSettings(ollama, withContextWindow: true);
        var forNoModel = ModelRequirements.ToModelSettings(unnamed, withContextWindow: false);

        // Assert: Copilot gets the trimmed size, Ollama none, and no model means no choice at all
        Assert.Equal("long_context", forCopilot!.ContextTier);
        Assert.Null(forOllama!.ContextTier);
        Assert.Null(forNoModel);
    }

    /// <summary>
    ///     Test that Copilot model settings that the catalog refuses give the typed status with the sentence of the library
    ///     and no agent: a model without tool calling, a model that is not offered, an effort that the model does not offer,
    ///     and a model that cannot be used.
    /// </summary>
    [Theory]
    [InlineData("no-tools", "", "does not support tool calling")]
    [InlineData("missing-model", "", "is not available from provider 'copilot'")]
    [InlineData("claude-haiku-5.5", "bogus", "does not support reasoning effort 'bogus'. Supported: low, medium.")]
    [InlineData("plain", "low", "does not support reasoning effort settings")]
    [InlineData("disabled", "", "cannot be used")]
    public async Task AgentProviderFactory_Create_Copilot_ModelSettingsTheCatalogRefuses_AreModelSettingsInvalid(string model, string effort, string sentence)
    {
        // Arrange: a runtime with a few models
        await using var factory = CopilotFactory(
            out var source,
            TestModels.Make("copilot", "claude-haiku-5.5", efforts: ["low", "medium"]),
            TestModels.Make("copilot", "no-tools", toolCalls: false),
            TestModels.Make("copilot", "plain"),
            TestModels.Make("copilot", "disabled", usable: false));

        // Act
        await using var result = await factory.CreateAsync(Request(new AiProviderSettings { ModelName = model, ReasoningEffort = effort }), TestContext.Current.CancellationToken);

        // Assert: no agent, the status, and the sentence of the library
        Assert.False(result.IsCreated);
        Assert.Equal(AgentReadinessStatus.ModelSettingsInvalid, result.Failure!.Status);
        Assert.Contains(sentence, result.Failure.Message, StringComparison.Ordinal);
        Assert.Empty(source.Requests);
    }

    /// <summary>
    ///     Test that a catalog that cannot be read gives the status of a runtime that is not usable, with the sentence of
    ///     the library, and no agent.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Copilot_CatalogCannotBeRead_IsRuntimeUnavailable()
    {
        // Arrange: a runtime whose catalog fails as a runtime that is not signed in does
        await using var factory = CopilotFactory(out var source);
        source.Catalog.Failure = new InvalidOperationException("The Copilot runtime could not list models: it is not signed in.");

        // Act
        await using var result = await factory.CreateAsync(Request(new AiProviderSettings { ModelName = "any-model" }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentReadinessStatus.CopilotRuntimeUnavailable, result.Failure!.Status);
        Assert.Contains("could not list models", result.Failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that Ollama model, reasoning effort, and context window are validated by the catalog of the server and
    ///     reach the wire: the canonical id of the model, the window, and the thinking level of the effort.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Ollama_ModelEffortAndWindow_ReachTheWire()
    {
        // Arrange: a server whose catalog describes a model that thinks, asked through the real catalog and a stand-in server
        var token = TestContext.Current.CancellationToken;
        var handler = new QueuedOllamaHandler(FinalAnswer)
        {
            Tags = """{"models":[{"name":"qwen3:latest","model":"qwen3:latest","size":1,"details":{"family":"qwen3"}}]}""",
            Show = """{"capabilities":["completion","tools","thinking"],"model_info":{"general.architecture":"qwen3","qwen3.context_length":40960},"details":{"family":"qwen3"}}""",
        };
        await using var factory = new AgentProviderFactory(handler: handler);
        var settings = Ollama(model: "qwen3", contextTokens: 24576) with { ReasoningEffort = "high" };

        // Act: make the agent for the name without a tag, and run a turn
        await using var result = await factory.CreateAsync(Request(settings), token);
        var events = await RunTurn(result.Agent!, settings);

        // Assert: the chat request names the model of the catalog, the window, and the level of thinking
        Assert.True(result.IsCreated);
        var chat = Assert.Single(handler.Requests, request => request.Address.AbsolutePath == "/api/chat");
        Assert.Contains("\"model\":\"qwen3:latest\"", chat.Body, StringComparison.Ordinal);
        Assert.Contains("\"num_ctx\":24576", chat.Body, StringComparison.Ordinal);
        Assert.Matches("\"think\":\\s*(\"high\"|true)", chat.Body);
        Assert.IsType<AgentEvent.Completed>(events[^1]);
    }

    /// <summary>
    ///     Test that Ollama settings that the catalog refuses give the typed status with the sentence of the library and no
    ///     agent: a model that cannot call tools, a window that is larger than the model was trained for, an effort that the
    ///     model does not offer, and a model that the server does not have.
    /// </summary>
    [Theory]
    [InlineData("no-tools", 24576, "", "does not support tool calling")]
    [InlineData("small", 24576, "", "supports at most 8,192 tokens; a 24,576-token window is not supported")]
    [InlineData("gemma3:12b", 24576, "high", "does not support reasoning effort settings")]
    [InlineData("absent", 24576, "", "is not available from provider 'ollama'")]
    public async Task AgentProviderFactory_Create_Ollama_ModelSettingsTheCatalogRefuses_AreModelSettingsInvalid(
        string model,
        int window,
        string effort,
        string sentence)
    {
        // Arrange: a server with a model without tools, a model trained for a small window, and an ordinary model
        var handler = new QueuedOllamaHandler();
        var catalog = new FakeModelCatalog(
            "ollama",
            TestModels.Make("ollama", "gemma3:12b", windowTokens: 131_072),
            TestModels.Make("ollama", "no-tools:latest", toolCalls: false),
            TestModels.Make("ollama", "small:latest", windowTokens: 8192));
        await using var factory = OllamaFactory(handler, catalog);

        // Act
        await using var result = await factory.CreateAsync(Request(Ollama(model: model, contextTokens: window) with { ReasoningEffort = effort }), TestContext.Current.CancellationToken);

        // Assert: no agent and no chat request, and the sentence of the library
        Assert.False(result.IsCreated);
        Assert.Equal(AgentReadinessStatus.ModelSettingsInvalid, result.Failure!.Status);
        Assert.Contains(sentence, result.Failure.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    /// <summary>
    ///     Test that a server that cannot describe its models leaves the typed model to be tried as it is, so that typing a
    ///     name still works when the catalog cannot be read.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Ollama_CatalogUnreachable_UsesTheTypedModelWithoutACheck()
    {
        // Arrange: a catalog that fails like a server that does not answer
        var handler = new QueuedOllamaHandler(FinalAnswer);
        var catalog = new FakeModelCatalog("ollama") { Failure = new HttpRequestException("Connection refused.") };
        await using var factory = OllamaFactory(handler, catalog);
        var settings = Ollama(model: "typed-model");

        // Act: make the agent and run a turn
        await using var result = await factory.CreateAsync(Request(settings), TestContext.Current.CancellationToken);
        var events = await RunTurn(result.Agent!, settings);

        // Assert: the agent works with the name as typed
        Assert.True(result.IsCreated);
        Assert.Contains("\"model\":\"typed-model\"", handler.Requests.Single().Body, StringComparison.Ordinal);
        Assert.IsType<AgentEvent.Completed>(events[^1]);
    }

    /// <summary>
    ///     Makes the settings of Ollama.
    /// </summary>
    /// <param name="endpoint">The address of the server.</param>
    /// <param name="model">The model.</param>
    /// <param name="contextTokens">The context window.</param>
    /// <returns>The settings.</returns>
    private static AiProviderSettings Ollama(string endpoint = "http://ollama.test:12345", string model = "gemma3:12b", int contextTokens = 24576)
    {
        return new AiProviderSettings
        {
            Provider = AiProviderKind.Ollama,
            OllamaEndpoint = endpoint,
            ModelName = model,
            ContextWindowTokens = contextTokens,
        };
    }

    /// <summary>
    ///     Makes the request for an agent with the one tool.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="tools">The tools; the one tool when <see langword="null" />.</param>
    /// <returns>The request.</returns>
    private static AgentCreationRequest Request(AiProviderSettings settings, IReadOnlyList<AIFunction>? tools = null)
    {
        return new AgentCreationRequest(settings, tools ?? [Tool()], Instructions, ProjectFolder);
    }

    /// <summary>
    ///     Makes a locator on the development machine, where the real CLI answers with a version.
    /// </summary>
    /// <param name="version">The version that the CLI reports.</param>
    /// <returns>The locator.</returns>
    private static CopilotCliLocator Locator(string version = "1.0.114")
    {
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, version);
        return new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());
    }

    /// <summary>
    ///     Runs one turn of an agent and maps its updates to events.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="settings">The settings, which give the provider and the model to the mapper.</param>
    /// <returns>The events.</returns>
    private static async Task<List<AgentEvent>> RunTurn(AIAgent agent, AiProviderSettings settings)
    {
        var token = TestContext.Current.CancellationToken;
        var session = await agent.CreateSessionAsync(token);
        var mapper = new AgentEventMapper(settings.Provider, settings.ModelName, cancellationToken: token);
        var events = new List<AgentEvent>();
        await foreach (var mapped in mapper.MapAsync(agent.RunStreamingAsync("Ping?", session, cancellationToken: token)))
        {
            events.Add(mapped);
        }

        return events;
    }

    /// <summary>
    ///     Test that the constants of the unit are the documented values.
    /// </summary>
    [Fact]
    public void AgentProviderFactory_Constants_Rule_AreTheDocumentedValues()
    {
        // Assert: the name of the agent and the sentences for settings that cannot be used
        Assert.Equal("CanvasPilot", AgentProviderFactory.AgentName);
        Assert.Equal("Enter the address of the Ollama server in Settings, for example http://localhost:11434.", AgentProviderFactory.InvalidEndpointMessage);
        Assert.Equal("Enter the name of an Ollama model in Settings, for example gemma3.", AgentProviderFactory.MissingModelMessage);
    }

    /// <summary>
    ///     Test that missing arguments are refused.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_BadArguments_AreRefused()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        await using var factory = new AgentProviderFactory();
        var settings = Ollama();

        // Act and assert: a missing request, missing parts, and a blank project folder are refused
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateAsync(null!, token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateAsync(new AgentCreationRequest(null!, [Tool()], Instructions, ProjectFolder), token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateAsync(new AgentCreationRequest(settings, null!, Instructions, ProjectFolder), token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateAsync(new AgentCreationRequest(settings, [Tool()], null!, ProjectFolder), token));
        await Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAsync(new AgentCreationRequest(settings, [Tool()], Instructions, "  "), token));
        Assert.Throws<ArgumentNullException>(() => AgentCreationResult.Created(null!));
        Assert.Throws<ArgumentNullException>(() => AgentCreationResult.Unavailable(null!));
        Assert.Throws<ArgumentNullException>(() => new CopilotRuntimeAgentSource(null!));
    }

    /// <summary>
    ///     Test that the Ollama agent talks to the address, the model, and the context window of the settings, sends the
    ///     instructions and the tools, runs a whole tool turn, and gives the events of the turn.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Ollama_SettingsReachTheWireAndAToolTurnRuns()
    {
        // Arrange: a stand-in server that asks for the tool and then answers
        var token = TestContext.Current.CancellationToken;
        var handler = new QueuedOllamaHandler(ToolCallAnswer, FinalAnswer);
        await using var factory = OllamaFactory(handler);
        var settings = Ollama();

        // Act: make the agent and run a turn
        await using var result = await factory.CreateAsync(Request(settings), token);
        var events = await RunTurn(result.Agent!, settings);

        // Assert: an agent was made with no failure
        Assert.True(result.IsCreated);
        Assert.Null(result.Failure);

        // The two requests went to the address of the settings, for the model, with the window, the instructions, and the tool
        Assert.Equal(2, handler.Requests.Count);
        var first = handler.Requests[0];
        Assert.Equal("ollama.test", first.Address.Host);
        Assert.Equal(12345, first.Address.Port);
        Assert.Equal("/api/chat", first.Address.AbsolutePath);
        Assert.Contains("\"model\":\"gemma3:12b\"", first.Body, StringComparison.Ordinal);
        Assert.Contains("\"num_ctx\":24576", first.Body, StringComparison.Ordinal);
        Assert.Contains("\"stream\":true", first.Body, StringComparison.Ordinal);
        Assert.Contains("Test instructions: draw with the tools.", first.Body, StringComparison.Ordinal);
        Assert.Contains("\"demo_ping\"", first.Body, StringComparison.Ordinal);
        Assert.Contains("\"num_ctx\":24576", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("pong", handler.Requests[1].Body, StringComparison.Ordinal);

        // The events: the words, the call and its result, the words after, and the end
        Assert.Equal("Let me The tool said pong.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
        var started = Assert.Single(events.OfType<AgentEvent.ToolStarted>());
        var finished = Assert.Single(events.OfType<AgentEvent.ToolFinished>());
        Assert.Equal("demo_ping", started.Name);
        Assert.Equal("note=hi", started.ArgumentSummary);
        Assert.Equal((started.CallId, "demo_ping", true, "pong"), (finished.CallId, finished.Name, finished.Succeeded, finished.Summary));
        Assert.IsType<AgentEvent.Completed>(events[^1]);
    }

    /// <summary>
    ///     Test that a model that the server does not have, which the server answers with a 404, becomes the failure with
    ///     the pull command through the mapper, with the agent of the factory.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Ollama_MissingModel_TurnFailsWithThePullCommand()
    {
        // Arrange: a server that answers every chat with 404
        var token = TestContext.Current.CancellationToken;
        var handler = new QueuedOllamaHandler("""{"error":"model 'gemma3:12b' not found"}""") { Status = HttpStatusCode.NotFound };
        await using var factory = OllamaFactory(handler);
        var settings = Ollama();
        await using var result = await factory.CreateAsync(Request(settings), token);

        // Act
        var events = await RunTurn(result.Agent!, settings);

        // Assert
        var failed = Assert.IsType<AgentEvent.Failed>(Assert.Single(events));
        Assert.Equal(AgentFailureKind.ModelNotFound, failed.Kind);
        Assert.Contains("`ollama pull gemma3:12b`", failed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that settings of Ollama that cannot be used give the typed answer with the sentence that says what to
    ///     enter, and that no request is made.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Ollama_SettingsThatCannotBeUsed_AreNotConfigured()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        var handler = new QueuedOllamaHandler();
        await using var factory = OllamaFactory(handler);

        // Act: an address that is no address, a blank address, a blank model, and a window out of range
        var badAddress = await factory.CreateAsync(Request(Ollama(endpoint: "not an address")), token);
        var blankAddress = await factory.CreateAsync(Request(Ollama(endpoint: " ")), token);
        var noModel = await factory.CreateAsync(Request(Ollama(model: "  ")), token);
        var smallWindow = await factory.CreateAsync(Request(Ollama(contextTokens: 100)), token);

        // Assert
        foreach (var result in new[] { badAddress, blankAddress })
        {
            Assert.False(result.IsCreated);
            Assert.Equal(AgentReadinessStatus.NotConfigured, result.Failure!.Status);
            Assert.Equal(AgentProviderFactory.InvalidEndpointMessage, result.Failure.Message);
        }

        Assert.Equal(AgentReadinessStatus.NotConfigured, noModel.Failure!.Status);
        Assert.Equal(AgentProviderFactory.MissingModelMessage, noModel.Failure.Message);
        Assert.Equal(AgentReadinessStatus.NotConfigured, smallWindow.Failure!.Status);
        Assert.Contains("between 512 and 2,000,000 tokens", smallWindow.Failure.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
        Assert.Null(badAddress.Agent);
    }

    /// <summary>
    ///     Test that a set of tools that the AI library refuses is an exception and leaves the factory usable, and that the
    ///     model name and the address are trimmed.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Ollama_NoTools_ThrowsAndTheFactoryStillWorks()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        var handler = new QueuedOllamaHandler(FinalAnswer);
        await using var factory = OllamaFactory(handler);
        var settings = Ollama(endpoint: " http://ollama.test:12345 ", model: " gemma3:12b ");

        // Act: no tools is refused by the library; the next request works with the trimmed settings
        await Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAsync(Request(settings, []), token));
        await using var result = await factory.CreateAsync(Request(settings), token);
        await RunTurn(result.Agent!, settings);

        // Assert
        Assert.Contains("\"model\":\"gemma3:12b\"", handler.Requests.Single().Body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that each Ollama agent owns its clients and not the handler: disposing one result, twice, leaves another
    ///     agent of the same factory working.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Ollama_DisposingAResult_LeavesOtherAgentsWorking()
    {
        // Arrange: two agents from one factory over one handler
        var token = TestContext.Current.CancellationToken;
        var handler = new QueuedOllamaHandler(FinalAnswer);
        await using var factory = OllamaFactory(handler);
        var settings = Ollama();
        var first = await factory.CreateAsync(Request(settings), token);
        await using var second = await factory.CreateAsync(Request(settings), token);

        // Act: dispose the first, twice, and run the second
        await first.DisposeAsync();
        await first.DisposeAsync();
        var events = await RunTurn(second.Agent!, settings);

        // Assert
        Assert.IsType<AgentEvent.Completed>(events[^1]);
        Assert.Equal("The tool said pong.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
    }

    /// <summary>
    ///     Test that a starter that refuses the runtime for each cause gives the typed answer with the starter's own
    ///     sentence and no agent, with the Copilot CLI of the development machine found.
    /// </summary>
    /// <param name="start">The status that the starter answers.</param>
    /// <param name="expected">The status that the answer must have.</param>
    [Theory]
    [InlineData(CopilotStartStatus.CliTooOld, AgentReadinessStatus.CopilotCliTooOld)]
    [InlineData(CopilotStartStatus.NotSignedIn, AgentReadinessStatus.CopilotNotSignedIn)]
    [InlineData(CopilotStartStatus.Failed, AgentReadinessStatus.CopilotRuntimeUnavailable)]
    public async Task AgentProviderFactory_Create_Copilot_StarterRefuses_ReturnsTheMatchingStatusAndNoAgent(
        CopilotStartStatus start,
        AgentReadinessStatus expected)
    {
        // Arrange: the development machine and a starter that refuses
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(start, "The sentence of the starter."));
        await using var factory = new AgentProviderFactory(Locator(), starter);

        // Act
        await using var result = await factory.CreateAsync(Request(new AiProviderSettings()), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsCreated);
        Assert.Null(result.Agent);
        Assert.Equal(expected, result.Failure!.Status);
        Assert.Equal("The sentence of the starter.", result.Failure.Message);
    }

    /// <summary>
    ///     Test that a missing CLI and a CLI that is too old are reported as the readiness check reports them, and that the
    ///     starter is not asked.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Copilot_NoCliOrOldCli_ReportsTheCliAndDoesNotStart()
    {
        // Arrange: a machine with nothing installed, and one with an old CLI, each with a starter that records its requests
        var token = TestContext.Current.CancellationToken;
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "unused"));
        await using var empty = new AgentProviderFactory(
            new CopilotCliLocator(new FakeProcessRunner(), new FakeCliFileSystem(), new FakeEnvironment(DevMachine.LocalAppData, @"C:\Windows")),
            starter);
        await using var old = new AgentProviderFactory(Locator("1.0.100"), starter);

        // Act
        var missing = await empty.CreateAsync(Request(new AiProviderSettings()), token);
        var tooOld = await old.CreateAsync(Request(new AiProviderSettings()), token);

        // Assert
        Assert.Equal(AgentReadinessStatus.CopilotCliMissing, missing.Failure!.Status);
        Assert.Contains("winget install GitHub.Copilot", missing.Failure.Message, StringComparison.Ordinal);
        Assert.Equal(AgentReadinessStatus.CopilotCliTooOld, tooOld.Failure!.Status);
        Assert.Contains("1.0.100", tooOld.Failure.Message, StringComparison.Ordinal);
        Assert.Empty(starter.Requests);
    }

    /// <summary>
    ///     Test the Copilot branch with a fake starter: the runtime is started on the CLI that was found, every agent
    ///     is made on it with the tools, the instructions, the name, the model of the settings (none when blank), and the
    ///     project folder, a whole turn runs, and neither the results nor a factory that does not own the starter dispose
    ///     the runtime, which belongs to the starter.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Copilot_FakeStarter_MakesAgentsOnTheRuntimeThatBelongsToTheStarter()
    {
        // Arrange: a starter that gives a runtime whose agents talk to a scripted client
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("c1", "demo_ping", new Dictionary<string, object?>())])],
            [new ChatResponseUpdate(ChatRole.Assistant, "Pong received.")],
            [new ChatResponseUpdate(ChatRole.Assistant, "Second agent.")]);
        var source = new FakeCopilotAgentSource(client, new FakeModelCatalog("copilot", TestModels.Make("copilot", "gpt-5.4-mini")));
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "Started.", source));
        var factory = new AgentProviderFactory(Locator(), starter);
        var withModel = new AiProviderSettings { ModelName = " gpt-5.4-mini " };
        var tool = Tool();

        // Act: make an agent with the default model and run a turn, then make a second one with a model
        var first = await factory.CreateAsync(Request(new AiProviderSettings(), [tool]), token);
        var events = await RunTurn(first.Agent!, new AiProviderSettings());
        await first.DisposeAsync();
        var second = await factory.CreateAsync(Request(withModel, [tool]), token);
        await second.DisposeAsync();
        await factory.DisposeAsync();
        await factory.DisposeAsync();

        // Assert: both asks reached the starter with the CLI that was found
        Assert.All(starter.Requests, start => Assert.Equal((DevMachine.RealCli, new Version(1, 0, 114)), (start.CliPath, start.CliVersion)));

        // Two agents were made on the runtime with the tools, the instructions, the name, the model, and the folder
        Assert.Equal(2, source.Requests.Count);
        Assert.All(source.Requests, request =>
        {
            Assert.Equal(AgentProviderFactory.AgentName, request.Name);
            Assert.Equal(Instructions, request.Instructions);
            Assert.Equal(ProjectFolder, request.WorkingDirectory);
            Assert.Equal([tool], request.Tools);
        });
        Assert.Null(source.Requests[0].Selection);
        Assert.Equal("gpt-5.4-mini", source.Requests[1].Selection!.Model.Id);

        // The turn ran through the agent of the runtime
        Assert.Equal("Pong received.", string.Concat(events.OfType<AgentEvent.TextDelta>().Select(delta => delta.Text)));
        Assert.Equal("demo_ping", Assert.Single(events.OfType<AgentEvent.ToolStarted>()).Name);
        Assert.IsType<AgentEvent.Completed>(events[^1]);

        // The runtime belongs to the starter, which this factory does not own
        Assert.Equal(0, source.DisposeCount);
    }

    /// <summary>
    ///     Test that a factory that owns its starter stops the runtime once when it is disposed, and that a factory made
    ///     without a starter owns the one that it makes.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_DisposeAsync_OwnedStarter_StopsTheRuntimeOnce()
    {
        // Arrange: the real starter over a start function that gives a fake runtime, owned by the factory, and a CLI that is
        // a full path of this platform, which the real starter requires
        var token = TestContext.Current.CancellationToken;
        var source = new FakeCopilotAgentSource(new ScriptedChatClient());
        var starter = new CopilotRuntimeStarter(Path.Combine(Path.GetTempPath(), "canvaspilot-factory-data"), (_, _) => Task.FromResult<ICopilotAgentSource>(source));
        var factory = new AgentProviderFactory(DevMachine.CreatePlatformLocator(), starter, ownsStarter: true);
        var settings = new AiProviderSettings { CopilotCliPath = DevMachine.PlatformCli };
        await using var made = await factory.CreateAsync(Request(settings), token);

        // Act: dispose the factory twice
        await factory.DisposeAsync();
        await factory.DisposeAsync();

        // Assert: the runtime was stopped once, and the factory refuses more requests
        Assert.True(made.IsCreated);
        Assert.Equal(1, source.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => factory.CreateAsync(Request(settings), token));
    }

    /// <summary>
    ///     Test that a starter that answers with a failure, or with a started runtime that has no way to make agents, gives
    ///     the typed answer, and that nothing is kept so that the next request asks the starter again.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Copilot_StarterFailureOrNoSource_IsUnavailableAndNotCached()
    {
        // Arrange: a starter that fails, and a starter that says started without a source
        var token = TestContext.Current.CancellationToken;
        var failing = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Failed, "The runtime could not be started."));
        var sourceless = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "Started."));
        await using var failingFactory = new AgentProviderFactory(Locator(), failing);
        await using var sourcelessFactory = new AgentProviderFactory(Locator(), sourceless);

        // Act: ask each twice
        var failed1 = await failingFactory.CreateAsync(Request(new AiProviderSettings()), token);
        var failed2 = await failingFactory.CreateAsync(Request(new AiProviderSettings()), token);
        var none1 = await sourcelessFactory.CreateAsync(Request(new AiProviderSettings()), token);
        var none2 = await sourcelessFactory.CreateAsync(Request(new AiProviderSettings()), token);

        // Assert: the sentence of the starter, the sentence for no source, and a new question each time
        Assert.Equal((AgentReadinessStatus.CopilotRuntimeUnavailable, "The runtime could not be started."), (failed1.Failure!.Status, failed1.Failure.Message));
        Assert.Equal(AgentReadinessStatus.CopilotRuntimeUnavailable, failed2.Failure!.Status);
        Assert.Equal(AgentProviderFactory.NoAgentSourceMessage, none1.Failure!.Message);
        Assert.Equal(AgentReadinessStatus.CopilotRuntimeUnavailable, none2.Failure!.Status);
        Assert.Equal(2, failing.Requests.Count);
        Assert.Equal(2, sourceless.Requests.Count);
    }

    /// <summary>
    ///     Test that eight requests at once start the runtime only once (the starter shares it), and that a factory that was
    ///     disposed refuses a request for Copilot.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Copilot_ConcurrentRequestsAndDisposedFactory_StartOnceAndRefuse()
    {
        // Arrange: the real starter over a start function that counts its calls
        var token = TestContext.Current.CancellationToken;
        var source = new FakeCopilotAgentSource(new ScriptedChatClient());
        var starts = 0;
        var starter = new CopilotRuntimeStarter(
            Path.Combine(Path.GetTempPath(), "canvaspilot-factory-data"),
            async (_, cancellation) =>
            {
                Interlocked.Increment(ref starts);
                await Task.Delay(50, cancellation);
                return source;
            });
        var factory = new AgentProviderFactory(DevMachine.CreatePlatformLocator(), starter, ownsStarter: true);
        var settings = new AiProviderSettings { CopilotCliPath = DevMachine.PlatformCli };

        // Act: eight requests at once, then dispose the factory and ask again
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => factory.CreateAsync(Request(settings), token)));
        await factory.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => factory.CreateAsync(Request(settings), token));

        // Assert
        Assert.Equal(1, starts);
        Assert.All(results, result => Assert.True(result.IsCreated));
        Assert.Equal(8, source.Requests.Count);
        Assert.Equal(1, source.DisposeCount);
    }

    /// <summary>
    ///     Test that the data folder and the logger of the request reach the starter of the runtime, and that a request
    ///     without them gives the starter none, so that the starter never has to choose a default of the library itself.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_Create_Copilot_DataDirectoryAndLogger_ReachTheStarter()
    {
        // Arrange: a request that names a data folder and a logger, and a starter that records what it gets
        var token = TestContext.Current.CancellationToken;
        var source = new FakeCopilotAgentSource(new ScriptedChatClient());
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "Started.", source));
        await using var factory = new AgentProviderFactory(Locator(), starter);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger>();
        var request = Request(new AiProviderSettings()) with { CopilotDataDirectory = @"C:\data\CanvasPilot\copilot", CopilotLogger = logger };

        // Act
        await using var result = await factory.CreateAsync(request, token);

        // Assert
        var start = Assert.Single(starter.Requests);
        Assert.Equal(@"C:\data\CanvasPilot\copilot", start.DataDirectory);
        Assert.Same(logger, start.Logger);
        Assert.Null(new CopilotStartRequest("copilot.exe", new Version(1, 0, 114)).DataDirectory);
    }

    /// <summary>
    ///     Test that deleting the session of a conversation reaches the runtime only for a Copilot session that has an id,
    ///     and does nothing for an Ollama session, for a session of a runtime that was never started, or for a missing
    ///     session.
    /// </summary>
    [Fact]
    public async Task AgentProviderFactory_DeleteSession_CopilotSessionOnly_DeletesThroughTheRuntime()
    {
        // Arrange: a factory with a started fake runtime, and sessions of both providers
        var token = TestContext.Current.CancellationToken;
        var source = new FakeCopilotAgentSource(new ScriptedChatClient());
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "Started.", source));
        await using var factory = new AgentProviderFactory(Locator(), starter);
        await using var neverStarted = new AgentProviderFactory(Locator(), starter);
        await using var made = await factory.CreateAsync(Request(new AiProviderSettings()), token);
        var ollamaSession = await made.Agent!.CreateSessionAsync(token);
        var copilotSession = MakeCopilotSession("session-42");
        var withoutId = MakeCopilotSession(string.Empty);

        // Act
        await factory.DeleteSessionAsync(ollamaSession, token);
        await factory.DeleteSessionAsync(withoutId, token);
        await neverStarted.DeleteSessionAsync(copilotSession, token);
        await factory.DeleteSessionAsync(copilotSession, token);

        // Assert: only the Copilot session with an id on the started runtime was deleted
        Assert.Equal(["session-42"], source.DeletedSessions);
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.DeleteSessionAsync(null!, token));
    }

    /// <summary>
    ///     Makes a session of the Copilot agent with an id, which the AI library only makes while a conversation runs on a
    ///     started runtime.
    /// </summary>
    /// <param name="id">The id of the session.</param>
    /// <returns>The session.</returns>
    private static Microsoft.Agents.AI.AgentSession MakeCopilotSession(string id)
    {
        return (Microsoft.Agents.AI.AgentSession)Activator.CreateInstance(
            typeof(DemaConsulting.AgentKit.Agents.Copilot.CopilotAgentSession),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            null,
            [id, new Microsoft.Agents.AI.AgentSessionStateBag()],
            null)!;
    }
}

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

using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ProviderModelCatalog" /> and <see cref="OllamaModelCatalogs" />: the mapping of the models
///     of the AI library to the models of the settings (capabilities, reasoning efforts, window, unusable models, warnings),
///     the list of GitHub Copilot through the shared runtime, the list of Ollama (through a fake catalog and through a
///     stand-in server), and the rule that a provider that cannot be asked is a message and never an exception. No program
///     is run and no server is asked.
/// </summary>
public sealed class ProviderModelCatalogTests
{
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
    ///     Makes the catalog over a runtime that offers the models.
    /// </summary>
    /// <param name="source">The runtime that the starter gives.</param>
    /// <param name="start">What the starter answers; started with the source when <see langword="null" />.</param>
    /// <returns>The catalog.</returns>
    private static ProviderModelCatalog CopilotCatalog(FakeCopilotAgentSource source, CopilotStartResult? start = null)
    {
        var starter = new FakeCopilotRuntimeStarter(start ?? new CopilotStartResult(CopilotStartStatus.Started, "Started.", source));
        return new ProviderModelCatalog(Locator(), starter, new FakeOllamaCatalogs(new FakeModelCatalog("ollama")));
    }

    /// <summary>
    ///     Makes the catalog over an Ollama server that is described by a catalog.
    /// </summary>
    /// <param name="catalog">The catalog of the server.</param>
    /// <returns>The catalog.</returns>
    private static ProviderModelCatalog OllamaCatalog(IModelCatalog catalog)
    {
        return new ProviderModelCatalog(
            Locator(),
            new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Failed, "unused")),
            new FakeOllamaCatalogs(catalog));
    }

    /// <summary>
    ///     Test that a model is mapped with what it can do: the capabilities, the efforts that it offers (only when the effort
    ///     can be adjusted), the window, the default effort, and the warnings of the catalog without its plain notes.
    /// </summary>
    [Fact]
    public void ProviderModelCatalog_Map_Model_CarriesCapabilitiesEffortsWindowAndWarnings()
    {
        // Arrange: a model that does everything, with a note of each kind
        var model = TestModels.Make("copilot", "claude-haiku-5.5", efforts: ["none", "low", "xhigh"], windowTokens: 1_000_000, displayName: "Claude Haiku 5.5") with
        {
            DefaultReasoningEffort = "low",
            Notices =
            [
                new ModelNotice(ModelNoticeKind.Info, "Plain information."),
                new ModelNotice(ModelNoticeKind.Warning, "Premium requests apply."),
                new ModelNotice(ModelNoticeKind.Required, "Accept the terms first."),
            ],
        };

        // Act
        var mapped = ProviderModelCatalog.Map(model);

        // Assert
        Assert.Equal(("claude-haiku-5.5", "Claude Haiku 5.5"), (mapped.Id, mapped.DisplayName));
        Assert.True(mapped.SupportsToolCalls);
        Assert.True(mapped.SupportsVision);
        Assert.True(mapped.SupportsReasoning);
        Assert.Equal(1_000_000, mapped.ContextWindowTokens);
        Assert.Equal(["none", "low", "xhigh"], mapped.ReasoningEfforts.Select(effort => effort.Id).ToArray());
        Assert.Equal("low", mapped.DefaultReasoningEffort);
        Assert.True(mapped.IsUsable);
        Assert.Null(mapped.UnusableReason);
        Assert.Equal(["Premium requests apply.", "Accept the terms first."], mapped.Warnings);
    }

    /// <summary>
    ///     Test that a model without tool calling, vision, or efforts, and one that cannot be used, are mapped as they are,
    ///     so that the page can warn about them, and that efforts of a model that cannot adjust them are not offered.
    /// </summary>
    [Fact]
    public void ProviderModelCatalog_Map_PlainAndUnusableModels_AreMappedWithTheirLimits()
    {
        // Arrange: a model with nothing, one that cannot be used, and one that lists efforts without being adjustable
        var plain = TestModels.Make("ollama", "plain:latest", toolCalls: false, vision: false, windowTokens: null);
        var unusable = TestModels.Make("copilot", "disabled", usable: false);
        var fixedEffort = TestModels.Make("copilot", "fixed", efforts: ["high"]) with { Adjustments = ModelAdjustments.None };

        // Act
        var plainMapped = ProviderModelCatalog.Map(plain);
        var unusableMapped = ProviderModelCatalog.Map(unusable);
        var fixedMapped = ProviderModelCatalog.Map(fixedEffort);

        // Assert
        Assert.False(plainMapped.SupportsToolCalls);
        Assert.False(plainMapped.SupportsVision);
        Assert.False(plainMapped.SupportsReasoning);
        Assert.Null(plainMapped.ContextWindowTokens);
        Assert.Empty(plainMapped.ReasoningEfforts);
        Assert.False(unusableMapped.IsUsable);
        Assert.Equal("The model is disabled by your organization.", unusableMapped.UnusableReason);
        Assert.Empty(fixedMapped.ReasoningEfforts);
        Assert.Throws<ArgumentNullException>(() => ProviderModelCatalog.Map(null!));
    }

    /// <summary>
    ///     Test that GitHub Copilot lists every model of the shared runtime, usable and not, with the usable ones first and
    ///     each group by name, and asks the CLI that was found and not a second runtime.
    /// </summary>
    [Fact]
    public async Task ProviderModelCatalog_ListModels_Copilot_ListsEveryModelOfTheSharedRuntime()
    {
        // Arrange: a runtime with models in no order, one of which cannot be used
        var source = new FakeCopilotAgentSource(
            new ScriptedChatClient(),
            new FakeModelCatalog(
                "copilot",
                TestModels.Make("copilot", "zeta"),
                TestModels.Make("copilot", "disabled", usable: false),
                TestModels.Make("copilot", "alpha", toolCalls: false)));
        var starter = new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "Started.", source));
        var catalog = new ProviderModelCatalog(Locator(), starter, new FakeOllamaCatalogs(new FakeModelCatalog("ollama")));

        // Act
        var list = await catalog.ListModelsAsync(new AiProviderSettings(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert: all three, the usable ones first by name, no message, and one start on the CLI that was found
        Assert.Equal(["alpha", "zeta", "disabled"], list.Models.Select(model => model.Id).ToArray());
        Assert.Equal(string.Empty, list.Message);
        Assert.False(list.Models[0].SupportsToolCalls);
        Assert.False(list.Models[2].IsUsable);
        var request = Assert.Single(starter.Requests);
        Assert.Equal((DevMachine.RealCli, new Version(1, 0, 114)), (request.CliPath, request.CliVersion));
        Assert.Equal(0, source.Catalog.Refreshes);
    }

    /// <summary>
    ///     Test that a refresh asks the runtime for the models again and that a plain list does not.
    /// </summary>
    [Fact]
    public async Task ProviderModelCatalog_ListModels_CopilotRefresh_AsksTheRuntimeAgain()
    {
        // Arrange
        var source = new FakeCopilotAgentSource(new ScriptedChatClient(), new FakeModelCatalog("copilot", TestModels.Make("copilot", "alpha")));
        var catalog = CopilotCatalog(source);
        var token = TestContext.Current.CancellationToken;

        // Act: a plain list, then a refresh
        await catalog.ListModelsAsync(new AiProviderSettings(), false, token);
        var refreshedBefore = source.Catalog.Refreshes;
        var list = await catalog.ListModelsAsync(new AiProviderSettings(), true, token);

        // Assert
        Assert.Equal(0, refreshedBefore);
        Assert.Equal(1, source.Catalog.Refreshes);
        Assert.Single(list.Models);
    }

    /// <summary>
    ///     Test that GitHub Copilot answers with no models and one sentence, and never an exception, when the CLI is missing,
    ///     the runtime cannot be started, a runtime is started without a source, or the catalog cannot be read.
    /// </summary>
    [Fact]
    public async Task ProviderModelCatalog_ListModels_CopilotCannotBeAsked_IsAMessageAndNoModels()
    {
        // Arrange: a machine without the CLI, starters that refuse, one that gives no source, and a catalog that fails
        var token = TestContext.Current.CancellationToken;
        var empty = new ProviderModelCatalog(
            new CopilotCliLocator(new FakeProcessRunner(), new FakeCliFileSystem(), new FakeEnvironment(DevMachine.LocalAppData, @"C:\Windows")),
            new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Started, "unused")),
            new FakeOllamaCatalogs(new FakeModelCatalog("ollama")));
        var refused = CopilotCatalog(new FakeCopilotAgentSource(new ScriptedChatClient()), new CopilotStartResult(CopilotStartStatus.NotSignedIn, "Sign in first."));
        var sourceless = CopilotCatalog(new FakeCopilotAgentSource(new ScriptedChatClient()), new CopilotStartResult(CopilotStartStatus.Started, "Started."));
        var failing = new FakeCopilotAgentSource(new ScriptedChatClient(), new FakeModelCatalog("copilot") { Failure = new InvalidOperationException("The Copilot runtime could not list models: it is not signed in.") });
        var stopped = new FakeCopilotAgentSource(new ScriptedChatClient(), new FakeModelCatalog("copilot") { Failure = new ObjectDisposedException("runtime") });

        // Act
        var missing = await empty.ListModelsAsync(new AiProviderSettings(), cancellationToken: token);
        var notSignedIn = await refused.ListModelsAsync(new AiProviderSettings(), cancellationToken: token);
        var noSource = await sourceless.ListModelsAsync(new AiProviderSettings(), cancellationToken: token);
        var unreadable = await CopilotCatalog(failing).ListModelsAsync(new AiProviderSettings(), cancellationToken: token);
        var wasStopped = await CopilotCatalog(stopped).ListModelsAsync(new AiProviderSettings(), cancellationToken: token);

        // Assert: no models and a sentence each time
        Assert.All(new[] { missing, notSignedIn, noSource, unreadable, wasStopped }, list => Assert.Empty(list.Models));
        Assert.Contains("winget install GitHub.Copilot", missing.Message, StringComparison.Ordinal);
        Assert.Equal("Sign in first.", notSignedIn.Message);
        Assert.Equal(AgentProviderFactory.NoAgentSourceMessage, noSource.Message);
        Assert.Contains("could not list models", unreadable.Message, StringComparison.Ordinal);
        Assert.Contains("Refresh models", wasStopped.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that Ollama lists the models of the server at the address of the settings, with the capabilities that the
    ///     catalog knows, and asks for a new description on a refresh.
    /// </summary>
    [Fact]
    public async Task ProviderModelCatalog_ListModels_Ollama_ListsTheModelsOfTheServerAtTheAddress()
    {
        // Arrange: a server with two models, the second of which cannot call tools
        var model = new FakeModelCatalog(
            "ollama",
            TestModels.Make("ollama", "gemma3:12b", efforts: ["low", "high"], windowTokens: 131_072),
            TestModels.Make("ollama", "embed:latest", toolCalls: false, usable: false));
        var catalogs = new FakeOllamaCatalogs(model);
        var catalog = new ProviderModelCatalog(
            Locator(),
            new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Failed, "unused")),
            catalogs);
        var settings = new AiProviderSettings { Provider = AiProviderKind.Ollama, OllamaEndpoint = " http://ollama.test:12345 " };
        var token = TestContext.Current.CancellationToken;

        // Act
        var list = await catalog.ListModelsAsync(settings, cancellationToken: token);
        await catalog.ListModelsAsync(settings, true, token);

        // Assert: both models, the usable one first, asked at the trimmed address, and described again on the refresh
        Assert.Equal(["gemma3:12b", "embed:latest"], list.Models.Select(m => m.Id).ToArray());
        Assert.Equal(["low", "high"], list.Models[0].ReasoningEfforts.Select(effort => effort.Id).ToArray());
        Assert.False(list.Models[1].SupportsToolCalls);
        Assert.Equal(string.Empty, list.Message);
        Assert.All(catalogs.Endpoints, endpoint => Assert.Equal("http://ollama.test:12345/", endpoint.AbsoluteUri));
        Assert.Equal(1, model.Refreshes);
    }

    /// <summary>
    ///     Test that an Ollama server that cannot be asked gives no models and a sentence that names the address and says
    ///     that the name can still be typed, that a server without models says what to pull, and that an address that is no
    ///     address is told as the factory tells it.
    /// </summary>
    [Fact]
    public async Task ProviderModelCatalog_ListModels_OllamaCannotBeAsked_IsAMessageAndNoModels()
    {
        // Arrange: servers that refuse, answer something else, and have no model
        var token = TestContext.Current.CancellationToken;
        var settings = new AiProviderSettings { Provider = AiProviderKind.Ollama, OllamaEndpoint = "http://localhost:11434" };
        var refused = OllamaCatalog(new FakeModelCatalog("ollama") { Failure = new HttpRequestException("Connection refused.") });
        var notOllama = OllamaCatalog(new FakeModelCatalog("ollama") { Failure = new System.Text.Json.JsonException("Not JSON.") });
        var timedOut = OllamaCatalog(new FakeModelCatalog("ollama") { Failure = new TaskCanceledException("Timed out.") });
        var noModels = OllamaCatalog(new FakeModelCatalog("ollama"));

        // Act
        var connection = await refused.ListModelsAsync(settings, cancellationToken: token);
        var other = await notOllama.ListModelsAsync(settings, cancellationToken: token);
        var slow = await timedOut.ListModelsAsync(settings, cancellationToken: token);
        var none = await noModels.ListModelsAsync(settings, cancellationToken: token);
        var badAddress = await noModels.ListModelsAsync(settings with { OllamaEndpoint = "not an address" }, cancellationToken: token);

        // Assert
        Assert.All(new[] { connection, other, slow }, list =>
        {
            Assert.Empty(list.Models);
            Assert.Contains("Ollama did not answer at http://localhost:11434", list.Message, StringComparison.Ordinal);
            Assert.Contains("type the name", list.Message, StringComparison.Ordinal);
        });
        Assert.Equal(ProviderModelCatalog.NoOllamaModelsMessage, none.Message);
        Assert.Empty(none.Models);
        Assert.Equal(AgentProviderFactory.InvalidEndpointMessage, badAddress.Message);
    }

    /// <summary>
    ///     Test that a cancellation by the caller is an exception, not a message, for both providers.
    /// </summary>
    [Fact]
    public async Task ProviderModelCatalog_ListModels_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange: a token that is already cancelled
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var source = new FakeCopilotAgentSource(new ScriptedChatClient(), new FakeModelCatalog("copilot", TestModels.Make("copilot", "alpha")));
        var copilot = CopilotCatalog(source);
        var ollama = OllamaCatalog(new FakeModelCatalog("ollama", TestModels.Make("ollama", "m:latest")));

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => copilot.ListModelsAsync(new AiProviderSettings(), cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ollama.ListModelsAsync(new AiProviderSettings { Provider = AiProviderKind.Ollama }, cancellationToken: cancellation.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => copilot.ListModelsAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test the real Ollama catalog on a stand-in server: the models and what they can do come from the server's own
    ///     description, an embedding model is listed as unusable, a model that does not think offers no reasoning level, a
    ///     second list within the cache time asks the server nothing, and a refresh asks again.
    /// </summary>
    [Fact]
    public async Task ProviderModelCatalog_ListModels_OllamaOnStandInServer_MapsWhatTheServerDescribes()
    {
        // Arrange: a server whose one description is a thinking model with tools and vision
        var handler = new QueuedOllamaHandler
        {
            Tags = """{"models":[{"name":"qwen3-vl:8b","model":"qwen3-vl:8b","size":1,"details":{"family":"qwen3"}},{"name":"embed:latest","model":"embed:latest","size":1,"details":{"family":"bert"}}]}""",
        };
        handler.Show = """{"capabilities":["completion","tools","vision","thinking"],"model_info":{"general.architecture":"qwen3","qwen3.context_length":262144},"details":{"family":"qwen3"}}""";
        var catalog = new ProviderModelCatalog(
            Locator(),
            new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Failed, "unused")),
            new OllamaModelCatalogs(handler));
        var settings = new AiProviderSettings { Provider = AiProviderKind.Ollama, OllamaEndpoint = "http://ollama.test:11434" };
        var token = TestContext.Current.CancellationToken;

        // Act: list twice, then refresh
        var first = await catalog.ListModelsAsync(settings, cancellationToken: token);
        var requestsAfterFirst = handler.Requests.Count;
        await catalog.ListModelsAsync(settings, cancellationToken: token);
        var requestsAfterSecond = handler.Requests.Count;
        await catalog.ListModelsAsync(settings, true, token);

        // Assert: what the server described, the cache, and the refresh
        var qwen = first.Models.Single(model => model.Id == "qwen3-vl:8b");
        Assert.True(qwen.SupportsToolCalls);
        Assert.True(qwen.SupportsVision);
        Assert.True(qwen.SupportsReasoning);
        Assert.Equal(262_144, qwen.ContextWindowTokens);
        Assert.Equal(["none", "low", "medium", "high"], qwen.ReasoningEfforts.Select(effort => effort.Id).ToArray());
        Assert.Equal(requestsAfterFirst, requestsAfterSecond);
        Assert.True(handler.Requests.Count > requestsAfterSecond);
        Assert.All(handler.Requests, request => Assert.Equal("ollama.test", request.Address.Host));
    }

    /// <summary>
    ///     Test that an unreachable stand-in server (an error status) becomes the message and no models through the real
    ///     catalog, so that the failure of the network is data for the page.
    /// </summary>
    [Fact]
    public async Task ProviderModelCatalog_ListModels_OllamaServerError_IsAMessageAndNoModels()
    {
        // Arrange: a server that answers every question with an error
        var handler = new QueuedOllamaHandler { Status = System.Net.HttpStatusCode.InternalServerError };
        var catalog = new ProviderModelCatalog(
            Locator(),
            new FakeCopilotRuntimeStarter(new CopilotStartResult(CopilotStartStatus.Failed, "unused")),
            new OllamaModelCatalogs(handler));

        // Act
        var list = await catalog.ListModelsAsync(
            new AiProviderSettings { Provider = AiProviderKind.Ollama, OllamaEndpoint = "http://ollama.test:11434" },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(list.Models);
        Assert.Contains("Ollama did not answer", list.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the catalogs of Ollama are kept for the address that was asked for last, and that a missing address is
    ///     refused.
    /// </summary>
    [Fact]
    public async Task OllamaModelCatalogs_For_SameAddress_SharesWhatTheServerSaid()
    {
        // Arrange: a server and the catalogs over it
        var handler = new QueuedOllamaHandler { Tags = """{"models":[]}""" };
        var catalogs = new OllamaModelCatalogs(handler);
        var address = new Uri("http://ollama.test:11434");
        var token = TestContext.Current.CancellationToken;

        // Act: list twice at the address, then at another
        await catalogs.For(address).ListAsync(null, token);
        await catalogs.For(address).ListAsync(null, token);
        var afterSame = handler.Requests.Count;
        await catalogs.For(new Uri("http://other.test:11434")).ListAsync(null, token);

        // Assert: the second list at the same address asked nothing, and the other address was asked
        Assert.Equal(1, afterSame);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("other.test", handler.Requests[1].Address.Host);
        Assert.Throws<ArgumentNullException>(() => catalogs.For(null!));
    }
}

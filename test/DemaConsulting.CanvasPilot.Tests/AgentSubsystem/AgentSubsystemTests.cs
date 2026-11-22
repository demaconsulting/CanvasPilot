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
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;

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

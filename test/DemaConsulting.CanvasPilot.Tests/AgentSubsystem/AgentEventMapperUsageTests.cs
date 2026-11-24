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
using Microsoft.Agents.AI;
using Microsoft.Copilot;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the usage mapping of <see cref="AgentEventMapper" />: the model multiplier, the reasoning tokens and
///     the model that the Copilot runtime reports on the raw event next to the usage content, built by hand.
/// </summary>
public sealed class AgentEventMapperUsageTests
{
    /// <summary>
    ///     Makes the raw usage event of the runtime for one model call.
    /// </summary>
    /// <param name="multiplier">The model multiplier.</param>
    /// <param name="reasoningTokens">The reasoning tokens.</param>
    /// <param name="model">The model.</param>
    /// <returns>The event.</returns>
    private static AssistantUsageEvent RawUsage(double multiplier, long reasoningTokens, string model)
    {
#pragma warning disable MCPSDK001 // The multiplier is experimental; the test builds the event of the runtime by hand
        return new AssistantUsageEvent
        {
            Data = new AssistantUsageData { Cost = multiplier, ReasoningTokens = reasoningTokens, Model = model },
        };
#pragma warning restore MCPSDK001
    }

    /// <summary>
    ///     Test that the usage content that carries the raw event of the runtime is mapped with the tokens of the AI library
    ///     and with the multiplier, the reasoning tokens, and the model of the raw event.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_UsageWithRawCopilotEvent_CarriesMultiplierReasoningAndModel()
    {
        // Arrange
        var mapper = new AgentEventMapper(AiProviderKind.GitHubCopilot, "claude-opus-5.5");
        var content = new UsageContent(new UsageDetails { InputTokenCount = 1200, OutputTokenCount = 340 })
        {
            RawRepresentation = RawUsage(15, 200, "claude-opus-5.5"),
        };

        // Act
        var events = mapper.Map(new AgentResponseUpdate(ChatRole.Assistant, [content]));

        // Assert
        var usage = Assert.IsType<AgentEvent.Usage>(Assert.Single(events));
        Assert.Equal((1200L, 340L, 200L, 15.0, "claude-opus-5.5"), (usage.InputTokens, usage.OutputTokens, usage.ReasoningTokens, usage.Multiplier, usage.Model));
    }

    /// <summary>
    ///     Test that a usage content without a raw event of the runtime (Ollama) carries tokens only, and that the
    ///     reasoning tokens of the AI library are taken when there is no raw event.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_UsageWithoutRawEvent_CarriesTokensOnly()
    {
        // Arrange
        var mapper = new AgentEventMapper(AiProviderKind.Ollama, "llama");
        var content = new UsageContent(new UsageDetails { InputTokenCount = 10, OutputTokenCount = 20, ReasoningTokenCount = 5 });

        // Act
        var events = mapper.Map(new AgentResponseUpdate(ChatRole.Assistant, [content]));

        // Assert
        var usage = Assert.IsType<AgentEvent.Usage>(Assert.Single(events));
        Assert.Equal((10L, 20L, 5L, (double?)null, (string?)null), (usage.InputTokens, usage.OutputTokens, usage.ReasoningTokens, usage.Multiplier, usage.Model));
    }
}

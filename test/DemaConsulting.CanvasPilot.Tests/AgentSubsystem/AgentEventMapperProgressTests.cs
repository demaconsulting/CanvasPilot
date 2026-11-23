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
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Copilot;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the progress mapping of <see cref="AgentEventMapper" />: the reasoning, tool-argument, and
///     response-size events of the Copilot SDK that the provider may carry as the raw representation of the content, built
///     by hand, mapped to typed progress events that carry counts and a path and never the content, and spaced by the
///     clock that the mapper is given.
/// </summary>
public sealed class AgentEventMapperProgressTests
{
    /// <summary>A text that stands for the content of a drawing, which must never be part of an event.</summary>
    private const string Sentinel = "Sentinel-Drawing-Content-4711";

    /// <summary>The clock of the mapper, which moves when a test says so.</summary>
    private readonly ManualTimeProvider _clock = new();

    /// <summary>
    ///     Makes the mapper of a turn on GitHub Copilot, on the clock of the test.
    /// </summary>
    /// <returns>The mapper.</returns>
    private AgentEventMapper Create()
    {
        return new AgentEventMapper(
            AiProviderKind.GitHubCopilot,
            "model",
            null,
            TestContext.Current.CancellationToken,
            _clock);
    }

    /// <summary>
    ///     Makes an update whose content carries the raw event of the provider.
    /// </summary>
    /// <param name="raw">The raw event.</param>
    /// <returns>The update.</returns>
    private static AgentResponseUpdate Raw(object raw)
    {
        return new AgentResponseUpdate(ChatRole.Assistant, [new AIContent { RawRepresentation = raw }]);
    }

    /// <summary>
    ///     Makes the event that says that the model wrote a piece of the arguments of a call.
    /// </summary>
    /// <param name="callId">The id of the call.</param>
    /// <param name="delta">The piece.</param>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>The event.</returns>
    private static AssistantToolCallDeltaEvent ToolDelta(string callId, string delta, string toolName = "project_write")
    {
        return new AssistantToolCallDeltaEvent
        {
            Data = new AssistantToolCallDeltaData { ToolCallId = callId, ToolName = toolName, InputDelta = delta },
        };
    }

    /// <summary>
    ///     Test that the pieces of the arguments of a call are counted in bytes, that the path is taken from the first
    ///     characters once it is whole, that the path forces an event whatever the interval, and that the content of the
    ///     arguments is never part of an event.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_ToolCallDeltas_CountBytesAndFindThePathWithoutTheContent()
    {
        // Arrange
        var mapper = Create();
        var first = "{\"pa";
        var second = "th\":\"diagrams/flow.svg\",\"content\":\"<svg>" + Sentinel;

        // Act: the first piece starts the call, the second completes the path within the same interval
        var started = mapper.Map(Raw(ToolDelta("call-1", first)));
        var named = mapper.Map(Raw(ToolDelta("call-1", second)));

        // Assert: a progress event with no path first, then one with the whole count and the path, and no content
        Assert.Equal(new AgentEvent.ToolProgress("call-1", "project_write", first.Length, null), Assert.Single(started));
        var progress = Assert.IsType<AgentEvent.ToolProgress>(Assert.Single(named));
        Assert.Equal(("call-1", "project_write", "diagrams/flow.svg"), (progress.CallId, progress.Name, progress.Path));
        Assert.Equal(Encoding.UTF8.GetByteCount(first + second), progress.ArgumentBytes);
        Assert.DoesNotContain(Sentinel, progress.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the bytes of a piece are counted as UTF-8 bytes and not as characters.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_ToolCallDelta_CountsUtf8Bytes()
    {
        // Arrange: a piece of three characters, two of which take more than one byte
        var mapper = Create();
        var piece = "aé€";

        // Act
        var events = mapper.Map(Raw(ToolDelta("call-1", piece)));

        // Assert
        var progress = Assert.IsType<AgentEvent.ToolProgress>(Assert.Single(events));
        Assert.Equal(1 + 2 + 3, progress.ArgumentBytes);
    }

    /// <summary>
    ///     Test that many pieces within the interval give one event, that the next event after the interval carries the
    ///     whole count so far, and that a phase change gives an event at once.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_ManyDeltas_AreSpacedByTheIntervalAndNotLost()
    {
        // Arrange
        var mapper = Create();
        var events = new List<AgentEvent>();

        // Act: 100 pieces of 10 bytes, one millisecond apart, which is less than the interval in all
        for (var i = 0; i < 100; i++)
        {
            events.AddRange(mapper.Map(Raw(ToolDelta("call-1", "0123456789"))));
            _clock.Advance(TimeSpan.FromMilliseconds(1));
        }

        var withinTheInterval = events.Count;

        // Act: the interval has passed, and one more piece arrives
        _clock.Advance(AgentEventMapper.ProgressInterval);
        events.AddRange(mapper.Map(Raw(ToolDelta("call-1", "0123456789"))));

        // Assert: one event for the first interval, then one with all the bytes so far
        Assert.Equal(1, withinTheInterval);
        Assert.Equal(2, events.Count);
        Assert.Equal(1010, Assert.IsType<AgentEvent.ToolProgress>(events[^1]).ArgumentBytes);
    }

    /// <summary>
    ///     Test that a path that is not whole in the first characters, or that comes after them, is not reported, and that
    ///     only the first characters are ever searched.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_PathAfterTheScanLength_IsNotReported()
    {
        // Arrange: arguments whose path comes after more than the characters that are searched
        var mapper = Create();
        var padding = "{\"content\":\"" + new string('x', AgentEventMapper.PathScanLength) + "\",";

        // Act
        mapper.Map(Raw(ToolDelta("call-1", padding)));
        _clock.Advance(AgentEventMapper.ProgressInterval);
        var late = mapper.Map(Raw(ToolDelta("call-1", "\"path\":\"late.svg\"}")));

        // Assert
        Assert.Null(Assert.IsType<AgentEvent.ToolProgress>(Assert.Single(late)).Path);
    }

    /// <summary>
    ///     Test that a path with an escaped quote and a split piece is read whole, and that a call without an id or a name
    ///     is ignored.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_EscapedPathAndUnidentifiedCalls_AreHandled()
    {
        // Arrange
        var mapper = Create();

        // Act
        var unidentified = mapper.Map(Raw(ToolDelta(string.Empty, "{}")));
        var unnamed = mapper.Map(Raw(ToolDelta("call-9", "{}", string.Empty)));
        mapper.Map(Raw(ToolDelta("call-1", "{ \"path\" : \"a\\\"b")));
        var named = mapper.Map(Raw(ToolDelta("call-1", ".svg\" }")));

        // Assert
        Assert.Empty(unidentified);
        Assert.Empty(unnamed);
        Assert.Equal("a\"b.svg", Assert.IsType<AgentEvent.ToolProgress>(Assert.Single(named)).Path);
    }

    /// <summary>
    ///     Test that the size of the response is reported as the model working, spaced by the interval.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_StreamingDeltas_AreModelWorkingSpacedByTheInterval()
    {
        // Arrange
        var mapper = Create();
        static AssistantStreamingDeltaEvent Delta(long bytes) => new() { Data = new AssistantStreamingDeltaData { TotalResponseSizeBytes = bytes } };

        // Act
        var first = mapper.Map(Raw(Delta(100)));
        var second = mapper.Map(Raw(Delta(200)));
        _clock.Advance(AgentEventMapper.ProgressInterval);
        var third = mapper.Map(Raw(Delta(300)));

        // Assert
        Assert.Equal(new AgentEvent.ModelWorking(100), Assert.Single(first));
        Assert.Empty(second);
        Assert.Equal(new AgentEvent.ModelWorking(300), Assert.Single(third));
    }

    /// <summary>
    ///     Test that reasoning, as reasoning content or as the raw event, is reported once for the phase and not for every
    ///     piece, carries no words, and is reported again after the phase was left.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_Reasoning_IsThinkingOncePerPhaseWithoutWords()
    {
        // Arrange
        var mapper = Create();
        var reasoning = new AssistantReasoningDeltaEvent { Data = new AssistantReasoningDeltaData { DeltaContent = Sentinel, ReasoningId = "r1" } };
        var content = new AgentResponseUpdate(ChatRole.Assistant, [new TextReasoningContent(Sentinel)]);

        // Act
        var first = mapper.Map(content);
        var second = mapper.Map(Raw(reasoning));
        _clock.Advance(AgentEventMapper.ProgressInterval * 10);
        var third = mapper.Map(content);
        var text = mapper.Map(new AgentResponseUpdate(ChatRole.Assistant, [new TextContent("Hello")]));
        var again = mapper.Map(Raw(reasoning));

        // Assert
        Assert.Equal(new AgentEvent.Thinking(), Assert.Single(first));
        Assert.Empty(second);
        Assert.Empty(third);
        Assert.Equal(new AgentEvent.TextDelta("Hello"), Assert.Single(text));
        Assert.Equal(new AgentEvent.Thinking(), Assert.Single(again));
    }

    /// <summary>
    ///     Test that a change of phase is reported at once, within the interval, and that the start of the call that the
    ///     model was writing is reported after its progress.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_PhaseChange_IsReportedAtOnce()
    {
        // Arrange
        var mapper = Create();
        var streaming = new AssistantStreamingDeltaEvent { Data = new AssistantStreamingDeltaData { TotalResponseSizeBytes = 10 } };

        // Act: the model phase, then a tool phase, then the call itself, all at the same time
        var model = mapper.Map(Raw(streaming));
        var tool = mapper.Map(Raw(ToolDelta("call-1", "{}")));
        var call = mapper.Map(new AgentResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "project_write", null)]));
        var after = mapper.Map(Raw(streaming));

        // Assert
        Assert.IsType<AgentEvent.ModelWorking>(Assert.Single(model));
        Assert.IsType<AgentEvent.ToolProgress>(Assert.Single(tool));
        Assert.IsType<AgentEvent.ToolStarted>(Assert.Single(call));
        Assert.IsType<AgentEvent.ModelWorking>(Assert.Single(after));
    }

    /// <summary>
    ///     Test that an update that has no content but carries the raw event is mapped too, and that a raw event that is
    ///     not a progress event is ignored.
    /// </summary>
    [Fact]
    public void AgentEventMapper_Map_RawEventOnTheUpdate_IsMappedAndOtherRawEventsAreIgnored()
    {
        // Arrange
        var mapper = Create();
        var onUpdate = new AgentResponseUpdate(ChatRole.Assistant, []) { RawRepresentation = ToolDelta("call-1", "{}") };
        var other = new AgentResponseUpdate(ChatRole.Assistant, [new AIContent { RawRepresentation = new object() }]);

        // Act
        var mapped = mapper.Map(onUpdate);
        var ignored = mapper.Map(other);

        // Assert
        Assert.IsType<AgentEvent.ToolProgress>(Assert.Single(mapped));
        Assert.Empty(ignored);
    }

    /// <summary>
    ///     Test that the events of progress hold what the contract says: no empty ids, no negative counts.
    /// </summary>
    [Fact]
    public void AgentEvent_ProgressEvents_ValidateTheirArguments()
    {
        // Act and assert
        Assert.Throws<ArgumentException>(() => new AgentEvent.ToolProgress(string.Empty, "project_write", 1, null));
        Assert.Throws<ArgumentException>(() => new AgentEvent.ToolProgress("call-1", string.Empty, 1, null));
        Assert.Equal(0, new AgentEvent.ToolProgress("call-1", "project_write", -5, "  ").ArgumentBytes);
        Assert.Null(new AgentEvent.ToolProgress("call-1", "project_write", 1, "  ").Path);
    }
}

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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the records of the Agent subsystem contract: the events, the turn request, and the readiness report.
/// </summary>
public sealed class AgentContractTests
{
    /// <summary>
    ///     Test that a tool event keeps a short description as it is, and cuts a long one to the limit with an ellipsis.
    /// </summary>
    [Fact]
    public void AgentEvent_ToolEvents_LongSummaries_AreCutToTheLimit()
    {
        // Arrange: descriptions of exactly the limit, one longer, and one with white space around it
        var atLimit = new string('a', AgentEvent.MaxSummaryLength);
        var tooLong = new string('b', AgentEvent.MaxSummaryLength + 50);

        // Act: build the events
        var started = new AgentEvent.ToolStarted("c1", "project_read", tooLong);
        var finished = new AgentEvent.ToolFinished("c1", "project_read", true, atLimit, false);
        var padded = new AgentEvent.ToolFinished("c2", "project_list", false, "  3 files  ", true);

        // Assert: the long one is cut with an ellipsis, the one at the limit is kept, and white space is trimmed
        Assert.Equal(AgentEvent.MaxSummaryLength, started.ArgumentSummary.Length);
        Assert.EndsWith("...", started.ArgumentSummary, StringComparison.Ordinal);
        Assert.Equal(atLimit, finished.Summary);
        Assert.Equal("3 files", padded.Summary);
        Assert.False(padded.Succeeded);
        Assert.True(padded.HasImage);
    }

    /// <summary>
    ///     Test that a missing summary becomes an empty one and that a tool event needs a call id and a name.
    /// </summary>
    [Fact]
    public void AgentEvent_ToolEvents_NullSummaryEmptyIdOrName_AreHandled()
    {
        // Act and assert: a missing summary is empty
        Assert.Equal(string.Empty, new AgentEvent.ToolStarted("c1", "t", null).ArgumentSummary);
        Assert.Equal(string.Empty, new AgentEvent.ToolFinished("c1", "t", true, null, false).Summary);

        // Act and assert: an empty or missing id or name is refused
        Assert.Throws<ArgumentException>(() => new AgentEvent.ToolStarted(string.Empty, "t", null));
        Assert.Throws<ArgumentException>(() => new AgentEvent.ToolStarted("c1", string.Empty, null));
        Assert.Throws<ArgumentNullException>(() => new AgentEvent.ToolFinished(null!, "t", true, null, false));
        Assert.Throws<ArgumentNullException>(() => new AgentEvent.ToolFinished("c1", null!, true, null, false));
    }

    /// <summary>
    ///     Test that a text delta needs text, which may be empty white space, and a failure needs a message.
    /// </summary>
    [Fact]
    public void AgentEvent_TextDeltaAndFailed_InvalidArguments_AreRefused()
    {
        // Act and assert: text may not be missing but may be white space, because a stream can break a word anywhere
        Assert.Throws<ArgumentNullException>(() => new AgentEvent.TextDelta(null!));
        Assert.Equal(" ", new AgentEvent.TextDelta(" ").Text);

        // Act and assert: a failure must say something
        Assert.Throws<ArgumentException>(() => new AgentEvent.Failed(AgentFailureKind.Unexpected, " "));
        Assert.Throws<ArgumentNullException>(() => new AgentEvent.Failed(AgentFailureKind.Unexpected, null!));
        var failed = new AgentEvent.Failed(AgentFailureKind.ModelNotFound, "Run ollama pull llama3.");
        Assert.Equal(AgentFailureKind.ModelNotFound, failed.Kind);
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

    /// <summary>
    ///     Test that events compare by value, so that a consumer can assert on a stream.
    /// </summary>
    [Fact]
    public void AgentEvent_Records_WithSameValues_AreEqual()
    {
        // Assert: equal values give equal events and different values do not
        Assert.Equal(new AgentEvent.TextDelta("a"), new AgentEvent.TextDelta("a"));
        Assert.Equal(new AgentEvent.ToolStarted("c", "n", "s"), new AgentEvent.ToolStarted("c", "n", "s"));
        Assert.Equal(new AgentEvent.FileWritten("a.svg", true), new AgentEvent.FileWritten("a.svg", true));
        Assert.NotEqual(new AgentEvent.FileWritten("a.svg", true), new AgentEvent.FileWritten("a.svg", false));
        Assert.Equal(new AgentEvent.Usage(1, null), new AgentEvent.Usage(1, null));
        Assert.Equal(new AgentEvent.Completed(), new AgentEvent.Completed());
    }

    /// <summary>
    ///     Test that a turn request keeps its payload, has no restored history unless it is given, and refuses a missing
    ///     payload.
    /// </summary>
    [Fact]
    public void AgentTurnRequest_Constructor_PayloadAndHistory_AreKept()
    {
        // Arrange: a payload and two earlier turns
        var payload = AgentSubsystemTests.CreatePayload("Hello");
        var history = new[]
        {
            new AgentHistoryTurn(AgentHistoryRole.User, "Earlier"),
            new AgentHistoryTurn(AgentHistoryRole.Assistant, "Reply"),
        };

        // Act: build a request without and with history
        var plain = new AgentTurnRequest(payload);
        var restored = new AgentTurnRequest(payload) { RestoredHistory = history };

        // Assert: the payload is kept, history is empty by default and in order when given, and null is refused
        Assert.Same(payload, plain.Payload);
        Assert.Empty(plain.RestoredHistory);
        Assert.Equal(history, restored.RestoredHistory);
        Assert.Throws<ArgumentNullException>(() => new AgentTurnRequest(null!));
    }

    /// <summary>
    ///     Test that a readiness report can be used for a ready provider and for one with a warning, and for no other
    ///     status.
    /// </summary>
    /// <param name="status">The status of the report.</param>
    /// <param name="expected">Whether a turn can be run.</param>
    [Theory]
    [InlineData(AgentReadinessStatus.Ready, true)]
    [InlineData(AgentReadinessStatus.OllamaContextTooSmall, true)]
    [InlineData(AgentReadinessStatus.NotConfigured, false)]
    [InlineData(AgentReadinessStatus.CopilotCliMissing, false)]
    [InlineData(AgentReadinessStatus.CopilotCliTooOld, false)]
    [InlineData(AgentReadinessStatus.CopilotNotSignedIn, false)]
    [InlineData(AgentReadinessStatus.CopilotRuntimeUnavailable, false)]
    [InlineData(AgentReadinessStatus.OllamaUnreachable, false)]
    [InlineData(AgentReadinessStatus.OllamaModelMissing, false)]
    public void AgentReadinessReport_IsUsable_ByStatus_IsTrueOnlyForReadyAndWarning(AgentReadinessStatus status, bool expected)
    {
        // Act: build a report of the status
        var report = new AgentReadinessReport(status, "A message.");

        // Assert: usable only for the two statuses that can run a turn
        Assert.Equal(expected, report.IsUsable);
        Assert.Equal(status, report.Status);
        Assert.Equal("A message.", report.Message);
    }

    /// <summary>
    ///     Test that a readiness report refuses a message that is missing or has no words.
    /// </summary>
    [Fact]
    public void AgentReadinessReport_Constructor_EmptyMessage_IsRefused()
    {
        // Act and assert: a report must say something
        Assert.Throws<ArgumentNullException>(() => new AgentReadinessReport(AgentReadinessStatus.Ready, null!));
        Assert.Throws<ArgumentException>(() => new AgentReadinessReport(AgentReadinessStatus.Ready, string.Empty));
        Assert.Throws<ArgumentException>(() => new AgentReadinessReport(AgentReadinessStatus.Ready, "  "));
    }
}

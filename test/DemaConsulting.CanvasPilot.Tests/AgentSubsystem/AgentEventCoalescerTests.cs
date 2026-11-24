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
///     Unit tests for <see cref="AgentEventCoalescer" />: what is merged while the reader lags and what never is, the order
///     of the events, the end of the stream, and the fault that the reader gets after the events before it.
/// </summary>
public sealed class AgentEventCoalescerTests
{
    /// <summary>
    ///     Reads every event of a coalescer that has ended.
    /// </summary>
    /// <param name="coalescer">The coalescer.</param>
    /// <returns>The events.</returns>
    private static async Task<List<AgentEvent>> ReadAllAsync(AgentEventCoalescer coalescer)
    {
        var events = new List<AgentEvent>();
        await foreach (var item in coalescer.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        return events;
    }

    /// <summary>
    ///     Test that pieces of text that wait are joined in order into one event.
    /// </summary>
    [Fact]
    public async Task AgentEventCoalescer_Add_AdjacentText_IsJoined()
    {
        // Arrange
        var coalescer = new AgentEventCoalescer();

        // Act
        coalescer.Add(new AgentEvent.TextDelta("Hel"));
        coalescer.Add(new AgentEvent.TextDelta("lo "));
        coalescer.Add(new AgentEvent.TextDelta("world"));
        coalescer.Complete();
        var events = await ReadAllAsync(coalescer);

        // Assert
        Assert.Equal(new AgentEvent.TextDelta("Hello world"), Assert.Single(events));
        Assert.Equal(2, coalescer.Merged);
    }

    /// <summary>
    ///     Test that progress is reduced to the latest of the same call, that progress of another call and of another kind
    ///     is kept, and that a repeated thinking is dropped.
    /// </summary>
    [Fact]
    public async Task AgentEventCoalescer_Add_Progress_KeepsOnlyTheLatestOfTheSameKind()
    {
        // Arrange
        var coalescer = new AgentEventCoalescer();

        // Act
        coalescer.Add(new AgentEvent.Thinking());
        coalescer.Add(new AgentEvent.Thinking());
        coalescer.Add(new AgentEvent.ModelWorking(10));
        coalescer.Add(new AgentEvent.ModelWorking(20));
        coalescer.Add(new AgentEvent.ToolProgress("a", "project_write", 100, null));
        coalescer.Add(new AgentEvent.ToolProgress("a", "project_write", 200, "x.svg"));
        coalescer.Add(new AgentEvent.ToolProgress("b", "project_write", 5, null));
        coalescer.Complete();
        var events = await ReadAllAsync(coalescer);

        // Assert
        Assert.Collection(
            events,
            first => Assert.Equal(new AgentEvent.Thinking(), first),
            second => Assert.Equal(new AgentEvent.ModelWorking(20), second),
            third => Assert.Equal(new AgentEvent.ToolProgress("a", "project_write", 200, "x.svg"), third),
            fourth => Assert.Equal(new AgentEvent.ToolProgress("b", "project_write", 5, null), fourth));
        Assert.Equal(3, coalescer.Merged);
    }

    /// <summary>
    ///     Test that the events that carry facts are never merged, dropped, or reordered, and that text that is not next to
    ///     other text stays where it is.
    /// </summary>
    [Fact]
    public async Task AgentEventCoalescer_Add_FactsAndTextAroundThem_AreKeptInOrder()
    {
        // Arrange: a turn whose events alternate between text, progress and facts
        var coalescer = new AgentEventCoalescer();
        AgentEvent[] given =
        [
            new AgentEvent.TextDelta("one"),
            new AgentEvent.ToolStarted("c1", "project_write", "path=a.svg"),
            new AgentEvent.TextDelta("two"),
            new AgentEvent.FileWritten("a.svg", false),
            new AgentEvent.ToolFinished("c1", "project_write", true, "ok", false),
            new AgentEvent.Usage(1, 2),
            new AgentEvent.Usage(3, 4),
            new AgentEvent.Completed(),
        ];

        // Act
        foreach (var item in given)
        {
            coalescer.Add(item);
        }

        coalescer.Complete();
        var events = await ReadAllAsync(coalescer);

        // Assert: exactly what was given, in the same order
        Assert.Equal(given, events);
        Assert.Equal(0, coalescer.Merged);
    }

    /// <summary>
    ///     Test that a reader that waits is woken by an event, gets the events in order, and ends when the stream ends.
    /// </summary>
    [Fact]
    public async Task AgentEventCoalescer_ReadAll_WaitsForEventsAndEndsWithTheStream()
    {
        // Arrange: a reader that starts before anything was added
        var coalescer = new AgentEventCoalescer();
        var reading = ReadAllAsync(coalescer);
        Assert.False(reading.IsCompleted);

        // Act
        coalescer.Add(new AgentEvent.TextDelta("a"));
        coalescer.Add(new AgentEvent.Completed());
        coalescer.Complete();
        coalescer.Add(new AgentEvent.TextDelta("after the end"));
        var events = await reading.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal<AgentEvent>([new AgentEvent.TextDelta("a"), new AgentEvent.Completed()], events);
    }

    /// <summary>
    ///     Test that a fault ends the stream after the events that were added before it, and that the reader gets it.
    /// </summary>
    [Fact]
    public async Task AgentEventCoalescer_Complete_WithAFault_ThrowsAfterTheEventsBeforeIt()
    {
        // Arrange
        var coalescer = new AgentEventCoalescer();
        coalescer.Add(new AgentEvent.TextDelta("kept"));
        coalescer.Complete(new InvalidOperationException("boom"));

        // Act
        var seen = new List<AgentEvent>();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var item in coalescer.ReadAllAsync(TestContext.Current.CancellationToken))
            {
                seen.Add(item);
            }
        });

        // Assert
        Assert.Equal("boom", failure.Message);
        Assert.Equal(new AgentEvent.TextDelta("kept"), Assert.Single(seen));
    }

    /// <summary>
    ///     Test that canceling the token ends the wait for the next event, that a null event is refused, and that a reader
    ///     whose token was canceled still gets the events that wait before it is canceled.
    /// </summary>
    [Fact]
    public async Task AgentEventCoalescer_ReadAll_TokenCanceled_EndsTheWaitAfterTheEventsThatWait()
    {
        // Arrange: an event waits, and the token is canceled before it is read
        var coalescer = new AgentEventCoalescer();
        coalescer.Add(new AgentEvent.TextDelta("kept"));
        using var canceledAlready = new CancellationTokenSource();
        await canceledAlready.CancelAsync();
        var seen = new List<AgentEvent>();

        // Act and assert: the event is handed over, and then the reader is canceled
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in coalescer.ReadAllAsync(canceledAlready.Token))
            {
                seen.Add(item);
            }
        });
        Assert.Equal(new AgentEvent.TextDelta("kept"), Assert.Single(seen));
    }

    /// <summary>
    ///     Test that canceling the token ends the wait for the next event, and that a null event is refused.
    /// </summary>
    [Fact]
    public async Task AgentEventCoalescer_ReadAll_TokenCanceled_EndsTheWait()
    {
        // Arrange
        var coalescer = new AgentEventCoalescer();
        using var cancellation = new CancellationTokenSource();
        var reading = Task.Run(
            async () =>
            {
                await foreach (var item in coalescer.ReadAllAsync(cancellation.Token))
                {
                    Assert.NotNull(item);
                }
            },
            TestContext.Current.CancellationToken);

        // Act
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => coalescer.Add(null!));
    }
}

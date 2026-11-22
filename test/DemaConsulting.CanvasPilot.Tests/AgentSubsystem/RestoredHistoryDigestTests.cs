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
///     Tests for the digest that stands for the restored turns of a conversation at the start of a new provider session.
/// </summary>
public sealed class RestoredHistoryDigestTests
{
    /// <summary>
    ///     Test that a history without words has no digest.
    /// </summary>
    [Fact]
    public void RestoredHistoryDigest_Build_NoWords_ReturnsNull()
    {
        // Act and assert: an empty history and a history of blank turns have no digest, and a missing history is refused
        Assert.Null(RestoredHistoryDigest.Build([]));
        Assert.Null(RestoredHistoryDigest.Build([new AgentHistoryTurn(AgentHistoryRole.User, "  \r\n "), new AgentHistoryTurn(AgentHistoryRole.Assistant, string.Empty)]));
        Assert.Throws<ArgumentNullException>(() => RestoredHistoryDigest.Build(null!));
    }

    /// <summary>
    ///     Test that a short history is written whole, oldest first, with the role of each turn, and with white space made
    ///     into single spaces.
    /// </summary>
    [Fact]
    public void RestoredHistoryDigest_Build_ShortHistory_KeepsEveryTurnInOrder()
    {
        // Act
        var digest = RestoredHistoryDigest.Build(
        [
            new AgentHistoryTurn(AgentHistoryRole.User, "Draw   a\r\nbox"),
            new AgentHistoryTurn(AgentHistoryRole.Assistant, "  I drew it. "),
            new AgentHistoryTurn(AgentHistoryRole.User, "  "),
            new AgentHistoryTurn(AgentHistoryRole.User, "Make it blue"),
        ]);

        // Assert: the header, then three lines, with no note about omitted turns
        Assert.Equal($"{RestoredHistoryDigest.Header}\nUser: Draw a box\nAssistant: I drew it.\nUser: Make it blue", digest);
    }

    /// <summary>
    ///     Test that a long history keeps the newest turns that fit in the limit, says how many were left out, and never
    ///     exceeds the limit, whatever the history holds.
    /// </summary>
    /// <param name="turns">The number of turns.</param>
    /// <param name="turnLength">The length of each turn in characters.</param>
    [Theory]
    [InlineData(10, 100)]
    [InlineData(200, 50)]
    [InlineData(200, 400)]
    [InlineData(40, 5_000)]
    [InlineData(1, 100_000)]
    public void RestoredHistoryDigest_Build_LongHistory_StaysWithinTheLimitAndKeepsTheNewestTurns(int turns, int turnLength)
    {
        // Arrange: numbered turns of the given length
        var history = Enumerable.Range(1, turns)
            .Select(i => new AgentHistoryTurn(i % 2 == 1 ? AgentHistoryRole.User : AgentHistoryRole.Assistant, $"#{i}# " + new string('w', turnLength)))
            .ToArray();

        // Act
        var digest = RestoredHistoryDigest.Build(history)!;

        // Assert: within the limit, the newest turn is there, and a note counts the turns that are not
        Assert.True(digest.Length <= RestoredHistoryDigest.MaxCharacters, $"length {digest.Length}");
        Assert.Contains($"#{turns}#", digest, StringComparison.Ordinal);
        var kept = digest.Split('\n').Count(line => line.StartsWith("User: #", StringComparison.Ordinal) || line.StartsWith("Assistant: #", StringComparison.Ordinal));
        if (kept < turns)
        {
            Assert.Contains($"[{turns - kept} earlier turn{(turns - kept == 1 ? " was" : "s were")} left out.]", digest, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("left out", digest, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Test that one very long turn is cut to the limit of a turn with an ellipsis.
    /// </summary>
    [Fact]
    public void RestoredHistoryDigest_Build_VeryLongTurn_IsCutWithAnEllipsis()
    {
        // Act
        var digest = RestoredHistoryDigest.Build([new AgentHistoryTurn(AgentHistoryRole.Assistant, new string('a', 10_000))])!;

        // Assert: the line has the role, the cut text, and the ellipsis
        var line = digest.Split('\n')[1];
        Assert.Equal("Assistant: ".Length + RestoredHistoryDigest.MaxTurnCharacters, line.Length);
        Assert.EndsWith("...", line, StringComparison.Ordinal);
    }
}

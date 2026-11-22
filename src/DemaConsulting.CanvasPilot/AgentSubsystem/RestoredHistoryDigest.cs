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

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Writes the one block of text that stands for the earlier turns of a restored conversation when a new provider
///     session starts: a header, the newest turns that fit, and a note that says how many older turns were left out.
/// </summary>
/// <remarks>
///     The digest lives in memory only. The agent service puts it at the start of the first message of a new provider
///     session and never writes it anywhere, and the log never gets its text. The block is at most
///     <see cref="MaxCharacters" /> characters long, however long the history is. The class is pure and safe for
///     concurrent use.
/// </remarks>
internal static class RestoredHistoryDigest
{
    /// <summary>
    ///     The most characters that the digest has.
    /// </summary>
    internal const int MaxCharacters = 6_000;

    /// <summary>
    ///     The most characters that one turn keeps; a longer turn is cut with an ellipsis.
    /// </summary>
    internal const int MaxTurnCharacters = 1_200;

    /// <summary>
    ///     The first line of the digest, which tells the model what the block is.
    /// </summary>
    internal const string Header =
        "[Earlier conversation, restored from the saved text of this project. Pictures and tool results were not kept.]";

    /// <summary>
    ///     The room that the note about omitted turns may need.
    /// </summary>
    private const int OmittedNoteReserve = 80;

    /// <summary>
    ///     Writes the digest of a history.
    /// </summary>
    /// <param name="history">The earlier turns, oldest first.</param>
    /// <returns>The digest, or <see langword="null" /> when the history holds no words.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the history is <see langword="null" />.</exception>
    internal static string? Build(IReadOnlyList<AgentHistoryTurn> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        // Normalize each turn to one line, and drop the turns that hold no words
        var lines = new List<string>(history.Count);
        foreach (var turn in history)
        {
            var text = Normalize(turn?.Text);
            if (text.Length == 0)
            {
                continue;
            }

            var role = turn!.Role == AgentHistoryRole.Assistant ? "Assistant" : "User";
            lines.Add($"{role}: {text}");
        }

        if (lines.Count == 0)
        {
            return null;
        }

        // Keep the newest turns that fit, because the end of a conversation matters most
        var remaining = MaxCharacters - Header.Length - OmittedNoteReserve;
        var kept = 0;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var cost = lines[i].Length + 1;
            if (cost > remaining)
            {
                break;
            }

            remaining -= cost;
            kept++;
        }

        var builder = new StringBuilder(Header);
        var omitted = lines.Count - kept;
        if (omitted > 0)
        {
            builder.Append('\n').Append('[').Append(omitted).Append(omitted == 1 ? " earlier turn was left out.]" : " earlier turns were left out.]");
        }

        for (var i = lines.Count - kept; i < lines.Count; i++)
        {
            builder.Append('\n').Append(lines[i]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Makes the text of a turn one line of at most <see cref="MaxTurnCharacters" /> characters.
    /// </summary>
    /// <param name="text">The text, which may be missing.</param>
    /// <returns>The line; empty when there are no words.</returns>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Runs of white space (including line breaks) become one space, so a turn is one line
        var builder = new StringBuilder(Math.Min(text.Length, MaxTurnCharacters + 3));
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
            if (builder.Length > MaxTurnCharacters)
            {
                break;
            }
        }

        if (builder.Length <= MaxTurnCharacters)
        {
            return builder.ToString();
        }

        builder.Length = MaxTurnCharacters - 3;
        return builder.Append("...").ToString();
    }
}

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

using System.Globalization;
using System.Text;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Turns the rows of the transcript into the rows of the saved history, the saved rows back into rows of the
///     transcript, and the saved rows into the text turns that a new provider session starts from. It keeps words only:
///     no picture and no payload leave the transcript.
/// </summary>
/// <remarks>
///     The class is stateless and has no file access. A notice is not part of the conversation and is never saved, nor is
///     a tool call that has no result.
/// </remarks>
internal static class ChatHistoryMapper
{
    /// <summary>
    ///     Makes the saved rows of the rows of a turn: the message and what followed it.
    /// </summary>
    /// <param name="rows">The rows of the transcript, oldest first.</param>
    /// <param name="utc">The time at which the turn ended, which dates the rows that have no time of their own.</param>
    /// <returns>The saved rows, oldest first.</returns>
    public static IReadOnlyList<ChatHistoryRow> ToHistoryRows(IEnumerable<TranscriptRowViewModel> rows, DateTimeOffset utc)
    {
        var result = new List<ChatHistoryRow>();
        foreach (var row in rows)
        {
            switch (row)
            {
                case TranscriptEntryViewModel entry:
                    result.Add(ToHistoryRow(entry));
                    break;

                case AssistantReplyViewModel { HasText: true } reply:
                    result.Add(new ChatHistoryRow
                    {
                        Kind = ChatHistoryRowKind.Assistant,
                        Utc = utc,
                        Text = reply.Text,
                        Reply = reply.State switch
                        {
                            AssistantReplyState.Stopped => ChatHistoryReplyState.Stopped,
                            AssistantReplyState.Failed => ChatHistoryReplyState.Failed,
                            _ => ChatHistoryReplyState.Completed,
                        },
                    });
                    break;

                case ToolActivityRowViewModel { IsRunning: false } tool:
                    result.Add(new ChatHistoryRow
                    {
                        Kind = ChatHistoryRowKind.Tool,
                        Utc = utc,
                        Tool = tool.ToolName,
                        Text = tool.Summary,
                        Succeeded = tool.Succeeded,
                    });
                    break;
            }
        }

        return result;
    }

    /// <summary>
    ///     Makes the row of the transcript that shows a saved row.
    /// </summary>
    /// <param name="row">The saved row.</param>
    /// <returns>The row of the transcript.</returns>
    public static TranscriptRowViewModel ToTranscriptRow(ChatHistoryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.Kind switch
        {
            ChatHistoryRowKind.User => new RestoredEntryViewModel(row),
            ChatHistoryRowKind.Assistant => new AssistantReplyViewModel(
                row.Text,
                row.Reply switch
                {
                    ChatHistoryReplyState.Stopped => AssistantReplyState.Stopped,
                    ChatHistoryReplyState.Failed => AssistantReplyState.Failed,
                    _ => AssistantReplyState.Completed,
                }),
            _ => new ToolActivityRowViewModel(row.Tool ?? "tool", row.Text, row.Succeeded ?? true),
        };
    }

    /// <summary>
    ///     Makes the text turns that a new provider session starts from, so that the conversation goes on after the
    ///     application was restarted. A tool call is a line of the assistant; pictures and results are not there.
    /// </summary>
    /// <param name="rows">The saved rows, oldest first.</param>
    /// <returns>The turns, oldest first.</returns>
    public static IReadOnlyList<AgentHistoryTurn> ToAgentTurns(IReadOnlyList<ChatHistoryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var turns = new List<AgentHistoryTurn>(rows.Count);
        foreach (var row in rows)
        {
            switch (row.Kind)
            {
                case ChatHistoryRowKind.User:
                    turns.Add(new AgentHistoryTurn(AgentHistoryRole.User, DescribeUserRow(row)));
                    break;

                case ChatHistoryRowKind.Assistant:
                    turns.Add(new AgentHistoryTurn(AgentHistoryRole.Assistant, DescribeAssistantRow(row)));
                    break;

                case ChatHistoryRowKind.Tool:
                    var outcome = row.Succeeded == false ? " (it failed)" : string.Empty;
                    turns.Add(new AgentHistoryTurn(AgentHistoryRole.Assistant, $"[Used the tool {row.Tool}: {row.Text}{outcome}]"));
                    break;
            }
        }

        return turns;
    }

    /// <summary>
    ///     Makes the saved row of a message that was sent.
    /// </summary>
    /// <param name="entry">The row of the message in the transcript.</param>
    /// <returns>The saved row.</returns>
    private static ChatHistoryRow ToHistoryRow(TranscriptEntryViewModel entry)
    {
        var message = entry.Message;
        var text = new StringBuilder();
        foreach (var segment in message.Message.Segments)
        {
            switch (segment)
            {
                case TextSegment words:
                    text.Append(words.Text);
                    break;

                case MarkChip mark:
                    text.Append('[').Append(mark.Label).Append(']');
                    break;

                case AttachmentChip attachment:
                    text.Append("[Attachment: ").Append(attachment.Name).Append(']');
                    break;
            }
        }

        // The paths of the attachments and the notes of the marks go next to the words, so that a person can read the row
        var attachments = message.Attachments.Select(staged => staged.ProjectRelativePath).ToList();
        var marks = message.Marks.OrderBy(mark => mark.Number).Select(mark => new ChatHistoryMark(mark.Number, mark.Note)).ToList();
        return new ChatHistoryRow
        {
            Kind = ChatHistoryRowKind.User,
            Utc = message.SentUtc,
            Text = text.ToString(),
            Attachments = attachments.Count > 0 ? attachments : null,
            Marks = marks.Count > 0 ? marks : null,
        };
    }

    /// <summary>
    ///     Writes the words that the copilot is given for a message of the user.
    /// </summary>
    /// <param name="row">The saved row.</param>
    /// <returns>The words: the text, the notes of the marks, and the attachments.</returns>
    private static string DescribeUserRow(ChatHistoryRow row)
    {
        var builder = new StringBuilder(row.Text);
        foreach (var mark in (row.Marks ?? []).Where(mark => !string.IsNullOrWhiteSpace(mark.Note)).OrderBy(mark => mark.Number))
        {
            builder.Append(string.Create(CultureInfo.InvariantCulture, $" (Mark {mark.Number}: {mark.Note})"));
        }

        if (row.Attachments is { Count: > 0 } attachments)
        {
            builder.Append(" (Attached: ").AppendJoin(", ", attachments).Append(')');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Writes the words that the copilot is given for something that it said.
    /// </summary>
    /// <param name="row">The saved row.</param>
    /// <returns>The text, with a remark when the reply did not end normally.</returns>
    private static string DescribeAssistantRow(ChatHistoryRow row)
    {
        return row.Reply switch
        {
            ChatHistoryReplyState.Stopped => row.Text + " [The reply was stopped.]",
            ChatHistoryReplyState.Failed => row.Text + " [The reply failed.]",
            _ => row.Text,
        };
    }
}

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

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     What a saved row of the chat history is.
/// </summary>
public enum ChatHistoryRowKind
{
    /// <summary>A message that the user sent.</summary>
    User,

    /// <summary>The text that the AI copilot wrote.</summary>
    Assistant,

    /// <summary>A tool that the AI copilot called, as one line about what it did.</summary>
    Tool,
}

/// <summary>
///     How a saved reply of the AI copilot ended.
/// </summary>
public enum ChatHistoryReplyState
{
    /// <summary>The reply is whole.</summary>
    Completed,

    /// <summary>The user stopped the turn; the text is what had been written until then.</summary>
    Stopped,

    /// <summary>The turn failed; the text is what had been written until then.</summary>
    Failed,
}

/// <summary>
///     The number and the note of a mark that was drawn on a picture, as the history keeps them. The mark's pictures are
///     not kept.
/// </summary>
/// <param name="Number">The number of the mark; at least 1.</param>
/// <param name="Note">The note that the user gave the mark, or <see langword="null" /> for none.</param>
public sealed record ChatHistoryMark(int Number, string? Note);

/// <summary>
///     One row of the chat history that is kept on the computer of the user: words only. A row holds no picture, no
///     payload, and no tool result, so that what is saved is small and can be read by a person.
/// </summary>
/// <remarks>
///     <para>
///         <b>User rows</b> hold the words of the message with the labels of its chips in their places (<c>[Mark 1]</c>,
///         <c>[Attachment: name]</c>), the project-relative paths of the attachments, and the numbers and notes of the
///         marks. <b>Assistant rows</b> hold the text and how the reply ended. <b>Tool rows</b> hold the name of the tool,
///         the sentence about what it did, and whether it worked.
///     </para>
///     <para>
///         The record is the shape of the saved file; a property that does not belong to the kind of the row is
///         <see langword="null" />, and the file leaves it out.
///     </para>
/// </remarks>
public sealed record ChatHistoryRow
{
    /// <summary>
    ///     Gets what the row is.
    /// </summary>
    public ChatHistoryRowKind Kind { get; init; }

    /// <summary>
    ///     Gets when the message was sent (user rows) or the turn ended (the other rows), in UTC.
    /// </summary>
    public DateTimeOffset Utc { get; init; }

    /// <summary>
    ///     Gets the words of a user row with the labels of its chips, or the text of an assistant row. For a tool row it is
    ///     the sentence about what the tool did.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the project-relative paths of the attachments of a user row, or <see langword="null" /> for none.
    /// </summary>
    public IReadOnlyList<string>? Attachments { get; init; }

    /// <summary>
    ///     Gets the marks of a user row, or <see langword="null" /> for none.
    /// </summary>
    public IReadOnlyList<ChatHistoryMark>? Marks { get; init; }

    /// <summary>
    ///     Gets the name of the tool of a tool row, or <see langword="null" /> for another kind of row.
    /// </summary>
    public string? Tool { get; init; }

    /// <summary>
    ///     Gets whether the tool of a tool row did what was asked, or <see langword="null" /> for another kind of row.
    /// </summary>
    public bool? Succeeded { get; init; }

    /// <summary>
    ///     Gets how the reply of an assistant row ended, or <see langword="null" /> for another kind of row.
    /// </summary>
    public ChatHistoryReplyState? Reply { get; init; }
}

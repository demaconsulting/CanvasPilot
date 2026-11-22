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

using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The outcome of saving or clearing the chat history. A history that cannot be written is a result, not an
///     exception, because it must never fail the conversation.
/// </summary>
/// <param name="Succeeded">Whether the history was saved or cleared.</param>
/// <param name="Message">A sentence that says what went wrong, or an empty string when it worked.</param>
public sealed record ChatHistoryResult(bool Succeeded, string Message)
{
    /// <summary>
    ///     Gets the result of something that worked.
    /// </summary>
    public static ChatHistoryResult Ok { get; } = new(true, string.Empty);

    /// <summary>
    ///     Makes the result of something that did not work.
    /// </summary>
    /// <param name="message">The sentence that says what went wrong.</param>
    /// <returns>The result.</returns>
    public static ChatHistoryResult Failed(string message) => new(false, message);
}

/// <summary>
///     Where the text of the conversation of each project is kept on the computer of the user, so that the conversation
///     is still there when the project is opened again.
/// </summary>
public interface IChatHistoryStore
{
    /// <summary>
    ///     Reads the saved history of a project.
    /// </summary>
    /// <param name="project">The open project.</param>
    /// <returns>The saved rows, oldest first; empty when nothing was saved or the file cannot be used.</returns>
    IReadOnlyList<ChatHistoryRow> Load(ProjectInfo project);

    /// <summary>
    ///     Saves the history of a project, keeping only the newest rows that fit the limits.
    /// </summary>
    /// <param name="project">The open project.</param>
    /// <param name="rows">The rows, oldest first.</param>
    /// <returns>Whether it was saved, and what went wrong when it was not.</returns>
    ChatHistoryResult Save(ProjectInfo project, IReadOnlyList<ChatHistoryRow> rows);

    /// <summary>
    ///     Deletes the saved history of a project.
    /// </summary>
    /// <param name="project">The open project.</param>
    /// <returns>Whether nothing of it is left, and what went wrong when something is.</returns>
    ChatHistoryResult Clear(ProjectInfo project);
}

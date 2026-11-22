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

using CommunityToolkit.Mvvm.ComponentModel;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     What a row of the transcript is.
/// </summary>
public enum TranscriptRowKind
{
    /// <summary>A message that the user sent.</summary>
    User,

    /// <summary>The answer of the AI copilot, or a part of it.</summary>
    Assistant,

    /// <summary>A tool that the AI copilot called, shown as one line of what it did.</summary>
    Tool,

    /// <summary>A notice or an error that the application says in the conversation.</summary>
    Notice,
}

/// <summary>
///     One row of the transcript: a sent message, a part of the answer of the AI copilot, a line about a tool it called,
///     or a notice. The panel shows the rows in the order they were added and picks the look of a row from its
///     <see cref="Kind" />.
/// </summary>
/// <remarks>
///     A row is owned by the user interface thread. A row that holds resources, such as the pictures of a sent message,
///     releases them when it is disposed, which the transcript does when it drops the row.
/// </remarks>
public abstract class TranscriptRowViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     Gets what the row is.
    /// </summary>
    public abstract TranscriptRowKind Kind { get; }

    /// <summary>
    ///     Releases what the row holds.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases what the row holds. The base implementation holds nothing.
    /// </summary>
    /// <param name="disposing"><see langword="true" /> when called from the public method, <see langword="false" /> from a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}

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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     One problem found in a kit: where it is, what is wrong, and whether it stops the kit from being used.
/// </summary>
/// <remarks>
///     The path names the property in the way a person edits the file, for example <c>palette.primary</c> or
///     <c>sizePresets[1].width</c>, so that every message can be fixed without searching. An empty path means the file as
///     a whole.
/// </remarks>
/// <param name="Path">The property that the problem is about, or an empty string for the whole file.</param>
/// <param name="Message">What is wrong and how to fix it, as a sentence without the path.</param>
/// <param name="IsError"><see langword="true" /> when the kit cannot be used; <see langword="false" /> for a warning.</param>
public sealed record StyleIssue(string Path, string Message, bool IsError)
{
    /// <summary>
    ///     Writes the issue as one line for a log or a message: the path, a colon, and the message.
    /// </summary>
    /// <returns>The text of the issue, without the path when the issue is about the whole file.</returns>
    public override string ToString() => Path.Length == 0 ? Message : $"{Path}: {Message}";
}

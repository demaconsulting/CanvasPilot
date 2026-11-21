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

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     Puts text on the clipboard of the system, for the browser of the drawing knowledge. The window of the browser
///     supplies it, because the clipboard belongs to a window; tests supply a recording one.
/// </summary>
public interface IKnowledgeClipboard
{
    /// <summary>
    ///     Replaces the contents of the clipboard with some text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>
    ///     A task whose result is <see langword="true" /> when the text is on the clipboard, and <see langword="false" />
    ///     when the clipboard cannot be reached, which can happen because another program has it locked.
    /// </returns>
    Task<bool> SetTextAsync(string text);
}

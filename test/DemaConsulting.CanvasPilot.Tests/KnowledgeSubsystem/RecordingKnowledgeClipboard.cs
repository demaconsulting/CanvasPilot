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

using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A clipboard for tests of the browser of the drawing knowledge: it records the text that is put on it and answers as
///     the test configured, so no window and no system clipboard is needed.
/// </summary>
internal sealed class RecordingKnowledgeClipboard : IKnowledgeClipboard
{
    /// <summary>
    ///     Gets the texts that were put on the clipboard, in order.
    /// </summary>
    public List<string> Texts { get; } = [];

    /// <summary>
    ///     Gets or sets whether putting text on the clipboard succeeds.
    /// </summary>
    public bool Succeeds { get; set; } = true;

    /// <summary>
    ///     Gets or sets an exception that putting text on the clipboard throws, or <see langword="null" /> for none.
    /// </summary>
    public Exception? Throws { get; set; }

    /// <inheritdoc />
    public Task<bool> SetTextAsync(string text)
    {
        if (Throws is not null)
        {
            throw Throws;
        }

        if (Succeeds)
        {
            Texts.Add(text);
        }

        return Task.FromResult(Succeeds);
    }
}

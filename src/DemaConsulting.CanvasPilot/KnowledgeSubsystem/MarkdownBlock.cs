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
///     A block of a card that has been read from Markdown: a heading, a paragraph, an item of a list, or a block of code.
///     The blocks are plain values, so <see cref="MarkdownBlocksPanel" /> can draw them with its own controls.
/// </summary>
/// <remarks>
///     The hierarchy is closed: only the blocks of this library derive from it, which is why the constructor is not
///     visible outside the assembly.
/// </remarks>
public abstract record MarkdownBlock
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MarkdownBlock" /> class; only the blocks of this library can derive from it.
    /// </summary>
    private protected MarkdownBlock()
    {
    }
}

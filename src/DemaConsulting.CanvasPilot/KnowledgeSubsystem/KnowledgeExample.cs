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
///     One example of a knowledge card: a file or a snippet of text that shows how to write something.
/// </summary>
/// <param name="Name">The name of the example, unique within the tree of its card; lower case letters, digits, and dashes.</param>
/// <param name="Kind">The kind: <c>source</c> (a file that can be drawn alone), <c>snippet</c> (a file that needs other files), or <c>command</c>.</param>
/// <param name="Language">The language of the content, for example <c>xml</c> or <c>json</c>.</param>
/// <param name="FileName">The name the content is saved under, or <see langword="null" /> for a command.</param>
/// <param name="Description">What the example shows.</param>
/// <param name="Content">The text of the example.</param>
public sealed record KnowledgeExample(
    string Name,
    string Kind,
    string Language,
    string? FileName,
    string Description,
    string Content);

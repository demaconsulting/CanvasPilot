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
///     One knowledge card: a topic that tells the drawing assistant, or the user, how to do one thing.
/// </summary>
/// <param name="Id">The identifier: the path of the card, for example <c>svg/framed-figure</c>.</param>
/// <param name="Name">The last segment of the identifier.</param>
/// <param name="ParentId">The identifier of the nearest visible card above this one, or <see langword="null" /> for a root.</param>
/// <param name="Title">The title.</param>
/// <param name="Summary">A one-line summary.</param>
/// <param name="Tags">The tags of the card.</param>
/// <param name="Aliases">The other names the card can be found by.</param>
/// <param name="Body">
///     The text of the card in Markdown, with every link written as the identifier of a card in code quotes or as an
///     example name of a card.
/// </param>
/// <param name="ChildIds">The identifiers of the visible cards below this one, in order.</param>
/// <param name="SeeAlsoIds">The identifiers of the visible related cards, in order.</param>
/// <param name="Examples">The examples of the card.</param>
public sealed record KnowledgeCard(
    string Id,
    string Name,
    string? ParentId,
    string Title,
    string Summary,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Aliases,
    string Body,
    IReadOnlyList<string> ChildIds,
    IReadOnlyList<string> SeeAlsoIds,
    IReadOnlyList<KnowledgeExample> Examples);

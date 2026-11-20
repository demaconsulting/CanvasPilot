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

using System.Collections.Immutable;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     A part of the source file that lies under a mark: a node or an edge of a diagram, a mark or an axis of a chart,
///     or an anchor of a model, with what the renderer knows about it.
/// </summary>
/// <param name="Kind">The kind of part: <c>node</c>, <c>edge</c>, <c>mark</c>, <c>axis</c>, or <c>anchor</c>.</param>
/// <param name="Id">The identifier the source gives the part, or the name of an anchor.</param>
/// <param name="Text">The text of the part, such as the label of a node, or <see langword="null" /> for none.</param>
/// <param name="Line">The 1-based line of the source file that declares the part, or <see langword="null" /> when unknown.</param>
/// <param name="Box">The bounds of the part in picture pixels, or <see langword="null" /> for an anchor and when unknown.</param>
/// <param name="Pixel">The point of an anchor in picture pixels, or a point on a part, or <see langword="null" /> when unknown.</param>
public sealed record SourceElementReference(
    string Kind,
    string Id,
    string? Text,
    int? Line,
    RenderBox? Box,
    RenderPoint? Pixel);

/// <summary>
///     The outcome of looking for the parts of the source file that lie under a mark: the parts, or the honest statement
///     that this kind of picture cannot say.
/// </summary>
public sealed class SourceElementSearchResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SourceElementSearchResult" /> class.
    /// </summary>
    /// <param name="elements">The parts under the mark, most relevant first.</param>
    /// <param name="isAvailable">Whether the picture can say which parts of its source lie where.</param>
    /// <param name="unavailableReason">Why it cannot, or <see langword="null" /> when it can.</param>
    /// <param name="isTruncated">Whether parts were left out because there were too many.</param>
    private SourceElementSearchResult(
        ImmutableArray<SourceElementReference> elements,
        bool isAvailable,
        string? unavailableReason,
        bool isTruncated)
    {
        Elements = elements;
        IsAvailable = isAvailable;
        UnavailableReason = unavailableReason;
        IsTruncated = isTruncated;
    }

    /// <summary>
    ///     Gets the parts of the source that lie under the mark, most relevant first; empty when none do and when
    ///     <see cref="IsAvailable" /> is <see langword="false" />.
    /// </summary>
    public ImmutableArray<SourceElementReference> Elements { get; }

    /// <summary>
    ///     Gets a value indicating whether the picture can say which parts of its source lie where. An empty
    ///     <see cref="Elements" /> with this set means that nothing lies under the mark; with it clear it means that the
    ///     picture cannot say.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    ///     Gets the reason the picture cannot say, a sentence fragment such as <c>svg provides no element positions</c>, or
    ///     <see langword="null" /> when it can.
    /// </summary>
    public string? UnavailableReason { get; }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Elements" /> lacks parts because there were more than the limit, or
    ///     because the renderer itself left parts out of what it reported.
    /// </summary>
    public bool IsTruncated { get; }

    /// <summary>
    ///     Makes the result of a picture that can say which parts lie under the mark.
    /// </summary>
    /// <param name="elements">The parts, most relevant first.</param>
    /// <param name="isTruncated">Whether parts were left out.</param>
    /// <returns>The result.</returns>
    internal static SourceElementSearchResult Found(IEnumerable<SourceElementReference> elements, bool isTruncated)
    {
        return new SourceElementSearchResult([.. elements], true, null, isTruncated);
    }

    /// <summary>
    ///     Makes the result of a picture that cannot say which parts lie under the mark.
    /// </summary>
    /// <param name="reason">Why it cannot.</param>
    /// <returns>The result.</returns>
    internal static SourceElementSearchResult NotAvailable(string reason)
    {
        return new SourceElementSearchResult([], false, reason, false);
    }
}

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
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.PayloadSubsystem;

/// <summary>
///     One mark of the message as the reader finds it: its number, its shape and geometry in the pixels of the picture
///     it is on, its note, a picture of the area around it, and the parts of the source file under it.
/// </summary>
/// <remarks>
///     The object is immutable: it copies the lists and the bytes it is given, so that nothing the caller does with them
///     afterwards changes it, and it may be shared between threads.
/// </remarks>
public sealed class PayloadMark
{
    /// <summary>
    ///     The bytes of the picture of the area around the mark, which the object owns.
    /// </summary>
    private readonly byte[] _cropPng;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadMark" /> class.
    /// </summary>
    /// <param name="number">The number of the mark, one or more.</param>
    /// <param name="kind">The shape of the mark.</param>
    /// <param name="points">The points of the shape in picture pixels, in the normal form of <see cref="MarkGeometry" />; copied.</param>
    /// <param name="boundingBox">The smallest rectangle in picture pixels that holds the shape.</param>
    /// <param name="note">The short note of the mark, or <see langword="null" /> for none.</param>
    /// <param name="cropPng">The PNG file of the area around the mark with all marks drawn; copied.</param>
    /// <param name="cropBox">The area in picture pixels that <paramref name="cropPng" /> shows.</param>
    /// <param name="sourceElements">The parts of the source file under the mark, most relevant first; copied.</param>
    /// <param name="sourceElementsAvailable">Whether the picture could say which parts lie under the mark.</param>
    /// <param name="sourceElementsUnavailableReason">Why it could not, or <see langword="null" /> when it could.</param>
    /// <param name="sourceElementsTruncated">Whether parts were left out of <paramref name="sourceElements" />.</param>
    /// <exception cref="ArgumentNullException">Thrown when a list is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="number" /> is less than one.</exception>
    /// <exception cref="ArgumentException">Thrown when there are fewer than two points or the crop is empty.</exception>
    public PayloadMark(
        int number,
        MarkKind kind,
        IEnumerable<RenderPoint> points,
        RenderBox boundingBox,
        string? note,
        ReadOnlyMemory<byte> cropPng,
        RenderBox cropBox,
        IEnumerable<SourceElementReference> sourceElements,
        bool sourceElementsAvailable,
        string? sourceElementsUnavailableReason,
        bool sourceElementsTruncated)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(sourceElements);
        if (cropPng.IsEmpty)
        {
            throw new ArgumentException("The picture of the area around a mark cannot be empty.", nameof(cropPng));
        }

        Number = number;
        Kind = kind;
        Points = [.. points];
        if (Points.Length < 2)
        {
            throw new ArgumentException("A mark needs at least two points.", nameof(points));
        }

        BoundingBox = boundingBox;
        Note = note;
        _cropPng = cropPng.ToArray();
        CropBox = cropBox;
        SourceElements = [.. sourceElements];
        SourceElementsAvailable = sourceElementsAvailable;
        SourceElementsUnavailableReason = sourceElementsUnavailableReason;
        SourceElementsTruncated = sourceElementsTruncated;
    }

    /// <summary>
    ///     Gets the number of the mark, which is the number it shows on the picture and in the text of the message.
    /// </summary>
    public int Number { get; }

    /// <summary>
    ///     Gets the shape of the mark.
    /// </summary>
    public MarkKind Kind { get; }

    /// <summary>
    ///     Gets the points of the shape in the pixels of the picture, with the origin at its top-left corner: two corners
    ///     for a rectangle or an ellipse, the tail and the head for an arrow, and the polyline for a freehand mark.
    /// </summary>
    public ImmutableArray<RenderPoint> Points { get; }

    /// <summary>
    ///     Gets the smallest rectangle, in picture pixels, that holds the shape.
    /// </summary>
    public RenderBox BoundingBox { get; }

    /// <summary>
    ///     Gets the short note the user wrote with the mark, or <see langword="null" /> when there is none.
    /// </summary>
    public string? Note { get; }

    /// <summary>
    ///     Gets the PNG file of the area around the mark, cut from the annotated picture, so that all marks that fall in
    ///     the area are drawn on it.
    /// </summary>
    public ReadOnlyMemory<byte> CropPng => _cropPng;

    /// <summary>
    ///     Gets the area, in picture pixels, that <see cref="CropPng" /> shows: the bounding box with a margin, at least
    ///     64 by 64 pixels, inside the picture.
    /// </summary>
    public RenderBox CropBox { get; }

    /// <summary>
    ///     Gets the parts of the source file that lie under the mark, most relevant first.
    /// </summary>
    public ImmutableArray<SourceElementReference> SourceElements { get; }

    /// <summary>
    ///     Gets a value indicating whether the picture could say which parts of its source lie under the mark. When it is
    ///     <see langword="false" />, <see cref="SourceElements" /> is empty because the picture cannot tell, not because
    ///     nothing is under the mark.
    /// </summary>
    public bool SourceElementsAvailable { get; }

    /// <summary>
    ///     Gets why the picture cannot say which parts lie under the mark, for example
    ///     <c>svg provides no element positions</c>, or <see langword="null" /> when it can.
    /// </summary>
    public string? SourceElementsUnavailableReason { get; }

    /// <summary>
    ///     Gets a value indicating whether parts were left out of <see cref="SourceElements" />.
    /// </summary>
    public bool SourceElementsTruncated { get; }
}

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
///     The area around one mark, cut from the annotated picture.
/// </summary>
/// <param name="Number">The number of the mark that the area was cut for.</param>
/// <param name="Png">The PNG file of the area.</param>
/// <param name="Box">The area in picture pixels.</param>
public sealed record MarkCrop(int Number, ReadOnlyMemory<byte> Png, RenderBox Box);

/// <summary>
///     The pictures that are made from one rendered picture and the marks that were drawn on it: the whole picture with
///     the marks drawn and numbered on it, and the area around each mark.
/// </summary>
/// <param name="AnnotatedPng">The PNG file of the whole picture with every mark drawn and numbered on it.</param>
/// <param name="Crops">The area around each mark, in the order of the marks' numbers.</param>
public sealed record MarkedPictureImages(ReadOnlyMemory<byte> AnnotatedPng, ImmutableArray<MarkCrop> Crops);

/// <summary>
///     Makes the pictures of a message that show where the user pointed: the rendered picture with the marks drawn and
///     numbered on it, and a cut-out of the area around each mark. It is a seam, so that what builds the message can be
///     tested without drawing.
/// </summary>
public interface IMarkImageComposer
{
    /// <summary>
    ///     Draws the marks on the picture and cuts out the area around each of them.
    /// </summary>
    /// <param name="picture">The picture the marks were drawn on.</param>
    /// <param name="marks">The marks to draw, all of which were drawn on exactly <paramref name="picture" />.</param>
    /// <returns>The annotated picture and one cut-out for each mark.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when a mark was drawn on another picture.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the marks cannot be drawn.</exception>
    MarkedPictureImages Compose(RenderedImage picture, IReadOnlyList<Mark> marks);
}

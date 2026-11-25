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

using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     One mark that the user drew on a picture: its number, where it is on the picture, an optional short note, and the
///     exact picture it was drawn on.
/// </summary>
/// <remarks>
///     A mark holds the <see cref="RenderedImage" /> it was drawn on, not just a name, so that the pixels the user saw,
///     the file they came from, and the fingerprints of both stay with the mark even after the file changes and the
///     preview shows a new picture. A mark never moves to another picture. Instances are immutable and safe to share
///     between threads; a changed note is a new instance with the same <see cref="Id" />, made by the
///     <see cref="MarkRegistry" />.
/// </remarks>
public sealed class Mark
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Mark" /> class.
    /// </summary>
    /// <param name="id">The identity of the mark, which its chip in the message carries.</param>
    /// <param name="number">The number the mark shows, one or more.</param>
    /// <param name="geometry">Where the mark is, in the pixels of <paramref name="snapshot" />.</param>
    /// <param name="note">The short note the user wrote with the mark, or <see langword="null" /> for none.</param>
    /// <param name="snapshot">The picture the mark was drawn on.</param>
    /// <param name="createdUtc">When the mark was made.</param>
    /// <exception cref="ArgumentNullException">Thrown when the geometry or the snapshot is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="number" /> is less than one.</exception>
    public Mark(Guid id, int number, MarkGeometry geometry, string? note, RenderedImage snapshot, DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        Id = id;
        Number = number;
        Geometry = geometry;
        Note = note;
        Snapshot = snapshot;
        CreatedUtc = createdUtc;
    }

    /// <summary>
    ///     Gets the identity of the mark, which its chip in the message carries.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    ///     Gets the number the mark shows on the picture and in the message.
    /// </summary>
    public int Number { get; }

    /// <summary>
    ///     Gets where the mark is, in the pixels of <see cref="Snapshot" />.
    /// </summary>
    public MarkGeometry Geometry { get; }

    /// <summary>
    ///     Gets the shape of the mark.
    /// </summary>
    public MarkKind Kind => Geometry.Kind;

    /// <summary>
    ///     Gets the short note the user wrote with the mark, one line without control characters, or
    ///     <see langword="null" /> when there is none.
    /// </summary>
    public string? Note { get; }

    /// <summary>
    ///     Gets the exact picture the mark was drawn on.
    /// </summary>
    public RenderedImage Snapshot { get; }

    /// <summary>
    ///     Gets when the mark was made.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }
}

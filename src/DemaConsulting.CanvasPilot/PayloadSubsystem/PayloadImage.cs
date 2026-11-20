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

namespace DemaConsulting.CanvasPilot.PayloadSubsystem;

/// <summary>
///     One picture that the message refers to, as the reader finds it: which file and which version of it, the picture
///     with the marks drawn and numbered on it, and the marks that were drawn on it.
/// </summary>
/// <remarks>
///     There is one image for each distinct picture that a mark of the message was drawn on, so a message with marks on
///     two versions of a file holds two images. The object is immutable: it copies the list and the bytes it is given and
///     may be shared between threads.
/// </remarks>
public sealed class PayloadImage
{
    /// <summary>
    ///     The bytes of the annotated picture, which the object owns.
    /// </summary>
    private readonly byte[] _annotatedPng;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadImage" /> class.
    /// </summary>
    /// <param name="sourcePath">The full path of the file the picture was drawn from.</param>
    /// <param name="sourceFileHash">The SHA-256 fingerprint of the file as it was when drawn, or <see langword="null" /> when unknown.</param>
    /// <param name="contentHash">The SHA-256 fingerprint of the pixels the user saw.</param>
    /// <param name="format">The name of the format that was drawn, for example <c>mermaid</c>.</param>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <param name="annotatedPng">The PNG file of the whole picture with every mark drawn and numbered on it; copied.</param>
    /// <param name="marks">The marks that were drawn on the picture, in the order of their numbers; copied.</param>
    /// <exception cref="ArgumentException">Thrown when a text is blank, the picture is empty, or a size is not positive.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a text or the marks are <see langword="null" />.</exception>
    public PayloadImage(
        string sourcePath,
        string? sourceFileHash,
        string contentHash,
        string format,
        int width,
        int height,
        ReadOnlyMemory<byte> annotatedPng,
        IEnumerable<PayloadMark> marks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (annotatedPng.IsEmpty)
        {
            throw new ArgumentException("The annotated picture cannot be empty.", nameof(annotatedPng));
        }

        SourcePath = sourcePath;
        SourceFileHash = sourceFileHash;
        ContentHash = contentHash;
        Format = format;
        Width = width;
        Height = height;
        _annotatedPng = annotatedPng.ToArray();
        Marks = [.. marks];
    }

    /// <summary>
    ///     Gets the full path of the file the picture was drawn from.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    ///     Gets the SHA-256 fingerprint, as lower case hexadecimal, of the bytes of the source file as they were when the
    ///     picture was drawn, or <see langword="null" /> when the file could not be read for it.
    /// </summary>
    public string? SourceFileHash { get; }

    /// <summary>
    ///     Gets the SHA-256 fingerprint, as lower case hexadecimal, of what the user saw: the size and every pixel.
    /// </summary>
    public string ContentHash { get; }

    /// <summary>
    ///     Gets the name of the format that was drawn, for example <c>mermaid</c> or <c>svg</c>.
    /// </summary>
    public string Format { get; }

    /// <summary>
    ///     Gets the width of the picture in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets the height of the picture in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    ///     Gets the PNG file of the whole picture with every mark drawn and numbered on it, in the pixels the user saw.
    /// </summary>
    public ReadOnlyMemory<byte> AnnotatedPng => _annotatedPng;

    /// <summary>
    ///     Gets the marks that were drawn on this picture, in the order of their numbers.
    /// </summary>
    public ImmutableArray<PayloadMark> Marks { get; }
}

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

using Avalonia.Media.Imaging;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     One picture of a payload as the inspector shows it: the annotated picture of a marked file, or the cut-out of the
///     area around one mark, with a caption that says what it is.
/// </summary>
/// <remarks>
///     The PNG bytes are the very bytes of the payload, so the inspector shows what the assistant would receive and not
///     a redraw of it. The picture is decoded the first time the view asks for it, which keeps the view model free of
///     the graphics platform until a window shows it. The decoded picture belongs to this object and is released by
///     <see cref="Dispose" />. The class is owned by the user interface thread.
/// </remarks>
public sealed class PayloadPictureViewModel : IDisposable
{
    /// <summary>
    ///     The PNG bytes of the picture, which are the bytes of the payload.
    /// </summary>
    private readonly ReadOnlyMemory<byte> _png;

    /// <summary>
    ///     The decoded picture, or <see langword="null" /> before it was asked for or when it cannot be decoded.
    /// </summary>
    private Bitmap? _bitmap;

    /// <summary>
    ///     Whether decoding was tried, so that a picture that cannot be decoded is not tried again on every read.
    /// </summary>
    private bool _decoded;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadPictureViewModel" /> class.
    /// </summary>
    /// <param name="caption">What the picture is, in a sentence.</param>
    /// <param name="png">The PNG file of the picture.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="caption" /> is <see langword="null" />.</exception>
    public PayloadPictureViewModel(string caption, ReadOnlyMemory<byte> png)
    {
        ArgumentNullException.ThrowIfNull(caption);

        Caption = caption;
        _png = png;
    }

    /// <summary>
    ///     Gets what the picture is, in a sentence, for example <c>Mark 2 - area 10, 20, 120 x 80 pixels</c>.
    /// </summary>
    public string Caption { get; }

    /// <summary>
    ///     Gets the number of bytes of the PNG file, as the assistant would receive them.
    /// </summary>
    public int ByteCount => _png.Length;

    /// <summary>
    ///     Gets the decoded picture, or <see langword="null" /> when the bytes are not a picture the decoder knows. It is
    ///     decoded on first use and is shown by the view but owned by this object.
    /// </summary>
    public Bitmap? Bitmap
    {
        get
        {
            if (!_decoded)
            {
                _decoded = true;
                try
                {
                    using var stream = new MemoryStream(_png.ToArray());
                    _bitmap = new Bitmap(stream);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // Bytes that cannot be decoded leave the caption alone; the inspector still shows what they are
                    _bitmap = null;
                }
            }

            return _bitmap;
        }
    }

    /// <summary>
    ///     Releases the decoded picture.
    /// </summary>
    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _decoded = true;
    }
}

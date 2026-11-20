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

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     What a drop or a paste brought to the composer, in a form that does not depend on the user interface: the paths
///     of files and folders, the bytes of pictures such as screenshots, and plain text. The view reads the drag or the
///     clipboard and hands over this; the intake decides what becomes of it.
/// </summary>
/// <remarks>
///     Everything in it is data from outside the application. It is never opened or run by the composer: paths are
///     only checked and shown, picture bytes are only saved, and text is only cleaned.
/// </remarks>
public sealed class DroppedContent
{
    /// <summary>
    ///     Gets the full paths of the files and folders that were dropped or pasted, in the order the operating
    ///     system gave them.
    /// </summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>
    ///     Gets the PNG bytes of the pictures that were pasted, such as a screenshot from the clipboard.
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> Images { get; init; } = [];

    /// <summary>
    ///     Gets the plain text that was dropped or pasted, or <see langword="null" /> when there was none.
    /// </summary>
    public string? Text { get; init; }
}

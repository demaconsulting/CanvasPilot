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

using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     Decides which files are image source files that belong in the project explorer.
/// </summary>
public interface IImageSourceFilter
{
    /// <summary>
    ///     Determines whether a file is an image source file.
    /// </summary>
    /// <param name="fileName">The file name, with or without a folder.</param>
    /// <returns><see langword="true" /> when the file is an image source file.</returns>
    bool IsImageSource(string fileName);
}

/// <summary>
///     Treats a file as an image source when its name ends with an extension that CanvasNet can draw, as listed
///     by <see cref="CanvasNetViewer.Extensions" />. Comparison ignores case, and compound extensions such as
///     <c>.vl.json</c> match as a whole.
/// </summary>
public sealed class CanvasNetImageSourceFilter : IImageSourceFilter
{
    /// <summary>
    ///     The extensions CanvasNet claims, lower case, with the leading dot.
    /// </summary>
    private readonly string[] _extensions = [.. CanvasNetViewer.Extensions];

    /// <inheritdoc />
    public bool IsImageSource(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return _extensions.Any(extension =>
            name.Length > extension.Length && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}

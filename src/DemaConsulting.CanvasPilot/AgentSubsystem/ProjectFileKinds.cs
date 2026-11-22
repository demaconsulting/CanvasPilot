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

using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Tells which files the project tools may read and write as text, and which are pictures that only
///     <c>render_image</c> can show.
/// </summary>
/// <remarks>
///     <para>
///         The files of the tools are the image sources that CanvasNet draws, which are text files (SVG, Mermaid, PlantUML,
///         OpenSCAD, Vega-Lite) or pictures and documents in a binary format (PNG, JPEG, PDF, PowerPoint, Visio), and the
///         small data files that drawings read (<c>.csv</c>, <c>.json</c>, <c>.txt</c>). A model reads and writes text, so
///         the binary image sources are left to <c>render_image</c>; writing text into a file with the name of a picture
///         would only make a broken picture.
///     </para>
///     <para>
///         The set of image sources comes from the filter of the application, which asks CanvasNet, so a format that
///         CanvasNet learns is a text file here when it is not in the list of the binary extensions. The class holds no
///         state beyond the filter and is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class ProjectFileKinds
{
    /// <summary>
    ///     The extensions of the data files that a drawing may read besides the image sources.
    /// </summary>
    internal static readonly IReadOnlyList<string> DataExtensions = [".csv", ".json", ".txt"];

    /// <summary>
    ///     The extensions of the image sources in a binary format, which are not text and so are not read or written by the
    ///     text tools.
    /// </summary>
    internal static readonly IReadOnlyList<string> BinaryExtensions =
    [
        ".bmp", ".gif", ".j2c", ".j2k", ".jp2", ".jpeg", ".jpg", ".pdf", ".png", ".pptx", ".tif", ".tiff", ".vsdx",
    ];

    /// <summary>
    ///     Decides which files are image sources.
    /// </summary>
    private readonly IImageSourceFilter _filter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectFileKinds" /> class.
    /// </summary>
    /// <param name="filter">Decides which files are image sources.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter" /> is <see langword="null" />.</exception>
    internal ProjectFileKinds(IImageSourceFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filter = filter;
    }

    /// <summary>
    ///     Tells whether CanvasNet draws a file.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns><see langword="true" /> when the file is an image source, text or binary.</returns>
    internal bool IsImageSource(string fileName)
    {
        return _filter.IsImageSource(fileName);
    }

    /// <summary>
    ///     Tells whether a file is an image source in a binary format.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns><see langword="true" /> when the file is an image source whose extension is in the binary list.</returns>
    internal bool IsBinaryImage(string fileName)
    {
        return _filter.IsImageSource(fileName) && HasExtension(fileName, BinaryExtensions);
    }

    /// <summary>
    ///     Tells whether a file is one that the text tools may read and write: a text image source or a small data file.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns><see langword="true" /> when the file is a text image source or has a data extension.</returns>
    internal bool IsTextFile(string fileName)
    {
        if (_filter.IsImageSource(fileName))
        {
            return !HasExtension(fileName, BinaryExtensions);
        }

        return HasExtension(fileName, DataExtensions);
    }

    /// <summary>
    ///     Describes the kinds of files that the text tools accept, for a message to the model.
    /// </summary>
    /// <returns>A sentence fragment such as <c>image sources (SVG, Mermaid, ...) and .csv, .json, .txt files</c>.</returns>
    internal static string DescribeTextFiles()
    {
        return "text image sources (such as .svg, .mmd, .puml, .scad, .vl.json) and data files (" + string.Join(", ", DataExtensions) + ")";
    }

    /// <summary>
    ///     Tells whether the name of a file ends with one of the extensions, ignoring case.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="extensions">The extensions, with the leading dot.</param>
    /// <returns><see langword="true" /> when the name ends with one of them.</returns>
    private static bool HasExtension(string fileName, IReadOnlyList<string> extensions)
    {
        var name = Path.GetFileName(fileName);
        return extensions.Any(extension => name.Length > extension.Length && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}

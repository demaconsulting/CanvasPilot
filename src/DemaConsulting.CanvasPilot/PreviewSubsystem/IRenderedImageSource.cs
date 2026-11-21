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

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Gives access to the picture that is currently shown, and tells when it changes. The image exporter uses
///     it so it does not depend on the preview pane itself.
/// </summary>
public interface IRenderedImageSource
{
    /// <summary>
    ///     Raised after <see cref="CurrentImage" /> changed.
    /// </summary>
    event EventHandler? CurrentImageChanged;

    /// <summary>
    ///     Gets the picture that is currently shown, or <see langword="null" /> when there is none.
    /// </summary>
    RenderedImage? CurrentImage { get; }

    /// <summary>
    ///     Gets the folder of the open project, which is the only folder a drawn file may read from, or
    ///     <see langword="null" /> when no project is open.
    /// </summary>
    /// <remarks>
    ///     A part that draws the shown file again at another size needs it, because the picture alone does not say which
    ///     folder the file was allowed to read from.
    /// </remarks>
    string? ProjectFolder { get; }
}

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
///     Draws an image source file as a picture and encodes pictures as PNG. It is the one place in the application
///     that talks to the CanvasNet viewer library, so everything else can be tested with a substitute.
/// </summary>
public interface IImageRenderService
{
    /// <summary>
    ///     Draws a file on a background thread. The file may only read other files (embedded pictures, includes,
    ///     data) from inside the project folder.
    /// </summary>
    /// <param name="filePath">The full path of the file to draw.</param>
    /// <param name="projectFolder">The project folder, which is the only folder the drawn file may read from.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>
    ///     A task with the picture, the reason the file could not be drawn, or a cancelled result. The task does
    ///     not fail for ordinary problems with the file.
    /// </returns>
    Task<RenderResult> RenderAsync(string filePath, string projectFolder, CancellationToken cancellationToken);

    /// <summary>
    ///     Encodes a picture as a PNG file image, keeping its transparency.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <returns>The bytes of a PNG file.</returns>
    byte[] EncodePng(RenderedImage image);
}

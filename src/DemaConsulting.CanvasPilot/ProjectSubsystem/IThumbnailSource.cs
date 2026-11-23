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

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     Makes the small pictures that the explorer shows at the right of the rows of the files it can draw.
/// </summary>
public interface IThumbnailSource
{
    /// <summary>
    ///     Makes the thumbnail of a file. The work is done off the user interface thread, and every call gives a new bitmap,
    ///     which the caller owns and disposes.
    /// </summary>
    /// <param name="filePath">The full path of the file.</param>
    /// <param name="projectFolder">The project folder, which is the only folder that the file may read from.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task with the bitmap, or <see langword="null" /> when the file could not be drawn.</returns>
    Task<Bitmap?> CreateAsync(string filePath, string projectFolder, CancellationToken cancellationToken);
}

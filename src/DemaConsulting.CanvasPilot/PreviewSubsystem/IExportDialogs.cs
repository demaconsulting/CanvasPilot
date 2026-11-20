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
///     The platform dialogs and clipboard that exporting the rendered image needs. The Preview subsystem owns
///     this narrow interface, so the exporter depends on nothing above it; the application's dialog service
///     extends it, so the main window supplies the implementation.
/// </summary>
public interface IExportDialogs
{
    /// <summary>
    ///     Asks the user where to save a file.
    /// </summary>
    /// <param name="title">The picker title.</param>
    /// <param name="suggestedFileName">The file name to suggest, including its extension.</param>
    /// <param name="filterName">The name of the file type shown in the picker, for example <c>PNG image</c>.</param>
    /// <param name="filterPattern">The file name pattern of the type, for example <c>*.png</c>.</param>
    /// <returns>The full path chosen, or <see langword="null" /> when cancelled.</returns>
    Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string filterName, string filterPattern);

    /// <summary>
    ///     Puts a picture on the system clipboard as an image.
    /// </summary>
    /// <param name="image">The picture to copy.</param>
    /// <returns>
    ///     <see langword="true" /> when the picture is on the clipboard; <see langword="false" /> when the
    ///     platform has no clipboard available or refused it.
    /// </returns>
    Task<bool> CopyImageToClipboardAsync(RenderedImage image);

    /// <summary>
    ///     Shows a message with an OK button.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the user has closed the message.</returns>
    Task ShowMessageAsync(string title, string message);
}

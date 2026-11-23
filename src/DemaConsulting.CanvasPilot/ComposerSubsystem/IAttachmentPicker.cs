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
///     Asks the user to choose files to attach to the message, with the picker of the platform. The attach button of the
///     chat panel asks for it, and does not know what shows it, because a picker needs the main window as its owner; the
///     main window implements it through the dialog service, and tests supply a substitute.
/// </summary>
public interface IAttachmentPicker
{
    /// <summary>
    ///     Asks the user to choose one or more files, which can be any file.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, or <see langword="null" /> for the platform default.</param>
    /// <returns>The full paths of the chosen files; empty when the user cancelled.</returns>
    Task<IReadOnlyList<string>> PickAttachmentsAsync(string? initialFolder);
}

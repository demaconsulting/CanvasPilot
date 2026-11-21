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

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     Shows the browser of the drawing knowledge to the user. The command of the Help menu asks for it and does not
///     know what shows it, because showing a window needs the main window as its owner; the main window implements it
///     through the dialog service, and tests supply a substitute.
/// </summary>
public interface IKnowledgeBrowserLauncher
{
    /// <summary>
    ///     Shows the browser in a window of its own that does not block the main window. There is one browser at a time:
    ///     when it is already open, the open window is brought to the front and the view model that was given is not used.
    /// </summary>
    /// <param name="viewModel">The view model of the browser.</param>
    /// <returns>A task that completes when the window is shown, not when it is closed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel" /> is <see langword="null" />.</exception>
    Task ShowKnowledgeBrowserAsync(KnowledgeBrowserViewModel viewModel);
}

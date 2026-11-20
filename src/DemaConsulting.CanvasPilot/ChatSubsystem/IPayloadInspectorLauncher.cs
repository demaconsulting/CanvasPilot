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

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Shows the payload inspector to the user. The chat panel asks for it when the user chooses "View payload" on a
///     sent message, and does not know what shows it, because showing a window needs the main window as its owner;
///     the main window implements it through the dialog service, and tests supply a substitute.
/// </summary>
public interface IPayloadInspectorLauncher
{
    /// <summary>
    ///     Shows the inspector modally.
    /// </summary>
    /// <param name="viewModel">The view model of the inspector, which holds what the assistant would receive.</param>
    /// <returns>A task that completes when the user has closed the inspector.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel" /> is <see langword="null" />.</exception>
    Task ShowPayloadInspectorAsync(PayloadInspectorViewModel viewModel);
}

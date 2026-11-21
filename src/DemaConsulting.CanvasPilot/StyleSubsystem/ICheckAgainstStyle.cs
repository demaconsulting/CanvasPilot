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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Checks the picture that is shown against the style kit of the open project, for the command of the Image menu. The
///     Style subsystem owns this narrow interface, so that the main window can offer the command without knowing how the
///     check is made, and so that the Style subsystem does not depend on the main window.
/// </summary>
public interface ICheckAgainstStyle
{
    /// <summary>
    ///     Raised after <see cref="CanCheck" /> may have changed. It may be raised on any thread.
    /// </summary>
    event EventHandler? CanCheckChanged;

    /// <summary>
    ///     Gets a value indicating whether there is a picture to check.
    /// </summary>
    bool CanCheck { get; }

    /// <summary>
    ///     Checks the picture that is shown against the kit that the open project uses and shows the result among the notes of
    ///     the picture. The colors are measured on a worker thread; the result is shown on the thread that the call was made
    ///     on.
    /// </summary>
    /// <returns>
    ///     A task whose result is <see langword="null" /> when the notes were shown, or a sentence to tell the user why
    ///     nothing was checked: no picture, no style kit, a kit that cannot be found, or a check that failed.
    /// </returns>
    Task<string?> RunAsync();
}

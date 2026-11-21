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

using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     The clipboard of the system as the browser of the drawing knowledge uses it: the clipboard of the window that shows
///     the browser, looked up when the text is copied because a window has it only while it is shown.
/// </summary>
internal sealed class WindowKnowledgeClipboard : IKnowledgeClipboard
{
    /// <summary>
    ///     The window whose clipboard is used.
    /// </summary>
    private readonly TopLevel _window;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WindowKnowledgeClipboard" /> class.
    /// </summary>
    /// <param name="window">The window that shows the browser.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="window" /> is <see langword="null" />.</exception>
    public WindowKnowledgeClipboard(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    /// <inheritdoc />
    public async Task<bool> SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var clipboard = _window.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        try
        {
            await clipboard.SetTextAsync(text);
            await clipboard.FlushAsync();
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or COMException or IOException)
        {
            // The platform clipboard is shared with other programs and can be locked or unavailable.
            return false;
        }
    }
}

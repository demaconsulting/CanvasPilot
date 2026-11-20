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
///     Something whose changes the user can undo and redo, such as the message being composed. The Edit menu and the
///     keyboard shortcuts reach the part of the window that has the focus through this interface, so the window does
///     not need to know what the part is.
/// </summary>
public interface IUndoRedoTarget
{
    /// <summary>
    ///     Raised when <see cref="CanUndo" /> or <see cref="CanRedo" /> may have changed, so that menu items and
    ///     buttons can update whether they are enabled.
    /// </summary>
    event EventHandler? UndoRedoStateChanged;

    /// <summary>
    ///     Gets a value indicating whether there is a step to undo.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    ///     Gets a value indicating whether there is a step to redo.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    ///     Undoes the latest step; nothing happens when <see cref="CanUndo" /> is <see langword="false" />.
    /// </summary>
    void Undo();

    /// <summary>
    ///     Redoes the step that was undone last; nothing happens when <see cref="CanRedo" /> is <see langword="false" />.
    /// </summary>
    void Redo();
}

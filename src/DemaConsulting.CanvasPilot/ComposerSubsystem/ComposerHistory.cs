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
///     The undo and redo of the composer: it applies each edit the user makes to the document, remembers it as one
///     step, and undoes and redoes whole steps. The history lives only in memory, as long as the draft; it is never
///     written anywhere, and sending the message clears it.
/// </summary>
/// <remarks>
///     <para>
///         The granularity of undo is the granularity of the edits that are executed: every executed edit is exactly one
///         step, and redo applies the very same edit again, so it has the same granularity. An edit that changes nothing
///         is not recorded. A new edit after an undo discards the steps that could have been redone, because they no
///         longer follow from what the document holds. The number of steps is limited, and the oldest step is dropped
///         when the limit is passed, so a very long draft cannot use unlimited memory.
///     </para>
///     <para>
///         The history and the editing control must not change the document in different ways: every change of the
///         document goes through <see cref="Execute" />, <see cref="Undo" />, <see cref="Redo" />, or
///         <see cref="CompleteSend" />. The type is not safe for concurrent use; the user interface thread owns it.
///     </para>
/// </remarks>
public sealed class ComposerHistory
{
    /// <summary>
    ///     The number of steps that are kept unless another limit is given.
    /// </summary>
    public const int DefaultDepthLimit = 1000;

    /// <summary>
    ///     The steps that can be undone, the oldest first.
    /// </summary>
    private readonly List<ComposerEdit> _undoSteps = [];

    /// <summary>
    ///     The steps that can be redone, the one to redo next last.
    /// </summary>
    private readonly List<ComposerEdit> _redoSteps = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerHistory" /> class over a document.
    /// </summary>
    /// <param name="document">The document whose changes the history records. Its content at this moment is the start, which cannot be undone.</param>
    /// <param name="depthLimit">The largest number of steps that can be undone, one or more.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="depthLimit" /> is less than one.</exception>
    public ComposerHistory(ComposerDocument document, int depthLimit = DefaultDepthLimit)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfLessThan(depthLimit, 1);
        Document = document;
        DepthLimit = depthLimit;
    }

    /// <summary>
    ///     Gets the document whose changes the history records.
    /// </summary>
    public ComposerDocument Document { get; }

    /// <summary>
    ///     Gets the largest number of steps that can be undone.
    /// </summary>
    public int DepthLimit { get; }

    /// <summary>
    ///     Gets the number of steps that can be undone.
    /// </summary>
    public int UndoCount => _undoSteps.Count;

    /// <summary>
    ///     Gets the number of steps that can be redone.
    /// </summary>
    public int RedoCount => _redoSteps.Count;

    /// <summary>
    ///     Gets a value indicating whether there is a step to undo.
    /// </summary>
    public bool CanUndo => _undoSteps.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether there is a step to redo.
    /// </summary>
    public bool CanRedo => _redoSteps.Count > 0;

    /// <summary>
    ///     Applies an edit to the document and records it as one step.
    /// </summary>
    /// <param name="edit">The edit, made from the document's current state.</param>
    /// <returns><see langword="true" /> when the edit was applied and recorded; <see langword="false" /> when it was empty and nothing happened.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edit" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown, without any change, when the document does not hold what the edit expects.</exception>
    public bool Execute(ComposerEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (edit.IsEmpty)
        {
            return false;
        }

        // Apply first, so that an edit that does not fit leaves both the document and the history as they were
        edit.Apply(Document);

        // A new edit ends the future of the steps that were undone, and the oldest step is dropped at the limit
        _redoSteps.Clear();
        _undoSteps.Add(edit);
        if (_undoSteps.Count > DepthLimit)
        {
            _undoSteps.RemoveAt(0);
        }

        return true;
    }

    /// <summary>
    ///     Undoes the latest step.
    /// </summary>
    /// <returns>The caret offset to restore, or <see langword="null" /> when there was nothing to undo.</returns>
    /// <exception cref="InvalidOperationException">Thrown, without any change, when the document does not hold what the step expects.</exception>
    public int? Undo()
    {
        if (_undoSteps.Count == 0)
        {
            return null;
        }

        // Revert before taking the step off, so a failed revert keeps the step
        var edit = _undoSteps[^1];
        edit.Revert(Document);
        _undoSteps.RemoveAt(_undoSteps.Count - 1);
        _redoSteps.Add(edit);
        return edit.CaretBefore;
    }

    /// <summary>
    ///     Redoes the step that was undone last, applying the same edit again.
    /// </summary>
    /// <returns>The caret offset to restore, or <see langword="null" /> when there was nothing to redo.</returns>
    /// <exception cref="InvalidOperationException">Thrown, without any change, when the document does not hold what the step expects.</exception>
    public int? Redo()
    {
        if (_redoSteps.Count == 0)
        {
            return null;
        }

        // Apply before taking the step off, so a failed apply keeps the step
        var edit = _redoSteps[^1];
        edit.Apply(Document);
        _redoSteps.RemoveAt(_redoSteps.Count - 1);
        _undoSteps.Add(edit);
        return edit.CaretAfter;
    }

    /// <summary>
    ///     Forgets every step, without changing the document.
    /// </summary>
    public void Clear()
    {
        _undoSteps.Clear();
        _redoSteps.Clear();
    }

    /// <summary>
    ///     Completes sending the message: takes the content of the document, empties the document, and forgets every
    ///     step, so that nothing of the sent message can be undone, redone, or found in the next draft.
    /// </summary>
    /// <returns>An unchangeable copy of what the document held, from which the message is built.</returns>
    public ComposerSnapshot CompleteSend()
    {
        var snapshot = Document.Snapshot();
        Document.Clear();
        Clear();
        return snapshot;
    }
}
